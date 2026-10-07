namespace ProjectMind.Application.Ml;

/// <summary>Gecikme olasılığı veren eğitilmiş sınıflandırıcı (ML.NET Infrastructure'da).</summary>
public interface IDelayClassifier
{
    string Algorithm { get; }
    IReadOnlyList<float> PredictProbabilities(IReadOnlyList<DelayFeatures> features);
}

/// <summary>Süre oranını (gerçek süre / planlanan süre) tahmin eden eğitilmiş regresyon modeli.</summary>
public interface IDurationRegressor
{
    string Algorithm { get; }
    IReadOnlyList<float> PredictRatios(IReadOnlyList<DelayFeatures> features);
}

/// <summary>Model eğitimi (ML.NET). Application sadece bu soyutlamayı bilir; değerlendirme ve seçim burada yapılır.</summary>
public interface IDelayLearner
{
    IReadOnlyList<string> Classifiers { get; }
    IDelayClassifier TrainClassifier(string algorithm, IReadOnlyList<DelaySample> data, int seed);
    IDurationRegressor TrainRegressor(IReadOnlyList<DelaySample> data, int seed);
}

/// <summary>Seçilen modelin kaydı/yüklenmesi (dosya sistemi).</summary>
public interface IDelayModelStore
{
    void Save(DelayModelBundle bundle);
    DelayModelBundle? Load();
}

public sealed record DelayModelInfo(
    string Classifier, string Regressor, string DatasetVersion, int Seed, int Projects, int TrainRows,
    double TestAuc, double TestF1, double TestMae, DateTimeOffset TrainedAt);

public sealed record DelayModelBundle(IDelayClassifier Classifier, IDurationRegressor Regressor, DelayModelInfo Info);

public enum DelayRisk { Low, Medium, High }

public sealed record DelayPrediction(
    float Probability, float DurationRatio, DateOnly? ForecastFinish, bool OutsideTrainingRange, DelayModelInfo Model)
{
    public const float MediumRiskProbability = 0.4f;
    public const float HighRiskProbability = 0.6f;

    public DelayRisk Risk => Probability >= HighRiskProbability ? DelayRisk.High
        : Probability >= MediumRiskProbability ? DelayRisk.Medium : DelayRisk.Low;
}

public sealed class MlOptions
{
    public const string Section = "Ml";
    public const string DatasetVersion = "synthetic-v1";

    public int Projects { get; set; } = 400;
    public int Seed { get; set; } = 42;
    public float DelayTolerance { get; set; } = 0.10f;
    public double TrainShare { get; set; } = 0.7;
    public double Threshold { get; set; } = 0.5;
    public int PermutationRepeats { get; set; } = 5;
    public string ModelDirectory { get; set; } = "App_Data/models";

    /// <summary>Eğitim verisindeki geçen süre oranı aralığı; dışındaki tahminler "düşük güven" olarak işaretlenir.</summary>
    public float MinElapsedRatio { get; set; } = 0.15f;
    public float MaxElapsedRatio { get; set; } = 0.9f;

    public SyntheticOptions ToSynthetic() => new(Projects, Seed, DelayTolerance);
}
