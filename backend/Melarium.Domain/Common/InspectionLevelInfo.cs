using Melarium.Domain.Enums;

namespace Melarium.Domain.Common;

/// <summary>The three inspection columns the dashboard needs — no notes, no photos (SPEC-29).</summary>
public sealed record InspectionLevelInfo(int BeehiveId, DateTime Date, HoneyLevel HoneyLevel);
