using Melarium.Application.Features.Ai;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// Tolerant parsing of the vision model's OCR JSON for the "scan by number" flow: <c>number</c> may
/// arrive as a string or a JSON number; anything unreadable/malformed yields a null number, not a throw.
/// </summary>
public class HiveNumberOcrParsingTests
{
    [Fact]
    public void Parse_StringNumber_IsRead()
    {
        var r = GroqHiveNumberOcrClient.Parse("""{ "number": "7", "confidence": 0.92 }""");
        Assert.Equal("7", r.Number);
        Assert.Equal(0.92, r.Confidence);
    }

    [Fact]
    public void Parse_NumericNumber_IsCoercedToString()
    {
        var r = GroqHiveNumberOcrClient.Parse("""{ "number": 12, "confidence": 1 }""");
        Assert.Equal("12", r.Number);
        Assert.Equal(1.0, r.Confidence);
    }

    [Theory]
    [InlineData("""{ "number": null, "confidence": 0 }""")]
    [InlineData("""{ "confidence": 0.5 }""")]
    [InlineData("""{ "number": "   " }""")]
    public void Parse_NoNumber_IsNull(string json)
        => Assert.Null(GroqHiveNumberOcrClient.Parse(json).Number);

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{ \"number\": ")]
    [InlineData("null")]
    [InlineData("\"7\"")]
    public void Parse_MalformedOrNonObject_YieldsEmptyResult(string malformed)
    {
        var r = GroqHiveNumberOcrClient.Parse(malformed);
        Assert.Null(r.Number);
        Assert.Null(r.Confidence);
    }

    [Fact]
    public void Parse_TrimsWhitespace()
        => Assert.Equal("A3", GroqHiveNumberOcrClient.Parse("""{ "number": "  A3 " }""").Number);
}
