namespace Melarium.Application.Features.Dashboard.DTOs;

/// <summary>Work in progress on an apiary: a feeding programme, a multi-round treatment, strips, a karenca.</summary>
/// <param name="Kind"><c>Feeding</c>, <c>TreatmentRounds</c>, <c>Strips</c> or <c>Karenca</c>.</param>
/// <param name="Done">Rounds done, for the two kinds that have rounds.</param>
/// <param name="Date">Next round, remove-by date for strips, or the day honey may be harvested again.</param>
public record ProgrammeDto(
    string Kind,
    int Id,
    string ApiaryName,
    string Title,
    int? Done,
    int? Total,
    DateOnly? Date,
    string LinkPath);
