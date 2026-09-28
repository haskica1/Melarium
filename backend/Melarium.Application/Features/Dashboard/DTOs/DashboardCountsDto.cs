namespace Melarium.Application.Features.Dashboard.DTOs;

public class DashboardCountsDto
{
    public int Apiaries { get; set; }
    public int Beehives { get; set; }
    public int InspectionsThisMonth { get; set; }
    public decimal YieldThisYearKg { get; set; }

    /// <summary>Last year up to the same day — a fair comparison in September, unlike the whole of last year.</summary>
    public decimal YieldLastYearToDateKg { get; set; }

    public int OpenTodos { get; set; }
    public int OverdueTodos { get; set; }
}
