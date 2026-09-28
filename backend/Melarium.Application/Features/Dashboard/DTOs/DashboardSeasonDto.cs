using Melarium.Application.Common.Seasons;
using Melarium.Domain.Enums;

namespace Melarium.Application.Features.Dashboard.DTOs;

/// <summary>The organization's current season phase and the steps of its year (SPEC-29).</summary>
public class DashboardSeasonDto
{
    public SeasonPhase Phase { get; set; }
    public DateOnly Start { get; set; }
    public DateOnly End { get; set; }
    public SeasonPhase NextPhase { get; set; }
    public DateOnly NextStart { get; set; }
    public int ShiftDays { get; set; }
    public IReadOnlyList<SeasonPhaseRange> Phases { get; set; } = [];
}
