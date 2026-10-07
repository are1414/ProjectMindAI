using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML;
using ProjectMind.Application.Ml;

namespace ProjectMind.Infrastructure.Ml;

/// <summary>
/// Seçilen modeli klasöre kaydeder: classifier.zip, regressor.zip (ML.NET formatı) ve model.json (sürüm/metrik bilgisi).
/// Veri seti sürümü değişmişse eski model yüklenmez (yeniden eğitilir).
/// </summary>
public sealed class FileDelayModelStore(IOptions<MlOptions> options, ILogger<FileDelayModelStore> logger) : IDelayModelStore
{
    private const string ClassifierFile = "classifier.zip";
    private const string RegressorFile = "regressor.zip";
    private const string InfoFile = "model.json";

    private string Directory => Path.GetFullPath(options.Value.ModelDirectory);

    public void Save(DelayModelBundle bundle)
    {
        if (bundle.Classifier is not MlNetClassifier classifier || bundle.Regressor is not MlNetRegressor regressor)
            throw new ArgumentException("Sadece ML.NET modelleri kaydedilebilir.", nameof(bundle));

        System.IO.Directory.CreateDirectory(Directory);
        var ml = new MLContext();
        ml.Model.Save(classifier.Model, classifier.Schema, Path.Combine(Directory, ClassifierFile));
        ml.Model.Save(regressor.Model, regressor.Schema, Path.Combine(Directory, RegressorFile));
        File.WriteAllText(Path.Combine(Directory, InfoFile), JsonSerializer.Serialize(bundle.Info, JsonOptions));
        logger.LogInformation("Gecikme modeli kaydedildi: {Classifier} ({Directory})", bundle.Info.Classifier, Directory);
    }

    public DelayModelBundle? Load()
    {
        var infoPath = Path.Combine(Directory, InfoFile);
        if (!File.Exists(infoPath))
            return null;

        try
        {
            var info = JsonSerializer.Deserialize<DelayModelInfo>(File.ReadAllText(infoPath), JsonOptions);
            if (info is null || info.DatasetVersion != MlOptions.DatasetVersion)
                return null;

            var ml = new MLContext();
            var classifier = ml.Model.Load(Path.Combine(Directory, ClassifierFile), out var classifierSchema);
            var regressor = ml.Model.Load(Path.Combine(Directory, RegressorFile), out var regressorSchema);
            return new DelayModelBundle(
                new MlNetClassifier(info.Classifier, ml, classifier, classifierSchema),
                new MlNetRegressor(info.Regressor, ml, regressor, regressorSchema), info);
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException or FormatException)
        {
            logger.LogWarning(ex, "Kayıtlı gecikme modeli okunamadı; yeniden eğitilecek");
            return null;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
}
