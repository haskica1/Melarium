using Melarium.Application.Features.Reports.DTOs;

namespace Melarium.Application.Features.Reports;

public interface IReportService
{
    /// <summary>
    /// One consolidated report for the requested period, scoped to the apiaries the caller can reach
    /// (SPEC-25). Throws <c>ForbiddenAccessException</c> for a caller without an organization, and
    /// for an <c>ApiaryId</c> outside their scope.
    /// </summary>
    Task<SeasonReportDto> GetSeasonReportAsync(SeasonReportQueryDto query);
}
