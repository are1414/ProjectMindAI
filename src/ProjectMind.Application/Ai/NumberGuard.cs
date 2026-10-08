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
/// Kanıt JSON ve Türkçe biçimli metin karışımıdır: virgüllü sayı Türkçe ondalık okunur (JSON sayısı virgül içermez; "[" ile
/// başlayan virgüllü dizi JSON listesidir), "1.250.000" gibi binlik gruplu sayı hem JSON hem Türkçe okunur.
/// "1,2 milyon", "120 bin", "3 milyar" çarpanlı yazımlar (cevapta ve kanıtta) çarpılmış değerle karşılaştırılır; cevaptaki
/// çarpanlı sayı, kanıttaki değerin o birime yuvarlanmış hâliyle eşleşmelidir (1.250.000 ↔ "1,25 milyon" / "1,3 milyon").
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

    /// <summary>Value: çarpanla çarpılmış değer; Multiplier: "bin/milyon/milyar" çarpanı (yoksa 1).</summary>
    private sealed record Token(string Raw, decimal Value, int Decimals, bool IsPercent, decimal Multiplier = 1);

    /// <summary>Türkçe sayı çarpanları ("120 bin", "1,2 milyon").</summary>
    private static readonly Dictionary<string, decimal> Multipliers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bin"] = 1_000m,
        ["milyon"] = 1_000_000m,
        ["milyar"] = 1_000_000_000m
    };

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
            if (token.Multiplier == 1 && token.Value <= SmallIntegerLimit && token.Value == decimal.Truncate(token.Value))
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
        // Çarpanlı sayı (1,2 milyon) çarpan biriminde karşılaştırılır: round(1.250.000 / 1.000.000; 1) = 1,3.
        var claimed = token.Value / token.Multiplier;
        return candidates.Any(c => Math.Round(c / token.Multiplier, token.Decimals, MidpointRounding.AwayFromZero) == claimed);
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
            var percent = IsPercentContext(rest, m.Index, m.Length);
            var multiplierWord = MultiplierRegex().Match(rest, m.Index + m.Length);
            var multiplier = multiplierWord.Success ? Multipliers[multiplierWord.Groups[1].Value] : 1m;
            var shown = multiplierWord.Success ? $"{raw} {multiplierWord.Groups[1].Value}" : raw;

            var values = turkish ? AnswerValues(raw) : EvidenceValues(raw, PrecededByBracket(rest, m.Index));
            if (values.Count == 0)
                unparsed.Add(shown);
            foreach (var (value, decimals) in values)
                numbers.Add(new Token(shown, value * multiplier, decimals, percent, multiplier));
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

    /// <summary>Cevaptaki sayı Türkçe biçimdedir (1.250.000,50); tek değer.</summary>
    private static List<(decimal Value, int Decimals)> AnswerValues(string raw) =>
        TryParseTurkish(raw, out var value, out var decimals) ? [(value, decimals)] : [];

    /// <summary>
    /// Kanıttaki sayının olası değerleri: virgüllü sayı Türkçe ondalıktır ("0,87" → 0,87), "[" ile başlıyorsa JSON listesidir
    /// ("[8,16]" → 8 ve 16). Virgülsüz sayı JSON (nokta ondalık) okunur; binlik gruplu yazımsa ("1.250.000", "250.000")
    /// Türkçe değeri de eklenir.
    /// </summary>
    private static List<(decimal Value, int Decimals)> EvidenceValues(string raw, bool jsonList)
    {
        var values = new List<(decimal, int)>();
        if (raw.Contains(','))
        {
            if (jsonList)
            {
                foreach (var part in raw.Split(','))
                    if (TryParseInvariant(part, out var v, out var d))
                        values.Add((v, d));
            }
            else if (TryParseTurkish(raw, out var v, out var d))
                values.Add((v, d));
            return values;
        }

        if (TryParseInvariant(raw, out var json, out var jsonDecimals))
            values.Add((json, jsonDecimals));
        if (IsTurkishThousands(raw) && TryParseTurkish(raw, out var tr, out var trDecimals))
            values.Add((tr, trDecimals));
        return values;
    }

    private static bool IsTurkishThousands(string raw) =>
        ThousandsRegex().IsMatch(raw) && !raw.StartsWith("0.", StringComparison.Ordinal);

    private static bool TryParseTurkish(string raw, out decimal value, out int decimals)
    {
        var normalized = raw.Contains(',') ? raw.Replace(".", "").Replace(',', '.')
            : IsTurkishThousands(raw) ? raw.Replace(".", "")
            : raw;
        return TryParseInvariant(normalized, out value, out decimals);
    }

    private static bool TryParseInvariant(string normalized, out decimal value, out int decimals)
    {
        var dot = normalized.IndexOf('.');
        decimals = dot < 0 ? 0 : normalized.Length - dot - 1;
        return decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, Invariant, out value);
    }

    private static bool PrecededByBracket(string text, int index) => text[..index].TrimEnd().EndsWith('[');

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

    [GeneratedRegex(@"\G\s+(bin|milyon|milyar)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MultiplierRegex();

    [GeneratedRegex(@"^\d{1,3}(?:\.\d{3})+$")]
    private static partial Regex ThousandsRegex();

    [GeneratedRegex(@"^\d{1,2}(?:\.\d{1,2}){2,}$")]
    private static partial Regex WbsRegex();
}
