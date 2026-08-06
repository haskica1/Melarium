namespace Melarium.Application.Features.Diets.DTOs;

/// <summary>Adds hives to a running programme. They join the schedule from now on; past rounds are
/// not retroactively attributed to them.</summary>
public class AddDietBeehivesDto
{
    public List<int> BeehiveIds { get; set; } = [];
}
