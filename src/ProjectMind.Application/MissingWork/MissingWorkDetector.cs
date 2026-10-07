using System.Globalization;
using System.Text;
using ProjectMind.Application.WorkItems;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.MissingWork;

public sealed record MissingWorkItem(
    string TemplateKey, string Name, WorkPhase Phase, Skill Skill, decimal DefaultHours, string Reason,
    IReadOnlyList<string> SuggestedSuccessors);

public sealed record MissingWorkResult(
    IReadOnlyList<MissingWorkItem> Missing,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Covered)
{
    public decimal TotalDefaultHours => Missing.Sum(m => m.DefaultHours);
}

/// <summary>
/// Kural tabanlı eksik iş tespiti: proje tipine uygun şablonlar, mevcut işlerin ad/açıklamalarıyla anahtar kelime
/// üzerinden eşleştirilir; hiçbir işle eşleşmeyen şablon "eksik" sayılır. Deterministiktir ve açıklanabilir.
/// </summary>
public static class MissingWorkDetector
{
    private const int ShortKeywordLength = 3;

    public static MissingWorkResult Detect(ProjectType type, IReadOnlyList<WorkItemResponse> items)
    {
        var templates = WorkTemplateCatalog.For(type).ToList();
        var texts = items.Select(i => (Item: i, Text: Normalize($"{i.Name} {i.Description}"))).ToList();

        var covered = new Dictionary<string, IReadOnlyList<string>>();
        foreach (var template in templates)
        {
            var matches = texts.Where(t => Matches(t.Text, template.Keywords)).Select(t => t.Item.Name).ToList();
            if (matches.Count > 0)
                covered[template.Key] = matches;
        }

        var missing = templates
            .Where(t => !covered.ContainsKey(t.Key))
            .Select(t => new MissingWorkItem(
                t.Key, t.Name, t.Phase, t.Skill, t.DefaultHours, t.Reason,
                // Eksik iş, mevcut hangi işlerden önce bitmeli? (Finish-to-Start önerisi)
                (t.Before ?? []).Where(covered.ContainsKey).SelectMany(k => covered[k]).Distinct().ToList()))
            .ToList();

        return new MissingWorkResult(missing, covered);
    }

    public static bool Matches(string normalizedText, IEnumerable<string> keywords)
    {
        var tokens = normalizedText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var keyword in keywords.Select(Normalize))
        {
            if (keyword.Contains(' ') || keyword.Contains('/') || keyword.Contains('-'))
            {
                if (normalizedText.Contains(keyword))
                    return true;
            }
            else if (keyword.Length <= ShortKeywordLength
                ? tokens.Contains(keyword)
                : tokens.Any(token => token.StartsWith(keyword, StringComparison.Ordinal)))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Küçük harf, Türkçe karakter sadeleştirme (ı→i, ş→s…), harf/rakam dışı karakterler boşluk.</summary>
    public static string Normalize(string text)
    {
        var lower = text.ToLower(CultureInfo.GetCultureInfo("tr-TR"))
            .Replace('ı', 'i').Replace('ş', 's').Replace('ğ', 'g').Replace('ü', 'u').Replace('ö', 'o').Replace('ç', 'c');
        var decomposed = lower.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(char.IsLetterOrDigit(c) || c is '/' or '-' ? c : ' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
