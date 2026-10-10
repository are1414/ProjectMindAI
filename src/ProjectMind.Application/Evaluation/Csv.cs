using System.Globalization;
using System.Text;

namespace ProjectMind.Application.Evaluation;

/// <summary>İndirilecek CSV dosyası. İçerik UTF-8 BOM (U+FEFF) ile başlar; Excel Türkçe karakterleri doğru açar.</summary>
public sealed record CsvFile(string FileName, string Content);

/// <summary>
/// RFC 4180 CSV üretimi: virgül ayırıcı, CRLF satır sonu, gerekirse çift tırnak. Sayılar ve tarihler kültürden
/// bağımsızdır (nokta ondalık, ISO tarih); R/Python/Excel'de aynı okunur.
/// </summary>
public static class Csv
{
    public const char Bom = '﻿';
    public const string NewLine = "\r\n";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Build(IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string>> rows)
    {
        var sb = new StringBuilder().Append(Bom);
        sb.Append(string.Join(',', header.Select(Field))).Append(NewLine);
        foreach (var row in rows)
        {
            if (row.Count != header.Count)
                throw new InvalidOperationException($"CSV satırında {row.Count} alan var, başlıkta {header.Count}.");
            sb.Append(string.Join(',', row.Select(Field))).Append(NewLine);
        }

        return sb.ToString();
    }

    public static string Field(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }

    public static string Num(decimal? v, string format = "0.####") => v?.ToString(format, Inv) ?? "";
    public static string Num(double? v, string format = "0.####") => v is { } d && !double.IsNaN(d) ? d.ToString(format, Inv) : "";
    public static string Int(long? v) => v?.ToString(Inv) ?? "";
    public static string Bool(bool? v) => v is null ? "" : v.Value ? "1" : "0";
    public static string Date(DateOnly? d) => d?.ToString("yyyy-MM-dd", Inv) ?? "";
    public static string Time(DateTime? t) => t?.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", Inv) ?? "";
}
