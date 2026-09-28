using Melarium.Application.Features.Dashboard.DTOs;

namespace Melarium.Application.Features.Dashboard;

/// <summary>The start page of every role except SystemAdmin (SPEC-29).</summary>
public interface IDashboardService
{
    Task<DashboardDto> GetAsync();

    /// <summary>Separate from <see cref="GetAsync"/> so a slow or unreachable forecast never holds up the page.</summary>
    Task<IReadOnlyList<ApiaryWeatherDto>> GetWeatherAsync();
}
