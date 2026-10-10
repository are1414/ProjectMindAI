using ProjectMind.Application.Common;
using ProjectMind.Application.Evaluation;

namespace ProjectMind.Tests.Unit;

/// <summary>SUS puanı, Bangor sıfatı, betimsel istatistik, Likert özeti ve CSV alan kaçışı (elle hesaplanmış örnekler).</summary>
public class EvaluationMathTests
{
    [Fact]
    public void Sus_all_neutral_answers_score_50()
    {
        // Tek maddeler 3−1 = 2, çift maddeler 5−3 = 2 → 10 × 2 = 20 → × 2,5 = 50.
        Assert.Equal(50m, SusScore.Compute([3, 3, 3, 3, 3, 3, 3, 3, 3, 3]));
    }

    [Fact]
    public void Sus_best_answers_score_100_and_worst_score_0()
    {
        Assert.Equal(100m, SusScore.Compute([5, 1, 5, 1, 5, 1, 5, 1, 5, 1]));
        Assert.Equal(0m, SusScore.Compute([1, 5, 1, 5, 1, 5, 1, 5, 1, 5]));
    }

    [Theory]
    // Tek maddeler 4,5,4,4,3 → 3+4+3+3+2 = 15; çift maddeler 2,1,2,2,3 → 3+4+3+3+2 = 15; 30 × 2,5 = 75.
    [InlineData(new[] { 4, 2, 5, 1, 4, 2, 4, 2, 3, 3 }, 75.0)]
    // Tek maddeler 3,2,3,2,3 → 2+1+2+1+2 = 8; çift maddeler 4,3,4,3,4 → 1+2+1+2+1 = 7; 15 × 2,5 = 37,5.
    [InlineData(new[] { 3, 4, 2, 3, 3, 4, 2, 3, 3, 4 }, 37.5)]
    public void Sus_mixed_answers_match_hand_calculation(int[] answers, double expected)
    {
        Assert.Equal((decimal)expected, SusScore.Compute(answers));
    }

    [Fact]
    public void Sus_rejects_missing_or_out_of_range_answers()
    {
        var missing = Assert.Throws<BusinessRuleException>(() => SusScore.Compute([3, 3, 3, 3, 3, 3, 3, 3, 3]));
        Assert.Contains("10 maddenin hepsi", missing.Message);
        var range = Assert.Throws<BusinessRuleException>(() => SusScore.Compute([3, 3, 3, 3, 3, 3, 0, 3, 3, 3]));
        Assert.Contains("SUS7", range.Message);
        Assert.Throws<BusinessRuleException>(() => SusScore.Compute([3, 3, 3, 3, 3, 3, 3, 3, 3, 6]));
    }

    [Theory]
    [InlineData(100.0, "Hayal edilebilecek en iyi")]
    [InlineData(90.9, "Hayal edilebilecek en iyi")]
    [InlineData(90.8, "Mükemmel")]
    [InlineData(85.5, "Mükemmel")]
    [InlineData(75.0, "İyi")]
    [InlineData(71.4, "İyi")]
    [InlineData(68.0, "Fena değil (OK)")]
    [InlineData(50.9, "Fena değil (OK)")]
    [InlineData(50.8, "Zayıf")]
    [InlineData(35.7, "Zayıf")]
    [InlineData(25.0, "Berbat")]
    [InlineData(0.0, "Hayal edilebilecek en kötü")]
    public void Bangor_adjective_bands(double score, string expected)
    {
        Assert.Equal(expected, SusScore.Adjective((decimal)score));
    }

    [Fact]
    public void Descriptive_stats_use_sample_standard_deviation()
    {
        // 1..5: ortalama 3, kareli sapmalar toplamı 4+1+0+1+4 = 10, örneklem varyansı 10/4 = 2,5, SS = √2,5 ≈ 1,5811.
        var s = DescriptiveStats.Of([1m, 2m, 3m, 4m, 5m]);
        Assert.Equal(5, s.N);
        Assert.Equal(3m, s.Mean);
        Assert.Equal(1.5811m, Math.Round(s.StdDev!.Value, 4));
        Assert.Equal(1m, s.Min);
        Assert.Equal(5m, s.Max);

        // 75 ve 37,5: ortalama 56,25; sapmalar ±18,75 → varyans 2 × 351,5625 / 1 = 703,125; SS ≈ 26,5165.
        var two = DescriptiveStats.Of([75m, 37.5m]);
        Assert.Equal(56.25m, two.Mean);
        Assert.Equal(26.5165m, Math.Round(two.StdDev!.Value, 4));

        var one = DescriptiveStats.Of([50m]);
        Assert.Equal(50m, one.Mean);
        Assert.Null(one.StdDev);

        var none = DescriptiveStats.Of([]);
        Assert.Equal(0, none.N);
        Assert.Null(none.Mean);
    }

    [Fact]
    public void Likert_summary_counts_distribution_and_agreement()
    {
        // 5,4,4,2,1 → dağılım (1..5) = 1,1,0,2,1; ortalama 16/5 = 3,2; katılıyor (4–5) = 3/5 = 0,6.
        var l = LikertSummary.Of("RQ3_1", "x", [5, 4, 4, 2, 1]);
        Assert.Equal([1, 1, 0, 2, 1], l.Distribution);
        Assert.Equal(3.2m, l.Stats.Mean);
        Assert.Equal(0.6m, l.AgreeShare);

        var empty = LikertSummary.Of("RQ3_1", "x", []);
        Assert.Equal([0, 0, 0, 0, 0], empty.Distribution);
        Assert.Null(empty.AgreeShare);
    }

    [Fact]
    public void Csv_quotes_only_when_needed_and_starts_with_bom()
    {
        Assert.Equal("abc", Csv.Field("abc"));
        Assert.Equal("\"a,b\"", Csv.Field("a,b"));
        Assert.Equal("\"say \"\"merhaba\"\"\"", Csv.Field("say \"merhaba\""));
        Assert.Equal("", Csv.Field(null));
        Assert.Equal("0.875", Csv.Num(0.875m));
        Assert.Equal("", Csv.Num((double?)double.NaN));

        var text = Csv.Build(["a", "b"], [["1", "x,y"]]);
        Assert.Equal("﻿a,b\r\n1,\"x,y\"\r\n", text);
        Assert.Throws<InvalidOperationException>(() => Csv.Build(["a", "b"], [["1"]]));
    }

    [Fact]
    public void Questionnaire_has_ten_sus_items_and_two_likert_items_per_rq()
    {
        Assert.Equal(SusScore.ItemCount, SurveyQuestionnaire.SusItems.Count);
        Assert.Equal(Enumerable.Range(1, 10).Select(i => $"SUS{i}"), SurveyQuestionnaire.SusItems.Select(i => i.Code));
        Assert.Equal(2, SurveyQuestionnaire.LikertItems.Count(i => i.Group == SurveyItemGroup.Rq3));
        Assert.Equal(2, SurveyQuestionnaire.LikertItems.Count(i => i.Group == SurveyItemGroup.Rq4));
        Assert.Equal(SurveyQuestionnaire.AllItems.Count, SurveyQuestionnaire.AllItems.Select(i => i.Code).Distinct().Count());
        Assert.True(SurveyQuestionnaire.IsValidParticipantCode("P01"));
        Assert.True(SurveyQuestionnaire.IsValidParticipantCode("P105"));
        Assert.False(SurveyQuestionnaire.IsValidParticipantCode("Ahmet"));
        Assert.False(SurveyQuestionnaire.IsValidParticipantCode("a@b.com"));
        Assert.False(SurveyQuestionnaire.IsValidParticipantCode("P1"));
    }
}
