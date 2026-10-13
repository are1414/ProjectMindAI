namespace ProjectMind.Application.Evaluation;

/// <summary>appsettings "Evaluation" bölümü: ölçüm koşucularının ayarları (D31).</summary>
public sealed class EvaluationOptions
{
    public const string Section = "Evaluation";

    /// <summary>Sağlayıcı karşılaştırmasında her görevin sağlayıcı başına tekrar sayısı.</summary>
    public int Repeats { get; set; } = 3;

    /// <summary>Tekrar sayısının üst sınırı (maliyet koruması).</summary>
    public int MaxRepeats { get; set; } = 10;

    /// <summary>RQ4 deneyinde çıkarılan, şablonun kapsadığı iş sayısı (k).</summary>
    public int RecallTemplateItems { get; set; } = 4;

    /// <summary>RQ4 deneyinde çıkarılan, hiçbir şablona uymayan iş sayısı (k).</summary>
    public int RecallOtherItems { get; set; } = 4;

    /// <summary>RQ4 deneyinde çıkarılacak işleri seçen tohum (aynı tohum = aynı işler).</summary>
    public int RecallSeed { get; set; } = 11;

    /// <summary>RQ4 deneyinin hibrit (LLM) kısmının tekrar sayısı.</summary>
    public int RecallRepeats { get; set; } = 3;
}
