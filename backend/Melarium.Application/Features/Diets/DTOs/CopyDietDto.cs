namespace Melarium.Application.Features.Diets.DTOs;

/// <summary>
/// Request to copy an existing diet's programme (definition + schedule) onto other beehives.
/// The source diet is identified by the route; only the target hives travel in the body.
/// </summary>
public class CopyDietDto
{
    /// <summary>Beehives that should receive a copy of the diet. The source hive is ignored if present.</summary>
    public List<int> TargetBeehiveIds { get; set; } = new();
}
