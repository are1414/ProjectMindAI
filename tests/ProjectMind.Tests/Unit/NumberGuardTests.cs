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

    [Theory]
    [InlineData("SPI 1,2 iken 120 saat kaldı.", "120")]           // spi 1.2 ×100 → yüzde bağlamı yok
    [InlineData("CPI 0,87; ek maliyet 87 bin TL.", "87")]           // cpi 0.87 ×100 → yüzde bağlamı yok
    public void Ratio_scaling_is_not_applied_without_percent_sign(string answer, string expected) =>
        Assert.Equal([expected], NumberGuard.FindUnverified(answer, """{"spi":1.2,"cpi":0.87}"""));

    [Theory]
    [InlineData("Tamamlanma %120 değil, SPI yüzde 120 düzeyinde.")]
    [InlineData("Maliyet verimliliği 87% civarında.")]
    public void Ratio_scaling_is_applied_with_percent_sign(string answer) =>
        Assert.Empty(NumberGuard.FindUnverified(answer, """{"spi":1.2,"cpi":0.87}"""));

    [Theory]
    [InlineData("Bitiş 5.12.2026 görünüyor.")]
    [InlineData("Bitiş 05.12.2026 görünüyor.")]
    [InlineData("Bitiş 5 Aralık 2026'da bekleniyor.")]
    [InlineData("Bitiş 2026-12-05 görünüyor.")]
    [InlineData("Bitiş 5 aralık civarı, yani 2026 sonu.")]
    [InlineData("Toplantı 14:30'da; bitiş 5 Aralık 2026.")]
    public void Date_formats_are_accepted_when_date_is_in_evidence(string answer) =>
        Assert.Empty(NumberGuard.FindUnverified(answer, """{"forecastFinish":"2026-12-05"}"""));

    [Theory]
    [InlineData("Bitiş 5.12.2026 görünüyor.", "5.12.2026")]
    [InlineData("Bitiş 05.12.2026 görünüyor.", "05.12.2026")]
    [InlineData("Bitiş 5 Aralık 2026'da bekleniyor.", "5 Aralık 2026")]
    [InlineData("Bitiş 2026-12-05 görünüyor.", "2026-12-05")]
    [InlineData("Bitiş 15 Aralık civarı.", "15 Aralık")]
    [InlineData("Bitiş 31.02.2027 görünüyor.", "31.02.2027")]           // geçersiz tarih
    public void Date_formats_are_reported_when_date_is_not_in_evidence(string answer, string expected) =>
        Assert.Equal([expected], NumberGuard.FindUnverified(answer, """{"forecastFinish":"2026-12-12"}"""));

    [Fact]
    public void Time_of_day_is_not_treated_as_numbers() =>
        Assert.Empty(NumberGuard.FindUnverified("Rapor 14:30'da hazır olur.", "{}"));

    [Fact]
    public void Unparseable_number_like_token_is_reported() =>
        Assert.Equal(["12,5,75"], NumberGuard.FindUnverified("Değerler 12,5,75 şeklinde.", """{"a":12.5,"b":75}"""));
}
