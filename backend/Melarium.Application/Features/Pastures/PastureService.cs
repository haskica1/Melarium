using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Pastures.DTOs;
using Melarium.Domain.Entities;

namespace Melarium.Application.Features.Pastures;

/// <summary>
/// Org-scoped pasture registry (SPEC-10). Reads are open to every role in the organization;
/// writes are restricted to OrgAdmin/SystemAdmin at the controller (<c>Roles.OrgManagers</c>),
/// with org membership re-checked here.
/// </summary>
public class PastureService : IPastureService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IAccessGuard _access;
    private readonly IPlanGuard _plan;

    public PastureService(IUnitOfWork uow, ICurrentUser currentUser, IAccessGuard access, IPlanGuard plan)
    {
        _uow = uow;
        _currentUser = currentUser;
        _access = access;
        _plan = plan;
    }

    public async Task<IEnumerable<PastureDto>> GetAllAsync()
    {
        if (_currentUser.OrganizationId is not int organizationId)
            return [];

        var rows = await _uow.Pastures.GetByOrganizationWithCountsAsync(organizationId);
        return rows.Select(r => ToDto(r.Pasture, r.ApiariesOnPasture));
    }

    public async Task<PastureDto> CreateAsync(SavePastureDto dto)
    {
        if (_currentUser.OrganizationId is not int organizationId)
            throw new ForbiddenAccessException("Morate pripadati organizaciji da biste kreirali pašnjak.");

        // Pastures are a paid-plan feature (SPEC-09); existing data stays readable on downgrade.
        await _plan.EnsureFeatureAsync(organizationId, PlanFeature.Pastures);

        var pasture = new Pasture { OrganizationId = organizationId };
        Apply(pasture, dto);

        await _uow.Pastures.AddAsync(pasture);
        await _uow.SaveChangesAsync();
        return ToDto(pasture, apiariesOnPasture: 0);
    }

    public async Task<PastureDto> UpdateAsync(int id, SavePastureDto dto)
    {
        var pasture = await _uow.Pastures.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Pasture), id);

        _access.EnsureInOrganization(pasture.OrganizationId);

        Apply(pasture, dto);
        pasture.UpdatedAt = DateTime.UtcNow;

        await _uow.Pastures.UpdateAsync(pasture);
        await _uow.SaveChangesAsync();
        return ToDto(pasture, await CountApiariesOnAsync(pasture.Id));
    }

    public async Task DeleteAsync(int id)
    {
        var pasture = await _uow.Pastures.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Pasture), id);

        _access.EnsureInOrganization(pasture.OrganizationId);

        // History is the point of the feature — a referenced pasture must not disappear.
        if (await _uow.Pastures.HasReferencesAsync(id))
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["pasture"] = ["Pašnjak se ne može obrisati dok je na njemu pčelinjak ili dok postoje selidbe koje ga referenciraju."]
            });

        await _uow.Pastures.DeleteAsync(pasture);
        await _uow.SaveChangesAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<int> CountApiariesOnAsync(int pastureId)
    {
        if (_currentUser.OrganizationId is not int organizationId) return 0;
        var rows = await _uow.Pastures.GetByOrganizationWithCountsAsync(organizationId);
        return rows.FirstOrDefault(r => r.Pasture.Id == pastureId).ApiariesOnPasture;
    }

    private static void Apply(Pasture pasture, SavePastureDto dto)
    {
        pasture.Name       = dto.Name.Trim();
        pasture.Latitude   = dto.Latitude;
        pasture.Longitude  = dto.Longitude;
        pasture.Address    = Normalize(dto.Address);
        pasture.FloraNotes = Normalize(dto.FloraNotes);
        pasture.Notes      = Normalize(dto.Notes);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static PastureDto ToDto(Pasture p, int apiariesOnPasture) => new()
    {
        Id                = p.Id,
        Name              = p.Name,
        Latitude          = p.Latitude,
        Longitude         = p.Longitude,
        Address           = p.Address,
        FloraNotes        = p.FloraNotes,
        Notes             = p.Notes,
        ApiariesOnPasture = apiariesOnPasture,
        CreatedAt         = p.CreatedAt,
    };
}
