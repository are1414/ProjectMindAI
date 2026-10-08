namespace ProjectMind.Application.MissingWork;

/// <summary>LLM'in eksik iş önerisi için döndürdüğü büyüklük sınıfı. Saat, C#'ta <see cref="MissingWorkOptions"/>'tan atanır.</summary>
public enum WorkSize
{
    S,
    M,
    L
}

/// <summary>
/// Hibrit eksik iş önerisi ayarları (ayar bölümü "MissingWork"). LLM sayı üretmez (D9, D26): yalnız büyüklük sınıfı
/// döndürür; önerinin saati buradaki isimli değerlerden gelir.
/// </summary>
public sealed class MissingWorkOptions
{
    public const string Section = "MissingWork";

    /// <summary>Büyüklük sınıfı → tahmini efor (saat).</summary>
    public SizeHoursOptions SizeHours { get; set; } = new();

    /// <summary>LLM katmanından en fazla kaç ek öneri kart olarak gösterilir (tekrarlar elendikten sonra).</summary>
    public int MaxLlmSuggestions { get; set; } = 5;

    /// <summary>LLM'e gönderilen en fazla mevcut iş adı (bağlam boyutu sınırı).</summary>
    public int MaxContextWorkItems { get; set; } = 150;

    public decimal HoursFor(WorkSize size) => size switch
    {
        WorkSize.S => SizeHours.S,
        WorkSize.M => SizeHours.M,
        WorkSize.L => SizeHours.L,
        _ => throw new ArgumentOutOfRangeException(nameof(size), size, null)
    };
}

public sealed class SizeHoursOptions
{
    /// <summary>Küçük iş (yaklaşık 1 iş günü).</summary>
    public decimal S { get; set; } = 8;

    /// <summary>Orta iş (yaklaşık 3 iş günü).</summary>
    public decimal M { get; set; } = 24;

    /// <summary>Büyük iş (yaklaşık 2 hafta).</summary>
    public decimal L { get; set; } = 80;
}
