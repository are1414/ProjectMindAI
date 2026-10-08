using System.Globalization;
using System.Text.RegularExpressions;

namespace ProjectMind.Application.Ai;

/// <summary>
/// Halüsinasyon önlemi: AI cevabındaki sayıların, modele verilen veride (bağlam, kullanıcı mesajı, araç sonuçları,
/// öneri kartları) gerçekten bulunup bulunmadığını kontrol eder. Bulunmayanlar kullanıcıya işaretlenir.
/// Küçük tam sayılar (≤ 10: adet, sıra) ve sayının yuvarlanmış hali kabul edilir. Oran ↔ yüzde çevirisi (×100 / ÷100)
/// yalnızca cevaptaki sayının yanında "%" veya "yüzde" varsa kabul edilir. Tarihler (05.12.2026, 5.12.2026,
/// 5 Aralık 2026, 2026-12-05) gün bazında karşılaştırılır; kanıttaki tarihlerin yılı bilinen sayı sayılır.
/// Saatler (14:30) sayı olarak değerlendirilmez. Ayrıştırılamayan sayı benzeri ifadeler işaretlenir.
/// </summary>
public static partial class NumberGuard
{
    private const decimal SmallIntegerLimit = 10;
    private const decimal PercentScale = 100;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>Türkçe ay adları (küçük harf, Türkçe kültürle karşılaştırılır) → ay numarası.</summary>
    private static readonly string[] TurkishMonths =
        ["ocak", "şubat", "mart", "nisan", "mayıs", "haziran", "temmuz", "ağustos", "eylül", "ekim", "kasım", "aralık"];

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    private sealed record Token(string Raw, decimal Value, int Decimals, bool IsPercent);

    private sealed record DateToken(string Raw, DateOnly? Date, int? Month, int? Day);

    private sealed record Extraction(List<Token> Numbers, List<DateToken> Dates, List<string> Unparsed);

    public static IReadOnlyList<string> FindUnverified(string answer, string evidence)
    {
        // Kanıt (JSON / sistem metni) nokta ondalıklı okunur; cevap Türkçe biçimde (1.250.000,5).
        var proof = Extract(evidence, turkish: false);
        var evidenceDates = proof.Dates.Where(d => d.Date is not null).Select(d => d.Date!.Value).ToHashSet();
        var known = proof.Numbers.Select(t => t.Value)
            .Concat(evidenceDates.Select(d => (decimal)d.Year))
            .Distinct()
            .ToList();
        var rawTokens = proof.Numbers.Select(t => t.Raw).ToHashSet();

        var claimed = Extract(answer, turkish: true);
        var unverified = new List<string>();

        foreach (var date in claimed.Dates)
        {
            var verified = date.Date is { } full
                ? evidenceDates.Contains(full)
                : date.Month is { } month && date.Day is { } day && evidenceDates.Any(d => d.Month == month && d.Day == day);
            if (!verified)
                unverified.Add(date.Raw);
        }
        foreach (var token in claimed.Numbers)
        {
            if (token.Value <= SmallIntegerLimit && token.Value == decimal.Truncate(token.Value))
                continue;
            if (rawTokens.Contains(token.Raw) || known.Any(k => Matches(token, k)))
                continue;
            unverified.Add(token.Raw);
        }
        unverified.AddRange(claimed.Unparsed);
        return unverified.Distinct().ToList();
    }

    private static bool Matches(Token token, decimal known)
    {
        IEnumerable<decimal> candidates = token.IsPercent
            ? [known, known * PercentScale, known / PercentScale]
            : [known];
        return candidates.Any(c => Math.Round(c, token.Decimals, MidpointRounding.AwayFromZero) == token.Value);
    }

    private static Extraction Extract(string text, bool turkish)
    {
        var dates = new List<DateToken>();

        // Tarihler önce çıkarılır ve metinden silinir (aynı uzunlukta boşlukla — konumlar korunur).
        var rest = MaskMatches(text, IsoDateRegex(), m =>
            dates.Add(new DateToken(m.Value, ToDate(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value), null, null)));
        rest = MaskMatches(rest, NumericDateRegex(), m =>
            dates.Add(new DateToken(m.Value, ToDate(m.Groups[3].Value, m.Groups[2].Value, m.Groups[1].Value), null, null)));
        rest = MaskMatches(rest, MonthNameDateRegex(), m =>
        {
            var month = Array.IndexOf(TurkishMonths, m.Groups[2].Value.ToLower(Turkish)) + 1;
            var day = int.Parse(m.Groups[1].Value, Invariant);
            if (m.Groups[3].Success)
                dates.Add(new DateToken(m.Value, ToDate(m.Groups[3].Value, month.ToString(Invariant), m.Groups[1].Value), null, null));
            else
                dates.Add(new DateToken(m.Value, null, month, day));
        });
        rest = MaskMatches(rest, TimeRegex(), _ => { });   // 14:30 → sayı değil

        var numbers = new List<Token>();
        var unparsed = new List<string>();
        foreach (Match m in NumberRegex().Matches(rest))
        {
            var raw = m.Value;
            if (WbsRegex().IsMatch(raw))
                continue;   // 1.2.1 gibi WBS numarası
            if (TryParse(raw, turkish, out var value, out var decimals))
                numbers.Add(new Token(raw, value, decimals, IsPercentContext(rest, m.Index, m.Length)));
            else
                unparsed.Add(raw);
        }

        // Geçersiz tarih (ör. 31.02.2026; Date ve Month boş) cevapta doğrulanamaz; kanıtta yok sayılır.
        return new Extraction(numbers, dates, unparsed);
    }

    private static string MaskMatches(string text, Regex regex, Action<Match> onMatch) =>
        regex.Replace(text, m =>
        {
            onMatch(m);
            return new string(' ', m.Length);
        });

    private static DateOnly? ToDate(string year, string month, string day) =>
        int.TryParse(year, Invariant, out var y) && int.TryParse(month, Invariant, out var mo) && int.TryParse(day, Invariant, out var d)
        && mo is >= 1 and <= 12 && d >= 1 && d <= DateTime.DaysInMonth(Math.Clamp(y, 1, 9999), mo)
            ? new DateOnly(y, mo, d)
            : null;

    /// <summary>"%87", "% 87", "87%", "yüzde 87" → yüzde bağlamı.</summary>
    private static bool IsPercentContext(string text, int index, int length)
    {
        var before = text[..index].TrimEnd();
        if (before.EndsWith('%') || before.EndsWith("yüzde", StringComparison.OrdinalIgnoreCase))
            return true;
        var after = text[(index + length)..].TrimStart();
        return after.StartsWith('%');
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

    [GeneratedRegex(@"\b(\d{4})-(\d{2})-(\d{2})(?=\b|T)")]
    private static partial Regex IsoDateRegex();

    [GeneratedRegex(@"\b(\d{1,2})[./](\d{1,2})[./](\d{4})\b")]
    private static partial Regex NumericDateRegex();

    [GeneratedRegex(@"\b(\d{1,2})\s+(ocak|şubat|mart|nisan|mayıs|haziran|temmuz|ağustos|eylül|ekim|kasım|aralık)(?:\s+(\d{4})\b)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MonthNameDateRegex();

    [GeneratedRegex(@"\b([01]?\d|2[0-3]):[0-5]\d(?::[0-5]\d)?\b")]
    private static partial Regex TimeRegex();

    [GeneratedRegex(@"\d+(?:[.,]\d+)*")]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"^\d{1,3}(?:\.\d{3})+$")]
    private static partial Regex ThousandsRegex();

    [GeneratedRegex(@"^\d{1,2}(?:\.\d{1,2}){2,}$")]
    private static partial Regex WbsRegex();
}
