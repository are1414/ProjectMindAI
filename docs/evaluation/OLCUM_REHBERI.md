# Ölçüm Rehberi (gerçek anahtarlarla kendi ölçümleriniz)

Bu rehber, PLAN.md Faz 9'daki açık madde "LLM sağlayıcı karşılaştırmasının ve RQ4 hibrit deneyinin gerçek anahtarlarla
çalıştırılması" ile RQ1 çok tohumlu ölçümün kendi bilgisayarınızda tekrarlanması için adım adım yol gösterir.
Kodu değiştirmeniz gerekmez; her şey uygulama arayüzünden yapılır.

> Bu belgede **sonuç yoktur.** Süre ve maliyet notları **kaba tahmindir**, ölçülmüş değer değildir; gerçek değerleri
> CSV'lerden siz hesaplayıp `____` alanlarına yazın.

---

## 0. Önerilen sıra ve genel hazırlık

1. **Önce ölçümler, sonra kullanıcı testi.** Sağlayıcı karşılaştırması demo projesinde çalışır ve sonuçları
   veritabanında (`EvaluationRuns`) saklar. Kullanıcı testi için ayrı/temiz veritabanına geçecekseniz
   (`KULLANICI_TESTI.md` §3.1) önce bu rehberdeki tüm CSV'leri indirin.
2. Uygulama açık: `dotnet run --project src/ProjectMind.Web`.
3. Demo projesi var olmalı: ana sayfa → **Demo projesini oluştur** (sağlayıcı karşılaştırması demo yoksa kendisi de
   oluşturur). Ölçüm koşucusu demo projesinin bugünkü durumunu/snapshot'ını günceller (Durum sekmesini açmakla aynı).
4. **Anahtarlar** (sol menü **⚙ Ayarlar**). Ayarlar sayfası her **Kaydet**'te seçilen sağlayıcıyı aktif yapar; diğer
   sağlayıcının anahtarı dosyada korunur. Bu yüzden:
   - Sağlayıcı "Anthropic Claude" → anahtar → **Kaydet**.
   - Sağlayıcı "Google Gemini" → anahtar → **Kaydet** (en son kaydedilen aktif kalır; varsayılan sağlayıcı Gemini).
   - **Teşhis** kutusunda `AI:Gemini:ApiKey` ve `AI:Claude:ApiKey` değerlerinin ikisinin de "YOK" olmadığını kontrol edin.
   - Model alanı boşsa `appsettings.json` → `AI` varsayılanları kullanılır (Gemini `gemini-3.5-flash`, Claude
     `claude-opus-5-5`). Daha ucuz bir Claude modeliyle ölçmek isterseniz Model alanına yazın ve rapora model adını
     not edin. Kullanılan modeller: Gemini `____` · Claude `____`.
5. Ölçüm sırasında demo projesinin sohbetini kullanmayın (koşu kendi geçici sohbet oturumlarını açar ve sonunda siler).
6. Her CSV'yi indirdikten sonra repo dışında bir klasörde saklayın (ör. `rapor/ekler/olcumler/`), dosya adını
   değiştirmeyin (tarih-saat damgası koşuyu tanımlar).

## 1. LLM sağlayıcı karşılaştırması (`eval-v1`) — `/evaluation`

**Ne ölçer:** sabit 8 görev (proje yorumu, iki senaryolu senaryo yorumu (+1 Backend / +1 Frontend), hibrit eksik iş,
5 sohbet sorusu: durum ×2, plan, eksik iş, what-if) her sağlayıcıyla n kez. Ölçütler: hata oranı, şema geçerlilik,
doğrulanamayan sayılı cevap payı ve cevap başına sayı, süre ort. ± SS, token ortalamaları, beklenen araç oranı.
Kartlar uygulanmaz, otomatik reddedilir ve RQ4 kabul oranına girmez.

**Adımlar**

1. Sol menü **📊 Değerlendirme** → aşağıda **LLM sağlayıcı karşılaştırması (eval-v1)** bölümü.
2. Sağlayıcı listesinde iki satırda da **✓** ve model adı görünmeli. "—" ile başlayan satır atlanacak demektir
   (sebebi yanında yazar, ör. anahtar yok).
3. **Tekrar** alanı: varsayılan `3` (en çok 10). Rapor için 3 yeterli kabul edildi; bütçe izin verirse 5.
4. **Karşılaştırmayı çalıştır**. İlerleme metni butonun yanında görünür; gerekirse **İptal et** (koşu `Cancelled`
   olarak kalır, tamamlanan çağrılar kayıtlıdır). Sayfadan çıkmayın; çıkarsanız koşu iptal edilir.
5. Bitince tablo görünür. **Koşu** açılır listesinde koşu numarası, tarih, durum ve çağrı sayısı vardır.
   Not satırında atlanan sağlayıcı ya da hata notu varsa rapora yazın.
6. **CSV indir** → `saglayici-karsilastirma-{koşuNo}-yyyyMMdd-HHmm.csv`
   (kolonlar: `run_id, task_set, provider, models, task, calls, errors, error_rate, schema_checked, schema_valid,
   schema_valid_rate, answered, with_unverified, unverified_share, unverified_per_answer, mean_duration_ms, …`).
7. Aynı sayfada **RQ4** bölümü → **AI kayıtları CSV** → `rq4-ai-kayitlari-*.csv`. Koşunun ham çağrıları burada
   `evaluation_run_id` = koşu no, `evaluation_provider`, `evaluation_task`, `input_tokens`, `output_tokens` ile yer alır
   (yorum metni ve bağlam CSV'de yoktur).

**Kaba tahmin (doğrulanmadı):**

- Çağrı sayısı: sağlayıcı başına 8 görev × tekrar. Yorum ve eksik iş görevleri genelde 1 çağrı; sohbet görevleri araç
  turu nedeniyle en az 2 çağrı yapar → tekrar = 3 iken sağlayıcı başına kabaca **40 civarı** model çağrısı.
- Süre: çağrı başına birkaç saniye ile birkaç on saniye arası → sağlayıcı başına kabaca **5–20 dakika**.
- Maliyet: önceden kestirilemez; koşudan sonra hesaplayın:
  `maliyet ≈ Σ input_tokens × (giriş fiyatı / 1M) + Σ output_tokens × (çıkış fiyatı / 1M)`.
  Fiyatı sağlayıcının o günkü fiyat sayfasından alın ve tarihiyle birlikte not edin. Gemini çıkış token'ı düşünme
  token'larını da içerir; Claude önbellek token'ları ayrı tutulmaz. Gemini ücretsiz katmanında hız sınırı (rate limit)
  hatası alınırsa hata oranına yansır — rapora "ücretsiz katman" olarak yazın.
- Ölçülen: Gemini süre `____` dk, token giriş/çıkış `____`/`____`, maliyet `____` · Claude süre `____` dk,
  token `____`/`____`, maliyet `____` (fiyat tarihi `____`).

**Raporda yeri (öneri):** Bulgular → "LLM sağlayıcı karşılaştırması" tablosu: satır = sağlayıcı × görev (ya da sağlayıcı
toplamı), sütunlar = hata oranı, şema geçerlilik, doğrulanamayan sayılı cevap payı, süre ort. ± SS, token ort.,
beklenen araç oranı. Yöntem bölümünde görev seti sürümü (`eval-v1`), tekrar sayısı, model adları ve tarih.
RQ4'ün "LLM açıklamaları güvenilir mi" kısmının nicel kanıtı ve NumberGuard'ın (D18/D23) etkinliğinin göstergesidir.

## 2. Tohumlanmış eksik iş deneyi (`recall-v1`, RQ4) — `/evaluation`

**Ne ölçer:** demo iş listesinden **bellekte** 4 şablon işi + 4 projeye özgü iş çıkarılır (tohum 11; veritabanı
değişmez, kart oluşmaz). Kural-yalnız mod (deterministik) ve hibrit mod (ayarlı sağlayıcı, 3 tekrar) bu işleri ne kadar
geri buluyor: recall (şablon / özgü / tümü) ve precision (alt sınır).

**Adımlar**

1. **📊 Değerlendirme** → **RQ4 · Hibrit öneriler ve AI açıklamaları** bölümünün altında **Tohumlanmış eksik iş deneyi
   (recall-v1): kural-yalnız vs hibrit**.
2. **Deneyi çalıştır**. Hibrit kısım **o an seçili sağlayıcıyla** çalışır (sohbet başlığındaki model).
   Mock modda hibrit satırlar atlanır, yalnız kural satırı gelir.
3. Tabloda "Kural-yalnız", "Hibrit #1…#3" ve "Hibrit ort. ± SS" satırları; üstte "Çıkarılan işler" listesi.
   Bir hibrit satırında parantez içinde durum (ör. hata) varsa o tekrar ortalamaya girmez; rapora yazın.
4. **CSV indir** → `rq4-eksik-is-bulma-yyyyMMdd-HHmm.csv`
   (kolonlar: `experiment, seed, mode, repeat, llm_status, removed_template, recovered_template, recall_template,
   removed_other, recovered_other, recall_other, recall_all, suggestions, matched_suggestions, precision, …`).
5. **İkinci sağlayıcı için:** **⚙ Ayarlar** → diğer sağlayıcıyı seçin → anahtarını yeniden yapıştırın (Kaydet butonu
   anahtar alanı boşken pasiftir) → **Kaydet** → "Şu an kullanılan" güncellenince 2–4. adımları tekrarlayın.
   Bitince varsayılan sağlayıcıya geri dönün.

**Beklenen (kod tasarımından, ölçüm değil):** kural-yalnız satırı deterministiktir; PROGRESS (24)'te demo üzerinde
şablon recall 1, özgü recall 0 (tanım gereği: şablonda olmayan işi kural bulamaz), toplam 0,5 kaydedildi. Hibrit
satırlarının özgü recall'ı ve precision'ı modele göre değişir; asıl ölçülecek budur.

**Kaba tahmin:** sağlayıcı başına 3 LLM çağrısı; birkaç dakikadan kısa; maliyeti sağlayıcı karşılaştırmasının küçük bir
kısmı. Bu çağrılar da `rq4-ai-kayitlari` CSV'sinde koşuya etiketli görünür.

**Raporda yeri (öneri):** Bulgular → RQ4 "Hibrit eksik iş tespiti plan bütünlüğünü artırıyor mu?" — tablo: mod (kural /
hibrit-Gemini / hibrit-Claude) × recall şablon / özgü / tümü, öneri sayısı, precision; hibrit için ort. ± SS (n = 3).
Sınırlılıklar: precision alt sınırdır (çıkarılmamış ama yararlı öneriler yanlış sayılır), eşleşme ad benzerliğine dayanır,
tek demo projesi ve tek tohum.

## 3. RQ1 çok tohumlu deney ve erken uyarı — `/experiments`

API anahtarı gerekmez, ücretsizdir (yalnız ML.NET, sentetik veri). Sonuçlar bellekte tutulur; sayfadan çıkmadan
CSV'leri indirin.

**3.1 Tek tohumlu deney (RQ1 + RQ2 özellik önemi)**

1. Sol menü **🧪 Deneyler (ML)** → **Deneyi çalıştır ve modeli yeniden eğit**. Bu buton üretim modelini de yeniden
   eğitir (aynı ayar ve tohumla aynı model beklenir); bu yüzden kullanıcı testinden **önce** yapın.
2. Sonuç bölümleri: **Sonuçlar (test seti)**, **Erken uyarı (RQ1, test seti)**, **Özellik önemi (…, permütasyon)**.
3. **CSV indir** → `gecikme-deneyi-yyyyMMdd-HHmm.csv` (metrikler, erken uyarı bölümü, özellik önemi).
4. Aynı uygulama oturumunda **📊 Değerlendirme** → **RQ2 · Gecikmeyi açıklayan faktörler** → **CSV indir** →
   `rq2-ozellik-onemi-*.csv` (RQ2 tablosu yalnız bu oturumda deney çalıştırıldıysa doludur).

**3.2 Çok tohumlu sağlamlık analizi**

1. **Deneyler (ML)** → **Çok tohumlu sağlamlık analizi (RQ1/RQ2)** → **Tohum sayısı** (varsayılan 5, 2–30).
   Rapor için 5 (PROGRESS (24) ile aynı); daha dar SS için 10.
2. **Çok tohumlu deneyi çalıştır** (arka planda, **İptal et** ile durdurulabilir; üretim modeli değişmez).
3. **CSV indir** → `cok-tohumlu-deney-yyyyMMdd-HHmm.csv` (F1/AUC/doğruluk ve erken uyarı ölçütleri ort. ± SS,
   "Seçilen ML" satırı).

**Kaba tahmin:** her tohum 400 sentetik projeyle bir tam eğitim/test turu → 5 tohum için makineye bağlı olarak birkaç
dakika (ölçülmedi). Ölçülen süre: `____`.

**Kontrol:** Varsayılan ayarlarla (400 proje, tohum 42–46, eşik 0,5, tolerans %10) sonuçların PROGRESS (24)'teki
değerlerle aynı ya da çok yakın çıkması beklenir (örn. tüm kontrol noktalarında F1 EVM 0,665 ± 0,058 vs seçilen ML
0,873 ± 0,027). Belirgin fark varsa ayarları (`Ml` bölümü) ve uygulama sürümünü (commit) not edin; rapora kendi
koşunuzun değerini yazın.

**Raporda yeri (öneri):** Bulgular → RQ1: (a) tek tohumlu test seti metrik tablosu (EVM vs LR / FastTree / FastForest);
(b) erken uyarı tablosu (yakalanan pay, ilk işaret noktası, uyarı süresi, yanlış alarm); (c) çok tohumlu ort. ± SS tablosu
— asıl iddia (c)'ye dayanır. RQ2: özellik önemi çubuk grafiği (AUC düşüşü). Sınırlılıklar: sentetik veri (D21), kontrol
noktaları yalnız %25/%50/%75, 0,5 eşiğinde ML'nin yüksek yanlış alarmı (kalibrasyon). İsteğe bağlı ek: demo projesinin
`rq1-evm-ml-zaman-serisi` CSV'si ile "tek projede EVM ve ML tahmininin günlere göre seyri" grafiği (gösterim amaçlı,
doğrulama değil).

## 4. Kayıt tablosu (doldurun)

| Ölçüm | Tarih | Sağlayıcı / model | Ayar (tekrar / tohum) | CSV dosya adı | Not |
|---|---|---|---|---|---|
| Sağlayıcı karşılaştırması (eval-v1) | | | tekrar = | | koşu no: |
| AI kayıtları (koşu çağrıları) | | | | | |
| Recall deneyi — Gemini | | | tohum 11, 3 tekrar | | |
| Recall deneyi — Claude | | | tohum 11, 3 tekrar | | |
| Tek tohumlu deney (RQ1/RQ2) | | — | tohum 42 | | |
| RQ2 özellik önemi | | — | | | |
| Çok tohumlu deney | | — | 5 tohum | | |

Bitince PLAN.md Faz 9'daki "gerçek anahtarlarla çalıştırılması" maddesini işaretleyin ve PROGRESS.md'ye sonuç özetini
(sayılarla, CSV adlarıyla) ekleyin.
