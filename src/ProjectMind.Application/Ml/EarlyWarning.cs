namespace ProjectMind.Application.Ml;

/// <summary>Bir test projesinin bir yöntemle kontrol noktası bazında "gecikecek" işaretleri.</summary>
public sealed record EarlyWarningProject(
    int ProjectId, bool Delayed, int PlannedWeeks, int ActualWeeks, IReadOnlyList<(int Checkpoint, bool Flagged)> Checks);

/// <summary>
/// RQ1 erken uyarı ölçümü (D31). Kontrol noktaları sentetik üreticinin noktalarıdır (planlanan sürenin %25 / %50 / %75'i);
/// ara haftalar ölçülmez. Geciken projede ilk işaret = gecikmeyi ilk doğru gördüğü kontrol noktası; uyarı süresi
/// (hafta) = gerçek bitiş haftası − o kontrol noktasının haftası. Zamanında biten projede herhangi bir noktada işaret
/// "yanlış alarm"dır. Ortalamalar yalnız yakalanan projeler üzerindendir.
/// </summary>
public sealed record EarlyWarningResult(
    string Method, int DelayedProjects, int Detected, double? MeanFirstCheckpoint, double? MeanLeadWeeks, double? MeanLeadShare,
    int OnTimeProjects, int FalseAlarms)
{
    /// <summary>Yakalanan geciken proje payı (en az bir kontrol noktasında işaretlenen).</summary>
    public double? DetectedShare => DelayedProjects == 0 ? null : (double)Detected / DelayedProjects;

    /// <summary>Zamanında biten projelerde en az bir yanlış alarm payı.</summary>
    public double? FalseAlarmShare => OnTimeProjects == 0 ? null : (double)FalseAlarms / OnTimeProjects;

    public static EarlyWarningResult Compute(string method, IReadOnlyList<EarlyWarningProject> projects)
    {
        var delayed = projects.Where(p => p.Delayed).ToList();
        var onTime = projects.Where(p => !p.Delayed).ToList();
        var detections = delayed
            .Select(p => (Project: p, First: p.Checks.Where(c => c.Flagged).OrderBy(c => c.Checkpoint).Select(c => (int?)c.Checkpoint).FirstOrDefault()))
            .Where(x => x.First is not null)
            .Select(x =>
            {
                var lead = x.Project.ActualWeeks - SyntheticProjectGenerator.CheckpointWeek(x.Project.PlannedWeeks, x.First!.Value);
                return (Checkpoint: (double)x.First.Value, Lead: (double)lead, Share: (double)lead / x.Project.ActualWeeks);
            })
            .ToList();

        return new EarlyWarningResult(method, delayed.Count, detections.Count,
            detections.Count == 0 ? null : detections.Average(d => d.Checkpoint),
            detections.Count == 0 ? null : detections.Average(d => d.Lead),
            detections.Count == 0 ? null : detections.Average(d => d.Share),
            onTime.Count, onTime.Count(p => p.Checks.Any(c => c.Flagged)));
    }
}
