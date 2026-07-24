using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Pastures.DTOs;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Pastures;

/// <summary>
/// Apiary relocation events (selidbe, SPEC-10). The move is the single place that changes an
/// apiary's location: it sets <see cref="Apiary.CurrentPastureId"/> and snapshots the pasture's
/// coordinates into the apiary, so weather, frost alerts, and map links follow with zero changes.
/// FromPasture is always resolved server-side — never trusted from the client.
/// </summary>
public class ApiaryMoveService : IApiaryMoveService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IAccessGuard _access;
    private readonly IPlanGuard _plan;

    public ApiaryMoveService(IUnitOfWork uow, ICurrentUser currentUser, IAccessGuard access, IPlanGuard plan)
    {
        _uow = uow;
        _currentUser = currentUser;
        _access = access;
        _plan = plan;
    }

    public async Task<IEnumerable<ApiaryMoveDto>> GetByApiaryAsync(int apiaryId)
    {
        var apiary = await _uow.Apiaries.GetByIdAsync(apiaryId)
            ?? throw new NotFoundException(nameof(Apiary), apiaryId);

        // View access mirrors the apiary page: a Beekeeper sees it through their assigned hives.
        if (_currentUser.Role == UserRole.Beekeeper)
        {
            var assigned = await _access.GetAssignedApiaryIdsAsync();
            if (!assigned.Contains(apiaryId)) throw new ForbiddenAccessException();
        }
        else
        {
            _access.EnsureCanManageApiary(apiary.Id, apiary.OrganizationId);
        }

        return (await _uow.ApiaryMoves.GetByApiaryAsync(apiaryId)).Select(ToDto);
    }

    public async Task<ApiaryMoveDto> CreateAsync(int apiaryId, CreateApiaryMoveDto dto)
    {
        var apiary = await _uow.Apiaries.GetByIdAsync(apiaryId)
            ?? throw new NotFoundException(nameof(Apiary), apiaryId);

        _access.EnsureInOrganization(apiary.OrganizationId);
        await _plan.EnsureFeatureAsync(apiary.OrganizationId, PlanFeature.Pastures);

        var pasture = await _uow.Pastures.GetByIdAsync(dto.ToPastureId)
            ?? throw new NotFoundException(nameof(Pasture), dto.ToPastureId);

        if (pasture.OrganizationId != apiary.OrganizationId)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["toPastureId"] = ["Pašnjak ne pripada vašoj organizaciji."]
            });

        if (apiary.CurrentPastureId == pasture.Id)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["toPastureId"] = ["Pčelinjak je već na ovom pašnjaku."]
            });

        var move = new ApiaryMove
        {
            ApiaryId          = apiary.Id,
            FromPastureId     = apiary.CurrentPastureId, // server-side — never from the client
            ToPastureId       = pasture.Id,
            MovedAt           = dto.MovedAt,
            CertificateNumber = Normalize(dto.CertificateNumber),
            Notes             = Normalize(dto.Notes),
            CreatedById       = _currentUser.UserId,
        };

        ApplyLocationSnapshot(apiary, pasture);
        apiary.CurrentPastureId = pasture.Id;
        apiary.UpdatedAt = DateTime.UtcNow;

        await _uow.ApiaryMoves.AddAsync(move);
        await _uow.Apiaries.UpdateAsync(apiary);
        await _uow.SaveChangesAsync(); // move + apiary update atomically

        var saved = await _uow.ApiaryMoves.GetByApiaryAsync(apiaryId);
        return ToDto(saved.First(m => m.Id == move.Id));
    }

    public async Task DeleteAsync(int apiaryId, int moveId)
    {
        var apiary = await _uow.Apiaries.GetByIdAsync(apiaryId)
            ?? throw new NotFoundException(nameof(Apiary), apiaryId);

        _access.EnsureInOrganization(apiary.OrganizationId);

        var move = await _uow.ApiaryMoves.GetByIdAsync(moveId);
        if (move is null || move.ApiaryId != apiaryId)
            throw new NotFoundException(nameof(ApiaryMove), moveId);

        // Deleting mid-history would corrupt yield attribution — only the latest move is correctable.
        var latest = await _uow.ApiaryMoves.GetLatestForApiaryAsync(apiaryId);
        if (latest is null || latest.Id != move.Id)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["moveId"] = ["Može se obrisati samo posljednja selidba pčelinjaka."]
            });

        // Revert to the previous pasture; the original ("matična") location was never stored,
        // so reverting the first move keeps the coordinates as they are (documented trade-off).
        apiary.CurrentPastureId = move.FromPastureId;
        if (move.FromPastureId is int fromId)
        {
            var fromPasture = await _uow.Pastures.GetByIdAsync(fromId);
            if (fromPasture is not null) ApplyLocationSnapshot(apiary, fromPasture);
        }
        apiary.UpdatedAt = DateTime.UtcNow;

        await _uow.ApiaryMoves.DeleteAsync(move);
        await _uow.Apiaries.UpdateAsync(apiary);
        await _uow.SaveChangesAsync();
    }

    public async Task<ApiaryMoveDto> ReturnHomeAsync(int apiaryId)
    {
        var apiary = await _uow.Apiaries.GetByIdAsync(apiaryId)
            ?? throw new NotFoundException(nameof(Apiary), apiaryId);

        _access.EnsureInOrganization(apiary.OrganizationId);
        await _plan.EnsureFeatureAsync(apiary.OrganizationId, PlanFeature.Pastures);

        if (apiary.HomeLatitude is not double homeLat || apiary.HomeLongitude is not double homeLon)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["apiaryId"] = ["Matična lokacija nije postavljena za ovaj pčelinjak."]
            });

        if (apiary.CurrentPastureId is null)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["apiaryId"] = ["Pčelinjak je već na matičnoj lokaciji."]
            });

        var move = new ApiaryMove
        {
            ApiaryId      = apiary.Id,
            FromPastureId = apiary.CurrentPastureId, // server-side — never from the client
            ToPastureId   = null,                    // returning to the matična lokacija
            MovedAt       = DateTime.UtcNow,
            CreatedById   = _currentUser.UserId,
        };

        apiary.Latitude         = homeLat;
        apiary.Longitude        = homeLon;
        apiary.CurrentPastureId = null;
        apiary.UpdatedAt        = DateTime.UtcNow;

        await _uow.ApiaryMoves.AddAsync(move);
        await _uow.Apiaries.UpdateAsync(apiary);
        await _uow.SaveChangesAsync();

        var saved = await _uow.ApiaryMoves.GetByApiaryAsync(apiaryId);
        return ToDto(saved.First(m => m.Id == move.Id));
    }

    public async Task SetHomeLocationAsync(int apiaryId, double latitude, double longitude)
    {
        var apiary = await _uow.Apiaries.GetByIdAsync(apiaryId)
            ?? throw new NotFoundException(nameof(Apiary), apiaryId);

        _access.EnsureInOrganization(apiary.OrganizationId);

        apiary.HomeLatitude  = latitude;
        apiary.HomeLongitude = longitude;
        apiary.UpdatedAt     = DateTime.UtcNow;

        await _uow.Apiaries.UpdateAsync(apiary);
        await _uow.SaveChangesAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>Copies the pasture's coordinates onto the apiary when the pasture has both set.</summary>
    private static void ApplyLocationSnapshot(Apiary apiary, Pasture pasture)
    {
        if (pasture.Latitude is double lat && pasture.Longitude is double lon)
        {
            apiary.Latitude  = lat;
            apiary.Longitude = lon;
        }
        // Pasture without coordinates → an "administrative" move: apiary coordinates stay as-is.
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ApiaryMoveDto ToDto(ApiaryMove m) => new()
    {
        Id                = m.Id,
        ApiaryId          = m.ApiaryId,
        FromPastureId     = m.FromPastureId,
        FromPastureName   = m.FromPasture?.Name,
        ToPastureId       = m.ToPastureId,
        ToPastureName     = m.ToPasture?.Name ?? (m.ToPastureId is null ? "Matična lokacija" : $"Pašnjak {m.ToPastureId}"),
        MovedAt           = m.MovedAt,
        CertificateNumber = m.CertificateNumber,
        Notes             = m.Notes,
        CreatedByName     = m.CreatedBy is not null ? $"{m.CreatedBy.FirstName} {m.CreatedBy.LastName}" : null,
        CreatedAt         = m.CreatedAt,
    };
}
