namespace Melarium.Application.Features.Diets.DTOs;

/// <summary>
/// Body for ticking a feeding round. The note is the escape hatch for the rare per-hive exception
/// ("košnice 7 i 12 preskočene"); ticking without one is the normal case, so an empty body is valid.
/// </summary>
public class CompleteFeedingEntryDto
{
    public string? Note { get; set; }
}
