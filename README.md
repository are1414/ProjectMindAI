# ProjectMind AI

Yapay zekâ destekli yazılım proje planlama ve karar destek sistemi (Mühendislik Yönetimi Yüksek Lisans bitirme projesi).

Proje yöneticisi projeyi tanımlar, işleri ve kişileri girer; sistem eksik işleri önerir, planı otomatik çıkarır,
gerçekleşmeyi takip eder, gecikmeyi tahmin eder, what-if senaryolarını simüle eder ve AI ile yorumlar.

- Plan ve fazlar: [`docs/PLAN.md`](docs/PLAN.md)
- Kesinleşmiş kararlar: [`docs/DECISIONS.md`](docs/DECISIONS.md)
- İlerleme günlüğü: [`docs/PROGRESS.md`](docs/PROGRESS.md)
- AI ajanları için kurallar: [`CLAUDE.md`](CLAUDE.md)

## Çalıştırma (lokal)

Gereksinimler: .NET 10 SDK, lokal SQL Server (Windows Authentication), Claude API anahtarı (opsiyonel).

Bağlantı: `src/ProjectMind.Web/appsettings.json` → `ConnectionStrings:Default`
(varsayılan: `Data Source=.` = bu bilgisayardaki varsayılan SQL Server instance'ı, veritabanı `ProjectMindAIDb`).
Named instance kullanıyorsan `Data Source=.\SQLEXPRESS` gibi değiştir. SQL Server kurulu değilse alternatif: `docker compose up -d`
ve docker-compose içindeki `sa` bağlantısı.

```bash
dotnet tool restore                        # dotnet-ef
dotnet ef database update -p src/ProjectMind.Infrastructure -s src/ProjectMind.Web
dotnet run --project src/ProjectMind.Web
```

### AI bağlantısı (Gemini)

**En kolay yol:** uygulamayı açın → sol menü **⚙ Ayarlar** → anahtarı yapıştırın → Kaydet. Yeniden başlatmak gerekmez.

Elle yapmak isterseniz:

1. https://aistudio.google.com/apikey adresinden Gemini API anahtarı alın.
2. `src/ProjectMind.Web/appsettings.Local.example.json` dosyasını aynı klasöre `appsettings.Local.json` adıyla kopyalayın
   ve anahtarı yazın. Bu dosya `.gitignore`'dadır, GitHub'a gitmez. (**Anahtarı `appsettings.json`'a yazmayın.**)

```json
{ "AI": { "Provider": "Gemini", "Gemini": { "ApiKey": "..." } } }
```

Anahtar yoksa uygulama Mock modda çalışır (AI cevap vermez). Model ve diğer ayarlar `appsettings.json` → `AI` bölümünde
(`AI:Gemini:Model`). Claude'a geçmek için `"Provider": "Claude"` ve `"Claude": { "ApiKey": "..." }`.

```bash
dotnet test                                # testler
```

## Demo projesi

Ana sayfada **Demo projesini oluştur** butonu, "Mobile Banking Modernization" projesini ve ona bağlı "Demo · Mobile Banking
Modernization" sohbetini oluşturup açar. Proje bugünden yaklaşık 9 hafta önce başlamış gibi üretilir: 6 kişilik ekip, WBS'li
35 iş ve bağımlılıklar, ilk gün alınmış baseline, her hafta girilmiş ilerleme ve günlük snapshot'lar (S-eğrisi, EVM, ML tahmini).
Hikâye: hafif gecikme (SPI(t) ≈ 0,88), bir bloke iş, baseline sonrası eklenen kapsam (≈ %10), bir iptal edilen iş ve bilerek
eksik bırakılan CI/CD, kullanıcı kabul testi ve dokümantasyon işleri ("Eksik iş var mı?" ile bulunur).

- Veriler gerçek uygulama servisleriyle (geriye alınmış saatle) üretilir; LLM çağrılmaz, Mock modda da çalışır.
- Tarihler bugüne göredir; aynı gün aynı sonuç (sabit tohum; isteğe bağlı ayar bölümü `Demo`: `WeeksBack`, `Seed`, `Velocity`).
- Demo zaten varsa ikinci kopya açılmaz; yeniden oluşturmak için sol menüden sohbeti **projeyle birlikte** silin.
- İlk kullanımda ML modeli henüz eğitilmemişse eğitim birkaç saniye sürebilir.
