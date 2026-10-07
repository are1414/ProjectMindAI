namespace ProjectMind.Application.Ml;

/// <summary>
/// Gecikme modelinin girdileri. Hepsi gerçek bir projede de ölçülebilen oranlardır (simülatördeki gizli değişkenler
/// — verimlilik, tahmin iyimserliği vb. — bilerek dahil edilmez; model sonucu bu göstergelerden çıkarmak zorundadır).
/// </summary>
public sealed record DelayFeatures(
    float ElapsedRatio,       // geçen süre / planlanan süre
    float PercentComplete,    // EV / BAC
    float Spi,                // EV / PV
    float Cpi,                // EV / AC
    float SpiTime,            // ES / AT
    float RecentVelocity,     // son ~2 haftada kazanılan / planlanan
    float ScopeGrowth,        // eklenen efor / BAC
    float BlockedShare,       // bloke kalan efor payı
    float TeamSize)
{
    public static readonly string[] Names =
        [nameof(ElapsedRatio), nameof(PercentComplete), nameof(Spi), nameof(Cpi), nameof(SpiTime),
         nameof(RecentVelocity), nameof(ScopeGrowth), nameof(BlockedShare), nameof(TeamSize)];

    public float[] ToArray() =>
        [ElapsedRatio, PercentComplete, Spi, Cpi, SpiTime, RecentVelocity, ScopeGrowth, BlockedShare, TeamSize];

    public static DelayFeatures FromArray(float[] v) => new(v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8]);

    /// <summary>EVM karşılaştırma kuralı: Earned Schedule tahmini (PD / SPI(t)) %10 toleransı aşıyorsa "gecikecek".</summary>
    public bool EvmPredictsDelay(float tolerance) => SpiTime > 0 && 1f / SpiTime > 1f + tolerance;

    /// <summary>EVM'nin süre oranı tahmini (tahmini süre / planlanan süre = 1 / SPI(t)).</summary>
    public float EvmDurationRatio => SpiTime > 0 ? 1f / SpiTime : float.NaN;
}

/// <summary>Veri setindeki bir satır: hangi proje, hangi kontrol noktası, özellikler ve gerçek sonuç.</summary>
public sealed record DelaySample(int ProjectId, int CheckpointPercent, DelayFeatures Features, bool Delayed, float DurationRatio);
