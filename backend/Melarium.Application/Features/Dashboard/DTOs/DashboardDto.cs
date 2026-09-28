namespace Melarium.Application.Features.Dashboard.DTOs;

/// <summary>
/// The start page (SPEC-29): everything is scoped to what the caller may see — a Beekeeper sees their
/// hives, an ApiaryAdmin their apiary — and nothing the plan has locked (SPEC-24). Weather comes from
/// its own endpoint so a slow forecast never holds up the rest of the page.
/// </summary>
public class DashboardDto
{
    public DashboardSeasonDto Season { get; set; } = null!;
    public DashboardCountsDto Counts { get; set; } = null!;

    /// <summary>What needs doing now, by the same rules and season as the alerts, grouped per apiary.</summary>
    public IReadOnlyList<AttentionItemDto> Attention { get; set; } = [];

    /// <summary>Today and the next seven days, from the same source as the agenda and the ICS feed.</summary>
    public IReadOnlyList<DashboardObligationDto> Obligations { get; set; } = [];

    /// <summary>True in winter: inspections are not tracked, so the hive-status chart has nothing to say.</summary>
    public bool HivesResting { get; set; }
    public IReadOnlyList<HiveStatusDto> HiveStatus { get; set; } = [];

    public IReadOnlyList<ProgrammeDto> Programmes { get; set; } = [];
    public IReadOnlyList<MonthYieldDto> YieldByMonth { get; set; } = [];
    public IReadOnlyList<DashboardTodoDto> OpenTodos { get; set; } = [];
    public DashboardTopicDto? Topic { get; set; }
}
