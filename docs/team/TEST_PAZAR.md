# Test ve Pazar Raporu

**Tarih:** 2026-10-08 · **Aktif faz:** FAZ 8 (AI katmanı — analiz yorumları) · **Hazırlayan:** pm-qa-market

## 1. Build / test sonucu

| Kontrol | Sonuç |
|---|---|
| `dotnet build` | 0 hata, 0 uyarı (ilk çalıştırmada 2 adet MSB3101 "state file" uyarısı çıktı; paralel derlemeden kaynaklı, tekrarında temiz) |
| `dotnet test` | **115/115 geçti** (≈5 sn) |
| Kenar durum denemesi | scratchpad'da ayrı xUnit projesi (`qa/EdgeTests.cs`, repoya eklenmedi), 20 deneme |

Kenar durum deneme çıktıları (özet):

| Deneme | Sonuç |
|---|---|
| Boş proje çizelgesi | Sorunsuz (süre 0) |
| Bağımlılık döngüsü | Anlaşılır Türkçe `BusinessRuleException` |
| Yeniden baseline (%50 bitmiş iş, durum günü 0) | PV=13,33 · EV=20 · **SPI=1,5 · SPI(t)=1,5** (yanlış iyimser) |
| Baseline'daki iş iptal edildi, diğeri %100 | BAC=80 · EV=40 · **SPI=0,5, %50 tamam** (proje aslında bitti) |
| Sabit atanan kişinin kapasitesi 0 | Atama sessizce düştü; uyarı **"'Backend' becerisine sahip kişi yok"** (yanlış neden) |
| 400 iş / 12 kişi tek çizelge | 69 ms |
| 400 iş Monte Carlo (500 tur) | **7,4 sn / senaryo** → mevcut plan + 4 senaryo ≈ 37 sn |
| What-if: alakasız beceride (Frontend) kişi ekle | Süre 50 → **53 gün** (mentorluk yükü, yeni kişi hiçbir işi yapamıyor) |
| What-if: negatif haftalık saat / ücret ile kişi ekle | **Kabul edildi** (doğrulama yok) |
| NumberGuard (12 cümle) | Aşağıda P2/P5 |

## 2. Bulunan problemler

| # | Önem | Problem | Kanıt | Önerilen çözüm |
|---|---|---|---|---|
| P1 | **Kritik** | **Sayı aklama:** NumberGuard kanıtına önceki asistan mesajları da giriyor. Bir turda uydurulan (hatta "Doğrulanamayan sayılar: 120" uyarısıyla işaretlenen) sayı, sonraki turda "doğrulanmış" sayılır. D18/CLAUDE.md §3 iddiasını boşa çıkarır; Faz 8'in açık maddesi ("cevaptaki her sayı bağlamda olmalı") doğrudan bununla ilgili. | `src/ProjectMind.Application/Chat/ChatService.cs:148` (`history.Select(h => h.Content)` rol ayrımı yok), uyarı metni `:151` aynı içeriğe ekleniyor | Kanıta yalnızca `ChatRole.User` geçmişi + bağlam + araç sonuçları girsin; asistan metinleri girmesin. Birim testi: önceki asistan cevabındaki sayı tekrar edilince işaretlenmeli. |
| P2 | **Yüksek** | NumberGuard çok **gevşek** (yanlış negatif): ×100 ve ÷100 ölçekleme ile yuvarlama her bilinen sayı için geniş bir "kabul kümesi" oluşturuyor. Bağlam büyüdükçe neredeyse her sayı geçer. | Deneme: kanıt `spi:1.2` → "120 saat" geçti; `cpi:0.87` → "87 bin TL" geçti; `x:12.4` → "12 kişi" geçti; `spi:0.87` → "0,9" geçti. `NumberGuard.cs:360-367` | ×100/÷100 eşleşmesini yalnızca cevapta `%` işareti (veya "yüzde") bitişikken uygula; yuvarlamayı en fazla 1 basamak kayıpla sınırla. RQ4/Faz 9 için yanlış negatif/pozitif oranını küçük bir etiketli cevap setiyle ölç. |
| P3 | **Yüksek** | Baseline'a girip sonra **iptal edilen iş** EVM'de sonsuza dek "kazanılmamış" kalıyor: SPI/SPI(t) ve %tamamlanma kalıcı düşük, sağlık skoru ve ML özellikleri bozuluyor, "Hedef tarih riski" yanlış kritik uyarı veriyor. | Deneme: SPI=0,5, %50 (proje bitmiş). `ProjectStatusService.cs:51-54` iptal durumunu iletmiyor; `EarnedValue.cs:74-83` | İptal edilen işleri BAC'den ve PV eğrisinden çıkar (EVM'de "descope" olarak) ve kapsam değişikliği olarak raporla; ya da iptal edilince yeni baseline öner. Elle doğrulanabilir test ekle. |
| P4 | **Yüksek** | **Yeniden baseline** sırasında kısmen bitmiş işin TAM eforu, sadece kalan efor için planlanan kısa pencereye yayılıyor → SPI/SPI(t) yapay olarak > 1, tahmini bitiş baseline'dan önce. Ayrıca planı uygulamak **Done işlerin planlanan tarihlerini bugüne çekiyor** (geçmiş Gantt kayboluyor). | Deneme: SPI=1,5. `ScheduleService.cs:68-74` (tüm aktiviteler, Done dahil, tarih yazılıyor), `:96-105` (tam efor + kalan pencere) | Baseline'da `Hours` = kalan efor ve "önceden kazanılmış" EV ayrı sabit olarak sakla (veya PV eğrisine baseline tarihinde tamamlanan kısmı başlangıç değeri olarak ekle). Done (ve başlamış işlerin başlangıç) tarihlerini plan uygularken değiştirme. D19 ile ilgili; karar notu gerekebilir. |
| P5 | Orta | NumberGuard **yanlış pozitif**: Türkçe yazılmış tarihler ("5 Aralık 2026" → 2026 işaretlenir), yıl ifadeleri, saat ("14:30" → 14, 30). Kullanıcıya gereksiz uyarı → güven kaybı (SUS testini etkiler). | Deneme çıktısı; `NumberGuard.cs:373-379` sadece `dd.MM.yyyy` ve ISO tanıyor | Ay adlı Türkçe tarih regex'i ve kanıttaki tarihlerin yıl/gün bileşenlerini bilinen sayılara ekle; `HH:mm` desenini atla. |
| P6 | Orta | LLM isteği **zaman aşımına uğrarsa** (`HttpClient.Timeout` 2 dk → `TaskCanceledException`) yakalanmıyor; UI sadece `BusinessRuleException` yakalıyor → Blazor devresi düşer ("unhandled error"), kullanıcı mesajı kaydedilmiş ama cevap yok. | `GeminiChatModel.cs:162-168` sadece `HttpRequestException`; `Infrastructure/DependencyInjection.cs:24`; `ChatPanel.razor:186` | Gemini'de `TaskCanceledException when !ct.IsCancellationRequested` → `ChatModelException("AI servisi zamanında cevap vermedi…")`. Mock modelle zaman aşımı testi. |
| P7 | Orta | What-if **performansı**: büyük projede (400 iş) senaryo başına ~7 sn; 4 senaryoda ~37 sn, iptal yok (`CancellationToken.None`), bu sürede arayüz sadece "meşgul". Chat aracı `simulate_what_if` da aynı maliyette. | Deneme; `WhatIfPanel.razor:241`, `MonteCarlo.cs:18-31` (her turda tam çizelge), `ResourceScheduler.cs:155-160` (her adımda O(n) aday taraması) | Hazır kuyruğunu öncelik kuyruğu ile tut; turları `Parallel.For` ile çalıştır (ortak rastgele sayılar zaten deterministik); bileşen kapanınca iptal için `CancellationTokenSource`. |
| P8 | Orta | Kapasitesi 0 olan **sabit atanmış kişi**: iş kişisiz planlanıyor, uyarı yanlış ("beceriye sahip kişi yok"); ataması olan işin neden kişisiz olduğu anlaşılmıyor. | Deneme; `ResourceScheduler.cs:222-226` | Ayrı uyarı: "'X' işine atanan Y'nin kapasitesi 0; iş kişisiz planlandı." İsteğe bağlı: aynı beceriye sahip başka kişiye düş. |
| P9 | Düşük | What-if "kişi ekle" haftalık saat/ücret doğrulaması yok (negatif kabul). Brooks mentorluk yükü, yeni kişinin becerisiyle ilgisi olmayan ekipten de düşülüyor (alakasız kişi eklemek süreyi uzatıyor — tartışılabilir ama açıklanmalı). | Deneme; `ScenarioApplier.cs:116-117`, `:194-199` | `WeeklyHours > 0`, `HourlyCost ≥ 0` kontrolü; mentorluk yükünü yalnızca ortak beceriye sahip kişilere uygula veya senaryo notunda açıkça yaz. |
| P10 | Düşük | Takvimde resmî tatil / kişi izni yok; `WorkCalendar.ToDate` her çağrıda gün gün ilerliyor (büyük projede gereksiz maliyet). | `WorkCalendar.cs:289-295` | Faz kapsamı dışı → Fikir Havuzu. |
| P11 | Düşük | İptal edilen / silinen işlerle ilgili hiç test yok (`tests/` içinde `Cancelled` geçen test bulunamadı). | `grep` sonucu boş | P3/P4 düzeltmeleriyle birlikte elle doğrulanabilir küçük örnek testleri ekle. |

## 3. Pazar karşılaştırması

| Ürün | Benzer özellik | Bizde var mı | Kaynak |
|---|---|---|---|
| Microsoft Planner Agent (M365 Copilot) | Hedeften görev hiyerarşisi üretme; risk altındaki işleri öne çıkarma; değişiklikleri **onay kartıyla** uygulama | Evet (chat → öneri kartı, Uygula/Vazgeç; eksik iş şablonu). Kart onayı bizde de temel tasarım | [topedia](https://blog-en.topedia.com/?p=68482), [collab365](https://spaces.collab365.com/posts/microsoft-makes-planner-agent-available-to-all-cop-iBb5Cj) |
| Microsoft Project (masaüstü) | Baseline, EVM tablosu (SPI, CPI, EAC, BAC, VAC), durum tarihi | Evet (+ Earned Schedule, efor bazlı). VAC ayrı gösterilmiyor; çoklu baseline karşılaştırması yok | [CSU MS Project eğitimi](https://pressbooks.ulib.csuohio.edu/projectmanagement2ndedition/chapter/11-5-ms-project-tutorial), [PMI forum](https://www.projectmanagement.com/discussion-topic/169689/can-ms-project-show-spi-cpi-for-baseline-1-and-greater-) |
| Jira (Atlassian Intelligence / Rovo) | AI work breakdown: büyük işi alt işlere bölme önerisi, düzenlenip onaylanır | Kısmen: şablon tabanlı eksik iş + LLM kartları; büyük işi alt işlere bölme özel aracı yok (LLM katmanı Faz 8'de açık) | [Atlassian blog](https://www.atlassian.com/blog/artificial-intelligence/ai-jira-issues), [Atlassian Community](https://answers.atlassian.com/t5/Atlassian-Intelligence-Articles/Spend-less-time-creating-work-and-more-time-moving-work-forward/ba-p/2681622) |
| LiquidPlanner (Tempo Portfolio Manager) | Aralıklı (en iyi/en kötü) tahmin, Monte Carlo ile beklenen (%50) / en geç (%90) bitiş, hedef tarih riski uyarısı | Kısmen: Monte Carlo P50/P80/P90 + hedefe yetişme olasılığı var; ama belirsizlik **iş bazında girilemiyor** (sabit üçgen 0,85/1,0/1,5) | [Tempo yardım](https://help.tempo.io/portfoliomanager/latest/introduction-to-schedule-bars), [LiquidPlanner](https://liquidplanner.com/?p=35975) |
| Forecast.app | Geçmiş veriden aşım tahmini; iyimser/tahmini/temkinli bitiş tarihleri; eksik kaynak için rol önerisi; bütçe eşik uyarısı | Kısmen: ML gecikme olasılığı (sentetik veriyle), EVM/ML bitiş, maliyet. Rol/kişi önerisi yok (what-if'te elle) | [forecast.app](https://www.forecast.app/control-projects-resources-budget-ai) |
| Asana AI (smart status) | AI ile durum raporu taslağı; risk, engel, açık soruları belirleme | Kısmen: `get_project_status` + chat yorumu; **kaydedilebilir/dışa aktarılabilir durum raporu yok** | [Asana yardım](https://help.asana.com/s/article/share-project-updates), [Zapier](https://zapier.com/blog/asana-ai/) |
| monday.com Portfolio Risk Insights | Panolardan AI ile proje başına risk listesi, günlük yenileme, risk gerekçesi ve ilgili işe bağlantı | Kısmen: kural tabanlı risk uyarıları (açıklamalı, deterministik); uyarıdan ilgili işe tıklama bağlantısı yok | [monday destek](https://support.monday.com/hc/en-us/articles/22551628427666-The-portfolio-Risk-Insights), [UC Today](https://www.uctoday.com/unified-communications/cutting-through-the-noise-what-sets-monday-coms-ai-risk-insights-apart/) |
| ClickUp Brain | AI ilerleme özeti, risk/gecikme öngörüsü, iş yüküne göre atama (pazarlama iddiası) | Kısmen: beceri + kapasiteye göre otomatik atama (deterministik), ilerleme özeti chat'te | [ClickUp](https://clickup.com/p/project-management-software/predicting-project-health), [ClickUp risk](https://clickup.com/p/ai-prompts/identifying-and-mitigating-project-risks) |

**Not:** Rakiplerin tahmin yöntemleri (model, doğruluk) yayımlanmamış; çoğu iddia pazarlama metni. Yukarıda sadece kaynakta yazan özellikler kullanıldı.

## 4. Uyum değerlendirmesi

**Farklılaştıran yönler (akademik olarak savunulabilir):**
- Rakiplerde AI riskleri/tahminleri **kara kutu**; ProjectMind'da her sayı deterministik koddan (CPM, kaynak kısıtlı SGS, EVM + Earned Schedule, AHP, ML.NET, Monte Carlo + Brooks) geliyor ve LLM yalnızca açıklıyor. Bu, NumberGuard ile görünür hale getirilen tek üründür — **ancak P1/P2 düzeltilmeden bu iddia savunulamaz.**
- ML tahmininin EVM ile aynı test setinde karşılaştırılması (RQ1) ve özellik önemi (RQ2) rakiplerde yok.
- Senaryo karşılaştırmasında ortak rastgele sayılar + Brooks etkisi: LiquidPlanner'ın Monte Carlo'sundan daha açıklanabilir.
- Öneri kartı onayı, Microsoft Planner Agent ile aynı çizgide → tasarım pazarla uyumlu.

**Eksikler (piyasada standart, bizde yok):** iş bazında belirsizlik aralığı (en iyi/en kötü), durum raporu çıktısı, tatil/izin takvimi, çoklu baseline karşılaştırması, uyarıdan ilgili işe bağlantı. Entegrasyonlar (Jira vb.), çoklu kullanıcı ve bildirimler YAPILMAYACAKLAR listesinde — önerilmez.

**Öneriler:**
- `[aktif faz]` P1 + P2 + P5: NumberGuard'ı sertleştir (asistan geçmişi kanıt değil; ölçekleme sadece `%` ile; Türkçe ay adlı tarih). Faz 8 "sayı doğrulayıcı" maddesinin kapanış koşulu olsun.
- `[aktif faz]` "Proje yorumu, senaryo yorumu" maddesi: Asana smart status benzeri yapılandırılmış (JSON şemalı) **durum özeti**: sağlık, EVM, ML, risk → LLM açıklar; her sayı araç sonucundan.
- `[aktif faz]` Eksik iş LLM katmanı: Jira work breakdown benzeri "bu işi alt işlere böl" — mevcut `add_work_item` kartları + `parentName` ile, yeni altyapı gerekmez.
- `[hata]` P3, P4 (EVM doğruluğu Faz 9 RQ1 ölçümlerini doğrudan etkiler), P6, P8 — Faz 9 demosundan önce.
- `[fikir havuzu]` İş bazında en iyi/en kötü efor tahmini (LiquidPlanner tarzı) ile Monte Carlo — mevcut "CPI'dan kalibrasyon" fikriyle birleştirilebilir.
- `[fikir havuzu]` Resmî tatil / kişi izni takvimi (P10).
- `[fikir havuzu]` Risk uyarısından ilgili işe/Gantt satırına bağlantı; VAC ve önceki baseline ile karşılaştırma.
- `[fikir havuzu]` What-if performansı (P7): öncelik kuyruğu + paralel turlar.
