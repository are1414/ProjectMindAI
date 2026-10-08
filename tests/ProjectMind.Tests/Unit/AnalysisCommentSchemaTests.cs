using System.Text.Json;
using ProjectMind.Application.Ai;
using ProjectMind.Infrastructure.Ai;

namespace ProjectMind.Tests.Unit;

public class AnalysisCommentSchemaTests
{
    private const string Valid = """
        {"summary":" Proje 2 gün geride. ","keyFindings":["SPI(t) 0,92"," "],"recommendedActions":["B işine destek verin"],"caveats":[]}
        """;

    [Fact]
    public void Valid_answer_is_parsed_and_trimmed_and_blank_items_dropped()
    {
        var comment = AnalysisCommentSchema.TryParse(Valid, out var error);

        Assert.Null(error);
        Assert.NotNull(comment);
        Assert.Equal("Proje 2 gün geride.", comment.Summary);
        Assert.Equal(["SPI(t) 0,92"], comment.KeyFindings);
        Assert.Equal(["B işine destek verin"], comment.RecommendedActions);
        Assert.Empty(comment.Caveats);
        Assert.Equal("Proje 2 gün geride.\nSPI(t) 0,92\nB işine destek verin", comment.AllText());
    }

    [Theory]
    [InlineData("", "cevap boş")]
    [InlineData("Proje iyi gidiyor.", "cevap geçerli JSON değil")]
    [InlineData("[1,2]", "cevap bir JSON nesnesi değil")]
    [InlineData("""{"keyFindings":[],"recommendedActions":[],"caveats":[]}""", "'summary' alanı eksik veya boş")]
    [InlineData("""{"summary":"  ","keyFindings":[],"recommendedActions":[],"caveats":[]}""", "'summary' alanı eksik veya boş")]
    [InlineData("""{"summary":"x","recommendedActions":[],"caveats":[]}""", "'keyFindings' alanı eksik veya liste değil")]
    [InlineData("""{"summary":"x","keyFindings":"tek","recommendedActions":[],"caveats":[]}""", "'keyFindings' alanı eksik veya liste değil")]
    [InlineData("""{"summary":"x","keyFindings":[3],"recommendedActions":[],"caveats":[]}""", "'keyFindings' listesinde metin olmayan madde var")]
    [InlineData("""{"summary":"x","keyFindings":[],"recommendedActions":[],"caveats":[],"score":87}""", "şemada olmayan alan: score")]
    public void Invalid_answers_are_rejected_with_reason(string json, string expected)
    {
        var comment = AnalysisCommentSchema.TryParse(json, out var error);

        Assert.Null(comment);
        Assert.Equal(expected, error);
    }

    [Fact]
    public void Too_many_items_or_too_long_texts_are_rejected()
    {
        var many = JsonSerializer.Serialize(new
        {
            summary = "x",
            keyFindings = Enumerable.Repeat("b", AnalysisCommentSchema.MaxItemsPerList + 1),
            recommendedActions = Array.Empty<string>(),
            caveats = Array.Empty<string>()
        });
        var longSummary = JsonSerializer.Serialize(new
        {
            summary = new string('a', AnalysisCommentSchema.MaxSummaryLength + 1),
            keyFindings = Array.Empty<string>(), recommendedActions = Array.Empty<string>(), caveats = Array.Empty<string>()
        });

        Assert.Null(AnalysisCommentSchema.TryParse(many, out _));
        Assert.Null(AnalysisCommentSchema.TryParse(longSummary, out _));
    }

    [Fact]
    public void Claude_output_format_carries_the_same_schema()
    {
        var format = ClaudeChatModel.ToOutputFormat(AnalysisCommentSchema.Schema);
        var json = JsonSerializer.SerializeToElement(format);

        Assert.Equal("json_schema", json.GetProperty("type").GetString());
        Assert.Equal("object", json.GetProperty("schema").GetProperty("type").GetString());
        Assert.Equal(4, json.GetProperty("schema").GetProperty("required").GetArrayLength());
    }
}
