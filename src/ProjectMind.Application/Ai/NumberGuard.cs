using System.Globalization;
using System.Text.RegularExpressions;

namespace ProjectMind.Application.Ai;

/// <summary>
/// Halüsinasyon önlemi: AI cevabındaki sayıların, modele verilen veride (bağlam, kullanıcı mesajı, araç sonuçları,
/// öneri kartları) gerçekten bulunup bulunmadığını kontrol eder. Bulunmayanlar kullanıcıya işaretlenir.
/// Küçük tam sayılar (≤ 10: adet, sıra) ve sayının yuvarlanmış/yüzdeye çevrilmiş hali kabul edilir.
/// </summary>
public static partial class NumberGuard
{
    private const decimal SmallIntegerLimit = 10;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static IReadOnlyList<string> FindUnverified(string answer, string evidence)
    {
        // Kanıt (JSON / sistem metni) nokta ondalıklı okunur; cevap Türkçe biçimde (1.250.000,5).
        var evidenceTokens = Extract(evidence, turkish: false).ToList();
        var known = evidenceTokens.Where(t => !t.IsDate).Select(t => t.Value).ToList();
        var rawTokens = evidenceTokens.Select(t => t.Raw).ToHashSet();
        var dates = evidenceTokens.Where(t => t.IsDate).Select(t => t.Iso).ToHashSet();

        var unverified = new List<string>();
        foreach (var token in Extract(answer, turkish: true))
        {
            if (token.IsDate)
            {
                if (!dates.Contains(token.Iso))
                    unverified.Add(token.Raw);
                continue;
            }
            if (token.Value <= SmallIntegerLimit && token.Value == decimal.Truncate(token.Value))
                continue;
            if (rawTokens.Contains(token.Raw) || known.Any(k => Matches(token, k)))
                continue;
            unverified.Add(token.Raw);
        }
        return unverified.Distinct().ToList();
    }

    private static bool Matches((string Raw, decimal Value, int Decimals, bool IsDate, string Iso) token, decimal known)
    {
        foreach (var candidate in new[] { known, known * 100, known / 100 })
        {
            if (Math.Round(candidate, token.Decimals, MidpointRounding.AwayFromZero) == token.Value)
                return true;
        }
        return false;
    }

    private static IEnumerable<(string Raw, decimal Value, int Decimals, bool IsDate, string Iso)> Extract(string text, bool turkish)
    {
        // Tarihler: 05.12.2026 (Türkçe) veya 2026-12-05 (ISO) → ISO'ya çevrilir.
        foreach (Match m in TurkishDateRegex().Matches(text))
            yield return (m.Value, 0, 0, true, $"{m.Groups[3].Value}-{m.Groups[2].Value}-{m.Groups[1].Value}");
        foreach (Match m in DateRegex().Matches(text))
            yield return (m.Value, 0, 0, true, m.Value);

        var withoutDates = DateRegex().Replace(TurkishDateRegex().Replace(text, " "), " ");
        foreach (Match m in NumberRegex().Matches(withoutDates))
        {
            var raw = m.Value;
            if (WbsRegex().IsMatch(raw))
                continue;   // 1.2.1 gibi WBS numarası
            if (TryParse(raw, turkish, out var value, out var decimals))
                yield return (raw, value, decimals, false, "");
        }
    }

    /// <summary>Türkçe (1.250.000,50) veya JSON (1250000.50) biçimini çözer.</summary>
    private static bool TryParse(string raw, bool turkish, out decimal value, out int decimals)
    {
        string normalized;
        if (!turkish)
            normalized = raw.Replace(",", "");
        else if (raw.Contains(','))
            normalized = raw.Replace(".", "").Replace(',', '.');
        else if (ThousandsRegex().IsMatch(raw) && !raw.StartsWith("0.", StringComparison.Ordinal))
            normalized = raw.Replace(".", "");
        else
            normalized = raw;

        var dot = normalized.IndexOf('.');
        decimals = dot < 0 ? 0 : normalized.Length - dot - 1;
        return decimal.TryParse(normalized, NumberStyles.Number, Invariant, out value);
    }

    [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}\b")]
    private static partial Regex DateRegex();

    [GeneratedRegex(@"\b(\d{2})\.(\d{2})\.(\d{4})\b")]
    private static partial Regex TurkishDateRegex();

    [GeneratedRegex(@"\d+(?:[.,]\d+)*")]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"^\d{1,3}(?:\.\d{3})+$")]
    private static partial Regex ThousandsRegex();

    [GeneratedRegex(@"^\d{1,2}(?:\.\d{1,2}){2,}$")]
    private static partial Regex WbsRegex();
}
