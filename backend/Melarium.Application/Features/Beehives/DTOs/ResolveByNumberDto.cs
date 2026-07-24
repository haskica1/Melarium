namespace Melarium.Application.Features.Beehives.DTOs;

/// <summary>Request body for resolving an on-device–recognised number to beehives (no image upload).</summary>
public class ResolveByNumberDto
{
    public string Number { get; set; } = string.Empty;
}
