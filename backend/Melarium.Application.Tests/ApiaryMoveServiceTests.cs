using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Pastures;
using Melarium.Application.Features.Pastures.DTOs;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>The move is the single mutation of an apiary's location (SPEC-10): FromPasture is
/// resolved server-side, coordinates are snapshotted, and only the latest move is correctable.</summary>
public class ApiaryMoveServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IAccessGuard _access = Substitute.For<IAccessGuard>();
    private readonly IPlanGuard _plan = Substitute.For<IPlanGuard>();

    private ApiaryMoveService Service() =>
        new(_uow, new TestCurrentUser { UserId = 1, Role = UserRole.OrganizationAdmin, OrganizationId = 1 }, _access, _plan);

    private static Apiary Apiary(
        int? currentPastureId = null, double? lat = 10, double? lon = 20,
        double? homeLat = null, double? homeLon = null) => new()
    {
        Id = 1, Name = "Pčelinjak Sjever", OrganizationId = 1,
        CurrentPastureId = currentPastureId, Latitude = lat, Longitude = lon,
        HomeLatitude = homeLat, HomeLongitude = homeLon,
    };

    private static Pasture Pasture(int id, int orgId = 1, double? lat = 44.2, double? lon = 17.9) => new()
    {
        Id = id, OrganizationId = orgId, Name = $"Pašnjak {id}", Latitude = lat, Longitude = lon,
    };

    private static CreateApiaryMoveDto Dto(int toPastureId = 7) => new()
    {
        ToPastureId = toPastureId, MovedAt = DateTime.UtcNow.AddDays(-1),
    };

    // ── Create ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ResolvesFromServerSide_AndSnapshotsCoordinates()
    {
        var apiary = Apiary(currentPastureId: 5);
        _uow.Apiaries.GetByIdAsync(1).Returns(apiary);
        _uow.Pastures.GetByIdAsync(7).Returns(Pasture(7));

        ApiaryMove? added = null;
        _uow.ApiaryMoves.AddAsync(Arg.Do<ApiaryMove>(m => added = m)).Returns(ci => ci.Arg<ApiaryMove>());
        _uow.ApiaryMoves.GetByApiaryAsync(1).Returns(_ => [added!]);

        var dto = await Service().CreateAsync(1, Dto(toPastureId: 7));

        Assert.Equal(5, added!.FromPastureId);        // from the apiary, never the client
        Assert.Equal(7, apiary.CurrentPastureId);
        Assert.Equal(44.2, apiary.Latitude);          // snapshot — weather/map follow automatically
        Assert.Equal(17.9, apiary.Longitude);
        Assert.Equal(7, dto.ToPastureId);
        await _uow.Received(1).SaveChangesAsync();    // move + apiary update atomically
    }

    [Fact]
    public async Task Create_FirstMove_HasNullFromPasture()
    {
        var apiary = Apiary(currentPastureId: null);
        _uow.Apiaries.GetByIdAsync(1).Returns(apiary);
        _uow.Pastures.GetByIdAsync(7).Returns(Pasture(7));

        ApiaryMove? added = null;
        _uow.ApiaryMoves.AddAsync(Arg.Do<ApiaryMove>(m => added = m)).Returns(ci => ci.Arg<ApiaryMove>());
        _uow.ApiaryMoves.GetByApiaryAsync(1).Returns(_ => [added!]);

        await Service().CreateAsync(1, Dto(toPastureId: 7));

        Assert.Null(added!.FromPastureId);
    }

    [Fact]
    public async Task Create_ToTheCurrentPasture_ThrowsValidation_NothingSaved()
    {
        _uow.Apiaries.GetByIdAsync(1).Returns(Apiary(currentPastureId: 7));
        _uow.Pastures.GetByIdAsync(7).Returns(Pasture(7));

        await Assert.ThrowsAsync<ValidationException>(() => Service().CreateAsync(1, Dto(toPastureId: 7)));
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Create_PastureFromAnotherOrganization_ThrowsValidation()
    {
        _uow.Apiaries.GetByIdAsync(1).Returns(Apiary());
        _uow.Pastures.GetByIdAsync(7).Returns(Pasture(7, orgId: 2));

        await Assert.ThrowsAsync<ValidationException>(() => Service().CreateAsync(1, Dto(toPastureId: 7)));
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Create_PastureWithoutCoordinates_KeepsApiaryCoordinates()
    {
        var apiary = Apiary(currentPastureId: null, lat: 10, lon: 20);
        _uow.Apiaries.GetByIdAsync(1).Returns(apiary);
        _uow.Pastures.GetByIdAsync(7).Returns(Pasture(7, lat: null, lon: null));

        ApiaryMove? added = null;
        _uow.ApiaryMoves.AddAsync(Arg.Do<ApiaryMove>(m => added = m)).Returns(ci => ci.Arg<ApiaryMove>());
        _uow.ApiaryMoves.GetByApiaryAsync(1).Returns(_ => [added!]);

        await Service().CreateAsync(1, Dto(toPastureId: 7));

        Assert.Equal(7, apiary.CurrentPastureId);     // administrative move
        Assert.Equal(10, apiary.Latitude);            // coordinates untouched
        Assert.Equal(20, apiary.Longitude);
    }

    // ── Delete (mistake correction, latest only) ─────────────────────────────────

    [Fact]
    public async Task Delete_LatestMove_RevertsPastureAndCoordinates()
    {
        var apiary = Apiary(currentPastureId: 7, lat: 44.2, lon: 17.9);
        var move = new ApiaryMove { Id = 3, ApiaryId = 1, FromPastureId = 5, ToPastureId = 7 };
        _uow.Apiaries.GetByIdAsync(1).Returns(apiary);
        _uow.ApiaryMoves.GetByIdAsync(3).Returns(move);
        _uow.ApiaryMoves.GetLatestForApiaryAsync(1).Returns(move);
        _uow.Pastures.GetByIdAsync(5).Returns(Pasture(5, lat: 43.5, lon: 18.5));

        await Service().DeleteAsync(1, 3);

        Assert.Equal(5, apiary.CurrentPastureId);
        Assert.Equal(43.5, apiary.Latitude);
        Assert.Equal(18.5, apiary.Longitude);
        await _uow.ApiaryMoves.Received(1).DeleteAsync(move);
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Delete_OlderMove_ThrowsValidation_NothingDeleted()
    {
        var older  = new ApiaryMove { Id = 3, ApiaryId = 1, ToPastureId = 5 };
        var latest = new ApiaryMove { Id = 9, ApiaryId = 1, ToPastureId = 7 };
        _uow.Apiaries.GetByIdAsync(1).Returns(Apiary(currentPastureId: 7));
        _uow.ApiaryMoves.GetByIdAsync(3).Returns(older);
        _uow.ApiaryMoves.GetLatestForApiaryAsync(1).Returns(latest);

        await Assert.ThrowsAsync<ValidationException>(() => Service().DeleteAsync(1, 3));
        await _uow.ApiaryMoves.DidNotReceive().DeleteAsync(Arg.Any<ApiaryMove>());
    }

    [Fact]
    public async Task Delete_FirstMove_RevertsToNullPasture_CoordinatesUnchanged()
    {
        var apiary = Apiary(currentPastureId: 7, lat: 44.2, lon: 17.9);
        var move = new ApiaryMove { Id = 3, ApiaryId = 1, FromPastureId = null, ToPastureId = 7 };
        _uow.Apiaries.GetByIdAsync(1).Returns(apiary);
        _uow.ApiaryMoves.GetByIdAsync(3).Returns(move);
        _uow.ApiaryMoves.GetLatestForApiaryAsync(1).Returns(move);

        await Service().DeleteAsync(1, 3);

        Assert.Null(apiary.CurrentPastureId);
        Assert.Equal(44.2, apiary.Latitude);          // matična lokacija was never stored — kept as-is
        Assert.Equal(17.9, apiary.Longitude);
    }

    // ── Return home ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReturnHome_RestoresHomeCoordinates_AndRecordsNullTargetMove()
    {
        var apiary = Apiary(currentPastureId: 7, lat: 44.2, lon: 17.9, homeLat: 10, homeLon: 20);
        _uow.Apiaries.GetByIdAsync(1).Returns(apiary);

        ApiaryMove? added = null;
        _uow.ApiaryMoves.AddAsync(Arg.Do<ApiaryMove>(m => added = m)).Returns(ci => ci.Arg<ApiaryMove>());
        _uow.ApiaryMoves.GetByApiaryAsync(1).Returns(_ => [added!]);

        var dto = await Service().ReturnHomeAsync(1);

        Assert.Equal(7, added!.FromPastureId);
        Assert.Null(added.ToPastureId);
        Assert.Null(apiary.CurrentPastureId);
        Assert.Equal(10, apiary.Latitude);
        Assert.Equal(20, apiary.Longitude);
        Assert.Null(dto.ToPastureId);
        Assert.Equal("Matična lokacija", dto.ToPastureName);
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task ReturnHome_WithoutKnownHomeLocation_ThrowsValidation_NothingSaved()
    {
        _uow.Apiaries.GetByIdAsync(1).Returns(Apiary(currentPastureId: 7, homeLat: null, homeLon: null));

        await Assert.ThrowsAsync<ValidationException>(() => Service().ReturnHomeAsync(1));
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task ReturnHome_WhenAlreadyAtHome_ThrowsValidation()
    {
        _uow.Apiaries.GetByIdAsync(1).Returns(Apiary(currentPastureId: null, homeLat: 10, homeLon: 20));

        await Assert.ThrowsAsync<ValidationException>(() => Service().ReturnHomeAsync(1));
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    // ── Set home location ────────────────────────────────────────────────────────

    [Fact]
    public async Task SetHomeLocation_UpdatesHomeOnly_CurrentPositionAndPastureUntouched()
    {
        var apiary = Apiary(currentPastureId: 7, lat: 44.2, lon: 17.9, homeLat: null, homeLon: null);
        _uow.Apiaries.GetByIdAsync(1).Returns(apiary);

        await Service().SetHomeLocationAsync(1, 10, 20);

        Assert.Equal(10, apiary.HomeLatitude);
        Assert.Equal(20, apiary.HomeLongitude);
        Assert.Equal(7, apiary.CurrentPastureId);     // away-ness untouched
        Assert.Equal(44.2, apiary.Latitude);          // current (pasture) position untouched
        Assert.Equal(17.9, apiary.Longitude);
        await _uow.Received(1).SaveChangesAsync();
    }
}
