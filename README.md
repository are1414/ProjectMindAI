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

## Değerlendirme (Faz 9)

Sol menüdeki **📊 Değerlendirme** sayfası (`/evaluation`) araştırma sorularının verisini özetler ve her birini CSV olarak indirir
(virgül ayırıcı, nokta ondalık, ISO tarih, Excel için UTF-8 BOM):

- **RQ1** snapshot zaman serisi: SPI, SPI(t), EVM (Earned Schedule) tahmini bitiş, ML gecikme olasılığı ve ML tahmini bitiş.
- **RQ2** son deneyin özellik önemi (deney Deneyler sayfasında çalıştırılır).
- **RQ3** what-if çalıştırmaları: panelden ve AI aracından (`simulate_what_if`) her karşılaştırma kaydedilir (senaryo
  değişiklikleri, P50/P80, hedefe yetişme olasılığı, süre).
- **RQ4** öneri kartları (kaynak × araç × durum × toplu karar) ve AI denetim kaydı (şema geçerliliği, doğrulanamayan sayılar,
  süre, yorum kartındaki "Bu açıklama anlaşılır mıydı?" Evet/Hayır + isteğe bağlı 1–5 puan). Yorum metni ve bağlam CSV'ye girmez.
- **Anket** (`/evaluation/survey`): onam metni, anonim katılımcı kodu (P01…; ad/e-posta yok), 10 SUS maddesi ve RQ3/RQ4 için
  ikişer Likert maddesi. SUS puanı uygulamada hesaplanır; sayfada n, ortalama, standart sapma, min/maks ve Bangor sıfatı görünür.
  Deneme ya da çekilen katılımcının cevabı sayfadan silinebilir.

### Ölçüm koşucuları (Tur 5b)

- **LLM sağlayıcı karşılaştırması** (`/evaluation` → "LLM sağlayıcı karşılaştırması"): sabit görev seti `eval-v1` (proje yorumu,
  iki senaryolu senaryo yorumu, hibrit eksik iş ve salt okunur araç gerektiren 5 sohbet sorusu) demo projesinde, **API anahtarı
  olan her sağlayıcıyla** (Gemini ve/veya Claude; Mock atlanır) `Evaluation:Repeats` (varsayılan 3) kez çalışır. Gerçek API
  çağrısı yapar, ücretli olabilir; "İptal et" ile durdurulabilir. Hiçbir kart uygulanmaz: sohbette oluşan kartlar otomatik
  reddedilir ve öneri kabul oranına girmez. Sonuç: sağlayıcı × görev için şema geçerlilik, doğrulanamayan sayı, hata oranı,
  süre ve (sağlayıcı bildiriyorsa) token ortalaması + CSV. Ham çağrılar "AI kayıtları CSV"sinde `evaluation_run_id` ile.
  Her iki anahtarı da ⚙ Ayarlar'a girin (seçili sağlayıcı olması gerekmez).
- **Tohumlanmış eksik iş deneyi (RQ4)** (aynı sayfa, RQ4 bölümü): demo iş listesinden bellekte 4 şablon işi + 4 projeye özgü iş
  çıkarılır (`Evaluation:RecallSeed`), kural-yalnız ve hibrit modun recall/precision'ı ölçülür. Kural kısmı anahtarsız çalışır;
  hibrit kısım ayarlı sağlayıcıyla `Evaluation:RecallRepeats` kez çalışır.
- **RQ1 erken uyarı ve çok tohumlu analiz** (`/experiments`): deney artık her yöntem için gecikmeyi ilk işaretlediği kontrol
  noktasını, uyarı süresini (hafta) ve yanlış alarm payını verir. "Çok tohumlu deneyi çalıştır" deneyi `Ml:RobustnessSeeds`
  (varsayılan 5) tohumla tekrarlar ve F1/AUC ortalama ± SS üretir (üretim modeli değişmez); CSV indirilebilir.
