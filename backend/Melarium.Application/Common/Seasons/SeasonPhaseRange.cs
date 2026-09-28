using Melarium.Domain.Enums;

namespace Melarium.Application.Common.Seasons;

/// <summary>One phase of a beekeeping year as concrete dates; <paramref name="End"/> is inclusive.</summary>
public sealed record SeasonPhaseRange(SeasonPhase Phase, DateOnly Start, DateOnly End);
