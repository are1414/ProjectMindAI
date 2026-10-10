namespace ProjectMind.Application.Evaluation;

/// <summary>Betimsel istatistik: n, ortalama, örneklem standart sapması (n − 1), en küçük, en büyük.</summary>
public sealed record DescriptiveStats(int N, decimal? Mean, decimal? StdDev, decimal? Min, decimal? Max)
{
    public static DescriptiveStats Of(IEnumerable<decimal> values)
    {
        var v = values.ToList();
        if (v.Count == 0)
            return new DescriptiveStats(0, null, null, null, null);

        var mean = v.Average();
        decimal? sd = null;
        if (v.Count > 1)
        {
            var variance = v.Sum(x => (x - mean) * (x - mean)) / (v.Count - 1);
            sd = (decimal)Math.Sqrt((double)variance);
        }

        return new DescriptiveStats(v.Count, mean, sd, v.Min(), v.Max());
    }
}

/// <summary>Bir Likert maddesinin özeti: istatistik, 1–5 cevap dağılımı ve katılma payı (4 veya 5).</summary>
public sealed record LikertSummary(string ItemCode, string Text, DescriptiveStats Stats, IReadOnlyList<int> Distribution, decimal? AgreeShare)
{
    /// <summary>"Katılıyorum" sayılan en küçük cevap (5'li ölçekte 4).</summary>
    public const int AgreeThreshold = 4;

    public static LikertSummary Of(string itemCode, string text, IEnumerable<int> answers)
    {
        var a = answers.ToList();
        var distribution = Enumerable.Range(SusScore.MinAnswer, SusScore.MaxAnswer - SusScore.MinAnswer + 1)
            .Select(v => a.Count(x => x == v)).ToList();
        decimal? agree = a.Count == 0 ? null : (decimal)a.Count(x => x >= AgreeThreshold) / a.Count;
        return new LikertSummary(itemCode, text, DescriptiveStats.Of(a.Select(x => (decimal)x)), distribution, agree);
    }
}
