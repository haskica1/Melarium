using AutoMapper;
using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Apiaries.DTOs;
using Melarium.Domain.Entities;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Apiaries;

public class ApiaryService : IApiaryService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ICurrentUser _currentUser;
    private readonly IAccessGuard _access;
    private readonly IPlanGuard _plan;

    public ApiaryService(IUnitOfWork uow, IMapper mapper, ICurrentUser currentUser, IAccessGuard access, IPlanGuard plan)
    {
        _uow = uow;
        _mapper = mapper;
        _currentUser = currentUser;
        _access = access;
        _plan = plan;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ApiaryDto>> GetAllForCurrentUserAsync()
    {
        if (_currentUser.OrganizationId is not int organizationId)
            return [];

        IEnumerable<(Apiary Apiary, int BeehiveCount)> rows =
            await _uow.Apiaries.GetByOrganizationWithCountsAsync(organizationId);

        switch (_currentUser.Role)
        {
            // An ApiaryAdmin only sees their assigned apiary.
            case UserRole.ApiaryAdmin:
                rows = _currentUser.ApiaryId is int apiaryId
                    ? rows.Where(r => r.Apiary.Id == apiaryId)
                    : [];
                break;

            // A Beekeeper only sees apiaries that contain a beehive assigned to them.
            case UserRole.Beekeeper:
                var assignedApiaryIds = await _access.GetAssignedApiaryIdsAsync();
                rows = rows.Where(r => assignedApiaryIds.Contains(r.Apiary.Id));
                break;
        }

        return rows.Select(r =>
        {
            var dto = _mapper.Map<ApiaryDto>(r.Apiary);
            dto.BeehiveCount = r.BeehiveCount;
            return dto;
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<ApiaryDetailDto> GetByIdAsync(int id)
    {
        var apiary = await _uow.Apiaries.GetWithBeehivesAsync(id)
            ?? throw new NotFoundException(nameof(Apiary), id);

        // Inspection counts come from a grouped query — the rows themselves are never loaded.
        var inspectionCounts = await _uow.Inspections.CountByBeehiveForApiaryAsync(id);

        var dto = _mapper.Map<ApiaryDetailDto>(apiary);
        foreach (var hive in dto.Beehives)
            hive.InspectionCount = inspectionCounts.GetValueOrDefault(hive.Id);

        // A Beekeeper may view an apiary only through the beehives assigned to them.
        if (_currentUser.Role == UserRole.Beekeeper)
        {
            var assignedIds = await _access.GetAssignedBeehiveIdsAsync();
            var visible = dto.Beehives.Where(b => assignedIds.Contains(b.Id)).ToList();
            if (visible.Count == 0)
                throw new ForbiddenAccessException();

            dto.Beehives = visible;
            dto.BeehiveCount = visible.Count;
            return dto;
        }

        // Managers must own the apiary (same org / same apiary).
        _access.EnsureCanManageApiary(apiary.Id, apiary.OrganizationId);
        return dto;
    }

    /// <inheritdoc />
    public async Task<ApiaryDto> CreateAsync(CreateApiaryDto dto)
    {
        if (_currentUser.OrganizationId is not int organizationId)
            throw new ForbiddenAccessException("You must belong to an organization to create an apiary.");

        await _plan.EnsureCanAddApiaryAsync(organizationId);

        var apiary = _mapper.Map<Apiary>(dto);
        apiary.OrganizationId = organizationId;
        apiary.CreatedById = _currentUser.UserId;
        // A brand-new apiary is always at its matična lokacija — capture it as Home immediately.
        apiary.HomeLatitude = dto.Latitude;
        apiary.HomeLongitude = dto.Longitude;
        await _uow.Apiaries.AddAsync(apiary);
        await _uow.SaveChangesAsync();
        // Reload to get CreatedBy nav property
        var saved = await _uow.Apiaries.GetWithBeehivesAsync(apiary.Id) ?? apiary;
        return _mapper.Map<ApiaryDto>(saved);
    }

    /// <inheritdoc />
    public async Task<ApiaryDto> UpdateAsync(int id, UpdateApiaryDto dto)
    {
        var apiary = await _uow.Apiaries.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Apiary), id);

        _access.EnsureCanManageApiary(apiary.Id, apiary.OrganizationId);

        _mapper.Map(dto, apiary);
        // While at the matična lokacija (no pasture), the location field IS the home location —
        // keep Home mirrored. While away, editing the current position must not overwrite Home.
        if (apiary.CurrentPastureId is null)
        {
            apiary.HomeLatitude = apiary.Latitude;
            apiary.HomeLongitude = apiary.Longitude;
        }
        apiary.UpdatedAt = DateTime.UtcNow;

        await _uow.Apiaries.UpdateAsync(apiary);
        await _uow.SaveChangesAsync();

        return _mapper.Map<ApiaryDto>(apiary);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int id)
    {
        var apiary = await _uow.Apiaries.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Apiary), id);

        _access.EnsureCanManageApiary(apiary.Id, apiary.OrganizationId);

        // Apiary → Todos has NO ACTION cascade (to avoid multiple-cascade-path errors).
        // Delete apiary-level todos explicitly before removing the apiary.
        // Beehive-level todos are handled by the existing Beehive → Todos cascade.
        var apiaryTodos = await _uow.Todos.GetByApiaryIdAsync(id);
        foreach (var todo in apiaryTodos)
            await _uow.Todos.DeleteAsync(todo);

        await _uow.Apiaries.DeleteAsync(apiary);
        await _uow.SaveChangesAsync();
    }
}
