namespace Melarium.Application.Features.Learning.DTOs;

/// <summary>Input for the AI draft assist (Phase 2) — the admin edits and publishes manually.</summary>
public class GenerateDraftDto
{
    public string Title { get; set; } = string.Empty;
    public string? Outline { get; set; }
}
