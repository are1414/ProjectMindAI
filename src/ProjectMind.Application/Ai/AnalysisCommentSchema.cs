using System.Text.Json;

namespace ProjectMind.Application.Ai;

/// <summary>AI analiz yorumu (proje durumu / senaryo karşılaştırması). Metinler LLM'den, sayılar bağlamdan gelir.</summary>
public sealed record AnalysisComment(
    string Summary,
    IReadOnlyList<string> KeyFindings,
    IReadOnlyList<string> RecommendedActions,
    IReadOnlyList<string> Caveats)
{
    /// <summary>Sayı doğrulaması için yorumun tüm metni.</summary>
    public string AllText() => string.Join("\n", [Summary, .. KeyFindings, .. RecommendedActions, .. Caveats]);
}

/// <summary>
/// Analiz yorumunun JSON şeması ve C# doğrulayıcısı (CLAUDE.md §3: LLM cevabı şema ile istenir ve doğrulanır).
/// Şema sağlayıcıya gönderilir (Gemini responseJsonSchema, Claude structured output); sağlayıcının zorlamasına
/// güvenilmez, cevap burada yeniden doğrulanır. Geçersiz cevap kullanıcıya gösterilmez.
/// </summary>
public static class AnalysisCommentSchema
{
    public const string Name = "analysis_comment";

    /// <summary>Özet metninin en fazla uzunluğu (karakter); aşan cevap geçersizdir.</summary>
    public const int MaxSummaryLength = 1500;

    /// <summary>Bir listedeki en fazla madde; aşan cevap geçersizdir (kart okunabilir kalsın).</summary>
    public const int MaxItemsPerList = 8;

    /// <summary>Bir maddenin en fazla uzunluğu (karakter).</summary>
    public const int MaxItemLength = 600;

    private const string SummaryField = "summary";
    private const string KeyFindingsField = "keyFindings";
    private const string RecommendedActionsField = "recommendedActions";
    private const string CaveatsField = "caveats";

    private static readonly string[] ListFields = [KeyFindingsField, RecommendedActionsField, CaveatsField];

    /// <summary>
    /// Hem Gemini hem Claude yapılandırılmış çıktısının desteklediği sade JSON Schema alt kümesi
    /// (type, properties, required, items, additionalProperties=false, description).
    /// </summary>
    public static readonly JsonElement Schema = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "summary": { "type": "string", "description": "2-4 cümlelik genel değerlendirme (Türkçe)." },
            "keyFindings": { "type": "array", "items": { "type": "string" }, "description": "En önemli bulgular; sayılar yalnız verideki değerler." },
            "recommendedActions": { "type": "array", "items": { "type": "string" }, "description": "Proje yöneticisine somut aksiyon önerileri." },
            "caveats": { "type": "array", "items": { "type": "string" }, "description": "Varsayımlar ve belirsizlikler (ör. sentetik ML modeli, Brooks etkisi)." }
          },
          "required": ["summary", "keyFindings", "recommendedActions", "caveats"],
          "additionalProperties": false
        }
        """).RootElement.Clone();

    /// <summary>JSON metnini şemaya göre doğrular; geçersizse null ve Türkçe hata nedeni döner.</summary>
    public static AnalysisComment? TryParse(string? json, out string? error)
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

            var allowed = new HashSet<string>([SummaryField, .. ListFields], StringComparer.Ordinal);
            var extra = root.EnumerateObject().Select(p => p.Name).FirstOrDefault(n => !allowed.Contains(n));
            if (extra is not null)
            {
                error = $"şemada olmayan alan: {extra}";
                return null;
            }

            if (!root.TryGetProperty(SummaryField, out var summary) || summary.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(summary.GetString()))
            {
                error = $"'{SummaryField}' alanı eksik veya boş";
                return null;
            }
            if (summary.GetString()!.Length > MaxSummaryLength)
            {
                error = $"'{SummaryField}' çok uzun";
                return null;
            }

            var lists = new Dictionary<string, IReadOnlyList<string>>();
            foreach (var field in ListFields)
            {
                if (!root.TryGetProperty(field, out var array) || array.ValueKind != JsonValueKind.Array)
                {
                    error = $"'{field}' alanı eksik veya liste değil";
                    return null;
                }
                if (array.GetArrayLength() > MaxItemsPerList)
                {
                    error = $"'{field}' listesinde çok fazla madde var";
                    return null;
                }

                var items = new List<string>();
                foreach (var item in array.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String)
                    {
                        error = $"'{field}' listesinde metin olmayan madde var";
                        return null;
                    }
                    var text = item.GetString()!.Trim();
                    if (text.Length > MaxItemLength)
                    {
                        error = $"'{field}' listesinde çok uzun madde var";
                        return null;
                    }
                    if (text.Length > 0)
                        items.Add(text);
                }
                lists[field] = items;
            }

            return new AnalysisComment(summary.GetString()!.Trim(),
                lists[KeyFindingsField], lists[RecommendedActionsField], lists[CaveatsField]);
        }
    }
}
