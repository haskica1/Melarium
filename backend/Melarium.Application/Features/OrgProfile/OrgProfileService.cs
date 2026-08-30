using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Validation;
using Melarium.Application.Features.OrgProfile.DTOs;
using Melarium.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Melarium.Application.Features.OrgProfile;

/// <summary>
/// Self-service organization profile (SPEC-22). The organization always comes from the caller's JWT,
/// so there is no id to tamper with and no <c>IAccessGuard</c> check to forget; the role split
/// (read = any member, write = OrganizationAdmin) is enforced by the controller's attributes.
/// </summary>
public class OrgProfileService : IOrgProfileService
{
    /// <summary>
    /// Smaller than the 5 MB feedback screenshot and the 8 MB inspection photo: this is a mark shown
    /// at ~64 px, and every member of the organization downloads it.
    /// </summary>
    public const long MaxLogoBytes = 2 * 1024 * 1024;

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IFileStorage _storage;
    private readonly ILogger<OrgProfileService> _logger;

    public OrgProfileService(
        IUnitOfWork uow,
        ICurrentUser currentUser,
        IFileStorage storage,
        ILogger<OrgProfileService> logger)
    {
        _uow         = uow;
        _currentUser = currentUser;
        _storage     = storage;
        _logger      = logger;
    }

    public async Task<MyOrganizationDto> GetMyOrganizationAsync()
    {
        var org = await LoadAsync();
        return await MapAsync(org);
    }

    public async Task<MyOrganizationDto> UpdateMyOrganizationAsync(UpdateMyOrganizationDto dto)
    {
        var org = await LoadAsync();

        org.Name = dto.Name.Trim();
        org.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();

        await _uow.Organizations.UpdateAsync(org);
        await _uow.SaveChangesAsync();

        return await MapAsync(org);
    }

    public async Task<MyOrganizationDto> SetLogoAsync(Stream content, long sizeBytes)
    {
        var org = await LoadAsync();

        if (sizeBytes > MaxLogoBytes)
            throw new BusinessRuleException(
                $"Logotip ne smije biti veći od {MaxLogoBytes / (1024 * 1024)} MB.");

        var (seekable, contentType) = await ImageRules.SniffContentTypeAsync(content);
        if (contentType is null)
            throw new BusinessRuleException(ImageRules.UnsupportedFormatMessage);

        var previousPath = org.LogoStoragePath;
        var storagePath = await _storage.SaveAsync(seekable, contentType);

        org.LogoStoragePath = storagePath;
        org.LogoContentType = contentType;

        try
        {
            await _uow.Organizations.UpdateAsync(org);
            await _uow.SaveChangesAsync();
        }
        catch
        {
            // The blob is already written — don't leave it orphaned if the DB update fails.
            await TryDeleteBlobAsync(storagePath);
            throw;
        }

        // Only once the new key is committed: a replaced logo's blob is unreachable from here on.
        if (previousPath is not null)
            await TryDeleteBlobAsync(previousPath);

        return await MapAsync(org);
    }

    public async Task<(Stream Content, string ContentType)> OpenLogoAsync()
    {
        var org = await LoadAsync();

        if (org.LogoStoragePath is null || org.LogoContentType is null)
            throw new NotFoundException(nameof(Organization), org.Id);

        try
        {
            var stream = await _storage.OpenReadAsync(org.LogoStoragePath);
            return (stream, org.LogoContentType);
        }
        catch (FileNotFoundException)
        {
            // Row points at a blob that is gone (e.g. wiped dev disk) — a clean 404 beats a 500.
            throw new NotFoundException(nameof(Organization), org.Id);
        }
    }

    public async Task<MyOrganizationDto> RemoveLogoAsync()
    {
        var org = await LoadAsync();

        var previousPath = org.LogoStoragePath;
        org.LogoStoragePath = null;
        org.LogoContentType = null;

        await _uow.Organizations.UpdateAsync(org);
        await _uow.SaveChangesAsync();

        // Best-effort: a storage hiccup must not undo the committed removal.
        if (previousPath is not null)
            await TryDeleteBlobAsync(previousPath);

        return await MapAsync(org);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// The caller's organization, tracked. A SystemAdmin has none, which is why this is a 403 and
    /// not a 404 — the row exists for everyone else, the caller just isn't in one.
    /// </summary>
    private async Task<Organization> LoadAsync()
    {
        if (_currentUser.OrganizationId is not int orgId)
            throw new ForbiddenAccessException("Vaš račun ne pripada nijednoj organizaciji.");

        return await _uow.Organizations.GetWithDetailsAsync(orgId)
            ?? throw new NotFoundException(nameof(Organization), orgId);
    }

    private async Task<MyOrganizationDto> MapAsync(Organization org)
    {
        // Hives hang off apiaries, so they are counted rather than read off the entity — and an org
        // with none is absent from the dictionary, where 0 is the real answer.
        var beehiveCounts = await _uow.Organizations.GetBeehiveCountsAsync(org.Id);

        return new MyOrganizationDto
        {
            Id = org.Id,
            Name = org.Name,
            Description = org.Description,
            HasLogo = org.LogoStoragePath is not null,
            CreatedAt = org.CreatedAt,
            UserCount = org.Users.Count,
            ApiaryCount = org.Apiaries.Count,
            BeehiveCount = beehiveCounts.GetValueOrDefault(org.Id),
        };
    }

    private async Task TryDeleteBlobAsync(string storagePath)
    {
        try
        {
            await _storage.DeleteAsync(storagePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete stored file {StoragePath} — orphaned blob left behind", storagePath);
        }
    }
}
