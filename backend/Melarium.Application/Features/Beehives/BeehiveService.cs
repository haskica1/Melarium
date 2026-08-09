using AutoMapper;
using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Common.Services;
using Melarium.Application.Features.Ai;
using Melarium.Application.Features.Beehives.DTOs;
using Melarium.Application.Features.Notifications;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Melarium.Application.Features.Beehives;

public class BeehiveService : IBeehiveService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IQrCodeService _qr;
    private readonly INotificationService _notifications;
    private readonly ICurrentUser _currentUser;
    private readonly IAccessGuard _access;
    private readonly IPlanGuard _plan;
    private readonly IHiveNumberOcrClient _ocr;
    private readonly ILogger<BeehiveService> _logger;
    private readonly string _frontendUrl;

    public BeehiveService(
        IUnitOfWork uow,
        IMapper mapper,
        IQrCodeService qr,
        INotificationService notifications,
        ICurrentUser currentUser,
        IAccessGuard access,
        IPlanGuard plan,
        IHiveNumberOcrClient ocr,
        ILogger<BeehiveService> logger,
        IConfiguration config)
    {
        _uow           = uow;
        _mapper        = mapper;
        _qr            = qr;
        _notifications = notifications;
        _currentUser   = currentUser;
        _access        = access;
        _plan          = plan;
        _ocr           = ocr;
        _logger        = logger;
        _frontendUrl   = config["FrontendUrl"] ?? "https://bee-hive-app.vercel.app";
    }

    public async Task<IEnumerable<BeehiveDto>> GetByApiaryIdAsync(int apiaryId)
    {
        if (!await _uow.Apiaries.ExistsAsync(apiaryId))
            throw new NotFoundException(nameof(Apiary), apiaryId);

        // Managers must own the apiary; a Beekeeper is filtered to assigned hives below.
        if (_currentUser.Role != UserRole.Beekeeper)
            await _access.EnsureCanManageApiaryAsync(apiaryId);

        var beehives = await _uow.Beehives.GetByApiaryIdAsync(apiaryId);
        var inspectionCounts = await _uow.Inspections.CountByBeehiveForApiaryAsync(apiaryId);

        if (_currentUser.Role == UserRole.Beekeeper)
        {
            var assignedIds = await _access.GetAssignedBeehiveIdsAsync();
            beehives = beehives.Where(b => assignedIds.Contains(b.Id)).ToList();
        }

        return beehives.Select(b =>
        {
            var dto = _mapper.Map<BeehiveDto>(b);
            dto.InspectionCount = inspectionCounts.GetValueOrDefault(b.Id);
            return dto;
        }).ToList();
    }

    public async Task<BeehiveDetailDto> GetByIdAsync(int id)
    {
        var beehive = await _uow.Beehives.GetWithInspectionsAsync(id)
            ?? throw new NotFoundException(nameof(Beehive), id);

        await _access.EnsureCanAccessBeehiveAsync(id);

        return _mapper.Map<BeehiveDetailDto>(beehive);
    }

    public async Task<BeehiveDto> CreateAsync(CreateBeehiveDto dto)
    {
        var apiary = await _uow.Apiaries.GetByIdAsync(dto.ApiaryId)
            ?? throw new NotFoundException(nameof(Apiary), dto.ApiaryId);

        await _access.EnsureCanManageApiaryAsync(dto.ApiaryId);
        await _plan.EnsureCanAddBeehiveAsync(apiary.OrganizationId);

        var beehive = _mapper.Map<Beehive>(dto);
        beehive.CreatedById  = _currentUser.UserId;
        beehive.UniqueId     = Guid.NewGuid();
        beehive.QrCodeBase64 = _qr.GeneratePngBase64($"{_frontendUrl}/scan/{beehive.UniqueId}");

        await _uow.Beehives.AddAsync(beehive);
        await _uow.SaveChangesAsync();

        var saved = await _uow.Beehives.GetWithInspectionsAsync(beehive.Id) ?? beehive;

        // Notify the creator's superior about the new beehive.
        if (_currentUser.UserId is int creatorId)
        {
            var creator = await _uow.Users.GetByIdWithOrganizationAsync(creatorId);
            if (creator != null)
                await SendBeehiveCreatedNotificationsAsync(saved, creator);
        }

        return _mapper.Map<BeehiveDto>(saved);
    }

    public async Task<BeehiveDto> UpdateAsync(int id, UpdateBeehiveDto dto)
    {
        var beehive = await _uow.Beehives.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Beehive), id);

        // Must be able to manage the beehive's current apiary…
        await _access.EnsureCanManageApiaryAsync(beehive.ApiaryId);

        if (!await _uow.Apiaries.ExistsAsync(dto.ApiaryId))
            throw new NotFoundException(nameof(Apiary), dto.ApiaryId);

        // …and the target apiary, in case the beehive is being moved.
        if (dto.ApiaryId != beehive.ApiaryId)
            await _access.EnsureCanManageApiaryAsync(dto.ApiaryId);

        _mapper.Map(dto, beehive);
        beehive.UpdatedAt = DateTime.UtcNow;

        await _uow.Beehives.UpdateAsync(beehive);
        await _uow.SaveChangesAsync();

        return _mapper.Map<BeehiveDto>(beehive);
    }

    public async Task DeleteAsync(int id)
    {
        var beehive = await _uow.Beehives.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Beehive), id);

        await _access.EnsureCanManageApiaryAsync(beehive.ApiaryId);

        await _uow.Beehives.DeleteAsync(beehive);
        await _uow.SaveChangesAsync();
    }

    public async Task<BeehiveScanDto?> GetScanInfoAsync(Guid uniqueId)
    {
        var beehive = await _uow.Beehives.GetByUniqueIdAsync(uniqueId);
        if (beehive is null) return null;
        return new BeehiveScanDto { Id = beehive.Id, Name = beehive.Name, ApiaryId = beehive.ApiaryId };
    }

    public async Task<IEnumerable<BeehiveQrDto>> GetQrCodesByApiaryAsync(int apiaryId)
    {
        if (!await _uow.Apiaries.ExistsAsync(apiaryId))
            throw new NotFoundException(nameof(Apiary), apiaryId);

        if (_currentUser.Role != UserRole.Beekeeper)
            await _access.EnsureCanManageApiaryAsync(apiaryId);

        var beehives = await _uow.Beehives.FindAsync(b => b.ApiaryId == apiaryId);

        if (_currentUser.Role == UserRole.Beekeeper)
        {
            var assignedIds = await _access.GetAssignedBeehiveIdsAsync();
            beehives = beehives.Where(b => assignedIds.Contains(b.Id));
        }

        return beehives
            .OrderBy(b => b.Name)
            .Select(b => new BeehiveQrDto
            {
                Id           = b.Id,
                Name         = b.Name,
                UniqueId     = b.UniqueId,
                QrCodeBase64 = b.QrCodeBase64,
            })
            .ToList();
    }

    public Task<bool> CanCurrentUserAccessAsync(int beehiveId) =>
        _access.CanAccessBeehiveAsync(beehiveId);

    public async Task<IEnumerable<BeehiveDto>> GetAllForCurrentUserAsync() =>
        _mapper.Map<IEnumerable<BeehiveDto>>(await GetAccessibleBeehivesAsync());

    /// <summary>
    /// Role-scoped set of beehive entities the current caller may see. The rules moved to
    /// <see cref="IAccessGuard.GetAccessibleBeehivesAsync"/> when SPEC-17 needed the same set — one
    /// source, so the assistant and the hive list can never drift apart.
    /// </summary>
    private Task<IReadOnlyList<Beehive>> GetAccessibleBeehivesAsync() =>
        _access.GetAccessibleBeehivesAsync();

    public async Task<BeehiveNumberMatchResult> MatchByNumberAsync(string number)
    {
        var target = HiveNumberMatcher.Normalize(number);
        if (target is null)
            return new BeehiveNumberMatchResult { RecognizedNumber = number };

        var matched = (await GetAccessibleBeehivesAsync())
            .Where(b => HiveNumberMatcher.Matches(b.LabelNumber, b.Name, target))
            .ToList();

        // Apiary names for the picker — batch-loaded so we don't rely on each query eager-loading Apiary.
        var apiaryIds = matched.Select(b => b.ApiaryId).Distinct().ToList();
        var apiaryNames = apiaryIds.Count > 0
            ? (await _uow.Apiaries.FindAsync(a => apiaryIds.Contains(a.Id))).ToDictionary(a => a.Id, a => a.Name)
            : new Dictionary<int, string>();

        return new BeehiveNumberMatchResult
        {
            RecognizedNumber = number,
            Matches = matched
                .Select(b => new BeehiveMatchDto
                {
                    Id          = b.Id,
                    Name        = b.Name,
                    LabelNumber = b.LabelNumber,
                    ApiaryId    = b.ApiaryId,
                    ApiaryName  = apiaryNames.GetValueOrDefault(b.ApiaryId),
                })
                .OrderBy(m => m.ApiaryName)
                .ThenBy(m => m.Name)
                .ToList(),
        };
    }

    public async Task<BeehiveNumberMatchResult> ScanByNumberAsync(byte[] image, string contentType, CancellationToken cancellationToken = default)
    {
        var ocr = await _ocr.RecognizeNumberAsync(image, contentType, cancellationToken);
        if (string.IsNullOrWhiteSpace(ocr.Number))
            return new BeehiveNumberMatchResult { RecognizedNumber = null };

        return await MatchByNumberAsync(ocr.Number);
    }

    public async Task<int> BackfillLabelNumbersFromNamesAsync()
    {
        var all = await _uow.Beehives.GetAllAsync();
        int count = 0;
        foreach (var b in all)
        {
            if (!string.IsNullOrWhiteSpace(b.LabelNumber)) continue;

            var parsed = HiveNumberMatcher.PrimaryNameNumber(b.Name);
            if (parsed is null) continue;

            b.LabelNumber = parsed;
            await _uow.Beehives.UpdateAsync(b);
            count++;
        }
        await _uow.SaveChangesAsync();
        return count;
    }

    public async Task<int> RegenerateAllQrCodesAsync()
    {
        var beehives = await _uow.Beehives.GetAllWithUniqueIdAsync();
        int count = 0;
        foreach (var b in beehives)
        {
            b.QrCodeBase64 = _qr.GeneratePngBase64($"{_frontendUrl}/scan/{b.UniqueId}");
            await _uow.Beehives.UpdateAsync(b);
            count++;
        }
        await _uow.SaveChangesAsync();
        return count;
    }

    // ── Notification helpers ──────────────────────────────────────────────────

    private async Task SendBeehiveCreatedNotificationsAsync(Beehive beehive, User creator)
    {
        var apiary = await _uow.Apiaries.GetByIdAsync(beehive.ApiaryId);
        if (apiary == null)
        {
            _logger.LogWarning("SendBeehiveCreatedNotifications: apiary {ApiaryId} not found — skipping", beehive.ApiaryId);
            return;
        }

        if (creator.Role == UserRole.ApiaryAdmin)
        {
            // Use apiary.OrganizationId (more reliable than creator.OrganizationId)
            var orgAdmins = await _uow.Users.FindAsync(u =>
                u.OrganizationId == apiary.OrganizationId && u.Role == UserRole.OrganizationAdmin);

            foreach (var orgAdmin in orgAdmins)
            {
                await _notifications.NotifyAsync(
                    orgAdmin.Id,
                    "Nova košnica",
                    $"Admin {creator.FirstName} {creator.LastName} je dodao/la košnicu '{beehive.Name}' u pčelinjak '{apiary.Name}'.",
                    NotificationType.BeehiveCreated,
                    beehive.Id, nameof(Beehive));
            }
        }
        else if (creator.Role == UserRole.OrganizationAdmin)
        {
            var admins = await _uow.Users.FindAsync(u =>
                u.ApiaryId == beehive.ApiaryId && u.Role == UserRole.ApiaryAdmin);

            foreach (var admin in admins)
            {
                await _notifications.NotifyAsync(
                    admin.Id,
                    "Nova košnica",
                    $"Administrator organizacije {creator.FirstName} {creator.LastName} je dodao/la košnicu '{beehive.Name}' u vaš pčelinjak '{apiary.Name}'.",
                    NotificationType.BeehiveCreated,
                    beehive.Id, nameof(Beehive));
            }
        }
        else if (creator.Role == UserRole.SystemAdmin)
        {
            var orgAdmins = await _uow.Users.FindAsync(u =>
                u.OrganizationId == apiary.OrganizationId && u.Role == UserRole.OrganizationAdmin);

            foreach (var orgAdmin in orgAdmins)
            {
                await _notifications.NotifyAsync(
                    orgAdmin.Id,
                    "Nova košnica",
                    $"Sistemski administrator je dodao košnicu '{beehive.Name}' u pčelinjak '{apiary.Name}'.",
                    NotificationType.BeehiveCreated,
                    beehive.Id, nameof(Beehive));
            }
        }
    }
}
