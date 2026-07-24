using Melarium.Application.Features.Alerts;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>The weekly digest is the only source of truth handed to the LLM, so it must be a
/// deterministic, faithful rendering of the gathered facts (SPEC-04 Part B).</summary>
public class WeeklyDigestBuilderTests
{
    private static WeeklyDigestInput Input(
        int inspections = 0, int feedings = 0, int created = 0, int completed = 0,
        int overdue = 0, decimal kg = 0m) =>
        new("Med d.o.o.", inspections, ["Košnica 1 (01.07): Matica uočena"], feedings,
            created, completed, overdue, kg, ["Pčelinjak A: 2 pregleda, zadnji nivo meda: nizak"],
            ["Pčelinjak A: 8–20°C, kiša 40%"]);

    [Fact]
    public void Build_IncludesAllCountsAndSections()
    {
        var text = WeeklyDigestBuilder.Build(Input(inspections: 3, feedings: 2, created: 4, completed: 1, overdue: 2, kg: 12.5m));

        Assert.Contains("Med d.o.o.", text);
        Assert.Contains("pregledi=3", text);
        Assert.Contains("hranjenja=2", text);
        Assert.Contains("zakašnjeli zadaci=2", text);
        Assert.Contains("prinos meda=12.5 kg", text);
        Assert.Contains("Matica uočena", text);
        Assert.Contains("Vremenska prognoza:", text);
    }

    [Fact]
    public void HasActivity_FalseWhenEverythingZero()
    {
        Assert.False(Input().HasActivity);
    }

    [Theory]
    [InlineData(1, 0, 0, 0, 0.0)]
    [InlineData(0, 0, 0, 0, 5.0)]
    [InlineData(0, 3, 0, 0, 0.0)]
    public void HasActivity_TrueWhenAnySignalPresent(int insp, int feed, int created, int completed, double kg)
    {
        Assert.True(Input(inspections: insp, feedings: feed, created: created, completed: completed, kg: (decimal)kg).HasActivity);
    }
}
