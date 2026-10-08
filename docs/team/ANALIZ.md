# Analiz Raporu — Faz 9 (Demo ve değerlendirme)

**Tarih:** 2026-10-08 · **Analist:** pm-analyst · **Aktif faz:** FAZ 9 (`docs/PLAN.md:102-105`)

> **Önceki turlar:** Tur 1 (hata düzeltmeleri, 25b5ce7), Tur 2 (AI yorum kartları + audit, 1f8da6f), Tur 3 (hibrit eksik iş, dc0f63e).
> Eski iş listeleri ve lider kararları git geçmişinde (`git log -- docs/team/ANALIZ.md`).
> `docs/team/TEST_PAZAR.md` hâlâ Tur 1'e ait (115 test, Faz 8); P1–P6, P8, P9 Tur 1'de kapatıldı, P7 ve P10 Fikir Havuzu'nda.

## Durum

- `dotnet build`: **0 uyarı / 0 hata.** İlk denemede `Microsoft.AspNetCore.Mvc.Testing.targets(38,5)` içinde geçici MSB4018 hatası
  çıktı; tekrar ve `--no-incremental` derlemelerde temiz. Paralel derlemede dosya kilidi, kod sorunu değil.
- `dotnet test`: **213/213 geçti** (≈9 sn). Çalışma ağacı temiz.
- Faz 9'un açık maddeleri: demo projesi, RQ1–RQ4 ölçümleri + LLM sağlayıcı karşılaştırması, 5–10 kişilik SUS testi.
  **Kodda bunların hiçbiri yok:** demo/seed kodu yok (`grep -i "demo|seed|Mobile Banking"` yalnız ML/Monte Carlo tohumlarını buluyor).
  Değerlendirme sayfası, anket ve dışa aktarma da yok (tek CSV: Deneyler sayfasındaki sentetik ML raporu, `Experiments.razor:185`).

## Ölçüm açıkları (RQ bazında)

| RQ | Bugün ne var | Eksik |
|---|---|---|
| RQ1 | Sentetik test setinde ML ve EVM, %25/%50/%75 kontrol noktalarında karşılaştırılıyor; CSV var (`DelayExperiment.cs:20-40`) | Deney tek tohumla çalışıyor (`MlOptions.Seed`, `DelayModels.cs:56`), ortalama ± sapma yok. Demo projede ML olasılığı snapshot'a yazılmıyor (`ProjectSnapshot.cs` alanları; `ProjectStatusService.cs:112-137`). Bu yüzden "hangisi daha erken uyardı" zaman çizelgesi üretilemiyor. Rapor yalnız bellekte tutuluyor (`IDelayPredictor.LastReport`). |
| RQ2 | Permütasyon özellik önemi (sentetik) | Tek tohum (yukarıdaki gibi). |
| RQ3 | What-if ekranı, Monte Carlo deterministik (`WhatIfOptions.Seed = 7`) | Hiçbir what-if çalıştırması kaydedilmiyor (`WhatIfService.cs:12`). Karar desteği için anket maddesi yok. |
| RQ4 | Kaynağa göre kabul oranı (`AiActionService.cs:96-100`), `AiAnalysisLog` | Kayıtlar silinebiliyor ve toplu uygulama oranı şişiriyor (madde 2). Kural katmanına karşı hibrit katmanın recall'u ölçülmüyor. "Açıklama anlaşılır mı?" sorusu yok. |
| Sağlayıcı | `AiAnalysisLog`: model, süre, şema geçerliliği, doğrulanamayan sayılar | Sabit görev seti ve koşucu yok. Token/maliyet kaydı yok (`ChatModelContracts.cs:16,27` yalnız metin ve model döndürüyor). Kayıtlar bir değerlendirme koşusuna etiketlenemiyor. |

## Geliştirme iş listesi (öncelik sırasıyla)

### 1. NumberGuard, Türkçe binlik ayraçlı kanıtı tanımıyor `[hata]` — S
- **Neden:** Kanıt her zaman JSON biçiminde ayrıştırılıyor (`NumberGuard.cs:35`, `:138-139`). Ama kanıtın bir kısmı Türkçe
  biçimli metin: kullanıcı mesajı (`ChatService.cs:146,164-165`) ve `Format.Money/Number` ile üretilen kart özetleri
  (`ChatService.cs:342`, `AiActionService.cs:203,218`). "1.250.000" gibi çok gruplu sayılar ayrıştırılamıyor ve kanıttan düşüyor.
  Scratchpad'da çalıştırarak doğruladım:
  - Kullanıcı "Bütçe 1.250.000 TL olsun" yazdı, cevap "Bütçe 1.250.000 TL." → **`1.250.000` doğrulanamadı** olarak işaretlendi.
  - Kart özeti "… 1.250.000 TRY", cevap "1.250.000,00 ₺" → işaretlendi.
  Demo projenin bütçesi milyonluk olacağı için kullanıcı testinde her turda yanlış uyarı çıkar. Bu SUS puanını düşürür ve
  "Doğrulanamayan sayılar" uyarısına güveni zayıflatır.
- **Dosyalar:** `Application/Ai/NumberGuard.cs`, `tests/.../Unit/NumberGuardTests.cs`.
- **Kabul:** Yukarıdaki iki örnek temiz dönmeli. `{"totalHours":1500}` kanıtı ile "1.500 saat" cevabı temiz kalmalı.
  JSON `0.87` kanıtıyla "SPI 0,87" eşleşmeye devam etmeli. Kanıtta olmayan "137" hâlâ işaretlenmeli. Mevcut NumberGuard testleri yeşil kalmalı.
  Önerilen çözüm: kanıtı hem JSON hem Türkçe biçimde ayrıştırıp bilinen sayıları birleştirmek.

### 2. RQ4 kabul verisi silinebiliyor ve toplu uygulama oranı şişiriyor `[hata]` — M
- **Neden:**
  - "Geçmişi temizle" ve sohbet silme öneri kayıtlarını da siliyor (`ChatService.cs:104-109`; ayrıca cascade:
    `Configurations.cs:105-106`). Kullanıcı testinden sonra temizlik yapılırsa Deneyler sayfasındaki kabul oranı geri getirilemez.
    `AiAnalysisLog` ise kalıcı (D25), yani iki kaynak tutarsızlaşıyor.
  - "Bekleyen N öneriyi uygula" butonu (`ChatPanel.razor:25,238`, `AiActionService.cs:159-174`) her kartı tek tek
    "Uygulandı" sayıyor. Kartlara bakmadan toplu kabul, RQ4 kabul oranını yapay olarak yükseltir.
- **Dosyalar:** `Chat/ChatService.cs` (RemoveHistoryAsync), `Ai/AiActionService.cs`, `Domain/Entities/AiAction.cs`
  (yeni `bool DecidedInBulk`), yeni migration, `Pages/Experiments.razor` (tabloya "tek tek / toplu" ayrımı).
- **Kabul:**
  - Geçmiş temizlenince karara bağlanmış kartlar kalmalı (`ChatMessageId = null`), bekleyenler silinmeli.
    Test: temizlik öncesi ve sonrası `GetAcceptanceBySourceAsync` sayıları aynı olmalı.
  - `ApplyAllPendingAsync` ile uygulanan kartlar `DecidedInBulk = true` olmalı.
  - `SourceAcceptance` iki oran vermeli: tümü ve yalnız tek tek kararlar. Elle doğrulanan örnek: 2 tek tek uygulandı +
    1 reddedildi + 3 toplu → 5/6 ve 2/3.

### 3. Demo projesi "Mobile Banking Modernization" (geçmişiyle birlikte) `[aktif faz]` — L
- **Neden:** EVM, S-eğrisi, ML ("son 2 hafta hızı" en az 2 haftalık snapshot ister, `DelayPredictionService.cs:21-25`) ve
  what-if ancak geçmişi olan bir projede anlamlı değer üretir. Bugün plan "bugünden" başlıyor (`ScheduleService.cs:184-185`)
  ve geçmiş üretmenin bir yolu yok.
- **Önerilen yaklaşım:** Gerçek servisleri, geriye kaydırılmış bir saatle (`TimeProvider`'dan türeyen küçük bir sınıf; yeni paket
  gerekmez) yeniden oynatmak:
  1. Proje (Mobile, bütçe, hedef tarih), ~6 kişi, ~30 iş (WBS + bağımlılık).
  2. Bugünden 8–10 hafta önce `ScheduleService.ApplyAsync` (baseline).
  3. Her hafta `WorkItemService` ile ilerleme ve harcanan saat girilir, ardından `ProjectStatusService.GetAsync` çalışır (o günün
     snapshot'ı). Sabit tohumla gerçekçi sapma: bir bloke iş, baseline sonrası eklenen kapsam, bir iptal edilen iş.
  4. Bilerek bırakılan birkaç eksik şablon işi ("Eksik iş var mı?" demosu için).
  Sonuç kodla üretilmeli; LLM sayı üretmemeli.
- **Dosyalar:** yeni `Application/Demo/DemoProjectSeeder.cs` (+ `DemoOptions`: hafta sayısı, tohum), DI kaydı. Tetikleyici olarak
  `Home.razor`'da "Demo projesini yükle" butonu (bileşen yalnız `AppScope` ile servisi çağırır). Testler `tests/.../Integration/DemoSeederTests.cs`.
- **Kabul (SQLite testi):**
  - En az 8 snapshot oluşmalı, hepsi aynı `BaselineId` ile.
  - Son durumda SPI(t) < 1 olmalı ve `Delay` null olmamalı.
  - Uyarılar en az bir bloke ve en az bir kapsam büyümesi içermeli.
  - Aynı tohumla iki çalıştırmada EVM değerleri aynı olmalı.
  - İsim zaten varsa ikinci proje açılmamalı; ya Türkçe hata vermeli ya da açıkça "yeniden oluştur" seçeneği sunmalı.
  - Mock modda tamamen çalışmalı.
- **Not:** `AppDbContext.cs:27` `CreatedAt` için gerçek saati kullanıyor. Baseline ve kartların oluşturulma zamanı "bugün"
  görünür; bu kabul edilebilir ama PROGRESS'e varsayım olarak yazılmalı.

### 4. RQ1/RQ2 için tekrarlanabilir kanıt: snapshot'ta ML tahmini + çok tohumlu deney `[aktif faz]` — M
- **Neden:** RQ1 "daha erken" sorusu demo projede ancak her snapshot'ta ML olasılığı ve bitiş tarihi saklanırsa gösterilebilir.
  Şu an yalnız EVM alanları saklanıyor (`ProjectSnapshot.cs`). Sentetik deney tek tohumla çalışıyor; akademik iddia için en az
  5 tohumda ortalama ± standart sapma gerekir.
- **Dosyalar:**
  - `Domain/Entities/ProjectSnapshot.cs` (+`DelayProbability`, `MlForecastFinish`) ve migration.
  - `Analytics/ProjectStatusService.cs`: tahmini snapshot yazımından önce hesaplayıp yazmalı. Şu an sıra ters:
    snapshot `:86`, tahmin `:98-106`.
  - `Ml/DelayExperiment.cs` (+`RunSeeds`), `Pages/Experiments.razor` (çok tohumlu tablo + CSV).
- **Kabul:**
  - Demo seed sonrası her snapshot'ta `DelayProbability` dolu olmalı.
  - Proje zaman çizelgesi CSV'si (`date,spi,spi_t,evm_forecast,ml_probability,ml_forecast`) satır sayısı snapshot sayısına eşit olmalı.
  - Çok tohumlu koşu her yöntem ve kontrol noktası için `mean,sd,n` vermeli. 3 sahte değerle elle doğrulanan ortalama/sapma testi yazılmalı.

### 5. Değerlendirme sayfası ve RQ dışa aktarımları `[aktif faz]` — M
- **Neden:** Rapor (Faz 10) için her RQ'nun tablosu tek tıkla ve aynı biçimde üretilebilmeli. Bugün kabul oranı yalnız ekranda;
  `AiAnalysisLog` ve snapshot için dışa aktarma yok.
- **Dosyalar:**
  - Yeni `Application/Evaluation/EvaluationReportService.cs` (CSV üretimi; sayılar C#'ta, InvariantCulture).
  - Yeni `Pages/Evaluation.razor` (`/evaluation`): RQ1 → madde 4, RQ3 → madde 8, RQ4 → kaynak × araç × durum ve yorum
    geri bildirimi, sağlayıcı → madde 6, SUS → madde 7.
  - İndirme için mevcut `pmDownload` JS fonksiyonu yeniden kullanılır.
- **Kabul:** Seed'li SQLite'ta her CSV'nin başlığı sabit olmalı ve satır sayısı DB'deki kayıtlarla eşleşmeli (test).
  CSV'de API anahtarı veya tam bağlam olmamalı (test: kolon listesi). Bileşende EF sorgusu olmamalı.

### 6. LLM sağlayıcı karşılaştırma koşucusu (denetim kaydını kullanan) `[aktif faz]` — L
- **Neden:** Faz 9 maddesi. Sağlayıcı bugün kapsam başına tek (`Infrastructure/DependencyInjection.cs:27-37`) ve kayıtlar bir
  koşuya bağlanamıyor.
- **Önerilen yaklaşım:** Demo projesi üzerinde sabit, sürümlü bir görev seti (`eval-v1`):
  - proje yorumu,
  - 2 senaryolu senaryo yorumu,
  - hibrit eksik iş: demo kopyasından bilinen k işi çıkarıp kural recall'u ile hibrit recall'u ölçmek
    (`HybridMissingWork.IsSameWork` ile eşleşme; RQ4 "plan bütünlüğü"),
  - 5 sabit sohbet sorusu.
  Her görev, yapılandırılmış her sağlayıcıda (`GeminiChatModel`, `ClaudeChatModel` somut tiplerle) n kez (ayar, varsayılan 3)
  çalıştırılmalı.
- **Dosyalar:**
  - Yeni `Application/Evaluation/ProviderComparisonService.cs` ve `EvaluationTasks.cs`.
  - `Domain/Entities/AiAnalysisLog.cs`: +`EvaluationRunId` (string?), +`InputTokens`/`OutputTokens` (int?). Migration.
  - `Ai/ChatModelContracts.cs:16,27`: sonuçlara isteğe bağlı kullanım (usage) bilgisi.
  - `Infrastructure/Ai/GeminiChatModel.cs` (`usageMetadata`), `ClaudeChatModel.cs` (`Usage`).
  - `/evaluation` sayfasında "Karşılaştırmayı çalıştır" butonu (yalnız butonla; maliyet uyarısı).
- **Kabul:**
  - Senaryolu iki sahte modelle test: her sağlayıcı için şema geçerlilik oranı, doğrulanamayan sayı oranı (sayı/cevap),
    medyan süre, hata oranı, hibrit recall.
  - Bir sahte modelin şema dışı cevabı oranı elle hesaplanan değere düşürmeli (ör. 2/3).
  - Tüm kayıtlar aynı `EvaluationRunId`'yi taşımalı. Testler gerçek API çağırmamalı.
  - Anahtarsız sağlayıcı atlanmalı ve raporda "yapılandırılmamış" yazmalı.

### 7. SUS anketi (+ RQ3/RQ4 Likert maddeleri) ve puan hesabı `[aktif faz]` — M
- **Neden:** Faz 9 maddesi. Puan hesabı test edilmiş C# kodu olmalı (CLAUDE.md §2).
- **Dosyalar:**
  - Yeni `Domain/Entities/SurveyResponse.cs`: katılımcı kodu (P01…; ad/e-posta yok), 10 SUS maddesi (1–5), RQ3 için 2 madde
    (what-if karar güveni / yararlılık), RQ4 için 2 madde (AI açıklaması anlaşılır / eksik iş önerisi yararlı), görev
    tamamlama süreleri (opsiyonel). Migration gerekir.
  - `Application/Evaluation/SusScore.cs` ve `SurveyService.cs`.
  - `Pages/Survey.razor` (`/evaluation/survey`): standart Türkçe SUS metinleri, ayarlanabilir sabitler. Hesap bileşende değil.
- **Kabul:** Birim testleri:
  - Hepsi 3 → 50.
  - Tek maddeler 5, çift maddeler 1 → 100.
  - Tersi → 0.
  - Örnek karışık cevap elle hesaplanmış değere eşit olmalı (Brooke formülü: (Σ(tek−1)+Σ(5−çift))×2,5).
  - Eksik madde varsa kayıt reddedilmeli (Türkçe hata).
  - Değerlendirme sayfasında n, ortalama, standart sapma, min/maks ve CSV olmalı.

### 8. RQ3/RQ4 kullanım kanıtı: what-if çalıştırma kaydı + yorum kartında "Anlaşılır mıydı?" `[aktif faz]` — M
- **Neden:** Bu iki satır şu an PLAN Fikir Havuzu'nda (`docs/PLAN.md:135-136`). Ama Faz 9 RQ3/RQ4 ölçümü için gereken veri
  bunlar. **Lider veya kullanıcı onayı gerekli** (faz kapsamına alınmaları).
- **Dosyalar:**
  - Yeni `WhatIfRunLog` (proje, senaryo değişiklikleri JSON, senaryo başına P80 ve hedefe yetişme olasılığı, süre ms) ve migration.
  - `WhatIf/WhatIfService.cs:12` sonunda kayıt yazılmalı.
  - `AiAnalysisLog.Helpful` (bool?).
  - `ProjectCommentService` (+`RateAsync`), `Shared/AiCommentCard.razor` (Evet/Hayır butonları, yalnız callback).
- **Kabul:**
  - `CompareAsync` her çağrıda tam bir kayıt yazmalı; kayıttaki P80 değerleri dönen karşılaştırmayla aynı olmalı (test).
  - Puanlama yalnız `Success` kayıtta kabul edilmeli, ikinci puan öncekinin üzerine yazmalı (test).
  - Mock modda puanlama butonu görünmemeli.

## Küçük teknik borç (listeye girmedi; uygun bir maddeyle birlikte ele alınabilir)

- `HybridMissingWork.cs:73-74`: üst sınırı (`MaxLlmSuggestions`) aşan öneri ne `Dropped`'a ne de kayda giriyor. RQ4 için
  "LLM kaç öneri verdi" sayısı eksik kalıyor; `DroppedSuggestion` nedeni olarak "üst sınır" eklenmeli.
- `ChatService.cs:258-263`: kural iş kartı reddedilirse bağımlılık kartları "Bekliyor" kalıyor. Sonra "hepsini uygula" bunları
  `Failed` yapıyor.
- `ChatService.cs:237-241`: kısayolda `CheckHybridAsync` içindeki etki hesabı `BusinessRuleException` atarsa (döngü,
  `CriticalPath.cs:76`), kullanıcı mesajı cevapsız kalıyor. Panel hatayı gösteriyor (`ChatPanel.razor`, RunShortcutAsync),
  ama mesaj geçmişte yetim kalıyor.
- `NumberGuard.cs:71`: yuvarlama toleransı ("0,9" ↔ 0,87) bilerek gevşek (Tur 1 bilinen sorun). Madde 6'daki görev setinde
  etiketli 20–30 cümleyle NumberGuard precision/recall'u ölçülüp raporda sınırlılık olarak verilmeli.

## Fikir havuzu (geliştirici uygulamaz, PLAN'a eklenir)

- `[fikir havuzu]` Deney raporunun DB'de kalıcı saklanması (bugün yalnız bellekte; deterministik olduğu için yeniden üretilebiliyor).
- `[fikir havuzu]` Demo senaryosunun JSON dosyasından içe aktarılması (farklı demo projeleri için).
- `[fikir havuzu]` Sağlayıcı karşılaştırmasında tahmini maliyet (token × birim fiyat, ayardan).
- `[fikir havuzu]` RQ1 için önyükleme (bootstrap) güven aralığı / eşleştirilmiş AUC testi.
- `[fikir havuzu]` (Zaten PLAN'da) Opsiyonel Ollama sağlayıcısı: madde 6 koşucusu hazır olunca yalnız yeni bir `IChatModel` eklemek yeterli.

## Riskler / açık sorular (kullanıcıya)

1. **SUS nerede toplanacak?** Uygulama içi form (madde 7, migration gerekir) mi, kâğıt/Google Forms + yalnız puan hesaplayıcı mı?
   Uygulama içi form tek kullanıcı kuralını (D2) bozmaz (hesap yok, katılımcı kodu var), ama onay gerekli.
2. **Katılımcı verisi / KVKK:** Yalnız anonim kod saklanacak; bilgilendirilmiş onam metni ve etik kurul gereği var mı?
3. **Sağlayıcı karşılaştırması bütçesi:** Hem Gemini hem Claude anahtarı olacak mı? Görev başına tekrar sayısı (öneri: 3)
   ve ücretsiz katman kota sınırları? Ollama dahil mi?
4. **Demo tarihleri:** Demo "bugüne göre 8–10 hafta önce" mi başlasın (her gün geçerli, önerilen), yoksa sabit tarihli mi
   (rapordaki ekran görüntüleri aynı kalır ama "bugün" sonrası EVM değişir)?
5. **Madde 8:** Fikir Havuzu'ndaki iki satır Faz 9 kapsamına alınsın mı (RQ3/RQ4 için önerilir)?
6. **Geçerlilik sınırı:** ML yalnız sentetik veriyle eğitiliyor (D21). Demo projesindeki ML sonucu doğrulama değil, gösterimdir;
   raporda böyle yazılmalı. RQ4 kabul oranı ancak katılımcıların kendi kararlarıyla anlamlıdır (geliştirici kullanımı ayrılmalı;
   madde 2 + `EvaluationRunId` / katılımcı kodu).

---

## Lider birleştirmesi ve kararları (Tur 4)

Açık sorulara kararlar: SUS ve RQ3/RQ4 Likert soruları **uygulama içinde** (anonim katılımcı kodu, kişisel veri yok, onay
metni gösterilir; CSV dışa aktarım). Demo tarihleri **bugüne göre** (saat 8–10 hafta geriye kaydırılarak gerçek servislerle
üretilir). Madde 9 (what-if çalıştırma kaydı + "açıklama anlaşılır mı?" butonu) **Faz 9'a alındı**. Sağlayıcı karşılaştırması
ayarlı anahtarlarla çalışır, varsayılan 3 tekrar; Ollama kapsam dışı.

### Tur 4a — hatalar (önce bunlar; demo bunlara bağlı)
| Sıra | Madde | Kaynak |
|---|---|---|
| H1 | Biten projede SPI(t) planlanan bitişten sonra düşmesin: EV ≥ BAC olunca AT tamamlanma tarihinde dondurulur | TEST_PAZAR Q1 |
| H2 | Baseline'daki işin silinmesi EVM'yi bozmasın: ilerlemesi/harcaması olan iş silinmez → İptal'e çevrilir (kart ve servis); baseline'da olup artık olmayan iş kapsam dışı sayılır | TEST_PAZAR Q2 |
| H3 | NumberGuard: kanıttaki Türkçe biçimli sayılar (1.250.000, 0,87) doğru okunur; "1,2 milyon", "120 bin" çarpanları; mevcut D23 testleri bozulmaz. Yön kelimeleri ve ≤10 tam sayılar bu turda yok (bilinen sorun olarak yaz) | ANALIZ 2, TEST_PAZAR Q5 |
| H4 | RQ4 veri bütünlüğü: sohbet silme/temizleme AiAction kayıtlarını silmesin (yeni migration ile ilişki SetNull/korunur); "hepsini uygula" ile uygulanan kartlar ayrı işaretlensin, kabul oranında ayrı gösterilsin | ANALIZ 3 |
| H5 | Hibrit tekrar eleme: tek kelimelik mevcut adlar sadece tam eşleşmede eler; elenen öneri sayısı/adları cevapta ve audit'te görünür; fazla madde (>Max) cevabı reddetmez, kesilir | TEST_PAZAR Q6, Q8 |
| H6 | Yorum önbelleği anahtarına model/sağlayıcı dahil; tüm işler iptalse CPI "—" | TEST_PAZAR Q10, Q12 |

### Tur 4b — demo (4a'dan sonra)
Demo "Mobile Banking Modernization" tohumu (ANALIZ madde 4) + snapshot'a ML olasılığı/tahmini (madde 5, migration).

### Sonraki turlar
Değerlendirme sayfası ve RQ CSV'leri, SUS/Likert formu, sağlayıcı karşılaştırma koşucusu, what-if kaydı + "anlaşılır mı?"
butonu; RQ1'e "erken uyarı süresi" ve çoklu tohum analizi (TEST_PAZAR öneri 10).
