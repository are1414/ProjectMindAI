# ProjectMind AI — Proje Planı

**Akademik başlık:** Yapay Zekâ Destekli Yazılım Proje Planlama ve Karar Destek Sistemi: Zamanlama, Kaynak ve Kapsam Yönetimi Üzerine Bir Uygulama

**Aktif faz: FAZ 9 — Demo ve değerlendirme**

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
- [x] Alt işler (WBS hiyerarşisi): üst iş eforu/ilerlemesi alt işlerden toplanır; chat'te `parentName`
- [x] WBS numaraları (1, 1.2, 1.2.1), +/− ile aç-kapat, sayfada ad/efor/% düzenleme

### FAZ 3 — Eksik iş önerisi (kural tabanlı katman, chat'e araç olarak) ✅
- [x] İş şablonu kataloğu (proje tipine göre, kodda sürümlü: templates-v1) — DB tablosu yerine kod (sabit bilgi)
- [x] Eşleştirme (Türkçe sadeleştirme + anahtar kelime, kısa kelimede tam kelime) → eksik işler + bağımlılık önerisi
- [x] `check_missing_work` salt okunur aracı; model add_work_item / add_dependency kartlarıyla önerir; chat kısayolu
- [ ] "Kabul etmeden önce plana etkisi" bilgisi (Faz 4 sonrası bağlanır) → Faz 4'e taşındı
- [x] Test: tam plandan birer iş çıkarma (leave-one-out) → precision = recall = 1.0 (15 şablon)

### FAZ 4 — Otomatik planlama ✅
- [x] Eksik iş önerisinin plana etkisi (ek iş günü, efor, maliyet — simülasyonla)
- [x] CPM: ES/EF/LS/LF, bolluk (slack), kritik yol
- [x] Kaynak kısıtlı çizelgeleme (seri çizelgeleme, öncelik = en küçük LS): beceri + haftalık kapasite, sabit atamalar korunur
- [x] Planı uygula (tarih + boş atamalar; üst iş tarihleri alt işlerden) — elle düzeltme: chat'ten update_work_item (plannedStart/End)
- [x] Baseline kaydet (Baselines / BaselineItems; EVM PV kaynağı)
- [x] Gantt ekranı (kritik yol / bolluk / kişisiz / üst iş), kişi doluluk tablosu, maliyet-bütçe, hedefe göre sapma
- [x] AI: preview_schedule (salt okunur, tarih/maliyet soruları), apply_schedule (kart)

### FAZ 5 — Takip ve analiz ✅
- [x] StatusUpdate (durum, % tamamlanma, harcanan saat — her değişiklikte)
- [x] Snapshot (günde bir, durum hesaplandığında güncellenir)
- [x] EVM: PV, EV, AC, SPI, CPI, EAC, Earned Schedule (SPI(t)) ile tahmini bitiş — efor bazlı + maliyet
- [x] Sağlık skoru (AHP; ikili karşılaştırma matrisi `Health` ayarında, CR kontrolü)
- [x] Kural tabanlı risk uyarıları (SPI/CPI eşikleri, hedef tarih, kapsam büyümesi, bloke, gecikmiş iş, kaynak)
- [x] Dashboard: Durum sekmesi (sağlık, EVM kartları, uyarılar, S-eğrisi + tablo görünümü)
- [x] AI: get_project_status aracı; NumberGuard (doğrulanamayan sayıları işaretler)

### FAZ 6 — Veri ve ML ✅
- [x] Sentetik proje üreteci (haftalık simülasyon; gizli değişkenler + gürültü + "%90 sendromu"; synthetic-v1, tohumlu)
- [x] Haftalık snapshot veri seti (%25/%50/%75 kontrol noktaları; proje bazlı eğitim/test ayrımı)
- [x] ML.NET: LogisticRegression / FastTree (GBM) / FastForest + FastTree süre regresyonu, metrikler (F1, AUC, MAE)
- [x] EVM (Earned Schedule) kuralı ile aynı test satırlarında karşılaştırma, permütasyon özellik önemi (AUC düşüşü)
- [x] `IDelayPredictor` ile entegrasyon: Durum sekmesinde gecikme olasılığı + ML/EVM bitiş, `get_project_status`'ta tahmin
- [x] Deneyler sayfası (`/experiments`): yeniden eğit, sonuç tablosu, özellik önemi, CSV indir

### FAZ 7 — What-if ✅
- [x] Senaryolar: kişi ekle/çıkar, iş çıkar (alt işleriyle, bağımlılık zinciri korunur), kapasite değiştir, deadline değiştir
- [x] Monte Carlo (efor belirsizliği, üçgen dağılım, ortak rastgele sayılar) → P50 / P80 / P90 bitiş, maliyet, hedefe yetişme olasılığı
- [x] Brooks etkisi (yeni kişide ısınma süresi + mevcut ekipte mentorluk yükü; katılım tarihi)
- [x] Senaryo karşılaştırma ekranı (What-if sekmesi: tablo + bitiş olasılığı eğrisi) ve AI aracı `simulate_what_if`

### FAZ 8 — AI katmanı (analiz yorumları) ✅ (opsiyonel Ollama açık)
- [x] `IChatModel`: Mock + Gemini + Claude (Faz 2.5'te)
- [x] ContextBuilder, sürümlü prompt (Faz 2.5'te)
- [x] JSON şemalı model çağrısı (`IChatModel.CompleteJsonAsync`: Gemini `responseJsonSchema`, Claude structured output) + C# şema doğrulaması (D25)
- [x] Analiz cevapları için sayı doğrulayıcı (cevaptaki her sayı bağlamda olmalı) — sohbet (D23) ve yorum kartları (D25)
- [x] Eksik iş önerisinin LLM katmanı (hibrit): kural önce, LLM ek önerileri (JSON şema, S/M/L → saat C#'ta), tekrar eleme, kart kaynağı `Rule/Llm/User` C#'ta, kaynağa göre kabul oranı Deneyler sayfasında (D26)
- [x] Proje yorumu, senaryo yorumu (EVM/ML/Monte Carlo sonuçlarını açıklama) — Durum ve What-if sekmelerinde "AI yorumu" kartı
- [x] AI öneri kaydı (AiAction) — [x] analiz cevapları için ayrıntılı audit (`AiAnalysisLog`)
- [ ] (Opsiyonel) Ollama sağlayıcısı ile karşılaştırma

### FAZ 9 — Demo ve değerlendirme
- [x] Tur 4a hata düzeltmeleri (demo öncesi): biten projede SPI(t), silme → iptal / eksik baseline işi kapsam dışı (D27),
  NumberGuard Türkçe kanıt + bin/milyon, RQ4 kayıt bütünlüğü + toplu kabul ayrımı, hibrit tekrar eleme, önbellekte sağlayıcı (D28)
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

- Projeye özel açıklama: tahmini en çok etkileyen göstergeler (yerel katkı) Durum kartında
- Monte Carlo efor belirsizliğini projenin gerçekleşen CPI dağılımından kalibre etmek
- Senaryoyu tek tıkla öneri kartlarına dönüştürmek (kişi ekle / iş çıkar kartları)
- Gerçek proje snapshot'larıyla (Faz 9 demo) modelin ince ayarı / doğrulanması
- İş bazında belirsizlik aralığı (en iyi / en kötü efor) ile Monte Carlo (LiquidPlanner tarzı)
- Resmî tatil / kişi izni takvimi (WorkCalendar'a)
- What-if hızlandırma: hazır kuyruğu için öncelik kuyruğu, Monte Carlo turlarını paralel çalıştırma, iptal desteği
- Risk uyarısından ilgili işe / Gantt satırına bağlantı
- Çoklu baseline karşılaştırması (ve VAC gösterimi)
- AI ile iş kırılımı ("bu işi alt işlere böl" — add_work_item + parentName kartları; Faz 8 hibrit eksik iş maddesiyle birlikte değerlendirilecek)
- Yorum kartında "Açıklama anlaşılır mıydı? (Evet/Hayır)" geri bildirimi (RQ4 ölçümü)
- What-if çalıştırmalarının ve seçilen senaryonun loglanması (RQ3 kanıtı)
- Öneri kabul oranı raporu (kaynak × araç) Deneyler sayfasında, CSV ile
- Deney raporunun DB'de kalıcı saklanması (bugün yalnız bellekte; deterministik, yeniden üretilebilir)
- Demo senaryosunun JSON dosyasından içe aktarılması (farklı demo projeleri için)
- Sağlayıcı karşılaştırmasında tahmini maliyet (token × birim fiyat, ayardan)
- RQ1 için önyükleme (bootstrap) güven aralığı / eşleştirilmiş AUC testi
- NumberGuard hızlandırma: bilinen değerleri ondalık basamağa göre yuvarlanmış HashSet'lerde tutmak (TEST_PAZAR Q9)
- Kapsamın tamamı iptal edildiğinde "Kapsamın tamamı iptal edildi" bilgi uyarısı (TEST_PAZAR Q12)
- Yorum yüklerinde yön ifadesini C#'ın hazır cümle olarak vermesi ("120,5 saat geride"), ardından NumberGuard'ın sadeleştirilmesi (TEST_PAZAR Q3)
