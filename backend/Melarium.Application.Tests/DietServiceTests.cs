using AutoMapper;
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
/// Locks the diet state machine: NotStarted → InProgress → Completed | StoppedEarly,
/// entry generation (duration/frequency), and the delete/update guards.
/// </summary>
public class DietServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IAccessGuard _access = Substitute.For<IAccessGuard>();
    private readonly DietService _service;

    public DietServiceTests()
    {
        _service = new DietService(
            _uow,
            Substitute.For<IMapper>(),
            new TestCurrentUser { UserId = 1, Role = UserRole.OrganizationAdmin, OrganizationId = 7 },
            _access);
    }

    private Diet? _savedDiet;

    /// <summary>Wires AddAsync + GetWithEntriesAsync so CreateAsync's save-then-reload works.</summary>
    private void WireCreatePipeline()
    {
        _uow.Beehives.ExistsAsync(10).Returns(true);
        _uow.Diets.AddAsync(Arg.Do<Diet>(d => _savedDiet = d)).Returns(ci => ci.Arg<Diet>());
        _uow.Diets.GetWithEntriesAsync(Arg.Any<int>()).Returns(_ => _savedDiet);
    }

    private static CreateDietDto NewDto(DateTime startDate, int durationDays = 10, int frequencyDays = 2) => new()
    {
        Name          = "Zimsko hranjenje",
        BeehiveId     = 10,
        StartDate     = startDate,
        Reason        = DietReason.WinterFeeding,
        DurationDays  = durationDays,
        FrequencyDays = frequencyDays,
        FoodType      = FoodType.SugarSyrup,
    };

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

    // ── CopyToBeehivesAsync ────────────────────────────────────────────────────

    private static Diet SourceDiet(DateTime start, int duration = 10, int frequency = 2, int beehiveId = 10) => new()
    {
        Id            = 1,
        Name          = "Zimsko hranjenje",
        BeehiveId     = beehiveId,
        StartDate     = start,
        Reason        = DietReason.WinterFeeding,
        DurationDays  = duration,
        FrequencyDays = frequency,
        FoodType      = FoodType.SugarSyrup,
        Status        = DietStatus.InProgress,
    };

    /// <summary>Wires the source load + captures every diet handed to AddAsync.</summary>
    private List<Diet> WireCopyPipeline(Diet source)
    {
        _uow.Diets.GetWithEntriesAsync(source.Id).Returns(source);
        var added = new List<Diet>();
        _uow.Diets.AddAsync(Arg.Do<Diet>(d => added.Add(d))).Returns(ci => ci.Arg<Diet>());
        return added;
    }

    [Fact]
    public async Task Copy_CreatesOneDietPerTarget_WithFreshPendingEntries()
    {
        var source = SourceDiet(DateTime.UtcNow.Date.AddDays(1), duration: 10, frequency: 2);
        var added = WireCopyPipeline(source);
        _uow.Beehives.ExistsAsync(11).Returns(true);
        _uow.Beehives.ExistsAsync(12).Returns(true);

        var result = await _service.CopyToBeehivesAsync(1, new CopyDietDto { TargetBeehiveIds = [11, 12] });

        Assert.Equal(2, added.Count);
        Assert.Equal([11, 12], added.Select(d => d.BeehiveId).OrderBy(x => x).ToArray());
        Assert.All(added, d => Assert.Equal(5, d.FeedingEntries.Count));
        Assert.All(added, d => Assert.All(d.FeedingEntries, e => Assert.Equal(FeedingEntryStatus.Pending, e.Status)));
        Assert.Equal(2, result.Count());
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Copy_PreservesSourceDefinitionAndStartDate()
    {
        var start = DateTime.UtcNow.Date.AddDays(-3);
        var source = SourceDiet(start, duration: 8, frequency: 4);
        var added = WireCopyPipeline(source);
        _uow.Beehives.ExistsAsync(11).Returns(true);

        await _service.CopyToBeehivesAsync(1, new CopyDietDto { TargetBeehiveIds = [11] });

        var copy = Assert.Single(added);
        Assert.Equal(source.Name, copy.Name);
        Assert.Equal(source.StartDate, copy.StartDate);
        Assert.Equal(source.Reason, copy.Reason);
        Assert.Equal(source.FoodType, copy.FoodType);
        Assert.Equal(source.DurationDays, copy.DurationDays);
        Assert.Equal(source.FrequencyDays, copy.FrequencyDays);
        Assert.Equal(2, copy.FeedingEntries.Count); // 8 / 4
    }

    [Fact]
    public async Task Copy_ExcludesSourceHiveAndDeduplicatesTargets()
    {
        var source = SourceDiet(DateTime.UtcNow.Date, beehiveId: 10);
        var added = WireCopyPipeline(source);
        _uow.Beehives.ExistsAsync(11).Returns(true);

        await _service.CopyToBeehivesAsync(1, new CopyDietDto { TargetBeehiveIds = [10, 11, 11] });

        var copy = Assert.Single(added); // 10 == source (skipped), 11 de-duplicated
        Assert.Equal(11, copy.BeehiveId);
    }

    [Fact]
    public async Task Copy_OnlySourceHiveInTargets_ThrowsAndSavesNothing()
    {
        var source = SourceDiet(DateTime.UtcNow.Date, beehiveId: 10);
        WireCopyPipeline(source);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.CopyToBeehivesAsync(1, new CopyDietDto { TargetBeehiveIds = [10] }));
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Copy_TargetAccessDenied_ThrowsAndSavesNothing()
    {
        var source = SourceDiet(DateTime.UtcNow.Date);
        WireCopyPipeline(source);
        _uow.Beehives.ExistsAsync(11).Returns(true);
        _uow.Beehives.ExistsAsync(12).Returns(true);
        _access.When(a => a.EnsureCanAccessBeehiveAsync(12))
               .Do(_ => throw new ForbiddenAccessException());

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            _service.CopyToBeehivesAsync(1, new CopyDietDto { TargetBeehiveIds = [11, 12] }));
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Copy_UnknownTargetHive_Throws()
    {
        var source = SourceDiet(DateTime.UtcNow.Date);
        WireCopyPipeline(source);
        _uow.Beehives.ExistsAsync(11).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CopyToBeehivesAsync(1, new CopyDietDto { TargetBeehiveIds = [11] }));
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Copy_UnknownSourceDiet_Throws()
    {
        _uow.Diets.GetWithEntriesAsync(99).Returns((Diet?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CopyToBeehivesAsync(99, new CopyDietDto { TargetBeehiveIds = [11] }));
    }

    // ── UpdateAsync guards ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(DietStatus.Completed)]
    [InlineData(DietStatus.StoppedEarly)]
    public async Task Update_FinishedDiet_Throws(DietStatus status)
    {
        _uow.Diets.GetWithEntriesAsync(5).Returns(new Diet { Id = 5, BeehiveId = 10, Status = status });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.UpdateAsync(5, new UpdateDietDto { Name = "X", StartDate = DateTime.UtcNow, DurationDays = 5, FrequencyDays = 1 }));
    }

    // ── CompleteEarlyAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task CompleteEarly_RequiresComment()
    {
        _uow.Diets.GetWithEntriesAsync(5).Returns(new Diet { Id = 5, BeehiveId = 10, Status = DietStatus.InProgress });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.CompleteEarlyAsync(5, new CompleteEarlyDto { Comment = "  " }));
    }

    [Fact]
    public async Task CompleteEarly_AlreadyFinished_Throws()
    {
        _uow.Diets.GetWithEntriesAsync(5).Returns(new Diet { Id = 5, BeehiveId = 10, Status = DietStatus.Completed });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.CompleteEarlyAsync(5, new CompleteEarlyDto { Comment = "roj je uginuo" }));
    }

    [Fact]
    public async Task CompleteEarly_SetsStoppedEarlyAndComment()
    {
        var diet = new Diet { Id = 5, BeehiveId = 10, Status = DietStatus.InProgress };
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);

        var result = await _service.CompleteEarlyAsync(5, new CompleteEarlyDto { Comment = "dovoljno hrane" });

        Assert.Equal(DietStatus.StoppedEarly, diet.Status);
        Assert.Equal("dovoljno hrane", diet.EarlyCompletionComment);
        Assert.Equal(DietStatus.StoppedEarly, result.Status);
    }

    // ── CompleteFeedingEntryAsync ──────────────────────────────────────────────

    private static Diet DietWithEntries(DietStatus status, params FeedingEntryStatus[] entryStatuses)
    {
        var diet = new Diet { Id = 5, BeehiveId = 10, Status = status, StartDate = DateTime.UtcNow.Date };
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

        await _service.CompleteFeedingEntryAsync(5, entryId: 101);

        Assert.Equal(DietStatus.Completed, diet.Status);
        Assert.All(diet.FeedingEntries, e => Assert.Equal(FeedingEntryStatus.Completed, e.Status));
    }

    [Fact]
    public async Task CompleteEntry_OtherEntriesRemain_MovesNotStartedToInProgress()
    {
        var diet = DietWithEntries(DietStatus.NotStarted, FeedingEntryStatus.Pending, FeedingEntryStatus.Pending);
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);

        await _service.CompleteFeedingEntryAsync(5, entryId: 100);

        Assert.Equal(DietStatus.InProgress, diet.Status);
    }

    [Fact]
    public async Task CompleteEntry_AlreadyCompleted_Throws()
    {
        var diet = DietWithEntries(DietStatus.InProgress, FeedingEntryStatus.Completed, FeedingEntryStatus.Pending);
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CompleteFeedingEntryAsync(5, entryId: 100));
    }

    [Fact]
    public async Task CompleteEntry_OnFinishedDiet_Throws()
    {
        var diet = DietWithEntries(DietStatus.StoppedEarly, FeedingEntryStatus.Pending);
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CompleteFeedingEntryAsync(5, entryId: 100));
    }

    [Fact]
    public async Task CompleteEntry_UnknownEntry_Throws()
    {
        var diet = DietWithEntries(DietStatus.InProgress, FeedingEntryStatus.Pending);
        _uow.Diets.GetWithEntriesAsync(5).Returns(diet);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.CompleteFeedingEntryAsync(5, entryId: 999));
    }

    // ── DeleteAsync guards ─────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_AlreadyStarted_Throws()
    {
        var diet = new Diet { Id = 5, BeehiveId = 10, StartDate = DateTime.UtcNow.Date.AddDays(-1) };
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
