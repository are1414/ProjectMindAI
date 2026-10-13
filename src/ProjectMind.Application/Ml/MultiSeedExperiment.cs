using System.Globalization;
using System.Text;

namespace ProjectMind.Application.Ml;

/// <summary>Bir ölçütün tohumlar üzerindeki özeti: ortalama, örneklem standart sapması (n − 1) ve n (NaN değerler hariç).</summary>
public sealed record MetricSummary(double? Mean, double? Sd, int N)
{
    public static MetricSummary Of(IEnumerable<double?> values)
    {
        var v = values.Where(x => x is { } d && !double.IsNaN(d)).Select(x => x!.Value).ToList();
        if (v.Count == 0)
            return new MetricSummary(null, null, 0);
        var mean = v.Average();
        double? sd = v.Count > 1 ? Math.Sqrt(v.Sum(x => (x - mean) * (x - mean)) / (v.Count - 1)) : null;
        return new MetricSummary(mean, sd, v.Count);
    }
}

public sealed record MultiSeedRow(string Method, int Checkpoint, MetricSummary F1, MetricSummary Auc, MetricSummary Accuracy);

public sealed record MultiSeedEarlyWarningRow(
    string Method, MetricSummary DetectedShare, MetricSummary MeanLeadWeeks, MetricSummary MeanFirstCheckpoint, MetricSummary FalseAlarmShare);

/// <summary>
/// RQ1/RQ2 sağlamlık analizi (D31): aynı deney N farklı tohumla (veri üretimi, eğitim/test ayrımı ve öğrenici tohumu birlikte)
/// çalıştırılır; her yöntem ve kontrol noktası için F1 / AUC / doğruluk ortalaması ± standart sapması verilir.
/// "Seçilen ML" satırı, her tohumda en yüksek test AUC'li sınıflandırıcının değerleridir (seçim tohuma göre değişebilir).
/// Üretim modeli değişmez.
/// </summary>
public sealed record MultiSeedReport(
    IReadOnlyList<int> Seeds, int ProjectsPerSeed, IReadOnlyList<MultiSeedRow> Rows, IReadOnlyList<MultiSeedEarlyWarningRow> EarlyWarning,
    IReadOnlyList<string> SelectedPerSeed, DateTimeOffset CreatedAt)
{
    public const string SelectedMlMethod = "Seçilen ML (en yüksek AUC)";

    public static MultiSeedReport Aggregate(IReadOnlyList<DelayExperimentReport> reports, DateTimeOffset now)
    {
        if (reports.Count == 0)
            throw new ArgumentException("En az bir deney raporu gerekli.", nameof(reports));

        // Her raporun satırlarına "seçilen ML" kopyası eklenir; sonra yöntem × kontrol noktası bazında toplanır.
        var evaluations = reports.SelectMany(r => r.Evaluations.Concat(
            r.Evaluations.Where(e => e.Method == r.SelectedClassifier).Select(e => e with { Method = SelectedMlMethod }))).ToList();
        var methodOrder = reports[0].Evaluations.Select(e => e.Method).Distinct().Append(SelectedMlMethod).ToList();

        var rows = evaluations.GroupBy(e => (e.Method, e.Checkpoint))
            .OrderBy(g => g.Key.Checkpoint == DelayExperimentReport.AllCheckpoints ? int.MaxValue : g.Key.Checkpoint)
            .ThenBy(g => Order(methodOrder, g.Key.Method))
            .Select(g => new MultiSeedRow(g.Key.Method, g.Key.Checkpoint,
                MetricSummary.Of(g.Select(e => (double?)e.Metrics.F1)),
                MetricSummary.Of(g.Select(e => (double?)e.Metrics.Auc)),
                MetricSummary.Of(g.Select(e => (double?)e.Metrics.Accuracy))))
            .ToList();

        var warnings = reports.SelectMany(r => (r.EarlyWarning ?? []).Concat(
            (r.EarlyWarning ?? []).Where(w => w.Method == r.SelectedClassifier).Select(w => w with { Method = SelectedMlMethod }))).ToList();
        var early = warnings.GroupBy(w => w.Method)
            .OrderBy(g => Order(methodOrder, g.Key))
            .Select(g => new MultiSeedEarlyWarningRow(g.Key,
                MetricSummary.Of(g.Select(w => w.DetectedShare)),
                MetricSummary.Of(g.Select(w => w.MeanLeadWeeks)),
                MetricSummary.Of(g.Select(w => w.MeanFirstCheckpoint)),
                MetricSummary.Of(g.Select(w => w.FalseAlarmShare))))
            .ToList();

        return new MultiSeedReport(reports.Select(r => r.Synthetic.Seed).ToList(), reports[0].Synthetic.Projects, rows, early,
            reports.Select(r => r.SelectedClassifier).ToList(), now);
    }

    private static int Order(List<string> order, string method)
    {
        var i = order.IndexOf(method);
        return i < 0 ? int.MaxValue : i;
    }

    public string ToCsv()
    {
        var c = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("method,checkpoint,n,f1_mean,f1_sd,auc_mean,auc_sd,accuracy_mean,accuracy_sd");
        foreach (var r in Rows)
            sb.AppendLine(string.Join(',', Quote(r.Method), r.Checkpoint == DelayExperimentReport.AllCheckpoints ? "all" : r.Checkpoint.ToString(c),
                r.F1.N.ToString(c), F(r.F1.Mean), F(r.F1.Sd), F(r.Auc.Mean), F(r.Auc.Sd), F(r.Accuracy.Mean), F(r.Accuracy.Sd)));

        sb.AppendLine();
        sb.AppendLine("early_warning_method,n,detected_share_mean,detected_share_sd,lead_weeks_mean,lead_weeks_sd,first_checkpoint_mean,first_checkpoint_sd,false_alarm_share_mean,false_alarm_share_sd");
        foreach (var w in EarlyWarning)
            sb.AppendLine(string.Join(',', Quote(w.Method), w.DetectedShare.N.ToString(c), F(w.DetectedShare.Mean), F(w.DetectedShare.Sd),
                F(w.MeanLeadWeeks.Mean), F(w.MeanLeadWeeks.Sd), F(w.MeanFirstCheckpoint.Mean), F(w.MeanFirstCheckpoint.Sd),
                F(w.FalseAlarmShare.Mean), F(w.FalseAlarmShare.Sd)));

        sb.AppendLine();
        sb.AppendLine("seed,projects,selected_classifier");
        for (var i = 0; i < Seeds.Count; i++)
            sb.AppendLine($"{Seeds[i].ToString(c)},{ProjectsPerSeed.ToString(c)},{SelectedPerSeed[i]}");
        return sb.ToString();

        string F(double? v) => v is { } d && !double.IsNaN(d) ? d.ToString("0.0000", c) : "";
        static string Quote(string s) => $"\"{s.Replace("\"", "\"\"")}\"";
    }
}
