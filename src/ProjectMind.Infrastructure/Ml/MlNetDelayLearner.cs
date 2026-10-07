using Microsoft.ML;
using Microsoft.ML.Data;
using ProjectMind.Application.Ml;

namespace ProjectMind.Infrastructure.Ml;

/// <summary>ML.NET ile gecikme sınıflandırıcıları (LogisticRegression, FastTree, FastForest) ve süre regresyonu (FastTree).</summary>
public sealed class MlNetDelayLearner : IDelayLearner
{
    public const string LogisticRegression = "LogisticRegression";
    public const string FastTree = "FastTree";
    public const string FastForest = "FastForest";
    public const string FastTreeRegression = "FastTreeRegression";

    // Küçük veri setinde aşırı uyumu sınırlamak için ağaç parametreleri.
    private const int Trees = 100;
    private const int Leaves = 16;
    private const int MinExamplesPerLeaf = 10;

    public IReadOnlyList<string> Classifiers { get; } = [LogisticRegression, FastTree, FastForest];

    public IDelayClassifier TrainClassifier(string algorithm, IReadOnlyList<DelaySample> data, int seed)
    {
        var ml = new MLContext(seed);
        var view = ml.Data.LoadFromEnumerable(data.Select(MlRow.From));
        var bc = ml.BinaryClassification.Trainers;

        IEstimator<ITransformer> pipeline = algorithm switch
        {
            LogisticRegression => ml.Transforms.NormalizeMeanVariance(MlRow.FeaturesColumn)
                .Append(bc.LbfgsLogisticRegression(nameof(MlRow.Label), MlRow.FeaturesColumn)),
            FastTree => bc.FastTree(nameof(MlRow.Label), MlRow.FeaturesColumn,
                numberOfLeaves: Leaves, numberOfTrees: Trees, minimumExampleCountPerLeaf: MinExamplesPerLeaf),
            // FastForest olasılık üretmez; Platt kalibrasyonu skoru olasılığa çevirir.
            FastForest => bc.FastForest(nameof(MlRow.Label), MlRow.FeaturesColumn,
                    numberOfLeaves: Leaves, numberOfTrees: Trees, minimumExampleCountPerLeaf: MinExamplesPerLeaf)
                .Append(ml.BinaryClassification.Calibrators.Platt(nameof(MlRow.Label))),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Bilinmeyen algoritma")
        };

        return new MlNetClassifier(algorithm, ml, pipeline.Fit(view), view.Schema);
    }

    public IDurationRegressor TrainRegressor(IReadOnlyList<DelaySample> data, int seed)
    {
        var ml = new MLContext(seed);
        var view = ml.Data.LoadFromEnumerable(data.Select(MlRow.From));
        var model = ml.Regression.Trainers.FastTree(nameof(MlRow.DurationRatio), MlRow.FeaturesColumn,
            numberOfLeaves: Leaves, numberOfTrees: Trees, minimumExampleCountPerLeaf: MinExamplesPerLeaf).Fit(view);
        return new MlNetRegressor(FastTreeRegression, ml, model, view.Schema);
    }
}

internal sealed class MlRow
{
    public const string FeaturesColumn = "Features";
    public const int FeatureCount = 9;

    [VectorType(FeatureCount)]
    public float[] Features { get; set; } = [];

    public bool Label { get; set; }

    public float DurationRatio { get; set; }

    public static MlRow From(DelaySample s) => new() { Features = s.Features.ToArray(), Label = s.Delayed, DurationRatio = s.DurationRatio };

    public static MlRow From(DelayFeatures f) => new() { Features = f.ToArray() };
}

/// <summary>Eğitilmiş ML.NET dönüştürücüsü + kayıt için giriş şeması.</summary>
internal abstract class MlNetModel(string algorithm, MLContext ml, ITransformer model, DataViewSchema schema)
{
    public string Algorithm { get; } = algorithm;
    public ITransformer Model { get; } = model;
    public DataViewSchema Schema { get; } = schema;

    protected IReadOnlyList<float> PredictColumn(IReadOnlyList<DelayFeatures> features, string column)
    {
        if (features.Count == 0)
            return [];
        var scored = Model.Transform(ml.Data.LoadFromEnumerable(features.Select(MlRow.From)));
        return scored.GetColumn<float>(column).ToList();
    }
}

internal sealed class MlNetClassifier(string algorithm, MLContext ml, ITransformer model, DataViewSchema schema)
    : MlNetModel(algorithm, ml, model, schema), IDelayClassifier
{
    public IReadOnlyList<float> PredictProbabilities(IReadOnlyList<DelayFeatures> features) => PredictColumn(features, "Probability");
}

internal sealed class MlNetRegressor(string algorithm, MLContext ml, ITransformer model, DataViewSchema schema)
    : MlNetModel(algorithm, ml, model, schema), IDurationRegressor
{
    public IReadOnlyList<float> PredictRatios(IReadOnlyList<DelayFeatures> features) => PredictColumn(features, "Score");
}
