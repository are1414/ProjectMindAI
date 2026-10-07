using System.Globalization;
using System.Text;

namespace ProjectMind.Application.Ml;

/// <summary>Bir yöntemin bir kontrol noktasındaki sınıflandırma sonucu (Checkpoint 0 = tüm kontrol noktaları).</summary>
public sealed record MethodEvaluation(string Method, int Checkpoint, BinaryMetrics Metrics, double DurationMae);

public sealed record FeatureImportance(string Feature, double AucDrop);

public sealed record DelayExperimentReport(
    SyntheticOptions Synthetic, int TrainProjects, int TestProjects, int TrainRows, int TestRows, double DelayedShare,
    IReadOnlyList<MethodEvaluation> Evaluations, string SelectedClassifier, IReadOnlyList<FeatureImportance> Importance,
    DateTimeOffset CreatedAt)
{
    public const string EvmMethod = "EVM (Earned Schedule)";
    public const int AllCheckpoints = 0;

    /// <summary>Rapor tablolarının CSV hâli (ondalık ayırıcı nokta; Excel/R/Python'da doğrudan açılır).</summary>
    public string ToCsv()
    {
        var c = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("method,checkpoint,count,accuracy,precision,recall,f1,auc,duration_mae");
        foreach (var e in Evaluations)
        {
            var m = e.Metrics;
            sb.AppendLine(string.Join(',', Quote(e.Method), e.Checkpoint == AllCheckpoints ? "all" : e.Checkpoint.ToString(c),
                m.Count.ToString(c), F(m.Accuracy), F(m.Precision), F(m.Recall), F(m.F1), F(m.Auc), F(e.DurationMae)));
        }

        sb.AppendLine();
        sb.AppendLine("feature,auc_drop");
        foreach (var f in Importance)
            sb.AppendLine($"{f.Feature},{F(f.AucDrop)}");
        return sb.ToString();

        string F(double v) => double.IsNaN(v) ? "" : v.ToString("0.0000", c);
        static string Quote(string s) => $"\"{s.Replace("\"", "\"\"")}\"";
    }
}

/// <summary>
/// RQ1/RQ2 deneyi: sentetik veri → proje bazlı eğitim/test ayrımı (aynı projenin kontrol noktaları iki tarafa bölünmez)
/// → her ML sınıflandırıcısı ve EVM kuralı aynı test satırlarında, kontrol noktası bazında değerlendirilir.
/// En yüksek test AUC'li sınıflandırıcı seçilir; özellik önemi permütasyonla (AUC düşüşü) ölçülür.
/// </summary>
public sealed class DelayExperiment(IDelayLearner learner)
{
    public (DelayExperimentReport Report, DelayModelBundle Model) Run(MlOptions options, DateTimeOffset now)
    {
        var projects = SyntheticProjectGenerator.Generate(options.ToSynthetic());
        var (train, test) = Split(projects, options.TrainShare, options.Seed);
        var trainRows = train.SelectMany(p => p.Samples).ToList();
        var testRows = test.SelectMany(p => p.Samples).ToList();
        var testFeatures = testRows.Select(s => s.Features).ToList();
        var actual = testRows.Select(s => s.Delayed).ToList();
        var actualRatio = testRows.Select(s => (double)s.DurationRatio).ToList();

        var regressor = learner.TrainRegressor(trainRows, options.Seed);
        var predictedRatio = regressor.PredictRatios(testFeatures).Select(v => (double)v).ToList();

        var evaluations = new List<MethodEvaluation>();

        // EVM kuralı: skor = tahmini süre oranı (1/SPI(t)); eşik = 1 + tolerans. AUC için oran sıralaması kullanılır.
        var evmRatio = testFeatures.Select(f => float.IsNaN(f.EvmDurationRatio) ? 1.0 : (double)f.EvmDurationRatio).ToList();
        evaluations.AddRange(Evaluate(DelayExperimentReport.EvmMethod, testRows, actual, evmRatio, 1 + options.DelayTolerance,
            actualRatio, evmRatio));

        var classifiers = new List<(IDelayClassifier Model, double Auc)>();
        foreach (var algorithm in learner.Classifiers)
        {
            var model = learner.TrainClassifier(algorithm, trainRows, options.Seed);
            var scores = model.PredictProbabilities(testFeatures).Select(v => (double)v).ToList();
            var rows = Evaluate(algorithm, testRows, actual, scores, options.Threshold, actualRatio, predictedRatio);
            evaluations.AddRange(rows);
            classifiers.Add((model, rows[0].Metrics.Auc));
        }

        var best = classifiers.OrderByDescending(c => c.Auc).First().Model;
        var importance = PermutationImportance(best, testFeatures, actual, options.PermutationRepeats, options.Seed);
        var bestAll = evaluations.First(e => e.Method == best.Algorithm && e.Checkpoint == DelayExperimentReport.AllCheckpoints);

        var report = new DelayExperimentReport(options.ToSynthetic(), train.Count, test.Count, trainRows.Count, testRows.Count,
            projects.Count(p => p.Delayed) / (double)projects.Count, evaluations, best.Algorithm, importance, now);
        var info = new DelayModelInfo(best.Algorithm, regressor.Algorithm, MlOptions.DatasetVersion, options.Seed, options.Projects,
            trainRows.Count, bestAll.Metrics.Auc, bestAll.Metrics.F1, bestAll.DurationMae, now);
        return (report, new DelayModelBundle(best, regressor, info));
    }

    /// <summary>Projeleri karıştırıp ayırır (satır değil proje bazında: sızıntı olmasın).</summary>
    public static (List<SyntheticProject> Train, List<SyntheticProject> Test) Split(
        IReadOnlyList<SyntheticProject> projects, double trainShare, int seed)
    {
        var shuffled = projects.ToList();
        var random = new Random(seed);
        for (var i = shuffled.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        var trainCount = (int)Math.Round(shuffled.Count * trainShare);
        return (shuffled.Take(trainCount).ToList(), shuffled.Skip(trainCount).ToList());
    }

    private static List<MethodEvaluation> Evaluate(string method, IReadOnlyList<DelaySample> rows, IReadOnlyList<bool> actual,
        IReadOnlyList<double> scores, double threshold, IReadOnlyList<double> actualRatio, IReadOnlyList<double> predictedRatio)
    {
        var result = new List<MethodEvaluation>
        {
            new(method, DelayExperimentReport.AllCheckpoints, ClassificationMetrics.Compute(actual, scores, threshold),
                ClassificationMetrics.MeanAbsoluteError(actualRatio, predictedRatio))
        };
        foreach (var checkpoint in SyntheticProjectGenerator.Checkpoints)
        {
            var idx = Enumerable.Range(0, rows.Count).Where(i => rows[i].CheckpointPercent == checkpoint).ToList();
            result.Add(new MethodEvaluation(method, checkpoint,
                ClassificationMetrics.Compute(idx.Select(i => actual[i]).ToList(), idx.Select(i => scores[i]).ToList(), threshold),
                ClassificationMetrics.MeanAbsoluteError(idx.Select(i => actualRatio[i]).ToList(), idx.Select(i => predictedRatio[i]).ToList())));
        }
        return result;
    }

    /// <summary>Bir özelliğin test sütunu karıştırılınca AUC'nin ortalama düşüşü (büyük = model o özelliğe dayanıyor).</summary>
    public static IReadOnlyList<FeatureImportance> PermutationImportance(
        IDelayClassifier model, IReadOnlyList<DelayFeatures> features, IReadOnlyList<bool> actual, int repeats, int seed)
    {
        var baseline = ClassificationMetrics.Auc(actual, model.PredictProbabilities(features).Select(v => (double)v).ToList());
        var matrix = features.Select(f => f.ToArray()).ToList();
        var random = new Random(seed);
        var result = new List<FeatureImportance>();

        for (var column = 0; column < DelayFeatures.Names.Length; column++)
        {
            double drop = 0;
            for (var r = 0; r < repeats; r++)
            {
                var values = matrix.Select(row => row[column]).OrderBy(_ => random.Next()).ToList();
                var permuted = matrix.Select((row, i) =>
                {
                    var copy = (float[])row.Clone();
                    copy[column] = values[i];
                    return DelayFeatures.FromArray(copy);
                }).ToList();
                drop += baseline - ClassificationMetrics.Auc(actual, model.PredictProbabilities(permuted).Select(v => (double)v).ToList());
            }
            result.Add(new FeatureImportance(DelayFeatures.Names[column], drop / repeats));
        }

        return result.OrderByDescending(f => f.AucDrop).ToList();
    }
}
