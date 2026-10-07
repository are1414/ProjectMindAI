# ProjectMind AI — Proje Planı

**Akademik başlık:** Yapay Zekâ Destekli Yazılım Proje Planlama ve Karar Destek Sistemi: Zamanlama, Kaynak ve Kapsam Yönetimi Üzerine Bir Uygulama

**Aktif faz: FAZ 3 — Eksik iş önerisi (kural tabanlı katman)**

## Ürün akışı (tek kullanıcı: proje yöneticisi)

```
1 Proje tanımı → 2 İş listesi → 3 Eksik iş önerisi (kabul/red) → 4 Kişiler
→ 5 Otomatik plan (CPM + kaynak atama) → baseline → 6 Durum girişi (snapshot)
→ 7 Analiz (EVM, sağlık skoru, risk) → 8 Gecikme tahmini (ML)
→ 9 What-if (Monte Carlo) → 10 AI yorum / chat
```

## Araştırma soruları

- **RQ1** ML tabanlı tahmin, gecikmeyi EVM/Earned Schedule'dan daha erken ve doğru öngörebilir mi?
- **RQ2** Hangi faktörler (doluluk, kapsam artışı, bloke işler…) gecikmeyi en çok açıklıyor?
- **RQ3** What-if (Monte Carlo) analizi yöneticinin karar vermesini destekliyor mu?
- **RQ4** Hibrit (kural + LLM) eksik iş tespiti plan bütünlüğünü artırıyor mu? LLM açıklamaları anlaşılır mı?

## Fazlar

### FAZ 0 — Kurallar ve plan ✅
- [x] CLAUDE.md (AI ajan kuralları)
- [x] DECISIONS.md, PLAN.md, PROGRESS.md

### FAZ 1 — Temel katmanlar ve veri ✅
- [x] Solution: Domain / Application / Infrastructure / Web / Tests (.NET 10)
- [x] SQL Server + EF Core, ilk migration, docker-compose (SQL Server)
- [x] Entity'ler: Project, Person, WorkItem, WorkItemDependency
- [x] Servisler: projeler, kişiler, işler, bağımlılıklar (CRUD + iş kuralları)
- [x] Bağımlılık kuralları: kendine bağımlılık yok, tekrar yok, **döngü yok**
- [x] Birim + entegrasyon testleri

### FAZ 2 — Web arayüzü ✅ (MVC formları sonradan Blazor + chat ile değiştirildi)
- [x] Ayrı API kaldırıldı; tek web uygulaması (D3, D6)
- [x] Türkçe arayüz ve biçimlendirme, 404/hata sayfaları

### FAZ 2.5 — Chat odaklı arayüz ve AI çekirdeği ✅
- [x] Blazor Server (interactive server), sade tasarım: kenar çubuğu (sohbetler) · proje paneli · AI chat paneli
- [x] Sohbet kayıtları (ChatSession, ChatMessage) veritabanında; proje sohbetten başlar
- [x] Gemini (varsayılan) ve Claude sağlayıcıları + araç çağırma; anahtar yoksa ücretsiz Mock'a düşer
- [x] Araçlar: proje oluştur/güncelle, kişi ekle/güncelle, iş ekle/güncelle/sil, bağımlılık ekle
- [x] Her araç çağrısı = öneri kartı (AiAction); Uygula / Vazgeç / hepsini uygula; mevcut servislerden geçer
- [x] Kart metni modelden değil veriden üretilir; kabul/red loglanır (öneri kabul oranı ölçümü için)
- [x] Kontrollü proje bağlamı (ProjectContextBuilder), sürümlü sistem prompt'u (chat-v1)
- [x] Testler: öneri/uygulama kuralları, sohbet turu (senaryolu sahte model), araç şemaları, web duman testi

### FAZ 3 — Eksik iş önerisi (kural tabanlı katman, chat'e araç olarak)
- [ ] TaskTemplate (proje tipine göre faz/iş şablonu) + seed
- [ ] Eşleştirme (anahtar kelime/eş anlamlı) → eksik işler
- [ ] `check_missing_work` aracı: kural sonucunu modele verir, model add_work_item kartlarıyla önerir (AiAction altyapısı)
- [ ] "Kabul etmeden önce plana etkisi" bilgisi (Faz 4 sonrası bağlanır)
- [ ] Test: tam plandan iş silip yakalama oranı (precision/recall)

### FAZ 4 — Otomatik planlama
- [ ] CPM: ES/EF/LS/LF, bolluk (slack), kritik yol
- [ ] Kaynak kısıtlı çizelgeleme (öncelik kurallı seri çizelgeleme): beceri + kapasite
- [ ] Planı uygula (atama + tarih), elle düzeltme
- [ ] Baseline kaydet
- [ ] Gantt ekranı, kişi doluluk tablosu

### FAZ 5 — Takip ve analiz
- [ ] StatusUpdate (durum, % tamamlanma, harcanan saat)
- [ ] Snapshot (her güncellemede proje metrikleri)
- [ ] EVM: PV, EV, AC, SPI, CPI, EAC, Earned Schedule ile tahmini bitiş
- [ ] Sağlık skoru (AHP ağırlıkları configuration'da)
- [ ] Kural tabanlı risk uyarıları (aşırı doluluk, kritik yolda gecikme, bloke iş)
- [ ] Dashboard

### FAZ 6 — Veri ve ML
- [ ] Sentetik proje üreteci (gürültü + gizli değişken, formül ezberini önlemek için)
- [ ] Haftalık snapshot veri seti
- [ ] ML.NET: LogisticRegression / FastTree (GBM) / FastForest, metrikler (F1, AUC, MAE)
- [ ] EVM baseline ile karşılaştırma, özellik önemi (Permutation Feature Importance)
- [ ] `IPredictionService` ile uygulamaya entegrasyon

### FAZ 7 — What-if
- [ ] Senaryolar: kişi ekle/çıkar, iş çıkar, kapasite değiştir, deadline değiştir
- [ ] Monte Carlo (efor belirsizliği) → P50 / P80 bitiş, maliyet
- [ ] Brooks etkisi (yeni kişide ısınma süresi)
- [ ] Senaryo karşılaştırma ekranı

### FAZ 8 — AI katmanı (analiz yorumları)
- [x] `IChatModel`: Mock + Gemini + Claude (Faz 2.5'te)
- [x] ContextBuilder, sürümlü prompt (Faz 2.5'te)
- [ ] Analiz cevapları için sayı doğrulayıcı (cevaptaki her sayı bağlamda olmalı)
- [ ] Eksik iş önerisinin LLM katmanı (hibrit)
- [ ] Proje yorumu, senaryo yorumu (EVM/ML/Monte Carlo sonuçlarını açıklama)
- [x] AI öneri kaydı (AiAction) — [ ] analiz cevapları için ayrıntılı audit
- [ ] (Opsiyonel) Ollama sağlayıcısı ile karşılaştırma

### FAZ 9 — Demo ve değerlendirme
- [ ] Demo projesi: "Mobile Banking Modernization"
- [ ] RQ1–RQ4 ölçümleri, LLM sağlayıcı karşılaştırması
- [ ] 5–10 kişilik kullanıcı testi (SUS anketi)

### FAZ 10 — Rapor ve sunum
- [ ] Rapor bölümleri, sunum, demo videosu

## Kod Yapısı

```
src/
  ProjectMind.Domain/          Entity, enum (bağımlılıksız)
  ProjectMind.Application/     Servisler, DTO, iş kuralları, hesaplama motorları
  ProjectMind.Infrastructure/  EF Core (SQL Server), migrations, AI sağlayıcıları (Claude, Mock), ML.NET
  ProjectMind.Web/             Blazor Server bileşenleri (Components/), wwwroot/app.css
tests/
  ProjectMind.Tests/           Birim + entegrasyon (servis ve form) testleri
docs/                          Plan, kararlar, ilerleme
```

## Fikir Havuzu (uygulanmadı — sadece not)

- (boş)
