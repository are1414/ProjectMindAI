# İlerleme Günlüğü

Her oturum sonunda en üste yeni kayıt eklenir. Format: yapılanlar · varsayımlar · bilinen sorunlar · sıradaki adım.

---

## 2026-10-08 (18) — Ekip turu 1: hata düzeltmeleri

Kapsam: `docs/team/ANALIZ.md` → "Lider birleştirmesi (Tur 1)" T1–T6 ([hata]). Faz 8 yorum kartları Tur 2'de.

- **T1 NumberGuard sayı aklama**: `ChatService` kanıtına artık yalnız bu turun mesajı (+ bağlam), önceki **kullanıcı**
  mesajları ve bu turun araç sonuçları giriyor; önceki asistan cevapları (uyarı satırı dahil) girmiyor. Test: 1. turda
  işaretlenen "137" 2. turda da işaretleniyor; kullanıcının önceki mesajındaki sayı kabul ediliyor.
- **T2 NumberGuard kuralları** (`Ai/NumberGuard.cs` yeniden düzenlendi): ×100/÷100 yalnız "%"/"yüzde" bitişikken;
  "5.12.2026", "05.12.2026", "5 Aralık 2026", "5 Aralık" (yılsız: gün+ay), ISO tarihler gün bazında; kanıttaki tarihlerin
  yılı bilinen sayı; saat ("14:30") atlanır; geçersiz tarih (31.02.2027) ve ayrıştırılamayan sayı benzeri ifade ("12,5,75")
  işaretlenir. Mevcut 5 InlineData + 2 test değişmeden yeşil.
- **T3 EVM iptal**: `EvmProgress.IsCancelled`; iptal edilen baseline işi BAC ve PV eğrisinden düşülür, `EvmResult.DescopedHours`
  olarak raporlanır (`get_project_status` → `evm.descopedHours`), harcanan saati AC'de kalır. Kapsam büyümesi de iptal düşülmüş
  baseline'a göre. Elle doğrulanan testler: B (24 s) iptal + A bitti → BAC 16, SPI 1, %100; hepsi iptal → BAC 0, hata yok;
  servis testi: hedef tarih riski uyarısı çıkmıyor.
- **T4 LLM zaman aşımı**: Gemini'de `HttpClient.Timeout` (TaskCanceledException, `ct` iptal değilken) →
  `ChatModelException("AI servisi zamanında cevap vermedi…")`; model listesi alınamazsa yedek arama `null` döner. Claude'da
  `HttpRequestException` ve zaman aşımı da çevriliyor. `ChatService` ek güvenlik ağı: çevrilmemiş zaman aşımı/bağlantı
  hatası da kullanıcıya dostça asistan mesajı olarak kaydedilir (sayfa çökmez). Ortak metinler `ChatModelMessages`.
  Testler: 50 ms zaman aşımlı gerçek `HttpClient` + cevap vermeyen handler; kullanıcı iptali zaman aşımı sayılmıyor;
  sohbet servisi seviyesinde TaskCanceledException.
- **T5 What-if doğrulama**: kişi ekle → haftalık saat 0 < h ≤ 168 (`ScenarioApplier.MaxWeeklyHours`), saatlik maliyet ≥ 0;
  kapasite değiştir → aynı üst sınır. `ResourceScheduler`: kapasitesi 0 olan sabit atanmış kişide ayrı uyarı
  ("'X' işine atanan Y kişisinin kapasitesi 0; iş kişisiz planlandı"), yanlış "becerisine sahip kişi yok" uyarısı verilmiyor.
- **T6 Yeniden baseline** (şema değişmedi): `ScheduleService.ApplyAsync` Bitti işlerin mevcut plan tarihlerini değiştirmiyor;
  başlamış işin daha erken plan başlangıcı korunuyor. Baseline'da başlamış/bitmiş işin tam eforu iki satıra ayrılıyor:
  kazanılmış kısım plan başlangıcından önce (eski tarihleriyle, en geç önceki iş günü), kalan kısım yeni pencerede.
  `EarnedValue` AC'yi iş başına bir kez sayıyor (satırlar gruplanıyor). Elle doğrulanan test: A 16 s bitti, B 40 s %50,
  9 Kasım'da yeniden plan → PV 42,67 / EV 36 / AC 36 / SPI 0,84 (eski hesap PV 29,33 → SPI 1,23); plan aynen uygulanınca
  11 Kasım'da SPI = SPI(t) = 1. Mevcut "Apply…baseline" testi 3 → 4 satır olarak güncellendi (DB %50 işi ikiye ayrıldı).
- Kararlar: D23 (NumberGuard kanıt/eşleşme), D24 (iptal = descope, yeniden baseline'da kazanılmış/kalan ayrımı).
- PLAN Fikir Havuzu'na 9 satır eklendi (lider listesi + analist fikir havuzu).
- Testler **150/150** (+35 vaka; 115'ten). `dotnet build` 0 uyarı / 0 hata. DB şeması / migration değişikliği yok.

**Varsayımlar**: İptal edilen işe harcanan saat gerçekleşmiş maliyettir (AC'de kalır, CPI'ı düşürür). Yeniden baseline
gününde (gün 0) SPI'ın 1'in biraz altında görünmesi doğaldır: durum günü o günün planlanan işini de içerir (ilk baseline'daki
davranışla aynı). Yılsız "15 Aralık" kanıttaki herhangi bir yılın aynı gün/ayıyla eşleşir. Ay adı eşleşmesi küçük/büyük harf
duyarsızdır ama "ARALIK" gibi büyük Türkçe "I" içeren yazımlar tanınmaz (nadir).

**Bilinen sorunlar**: NumberGuard yuvarlamayı hâlâ sınırsız kabul ediyor (0,8723 → "0,9"); "en fazla 1 basamak kayıp"
önerisi (TEST_PAZAR P2) meşru 2 basamaklı yuvarlamaları da reddedeceği için uygulanmadı — Faz 9'da etiketli cevap setiyle
ölçülüp karar verilmeli. Modelin kendi araç girdisi hâlâ kanıt sayılıyor (ANALIZ teknik borç; yorum kartlarında kanıt
sayılmamalı). Eski (bu düzeltmeden önce kaydedilmiş) baseline'lar yeniden hesaplanmaz; yarım iş içeren eski baseline'da
SPI yine yüksek görünür — yeniden baseline alınmalı. What-if performansı (P7) ve Brooks mentorluk yükünün beceriden bağımsız
olması (P9 ikinci yarı) bu turda ele alınmadı.

**Sıradaki adım**: Ekip turu 2 — Faz 8: JSON şemalı model çağrısı (`IChatModel` genişletmesi), proje yorumu ve senaryo
yorumu kartları (NumberGuard ile), ardından hibrit eksik iş ve analiz audit'i.

---

## 2026-10-07 (17) — Faz 7: What-if (senaryo, Monte Carlo, Brooks etkisi)

- `Planning/ResourceScheduler`: `PlanResource`'a gün bazlı kapasite pencereleri (`CapacityWindow`, çarpımsal). Pencere
  verilmezse davranış aynı (mevcut testler değişmeden geçti). `ScheduleService.BuildInputAsync` what-if için plan girdisi verir.
- `Application/WhatIf`: `ScenarioApplier` (kişi ekle [sayı, beceri, kapasite, katılım tarihi] / çıkar [sabit atamaları
  serbest kalır], kapasite değiştir, iş çıkar [alt işleriyle; öncüller ardıllara aktarılır], hedef tarih değiştir; Brooks
  pencereleri), `MonteCarlo` (üçgen dağılım ters CDF, SplitMix64 ile (tohum, tur, iş) bazlı belirlenimci sayılar, en yakın sıra
  yüzdeliği, bitiş CDF'i), `WhatIfService` (mevcut plan + en fazla 4 senaryo, P80 farkı). Ayarlar: `WhatIf` bölümü (D22).
- AI: salt okunur `simulate_what_if` aracı (düz alanlar: eklenecek kişi sayısı/beceri/kapasite/katılım, çıkarılacak kişi,
  kapasite değişimi, çıkarılacak iş, yeni hedef; adlar id'ye çevrilir). `ReadOnlyToolHandler.ExecuteAsync` artık araç
  girdisini de alıyor. Prompt chat-v8.
- Arayüz: proje panelinde **What-if** sekmesi: senaryo kartları (değişiklik ekle/sil), karşılaştırma tablosu (plan/P50/P80
  bitiş, hedef, hedefe yetişme olasılığı ikon+metin, P80 maliyet, P80 farkı), bitiş olasılığı eğrisi (seri renkleri sabit
  sırada; 5 renk doğrulayıcıdan geçti, kontrast uyarısı tablo görünümüyle karşılanıyor), hedef çizgisi, üzerine gelince değerler.
- Testler 115/115 (+10): kapasite pencereleri, Brooks (tek kişi + iki 80 s iş: yeni kişi ilk 4 haftada bitişi hızlandırmıyor
  — 20 gün; ısınmasız 10 gün), geç katılım, bağımlılık zinciri korunarak iş çıkarma, kişi çıkarma/kapasite/hedef, üçgen
  dağılım ve yüzdelik elle değerleri, belirsizliksiz Monte Carlo = deterministik plan, tekrarlanabilirlik ve P50 ≤ P80 ≤ P90,
  servis (kapsam azaltma ve süre uzatma), AI aracının ad çözümü ve bilinmeyen kişi hatası.
- Görsel kontrol (Playwright, SQLite kopya): iki senaryo (+1 backend; iş çıkar + hedef uzat), tablo, eğri, tooltip; konsol hatası yok.
  Küçük demo projesinde +1 kişi P80'i 2 iş günü geciktirdi (ısınma süresi proje süresinden uzun — Brooks etkisi).

**Varsayımlar**: Bir iş tek kişiye atanır (bölünmez); bu yüzden kişi eklemek sadece paralel işler varsa hızlandırır.
Belirsizlik yalnızca kalan eforda (bağımlılık/kişi değişikliği rastgele değil). Yeni kişinin saatlik maliyeti verilmezse ekip
ortalaması. Katılım tarihi verilmezse plan başlangıcı.

**Bilinen sorunlar**: Grafikte hedef çizgisi mevcut planın hedefidir; senaryoya özel hedef tabloda gösterilir.
Senaryolar kaydedilmiyor (sayfadan çıkınca kaybolur) — fikir havuzunda.

**Sıradaki adım**: Faz 8 — AI analiz yorumları: proje ve senaryo yorumu (EVM/ML/Monte Carlo sonuçlarını açıklayan, JSON
şemalı ve sayı doğrulamalı yorum kartı), eksik iş önerisinin LLM katmanı (hibrit), analiz cevapları için audit.

---

## 2026-10-07 (16) — Faz 6: Veri ve ML (gecikme tahmini, EVM ile karşılaştırma)

- Yeni paketler: `Microsoft.ML` 5.0.0 ve `Microsoft.ML.FastTree` 5.0.0 (Infrastructure). Gerekçe: D7 kararı ML'in uygulama
  içinde ML.NET ile yapılmasını öngörüyor; LogisticRegression/FastTree/FastForest elle yazmak hem riskli hem gereksiz.
  FastTree ayrı pakette olduğu için ikisi birden gerekli.
- `Application/Ml`: `SyntheticProjectGenerator` (haftalık ayrık simülasyon; gizli: ekip verimliliği, tahmin iyimserliği,
  kapsam oynaklığı, engel oranı; gürültü: izin/kullanılabilirlik, devir, raporlama; "%90 sendromu": ilerleme tahmini efora göre
  raporlanır, gerçek büyüklük ilerledikçe ortaya çıkar), `DelayFeatures`, `ClassificationMetrics` (doğruluk, kesinlik,
  duyarlılık, F1, AUC, MAE), `DelayExperiment` (proje bazlı ayrım, EVM kuralı ve ML aynı test satırlarında, kontrol noktası
  bazında; permütasyon önemi), `DelayFeatureBuilder` (gerçek projeden aynı özellikler; son 2 hafta hızı snapshot'lardan),
  `DelayPredictionService` (tekil; kayıtlı modeli yükler, yoksa eğitir).
- `Infrastructure/Ml`: `MlNetDelayLearner` (LbfgsLogisticRegression + normalizasyon, FastTree, FastForest + Platt kalibrasyonu,
  FastTree regresyonu), `FileDelayModelStore` (`App_Data/models`: classifier.zip, regressor.zip, model.json; veri sürümü
  değişirse yeniden eğitilir). `.gitignore`'a `App_Data/` eklendi.
- Arayüz: Durum sekmesinde **Gecikme olasılığı (ML)** kartı (olasılık, risk ikon+metin, ML ve EVM tahmini bitiş, hedef,
  model ve test AUC; eğitim aralığı dışında uyarı). Yeni **Deneyler (ML)** sayfası: yeniden eğit, sonuç tablosu
  (★ en iyi F1), özellik önemi çubukları, CSV indir. AI `get_project_status` cevabına `mlDelayPrediction` eklendi.
- İlk sonuçlar (400 proje, tohum 42, test 120 proje / 358 satır, geciken oranı %63,2):
  EVM F1 0,525 / 0,551 / 0,697 (%25/%50/%75), kesinliği yüksek (0,93–1,00) ama duyarlılığı düşük (0,37–0,54) — gecikmeyi geç
  fark ediyor. ML F1 0,77 / 0,86 / 0,87; tümünde LogisticRegression F1 0,830, AUC 0,877 (EVM AUC 0,852). Süre MAE: ML 0,191,
  EVM 0,264. En önemli göstergeler: CPI, tamamlanma, kapsam büyümesi (RQ1, RQ2 için ilk kanıt — sentetik veri sınırlılığı raporda
  belirtilmeli).
- Testler 105/105: metriklerin elle hesabı, üretecin tohumla tekrarlanabilirliği ve kontrol noktaları, gizli değişkenin
  sonuca etkisi, proje bazlı ayrımda çakışma olmaması, permütasyon öneminin kullanılan özelliği bulması, özellik oluşturucunun
  2 haftalık penceresi, ML.NET deneyinin şanstan iyi olması ve modelin kaydet/yükle sonrası aynı tahmini vermesi, durum
  servisinin tahmin servisine doğru özellikleri göndermesi.
- Görsel kontrol (Playwright, SQLite kopya): Durum kartı ve Deneyler sayfası, CSV indirme; konsol hatası yok.

**Varsayımlar**: Bloke payı gerçek projede "bloke işlerin kalan efor payı", simülasyonda "engellerle kaybolan kapasite payı"
olarak ölçülür (yakın anlam). Snapshot geçmişi 14 günden kısaysa son hız = SPI. Model ilk kullanımda eğitildiği için ilk Durum
açılışı birkaç saniye sürebilir. Eşikler: risk Orta ≥ %40, Yüksek ≥ %60 (`DelayPrediction` sabitleri); deney ayarları `Ml` bölümü.

**Bilinen sorunlar**: Model sentetik veriyle eğitildi; gerçek projelerde kalibrasyon doğrulanmadı. Küçük demo projelerinde
(ör. 4 haftalık) süre oranı tahmini uç değerlere gidebilir.

**Sıradaki adım**: Faz 7 — What-if (kişi ekle/çıkar, iş çıkar, kapasite/deadline), Monte Carlo P50/P80, Brooks etkisi.

---

## 2026-10-07 (15) — Faz 5: Takip ve analiz (EVM, AHP sağlık skoru, risk, S-eğrisi)

- `StatusUpdates` (her durum/%/saat değişikliği) ve `ProjectSnapshots` (proje × gün tekil) tabloları — migration
  `AddStatusHistoryAndSnapshots`. Faz 6 ML veri seti bu iki tablodan beslenecek.
- `Analytics/EarnedValue`: efor bazlı EVM + Earned Schedule (PV eğrisi işleri iş günlerine eşit yayar; ES doğrusal ara değer;
  tahmini bitiş = PD / SPI(t)); para birimi değerleri saat × saatlik maliyet. Baseline artık tam eforu saklar (D19).
- `Analytics/Ahp`: kuvvet yöntemiyle özvektör, λmax, CI, CR (Saaty RI). Varsayılan 5×5 matris tutarlı (CR ≈ 0,003).
- `ProjectHealth` (D20), `RiskRules`, `ProjectStatusService` (EVM + sağlık + uyarılar + günlük snapshot).
- AI: `get_project_status` salt okunur aracı; prompt chat-v6. `NumberGuard`: cevaptaki sayılar bağlam/araç sonuçlarıyla
  karşılaştırılır (Türkçe/ISO tarih, binlik/ondalık, yuvarlama, yüzde dönüşümü; ≤10 tam sayı ve WBS kodları serbest);
  bulunmayanlar cevap altında işaretlenir (D18). CLAUDE.md kuralı buna göre güncellendi.
- Arayüz: proje panelinde ilk sekme **Durum**: sağlık kartı (skor, seviye ikon+metin, AHP ağırlıklı bileşenler, CR),
  EVM kartları (SPI(t), CPI, tamamlanma, tahmini bitiş, EAC saat ve maliyet), uyarılar, S-eğrisi (PV/EV/AC, dataviz referans
  paletinin ilk üç rengi, 2px çizgi, açıklama + uç etiketleri, bugün çizgisi, üzerine gelince değerler; "Tablo olarak göster").
- Testler 98/98: EVM ve ES elle hesaplanan senaryo (PV 24, EV 22, AC 24, SPI(t) 0,92, tahmin 9 Kasım), proje bitince SPI'nin
  1'e dönüp SPI(t)'nin gecikmeyi göstermesi, AHP Saaty örneği (0,637/0,258/0,105, CR 0,033), tutarsız matris, sağlık/risk
  kuralları, NumberGuard, DB entegrasyonu (geçmiş kaydı, EVM, günlük tek snapshot).
- Görsel kontrol: test veritabanına geçmiş snapshot'lar eklenerek Durum sekmesi Playwright ile incelendi; bulunan sorunlar
  (kritik uyarıyla "İyi" görünmesi, ondalık biçimi, başlık odak çerçevesi) düzeltildi.

**Sıradaki adım**: Faz 6 — sentetik proje üreteci, haftalık snapshot veri seti, ML.NET gecikme modeli ve EVM ile karşılaştırma.

---

## 2026-10-07 (14) — Faz 4: Otomatik planlama (CPM + kaynak kısıtlı çizelge + baseline)

- `Planning/`: `WorkCalendar` (Pzt–Cum), `CriticalPath` (ileri/geri geçiş, bolluk, kritik yol, döngü tespiti),
  `ResourceScheduler` (seri çizelgeleme: öncülü bitenler arasından en küçük CPM-LS, eşitlikte öncelik; gereken beceriye sahip
  ve işi en erken bitirecek kişiye günlük kapasite aşılmadan), `ScheduleService` (DB'den girdi, önizleme, uygula, baseline).
- Girdi kuralları: sadece yaprak işler planlanır; üst işe verilen bağımlılık alt işlerine açılır; kalan efor =
  tahmini × (1 − %tamamlanma), "Bitti" = 0; iptal edilenler dışarıda; plan başlangıcı = max(proje başlangıcı, bugün).
- Varsayımlar (gün hassasiyeti): bağımlı iş, öncülü bittiği günün ertesi iş günü başlar; bir kişi aynı gün birden fazla
  bağımsız işe kalan kapasitesiyle çalışabilir; günlük kapasite = haftalık / 5; uygun kişi yoksa iş, günde HoursPerDay saatle
  kişisiz planlanır ve uyarı verilir; elle atanmış kişi korunur (beceri uymuyorsa uyarı).
- Yeni tablolar (migration `AddBaselines`): `Baselines`, `BaselineItems` (iş silinse de değişmeyen dondurulmuş plan).
- AI: `preview_schedule` (salt okunur; bitiş, sapma, kritik yol, doluluk, maliyet, uyarılar — sayılar koddan) ve
  `apply_schedule` (kart). Eksik iş kontrolü artık "eklenirse bitiş N iş günü uzar, +X saat, +Y maliyet" bilgisini de verir.
  Prompt chat-v5.
- Arayüz: proje paneli sekmeli (İşler · Plan (Gantt) · Ekip). Plan: özet kartlar, uyarılar, Gantt (kritik kırmızı, bolluklu mavi,
  kişisiz çizgili, üst iş siyah), kişi doluluk tablosu, "Planı uygula ve baseline kaydet".
- Testler 77/77: CPM ders kitabı örneği (elle hesap), çizelgeleme senaryoları (paralel kişi, yarı zamanlı kapasite, kritik öncelik,
  eksik beceri, sabit atama, maliyet/doluluk, hafta sonu), DB entegrasyonu (üst iş bağımlılığı açılımı, kalan efor, uygula + baseline,
  eksik iş etkisi, chat kartı). Arayüz Playwright ile denendi.

**Sıradaki adım**: Faz 5 — durum girişi geçmişi (StatusUpdate), snapshot, EVM (PV baseline'dan), sağlık skoru, risk uyarıları.

---

## 2026-10-07 (13) — Faz 3: Eksik iş kontrolü

Proje sahibi: "fazın sonraki adımlarına geç; proje değiştiyse gidişata göre sormadan yapıyı değiştir."
- Uyarlama: Plandaki TaskTemplate DB tablosu yerine kodda sürümlü katalog (`WorkTemplateCatalog`, templates-v1, 19 şablon,
  proje tipine göre filtre). Gerekçe: kullanıcı verisi değil sabit bilgi; test edilebilir, migration gerektirmez.
- `MissingWorkDetector`: Türkçe sadeleştirme (ı→i, ş→s…), çok kelimeli anahtar = alt metin, kısa (≤3) = tam kelime,
  diğerleri = kelime başı eşleşme. Eksik işler için "hangi mevcut işten önce bitmeli" önerisi (şablon `Before`).
- `check_missing_work` salt okunur AI aracı (`ReadOnlyToolHandler`): sonuç modele JSON döner, kart oluşturmaz; model eksikleri
  add_work_item / add_dependency kartlarıyla önerir (prompt chat-v4). Chat'te "Eksik iş var mı?" kısayolu.
- Gemini: parametresiz araçta şema gönderilmez (Gemini özelliksiz nesne şemasını kabul etmiyor).
- AI'a giden JSON artık Türkçe karakterleri kaçışlamıyor (okunaklı, daha az token).
- Değerlendirme (RQ4): eksiksiz web planından birer iş çıkarma → 15/15 doğru tespit, 0 yanlış pozitif (precision = recall = 1.0).
  Bu test sayesinde "SQL Server kurulumu"nun sunucu kurulumunu yanlışlıkla karşılaması bulunup düzeltildi.
- Varsayım: Kart kaynağı (kural mı serbest öneri mi) ayrıca etiketlenmiyor; şablon adlarıyla eşleştirilerek analiz edilebilir.
- Testler 63/63.

---

## 2026-10-07 (12) — Gemini yoğunluk (503) dayanıklılığı

Proje sahibinin lokal denemesi: anahtar çalışıyor (istek Google'a ulaştı), ancak `gemini-3.5-flash` 503 "high demand" döndü.
- Geçici hatalarda (503/429/500/504) artan beklemeyle tekrar deneme: `AI:RetryCount` (2), `AI:RetryDelayMs` (1500 → 3000).
- Hâlâ yoğunsa o tur için başka uygun Gemini modeline geçilir (önce tam "flash", yoksa "lite"); kalıcı değildir.
- Hiçbiri olmazsa Türkçe "Gemini şu an çok yoğun, birkaç dakika sonra tekrar deneyin" mesajı.
- Testler 54/54 (tekrar deneme başarısı, yoğunlukta model değişimi, alternatif yoksa mesaj).

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
