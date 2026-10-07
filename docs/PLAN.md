# ProjectMind AI — Proje Planı

**Akademik başlık:** Yapay Zekâ Destekli Yazılım Proje Planlama ve Karar Destek Sistemi: Zamanlama, Kaynak ve Kapsam Yönetimi Üzerine Bir Uygulama

**Aktif faz: FAZ 2 — Frontend iskeleti ve veri giriş ekranları**

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

### FAZ 1 — Backend temeli ve temel veri ✅
- [x] Solution: Domain / Application / Infrastructure / Api / Tests (.NET 10)
- [x] SQL Server + EF Core, ilk migration, docker-compose (SQL Server)
- [x] Entity'ler: Project, Person, WorkItem, WorkItemDependency
- [x] CRUD API: projeler, kişiler, işler, bağımlılıklar
- [x] Bağımlılık kuralları: kendine bağımlılık yok, tekrar yok, **döngü yok**
- [x] Global hata yönetimi (ProblemDetails), OpenAPI
- [x] Birim + entegrasyon testleri

### FAZ 2 — Frontend iskeleti ve veri giriş ekranları
- [ ] React + TS + Vite iskeleti, API istemcisi
- [ ] Proje listesi / oluşturma / düzenleme
- [ ] İş listesi (ekle, düzenle, sil, bağımlılık seç)
- [ ] Kişi listesi (ekle, düzenle, sil)

### FAZ 3 — Eksik iş önerisi (kural tabanlı katman)
- [ ] TaskTemplate (proje tipine göre faz/iş şablonu) + seed
- [ ] Eşleştirme (anahtar kelime/eş anlamlı) → eksik işler
- [ ] TaskSuggestion: öner, gerekçe, tahmini saat, bağımlılık; Kabul/Düzenle/Reddet
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
- [ ] Python FastAPI servis: LogReg / RandomForest / GBM, metrikler (F1, ROC-AUC, MAE)
- [ ] EVM baseline ile karşılaştırma, SHAP/özellik önemi
- [ ] .NET `IPredictionService` entegrasyonu

### FAZ 7 — What-if
- [ ] Senaryolar: kişi ekle/çıkar, iş çıkar, kapasite değiştir, deadline değiştir
- [ ] Monte Carlo (efor belirsizliği) → P50 / P80 bitiş, maliyet
- [ ] Brooks etkisi (yeni kişide ısınma süresi)
- [ ] Senaryo karşılaştırma ekranı

### FAZ 8 — AI katmanı
- [ ] `IAIService`: Mock, Ollama, Claude (ve/veya OpenAI uyumlu)
- [ ] ContextBuilder, prompt şablonları (versiyonlu), JSON şema + sayı doğrulayıcı
- [ ] Eksik iş önerisinin LLM katmanı (hibrit)
- [ ] Proje yorumu, senaryo yorumu, chat
- [ ] AI audit log

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
  ProjectMind.Infrastructure/  EF Core (SQL Server), migrations, AI sağlayıcıları
  ProjectMind.Api/             Controller'lar, hata yönetimi, OpenAPI
tests/
  ProjectMind.Tests/           Birim + entegrasyon testleri
frontend/                      (Faz 2) React + TS + Vite
ml/                            (Faz 6) Python ML servisi
docs/                          Plan, kararlar, ilerleme
```

## Fikir Havuzu (uygulanmadı — sadece not)

- (boş)
