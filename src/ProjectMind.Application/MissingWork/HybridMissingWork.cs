using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.MissingWork;

/// <summary>Kart olarak önerilmeye hazır LLM ek işi: saat büyüklük sınıfından C#'ta atanmıştır.</summary>
public sealed record LlmMissingWorkItem(string Name, WorkPhase Phase, Skill Skill, string Reason, WorkSize Size, decimal Hours);

/// <summary>
/// Elenen LLM önerisi ve nedeni (denetim / cevap): <see cref="DuplicateOf"/> doluysa tekrar sayıldığı ad; boşsa tekrar değil,
/// <see cref="MissingWorkOptions.MaxLlmSuggestions"/> üst sınırı dolduğu için gösterilmedi.
/// </summary>
public sealed record DroppedSuggestion(string Name, string? DuplicateOf)
{
    public bool OverLimit => DuplicateOf is null;
}

/// <summary>
/// Hibrit eksik işin deterministik parçaları (D26): LLM önerilerini mevcut işlerle ve kural sonuçlarıyla ad benzerliğine
/// göre eleme, büyüklük → saat ataması ve öneri kartının kaynağının (Rule / Llm / User) C#'ta belirlenmesi.
/// Ad karşılaştırması <see cref="MissingWorkDetector.Normalize"/> ile yapılır (Türkçe karakter sadeleştirme).
/// </summary>
public static class HybridMissingWork
{
    /// <summary>Kelime benzerliğinde dikkate alınmayan bağlaçlar / dolgu kelimeleri.</summary>
    private static readonly HashSet<string> StopWords = ["ve", "ile", "icin", "bir", "the", "and", "of", "for"];

    /// <summary>Kök eşleşmesi (ör. "test" ~ "testi") için kısa kelimenin en az uzunluğu.</summary>
    private const int MinStemLength = 4;

    /// <summary>
    /// Kök eşleşmesinde uzun kelimenin kökten fazla en çok harfi (Türkçe ek: "testi", "testleri"). Daha uzun fark ayrı kelimedir
    /// ("veri" ≠ "veritabani").
    /// </summary>
    private const int MaxSuffixLength = 4;

    /// <summary>
    /// İki iş adı aynı işi mi anlatıyor? Sadeleştirilmiş adlar eşitse veya kısa adın tüm anlamlı kelimeleri uzun adda
    /// (aynen ya da ortak kökle: "test" ~ "testi") geçiyorsa evet. Kısa ad tek anlamlı kelimeyse yalnız tam eşitlik sayılır:
    /// "Test" ≠ "Güvenlik testi", "API" ≠ "API dokümantasyonu" (genel tek kelime geçerli önerileri elemesin; Tur 4a H5).
    /// </summary>
    public static bool IsSameWork(string a, string b)
    {
        var na = MissingWorkDetector.Normalize(a);
        var nb = MissingWorkDetector.Normalize(b);
        if (na.Length == 0 || nb.Length == 0)
            return false;
        if (na == nb)
            return true;

        var ta = Tokens(na);
        var tb = Tokens(nb);
        if (ta.Count == 0 || tb.Count == 0)
            return false;
        var (shorter, longer) = ta.Count <= tb.Count ? (ta, tb) : (tb, ta);
        if (shorter.Count == 1)
            return false;   // tek kelime: yalnız (yukarıdaki) tam eşitlik
        return shorter.All(s => longer.Any(l => SameToken(s, l)));
    }

    private static List<string> Tokens(string normalized) =>
        normalized.Split([' ', '/', '-'], StringSplitOptions.RemoveEmptyEntries).Where(t => !StopWords.Contains(t)).ToList();

    private static bool SameToken(string a, string b)
    {
        if (a == b)
            return true;
        var (stem, word) = a.Length <= b.Length ? (a, b) : (b, a);
        return stem.Length >= MinStemLength && word.Length - stem.Length <= MaxSuffixLength
               && word.StartsWith(stem, StringComparison.Ordinal);
    }

    /// <summary>
    /// LLM önerilerinden mevcut işleri, kural (şablon) önerilerini veya birbirini tekrar edenleri eler; kalanlara
    /// büyüklük sınıfına göre saat atar ve en fazla <see cref="MissingWorkOptions.MaxLlmSuggestions"/> tanesini döner.
    /// Elenenler (tekrar veya üst sınır) ayrıca döner.
    /// </summary>
    public static (IReadOnlyList<LlmMissingWorkItem> Kept, IReadOnlyList<DroppedSuggestion> Dropped) Filter(
        IReadOnlyList<LlmMissingWorkSuggestion> suggestions,
        IEnumerable<string> existingWorkNames,
        IEnumerable<string> ruleSuggestionNames,
        MissingWorkOptions options)
    {
        var taken = existingWorkNames.Concat(ruleSuggestionNames).ToList();
        var kept = new List<LlmMissingWorkItem>();
        var dropped = new List<DroppedSuggestion>();
        foreach (var s in suggestions)
        {
            var duplicateOf = taken.FirstOrDefault(t => IsSameWork(s.Name, t))
                              ?? kept.Select(k => k.Name).FirstOrDefault(k => IsSameWork(s.Name, k));
            if (duplicateOf is not null)
            {
                dropped.Add(new DroppedSuggestion(s.Name, duplicateOf));
                continue;
            }
            if (kept.Count >= options.MaxLlmSuggestions)
            {
                dropped.Add(new DroppedSuggestion(s.Name, null));   // üst sınır: tekrar değil, kayda ve cevaba yazılır
                continue;
            }
            kept.Add(new LlmMissingWorkItem(s.Name, s.Phase, s.Skill, s.Reason, s.Size, options.HoursFor(s.Size)));
        }
        return (kept, dropped);
    }

    /// <summary>
    /// Sohbetten gelen iş kartının kaynağı: adı kural (şablon) önerisiyle eşleşen → Rule, son LLM katmanı önerisiyle
    /// eşleşen → Llm, diğerleri → User. Modelin beyanına bakılmaz; eşleşme sadeleştirilmiş adın eşitliğidir.
    /// </summary>
    public static AiActionSource ResolveSource(string workName, IEnumerable<string> ruleSuggestionNames, IEnumerable<string> llmSuggestionNames)
    {
        var normalized = MissingWorkDetector.Normalize(workName);
        if (normalized.Length == 0)
            return AiActionSource.User;
        if (ruleSuggestionNames.Any(r => MissingWorkDetector.Normalize(r) == normalized))
            return AiActionSource.Rule;
        if (llmSuggestionNames.Any(l => MissingWorkDetector.Normalize(l) == normalized))
            return AiActionSource.Llm;
        return AiActionSource.User;
    }
}
