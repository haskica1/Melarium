namespace Melarium.Application.Features.Ai;

/// <summary>Vision-model client for beehive frame photo analysis (SPEC-05 Phase 2).</summary>
public interface IPhotoAnalysisAiClient
{
    /// <summary>
    /// Sends the image to the vision model and returns the structured Bosnian assessment.
    /// Throws <see cref="Common.Exceptions.BusinessRuleException"/> for images too large for
    /// the provider's request limit or when the model returns unparseable output.
    /// </summary>
    Task<PhotoAnalysisResult> AnalyzeFrameAsync(byte[] imageBytes, string contentType, CancellationToken cancellationToken = default);
}
