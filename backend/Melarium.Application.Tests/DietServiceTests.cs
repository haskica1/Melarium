using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Diets;
using Melarium.Application.Features.Diets.DTOs;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// Locks the diet state machine (NotStarted → InProgress → Completed | StoppedEarly), round
/// generation, the delete/update guards, and — since SPEC-12 — the apiary-scoped hive set: one
/// programme covers many hives, removal is soft, and a removed hive can be re-added.
/// </summary>
public class DietServiceTests
{
    private const int Apiary = 3;

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IAccessGuard _access = Substitute.For<IAccessGuard>();
    private readonly DietService _service;

    public DietServiceTests()
    {
        // Default: nothing attributed to any diet. Tests covering the cost fold set this up themselves.
        _uow.Expenses.GetTotalsByDietsAsync(Arg.Any<IEnumerable<int>>())
            .Returns(new Dictionary<int, List<(string Currency, decimal Total)>>());

        _service = new DietService(
            _uow,
            new TestCurrentUser { UserId = 1, Role = UserRole.OrganizationAdmin, OrganizationId = 7 },
            _access);
    }

    /// <summary>A service whose caller is a Beekeeper assigned to the given hives.</summary>
    private DietService BeekeeperService(params int[] assignedHiveIds)
    {
        _access.GetAssignedBeehiveIdsAsync().Returns([.. assignedHiveIds]);
        _access.GetAssignedApiaryIdsAsync().Returns([Apiary]);
        return new DietService(
            _uow,
            new TestCurrentUser { UserId = 2, Role = UserRole.Beekeeper, OrganizationId = 7 },
            _access);
    }

    private Diet? _savedDiet;

    /// <summary>Wires apiary/hive lookups + AddAsync + GetWithEntriesAsync so save-then-reload works.</summary>
    private void WireCreatePipeline(params int[] apiaryHiveIds)
    {
        if (apiaryHiveIds.Length == 0) apiaryHiveIds = [10, 11, 12];

        _uow.Apiaries.ExistsAsync(Apiary).Returns(true);
        _uow.Beehives.GetByApiaryIdAsync(Apiary)
            .Returns(apiaryHiveIds.Select(id => new Beehive { Id = id, ApiaryId = Apiary, Name = $"Košnica {id}" }));
        _uow.Diets.AddAsync(Arg.Do<Diet>(d => _savedDiet = d)).Returns(ci => ci.Arg<Diet>());
        _uow.Diets.GetWithEntriesAsync(Arg.Any<int>()).Returns(_ => _savedDiet);
    }

    private static CreateDietDto NewDto(
        DateTime startDate, int durationDays = 10, int frequencyDays = 2, params int[] beehiveIds) => new()
    {
        Name          = "Zimsko hranjenje",
        ApiaryId      = Apiary,
        BeehiveIds    = beehiveIds.Length > 0 ? [.. beehiveIds] : [10],
        StartDate     = startDate,
        Reason        = DietReason.WinterFeeding,
        DurationDays  = durationDays,
        FrequencyDays = frequencyDays,
        FoodType      = FoodType.SugarSyrup,
    };

    private static Diet DietOn(DietStatus status, params int[] activeHiveIds)
    {
        var diet = new Diet { Id = 5, ApiaryId = Apiary, Status = status, StartDate = DateTime.UtcNow.Date };
        foreach (var id in activeHiveIds)
            diet.Beehives.Add(new DietBeehive { DietId = 5, BeehiveId = id });
        return diet;
    }

    // ── CreateAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_StartDateToday_IsInProgress()
    {
        WireCreatePipeline();

        var result = await _service.CreateAsync(NewDto(DateTime.UtcNow.Date));

        Assert.Equal(DietStatus.InProgress, result.Status);
    }

    [Fact]
    public async Task Create_FutureStartDate_IsNotStarted()
    {
        WireCreatePipeline();

        var result = await _service.CreateAsync(NewDto(DateTime.UtcNow.Date.AddDays(3)));

        Assert.Equal(DietStatus.NotStarted, result.Status);
    }

    [Fact]
    public async Task Create_ManyHives_ProducesOneDietWithOneRoundSetAndOneLinkPerHive()
    {
        // The whole point of SPEC-12: three hives means one programme, not three.
        WireCreatePipeline(10, 11, 12);
        var start = DateTime.UtcNow.Date.AddDays(1);

        var result = await _service.CreateAsync(NewDto(start, 10, 2, 10, 11, 12));

        await _uow.Diets.Received(1).AddAsync(Arg.Any<Diet>());
        Assert.NotNull(_savedDiet);
        Assert.Equal(3, _savedDiet!.Beehives.Count);
        Assert.Equal([10, 11, 12], _savedDiet.Beehives.Select(b => b.BeehiveId).OrderBy(x => x).ToArray());
        Assert.All(_savedDiet.Beehives, b => Assert.Null(b.RemovedOn));
        Assert.Equal(5, result.TotalEntries);   // 10 / 2, once — not once per hive
        Assert.Equal(3, result.HiveCount);
    }

    [Fact]
    public async Task Create_GeneratesEntries_DurationDividedByFrequency()
    {
        WireCreatePipeline();
        var start = DateTime.UtcNow.Date.AddDays(1);

        var result = await _service.CreateAsync(NewDto(start, durationDays: 10, frequencyDays: 2));

        Assert.Equal(5, result.TotalEntries);
        Assert.NotNull(_savedDiet);
        Assert.Equal(
            [start, start.AddDays(2), start.AddDays(4), start.AddDays(6), start.AddDays(8)],
            _savedDiet!.FeedingEntries.Select(e => e.ScheduledDate).OrderBy(d => d).ToArray());
        Assert.All(_savedDiet.FeedingEntries, e => Assert.Equal(FeedingEntryStatus.Pending, e.Status));
    }

    [Fact]
    public async Task Create_FrequencyLongerThanDuration_StillGeneratesOneEntry()
    {
        WireCreatePipeline();

        var result = await _service.CreateAsync(NewDto(DateTime.UtcNow.Date, durationDays: 3, frequencyDays: 7));

        Assert.Equal(1, result.TotalEntries);
    }

    [Fact]
    public async Task Create_HiveFromAnotherApiary_ThrowsAndSavesNothing()
    {
        WireCreatePipeline(10, 11);   // 99 belongs to someone else

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateAsync(NewDto(DateTime.UtcNow.Date, 10, 2, 10, 99)));
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Create_AccessDenied_ThrowsAndSavesNothing()
    {
        WireCreatePipeline();
        _access.When(a => a.EnsureCanManageApiaryAsync(Apiary))
               .Do(_ => throw new ForbiddenAccessException());

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            _service.CreateAsync(NewDto(DateTime.UtcNow.Date)));
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Create_UnknownApiary_Throws()
    {
        _uow.Apiaries.ExistsAsync(Apiary).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CreateAsync(NewDto(DateTime.UtcNow.Date)));
    }

    // ── Hive set: add / remove / re-add ────────────────────────────────────────

    [Fact]
    public async Task AddBeehives_AddsLinkForEachNewHive()
    {
        var diet = DietOn(DietStatus.InProgress, 10);
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);
        _uow.Beehives.GetByApiaryIdAsync(Apiary)
            .Returns([new Beehive { Id = 10, ApiaryId = Apiary }, new Beehive { Id = 11, ApiaryId = Apiary }]);

        var result = await _service.AddBeehivesAsync(5, new AddDietBeehivesDto { BeehiveIds = [11] });

        Assert.Equal(2, result.HiveCount);
        Assert.Contains(diet.Beehives, b => b.BeehiveId == 11 && b.RemovedOn == null);
    }

    [Fact]
    public async Task AddBeehives_HiveAlreadyOnProgramme_IsNoOp()
    {
        var diet = DietOn(DietStatus.InProgress, 10);
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);
        _uow.Beehives.GetByApiaryIdAsync(Apiary).Returns([new Beehive { Id = 10, ApiaryId = Apiary }]);

        var result = await _service.AddBeehivesAsync(5, new AddDietBeehivesDto { BeehiveIds = [10] });

        Assert.Single(diet.Beehives);
        Assert.Equal(1, result.HiveCount);
    }

    [Theory]
    [InlineData(DietStatus.Completed)]
    [InlineData(DietStatus.StoppedEarly)]
    public async Task AddBeehives_FinishedDiet_Throws(DietStatus status)
    {
        _uow.Diets.GetWithEntriesAsync(5).Returns(DietOn(status, 10));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.AddBeehivesAsync(5, new AddDietBeehivesDto { BeehiveIds = [11] }));
    }

    [Fact]
    public async Task RemoveBeehive_IsSoft_AndDropsOutOfTheCount()
    {
        var diet = DietOn(DietStatus.InProgress, 10, 11);
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);

        var result = await _service.RemoveBeehiveAsync(5, 11);

        Assert.Equal(2, diet.Beehives.Count);                                   // history survives
        Assert.NotNull(diet.Beehives.Single(b => b.BeehiveId == 11).RemovedOn);
        Assert.Equal(1, result.HiveCount);                                      // but is not counted
        Assert.Equal(2, result.Beehives.Count);                                 // and is still listed
    }

    [Fact]
    public async Task RemoveBeehive_ThenReAdd_CreatesASecondLinkAndCountIsOne()
    {
        // Why the unique index is partial: a plain one would make re-adding impossible forever.
        var diet = DietOn(DietStatus.InProgress, 10);
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);
        _uow.Beehives.GetByApiaryIdAsync(Apiary).Returns([new Beehive { Id = 10, ApiaryId = Apiary }]);

        await _service.RemoveBeehiveAsync(5, 10);
        var result = await _service.AddBeehivesAsync(5, new AddDietBeehivesDto { BeehiveIds = [10] });

        Assert.Equal(2, diet.Beehives.Count);
        Assert.Single(diet.Beehives, b => b.RemovedOn == null);
        Assert.Equal(1, result.HiveCount);
    }

    [Fact]
    public async Task RemoveBeehive_NotOnProgramme_Throws()
    {
        _uow.Diets.GetWithEntriesAsync(5).Returns(DietOn(DietStatus.InProgress, 10));

        await Assert.ThrowsAsync<NotFoundException>(() => _service.RemoveBeehiveAsync(5, 99));
    }

    [Fact]
    public async Task RemoveBeehive_LastHive_IsAllowedAndDoesNotStopTheProgramme()
    {
        var diet = DietOn(DietStatus.InProgress, 10);
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);

        var result = await _service.RemoveBeehiveAsync(5, 10);

        Assert.Equal(0, result.HiveCount);
        Assert.Equal(DietStatus.InProgress, diet.Status);   // stopping is an explicit, commented act
    }

    // ── Authorization (SPEC-12 role matrix) ────────────────────────────────────

    [Fact]
    public async Task Beekeeper_CannotCreate()
    {
        WireCreatePipeline();
        var service = BeekeeperService(10);
        _access.When(a => a.EnsureCanManageApiaryAsync(Apiary))
               .Do(_ => throw new ForbiddenAccessException());

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            service.CreateAsync(NewDto(DateTime.UtcNow.Date)));
    }

    [Fact]
    public async Task Beekeeper_CannotStopEarly()
    {
        _uow.Diets.GetWithEntriesAsync(5).Returns(DietOn(DietStatus.InProgress, 10));
        var service = BeekeeperService(10);
        _access.When(a => a.EnsureCanManageApiaryAsync(Apiary))
               .Do(_ => throw new ForbiddenAccessException());

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            service.CompleteEarlyAsync(5, new CompleteEarlyDto { Comment = "gotovo" }));
    }

    [Fact]
    public async Task Beekeeper_NotOnAnyActiveHive_CannotRead()
    {
        _uow.Diets.GetWithEntriesAsync(5).Returns(DietOn(DietStatus.InProgress, 11, 12));
        var service = BeekeeperService(10);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.GetByIdAsync(5));
    }

    [Fact]
    public async Task Beekeeper_OnlyOnARemovedHive_CannotRead()
    {
        var diet = DietOn(DietStatus.InProgress, 10, 11);
        diet.Beehives.Single(b => b.BeehiveId == 10).RemovedOn = DateTime.UtcNow.Date;
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);
        var service = BeekeeperService(10);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.GetByIdAsync(5));
    }

    [Fact]
    public async Task Beekeeper_TicksRoundForTheWholeGroup_IncludingHivesNotTheirs()
    {
        // Decided in SPEC-12: one tick closes the round for every hive on the programme. Feeding is
        // field work the Beekeeper performs; making them ask an admin to record it would be worse.
        var diet = DietWithEntries(DietStatus.InProgress, FeedingEntryStatus.Pending, FeedingEntryStatus.Pending);
        diet.Beehives.Add(new DietBeehive { DietId = 5, BeehiveId = 11 });   // not assigned to them
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);
        var service = BeekeeperService(10);

        await service.CompleteFeedingEntryAsync(5, 100, new CompleteFeedingEntryDto());

        Assert.Equal(FeedingEntryStatus.Completed, diet.FeedingEntries.Single(e => e.Id == 100).Status);
    }

    // ── UpdateAsync guards ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(DietStatus.Completed)]
    [InlineData(DietStatus.StoppedEarly)]
    public async Task Update_FinishedDiet_Throws(DietStatus status)
    {
        _uow.Diets.GetWithEntriesAsync(5).Returns(DietOn(status, 10));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.UpdateAsync(5, new UpdateDietDto { Name = "X", StartDate = DateTime.UtcNow, DurationDays = 5, FrequencyDays = 1 }));
    }

    [Fact]
    public async Task Update_DoesNotTouchTheHiveSet()
    {
        var diet = DietOn(DietStatus.InProgress, 10, 11);
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);

        var result = await _service.UpdateAsync(5, new UpdateDietDto
        {
            Name = "Novo ime", StartDate = DateTime.UtcNow.Date, DurationDays = 4, FrequencyDays = 2,
            Reason = DietReason.WinterFeeding, FoodType = FoodType.SugarSyrup,
        });

        Assert.Equal(2, result.HiveCount);
        Assert.Equal([10, 11], diet.Beehives.Select(b => b.BeehiveId).OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task Update_StoresAmountNumberUnitAndNote()
    {
        var diet = DietOn(DietStatus.InProgress, 10);
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);

        var result = await _service.UpdateAsync(5, new UpdateDietDto
        {
            Name = "Zimsko", StartDate = DateTime.UtcNow.Date, DurationDays = 4, FrequencyDays = 2,
            Reason = DietReason.WinterFeeding, FoodType = FoodType.SugarSyrup,
            AmountPerHive = 1.5m, AmountUnit = FeedingAmountUnit.Litre, AmountNote = "1:1",
        });

        Assert.Equal(1.5m, result.AmountPerHive);
        Assert.Equal(FeedingAmountUnit.Litre, result.AmountUnit);
        Assert.Equal("L", result.AmountUnitName);
        Assert.Equal("1:1", result.AmountNote);
    }

    // ── Feeding cost (SPEC-12 Phase E) ─────────────────────────────────────────

    [Fact]
    public async Task GetById_PlannedAmount_IsExactPerRoundDate_NotFromTodaysHiveCount()
    {
        // Dates are independent of "today" on purpose — CreatedAt/RemovedOn are only ever compared
        // against ScheduledDate, never against DateTime.UtcNow, so this is deterministic regardless
        // of when the test runs.
        var start = new DateTime(2026, 1, 1);
        var diet = new Diet
        {
            Id = 5, ApiaryId = Apiary, Status = DietStatus.InProgress, StartDate = start,
            AmountPerHive = 1m, AmountUnit = FeedingAmountUnit.Litre,
        };
        for (var h = 0; h < 10; h++)
            diet.Beehives.Add(new DietBeehive { DietId = 5, BeehiveId = 100 + h, CreatedAt = start.AddDays(-10) });
        for (var i = 0; i < 12; i++)
            diet.FeedingEntries.Add(new FeedingEntry
            {
                Id = i, DietId = 5, ScheduledDate = start.AddDays(i * 4), Status = FeedingEntryStatus.Pending,
            });
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);

        var before = await _service.GetByIdAsync(5);
        Assert.Equal(120m, before.PlannedAmount);

        // Removed right after round 6 (0-based index 5): still fed on round 6 itself (RemovedOn ==
        // that round's date counts as fed), but gone for the remaining 6 rounds — 6 fewer hive-rounds.
        diet.Beehives.First().RemovedOn = start.AddDays(5 * 4);

        var after = await _service.GetByIdAsync(5);
        Assert.Equal(114m, after.PlannedAmount);
    }

    [Fact]
    public async Task GetById_NoAmountRecorded_PlannedAndConsumedAreNull()
    {
        var diet = DietOn(DietStatus.InProgress, 10); // AmountPerHive left unset
        diet.FeedingEntries.Add(new FeedingEntry { Id = 1, DietId = 5, ScheduledDate = diet.StartDate, Status = FeedingEntryStatus.Pending });
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);

        var result = await _service.GetByIdAsync(5);

        Assert.Null(result.PlannedAmount);
        Assert.Null(result.ConsumedAmount);
    }

    [Fact]
    public async Task Beekeeper_ReadingADiet_GetsCostFieldsNull_ButConsumptionPresent()
    {
        var diet = DietOn(DietStatus.InProgress, 10);
        diet.AmountPerHive = 1.5m;
        diet.AmountUnit = FeedingAmountUnit.Litre;
        diet.FeedingEntries.Add(new FeedingEntry { Id = 1, DietId = 5, ScheduledDate = diet.StartDate, Status = FeedingEntryStatus.Pending });
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);
        // Money exists for this diet — but a Beekeeper must never see it, server-side, not just hidden.
        _uow.Expenses.GetTotalsByDietsAsync(Arg.Any<IEnumerable<int>>())
            .Returns(new Dictionary<int, List<(string Currency, decimal Total)>> { [5] = [("BAM", 40m)] });

        var service = BeekeeperService(10);
        var result = await service.GetByIdAsync(5);

        Assert.NotNull(result.PlannedAmount);
        Assert.Null(result.CostTotals);
        Assert.Null(result.CostPerHive);
    }

    [Fact]
    public async Task Manager_ReadingADiet_SeesCostTotalsAndPerHive_WhenOneCurrencyAttributed()
    {
        var diet = DietOn(DietStatus.InProgress, 10, 11); // hiveCount = 2
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);
        _uow.Expenses.GetTotalsByDietsAsync(Arg.Any<IEnumerable<int>>())
            .Returns(new Dictionary<int, List<(string Currency, decimal Total)>> { [5] = [("BAM", 40m)] });

        var result = await _service.GetByIdAsync(5);

        Assert.Equal("BAM", Assert.Single(result.CostTotals!).Currency);
        Assert.Equal(40m, Assert.Single(result.CostTotals!).Total);
        Assert.Equal(20m, result.CostPerHive); // 40 / 2 hives
    }

    [Fact]
    public async Task Manager_ReadingADiet_NoCostPerHive_WhenMultipleCurrenciesAttributed()
    {
        // Summing BAM and EUR into one number would be a silent lie, so per-hive cost is withheld
        // whenever more than one currency is involved — the totals themselves still show both.
        var diet = DietOn(DietStatus.InProgress, 10);
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);
        _uow.Expenses.GetTotalsByDietsAsync(Arg.Any<IEnumerable<int>>())
            .Returns(new Dictionary<int, List<(string Currency, decimal Total)>> { [5] = [("BAM", 40m), ("EUR", 5m)] });

        var result = await _service.GetByIdAsync(5);

        Assert.Equal(2, result.CostTotals!.Count);
        Assert.Null(result.CostPerHive);
    }

    // ── CompleteEarlyAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task CompleteEarly_RequiresComment()
    {
        _uow.Diets.GetWithEntriesAsync(5).Returns(DietOn(DietStatus.InProgress, 10));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.CompleteEarlyAsync(5, new CompleteEarlyDto { Comment = "  " }));
    }

    [Fact]
    public async Task CompleteEarly_AlreadyFinished_Throws()
    {
        _uow.Diets.GetWithEntriesAsync(5).Returns(DietOn(DietStatus.Completed, 10));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.CompleteEarlyAsync(5, new CompleteEarlyDto { Comment = "roj je uginuo" }));
    }

    [Fact]
    public async Task CompleteEarly_SetsStoppedEarlyAndComment()
    {
        var diet = DietOn(DietStatus.InProgress, 10);
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);

        var result = await _service.CompleteEarlyAsync(5, new CompleteEarlyDto { Comment = "dovoljno hrane" });

        Assert.Equal(DietStatus.StoppedEarly, diet.Status);
        Assert.Equal("dovoljno hrane", diet.EarlyCompletionComment);
        Assert.Equal(DietStatus.StoppedEarly, result.Status);
    }

    // ── CompleteFeedingEntryAsync ──────────────────────────────────────────────

    private static Diet DietWithEntries(DietStatus status, params FeedingEntryStatus[] entryStatuses)
    {
        var diet = DietOn(status, 10);
        for (var i = 0; i < entryStatuses.Length; i++)
        {
            diet.FeedingEntries.Add(new FeedingEntry
            {
                Id            = 100 + i,
                DietId        = 5,
                Status        = entryStatuses[i],
                ScheduledDate = diet.StartDate.AddDays(i),
            });
        }
        return diet;
    }

    [Fact]
    public async Task CompleteEntry_LastPendingEntry_CompletesDiet()
    {
        var diet = DietWithEntries(DietStatus.InProgress, FeedingEntryStatus.Completed, FeedingEntryStatus.Pending);
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);

        await _service.CompleteFeedingEntryAsync(5, 101, new CompleteFeedingEntryDto());

        Assert.Equal(DietStatus.Completed, diet.Status);
        Assert.All(diet.FeedingEntries, e => Assert.Equal(FeedingEntryStatus.Completed, e.Status));
    }

    [Fact]
    public async Task CompleteEntry_OtherEntriesRemain_MovesNotStartedToInProgress()
    {
        var diet = DietWithEntries(DietStatus.NotStarted, FeedingEntryStatus.Pending, FeedingEntryStatus.Pending);
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);

        await _service.CompleteFeedingEntryAsync(5, 100, new CompleteFeedingEntryDto());

        Assert.Equal(DietStatus.InProgress, diet.Status);
    }

    [Fact]
    public async Task CompleteEntry_StoresTheNote()
    {
        var diet = DietWithEntries(DietStatus.InProgress, FeedingEntryStatus.Pending, FeedingEntryStatus.Pending);
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);

        var result = await _service.CompleteFeedingEntryAsync(
            5, 100, new CompleteFeedingEntryDto { Note = "košnice 7 i 12 preskočene" });

        Assert.Equal("košnice 7 i 12 preskočene", result.Note);
    }

    [Fact]
    public async Task CompleteEntry_BlankNote_StoresNull()
    {
        var diet = DietWithEntries(DietStatus.InProgress, FeedingEntryStatus.Pending, FeedingEntryStatus.Pending);
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);

        var result = await _service.CompleteFeedingEntryAsync(5, 100, new CompleteFeedingEntryDto { Note = "   " });

        Assert.Null(result.Note);
    }

    [Fact]
    public async Task CompleteEntry_AlreadyCompleted_Throws()
    {
        var diet = DietWithEntries(DietStatus.InProgress, FeedingEntryStatus.Completed, FeedingEntryStatus.Pending);
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.CompleteFeedingEntryAsync(5, 100, new CompleteFeedingEntryDto()));
    }

    [Fact]
    public async Task CompleteEntry_OnFinishedDiet_Throws()
    {
        var diet = DietWithEntries(DietStatus.StoppedEarly, FeedingEntryStatus.Pending);
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.CompleteFeedingEntryAsync(5, 100, new CompleteFeedingEntryDto()));
    }

    [Fact]
    public async Task CompleteEntry_UnknownEntry_Throws()
    {
        var diet = DietWithEntries(DietStatus.InProgress, FeedingEntryStatus.Pending);
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CompleteFeedingEntryAsync(5, 999, new CompleteFeedingEntryDto()));
    }

    // ── DeleteAsync guards ─────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_AlreadyStarted_Throws()
    {
        var diet = DietOn(DietStatus.InProgress, 10);
        diet.StartDate = DateTime.UtcNow.Date.AddDays(-1);
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.DeleteAsync(5));
    }

    [Fact]
    public async Task Delete_HasCompletedEntries_Throws()
    {
        var diet = DietWithEntries(DietStatus.NotStarted, FeedingEntryStatus.Completed);
        diet.StartDate = DateTime.UtcNow.Date.AddDays(5);
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.DeleteAsync(5));
    }

    [Fact]
    public async Task Delete_FutureUnstartedDiet_Deletes()
    {
        var diet = DietWithEntries(DietStatus.NotStarted, FeedingEntryStatus.Pending);
        diet.StartDate = DateTime.UtcNow.Date.AddDays(5);
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);

        await _service.DeleteAsync(5);

        await _uow.Diets.Received(1).DeleteAsync(diet);
        await _uow.Received(1).SaveChangesAsync();
    }
}
