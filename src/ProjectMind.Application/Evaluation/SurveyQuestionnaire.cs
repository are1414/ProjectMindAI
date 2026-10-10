using System.Text.RegularExpressions;

namespace ProjectMind.Application.Evaluation;

public enum SurveyItemGroup { Sus, Rq3, Rq4 }

public sealed record SurveyItem(string Code, SurveyItemGroup Group, string Text);

/// <summary>
/// Kullanıcı testi anketi (survey-v1): 10 SUS maddesi (Brooke, 1996; Türkçe çeviri) + RQ3 ve RQ4 için ikişer
/// 5'li Likert maddesi. Metinler sürümlüdür; değişirse <see cref="Version"/> artırılır.
/// </summary>
public static partial class SurveyQuestionnaire
{
    public const string Version = "survey-v1";

    /// <summary>Anonim katılımcı kodu: P ve 2–3 rakam (P01, P12, P105). Ad/e-posta girilmesini engeller.</summary>
    [GeneratedRegex(@"^P\d{2,3}$")]
    private static partial Regex ParticipantCodeRegex();

    public const string ParticipantCodeExample = "P01";

    public static bool IsValidParticipantCode(string code) => ParticipantCodeRegex().IsMatch(code);

    public static string NormalizeParticipantCode(string? code) => (code ?? "").Trim().ToUpperInvariant();

    public const string ConsentText =
        "Bu anket, ProjectMind AI'nın kullanılabilirliğini ve karar desteğini değerlendiren akademik bir çalışma içindir. " +
        "Ad, e-posta veya başka bir kişisel veri istenmez; yalnız size verilen katılımcı kodu (ör. P01), cevaplarınız ve " +
        "gönderim zamanı saklanır. Katılım gönüllüdür; istediğiniz an bırakabilir, kodunuzu bildirerek cevaplarınızın " +
        "silinmesini isteyebilirsiniz. Sonuçlar yalnız toplu ve anonim olarak raporlanır.";

    public static readonly IReadOnlyList<string> ScaleLabels =
    [
        "Kesinlikle katılmıyorum", "Katılmıyorum", "Kararsızım", "Katılıyorum", "Kesinlikle katılıyorum"
    ];

    public static readonly IReadOnlyList<SurveyItem> SusItems =
    [
        new("SUS1", SurveyItemGroup.Sus, "Bu sistemi sık sık kullanmak isteyeceğimi düşünüyorum."),
        new("SUS2", SurveyItemGroup.Sus, "Sistemi gereksiz yere karmaşık buldum."),
        new("SUS3", SurveyItemGroup.Sus, "Sistemin kullanımının kolay olduğunu düşündüm."),
        new("SUS4", SurveyItemGroup.Sus, "Bu sistemi kullanabilmek için teknik bilgisi olan birinin desteğine ihtiyaç duyacağımı düşünüyorum."),
        new("SUS5", SurveyItemGroup.Sus, "Bu sistemdeki çeşitli işlevlerin iyi bütünleştirildiğini düşündüm."),
        new("SUS6", SurveyItemGroup.Sus, "Bu sistemde çok fazla tutarsızlık olduğunu düşündüm."),
        new("SUS7", SurveyItemGroup.Sus, "Çoğu insanın bu sistemi kullanmayı çok çabuk öğreneceğini düşünüyorum."),
        new("SUS8", SurveyItemGroup.Sus, "Sistemi kullanmayı çok zahmetli buldum."),
        new("SUS9", SurveyItemGroup.Sus, "Sistemi kullanırken kendime çok güvendim."),
        new("SUS10", SurveyItemGroup.Sus, "Bu sistemi kullanmaya başlamadan önce birçok şey öğrenmem gerekti.")
    ];

    public static readonly IReadOnlyList<SurveyItem> LikertItems =
    [
        new("RQ3_1", SurveyItemGroup.Rq3, "What-if (senaryo) karşılaştırması karar vermeme yardımcı oldu."),
        new("RQ3_2", SurveyItemGroup.Rq3, "Senaryo sonuçlarına (P80 bitiş, hedefe yetişme olasılığı) dayanarak verdiğim karara güvendim."),
        new("RQ4_1", SurveyItemGroup.Rq4, "AI yorumlarındaki açıklamalar anlaşılırdı."),
        new("RQ4_2", SurveyItemGroup.Rq4, "Eksik iş önerileri planımı daha eksiksiz hâle getirmek için yararlıydı.")
    ];

    public static readonly IReadOnlyList<SurveyItem> AllItems = [.. SusItems, .. LikertItems];
}
