namespace Melarium.Application.Features.Ai;

/// <summary>
/// Structured result of the AI frame analysis (SPEC-05 Phase 2). Serialized (camelCase)
/// into <c>InspectionPhoto.AnalysisJson</c> and returned to the frontend as-is.
/// </summary>
public class PhotoAnalysisResult
{
    /// <summary>False when the photo does not look like a beehive frame/comb — the rest stays null.</summary>
    public bool IsFramePhoto { get; set; }

    /// <summary>Brood pattern compactness/coverage, 1–5 (5 = excellent), null when not assessable.</summary>
    public int? BroodPattern { get; set; }

    public bool? QueenCellsVisible { get; set; }

    /// <summary>Short Bosnian phrases, observations only (never diagnoses). Empty when none.</summary>
    public List<string> Anomalies { get; set; } = [];

    /// <summary>2–3 sentence Bosnian summary.</summary>
    public string? Summary { get; set; }
}
