# Kullanıcı Testi Protokolü (Faz 9)

Bu belge, ProjectMind AI'nın 5–10 katılımcıyla yapılacak kullanıcı testinin uygulanmaya hazır protokolüdür. Test demo
projesi ("Mobile Banking Modernization") üzerinde yapılır. Arayüzdeki adlar (buton, sekme, sayfa) uygulamadaki metinle
birebir yazılmıştır; uygulama değişirse bu belge de güncellenmelidir.

> Durum: **protokol hazır, test oturumları henüz yapılmadı.** Aşağıdaki tablolarda sonuç yoktur; doldurulacak alanlar
> `____` ile işaretlidir.

---

## 1. Amaç

| Ölçülen | Araştırma sorusu | Veri kaynağı |
|---|---|---|
| What-if (Monte Carlo) karşılaştırmasının karar vermeye katkısı | **RQ3** | Görev 3 gözlemi, RQ3_1 / RQ3_2 Likert maddeleri, `rq3-whatif-calistirmalari` CSV |
| Hibrit (şablon + AI) eksik iş önerilerinin kabulü; AI açıklamalarının anlaşılırlığı | **RQ4** | Görev 4–5 gözlemi, öneri kartı kararları (kaynağa göre kabul oranı), "Bu açıklama anlaşılır mıydı?" geri bildirimi, RQ4_1 / RQ4_2 Likert maddeleri |
| Genel kullanılabilirlik | **SUS** | 10 maddelik SUS (uygulama içi anket) |

RQ1 (ML vs EVM) ve RQ2 (faktörler) bu testin konusu değildir; sentetik deneyle ölçülür (bkz. `OLCUM_REHBERI.md`).
Katılımcıya Durum sekmesindeki ML olasılığı yalnız bilgi olarak gösterilir.

## 2. Katılımcı profili

- **Sayı:** 5–10 (hedef 8). Nielsen'in 5 kullanıcı önerisi sorun bulmak için yeterlidir; SUS ortalaması için n küçüktür,
  sonuçlar **betimsel** raporlanır.
- **Dahil etme:** yazılım projesinde proje yöneticisi, takım lideri, analist veya en az 1 yıl deneyimli geliştirici;
  ya da proje yönetimi dersi almış mühendislik yönetimi / bilgisayar mühendisliği yüksek lisans öğrencisi.
- **Tercihen:** SPI/CPI, kritik yol gibi kavramlara aşinalık (zorunlu değil; ekranda açıklamalar var).
- **Hariç:** projenin geliştirilmesine katılmış kişiler, sistemi daha önce kullanmış kişiler.
- **Kaydedilecek profil (anonim, gözlem formunda):** rol kategorisi (PM / lider / geliştirici / öğrenci), deneyim yılı
  aralığı (0–2 / 3–5 / 6+), AI sohbet aracı kullanım sıklığı (hiç / ara sıra / her gün). Ad, kurum, e-posta yazılmaz.

## 3. Kurulum kontrol listesi (moderatör, test gününden önce)

### 3.1 Bir kez

- [ ] SQL Server çalışıyor; `dotnet run --project src/ProjectMind.Web` ile uygulama açılıyor.
- [ ] **Test için temiz veritabanı.** Öneri kartı kararları, what-if kayıtları ve AI kayıtları proje silinse de kalır
  (kabul oranı verisi korunur, D28/D30). Geliştirme ve deneme verisi teste karışmasın diye iki yoldan biri seçilir:
  - **A (önerilen): ayrı test veritabanı.** `src/ProjectMind.Web/appsettings.Local.json` dosyasına (git'e girmez) şu
    bölüm eklenir; mevcut `AI` bölümü korunur. Uygulama yeniden başlatılır; geliştirme ortamında migration'lar açılışta
    uygulanır ve boş veritabanı oluşur.
    ```json
    "ConnectionStrings": { "Default": "Data Source=.;Initial Catalog=ProjectMindAIDb_KullaniciTesti;Integrated Security=True;Encrypt=False;Connect Timeout=60" }
    ```
  - **B: mevcut veritabanını sıfırlamak.** Önce `/evaluation` sayfasındaki gerekli CSV'ler (özellikle sağlayıcı
    karşılaştırması) indirilir, sonra `dotnet ef database drop -p src/ProjectMind.Infrastructure -s src/ProjectMind.Web`
    ve `dotnet ef database update ...` çalıştırılır.
- [ ] **Gemini anahtarı:** sol menü **⚙ Ayarlar** → Sağlayıcı "Google Gemini" → API anahtarı → **Kaydet**. "Şu an
  kullanılan" satırında Gemini modelinin göründüğü, **Teşhis** kutusunda `AI:Gemini:ApiKey` değerinin "YOK" olmadığı
  kontrol edilir. Anahtar yoksa AI yorumu ve AI kaynaklı eksik iş önerileri çıkmaz (Mock) → test yapılmaz.
- [ ] Testte kullanılacak model adı not edilir (sohbet panelinin başlığında "AI Asistan" yanında görünür): `____`
- [ ] ML modeli eğitilmiş olmalı: bir kez **Demo projesini oluştur** denenir (ilk seferde model eğitimi birkaç saniye
  sürer), sonra demo silinir (aşağıdaki "her katılımcıdan önce" adımı).
- [ ] Ekran kaydı yapılacaksa yazılım hazır ve yalnız uygulama penceresini kaydedecek şekilde ayarlı (katılımcının
  yüzü ve sesi kaydedilmez; ses kaydı gerekiyorsa onam metnine ayrıca eklenmeli).
- [ ] Yazdırılmış: bu protokolün görev kartları (bölüm 6), gözlem formu (bölüm 7), onam metni (bölüm 5).
- [ ] Kronometre / saat.

### 3.2 Her katılımcıdan önce (≈ 3 dk)

Önceki katılımcının kabul ettiği kartlar demo projesine iş ekler; sonraki katılımcı aynı başlangıç durumunu görmelidir.

- [ ] Sol menüde "Demo · Mobile Banking Modernization" sohbeti varsa açılır → sohbet panelinde **Projeyi sil** → onay.
  (Karara bağlanmış öneriler kayıtta kalır; yalnız proje ve sohbet silinir.)
- [ ] Ana sayfa (`/`) → **Demo projesini oluştur** → sohbet açılır. "Demo projesi zaten var" hatası çıkarsa önceki
  adım tamamlanmamıştır.
- [ ] Proje panelinde **Durum** sekmesi açık; ekrandaki değerler gözlem formunun "başlangıç değerleri" satırına
  yazılır: SPI(t) `____`, Tamamlanma `%____`, Tahmini bitiş `____`, Hedef bitiş `____`, Proje sağlığı `____`,
  Gecikme olasılığı (ML) `%____`. (Demo tarihleri bugüne göre üretilir; README'deki örnek değerler SPI(t) ≈ 0,88,
  sağlık "Dikkat", bir bloke iş, ≈ %10 kapsam artışı. Esas alınacak değer o gün ekranda görülendir.)
- [ ] Sohbette hiç mesaj olmadığı kontrol edilir; tarayıcı yakınlaştırması %100; başka sekme açık değil.
- [ ] Oturumun başlangıç saati gözlem formuna yazılır (katılımcının kayıtlarını CSV'deki `created_at` ile eşlemek için).

## 4. Moderatör metni

Okunacak metin (kelimesi kelimesine okunması önerilir; parantez içleri moderatör notudur):

> "Merhaba, katıldığınız için teşekkürler. Bugün yazılım projelerini planlamaya ve izlemeye yardımcı olan bir sistemi
> deneyeceksiniz. Test edilen sizsiniz değil, sistem. Yapamadığınız ya da zorlandığınız her şey bizim için değerli bir
> bulgu. Size beş kısa görev vereceğim. Görevler sırasında aklınızdan geçenleri sesli söylerseniz çok yardımcı olur.
> Ben müdahale etmeyeceğim; takılırsanız önce kendiniz denemenizi isterim. Bir görevden vazgeçmek isterseniz
> 'geçiyorum' demeniz yeterli. Ekranda gördüğünüz proje gerçek değil, örnek bir mobil bankacılık projesi; yaptığınız
> değişiklikler kimseyi etkilemez. Oturum yaklaşık 40–45 dakika sürecek ve sonunda kısa bir anket var."

(Onam metni okunur ve sözlü onay alınır → bölüm 5.)

> "Siz bu projenin proje yöneticisisiniz. Proje 9 hafta önce başladı. Sol tarafta projenin bilgileri ve sekmeleri,
> sağ tarafta yapay zekâ asistanı var. Asistan bir şey değiştirmek istediğinde size bir öneri kartı gösterir; siz
> 'Uygula' demedikçe hiçbir şey değişmez. Hazırsanız ilk görevle başlayalım."

Moderatör kuralları:

- Görev kartını verir, okur, kronometreyi başlatır. Katılımcı "bitti" deyince ya da süre sınırının 2 katı dolunca durdurur.
- İpucu vermez. Katılımcı 2 dakika tamamen takılırsa tek bir **standart ipucu** verilebilir (görev kartında yazılı);
  ipucu verilen görev "yardımla başarılı" sayılır.
- "Doğru mu yaptım?" sorusuna: "Sizce nasıl? Devam edebilirsiniz."
- AI'nın cevabı uzun sürerse ("Düşünüyor…") bekleme süresi görev süresine dahildir; gözlem formuna not edilir.
- Katılımcı **Plan (Gantt)** sekmesinde **Planı uygula ve baseline kaydet**'e basarsa engellenmez ama not edilir (yeni
  baseline Durum değerlerini değiştirir); o katılımcının G1/G5 verisi bu notla yorumlanır.
- Görev sonrası tek soru: "Bu görev ne kadar kolaydı? 1 (çok zor) – 7 (çok kolay)" (SEQ; gözlem formuna yazılır).

## 5. Bilgilendirilmiş onam metni

Uygulama içi anket (`/evaluation/survey`) aşağıdaki metni gösterir ve onay kutusu işaretlenmeden cevap kaydetmez
(metin `SurveyQuestionnaire.ConsentText`):

> Bu anket, ProjectMind AI'nın kullanılabilirliğini ve karar desteğini değerlendiren akademik bir çalışma içindir.
> Ad, e-posta veya başka bir kişisel veri istenmez; yalnız size verilen katılımcı kodu (ör. P01), cevaplarınız ve
> gönderim zamanı saklanır. Katılım gönüllüdür; istediğiniz an bırakabilir, kodunuzu bildirerek cevaplarınızın
> silinmesini isteyebilirsiniz. Sonuçlar yalnız toplu ve anonim olarak raporlanır.

Oturum başında sözlü olarak ek bilgi verilir (KVKK'ya uygun, veri minimizasyonu):

- Katılımcı kodu sırayla verilir: **P01, P02, …** Kod ile kişi arasında **yazılı bir eşleşme listesi tutulmaz**
  (katılımcı geri çekilmek isterse kodunu kendisi bildirir; bunun için kod kendisine küçük bir kartla verilir).
- Gözlem formunda ad, kurum, yaş, cinsiyet gibi bilgi yoktur; yalnız bölüm 2'deki kategorik profil.
- Ekran kaydı (yapılırsa) yalnız uygulama penceresini içerir; analiz bittikten sonra silinir. Ses/yüz kaydı yapılmaz.
- Uygulama verisi (kartlar, what-if çalıştırmaları, AI kayıtları) yerel bilgisayardaki test veritabanında tutulur;
  dışa aktarılan CSV'lerde kişisel veri yoktur. AI'ya gönderilen bilgi yalnız demo projesinin özetidir; katılımcının
  sohbete kişisel bilgi yazmaması istenir.
- Çekilme: `/evaluation` → "Kullanılabilirlik (SUS)" tablosunda ilgili kodun satırında **Sil** → **Silmeyi onayla**.

Sözlü onay alındı mı: gözlem formunda işaretlenir.

## 6. Görevler

Sıralama: kart kabulü projeye iş eklediği için what-if görevi eksik iş görevinden **önce** yapılır; böylece her
katılımcı what-if'i aynı başlangıç planıyla çalıştırır. Hedef süreler pilot oturumla ayarlanmalıdır (pilot sonrası
güncellenen süre: `____`).

### Görev 1 — Projenin durumunu okumak (Durum sekmesi)

- **Katılımcıya okunacak kart:** "Yöneticiniz projenin neden geciktiğini soruyor. Sadece **Durum** sekmesine bakarak
  projenin plana göre nerede olduğunu ve gecikmenin nedenlerini bana anlatın. Bu görevde AI asistanı ve 'AI yorumu
  üret' butonunu kullanmayın."
- **Hedef süre:** 4 dk (sınır 8 dk).
- **Başarı ölçütü:** katılımcı ekrandaki bilgiye dayanarak aşağıdakilerden **en az ikisini** doğru söyler:
  1. Takvim performansı **SPI(t)** 1'in altında ("plandan geride").
  2. **Tahmini bitiş** hedef bitişe göre geç / hedef tarih riski uyarısı.
  3. **Uyarılar** listesinde bloke iş (kart işlemleri ve çekirdek bankacılık bağlantısı).
  4. Kapsam büyümesi (baseline sonrası eklenen işler) ya da planlanan bitişi geçmiş işler.
- **Kısmi başarı:** yalnız bir neden ya da nedenler doğru ama bir göstergeyi yanlış yorumluyor (ör. CPI'yı takvim sanmak).
- **Standart ipucu:** "Sayfanın aşağısındaki **Uyarılar** bölümüne de bakabilirsiniz."
- **Gözlem notu:** hangi kartı ilk okudu, SPI(t)/CPI'yı ayırt edebildi mi, "Gecikme olasılığı (ML)" kartını nasıl yorumladı,
  S-eğrisinde **Tablo olarak göster**'i kullandı mı.

### Görev 2 — AI asistanına durum sorusu sormak

- **Kart:** "Sağdaki AI asistanına projenin zamanında bitip bitmeyeceğini ve nedenini kendi cümlelerinizle sorun.
  Cevaptaki bilgilerin Durum sekmesiyle uyuşup uyuşmadığını kontrol edin."
- **Örnek soru (katılımcı kendi cümlesini kurmazsa okunur):** "Proje hedef tarihe yetişir mi? Neden?"
- **Hedef süre:** 3 dk (sınır 6 dk; AI bekleme süresi dahil).
- **Başarı ölçütü:** (a) soru gönderildi ve AI cevap verdi; (b) katılımcı cevaptaki en az bir sayıyı/tarihi (ör. SPI(t),
  tahmini bitiş) Durum sekmesindekiyle karşılaştırıp "uyuşuyor / uyuşmuyor" diyebildi. Cevabın altında
  "Doğrulanamayan sayılar" uyarısı çıkarsa katılımcının bunu fark edip etmediği ayrıca not edilir.
- **Standart ipucu:** "Mesaj kutusu sağ alttadır: 'Projenizle ilgili bir şey yazın…'"
- **Gözlem notu:** cevap süresi (yaklaşık sn), AI'nın öneri kartı üretip üretmediği (durum sorusunda beklenmez; üretirse
  katılımcının ne yaptığı), güven ifadesi ("buna güvenirim / emin değilim").

### Görev 3 — What-if senaryosu: +1 Backend geliştirici

- **Kart:** "Ekibe haftada 40 saat çalışan bir **Backend** geliştirici eklemeyi düşünüyorsunuz. **What-if** sekmesinde
  bu senaryoyu oluşturun, mevcut planla karşılaştırın ve **P80 bitiş** tarihine göre bu kişiyi almaya değer mi, karar verin."
- **Beklenen yol:** **What-if** sekmesi → "Senaryo 1" kartında değişiklik türü **Kişi ekle**, `1` kişi, beceri **Backend**,
  `40` saat/hafta (bunlar varsayılan değerlerdir; katılımcı fark etmeyebilir — not edilir) → istenirse senaryo adı
  "+1 Backend" yapılır → **Simüle et ve karşılaştır** → **Karşılaştırma** tablosunda "Mevcut plan" satırı ile senaryo
  satırının **P80 bitiş**, **Hedefe yetişme** ve **P80 farkı (iş günü)** sütunları karşılaştırılır.
- **Hedef süre:** 5 dk (sınır 10 dk).
- **Başarı ölçütü:** (a) karşılaştırma çalıştırıldı (what-if kaydına `Panel` kaynağıyla düşer); (b) katılımcı iki
  satırın P80 bitişini ya da P80 farkını doğru okudu; (c) gerekçeli bir karar söyledi ("değer / değmez, çünkü …").
  Kararın yönü başarıyı etkilemez.
- **İsteğe bağlı uzatma (süre kalırsa):** **+ Senaryo** ile ikinci senaryo (ör. Frontend ya da bir işi çıkarma) eklenip
  yeniden karşılaştırılır; "Bitiş tarihi olasılığı" grafiğinin okunup okunmadığı not edilir.
- **Standart ipucu:** "Sonuçlar butona bastıktan sonra **Karşılaştırma** tablosunda görünür."
- **Gözlem notu:** Brooks etkisini (yeni kişinin ısınma süresi) fark etti mi; "Hedefe yetişme" yüzdesini nasıl yorumladı;
  bu görevde **AI yorumu üret**'e basarsa not edilir (Görev 5'in What-if varyantı sayılmaz).

### Görev 4 — "Eksik iş var mı?" ve kartları tek tek değerlendirmek

- **Kart:** "Planınızda unutulmuş iş olup olmadığını öğrenmek istiyorsunuz. Sohbetteki **Eksik iş var mı?** kısayolunu
  kullanın. Gelen her öneri kartını **tek tek** okuyun ve her biri için **Uygula** ya da **Vazgeç** deyin. Lütfen toplu
  uygulama butonunu kullanmayın. Her karar için kısaca nedenini sesli söyleyin."
- **Beklenen yol:** mesaj kutusunun üstündeki **Eksik iş var mı?** çipi → kartlar gelir; kaynak rozeti **Şablon**
  (kural katmanı) ya da **AI** (LLM katmanı) → her kart için **Uygula** / **Vazgeç**. Demo bilerek CI/CD, kullanıcı kabul
  testi ve dokümantasyon işlerini eksik bırakır; şablon kartlarının bunları içermesi beklenir. AI kartlarının içeriği
  modele göre değişir.
- **Yasak / not edilecek:** "**Bekleyen n öneriyi uygula**" butonu. Katılımcı yine de basarsa engellenmez; bu kartlar
  kayıtta `DecidedInBulk` olarak işaretlenir ve analizde ayrı tutulur (bölüm 10).
- **Hedef süre:** 5 dk (sınır 10 dk).
- **Başarı ölçütü:** bekleyen kart kalmadı ve kartların hepsi tek tek karara bağlandı; katılımcı en az bir kart için
  gerekçe söyledi.
- **Standart ipucu:** "Kısayol, mesaj kutusunun hemen üstündedir."
- **Gözlem notu (gözlem formundaki kart tablosu):** her kart için rozet (Şablon/AI), iş adı, karar, söylenen gerekçe.
  Katılımcının rozetleri fark edip etmediği; AI önerisine şablon önerisinden farklı güvenip güvenmediği.

### Görev 5 — "AI yorumu üret" ve anlaşılırlık

- **Kart:** "**Durum** sekmesine dönün. **AI yorumu üret** butonuna basın, yorumu okuyun ve Görev 1'de sizin
  çıkardığınız nedenlerle karşılaştırın. Sonra kartın altındaki **Bu açıklama anlaşılır mıydı?** sorusuna cevap verin
  ve 1–5 arasında bir puan seçin."
- **Beklenen yol:** **Durum** → **AI yorumu** bölümü → **AI yorumu üret** → "Önemli bulgular", "Önerilen aksiyonlar",
  "Varsayımlar ve sınırlar" okunur → **Evet** / **Hayır** → "Puan (isteğe bağlı)" 1–5 → "Kaydedildi, teşekkürler."
- **Hedef süre:** 3 dk (sınır 6 dk; AI bekleme dahil).
- **Başarı ölçütü:** yorum üretildi, Evet/Hayır ve puan verildi; katılımcı yorumun Görev 1'deki kendi değerlendirmesiyle
  uyuşup uyuşmadığını bir cümleyle söyledi.
- **Not:** Görev 4'te kabul edilen kartlar proje verisini değiştirdiği için yorum Görev 1'deki ekrandan biraz farklı
  sayılar içerebilir; bu beklenen davranıştır. Yorumun altında "▲ Doğrulanamayan sayılar" uyarısı çıkarsa not edilir.
- **Gözlem notu:** okuma süresi, katılımcının yorumdaki bir öneriye itirazı, "Doğrulanamayan sayılar" uyarısına tepkisi.

### Oturum sonu

1. Katılımcıya anket açılır: `/evaluation/survey` (ya da `/evaluation` → **Anketi aç (katılımcı formu)**).
2. Katılımcı onam metnini okur, **Okudum, gönüllü olarak katılmayı kabul ediyorum.** kutusunu işaretler, **Katılımcı
   kodu** alanına kendi kodunu (ör. `P03`) yazar, 10 SUS maddesini ve 4 ek maddeyi (RQ3_1, RQ3_2, RQ4_1, RQ4_2) cevaplar,
   **Gönder**. "… kodlu cevaplar kaydedildi." mesajı görülür. Moderatör ekrana bakmaz.
3. Moderatör 2 dakikalık kısa görüşme yapar: "En yararlı bulduğunuz bir şey? En kafa karıştırıcı bir şey? AI
   önerilerine güvenir miydiniz, neden?" Cevaplar gözlem formuna kişisel bilgi içermeden yazılır.
4. Sonraki katılımcı için **Yeni katılımcı** butonu, ardından bölüm 3.2.

## 7. Gözlem formu

Her katılımcı için bir form. Başarı kodu: **B** = başarılı, **Y** = yardımla (ipucu) başarılı, **K** = kısmi,
**X** = başarısız / vazgeçti.

**Katılımcı:** P__ · **Tarih:** ____ · **Başlangıç saati:** ____ · **Bitiş saati:** ____ · **Sözlü onay:** ☐
**Profil:** rol ____ · deneyim ____ · AI sohbet aracı kullanımı ____ · **Model:** ____
**Başlangıç değerleri (Durum):** SPI(t) ____ · Tamamlanma %____ · Tahmini bitiş ____ · Hedef ____ · Sağlık ____ · ML %____

| Görev | Başarı (B/Y/K/X) | Süre (mm:ss) | Hedef | Hata sayısı | SEQ (1–7) | Notlar |
|---|---|---|---|---|---|---|
| G1 Durum sekmesini okuma | | | 4:00 | | | Söylenen nedenler: |
| G2 AI'ya durum sorusu | | | 3:00 | | | AI cevap süresi ≈ __ sn · Doğrulanamayan sayılar: var/yok |
| G3 What-if +1 Backend, P80 | | | 5:00 | | | Mevcut P80 ____ · Senaryo P80 ____ · Karar: |
| G4 Eksik iş kartları (tek tek) | | | 5:00 | | | Toplu buton kullanıldı mı: E/H |
| G5 AI yorumu + anlaşılırlık | | | 3:00 | | | Evet/Hayır ____ · Puan ____ |

**Hata tanımı:** hedefe götürmeyen bir eylem (yanlış sekme açmak, yanlış butona basmak, yanlış değeri okumak,
toplu uygulamaya basmak). Aynı hatanın tekrarı ayrı sayılır.

**G4 kart tablosu:**

| # | Rozet (Şablon/AI) | Önerilen iş | Karar (Uygula/Vazgeç/toplu) | Gerekçe (katılımcının sözü) |
|---|---|---|---|---|
| 1 | | | | |
| 2 | | | | |
| 3 | | | | |
| 4 | | | | |
| 5 | | | | |
| 6 | | | | |

**Görüşme notları:** en yararlı ____ · en kafa karıştırıcı ____ · AI'ya güven ____
**Teknik sorunlar (hata mesajı, bekleme, bağlantı):** ____

## 8. Test sonrası anket (uygulama içi)

- Adres: `/evaluation/survey` (başlık "Kullanıcı testi anketi").
- İçerik: onam + katılımcı kodu (`P` + 2–3 rakam; ad/e-posta girilemez), **Kullanılabilirlik (SUS)** (10 madde, Brooke
  1996'nın Türkçe çevirisi — geçerlik çalışması yapılmamış kendi çevirimiz, raporda belirtilmeli) ve **Senaryo analizi ve
  AI açıklamaları** (RQ3_1, RQ3_2, RQ4_1, RQ4_2). 5'li ölçek: 1 "Kesinlikle katılmıyorum" … 5 "Kesinlikle katılıyorum".
- 14 maddenin hepsi cevaplanmadan, onay kutusu işaretlenmeden veya kod geçersiz/tekrarlıysa kayıt yapılmaz (Türkçe hata).
- Kod başına tek cevap. Hatalı bir deneme kaydı `/evaluation` sayfasında **Sil** → **Silmeyi onayla** ile kaldırılır.
- SUS puanı katılımcıya gösterilmez; `/evaluation` sayfasında n, ortalama, standart sapma, min/maks ve Bangor sıfatı görünür.

## 9. Veri dışa aktarma kontrol listesi (son oturumdan sonra)

`/evaluation` sayfasında **Yenile**, sonra aşağıdaki butonlar. Dosyalar `ad-yyyyMMdd-HHmm.csv` biçiminde iner (virgül
ayırıcı, nokta ondalık, UTF-8 BOM; Excel Türkçe yerel ayarında "Veri → Metinden" ile açılır).

- [ ] **Kullanılabilirlik (SUS)** → **Anket CSV** → `anket-sus-likert-*.csv` (katılımcı başına satır; SUS maddeleri,
  `sus_score`, `sus_adjective`, RQ3/RQ4 maddeleri).
- [ ] **RQ3 · What-if karar desteği** → **CSV indir** → `rq3-whatif-calistirmalari-*.csv` (senaryo başına satır; `source`,
  `changes`, `p50_finish`, `p80_finish`, `on_time_probability`, P80 farkı).
- [ ] **RQ4 · Hibrit öneriler ve AI açıklamaları** → **Öneri kabul CSV** → `rq4-oneri-kabul-*.csv`
  (`source, tool, status, decided_in_bulk, count`).
- [ ] Aynı bölüm → **AI kayıtları CSV** → `rq4-ai-kayitlari-*.csv` (sohbet turları, yorumlar, eksik iş LLM çağrıları;
  `schema_valid`, `unverified_count`, `duration_ms`, `helpful`, `clarity_rating`, token ve `evaluation_run_id` kolonları).
- [ ] İsteğe bağlı: **RQ1 · EVM ve ML tahmini** → **CSV indir** → `rq1-evm-ml-zaman-serisi-*.csv` (demo snapshot'ları;
  kullanıcı testinin konusu değil, yalnız sistem davranışının kanıtı).
- [ ] `/evaluation` sayfasının ekran görüntüsü (SUS özeti, RQ4 kabul tablosu).
- [ ] Gözlem formları elektronik tabloya aktarıldı (katılımcı kodu, görev, başarı, süre, hata, SEQ).
- [ ] Dosyalar **repo dışında** (ör. raporun ek klasöründe) saklanır. CSV'lerde kişisel veri yoktur ama ham gözlem
  notları ve ekran kayıtları repoya konmaz; rapora yalnız özet tablolar girer.

Katılımcı ↔ kayıt eşlemesi: uygulama kayıtları katılımcı kodu taşımaz. Gerekirse gözlem formundaki başlangıç/bitiş
saatleri ile CSV'lerdeki `created_at` (UTC) karşılaştırılır. Bu eşleme yalnız katılımcı bazlı betimleme içindir.

## 10. Analiz planı

Tüm analiz **betimseldir**. n < 10 için çıkarımsal test (t-testi, güven aralığına dayalı genelleme, korelasyon
anlamlılığı) yapılmaz ve "anlamlı fark" iddiası kurulmaz. Raporlanan her sayının yanında n verilir.

1. **Görev performansı:** görev başına başarı oranı (B / n; ayrıca B + Y / n), süre medyanı ve min–maks (yalnız
   başarılı denemeler; ortalama yerine medyan), hata sayısı ortalaması, SEQ medyanı. Hedef süreyi aşan katılımcı sayısı.
2. **SUS:** uygulamanın hesapladığı katılımcı puanları (Brooke: (Σ(tek − 1) + Σ(5 − çift)) × 2,5); ortalama, standart
   sapma (n − 1), min/maks. Ortalama, **68** genel ortalamasıyla (Sauro & Lewis) yalnız betimsel olarak karşılaştırılır
   ("ortalamanın üstünde/altında"). Sıfat Bangor, Kortum ve Miller (2009) ortalamalarına göre: ≥ 90,9 en iyi akla gelen,
   ≥ 85,5 mükemmel, ≥ 71,4 iyi, ≥ 50,9 OK (fena değil), ≥ 35,7 zayıf, ≥ 20,3 berbat. Puan yüzde değildir. Madde bazında
   ortalamalar sorunlu maddeyi bulmak için verilir.
3. **RQ3:** RQ3_1 ve RQ3_2 için 1–5 dağılımı (frekans tablosu / yığılmış çubuk), medyan, 4–5 payı. Görev 3 kararlarının
   özeti (kaç katılımcı "değer" dedi, hangi gerekçeyle — P80 farkı mı, hedefe yetişme olasılığı mı, maliyet mi);
   `rq3-whatif-calistirmalari` CSV'sinden katılımcı başına çalıştırma ve senaryo sayısı.
4. **RQ4 — öneri kabulü:** `rq4-oneri-kabul` CSV'sinden **kaynağa göre** (Kural / şablon, LLM, Kullanıcı) kabul oranı =
   Uygulandı / (Uygulandı + Reddedildi). **`decided_in_bulk = true` satırları hariç** tutulur (bulk kabul oranı şişirir;
   sayfadaki "Kabul oranı (tek tek)" sütunu aynı hesaptır). Toplu karar sayısı ayrıca raporlanır. Ölçüm koşusu kartları
   bu CSV'ye zaten girmez. Kart düzeyinde gözlem tablosundan gerekçe temaları (ör. "zaten var", "kapsam dışı", "gerekli").
5. **RQ4 — açıklama anlaşılırlığı:** `rq4-ai-kayitlari` CSV'sinde `kind` = `ProjectComment` / `ScenarioComment` olan ve `helpful` dolu satırlardan Evet
   payı ve `clarity_rating` dağılımı/medyanı; RQ4_1 ve RQ4_2 Likert dağılımları; yorumlarda `unverified_count > 0` payı
   (doğrulanamayan sayı içeren yorum oranı) ve `schema_valid` payı.
6. **Nitel veri:** sesli düşünme ve görüşme notlarından tekrar eden sorunlar; her sorun için etkilenen katılımcı sayısı
   ve ciddiyet (engelleyici / yavaşlatıcı / kozmetik).

Raporlama şablonu (doldurulacak): SUS ortalama `____` ± `____` (n = `____`), sıfat `____`; G1–G5 başarı `__/__`;
Şablon kartı kabul (tek tek) `%____` (n = `____`), AI kartı kabul (tek tek) `%____` (n = `____`); "Anlaşılır mıydı?"
Evet `%____` (n = `____`).

## 11. Geçerlilik tehditleri ve sınırlılıklar

- **Küçük örneklem (5–10):** sonuçlar genellenemez; SUS ortalamasının belirsizliği büyüktür. Yalnız betimsel ifadeler.
- **Örneklem yanlılığı:** katılımcılar araştırmacının çevresinden (kolayda örnekleme); deneyim ve AI aşinalığı dengesiz
  olabilir. Profil bilgisi raporda verilir.
- **Moderatör ve sosyal istenirlik etkisi:** geliştiricinin kendisi moderatörse katılımcılar olumlu cevap vermeye
  eğilimli olabilir. Azaltma: "test edilen sistem" vurgusu, moderatörün anket sırasında ekrana bakmaması.
- **Yapay görev ve demo verisi:** proje sentetik bir demo; katılımcının sorumluluğu yok, karar sonuçları gerçek değil.
  Gerçek bir projede kullanım (uzun süreli) ölçülmüyor.
- **Görev sırası etkisi:** sabit sıra (öğrenme etkisi); G5'teki anlaşılırlık değerlendirmesi G1'de oluşan kendi
  yorumundan etkilenir. Sıra, veri tutarlılığı (G3'ün aynı planla çalışması) için bilerek sabit tutuldu.
- **LLM değişkenliği:** aynı soruya farklı katılımcılar farklı AI cevabı ve farklı sayıda AI kartı görebilir; model sürümü
  ve sağlayıcının o günkü davranışı sonuçları etkiler. Kullanılan model adı raporlanır.
- **Ölçüm araçları:** SUS Türkçe metni geçerlik çalışması yapılmamış kendi çevirimizdir; RQ3/RQ4 maddeleri bu çalışma için
  yazıldı (her biri tek ya da iki madde, ölçek güvenirliği hesaplanamaz). "Anlaşılır mıydı?" tek maddelik öz bildirimdir.
- **Kabul oranı yorumu:** kabul, önerinin doğru olduğunu değil yöneticinin uygun bulduğunu gösterir; demo bilerek eksik iş
  içerdiğinden şablon kartlarının kabulü beklenen yönde yüksek olabilir.
- **Teknik:** ağ gecikmesi veya sağlayıcı hatası görev süresini uzatabilir (gözlem formunda not edilir); anket ve kayıtlar
  katılımcı koduyla bağlanmadığı için katılımcı bazlı kart analizi yalnız gözlem formundan yapılır.
- **Hawthorne etkisi:** gözlem altında katılımcılar daha dikkatli davranabilir.
