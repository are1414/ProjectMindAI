# ProjectMind AI

Yapay zekâ destekli yazılım proje planlama ve karar destek sistemi (Mühendislik Yönetimi Yüksek Lisans bitirme projesi).

Proje yöneticisi projeyi tanımlar, işleri ve kişileri girer; sistem eksik işleri önerir, planı otomatik çıkarır,
gerçekleşmeyi takip eder, gecikmeyi tahmin eder, what-if senaryolarını simüle eder ve AI ile yorumlar.

- Plan ve fazlar: [`docs/PLAN.md`](docs/PLAN.md)
- Kesinleşmiş kararlar: [`docs/DECISIONS.md`](docs/DECISIONS.md)
- İlerleme günlüğü: [`docs/PROGRESS.md`](docs/PROGRESS.md)
- AI ajanları için kurallar: [`CLAUDE.md`](CLAUDE.md)

## Çalıştırma (lokal)

Gereksinimler: .NET 10 SDK, Docker.

```bash
docker compose up -d                       # SQL Server
dotnet tool restore                        # dotnet-ef
dotnet ef database update -p src/ProjectMind.Infrastructure -s src/ProjectMind.Api
dotnet run --project src/ProjectMind.Api   # API + /scalar (OpenAPI arayüzü)
dotnet test                                # testler
```
