using System.Text.Json;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.MissingWork;

/// <summary>LLM katmanının tek bir ek iş önerisi (doğrulanmış). Saat yok: büyüklük sınıfından C# atar.</summary>
public sealed record LlmMissingWorkSuggestion(string Name, WorkPhase Phase, Skill Skill, string Reason, WorkSize Size);

/// <summary>
/// Doğrulama sonucu: geçerli öneriler, şemaya uymadığı için düşürülen madde sayısı ve <see cref="LlmMissingWorkSchema.MaxItems"/>
/// sınırını aştığı için okunmayan (kesilen) madde sayısı.
/// </summary>
public sealed record LlmMissingWorkParseResult(
    IReadOnlyList<LlmMissingWorkSuggestion> Suggestions, int DroppedInvalid, int Truncated = 0);

/// <summary>
/// Hibrit eksik iş LLM katmanının JSON şeması ve C# doğrulayıcısı (CLAUDE.md §3, D26). Şema sağlayıcıya gönderilir;
/// sağlayıcının zorlamasına güvenilmez, cevap burada yeniden doğrulanır. Üst yapı bozuksa cevabın tamamı geçersizdir;
/// tek bir madde bozuksa (eksik alan, bilinmeyen faz/beceri/büyüklük, sayı gibi ek alan) yalnız o madde düşer.
/// Sınırdan fazla madde cevabı geçersiz kılmaz: ilk <see cref="MaxItems"/> madde okunur, kalanı kesilir ve sayısı raporlanır.
/// </summary>
public static class LlmMissingWorkSchema
{
    public const string Name = "missing_work_suggestions";

    /// <summary>Cevaptan okunan en fazla madde; fazlası kesilir (cevap geçersiz sayılmaz).</summary>
    public const int MaxItems = 10;

    public const int MaxNameLength = 120;
    public const int MaxReasonLength = 400;

    private const string SuggestionsField = "suggestions";
    private const string NameField = "name";
    private const string PhaseField = "phase";
    private const string SkillField = "skill";
    private const string ReasonField = "reason";
    private const string SizeField = "size";

    private static readonly string[] ItemFields = [NameField, PhaseField, SkillField, ReasonField, SizeField];

    private static readonly string[] PhaseNames = Enum.GetNames<WorkPhase>();
    private static readonly string[] SkillNames = Enum.GetNames<Skill>().Where(n => n != nameof(Skill.None)).ToArray();
    private static readonly string[] SizeNames = Enum.GetNames<WorkSize>();

    /// <summary>Gemini ve Claude yapılandırılmış çıktısının desteklediği sade alt küme (enum dahil).</summary>
    public static readonly JsonElement Schema = JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            [SuggestionsField] = new
            {
                type = "array",
                description = "Şablonların kapsamadığı ek eksik işler (boş liste olabilir).",
                items = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        [NameField] = new { type = "string", description = "Kısa iş adı (Türkçe)." },
                        [PhaseField] = new { type = "string", @enum = PhaseNames, description = "İşin fazı." },
                        [SkillField] = new { type = "string", @enum = SkillNames, description = "Gereken tek beceri." },
                        [ReasonField] = new { type = "string", description = "Bu işin neden gerektiği, 1 cümle (Türkçe)." },
                        [SizeField] = new { type = "string", @enum = SizeNames, description = "Büyüklük sınıfı: S küçük, M orta, L büyük. Saat yazma." }
                    },
                    required = ItemFields,
                    additionalProperties = false
                }
            }
        },
        required = new[] { SuggestionsField },
        additionalProperties = false
    });

    /// <summary>JSON metnini doğrular. Üst yapı geçersizse null ve Türkçe hata nedeni döner.</summary>
    public static LlmMissingWorkParseResult? TryParse(string? json, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "cevap boş";
            return null;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json.Trim());
        }
        catch (JsonException)
        {
            error = "cevap geçerli JSON değil";
            return null;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "cevap bir JSON nesnesi değil";
                return null;
            }
            var extra = root.EnumerateObject().Select(p => p.Name).FirstOrDefault(n => n != SuggestionsField);
            if (extra is not null)
            {
                error = $"şemada olmayan alan: {extra}";
                return null;
            }
            if (!root.TryGetProperty(SuggestionsField, out var array) || array.ValueKind != JsonValueKind.Array)
            {
                error = $"'{SuggestionsField}' alanı eksik veya liste değil";
                return null;
            }
            var truncated = Math.Max(array.GetArrayLength() - MaxItems, 0);

            var suggestions = new List<LlmMissingWorkSuggestion>();
            var dropped = 0;
            foreach (var item in array.EnumerateArray().Take(MaxItems))
            {
                var parsed = ParseItem(item);
                if (parsed is null)
                    dropped++;
                else
                    suggestions.Add(parsed);
            }
            return new LlmMissingWorkParseResult(suggestions, dropped, truncated);
        }
    }

    private static LlmMissingWorkSuggestion? ParseItem(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
            return null;
        if (item.EnumerateObject().Any(p => !ItemFields.Contains(p.Name, StringComparer.Ordinal)))
            return null;

        var name = Text(item, NameField, MaxNameLength);
        var reason = Text(item, ReasonField, MaxReasonLength);
        if (name is null || reason is null)
            return null;
        if (!TryEnum(item, PhaseField, PhaseNames, out WorkPhase phase)
            || !TryEnum(item, SkillField, SkillNames, out Skill skill)
            || !TryEnum(item, SizeField, SizeNames, out WorkSize size))
            return null;

        return new LlmMissingWorkSuggestion(name, phase, skill, reason, size);
    }

    private static string? Text(JsonElement item, string field, int maxLength)
    {
        if (!item.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.String)
            return null;
        var text = value.GetString()!.Trim();
        return text.Length == 0 || text.Length > maxLength ? null : text;
    }

    /// <summary>Yalnız şemadaki adlarla birebir eşleşen değer kabul edilir (sayısal enum veya bileşik bayrak değil).</summary>
    private static bool TryEnum<T>(JsonElement item, string field, string[] allowed, out T value) where T : struct, Enum
    {
        value = default;
        if (!item.TryGetProperty(field, out var element) || element.ValueKind != JsonValueKind.String)
            return false;
        var text = element.GetString();
        return text is not null && allowed.Contains(text, StringComparer.Ordinal) && Enum.TryParse(text, out value);
    }
}
