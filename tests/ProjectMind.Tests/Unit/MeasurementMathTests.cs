using System.Text.Json.Nodes;
using ProjectMind.Application.Ai;
using ProjectMind.Application.Evaluation;
using ProjectMind.Application.Ml;
using ProjectMind.Application.MissingWork;
using ProjectMind.Application.WorkItems;
using ProjectMind.Domain.Enums;
using ProjectMind.Infrastructure.Ai;

namespace ProjectMind.Tests.Unit;

/// <summary>Tur 5b ölçüm koşucularının hesapları (D31): elle doğrulanan küçük örnekler.</summary>
public class MeasurementMathTests
{
    private const double Tol = 1e-6;

    // ---------- RQ1 erken uyarı ----------

    [Fact]
    public void Early_warning_lead_time_detection_and_false_alarms_hand_example()
    {
        EarlyWarningProject P(int id, bool delayed, int planned, int actual, params (int, bool)[] checks) =>
            new(id, delayed, planned, actual, checks);

        var projects = new[]
        {
            // İlk işaret %50: hafta round(8 × 0,50) = 4 → uyarı süresi 12 − 4 = 8 hafta, kalan pay 8/12.
            P(1, true, 8, 12, (25, false), (50, true), (75, true)),
            // İlk işaret %25: hafta round(8 × 0,25) = 2 → 14 − 2 = 12 hafta, pay 12/14.
            P(2, true, 8, 14, (25, true), (50, false), (75, true)),
            // Hiç işaret yok: yakalanmadı.
            P(3, true, 8, 10, (25, false), (50, false), (75, false)),
            // Zamanında bitti ama %75'te işaret: yanlış alarm.
            P(4, false, 8, 8, (25, false), (50, false), (75, true)),
            P(5, false, 8, 7, (25, false), (50, false))
        };

        var r = EarlyWarningResult.Compute("EVM", projects);

        Assert.Equal(3, r.DelayedProjects);
        Assert.Equal(2, r.Detected);
        Assert.Equal(2.0 / 3, r.DetectedShare!.Value, Tol);
        Assert.Equal(37.5, r.MeanFirstCheckpoint!.Value, Tol);   // (50 + 25) / 2
        Assert.Equal(10.0, r.MeanLeadWeeks!.Value, Tol);         // (8 + 12) / 2
        Assert.Equal((8.0 / 12 + 12.0 / 14) / 2, r.MeanLeadShare!.Value, Tol);
        Assert.Equal(2, r.OnTimeProjects);
        Assert.Equal(1, r.FalseAlarms);
        Assert.Equal(0.5, r.FalseAlarmShare!.Value, Tol);
    }

    [Fact]
    public void Early_warning_without_detections_has_no_means()
    {
        var r = EarlyWarningResult.Compute("ML", [new EarlyWarningProject(1, true, 10, 12, [(25, false)])]);
        Assert.Equal(0, r.Detected);
        Assert.Null(r.MeanLeadWeeks);
        Assert.Equal(0, r.DetectedShare!.Value, Tol);
        Assert.Null(r.FalseAlarmShare);   // zamanında biten proje yok
    }

    [Fact]
    public void Checkpoint_week_matches_generator_rounding()
    {
        Assert.Equal(2, SyntheticProjectGenerator.CheckpointWeek(8, 25));
        Assert.Equal(6, SyntheticProjectGenerator.CheckpointWeek(8, 75));
        Assert.Equal(1, SyntheticProjectGenerator.CheckpointWeek(2, 25));   // en az 1. hafta
    }

    // ---------- RQ1/RQ2 çok tohumlu özet ----------

    private static BinaryMetrics M(double f1, double auc, double accuracy = 0.5) => new(10, accuracy, 0.5, 0.5, f1, auc);

    private static DelayExperimentReport Report(int seed, string selected, double evmF1, double evmAuc, double aF1, double bF1,
        double evmLead, double aLead) =>
        new(new SyntheticOptions(100, seed), 70, 30, 210, 90, 0.4,
            [
                new MethodEvaluation(DelayExperimentReport.EvmMethod, DelayExperimentReport.AllCheckpoints, M(evmF1, evmAuc), 0.1),
                new MethodEvaluation("A", DelayExperimentReport.AllCheckpoints, M(aF1, 0.8), 0.1),
                new MethodEvaluation("B", DelayExperimentReport.AllCheckpoints, M(bF1, double.NaN), 0.1),
                new MethodEvaluation(DelayExperimentReport.EvmMethod, 25, M(0.4, 0.6), 0.1)
            ],
            selected, [], DateTimeOffset.UnixEpoch,
            [
                new EarlyWarningResult(DelayExperimentReport.EvmMethod, 10, 5, 50, evmLead, 0.3, 20, 2),
                new EarlyWarningResult("A", 10, 8, 25, aLead, 0.5, 20, 4),
                new EarlyWarningResult("B", 10, 6, 50, 1, 0.1, 20, 0)
            ]);

    [Fact]
    public void Multi_seed_mean_and_sample_sd_hand_example()
    {
        var reports = new[]
        {
            Report(42, "A", 0.6, 0.5, 0.7, 0.5, evmLead: 2, aLead: 6),
            Report(43, "A", 0.7, 0.7, 0.8, 0.5, evmLead: 4, aLead: 6),
            Report(44, "B", 0.8, 0.9, 0.9, 0.5, evmLead: 6, aLead: 6)
        };

        var r = MultiSeedReport.Aggregate(reports, DateTimeOffset.UnixEpoch);

        Assert.Equal([42, 43, 44], r.Seeds);
        Assert.Equal(["A", "A", "B"], r.SelectedPerSeed);

        var evm = r.Rows.Single(x => x.Method == DelayExperimentReport.EvmMethod && x.Checkpoint == DelayExperimentReport.AllCheckpoints);
        Assert.Equal(0.7, evm.F1.Mean!.Value, Tol);       // (0,6 + 0,7 + 0,8) / 3
        Assert.Equal(0.1, evm.F1.Sd!.Value, Tol);         // √((0,01 + 0 + 0,01) / 2)
        Assert.Equal(0.7, evm.Auc.Mean!.Value, Tol);
        Assert.Equal(0.2, evm.Auc.Sd!.Value, Tol);
        Assert.Equal(3, evm.F1.N);

        // Seçilen ML: tohum 42 ve 43'te A (0,7; 0,8), 44'te B (0,5) → ort. 0,6667, SS √(0,046667 / 2) = 0,152753.
        var selected = r.Rows.Single(x => x.Method == MultiSeedReport.SelectedMlMethod && x.Checkpoint == DelayExperimentReport.AllCheckpoints);
        Assert.Equal(2.0 / 3, selected.F1.Mean!.Value, Tol);
        Assert.Equal(Math.Sqrt((Math.Pow(0.7 - 2.0 / 3, 2) + Math.Pow(0.8 - 2.0 / 3, 2) + Math.Pow(0.5 - 2.0 / 3, 2)) / 2), selected.F1.Sd!.Value, Tol);
        Assert.Equal(0.152753, selected.F1.Sd!.Value, 1e-5);

        // NaN AUC (B) atlanır; tohumlarda hep NaN ise ortalama yok.
        var b = r.Rows.Single(x => x.Method == "B" && x.Checkpoint == DelayExperimentReport.AllCheckpoints);
        Assert.Equal(0, b.Auc.N);
        Assert.Null(b.Auc.Mean);
        Assert.Equal(0.0, b.F1.Sd!.Value, Tol);

        // Kontrol noktası satırları önce, "tümü" en sonda.
        Assert.Equal(25, r.Rows[0].Checkpoint);

        var evmWarning = r.EarlyWarning.Single(w => w.Method == DelayExperimentReport.EvmMethod);
        Assert.Equal(4.0, evmWarning.MeanLeadWeeks.Mean!.Value, Tol);   // (2 + 4 + 6) / 3
        Assert.Equal(2.0, evmWarning.MeanLeadWeeks.Sd!.Value, Tol);
        Assert.Equal(0.5, evmWarning.DetectedShare.Mean!.Value, Tol);
        Assert.Equal(0.1, evmWarning.FalseAlarmShare.Mean!.Value, Tol);
        var selectedWarning = r.EarlyWarning.Single(w => w.Method == MultiSeedReport.SelectedMlMethod);
        Assert.Equal((6 + 6 + 1) / 3.0, selectedWarning.MeanLeadWeeks.Mean!.Value, Tol);

        var csv = r.ToCsv();
        Assert.StartsWith("method,checkpoint,n,f1_mean,f1_sd,auc_mean,auc_sd", csv);
        Assert.Contains("early_warning_method,n,detected_share_mean", csv);
        Assert.Contains("44,100,B", csv);
        Assert.Contains("0.7000,0.1000", csv);   // EVM F1 ort., SS (nokta ondalık)
    }

    [Fact]
    public void Metric_summary_single_value_has_no_sd()
    {
        var m = MetricSummary.Of([0.5, null, double.NaN]);
        Assert.Equal(1, m.N);
        Assert.Equal(0.5, m.Mean!.Value, Tol);
        Assert.Null(m.Sd);
    }

    // ---------- RQ4 tohumlanmış eksik iş ----------

    private static MissingWorkItem Missing(string key) =>
        new(key, key, WorkPhase.Analysis, Skill.Analysis, 8, "", []);

    [Fact]
    public void Recall_and_precision_hand_example_for_rule_and_hybrid()
    {
        RemovedItem[] removed =
        [
            new("Gereksinim çalıştayı", RemovedGroup.Template, ["requirements"]),
            new("Arayüz taslakları", RemovedGroup.Template, ["ui-design"]),
            new("Biyometrik giriş", RemovedGroup.Other, []),
            new("QR kod ile ödeme", RemovedGroup.Other, [])
        ];
        // Tam listede "user-docs" zaten eksik (önceden eksik: precision paydasına girmez).
        var full = new MissingWorkResult([Missing("user-docs")], new Dictionary<string, IReadOnlyList<string>>
        {
            ["requirements"] = ["Gereksinim çalıştayı"],
            ["ui-design"] = ["Arayüz taslakları"],
            ["testing"] = ["Testler"]
        });
        // Kısaltılmış listede kural yalnız "requirements"ı buldu.
        var reduced = new MissingWorkResult([Missing("user-docs"), Missing("requirements")], new Dictionary<string, IReadOnlyList<string>>());

        var rule = MissingWorkRecall.Score(removed, full, reduced, []);
        Assert.Equal(new GroupRecall(2, 1), rule.Template);
        Assert.Equal(new GroupRecall(2, 0), rule.Other);
        Assert.Equal(0.25m, rule.All.Recall);
        Assert.Equal(1, rule.Suggestions);
        Assert.Equal(1, rule.MatchedSuggestions);
        Assert.Equal(1m, rule.Precision);
        Assert.Equal(1, rule.PreExisting);
        Assert.Equal(["Gereksinim çalıştayı"], rule.RecoveredNames);

        // LLM: şablon anahtar kelimesiyle "ui-design" (ekran tasarımı / wireframe), adla "Biyometrik giriş"; üçüncüsü ilgisiz.
        var hybrid = MissingWorkRecall.Score(removed, full, reduced,
            ["Ekran tasarımı (wireframe)", "Biyometrik giriş", "Yedekleme planı"]);
        Assert.Equal(new GroupRecall(2, 2), hybrid.Template);
        Assert.Equal(new GroupRecall(2, 1), hybrid.Other);
        Assert.Equal(0.75m, hybrid.All.Recall);
        Assert.Equal(4, hybrid.Suggestions);
        Assert.Equal(3, hybrid.MatchedSuggestions);
        Assert.Equal(0.75m, hybrid.Precision);
    }

    [Fact]
    public void Removal_selection_uses_sole_covering_leaves_and_is_deterministic()
    {
        WorkItemResponse W(int id, string name, int? parent = null, WorkItemStatus status = WorkItemStatus.NotStarted) =>
            new(id, 1, name, null, WorkPhase.Development, Skill.Backend, Priority.Medium, 8, null, parent, null, null, status, 0, 0);

        var items = new[]
        {
            W(1, "Geliştirme"),                          // üst iş: uygun değil
            W(2, "Gereksinim analizi", 1),               // requirements'ın tek karşılığı
            W(3, "Birim testleri", 1),                   // unit-test şablonu: iki karşılık → tek karşılık değil
            W(4, "Test otomasyonu", 1),
            W(5, "Sadakat puanları", 1),                 // şablon dışı
            W(6, "QR kod ile ödeme", 1),                 // şablon dışı
            W(7, "Akıllı saat eklentisi", 1, WorkItemStatus.Cancelled)   // iptal: uygun değil
        };

        var a = MissingWorkRecall.SelectRemovals(ProjectType.MobileApplication, items, 5, 5, seed: 3);
        var b = MissingWorkRecall.SelectRemovals(ProjectType.MobileApplication, items, 5, 5, seed: 3);

        Assert.Equal(a.Removed, b.Removed, new RemovedComparer());
        Assert.Contains(a.Removed, r => r is { Name: "Gereksinim analizi", Group: RemovedGroup.Template });
        Assert.DoesNotContain(a.Removed, r => r.Name is "Birim testleri" or "Test otomasyonu" or "Geliştirme" or "Akıllı saat eklentisi");
        Assert.Equal(["QR kod ile ödeme", "Sadakat puanları"], a.Removed.Where(r => r.Group == RemovedGroup.Other).Select(r => r.Name).Order());
        Assert.Equal(items.Length - a.Removed.Count, a.Remaining.Count);

        // Kural katmanı çıkarılan şablon işinin şablonunu eksik bulur → şablon recall 1.
        var full = MissingWorkDetector.Detect(ProjectType.MobileApplication, items);
        var reduced = MissingWorkDetector.Detect(ProjectType.MobileApplication, a.Remaining);
        var score = MissingWorkRecall.Score(a.Removed, full, reduced, []);
        Assert.Equal(1m, score.Template.Recall);
        Assert.Equal(0m, score.Other.Recall);
    }

    private sealed class RemovedComparer : IEqualityComparer<RemovedItem>
    {
        public bool Equals(RemovedItem? x, RemovedItem? y) =>
            x!.Name == y!.Name && x.Group == y.Group && x.TemplateKeys.SequenceEqual(y.TemplateKeys);

        public int GetHashCode(RemovedItem obj) => obj.Name.GetHashCode();
    }

    // ---------- Sağlayıcı karşılaştırması ----------

    [Fact]
    public void Provider_stats_hand_example()
    {
        EvaluationLogRow Row(string task, AiAnalysisOutcome outcome, bool? schema, int unverified, long ms, int? input = null,
            int? output = null, string? tools = null) =>
            new("B", "b-1", task, task.StartsWith("chat") ? AiAnalysisKind.Chat : AiAnalysisKind.ProjectComment, outcome, schema,
                unverified, ms, input, output, tools);

        var rows = new[]
        {
            Row("comment-project", AiAnalysisOutcome.Success, true, 0, 100, 10, 5),
            Row("comment-project", AiAnalysisOutcome.Success, true, 2, 200),
            Row("comment-project", AiAnalysisOutcome.InvalidSchema, false, 0, 300, 30, 15),
            Row("chat-status", AiAnalysisOutcome.Success, null, 1, 400, tools: "get_project_status,preview_schedule"),
            Row("chat-plan", AiAnalysisOutcome.ModelError, null, 0, 0)
        };

        var stats = ProviderTaskStats.Compute(rows);
        var all = stats.Single(s => s.Task == ProviderTaskStats.AllTasks);
        Assert.Equal(5, all.Calls);
        Assert.Equal(0.2m, all.ErrorRate);                 // 1 / 5
        Assert.Equal(2m / 3, all.SchemaValidRate);        // 2 geçerli / 3 denetlenen
        Assert.Equal(3, all.Answered);
        Assert.Equal(2m / 3, all.UnverifiedShare);        // 2 cevapta doğrulanamayan sayı / 3 başarılı
        Assert.Equal(1m, all.UnverifiedPerAnswer);        // (2 + 1) / 3
        Assert.Equal(200m, all.DurationMs.Mean);          // (100 + 200 + 300 + 400 + 0) / 5
        Assert.Equal(2, all.TokenCalls);
        Assert.Equal(20m, all.MeanInputTokens);           // (10 + 30) / 2
        Assert.Equal(10m, all.MeanOutputTokens);
        Assert.Equal(1, all.ExpectedToolChecks);          // hatalı sohbet sayılmaz
        Assert.Equal(1m, all.ExpectedToolRate);
        Assert.Equal("b-1", all.Models);

        var project = stats.Single(s => s.Task == "comment-project");
        Assert.Equal(3, project.Calls);
        Assert.Equal(2m / 3, project.SchemaValidRate);
        Assert.Null(project.ExpectedToolRate);
        // Sıra: tümü, ardından görev setindeki sıra (comment-project, chat-status, chat-plan).
        Assert.Equal([ProviderTaskStats.AllTasks, "comment-project", "chat-status", "chat-plan"], stats.Select(s => s.Task));
    }

    [Fact]
    public void Gemini_usage_metadata_counts_thoughts_as_output()
    {
        var usage = GeminiChatModel.ReadUsage(JsonNode.Parse(
            """{"candidates":[],"usageMetadata":{"promptTokenCount":120,"candidatesTokenCount":30,"thoughtsTokenCount":12,"totalTokenCount":162}}"""));
        Assert.Equal(new ChatUsage(120, 42), usage);
        Assert.Equal(new ChatUsage(5, 0), GeminiChatModel.ReadUsage(JsonNode.Parse("""{"usageMetadata":{"promptTokenCount":5}}""")));
        Assert.Null(GeminiChatModel.ReadUsage(JsonNode.Parse("""{"candidates":[]}""")));
        Assert.Equal(new ChatUsage(15, 7), ChatUsage.Add(new ChatUsage(10, 5), new ChatUsage(5, 2)));
        Assert.Equal(new ChatUsage(1, 2), ChatUsage.Add(null, new ChatUsage(1, 2)));
        Assert.Null(ChatUsage.Add(null, null));
    }
}
