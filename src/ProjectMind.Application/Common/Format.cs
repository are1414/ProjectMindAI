using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;

namespace ProjectMind.Application.Common;

/// <summary>Kullanıcıya gösterim için Türkçe biçimlendirme (arayüz ve öneri özetleri ortak kullanır).</summary>
public static class Format
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static string Name(Enum value)
    {
        var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
        return member?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? value.ToString();
    }

    /// <summary>Flags enum değerini (ör. Backend | Database) "Backend, Veritabanı" olarak yazar.</summary>
    public static string Flags<T>(T value) where T : struct, Enum =>
        string.Join(", ", Enum.GetValues<T>()
            .Where(v => Convert.ToInt64(v) != 0 && value.HasFlag(v))
            .Select(v => Name(v)));

    public static string Date(DateOnly? date) => date?.ToString("dd.MM.yyyy", Turkish) ?? "—";

    public static string Number(decimal value) => value.ToString("#,0.##", Turkish);

    public static string Money(decimal value, string currency) => $"{value.ToString("N0", Turkish)} {currency}";
}
