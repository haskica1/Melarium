namespace Melarium.Application.Features.Beehives.DTOs;

/// <summary>Minimal public DTO returned by the unauthenticated QR scan lookup.</summary>
public class BeehiveScanDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ApiaryId { get; set; }

    /// <summary>
    /// Set when this hive's colony was merged away (SPEC-19). The sticker stays on the emptied box
    /// until the beekeeper replaces it, so the scan resolves and says where the colony went instead
    /// of looking like a broken code.
    /// </summary>
    public int? MergedIntoBeehiveId { get; set; }
    public string? MergedIntoBeehiveName { get; set; }
    public DateTime? MergedAt { get; set; }
}
