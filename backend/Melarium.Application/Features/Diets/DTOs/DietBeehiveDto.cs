namespace Melarium.Application.Features.Diets.DTOs;

/// <summary>One hive's membership in a feeding programme, including hives already removed from it.</summary>
public class DietBeehiveDto
{
    public int Id { get; set; }
    public int BeehiveId { get; set; }
    public string BeehiveName { get; set; } = string.Empty;

    /// <summary>When the hive joined the programme.</summary>
    public DateTime AddedOn { get; set; }

    /// <summary>Null while the hive is still on the programme.</summary>
    public DateTime? RemovedOn { get; set; }
}
