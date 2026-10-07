using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProjectMind.Application.Analytics;
using ProjectMind.Application.Ml;
using ProjectMind.Infrastructure.Ml;
using Xunit.Abstractions;

namespace ProjectMind.Tests.Unit;

public class MlTests(ITestOutputHelper output)
{
    [Fact]
    public void Metrics_match_hand_calculation()
    {
        // Gerçek: + + - -   skor: 0.9 0.4 0.6 0.1  (eşik 0.5) → TP=1, FN=1, FP=1, TN=1
        bool[] actual = [true, true, false, false];
        double[] scores = [0.9, 0.4, 0.6, 0.1];
        var m = ClassificationMetrics.Compute(actual, scores);

        Assert.Equal(0.5, m.Accuracy);
        Assert.Equal(0.5, m.Precision);
        Assert.Equal(0.5, m.Recall);
        Assert.Equal(0.5, m.F1);
        // Pozitif-negatif çiftleri: (0.9>0.6) (0.9>0.1) (0.4<0.6) (0.4>0.1) → 3/4
        Assert.Equal(0.75, m.Auc);
        Assert.Equal(0.5, ClassificationMetrics.Auc([true, false], [0.3, 0.3]));
        Assert.Equal(0.25, ClassificationMetrics.MeanAbsoluteError([1.0, 2.0], [1.5, 2.0]));
    }

    [Fact]
    public void Generator_is_deterministic_and_has_all_checkpoints()
    {
        var a = SyntheticProjectGenerator.Generate(new SyntheticOptions(Projects: 30, Seed: 7));
        var b = SyntheticProjectGenerator.Generate(new SyntheticOptions(Projects: 30, Seed: 7));

        Assert.Equal(a.SelectMany(p => p.Samples), b.SelectMany(p => p.Samples));
        Assert.All(a, p => Assert.Equal(SyntheticProjectGenerator.Checkpoints, p.Samples.Select(s => s.CheckpointPercent)));
        Assert.All(a, p => Assert.Equal(p.DurationRatio > 1.10f, p.Delayed));
    }

    [Fact]
    public void Generator_outcome_depends_on_hidden_variables()
    {
        var projects = SyntheticProjectGenerator.Generate(new SyntheticOptions(Projects: 400));
        double DelayRate(IEnumerable<SyntheticProject> ps) => ps.Average(p => p.Delayed ? 1.0 : 0.0);

        var optimistic = projects.Where(p => p.HiddenEstimationBias > 1.3f).ToList();
        var realistic = projects.Where(p => p.HiddenEstimationBias < 1.0f).ToList();
        Assert.True(DelayRate(optimistic) > DelayRate(realistic) + 0.2);

        // Sınıflar dengesiz olmamalı (ikisi de öğrenilebilir).
        var share = DelayRate(projects);
        Assert.InRange(share, 0.25, 0.85);
    }

    [Fact]
    public void Split_is_by_project_without_overlap()
    {
        var projects = SyntheticProjectGenerator.Generate(new SyntheticOptions(Projects: 50));
        var (train, test) = DelayExperiment.Split(projects, 0.7, 1);

        Assert.Equal(35, train.Count);
        Assert.Equal(15, test.Count);
        Assert.Empty(train.Select(p => p.Id).Intersect(test.Select(p => p.Id)));
    }

    [Fact]
    public void Permutation_importance_detects_the_used_feature()
    {
        var rows = SyntheticProjectGenerator.Generate(new SyntheticOptions(Projects: 60)).SelectMany(p => p.Samples).ToList();
        var importance = DelayExperiment.PermutationImportance(new SpiTimeOnlyModel(), rows.Select(r => r.Features).ToList(),
            rows.Select(r => r.Delayed).ToList(), repeats: 3, seed: 1);

        Assert.Equal(nameof(DelayFeatures.SpiTime), importance[0].Feature);
        Assert.True(importance[0].AucDrop > 0.05);
        Assert.All(importance.Skip(1), f => Assert.Equal(0, f.AucDrop, 6));
    }

    [Fact]
    public void Feature_builder_uses_two_week_window()
    {
        var evm = new EvmResult(new DateOnly(2026, 11, 30), 400, 200, 150, 160, 0.75m, 0.94m, 15, 21, 0.71m, 426,
            40, new DateOnly(2026, 11, 2), new DateOnly(2026, 12, 25), null, 0, null, []);
        SnapshotPoint[] history =
        [
            new(new DateOnly(2026, 11, 10), 60, 50, 50, null),
            new(new DateOnly(2026, 11, 16), 100, 80, 85, null),   // 14 gün önce: pencere başlangıcı
            new(new DateOnly(2026, 11, 23), 150, 110, 120, null)
        ];

        var f = DelayFeatureBuilder.FromStatus(evm, history, scopeGrowthPercent: 5, blockedRemainingShare: 0.1m, teamSize: 4)!;

        Assert.Equal(21f / 40f, f.ElapsedRatio, 4);
        Assert.Equal(150f / 400f, f.PercentComplete, 4);
        Assert.Equal((150f - 80f) / (200f - 100f), f.RecentVelocity, 4);
        Assert.Equal(0.05f, f.ScopeGrowth, 4);
        Assert.Equal(0.71f, f.SpiTime, 4);
        Assert.True(f.EvmPredictsDelay(0.10f));   // 1 / 0,71 = 1,41 > 1,10
    }

    [Fact]
    public void MlNet_experiment_beats_chance_and_model_round_trips()
    {
        var options = new MlOptions { Projects = 200, PermutationRepeats = 2 };
        var (report, model) = new DelayExperiment(new MlNetDelayLearner()).Run(options, DateTimeOffset.UnixEpoch);

        output.WriteLine($"delayed share {report.DelayedShare:0.000}, selected {report.SelectedClassifier}");
        foreach (var e in report.Evaluations)
            output.WriteLine($"{e.Method,-22} {e.Checkpoint,3} F1={e.Metrics.F1:0.000} AUC={e.Metrics.Auc:0.000} " +
                             $"P={e.Metrics.Precision:0.000} R={e.Metrics.Recall:0.000} MAE={e.DurationMae:0.000}");
        foreach (var f in report.Importance)
            output.WriteLine($"{f.Feature,-16} {f.AucDrop:0.0000}");

        Assert.Equal(4 * 4, report.Evaluations.Count);   // EVM + 3 model, (tümü + 3 kontrol noktası)
        Assert.All(report.Evaluations.Where(e => e.Method != DelayExperimentReport.EvmMethod), e => Assert.True(e.Metrics.Auc > 0.6));
        Assert.Contains("method,checkpoint", report.ToCsv());

        var dir = Path.Combine(Path.GetTempPath(), "pm-model-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileDelayModelStore(Options.Create(new MlOptions { ModelDirectory = dir }), NullLogger<FileDelayModelStore>.Instance);
            store.Save(model);
            var loaded = store.Load()!;
            var sample = SyntheticProjectGenerator.Generate(new SyntheticOptions(Projects: 3, Seed: 99)).SelectMany(p => p.Samples)
                .Select(s => s.Features).ToList();

            Assert.Equal(model.Info, loaded.Info);
            Assert.Equal(model.Classifier.PredictProbabilities(sample), loaded.Classifier.PredictProbabilities(sample));
            Assert.Equal(model.Regressor.PredictRatios(sample), loaded.Regressor.PredictRatios(sample));
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    private sealed class SpiTimeOnlyModel : IDelayClassifier
    {
        public string Algorithm => "test";
        public IReadOnlyList<float> PredictProbabilities(IReadOnlyList<DelayFeatures> features) =>
            features.Select(f => 1 - f.SpiTime).ToList();
    }
}
