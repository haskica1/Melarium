using Melarium.Domain.Common;
using Melarium.Domain.Enums;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// Locks the international queen-marking color code:
/// years ending 1/6 → White, 2/7 → Yellow, 3/8 → Red, 4/9 → Green, 5/0 → Blue.
/// </summary>
public class QueenMarkColorHelperTests
{
    [Theory]
    [InlineData(2020, QueenMarkColor.Blue)]
    [InlineData(2021, QueenMarkColor.White)]
    [InlineData(2022, QueenMarkColor.Yellow)]
    [InlineData(2023, QueenMarkColor.Red)]
    [InlineData(2024, QueenMarkColor.Green)]
    [InlineData(2025, QueenMarkColor.Blue)]
    [InlineData(2026, QueenMarkColor.White)]
    [InlineData(2027, QueenMarkColor.Yellow)]
    [InlineData(2028, QueenMarkColor.Red)]
    [InlineData(2029, QueenMarkColor.Green)]
    public void ForYear_ReturnsInternationalColor(int year, QueenMarkColor expected)
    {
        Assert.Equal(expected, QueenMarkColorHelper.ForYear(year));
    }
}
