using Melarium.Application.Features.Inspections.DTOs;

namespace Melarium.Application.Features.Beehives.DTOs;

/// <summary>Full beehive representation including its inspections and QR code.</summary>
public class BeehiveDetailDto : BeehiveDto
{
    // The QR PNG lives only on the detail DTO — on list DTOs it would add kilobytes per hive.
    public string? QrCodeBase64 { get; set; }
    public IEnumerable<InspectionDto> Inspections { get; set; } = new List<InspectionDto>();

    /// <summary>
    /// Id of the merge that took this hive out of its apiary — the row the undo endpoint takes
    /// (SPEC-19 §4). Null unless this hive was merged away.
    /// </summary>
    public int? MergeId { get; set; }

    /// <summary>
    /// Deadline of the 24-hour undo window, computed server-side so the client never derives it
    /// (SPEC-19 §6). Null when there is nothing to undo or the window already closed.
    /// </summary>
    public DateTime? CanUndoUntil { get; set; }
}
