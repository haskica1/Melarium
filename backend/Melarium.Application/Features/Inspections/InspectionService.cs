using AutoMapper;
using Melarium.Application.Common.Exceptions;
using Melarium.Application.Common.Interfaces;
using Melarium.Application.Common.Security;
using Melarium.Application.Features.Inspections.DTOs;
using Melarium.Application.Features.Weather;
using Melarium.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Melarium.Application.Features.Inspections;

public class InspectionService : IInspectionService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IAccessGuard _access;
    private readonly IWeatherService _weather;
    private readonly IFileStorage _storage;
    private readonly ILogger<InspectionService> _logger;

    public InspectionService(
        IUnitOfWork uow,
        IMapper mapper,
        IAccessGuard access,
        IWeatherService weather,
        IFileStorage storage,
        ILogger<InspectionService> logger)
    {
        _uow = uow;
        _mapper = mapper;
        _access = access;
        _weather = weather;
        _storage = storage;
        _logger = logger;
    }

    public async Task<IEnumerable<InspectionDto>> GetByBeehiveIdAsync(int beehiveId)
    {
        if (!await _uow.Beehives.ExistsAsync(beehiveId))
            throw new NotFoundException(nameof(Beehive), beehiveId);

        await _access.EnsureCanAccessBeehiveAsync(beehiveId);

        var inspections = await _uow.Inspections.GetByBeehiveIdAsync(beehiveId);
        return _mapper.Map<IEnumerable<InspectionDto>>(inspections);
    }

    public async Task<InspectionDto> GetByIdAsync(int id)
    {
        var inspection = await _uow.Inspections.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Inspection), id);

        await _access.EnsureCanAccessBeehiveAsync(inspection.BeehiveId);

        return _mapper.Map<InspectionDto>(inspection);
    }

    public async Task<InspectionDto> CreateAsync(CreateInspectionDto dto)
    {
        var beehive = await _uow.Beehives.GetByIdAsync(dto.BeehiveId)
            ?? throw new NotFoundException(nameof(Beehive), dto.BeehiveId);

        await _access.EnsureCanAccessBeehiveAsync(dto.BeehiveId);

        var inspection = _mapper.Map<Inspection>(dto);

        // Auto-populate temperature from the apiary's current weather.
        // Best-effort — a weather API failure never blocks saving an inspection.
        inspection.Temperature = await FetchCurrentTemperatureAsync(beehive.ApiaryId);

        await _uow.Inspections.AddAsync(inspection);
        await _uow.SaveChangesAsync();

        return _mapper.Map<InspectionDto>(inspection);
    }

    public async Task<InspectionDto> UpdateAsync(int id, UpdateInspectionDto dto)
    {
        var inspection = await _uow.Inspections.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Inspection), id);

        await _access.EnsureCanAccessBeehiveAsync(inspection.BeehiveId);

        if (!await _uow.Beehives.ExistsAsync(dto.BeehiveId))
            throw new NotFoundException(nameof(Beehive), dto.BeehiveId);

        if (dto.BeehiveId != inspection.BeehiveId)
            await _access.EnsureCanAccessBeehiveAsync(dto.BeehiveId);

        // Preserve the temperature that was captured automatically on creation.
        // The field is no longer user-editable so the DTO will carry null.
        var originalTemperature = inspection.Temperature;
        _mapper.Map(dto, inspection);
        inspection.Temperature = originalTemperature;
        inspection.UpdatedAt = DateTime.UtcNow;

        await _uow.Inspections.UpdateAsync(inspection);
        await _uow.SaveChangesAsync();

        return _mapper.Map<InspectionDto>(inspection);
    }

    public async Task DeleteAsync(int id)
    {
        var inspection = await _uow.Inspections.GetByIdAsync(id)
            ?? throw new NotFoundException(nameof(Inspection), id);

        await _access.EnsureCanAccessBeehiveAsync(inspection.BeehiveId);

        // Capture blob paths first — the FK cascade removes the photo rows with the inspection.
        var photos = await _uow.InspectionPhotos.GetByInspectionIdAsync(id);

        await _uow.Inspections.DeleteAsync(inspection);
        await _uow.SaveChangesAsync();

        // Blob removal is best-effort — a storage failure never blocks the delete (SPEC-05).
        foreach (var photo in photos)
        {
            try
            {
                await _storage.DeleteAsync(photo.StoragePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete stored file {StoragePath} while deleting inspection {InspectionId}", photo.StoragePath, id);
            }
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<double?> FetchCurrentTemperatureAsync(int apiaryId)
    {
        try
        {
            var apiary = await _uow.Apiaries.GetByIdAsync(apiaryId);
            if (apiary?.Latitude is null || apiary.Longitude is null)
                return null;

            return await _weather.GetCurrentTemperatureAsync(
                apiary.Latitude.Value, apiary.Longitude.Value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch current temperature for apiary {ApiaryId} — inspection saved without temperature", apiaryId);
            return null;
        }
    }
}
