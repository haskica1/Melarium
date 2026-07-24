using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Queens;
using Melarium.Application.Features.Queens.DTOs;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// Locks the queen lifecycle rules: a new queen is always Active, registering one atomically
/// closes the previous active queen as Replaced, and a hive can never have two active queens.
/// </summary>
public class QueenServiceTests
{
    private const int HiveId = 10;

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IAccessGuard _access = Substitute.For<IAccessGuard>();
    private readonly ICurrentUser _currentUser = new TestCurrentUser { UserId = 7 };
    private readonly QueenService _service;

    private Queen? _addedQueen;
    private readonly List<QueenEditLog> _addedEditLogs = [];

    public QueenServiceTests()
    {
        _service = new QueenService(_uow, _access, _currentUser);

        _uow.Beehives.ExistsAsync(HiveId).Returns(true);
        _uow.Queens.GetActiveByBeehiveIdAsync(HiveId).Returns((Queen?)null);
        _uow.Queens.AddAsync(Arg.Do<Queen>(q => _addedQueen = q)).Returns(ci => ci.Arg<Queen>());
        _uow.QueenEditLogs.AddAsync(Arg.Do<QueenEditLog>(l => _addedEditLogs.Add(l)))
            .Returns(ci => ci.Arg<QueenEditLog>());
    }

    private static CreateQueenDto NewDto(int year = 2026, QueenMarkColor? color = null) => new()
    {
        Year           = year,
        MarkColor      = color,
        IsMarked       = true,
        IsClipped      = false,
        Origin         = QueenOrigin.Purchased,
        IntroducedDate = DateTime.UtcNow.Date,
    };

    private static Queen ActiveQueen(int id, int year = 2024) => new()
    {
        Id             = id,
        BeehiveId      = HiveId,
        Year           = year,
        MarkColor      = QueenMarkColor.Green,
        Origin         = QueenOrigin.OwnBreeding,
        Status         = QueenStatus.Active,
        IntroducedDate = new DateTime(year, 5, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    // ── CreateAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_FirstQueen_IsActive_WithColorDerivedFromYear()
    {
        var result = await _service.CreateAsync(HiveId, NewDto(year: 2026));

        Assert.Equal(QueenStatus.Active, result.Status);
        Assert.Equal(QueenMarkColor.White, result.MarkColor); // 2026 ends in 6 → White
        Assert.Null(result.EndDate);
        Assert.Equal("Bijela", result.MarkColorName);
    }

    [Fact]
    public async Task Create_ExplicitColor_OverridesDerivedOne()
    {
        var result = await _service.CreateAsync(HiveId, NewDto(year: 2026, color: QueenMarkColor.Red));

        Assert.Equal(QueenMarkColor.Red, result.MarkColor);
    }

    [Fact]
    public async Task Create_WithExistingActiveQueen_ClosesItAsReplaced_InOneSave()
    {
        var old = ActiveQueen(id: 5);
        _uow.Queens.GetActiveByBeehiveIdAsync(HiveId).Returns(old);
        var dto = NewDto();

        await _service.CreateAsync(HiveId, dto);

        Assert.Equal(QueenStatus.Replaced, old.Status);
        Assert.Equal(dto.IntroducedDate, old.EndDate);
        Assert.NotNull(_addedQueen);
        Assert.Equal(QueenStatus.Active, _addedQueen!.Status);
        await _uow.Received(1).SaveChangesAsync(); // both changes in a single atomic save
    }

    [Fact]
    public async Task Create_BeehiveMissing_ThrowsNotFound()
    {
        _uow.Beehives.ExistsAsync(99).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateAsync(99, NewDto()));
    }

    [Fact]
    public async Task Create_AccessDenied_Propagates_AndSavesNothing()
    {
        _access.EnsureCanAccessBeehiveAsync(HiveId)
            .Returns(Task.FromException(new ForbiddenAccessException()));

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _service.CreateAsync(HiveId, NewDto()));
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    // ── UpdateAsync ────────────────────────────────────────────────────────────

    private static UpdateQueenDto UpdateDto(QueenStatus status, DateTime? endDate = null) => new()
    {
        Year           = 2024,
        MarkColor      = QueenMarkColor.Green,
        IsMarked       = true,
        IsClipped      = false,
        Origin         = QueenOrigin.OwnBreeding,
        Status         = status,
        IntroducedDate = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc),
        EndDate        = endDate,
    };

    [Fact]
    public async Task Update_ToActive_WhenAnotherActiveExists_ThrowsBusinessRule()
    {
        var replaced = ActiveQueen(id: 5);
        replaced.Status = QueenStatus.Replaced;
        _uow.Queens.GetByIdAsync(5).Returns(replaced);
        _uow.Queens.GetActiveByBeehiveIdAsync(HiveId).Returns(ActiveQueen(id: 6));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.UpdateAsync(5, UpdateDto(QueenStatus.Active)));
    }

    [Fact]
    public async Task Update_ToNonActiveStatus_WithoutEndDate_DefaultsEndDateToNow()
    {
        var queen = ActiveQueen(id: 5);
        _uow.Queens.GetByIdAsync(5).Returns(queen);

        var result = await _service.UpdateAsync(5, UpdateDto(QueenStatus.Died));

        Assert.Equal(QueenStatus.Died, result.Status);
        Assert.NotNull(result.EndDate);
        Assert.Equal("Uginula", result.StatusName);
    }

    [Fact]
    public async Task Update_SameQueenStaysActive_KeepsEndDateNull_AndDoesNotThrow()
    {
        var queen = ActiveQueen(id: 5);
        _uow.Queens.GetByIdAsync(5).Returns(queen);
        _uow.Queens.GetActiveByBeehiveIdAsync(HiveId).Returns(queen);

        var result = await _service.UpdateAsync(5, UpdateDto(QueenStatus.Active));

        Assert.Equal(QueenStatus.Active, result.Status);
        Assert.Null(result.EndDate);
    }

    [Fact]
    public async Task Update_ChangedFields_WritesOneEditLogRowPerChange()
    {
        var queen = ActiveQueen(id: 5); // Year 2024, MarkColor Green, Origin OwnBreeding
        _uow.Queens.GetByIdAsync(5).Returns(queen);
        _uow.Queens.GetActiveByBeehiveIdAsync(HiveId).Returns(queen);

        var dto = new UpdateQueenDto
        {
            Year           = 2023, // only the year is wrong in the initial data
            MarkColor      = queen.MarkColor,
            IsMarked       = queen.IsMarked,
            IsClipped      = queen.IsClipped,
            Origin         = queen.Origin,
            Status         = queen.Status,
            IntroducedDate = queen.IntroducedDate,
            EndDate        = queen.EndDate,
            Notes          = queen.Notes,
        };

        await _service.UpdateAsync(5, dto);

        var edit = Assert.Single(_addedEditLogs);
        Assert.Equal(5, edit.QueenId);
        Assert.Equal(7, edit.EditedById);
        Assert.Equal("Godište", edit.FieldLabel);
        Assert.Equal("2024", edit.OldValue);
        Assert.Equal("2023", edit.NewValue);
    }

    [Fact]
    public async Task Update_NoActualChanges_WritesNoEditLog()
    {
        var queen = ActiveQueen(id: 5); // Year 2024, Green, OwnBreeding, IsMarked/IsClipped false, no Notes
        _uow.Queens.GetByIdAsync(5).Returns(queen);
        _uow.Queens.GetActiveByBeehiveIdAsync(HiveId).Returns(queen);

        var dto = new UpdateQueenDto
        {
            Year           = queen.Year,
            MarkColor      = queen.MarkColor,
            IsMarked       = queen.IsMarked,
            IsClipped      = queen.IsClipped,
            Origin         = queen.Origin,
            Status         = queen.Status,
            IntroducedDate = queen.IntroducedDate,
            EndDate        = queen.EndDate,
            Notes          = queen.Notes,
        };

        await _service.UpdateAsync(5, dto);

        Assert.Empty(_addedEditLogs);
    }

    // ── GetByBeehiveIdAsync / DeleteAsync ──────────────────────────────────────

    [Fact]
    public async Task GetByBeehive_EnforcesBeehiveAccess()
    {
        _uow.Queens.GetByBeehiveIdAsync(HiveId).Returns([]);

        await _service.GetByBeehiveIdAsync(HiveId);

        await _access.Received(1).EnsureCanAccessBeehiveAsync(HiveId);
    }

    [Fact]
    public async Task Delete_QueenMissing_ThrowsNotFound()
    {
        _uow.Queens.GetByIdAsync(42).Returns((Queen?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAsync(42));
    }
}
