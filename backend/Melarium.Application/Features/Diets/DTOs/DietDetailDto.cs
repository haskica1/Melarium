namespace Melarium.Application.Features.Diets.DTOs;

public class DietDetailDto : DietDto
{
    public List<FeedingEntryDto> FeedingEntries { get; set; } = new();

    /// <summary>
    /// Every hive link, active and removed. Removed ones stay so the detail page can show that a hive
    /// was on the programme until a given date; only the active ones count towards <c>HiveCount</c>.
    /// </summary>
    public List<DietBeehiveDto> Beehives { get; set; } = new();
}
