namespace Melarium.Application.Features.Reports.DTOs;

/// <summary>
/// The period a report is asked for (SPEC-25 D3). A free range rather than a fixed
/// year/quarter/month picker: the period a subsidy application asks for does not have to line up
/// with a calendar quarter. The quick picks on the page are just presets over this.
/// </summary>
public record SeasonReportQueryDto
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }

    /// <summary>Restrict to one apiary; null covers every apiary the caller can reach.</summary>
    public int? ApiaryId { get; init; }
}
