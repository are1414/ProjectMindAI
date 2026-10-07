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

### AI (Claude) bağlantısı

Anahtarı repoya yazmadan, user-secrets ile girin (console.anthropic.com → API Keys):

```bash
dotnet user-secrets set "AI:ApiKey" "<anahtar>" --project src/ProjectMind.Web
```

Anahtar yoksa uygulama Mock modda çalışır (ücretsiz, AI cevap vermez). Model ve ayarlar:
`src/ProjectMind.Web/appsettings.json` → `AI` bölümü (`Model`, `Effort`, `MaxTokens`…).

```bash
dotnet test                                # testler
```
