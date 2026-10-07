using ProjectMind.Application.Ai;

namespace ProjectMind.Tests.Unit;

public class NumberGuardTests
{
    private const string Evidence = """
        {"spi":0.92,"cpi":0.875,"plannedFinish":"2026-12-02","forecastFinish":"2026-12-09","budget":8000000,
         "remainingHours":188,"percentComplete":55,"healthScore":72.4,"varianceWorkdays":5}
        """;

    [Theory]
    [InlineData("SPI 0,92 ve CPI 0,88; yani plana göre geridesiniz.")]           // Türkçe ondalık + yuvarlama
    [InlineData("Tahmini bitiş 09.12.2026, planlanan 02.12.2026.")]              // tarih biçimi çevrilir
    [InlineData("Bütçe 8.000.000 TL, kalan efor 188 saat, sağlık 72,4.")]       // binlik ayırıcı
    [InlineData("İşin %55'i tamamlandı ve 5 iş günü gecikme var.")]
    [InlineData("3 öneri hazırladım; 1.2.1 işini güncelledim.")]                 // küçük sayı ve WBS kodu
    public void Numbers_present_in_evidence_are_accepted(string answer) =>
        Assert.Empty(NumberGuard.FindUnverified(answer, Evidence));

    [Fact]
    public void Invented_numbers_and_dates_are_reported()
    {
        var unverified = NumberGuard.FindUnverified("Proje 18 gün gecikecek, bitiş 20.12.2026 ve maliyet 420.000 TL artar.", Evidence);
        Assert.Equal(["20.12.2026", "18", "420.000"], unverified);
    }

    [Fact]
    public void Percent_written_as_ratio_is_accepted() =>
        Assert.Empty(NumberGuard.FindUnverified("Maliyet performansı %87,5 düzeyinde.", Evidence));
}
