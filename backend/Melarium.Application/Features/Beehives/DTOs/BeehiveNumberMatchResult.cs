namespace Melarium.Application.Features.Beehives.DTOs;

/// <summary>
/// Result of resolving a number to beehives. <see cref="RecognizedNumber"/> is what was read
/// (null when OCR could not read a number); <see cref="Matches"/> is empty when nothing matched.
/// The frontend uses the two together to tell "couldn't read" from "read but no such hive".
/// </summary>
public class BeehiveNumberMatchResult
{
    public string? RecognizedNumber { get; set; }
    public IReadOnlyList<BeehiveMatchDto> Matches { get; set; } = new List<BeehiveMatchDto>();
}
