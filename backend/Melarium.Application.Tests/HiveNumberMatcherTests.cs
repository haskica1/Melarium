using Melarium.Application.Features.Beehives;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// Canonical matching of scanned/typed numbers to hives: a set LabelNumber is authoritative,
/// hives without one fall back to a number parsed from the name; "1", "01", "007" all canonicalise.
/// </summary>
public class HiveNumberMatcherTests
{
    [Theory]
    [InlineData("1", "1")]
    [InlineData(" 1 ", "1")]
    [InlineData("01", "1")]
    [InlineData("007", "7")]
    [InlineData("000", "0")]
    [InlineData("a3", "A3")]
    [InlineData("A3", "A3")]
    public void Normalize_CanonicalisesNumbersAndLabels(string raw, string expected)
        => Assert.Equal(expected, HiveNumberMatcher.Normalize(raw));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_BlankIsNull(string? raw)
        => Assert.Null(HiveNumberMatcher.Normalize(raw));

    [Fact]
    public void NameNumberTokens_ExtractsAllGroupsCanonicalised()
        => Assert.Equal(new[] { "2", "1" }, HiveNumberMatcher.NameNumberTokens("Grupa 2 - košnica 01"));

    [Fact]
    public void NameNumberTokens_NoDigits_IsEmpty()
        => Assert.Empty(HiveNumberMatcher.NameNumberTokens("Košnica bez broja"));

    [Theory]
    [InlineData("Košnica 5", "5")]
    [InlineData("2. red, košnica 7", "7")]
    [InlineData("12", "12")]
    [InlineData("Bez broja", null)]
    public void PrimaryNameNumber_TakesLastGroup(string name, string? expected)
        => Assert.Equal(expected, HiveNumberMatcher.PrimaryNameNumber(name));

    [Fact]
    public void Matches_LabelNumber_IsAuthoritative_NoNameFallback()
    {
        // Label "5" set while the name mentions "9": only "5" matches, never the name's "9".
        Assert.True(HiveNumberMatcher.Matches("5", "Košnica 9", "5"));
        Assert.False(HiveNumberMatcher.Matches("5", "Košnica 9", "9"));
    }

    [Fact]
    public void Matches_LabelNumber_LeadingZeroInsensitive()
        => Assert.True(HiveNumberMatcher.Matches("05", "Bilo šta", "5"));

    [Fact]
    public void Matches_NoLabel_FallsBackToAnyNameToken()
    {
        Assert.True(HiveNumberMatcher.Matches(null, "Grupa 2 - košnica 1", "1"));
        Assert.True(HiveNumberMatcher.Matches(null, "Grupa 2 - košnica 1", "2"));
        Assert.False(HiveNumberMatcher.Matches(null, "Grupa 2 - košnica 1", "3"));
    }

    [Fact]
    public void Matches_BlankLabel_FallsBackToName()
        => Assert.True(HiveNumberMatcher.Matches("   ", "Košnica 8", "8"));
}
