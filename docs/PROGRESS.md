# İlerleme Günlüğü

Her oturum sonunda en üste yeni kayıt eklenir. Format: yapılanlar · varsayımlar · bilinen sorunlar · sıradaki adım.

---

## 2026-10-07 (3) — Lokal SQL Server bağlantısı

- Development bağlantısı proje sahibinin lokal SQL Server'ına çevrildi: `Data Source=.;Initial Catalog=ProjectMindAIDb;Integrated Security=True;Encrypt=False`
  (`.` = makinedeki varsayılan instance; makine adı repoya yazılmadı). Docker opsiyonel kaldı (D4 güncellendi).
- Varsayım: SQL Server varsayılan instance (named instance değil) — verilen bağlantıda instance adı yoktu.
- Sıradaki adım değişmedi: Faz 3.

---

## 2026-10-07 (2) — Faz 2: tek MVC web uygulamasına geçiş

**Karar değişikliği (proje sahibi isteği)**: Ayrı Web API + React yerine tek ASP.NET Core MVC uygulaması;
ML de Python yerine ML.NET ile uygulama içinde (D3, D5, D6, D7 güncellendi).

**Yapılanlar**
- `ProjectMind.Api` kaldırıldı, `ProjectMind.Web` (MVC + Razor + Bootstrap 5) eklendi. Application servisleri aynen kullanılıyor.
- Sayfalar: `/projects` (liste), `/projects/new`, `/projects/{id}` (detay: işler + bağımlılıklar + kişiler),
  `/projects/{id}/edit`, `/projects/{id}/people/new|{id}/edit`, `/projects/{id}/work-items/new|{id}/edit`, silme formları
- İstek DTO'ları form bağlamaya uygun sınıflara çevrildi, Türkçe `Display` etiketleri; enum'lara Türkçe görünen adlar
- Kişi becerileri çoklu checkbox → flags enum'a birleştiriliyor
- NotFoundException → 404 sayfası; iş kuralı hataları formda gösteriliyor
- Testler: 20/20 (birim 10, servis 6, web form 4 — gerçek form gönderimi + antiforgery)
- Bootstrap/jQuery şablonundan sadece kullanılan min dosyalar tutuldu (9.5 MB → 0.5 MB)

**Varsayımlar**
- Form sayıları kültürden bağımsız (nokta ondalık) bağlanır; gösterim Türkçe biçimle (`Display` yardımcısı).
- Migration değişmedi (`has-pending-model-changes`: değişiklik yok).

**Bilinen sorunlar / sınırlar**
- Uygulama bu ortamda SQL Server olmadığı için tarayıcıda çalıştırılmadı; sayfalar testlerde SQLite ile render ediliyor.
  Lokal makinede `docker compose up -d` + `dotnet ef database update` + `dotnet run` ile doğrulanmalı.

**Sıradaki adım**: Faz 3 — eksik iş önerisi (TaskTemplate + kural tabanlı eşleştirme + Kabul/Reddet).

---

## 2026-10-07 — Faz 0 + Faz 1 tamamlandı

**Yapılanlar**
- Kural dosyaları: `CLAUDE.md`, `docs/PLAN.md`, `docs/DECISIONS.md`, `docs/PROGRESS.md`
- .NET 10 solution: Domain / Application / Infrastructure / Api / Tests
- Entity'ler: Project, Person, WorkItem, WorkItemDependency (+ enum'lar: ProjectType, ProjectStatus, Skill[Flags], WorkPhase, Priority, WorkItemStatus)
- EF Core 10 + SQL Server, `InitialCreate` migration, `docker-compose.yml` (SQL Server 2022)
- CRUD API:
  - `GET/POST /api/projects`, `GET/PUT/DELETE /api/projects/{id}`
  - `GET/POST /api/projects/{projectId}/people`, `GET/PUT/DELETE .../people/{id}`
  - `GET/POST /api/projects/{projectId}/work-items`, `GET/PUT/DELETE .../work-items/{id}`
  - `GET/POST /api/projects/{projectId}/dependencies`, `DELETE .../dependencies/{id}`
- İş kuralları: bitiş ≥ başlangıç; kişi en az 1 beceri; iş tek beceri; atanan kişi aynı projeden; Done ⇒ %100;
  bağımlılık: kendine yok, tekrar yok, döngü yok (DFS)
- Global hata yönetimi (ProblemDetails: 404 / 400 / 500), OpenAPI + Scalar UI (`/scalar`, sadece Development)
- Testler: 15/15 geçti (birim: döngü tespiti, beceri kuralları; entegrasyon: uçtan uca API akışı, SQLite in-memory)

**Paketler (gerekçe)**: EF Core SqlServer (D4), EF Design (migration), Scalar.AspNetCore (OpenAPI arayüzü — .NET 10 şablonunda Swagger UI yok),
Mvc.Testing + EF Sqlite (sadece testlerde, gerçek SQL Server olmadan API testi).

**Varsayımlar**
- Kişi birden fazla beceriye sahip olabilir (flags enum); bir iş tek beceri gerektirir.
- Kişi silinince işleri silinmez, ataması boşaltılır.
- `WorkItem` üzerindeki durum/% alanları şimdilik doğrudan güncellenir; geçmiş kaydı (StatusUpdate) Faz 5'te gelecek.
- `appsettings.Development.json` içindeki SQL şifresi sadece lokal docker içindir (gerçek sır değildir).

**Bilinen sorunlar / sınırlar**
- Entegrasyon testleri SQLite ile çalışıyor; migration'ın gerçek SQL Server'da uygulanması lokal makinede doğrulanmalı
  (`docker compose up -d` → `dotnet ef database update ...`).

**Sıradaki adım**: Faz 2 — React + TS + Vite iskeleti, proje / iş / kişi ekranları.
