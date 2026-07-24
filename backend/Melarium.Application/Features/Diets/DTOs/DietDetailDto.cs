namespace Melarium.Application.Features.Diets.DTOs;

public class DietDetailDto : DietDto
{
    public List<FeedingEntryDto> FeedingEntries { get; set; } = new();
}
