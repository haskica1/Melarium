using Melarium.Domain.Common;
using Melarium.Domain.Entities;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>Yield attribution is the stats backbone of SPEC-10: a harvest belongs to the pasture
/// the apiary was on at harvest date (same-day move → the new pasture; nothing before → null).</summary>
public class PastureAttributionTests
{
    private static ApiaryMove Move(int id, int? toPastureId, string movedAt, string? createdAt = null) => new()
    {
        Id = id,
        ApiaryId = 1,
        ToPastureId = toPastureId,
        MovedAt = DateTime.Parse(movedAt),
        CreatedAt = DateTime.Parse(createdAt ?? movedAt),
    };

    private static readonly List<ApiaryMove> Timeline =
    [
        Move(1, toPastureId: 10, movedAt: "2026-04-01"),
        Move(2, toPastureId: 20, movedAt: "2026-06-15"),
    ];

    [Fact]
    public void BeforeFirstMove_ReturnsNull_MaticnaLokacija() =>
        Assert.Null(PastureAttribution.ResolveToPastureId(Timeline, DateTime.Parse("2026-03-20")));

    [Fact]
    public void BetweenMoves_ReturnsEarlierPasture() =>
        Assert.Equal(10, PastureAttribution.ResolveToPastureId(Timeline, DateTime.Parse("2026-05-10")));

    [Fact]
    public void SameDayAsMove_BelongsToTheNewPasture() =>
        Assert.Equal(20, PastureAttribution.ResolveToPastureId(Timeline, DateTime.Parse("2026-06-15")));

    [Fact]
    public void AfterLastMove_ReturnsLastPasture() =>
        Assert.Equal(20, PastureAttribution.ResolveToPastureId(Timeline, DateTime.Parse("2026-09-01")));

    [Fact]
    public void TwoMovesSameDay_LaterCreatedWins()
    {
        var moves = new List<ApiaryMove>
        {
            Move(1, toPastureId: 10, movedAt: "2026-06-15", createdAt: "2026-06-15T08:00:00"),
            Move(2, toPastureId: 20, movedAt: "2026-06-15", createdAt: "2026-06-15T19:00:00"),
        };

        Assert.Equal(20, PastureAttribution.ResolveToPastureId(moves, DateTime.Parse("2026-06-16")));
    }

    [Fact]
    public void NoMoves_ReturnsNull() =>
        Assert.Null(PastureAttribution.ResolveToPastureId([], DateTime.Parse("2026-06-16")));

    [Fact]
    public void AfterReturnHome_ReturnsNull_MaticnaLokacija()
    {
        var moves = new List<ApiaryMove>
        {
            Move(1, toPastureId: 10, movedAt: "2026-04-01"),
            Move(2, toPastureId: null, movedAt: "2026-07-01"), // returned to matična lokacija
        };

        Assert.Null(PastureAttribution.ResolveToPastureId(moves, DateTime.Parse("2026-08-01")));
    }
}
