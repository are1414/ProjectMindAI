# Kesinleşmiş Kararlar

Bu kararlar ancak proje sahibinin açık onayıyla değişir. Değişirse eski kararın üstü çizilir, yenisi tarihle eklenir.

| # | Karar | Gerekçe |
|---|---|---|
| D1 | Ürün adı **ProjectMind AI** | — |
| D2 | **Tek kullanıcı** (proje yöneticisi). Login / rol / JWT yok. | Akademik değer katmıyor, süre yiyor. |
| D3 | ~~ASP.NET Core Web API~~ → ~~ASP.NET Core MVC~~ → **(2026-10-07) Tek .NET 10 web uygulaması: Blazor Server (interactive server, ön-render kapalı).** Ayrı API katmanı yazılmaz. | Chat odaklı canlı arayüz; her şey C#. |
| D4 | Veritabanı: **Microsoft SQL Server**, EF Core migrations. Lokal ortam: **(2026-10-07) bilgisayardaki SQL Server, Windows Authentication, DB `ProjectMindAIDb`**; docker-compose sadece yedek seçenek. | Proje sahibi tercihi. |
| D5 | Katmanlar: Domain / Application / Infrastructure / **Web** + Tests. Mikroservis yok. | Sade Clean Architecture yeterli. |
| D6 | ~~React + TypeScript~~ → **(2026-10-07) Arayüz Blazor bileşenleri + kendi CSS'imiz (`wwwroot/app.css`).** Şimdilik sade tasarım; profesyonel tasarım ayrı adımda. Grafik/Gantt gerekince küçük JS kütüphanesi (`wwwroot/lib`). npm/SPA yok. | Tek proje, tek dil. |
| D7 | ~~Python FastAPI servisi~~ → **(2026-10-07) ML uygulama içinde ML.NET ile.** Python sadece opsiyonel olarak çevrimdışı analiz/grafik için (uygulamanın parçası değil). | Her şey tek .NET projesinde kalsın. |
| D8 | LLM: `IChatModel` soyutlaması. ~~Ana sağlayıcı Claude~~ → **(2026-10-07) Şimdilik ana sağlayıcı Gemini** (REST generateContent, model `gemini-3.5-flash`; ayarlı model kapatılmışsa en yeni uygun flash modele otomatik geçer); Claude (Anthropic C# SDK) alternatif olarak hazır. Seçim `AI:Provider`. Anahtar git'e girmeyen `appsettings.Local.json`'dan okunur; yoksa otomatik **Mock**. | Proje sahibi tercihi (ücretsiz katman); sağlayıcı karşılaştırması imkânı. |
| D9 | LLM sadece **açıklar/önerir**; tüm sayısal hesaplar deterministik kod veya ML servisinde. | Halüsinasyonu önler, savunulabilir. |
| D10 | Baseline yöntem: **EVM / Earned Schedule**. ML modeli buna karşı kıyaslanır. | Mühendislik yönetimi bağlantısı. |
| D11 | Sağlık skoru ağırlıkları **AHP** ile belirlenir. | Ağırlıklar keyfi olmasın. |
| D12 | What-if senaryoları **Monte Carlo** + Brooks etkisi (yeni kişide ısınma süresi) | Gerçekçi senaryo sonuçları. |
| D13 | Bağımlılık türü sadece **Finish-to-Start**. | MVP için yeterli. |
| D14 | Kişiler projeye bağlıdır (proje bazlı ekip). | Basitlik. |
| D15 | Eksik iş önerisi **hibrit**: kural/şablon tabanlı + LLM. AI önerisi yönetici onayı olmadan eklenmez. | Açıklanabilirlik + kontrol. |
| D17 | **(2026-10-07) Chat odaklı kullanım:** proje, iş, kişi, bağımlılık sohbetten yönetilir. AI araç çağrısı = öneri kartı (AiAction); proje yöneticisi Uygula/Vazgeç der. Sohbet kayıtları saklanır. | Proje sahibi isteği; kabul oranı akademik ölçüm. |
| D16 | Kod İngilizce, dokümantasyon Türkçe. | — |
| D18 | **(2026-10-07) Sayı doğrulayıcı (NumberGuard)** cevabı reddetmez; bağlamda/araç sonuçlarında olmayan sayıları cevabın altında "Doğrulanamayan sayılar" olarak işaretler. | Tam ret, zararsız sayılarda (adet, sıra) sohbeti kullanılamaz yapıyordu; görünür uyarı şeffaflığı korur. |
| D19 | **(2026-10-07) EVM efor (saat) bazlıdır**; para birimi değerleri saat × saatlik maliyetten türetilir. Baseline'da işin **tam** eforu saklanır (plan kalan eforla yapılır). Baseline'dan sonra eklenen işler EV/AC'ye girmez, kapsam büyümesi olarak ölçülür. | Yazılım projelerinde efor birincil kaynak; EV = baseline eforu × %tamamlanma tutarlı kalır. |
| D20 | **(2026-10-07) Sağlık skoru bileşenleri:** Takvim = SPI(t), Maliyet = CPI (1'de tavan), Kapsam = 100 − 2×büyüme%, Kaynak = kişisi olmayan kalan efor payı, Risk = bloke/gecikmiş kalan efor payı. Ağırlıklar AHP (CR < 0,10). Kritik uyarı varken seviye en fazla "Dikkat". | Açıklanabilir, ayarlanabilir (`Health` bölümü). |
| D21 | **(2026-10-07) Gecikme modeli:** sentetik veri (synthetic-v1, haftalık simülasyon) ile ML.NET; gecikme = gerçek süre > plan × 1,10. Özellikler sadece gerçek projede ölçülebilen oranlar (geçen süre, tamamlanma, SPI, CPI, SPI(t), son 2 hafta hızı, kapsam büyümesi, bloke payı, ekip). Eğitim/test **proje bazında** ayrılır; en yüksek test AUC'li sınıflandırıcı kullanılır; süre için FastTree regresyonu. Model `App_Data/models`'a kaydedilir (git'e girmez), yoksa ilk ihtiyaçta eğitilir. Karşılaştırma kuralı: EVM tahmini süre = plan / SPI(t). | Gerçek veri yok; gizli değişkenler + gürültü + raporlama iyimserliği formül ezberini önler; RQ1/RQ2 aynı test setinde ölçülür. |

## YAPILMAYACAKLAR (eklenmesi yasak)

Login/Identity/JWT · çoklu kullanıcı/rol · ayrı Web API / REST katmanı · React/SPA/npm · Python servis · mikroservis · Kubernetes · Redis · Kafka · event sourcing · CQRS/MediatR framework'ü · Hangfire · e-posta/bildirim sistemi · Jira/Azure DevOps entegrasyonu · deep learning.
