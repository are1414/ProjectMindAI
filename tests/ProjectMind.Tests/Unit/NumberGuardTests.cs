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
    [InlineData("CPI 0,87; ek maliyet 87 bin TL.", "87 bin")]       // cpi 0.87 ×100 → yüzde bağlamı yok; "bin" çarpanı okunur (Tur 4a)
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

    // ---- Tur 4a H3: Türkçe biçimli kanıt ve "bin/milyon" çarpanları ----

    [Theory]
    [InlineData("Bütçe 1.250.000 TL.", "Kullanıcı: Bütçe 1.250.000 TL olsun")]                 // kullanıcı mesajı (Türkçe binlik)
    [InlineData("Bütçe 1.250.000,00 ₺.", "Proje oluştur: Mobil · 01.11.2026 → 30.04.2027 · 1.250.000 TRY")]   // kart özeti
    [InlineData("Toplam 1.500 saat.", """{"totalHours":1500}""")]                              // JSON kanıt, Türkçe cevap
    [InlineData("SPI 0,87 seviyesinde.", """{"spi":0.87}""")]                                  // JSON ondalık ↔ Türkçe ondalık
    [InlineData("Bütçe 250.000 TL.", "Bütçe 250.000 TL olsun")]                                // tek gruplu binlik
    [InlineData("Kalan 37,5 saat.", "Senaryo notu: 37,5 saat kişi kapasitesi")]               // kanıtta Türkçe ondalık
    [InlineData("PV eğrisi 16 ve 24 saatte.", """{"curve":[8,16,24]}""")]                      // JSON listesi ayrı sayılar
    public void Turkish_formatted_evidence_is_read_correctly(string answer, string evidence) =>
        Assert.Empty(NumberGuard.FindUnverified(answer, evidence));

    [Fact]
    public void Turkish_decimal_in_evidence_is_not_read_as_thousands_or_integer()
    {
        // Eskiden kanıttaki "0,87" JSON gibi okunup 87 oluyordu ve "87 saat" doğrulanmış görünüyordu (TEST_PAZAR Q5b).
        Assert.Equal(["87"], NumberGuard.FindUnverified("Kalan 87 saat.", "Uyarı: SPI 0,87"));
        Assert.Equal(["137"], NumberGuard.FindUnverified("Bütçe 1.250.000 TL, 137 saat.", "Bütçe 1.250.000 TL olsun"));
    }

    [Theory]
    [InlineData("Bütçe 1,2 milyon TL.", """{"budget":1200000}""")]
    [InlineData("Bütçe yaklaşık 1,25 milyon TL.", """{"budget":1250000}""")]
    [InlineData("Bütçe yaklaşık 1,3 milyon TL.", """{"budget":1250000}""")]                   // 1.250.000 / 10⁶ → 1,3 (yuvarlama)
    [InlineData("Ek maliyet 120 bin TL.", """{"extraCost":120000}""")]
    [InlineData("Ek maliyet 120.000 TL.", "Kullanıcı: ek maliyet 120 bin TL olabilir")]      // kanıtta çarpan
    [InlineData("Toplam 2 milyar TL.", """{"total":2000000000}""")]                          // küçük taban sayı muaf değil, eşleşir
    public void Multiplier_words_are_compared_with_multiplied_value(string answer, string evidence) =>
        Assert.Empty(NumberGuard.FindUnverified(answer, evidence));

    [Theory]
    [InlineData("Bütçe 2 milyon TL.", """{"budget":1200000}""", "2 milyon")]               // ≤ 10 muafiyeti çarpanlıda yok
    [InlineData("Ek maliyet 150 bin TL.", """{"extraCost":120000}""", "150 bin")]
    [InlineData("Ek maliyet 120 bin TL.", """{"hours":120}""", "120 bin")]                  // taban sayı kanıtta olsa da 120.000 yok
    public void Multiplier_words_with_wrong_value_are_reported(string answer, string evidence, string expected) =>
        Assert.Equal([expected], NumberGuard.FindUnverified(answer, evidence));
}
