using Melarium.Domain.Enums;

namespace Melarium.Application.Common.Seasons;

/// <summary>The season phase a local date falls into, with its bounds and what comes next (SPEC-29).</summary>
/// <param name="Start">First day of the phase.</param>
/// <param name="End">Last day of the phase, inclusive.</param>
public sealed record SeasonInfo(
    SeasonPhase Phase,
    DateOnly Start,
    DateOnly End,
    SeasonPhase NextPhase,
    DateOnly NextStart);
