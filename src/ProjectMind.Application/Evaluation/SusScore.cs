using ProjectMind.Application.Common;

namespace ProjectMind.Application.Evaluation;

/// <summary>
/// System Usability Scale puanı (Brooke, 1996). 10 madde, 1–5 Likert. Tek numaralı (olumlu) maddeler: cevap − 1;
/// çift numaralı (olumsuz) maddeler: 5 − cevap. Toplam × 2,5 → 0–100.
/// </summary>
public static class SusScore
{
    public const int ItemCount = 10;
    public const int MinAnswer = 1;
    public const int MaxAnswer = 5;
    private const decimal ScaleFactor = 2.5m;

    /// <summary>Sauro & Lewis karşılaştırma noktası: SUS ortalaması ≈ 68 (bir yüzde değil, "ortalama sistem").</summary>
    public const decimal AverageBenchmark = 68m;

    /// <summary>Cevaplar madde sırasıyla (SUS1 … SUS10). Eksik ya da 1–5 dışı cevap reddedilir.</summary>
    public static decimal Compute(IReadOnlyList<int> answers)
    {
        if (answers.Count != ItemCount)
            throw new BusinessRuleException($"SUS için {ItemCount} maddenin hepsi cevaplanmalı ({answers.Count} cevap var).");

        var sum = 0;
        for (var i = 0; i < ItemCount; i++)
        {
            var a = answers[i];
            if (a is < MinAnswer or > MaxAnswer)
                throw new BusinessRuleException($"SUS{i + 1} cevabı {MinAnswer}–{MaxAnswer} arasında olmalı.");
            // i = 0 → 1. madde (tek numaralı, olumlu).
            sum += i % 2 == 0 ? a - MinAnswer : MaxAnswer - a;
        }

        return sum * ScaleFactor;
    }

    /// <summary>
    /// Bangor, Kortum ve Miller (2009) sıfat ölçeği, "Determining What Individual SUS Scores Mean: Adding an Adjective
    /// Rating Scale", Journal of Usability Studies 4(3):114–123. Eşikler, makaledeki her sıfatın ortalama SUS puanıdır;
    /// puan bir sıfatın ortalamasına ulaştıysa o sıfat verilir (yaygın kullanılan sınıflama; varsayım).
    /// </summary>
    public static readonly IReadOnlyList<(decimal MinScore, string Adjective)> BangorBands =
    [
        (BestImaginable, "Hayal edilebilecek en iyi"),
        (Excellent, "Mükemmel"),
        (Good, "İyi"),
        (Ok, "Fena değil (OK)"),
        (Poor, "Zayıf"),
        (Awful, "Berbat"),
        (0m, "Hayal edilebilecek en kötü")
    ];

    public const decimal BestImaginable = 90.9m;
    public const decimal Excellent = 85.5m;
    public const decimal Good = 71.4m;
    public const decimal Ok = 50.9m;
    public const decimal Poor = 35.7m;
    public const decimal Awful = 20.3m;

    public static string Adjective(decimal score) => BangorBands.First(b => score >= b.MinScore).Adjective;
}
