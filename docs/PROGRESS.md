# İlerleme Günlüğü

Her oturum sonunda en üste yeni kayıt eklenir. Format: yapılanlar · varsayımlar · bilinen sorunlar · sıradaki adım.

---

## 2026-10-07 (11) — WBS numaraları, aç/kapat, sayfada düzenleme

Proje sahibi isteği: id yerine 4.1 gibi numaralar, +/− ile daraltma, sayfa az yer kaplasın, saat ve metinler sayfadan düzenlensin.
- `WorkItemTree` her satıra WBS kodu üretir (1, 1.2, 1.2.1; aynı seviyede oluşturulma sırası). Test eklendi.
- AI bağlamında her işin `wbs` alanı; kullanıcı "4.1'i güncelle" diyebilir (prompt chat-v3).
- İş tablosu: kompakt; Faz sütunu kaldırıldı (satır ipucunda); üst işlerde +/−; varsayılan kapalı; "Tümünü aç/kapat".
- Satır içi düzenleme: iş adı (tüm işler), efor saati ve % (sadece yaprak işler; üst işler alt işlerden hesaplanır).
  Enter kaydeder, Esc vazgeçer, odak çıkınca da kaydeder; mevcut WorkItemService iş kurallarından geçer, hata satır üstünde gösterilir.
- Varsayım: Sayfadan düzenleme proje yöneticisinin doğrudan veri girişidir, AI önerisi olmadığından kart/onay gerekmez.
- Testler 51/51; Playwright: kapalı→açık görünüm, 1.2.1 eforu 40→60 → üst toplamlar 76/100, toplam 132 saat.

---

## 2026-10-07 (10) — Alt işler (WBS hiyerarşisi)

Proje sahibi isteği: işlere alt iş eklenebilsin.
- `WorkItem.ParentId` (kendine referans, NO ACTION) + migration `AddWorkItemParent`.
- Kurallar: üst iş aynı projede olmalı; bir iş kendisinin/alt işinin altına taşınamaz (döngü yok); üst iş silinince alt ağaç ve
  bağımlılıkları silinir; proje silinirken üst-alt bağları önce koparılır.
- `WorkItemTree` (test edilen hesap): ağaç sırası ve derinlik; üst iş eforu = yaprak işlerin toplamı, ilerleme = efora göre
  ağırlıklı ortalama; proje toplam eforu sadece yapraklardan (çift sayım yok).
- AI: `add_work_item` / `update_work_item` için `parentName`; toplu uygulamada üst işler önce; bağlamda her işin `parent`'ı;
  prompt sürümü chat-v2.
- Arayüz: işler tablosu ağaç görünümünde (girinti, üst işler kalın, toplanan efor/ilerleme).
- Varsayım: üst işin kendi efor/ilerleme alanı gösterimde ve toplamlarda kullanılmaz (alt işlerden hesaplanır).
  İleride otomatik planlama (Faz 4) sadece yaprak işleri çizelgeleyecek.
- Testler 51/51; ağaç görünümü Playwright ile denendi (Backend 80 = 24 + 56, toplam 112 saat).

---

## 2026-10-07 (9) — Ayarlar sayfası (API anahtarı arayüzden)

Teşhis: proje sahibinin makinesinde `appsettings.Local.json` hiç yoktu. Dosyayı elle oluşturmak yerine:
- `/settings` sayfası: sağlayıcı, API anahtarı (şifre alanı), model; "Kaydet" → proje klasöründe appsettings.Local.json yazılır
  (diğer ayarlar korunur). Dosya açılışta yoksa da izlenir; değişiklik **yeniden başlatmadan** geçerli olur.
- AI ayarları her kapsamda güncel değerden okunur (`IOptionsMonitor` → scoped `AiOptions`); chat başlığı da canlı güncellenir.
- Mock cevabı ve geçersiz anahtar mesajı Ayarlar sayfasına yönlendirir; sayfada teşhis metni görünür.
- Doğrulama: Playwright ile ayar kaydı → etiket "Mock" → "Gemini · gemini-3.5-flash" (yeniden başlatmadan). Sahte anahtarla
  gerçek Gemini API'ye istek gitti, 400 API_KEY_INVALID → Türkçe "anahtar geçersiz" mesajı (istek biçimi API tarafından kabul edildi).
- Testler 43/43.

---

## 2026-10-07 (8) — Anahtar teşhisi, hızlı yükleme, sohbet silme

Proje sahibi: "Mock mod yazıyor", "geçmiş geç geliyor", "sohbeti silebilmeliyim".
- `LocalSettings`: appsettings.Local.json proje klasörü, repo kökü ve bin klasöründe aranır; bulunamazsa beklenen tam yol,
  ".txt" uzantısı kalmışsa uyarı, JSON hatası varsa hata mesajı. Mock cevabında ve açılış logunda teşhis + maskeli anahtar durumu.
- Hız: açılışta EF ısınma sorgusu; mesajlar ve öneriler paralel yüklenir; sohbet paneli projeyi beklemeden açılır;
  yüklenirken "yükleniyor" göstergesi.
- Sohbet silme: taslak sohbette "Sohbeti sil"; projeye bağlı sohbette "Geçmişi temizle" (proje kalır) ve "Projeyi sil"
  (proje + sohbet). Onay penceresi var.
- Varsayım: projeye bağlı sohbet, proje silinmeden tamamen silinemez (proje listede görünmez kalırdı).
- Not: Silinen sohbetin AI öneri kayıtları da silinir (kabul oranı ölçümünden düşer).
- Testler 41/41; arayüz Playwright ile denendi (Mock teşhis mesajı, sohbet silme → ana sayfa, kenar çubuğu güncellendi).

---

## 2026-10-07 (7) — Gemini "çalışmıyor" düzeltmeleri

Proje sahibi anahtarı ekledi ama chat çalışmadı (hata mesajı/commit repoya ulaşmadı; olası nedenler kodda sağlamlaştırıldı).
- `gemini-2.5-flash` Google tarafından kapatılıyor (16.10.2026; birçok kullanıcıda temmuzdan beri 404). Varsayılan `gemini-3.5-flash`.
- Model 404 dönerse ListModels ile generateContent destekleyen en yeni "flash" modeli bulunup ona geçilir (lite/önizleme/görüntü/ses hariç).
- Eski anahtar yapısı (`"AI": { "ApiKey": "..." }`) da okunur; anahtar baştaki/sondaki boşluklardan temizlenir.
- Chat başlığında ve açılış logunda etkin sağlayıcı/model görünür ("Mock (API anahtarı okunamadı)" teşhis için).
- Testler 34/34 (yedek modele geçiş testi eklendi).

---

## 2026-10-07 (6) — Geliştirmede otomatik migration

- Lokal çalıştırmada `Invalid object name 'ChatSessions'` hatası: yeni migration veritabanına uygulanmamıştı.
- Development ortamında uygulama açılışta bekleyen migration'ları uyguluyor (`Database.MigrateAsync`). Production'da kapalı.

---

## 2026-10-07 (5) — Gemini sağlayıcısı (varsayılan)

**Karar (proje sahibi isteği)**: Şimdilik AI sağlayıcısı Gemini; anahtar appsettings'ten okunur (D8 güncellendi).

**Yapılanlar**
- `GeminiChatModel`: generateContent REST API, function calling döngüsü; modelin içeriği (thoughtSignature dahil) aynen geri eklenir,
  "thought" parçaları cevaba girmez; hata kodlarına göre Türkçe mesaj (geçersiz anahtar, model yok, limit).
- `AiOptions` sağlayıcı başına: `AI:Provider`, `AI:Gemini:{Model,ApiKey}`, `AI:Claude:{Model,ApiKey}`, `AI:ClaudeEffort`.
- Anahtar için git'e girmeyen `appsettings.Local.json` (örnek: `appsettings.Local.example.json`); ortam değişkeni `GEMINI_API_KEY` de olur.
- Testler: 33/33 (Gemini döngüsü sahte HTTP cevaplarıyla: araç çağrısı → functionResponse, imza korunması, hata mesajı).

**Paketler (gerekçe)**: `Microsoft.Extensions.Http` (typed HttpClient / IHttpClientFactory; Gemini için resmi .NET SDK yerine
birkaç uç noktalık REST çağrısı yeterli, ek SDK bağımlılığı eklenmedi).

**Varsayımlar**
- Varsayılan model `gemini-2.5-flash` (ücretsiz katmanda kullanılabilir olduğu varsayıldı). Google model adını değiştirirse
  `AI:Gemini:Model` ayarından güncellenir; model bulunamazsa uygulama bunu açıkça söyler.

**Bilinen sorunlar / sınırlar**
- Gerçek Gemini API bu ortamda çağrılmadı (anahtar yok); lokal makinede denenmeli.

**Sıradaki adım**: Faz 3 — eksik iş kontrolü.

---

## 2026-10-07 (4) — Chat odaklı arayüz: Blazor Server + Claude (Faz 2.5)

**Karar değişikliği (proje sahibi isteği)**: Her şey sohbetten yönetilsin; arayüz Blazor Server, AI sağlayıcısı Claude
(D3, D6, D8 güncellendi, D17 eklendi). Tasarım şimdilik sade; profesyonel tasarım sonraki ayrı adım.

**Yapılanlar**
- MVC projesi kaldırıldı, `ProjectMind.Web` Blazor Server olarak yeniden kuruldu (ön-render kapalı).
- Ekranlar: `/` karşılama · `/chat/new` · `/chat/{id}` (sol: proje paneli, sağ: AI chat) · `/projects/{id}` (projenin sohbetini açar).
  Kenar çubuğunda tüm sohbetler (proje adıyla).
- Yeni tablolar (migration `AddChatAndAiActions`): `ChatSessions`, `ChatMessages`, `AiActions`.
- Application: `AiTools` (8 araç + JSON şema), `AiActionService` (öner → uygula/reddet, toplu uygulamada mantıklı sıra),
  `ProjectContextBuilder`, `ChatPrompts` (chat-v1), `ChatService` (sohbet turu + kayıt), `ProjectOverviewService`, `Format`.
- Infrastructure: `ClaudeChatModel` (Anthropic C# SDK, araç döngüsü, thinking bloklarının korunması, refusal ve hata türlerine
  göre Türkçe mesaj), `NotConfiguredChatModel` (anahtar yoksa).
- Testler: 30/30 (birim 13, servis 13, sohbet 3, web duman 1). Arayüz geçici SQLite kopyasında Playwright ile denendi:
  sohbet → 5 öneri kartı → hepsini uygula → proje paneli ve kenar çubuğu güncellendi, konsol hatası yok.

**Paketler (gerekçe)**: `Anthropic` 12.53.0 (resmi C# SDK; Claude'u elle HTTP ile çağırmak yerine). MVC'nin Bootstrap/jQuery dosyaları kaldırıldı.

**Varsayımlar**
- AI, kullanıcı efor vermezse tahmini saat önerebilir (öneri kartında görünür, kullanıcı onaylar); prompt bunu "tahmin" diye belirtmesini ister.
  Süre/maliyet/risk hesaplamaz.
- "Cevaptaki her sayı bağlamda olmalı" doğrulayıcısı analiz cevapları (Faz 8) için eklenecek; bu fazda AI sadece veri girişi öneriyor
  ve kart metinleri modelden değil veriden üretiliyor.
- Kişi/iş eşleştirmesi ada göre (SQL Server'da büyük/küçük harf duyarsız).
- Önceki sohbet turlarından sadece metin geçmişi gönderilir (son 20 mesaj); araç blokları tur içinde tutulur.
- Sunucu taraflı refusal fallback etkin değil (SDK'da "default" biçimi doğrulanamadı); refusal durumunda kullanıcıya mesaj gösterilir.

**Bilinen sorunlar / sınırlar**
- Gerçek Claude API bu ortamda çağrılmadı (anahtar yok). Lokal makinede anahtar girilip denenmeli.
- Manuel düzenleme formları kaldırıldı; düzeltmeler sohbetten yapılıyor.

**Sıradaki adım**: Faz 3 — eksik iş kontrolü (`check_missing_work` aracı, kural tabanlı şablon).

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
