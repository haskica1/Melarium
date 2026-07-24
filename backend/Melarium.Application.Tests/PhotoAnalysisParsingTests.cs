using Melarium.Application.Common.Exceptions;
using Melarium.Application.Features.Ai;
using Xunit;

namespace Melarium.Application.Tests;

/// <summary>
/// Robust parsing of the vision model's JSON (SPEC-05 Phase 2): malformed output becomes a
/// Bosnian error, non-frame photos empty out the assessment, out-of-range scores are dropped.
/// </summary>
public class PhotoAnalysisParsingTests
{
    [Fact]
    public void Parse_ValidFrameAnalysis_MapsAllFields()
    {
        var result = GroqPhotoAnalysisAiClient.ParseAnalysis(
            """
            {
              "isFramePhoto": true,
              "broodPattern": 4,
              "queenCellsVisible": false,
              "anomalies": ["moguće krečno leglo", ""],
              "summary": "Leglo je kompaktno."
            }
            """);

        Assert.True(result.IsFramePhoto);
        Assert.Equal(4, result.BroodPattern);
        Assert.False(result.QueenCellsVisible);
        Assert.Single(result.Anomalies); // blank entries are filtered out
        Assert.Equal("moguće krečno leglo", result.Anomalies[0]);
        Assert.Equal("Leglo je kompaktno.", result.Summary);
    }

    [Fact]
    public void Parse_NonFramePhoto_EmptiesAssessmentFields()
    {
        var result = GroqPhotoAnalysisAiClient.ParseAnalysis(
            """{ "isFramePhoto": false, "broodPattern": 3, "queenCellsVisible": true, "anomalies": ["x"], "summary": "Ovo nije okvir." }""");

        Assert.False(result.IsFramePhoto);
        Assert.Null(result.BroodPattern);
        Assert.Null(result.QueenCellsVisible);
        Assert.Empty(result.Anomalies);
        Assert.Equal("Ovo nije okvir.", result.Summary);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Parse_OutOfRangeBroodPattern_IsDropped(int score)
    {
        var result = GroqPhotoAnalysisAiClient.ParseAnalysis(
            $$"""{ "isFramePhoto": true, "broodPattern": {{score}}, "anomalies": [], "summary": null }""");

        Assert.Null(result.BroodPattern);
    }

    [Fact]
    public void Parse_MissingAnomalies_BecomesEmptyList()
    {
        var result = GroqPhotoAnalysisAiClient.ParseAnalysis(
            """{ "isFramePhoto": true, "broodPattern": 2 }""");

        Assert.NotNull(result.Anomalies);
        Assert.Empty(result.Anomalies);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{ \"isFramePhoto\": tru")]
    [InlineData("null")]
    public void Parse_MalformedOutput_ThrowsBosnianBusinessRule(string malformed)
    {
        var ex = Assert.Throws<BusinessRuleException>(() =>
            GroqPhotoAnalysisAiClient.ParseAnalysis(malformed));

        Assert.Contains("AI analiza nije uspjela", ex.Message);
    }
}
