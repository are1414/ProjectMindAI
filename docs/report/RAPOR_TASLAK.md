# Yapay Zekâ Destekli Yazılım Proje Planlama ve Karar Destek Sistemi: Zamanlama, Kaynak ve Kapsam Yönetimi Üzerine Bir Uygulama

> **Taslak durumu:** Bu metin Faz 10 rapor taslağıdır. Sayısal bulgular yalnızca `docs/PROGRESS.md` ve `docs/DECISIONS.md`'de
> kayıtlı ölçümlerden alınmıştır. Henüz ölçülmemiş her sonuç **[SONUÇ BEKLENİYOR: …]** ile, doğrulanması gereken her kaynak
> **[KAYNAK DOĞRULANMALI]** ile işaretlidir. Şekiller **[ŞEKİL: …]** yer tutucusudur.

---

## Kapak bilgileri

| Alan | Bilgi |
|---|---|
| Üniversite | [ÜNİVERSİTE ADI] |
| Enstitü | [ENSTİTÜ ADI] |
| Anabilim dalı / program | Mühendislik Yönetimi (Tezsiz Yüksek Lisans) |
| Proje türü | Dönem / bitirme projesi |
| Başlık | Yapay Zekâ Destekli Yazılım Proje Planlama ve Karar Destek Sistemi: Zamanlama, Kaynak ve Kapsam Yönetimi Üzerine Bir Uygulama |
| Hazırlayan | [AD SOYAD], [ÖĞRENCİ NO] |
| Danışman | [UNVAN AD SOYAD] |
| Yer, tarih | [ŞEHİR], [AY YIL] |
| Yazılım | ProjectMind AI (.NET 10, Blazor Server, SQL Server, ML.NET) |

---

## Özet

Yazılım projelerinde takvim, kaynak ve kapsam kararları çoğunlukla sezgiye ve geç fark edilen performans göstergelerine
dayanır. Bu çalışmada, tek bir proje yöneticisinin kullanımına yönelik, yapay zekâ destekli bir proje planlama ve karar destek
sistemi (ProjectMind AI) tasarım bilimi yaklaşımıyla geliştirilmiş ve değerlendirilmiştir. Sistem; Kritik Yol Metodu (CPM) ve
seri çizelgeleme şemasıyla (SSGS) kaynak kısıtlı otomatik plan, efor bazlı Kazanılmış Değer Yönetimi (EVM) ve Kazanılmış Takvim
(ES) ile takip, ağırlıkları Analitik Hiyerarşi Süreci (AHP) ile belirlenen sağlık skoru, sentetik veriyle eğitilmiş ML.NET
gecikme sınıflandırıcısı, Brooks etkisini içeren Monte Carlo what-if simülasyonu ve kural + büyük dil modeli (LLM) hibrit eksik
iş önerisini tek bir Blazor Server uygulamasında birleştirir. Temel tasarım ilkesi, LLM'in hiçbir sayıyı hesaplamaması; tüm
sayıların deterministik C# kodundan gelmesi, LLM cevaplarının JSON şemasıyla doğrulanması ve cevaptaki her sayının bağlamda
aranmasıdır (NumberGuard). Dört araştırma sorusundan RQ1 için 400 sentetik projede ve beş tohumda yapılan deneyde ML
sınıflandırıcısı F1'de EVM kuralını belirgin biçimde geçmiş (0,873 ± 0,027'ye karşı 0,665 ± 0,058), geciken projelerin daha
büyük bölümünü daha erken işaretlemiş, ancak 0,5 eşiğinde daha yüksek yanlış alarm üretmiştir; AUC farkı küçüktür
(0,902'ye karşı 0,891). RQ2–RQ4 ve kullanılabilirlik (SUS) ölçümlerinin altyapısı hazırdır; kullanıcı testi ve gerçek
sağlayıcılarla yapılacak ölçümlerin sonuçları beklenmektedir.

**Anahtar kelimeler:** yazılım proje yönetimi, karar destek sistemi, kazanılmış değer yönetimi, kazanılmış takvim, kaynak
kısıtlı çizelgeleme, Monte Carlo simülasyonu, makine öğrenmesi, büyük dil modelleri, halüsinasyon önleme

## Abstract

Schedule, resource and scope decisions in software projects often rely on intuition and on performance indicators that reveal
problems late. This study designs and evaluates, following a design science approach, an AI-supported project planning and
decision support system (ProjectMind AI) intended for a single project manager. The system integrates, within one Blazor Server
application, resource-constrained automatic scheduling based on the Critical Path Method (CPM) and the serial schedule generation
scheme (SSGS); effort-based Earned Value Management (EVM) with Earned Schedule (ES); a project health score whose weights are
derived with the Analytic Hierarchy Process (AHP); an ML.NET delay classifier trained on synthetic project data; Monte Carlo
what-if simulation including a Brooks' law ramp-up model; and a hybrid rule-plus-large-language-model (LLM) missing-work detector.
The central design principle is that the LLM never computes numbers: all figures come from deterministic C# code, LLM outputs are
requested and validated against a JSON schema, and every number in an answer is checked against the supplied context
(NumberGuard). For RQ1, an experiment on 400 synthetic projects and five random seeds shows that the ML classifier clearly
outperforms the EVM rule in F1 (0.873 ± 0.027 vs. 0.665 ± 0.058) and flags a larger share of delayed projects earlier, at the
cost of a higher false-alarm rate at the 0.5 threshold; the AUC difference is small (0.902 vs. 0.891). Measurement
infrastructure for RQ2–RQ4 and usability (SUS) is in place; results of the user study and real-provider runs are pending.

**Keywords:** software project management, decision support system, earned value management, earned schedule,
resource-constrained scheduling, Monte Carlo simulation, machine learning, large language models, hallucination mitigation

---

## 1. Giriş

### 1.1 Problem

Yazılım projeleri, belirsiz efor tahminleri, sonradan büyüyen kapsam, kişi ve beceri kısıtları ve geç fark edilen
gecikmelerle yönetilir. Proje yöneticisi aynı anda üç soruya cevap aramak zorundadır: *Ne zaman biter?* (zamanlama),
*Kim neyi, ne zaman yapar?* (kaynak) ve *Planda eksik iş var mı, kapsam ne kadar büyüdü?* (kapsam). Klasik araçlar bu soruların
her birine ayrı ayrı yanıt verir. CPM ağ hesabını, EVM performans endekslerini, Monte Carlo ise olasılıklı bitiş tarihini
üretir. Ancak bu yöntemler küçük ve orta ölçekli ekiplerde çoğunlukla birlikte kullanılmaz. Ayrıca EVM endekslerinin
gecikmeyi projenin geç evrelerinde ortaya koyduğu bilinmektedir (Vandevoorde & Vanhoucke, 2006; Lipke, 2003).

Son yıllarda proje yönetimi araçlarına eklenen büyük dil modeli (LLM) asistanları doğal dille etkileşimi kolaylaştırmıştır.
Bununla birlikte LLM'ler, bağlamda bulunmayan sayı ve olgular üretebilir (halüsinasyon; Ji vd., 2023). Tarih, maliyet ve
olasılık gibi sayısal kararların söz konusu olduğu bir karar destek sisteminde bu risk kabul edilemez. Dolayısıyla problem iki
katmanlıdır. Birincisi, planlama, takip, tahmin ve senaryo analizinin tek bir tutarlı karar akışında birleştirilmesidir.
İkincisi, LLM'in bu akışa sayısal güvenilirliği bozmadan, açıklayıcı ve önerici bir rolle eklenmesidir.

### 1.2 Amaç

Çalışmanın amacı, tek kullanıcılı (proje yöneticisi) bir yazılım proje planlama ve karar destek sistemi tasarlamak,
geliştirmek ve araştırma sorularıyla değerlendirmektir. Sistemin akışı şöyledir: proje tanımı → iş listesi → eksik iş önerisi
→ kişiler → otomatik plan → durum girişi → analiz ve tahmin → what-if → AI yorumu. Sistemde sayıları deterministik algoritmalar
ve makine öğrenmesi (ML) üretir. LLM yalnızca açıklar ve önerir. Veriyi değiştiren her AI önerisi yöneticinin
**Uygula / Vazgeç** onayından geçer.

### 1.3 Önem

Çalışmanın mühendislik yönetimi açısından üç katkısı vardır:

1. Klasik proje kontrol yöntemlerini (CPM, SSGS, EVM/ES, AHP, Monte Carlo) tek bir uygulamada, aynı veri modeli üzerinde ve
   test edilmiş kodla birleştirir.
2. ML tabanlı gecikme tahminini, literatürdeki karşılaştırma tasarımına (Wauters & Vanhoucke, 2016) uygun biçimde, aynı
   test satırlarında EVM/ES kuralıyla kıyaslar.
3. LLM'i "sayı üretmeyen" bir bileşen olarak konumlandırır. Bu amaçla JSON şema doğrulaması, sayı doğrulayıcı (NumberGuard),
   öneri kartı ve denetim kaydı kullanılır. Böylece savunulabilir ve ölçülebilir bir insan–AI iş bölümü önerilmiş olur.

### 1.4 Kapsam ve sınırlar

Kapsam içinde olanlar: tek kullanıcı (D2), tek web uygulaması (Blazor Server, D3), SQL Server veritabanı (D4), yalnız
Finish-to-Start bağımlılıklar (D13), proje bazlı ekip (D14), efor (saat) bazlı EVM (D19) ve sentetik veriyle eğitilen gecikme
modeli (D21). Kapsam dışında olanlar: kimlik doğrulama, çoklu kullanıcı, harici araç (Jira/Azure DevOps) entegrasyonu,
mikroservis mimarisi ve derin öğrenme (`docs/DECISIONS.md` → "YAPILMAYACAKLAR").

### 1.5 Araştırma soruları

- **RQ1:** ML tabanlı tahmin, gecikmeyi EVM/Earned Schedule'dan daha erken ve daha doğru öngörebilir mi?
- **RQ2:** Hangi faktörler (doluluk, kapsam artışı, bloke işler vb.) gecikmeyi en çok açıklıyor?
- **RQ3:** What-if (Monte Carlo) analizi yöneticinin karar vermesini destekliyor mu?
- **RQ4:** Hibrit (kural + LLM) eksik iş tespiti plan bütünlüğünü artırıyor mu? LLM açıklamaları anlaşılır mı?

### 1.6 Raporun yapısı

Bölüm 2 kavramsal çerçeveyi ve ilgili literatürü özetler. Bölüm 3 tasarım bilimi yöntemini, mimariyi, her hesaplama motorunun
formüllerini ve araştırma sorularının değerlendirme tasarımını verir. Bölüm 4 uygulamayı ve demo projesini tanıtır. Bölüm 5
bulguları, Bölüm 6 tartışmayı ve geçerlilik tehditlerini, Bölüm 7 sonuçları ve gelecek çalışmaları sunar.

---

## 2. Kavramsal Çerçeve ve Literatür

### 2.1 Kritik Yol Metodu (CPM)

CPM, faaliyetlerin süre ve öncelik ilişkilerinden en erken ve en geç başlangıç–bitiş zamanlarını hesaplayan bir ağ analizi
yöntemidir (Kelley & Walker, 1959). Ağ üzerinde ileri geçişle en erken başlangıç (ES) ve en erken bitiş (EF), geri geçişle en
geç başlangıç (LS) ve en geç bitiş (LF) değerleri bulunur. Bolluğu sıfır olan faaliyetler kritik yolu oluşturur. Benzer
dönemde geliştirilen PERT, süre belirsizliğini üç noktalı tahminle ele alır (Malcolm vd., 1959). CPM kaynak kısıtını dikkate
almaz. Bu nedenle tek başına gerçekçi bir yazılım planı üretmez.

### 2.2 Kaynak kısıtlı proje çizelgeleme ve SSGS

Kaynak kısıtlı proje çizelgeleme problemi (RCPSP) NP-zor bir problemdir. Uygulamada öncelik kuralı tabanlı sezgisellerle
çözülür. Kolisch (1996), seri (SSGS) ve paralel (PSGS) çizelge üretme şemalarını karşılaştırmıştır. SSGS her adımda öncülleri
çizelgelenmiş faaliyetler arasından öncelik kuralına göre bir faaliyet seçer ve onu kaynak kapasitesinin izin verdiği en erken
zamana yerleştirir. Hartmann ve Kolisch (2000) bu tür sezgiselleri deneysel olarak karşılaştırmıştır. En küçük geç başlangıç (min-LS)
gibi CPM tabanlı öncelik kuralları bu sezgisellerde yaygın biçimde kullanılır. ProjectMind AI, beceri eşleşmesi ve kişi bazlı günlük kapasiteyle
genişletilmiş bir SSGS kullanır.

### 2.3 Kazanılmış Değer Yönetimi ve Kazanılmış Takvim

EVM; planlanan değer (PV), kazanılmış değer (EV) ve gerçekleşen maliyet (AC) üzerinden takvim ve maliyet performansını ölçer
(Fleming & Koppelman, 2010; PMI, 2021). Klasik takvim endeksi $SPI = EV/PV$, proje sonunda gecikmiş olsa bile 1'e döner,
çünkü bitişte $EV = PV = BAC$ olur. Lipke (2003) bu sorunu Kazanılmış Takvim (Earned Schedule, ES) ile çözmüştür. ES, EV
değerine PV eğrisinin hangi zamanda ulaştığını ölçer. Zaman bazlı endeks $SPI(t) = ES/AT$ biçiminde tanımlanır.
Vandevoorde ve Vanhoucke (2006), ES tabanlı süre tahminlerinin klasik EVM tahminlerine göre özellikle projenin son evrelerinde
daha güvenilir olduğunu raporlamıştır. Batselier ve Vanhoucke (2015) ise 51 gerçek projede EVM zaman ve maliyet tahmin
doğruluğunu ampirik olarak değerlendirmiştir. Bu çalışmada ES, ML modelinin karşılaştırma tabanıdır (D10).

### 2.4 Analitik Hiyerarşi Süreci (AHP)

AHP, ölçütlerin ikili karşılaştırma matrisinden temel özvektörle ağırlık çıkaran ve yargıların tutarlılığını tutarlılık
oranıyla (CR) denetleyen çok ölçütlü bir karar yöntemidir (Saaty, 1980). Saaty, $CR < 0{,}10$ olan matrisleri kabul edilebilir
sayar. Bu çalışmada AHP, proje sağlık skorunun beş bileşeninin ağırlıklarını keyfî olmaktan çıkarmak için kullanılır (D11).

### 2.5 Monte Carlo simülasyonu ve Brooks yasası

Monte Carlo simülasyonu, belirsiz girdilerden (ör. efor) olasılık dağılımları örnekleyerek bitiş tarihi ve maliyet
dağılımını tahmin eder. Böylece P50, P80 gibi yüzdelik değerler elde edilir (Vose, 2008). Senaryo karşılaştırmasında ortak
rastgele sayılar (common random numbers) yöntemi, iki senaryo arasındaki farkın örnekleme gürültüsünden değil, senaryodan
kaynaklanmasını sağlayarak varyansı azaltır (Law, 2015). Brooks (1975) "gecikmiş bir yazılım projesine insan eklemek onu daha
da geciktirir" gözlemini, yeni gelenin öğrenme süresine ve mevcut ekibin iletişim ve mentorluk yüküne bağlamıştır. Bu çalışmada
Brooks etkisi, what-if senaryolarında ısınma süresi ve mentorluk yükü olarak modellenir (D12, D22).

### 2.6 Makine öğrenmesi ile gecikme tahmini

Wauters ve Vanhoucke (2016), yapay zekâ yöntemlerinin (ör. karar ağacı toplulukları, destek vektör makineleri) proje süresi
tahmininde EVM/ES tabanlı yöntemlerle karşılaştırılmasını Monte Carlo ile üretilmiş, topolojik olarak çeşitli bir proje
setinde yapmıştır. Bu tasarım, gerçek proje verisinin kıt olduğu durumlarda yaygın bir yaklaşımdır. Topluluk yöntemlerinden
rastgele orman (Breiman, 2001) ve gradyan artırma (Friedman, 2001) tablo verisinde güçlü başarım gösterir. Rastgele ormanla
birlikte önerilen permütasyon önemi (Breiman, 2001), özelliklerin tahmine katkısını modelden bağımsız biçimde ölçer.
Sınıflandırma başarımı eşikten bağımsız olarak ROC eğrisi altındaki alanla (AUC; Fawcett, 2006), eşiğe bağlı olarak ise F1
ölçütüyle raporlanır. Proje bazlı veri ayrımı yapılmaması, aynı projenin farklı zaman noktalarının hem eğitimde hem testte
yer almasına ve bilgi sızıntısına (leakage) yol açar (Kaufman vd., 2012).

Yazılım projesi dinamiklerinde sık görülen "%90 sendromu", ilerlemenin projenin büyük bölümünde iyimser raporlanması ve
gerçek iş büyüklüğünün geç ortaya çıkmasıdır. Abdel-Hamid (1988) bu olguyu sistem dinamiği simülasyonuyla incelemiştir
[KAYNAK DOĞRULANMALI]. Çalışmanın sentetik üreticisi bu olguyu bilerek modeller (Bölüm 3.5).

### 2.7 LLM tabanlı asistanlar, halüsinasyon ve yapılandırılmış çıktı

LLM'ler, araç çağırma (tool use) yeteneğiyle dış fonksiyonları çağırabilen asistanlara dönüşmüştür (Schick vd., 2023;
Yao vd., 2023). Bağlam dışı veya gerçeğe aykırı içerik üretimi (halüsinasyon) ise önemli bir risk olmaya devam eder
(Ji vd., 2023). Yaygın önlemler şunlardır: cevabı geri getirilen veya verilen kaynağa bağlamak (Lewis vd., 2020), çıktıyı
JSON şemasıyla kısıtlamak (sağlayıcıların yapılandırılmış çıktı özellikleri; Google Gemini API ve Anthropic Claude API
belgeleri [KAYNAK DOĞRULANMALI]) ve çıktıyı sonradan doğrulamak. Otomatik sadakat (faithfulness) dedektörlerinin halüsinasyonu
kaçırabildiği, insan etiketinin referans olarak kaldığı raporlanmaktadır (FaithJudge, 2025; UniSumEval, 2024
[KAYNAK DOĞRULANMALI]). Bu çalışma üç önlemi birlikte kullanır: deterministik araç çıktısına dayalı bağlam, JSON şeması ve sayı
doğrulayıcı.

Öneri temelli asistanların değerlendirilmesinde **kabul oranı** (kabul edilen / gösterilen öneri) yaygın bir davranış
ölçütüdür (Ziegler vd., 2022). Açıklama kalitesi için ise Hoffman vd. (2018) açıklama memnuniyeti ölçeğini önermiştir.

### 2.8 Karar destek sistemleri

Karar destek sistemleri (KDS), yarı yapılandırılmış kararlarda veri, model ve kullanıcı arayüzünü birleştirerek yöneticiyi
destekler (Sprague, 1980). ProjectMind AI model güdümlü bir KDS'dir. Modelleri CPM, SSGS, EVM/ES, AHP, ML ve Monte Carlo'dur.
Doğal dil arayüzü (LLM) bu modellerin sonuçlarını açıklar. Kararı ise her zaman yönetici verir (öneri kartı → Uygula / Vazgeç).
Sistemin kullanılabilirliği, yaygın ve kısa bir ölçek olan Sistem Kullanılabilirlik Ölçeği (SUS; Brooke, 1996) ile ölçülür.
Puanların yorumlanmasında Bangor, Kortum ve Miller'ın (2009) sıfat ölçeği ve yaklaşık 68'lik ortalama karşılaştırma noktası
(Sauro & Lewis, 2016) kullanılır.

### 2.9 Benzer ürünler

Ekip turu pazar incelemesi (`docs/team/TEST_PAZAR.md`) ticari araçları özetlemiştir. Ürün özellikleri satıcı belgelerine
dayandığından tablo [KAYNAK DOĞRULANMALI] olarak işaretlidir.

**Tablo 2.1** Benzer ürünlerin karşılaştırması

| Ürün | Öne çıkan AI / analitik özellik | ProjectMind AI'daki karşılığı |
|---|---|---|
| Microsoft Planner (Project Manager agent) | Önerileri onay kartıyla sunma | Öneri kartı (AiAction) + Uygula / Vazgeç |
| Microsoft Project | EVM alanları ve raporları | Efor bazlı EVM + ES (SPI(t)) |
| Jira (AI work breakdown) | İşi alt işlere bölme önerisi | Hibrit eksik iş önerisi (kural + LLM) |
| LiquidPlanner | Aralık tahminli Monte Carlo çizelgeleme | Monte Carlo what-if (üçgen dağılım, P50/P80/P90) |
| Forecast.app | Otomatik planlama / kaynak atama | SSGS + beceri/kapasite eşleşmesi |
| Asana (smart status) | AI ile durum özeti | Şemalı ve sayı doğrulamalı proje yorumu |
| monday.com (risk insights) | Risk işaretleme | Kural tabanlı risk uyarıları + ML gecikme olasılığı |
| ClickUp Brain | Genel amaçlı proje asistanı | Araç çağıran sohbet asistanı (salt okunur araçlar) |

İncelemede bu ürünlerin hiçbirinin AI özelliklerinin doğruluğunu veya açıklama sadakatini ölçülebilir biçimde yayımlamadığı
görülmüştür. ProjectMind AI'nin ayırt edici yönü, sayıların kaynağını ve LLM çıktılarının doğrulanmasını kayıt altına alması
ve ölçmesidir.

---

## 3. Yöntem

### 3.1 Araştırma yaklaşımı: tasarım bilimi

Çalışma, bilgi sistemlerinde tasarım bilimi yaklaşımını izler (Hevner vd., 2004). Bu yaklaşımda araştırma çıktısı bir yapay
nesnedir (artifact), burada çalışan bir yazılım sistemidir. Yapay nesnenin faydası titiz yöntemlerle değerlendirilir. Hevner
vd.'nin yönergeleri çalışmaya şöyle yansır:

**Tablo 3.1** Tasarım bilimi yönergelerinin çalışmadaki karşılığı

| Yönerge | Çalışmadaki karşılığı |
|---|---|
| Yapay nesne olarak tasarım | ProjectMind AI (Blazor Server uygulaması) |
| Problemin önemi | Bölüm 1.1 |
| Tasarımın değerlendirilmesi | RQ1–RQ4, SUS; deneysel (sentetik), gözlemsel (kayıt) ve betimsel (anket) yöntemler |
| Araştırma katkısı | Bölüm 6.2 |
| Araştırma titizliği | Elle doğrulanabilir birim testleri (313 test), sürümlü veri ve prompt'lar, tohumlu deneyler |
| Arama süreci olarak tasarım | Fazlı geliştirme (Faz 0–10), ekip turları (analiz → test → geliştirme → doğrulama) |
| İletişim | Bu rapor, sunum, demo |

Geliştirme, `docs/PLAN.md` fazlarıyla yürütülmüş ve her oturum `docs/PROGRESS.md`'de belgelenmiştir. Kesinleşen kararlar
`docs/DECISIONS.md`'de (D1–D31) tutulmuştur (Ek C). Bir işin bitmiş sayılması için hesaplama kodunun birim testi bulunmalı,
`dotnet build` uyarısız tamamlanmalı ve `dotnet test` yeşil olmalıdır. Son kayıtlı durum: **313/313 test geçti, derleme
0 uyarı** (PROGRESS (24)).

### 3.2 Mimari ve katmanlar

Sistem tek bir .NET 10 web uygulamasıdır (Blazor Server, interactive server, ön-render kapalı; D3). Ayrı bir API katmanı
yoktur. Katmanlar sade bir temiz mimari izler (D5):

**Tablo 3.2** Katmanlar

| Katman | Sorumluluk |
|---|---|
| `ProjectMind.Domain` | Entity ve enum'lar (bağımlılıksız) |
| `ProjectMind.Application` | Servisler, iş kuralları, hesaplama motorları (Planning, Analytics, Ml, WhatIf, MissingWork, Ai, Evaluation) |
| `ProjectMind.Infrastructure` | EF Core (SQL Server) ve migration'lar, LLM sağlayıcıları (Gemini, Claude, Mock), ML.NET öğrenicileri |
| `ProjectMind.Web` | Blazor bileşenleri; iş mantığı ve EF sorgusu içermez, Application servislerini `AppScope` ile çağırır |
| `ProjectMind.Tests` | Birim ve entegrasyon testleri (SQLite), senaryolu sahte LLM modeli |

[ŞEKİL 3.1: Katman diyagramı — Domain ← Application ← Infrastructure ← Web; LLM sağlayıcıları `IChatModel` arkasında]

LLM erişimi `IChatModel` soyutlamasıyla sağlanır. Varsayılan sağlayıcı Gemini'dir, Claude alternatif olarak hazırdır. API
anahtarı yoksa sistem ücretsiz Mock moduna düşer (D8). Anahtarlar yalnızca git'e girmeyen yerel ayarlarda tutulur. Testler
gerçek API çağırmaz.

**İnsan–AI iş bölümü (D9, D17).** Sohbet asistanı iki tür araç kullanır:

1. *Öneri araçları* (proje/kişi/iş/bağımlılık ekle–güncelle, planı uygula): Bu araçlar veriyi değiştirmez, bir öneri kartı
   (`AiAction`) üretir. Kart metni modelden değil veriden oluşturulur. Yönetici Uygula veya Vazgeç der ve karar loglanır.
2. *Salt okunur araçlar* (`get_project_status`, `preview_schedule`, `check_missing_work`, `simulate_what_if`): Sayı gerektiren
   her cevap bunlardan beslenir. Araçların JSON çıktısı deterministik C# kodundan gelir.

[ŞEKİL 3.2: Bir sohbet turunun sıra diyagramı — kullanıcı → ChatService → IChatModel → araç → C# motoru → JSON → model cevabı → NumberGuard → kart/cevap]

### 3.3 Zamanlama: CPM ve kaynak kısıtlı seri çizelgeleme

**CPM.** Faaliyet $i$'nin süresi $d_i$ (çalışma saati), öncül kümesi $P(i)$ ve ardıl kümesi $S(i)$ olsun. Sıralama Kahn
algoritmasıyla bulunur ve döngü varsa açıklayıcı hata verilir. İleri geçiş:

$ES_i = \max_{j \in P(i)} EF_j \ (P(i)=\emptyset \Rightarrow ES_i = 0), \qquad EF_i = ES_i + d_i$

Proje süresi $T = \max_i EF_i$ olur. Geri geçiş:

$LF_i = \min_{k \in S(i)} LS_k \ (S(i)=\emptyset \Rightarrow LF_i = T), \qquad LS_i = LF_i - d_i$

Bolluk $TF_i = LS_i - ES_i$ olarak hesaplanır. $TF_i \le \varepsilon$ ($\varepsilon = 10^{-4}$) olan faaliyet kritiktir.
Bağımlılıklar yalnız Finish-to-Start türündedir (D13).

**SSGS (öncelik = en küçük LS).** Yalnızca yaprak işler planlanır. Üst işe verilen bağımlılık alt işlerine açılır. Kalan
efor $d_i = \text{tahmini efor}_i \times (1 - \%\text{tamamlanma}_i)$ olarak alınır. Her adımda uygun küme şöyle tanımlanır:

$E = \{ i \notin \text{çizelgelenmiş} : P(i) \subseteq \text{çizelgelenmiş} \}$

Seçilen faaliyet $i^* = \arg\min_{i \in E} (LS_i,\ -\text{öncelik}_i,\ \text{id}_i)$ sözlük sırasıyla belirlenir. $i^*$ için
en erken gün, öncüllerin bitişinden sonraki iş günüdür. Gereken beceriye sahip ve kapasitesi sıfırdan büyük kişiler arasından,
günlük kapasite $c_p = \text{haftalık}_p / 5$ aşılmadan işi **en erken bitirecek** kişi seçilir. Eşitlikte daha az yüklü kişi
tercih edilir. Elle atanmış kişi korunur. Uygun kişi yoksa iş, günlük varsayılan saatle kişisiz planlanır ve uyarı üretilir.
Çıktılar şunlardır: iş başına başlangıç–bitiş tarihleri, kişi doluluğu (atanan saat / pencere kapasitesi), planlanan maliyet
($\sum_p \text{atanan saat}_p \times \text{saatlik maliyet}_p$) ve kaynak sınırsız CPM bitişi. Takvim yalnız Pazartesi–Cuma
iş günlerinden oluşur.

### 3.4 Takip: efor bazlı EVM ve Earned Schedule

EVM saat bazlıdır. Para birimi değerleri saat × saatlik maliyetten türetilir (D19). Baseline'da her işin tam eforu saklanır.

- **PV eğrisi:** Her baseline işinin saati, plan penceresindeki iş günlerine eşit yayılır. $PV(k)$, $k$. iş gününün sonuna
  kadar planlanan kümülatif saattir. $BAC = \sum_i h_i$ (iptal edilen işler hariç).
- $EV = \sum_i h_i \cdot \%\text{tamamlanma}_i$, $AC = \sum_i \text{harcanan saat}_i$
- $SPI = EV/PV$, $CPI = EV/AC$, $EAC = BAC/CPI$
- **Earned Schedule** (doğrusal ara değer): $C$, $PV(C) \le EV$ koşulunu sağlayan tam gün sayısı olsun. Bu durumda
  $ES = C + \dfrac{EV - PV(C)}{PV(C+1) - PV(C)}$
- **Gerçek süre** $AT$, durum gününe kadar geçen iş günü sayısıdır (dahil). Zaman bazlı endeks $SPI(t) = ES/AT$ ve
  tahmini süre $PD/SPI(t)$ olur. Burada $PD$ planlanan süredir (iş günü).

Uç durum kuralları literatüre ve test bulgularına göre eklenmiştir:

1. **İptal = kapsam dışı (D24):** İptal edilen işin saati BAC ve PV'den düşülür (`DescopedHours`). Harcanan saat AC'de kalır.
2. **Silme → iptal (D27):** İlerlemesi olan baseline işi silinmez, iptale çevrilir.
3. **Biten projede AT donar (D27):** $EV \ge BAC$ ise $AT$ tamamlanma gününde sabitlenir, $SPI(t) = PD/\text{gerçek süre}$.
   Bu kural, bitmiş projede SPI(t)'nin her gün düşmesi hatasını (TEST_PAZAR Q1) giderir.
4. **BAC = 0:** SPI, SPI(t), CPI ve EAC tanımsızdır ("—").
5. **Yeniden baseline (D24):** Başlamış işin eforu, kazanılmış ve kalan kısım olarak iki satıra ayrılır.

Baseline'dan sonra eklenen işler EV/AC'ye girmez. Bu işler kapsam büyümesi olarak ölçülür:

$\text{büyüme}\% = \max\!\left(0,\ \dfrac{\text{güncel efor} - (\text{baseline efor} - \text{descope})}{\text{baseline efor} - \text{descope}} \times 100\right)$

### 3.5 Sağlık skoru: AHP ağırlıkları ve bileşenler (D20)

**AHP.** $A$ ikili karşılaştırma matrisi (Saaty 1–9 ölçeği) olsun. Ağırlık vektörü $w$, $A$'nın temel özvektörüdür ve
kuvvet yöntemiyle bulunur:

$w^{(t+1)} = \dfrac{A w^{(t)}}{\lVert A w^{(t)} \rVert_1}, \qquad \lambda_{\max} = \dfrac{1}{n}\sum_i \dfrac{(Aw)_i}{w_i}$

Tutarlılık indeksi ve oranı $CI = \dfrac{\lambda_{\max} - n}{n-1}$ ve $CR = CI/RI_n$ biçimindedir ($RI_5 = 1{,}12$). Matris
$CR < 0{,}10$ ise kabul edilir. Varsayılan matrisin ölçüt sırası Takvim, Maliyet, Kapsam, Kaynak, Risk'tir. İlk satır
$(1, 2, 3, 3, 2)$ değerlerinden oluşur ve takvimi en önemli ölçüt olarak tanımlar. Bu matrisin tutarlılık oranı
$CR \approx 0{,}003$'tür (PROGRESS (15)). Matristen hesaplanan ağırlıklar yaklaşık şöyledir: Takvim 0,369; Maliyet 0,206;
Kapsam 0,109; Kaynak 0,109; Risk 0,206. *(Not: ağırlıklar varsayılan matrisin özvektörüdür. Uygulamanın Durum sekmesindeki
değerlerle karşılaştırılarak doğrulanmalıdır.)* Saaty'nin ders kitabı örneğiyle yazılan birim testi 0,637 / 0,258 / 0,105
ağırlıklarını ve CR = 0,033 değerini elle doğrular.

**Bileşen puanları (0–100):**

- Takvim $= 100 \cdot \min(SPI(t), 1)$ (SPI(t) yoksa SPI kullanılır)
- Maliyet $= 100 \cdot \min(CPI, 1)$
- Kapsam $= 100 - 2 \times \text{büyüme}\%$
- Kaynak $= 100 \cdot (1 - \text{kişisiz kalan efor} / \text{kalan efor})$
- Risk $= 100 \cdot (1 - \text{bloke veya gecikmiş kalan efor} / \text{kalan efor})$

Skor $H = \sum_c w_c s_c / \sum_c w_c$ olarak hesaplanır. Hesaplanamayan bileşen dışarıda bırakılır ve ağırlıklar yeniden
ölçeklenir. Seviyeler şöyledir: $H \ge 75$ "İyi", $H \ge 50$ "Dikkat", diğer durumlar "Kritik". Kritik bir risk uyarısı
varsa seviye en fazla "Dikkat" olabilir. Risk uyarıları kural tabanlıdır: SPI/CPI 0,95'in altında dikkat, 0,85'in altında
kritik; kapsam büyümesi %10 üzerinde; ayrıca hedef tarih, bloke iş, gecikmiş iş ve kaynak uyarıları üretilir. Tüm eşikler
`Health` ayar bölümündedir.

### 3.6 Gecikme tahmini: sentetik veri ve ML (D21)

**Sentetik üretici (synthetic-v1, tohumlu).** Gerçek proje verisi bulunmadığından haftalık ayrık zaman simülasyonu kullanılır.
Her proje için ekip büyüklüğü $\{2..8\}$ ve iş sayısı $\{10..60\}$ aralığından seçilir. İş eforları log-normal dağılımdan
gelir (medyan 24 saat, $[4, 120]$ aralığında kırpılır). Planlanan süre şöyle hesaplanır:

$PD = \left\lceil \dfrac{BAC}{\text{ekip} \times 40 \times 0{,}8} \right\rceil$ hafta

Modele **verilmeyen** gizli değişkenler:

**Tablo 3.3** Gizli değişkenler

| Gizli değişken | Dağılım | Anlam |
|---|---|---|
| Ekip verimliliği | $\text{LogNormal}(0{,}15;\ 0{,}2)$ | 1 = planlandığı gibi |
| Tahmin iyimserliği | $U(0{,}8;\ 1{,}35)$ | gerçek efor / tahmini efor |
| Kapsam oynaklığı | $U(0;\ 0{,}05)$ | haftalık kapsam ekleme olasılığı ölçeği |
| Engel oranı | $U(0;\ 0{,}12)$ | kapasitenin engellerle kaybolan payı |

Haftalık gürültü kaynakları şunlardır: kullanılabilirlik $U(0{,}82;\ 0{,}98)$, haftalık %3 olasılıkla iki haftalık devir
(verim 0,7), etkinlik $\mathcal{N}(1;\ 0{,}12)$ ve raporlama gürültüsü $\mathcal{N}(0;\ 0{,}03)$.

**"%90 sendromu".** Ekip ilerlemeyi tahmini efora göre raporlar ve gerçek büyüklük ilerledikçe ortaya çıkar:

$\text{revealed} = BAC + (\text{trueTotal} - BAC) \cdot \dfrac{\text{trueDone}}{\text{trueTotal}}, \qquad EV = BAC \cdot \dfrac{\text{trueDone}}{\text{revealed}}\,(1+\epsilon)$

Bu nedenle erken dönemde EVM iyimser kalır. Böylece gecikme tek bir formülden değil, gizli değişkenlerin haftalık
etkileşiminden ortaya çıkar. Gecikme etiketi $\text{gerçek süre} > PD \times 1{,}10$ koşuluyla verilir.

**Özellikler.** Özellikler gerçek bir projede de ölçülebilen oranlardır ve planlanan sürenin %25, %50 ve %75 kontrol
noktalarında hesaplanır: geçen süre oranı, tamamlanma ($EV/BAC$), SPI, CPI, SPI(t), son iki hafta hızı (kazanılan / planlanan),
kapsam büyümesi (eklenen / BAC), bloke payı ve ekip büyüklüğü. Gerçek projede aynı özellikleri `DelayFeatureBuilder` snapshot
geçmişinden üretir.

**Modeller (ML.NET).** Sınıflandırıcılar L-BFGS lojistik regresyon (normalizasyonla), FastTree (gradyan artırmalı ağaçlar)
ve FastForest'tır (Platt kalibrasyonuyla; Platt, 1999). Süre oranı regresyonu için FastTree kullanılır. Üretimde test AUC'si
en yüksek sınıflandırıcı seçilir.

**Ayrım ve karşılaştırma.** Eğitim/test ayrımı **proje bazlıdır** (%70/%30). Aynı projenin kontrol noktaları aynı kümede
kalır, böylece sızıntı önlenir. EVM karşılaştırma kuralı aynı test satırlarında uygulanır: $1/SPI(t) > 1 + 0{,}10$ ise EVM
"gecikecek" der. EVM süre tahmini $PD/SPI(t)$'dir. ML kuralı olasılık $\ge 0{,}5$ (`Ml:Threshold`) koşuludur. Ölçütler
doğruluk, kesinlik, duyarlılık, F1, AUC ve süre oranı için MAE'dir.

**Permütasyon önemi (RQ2).** Özellik $j$'nin önemi, test setinde $j$ sütunu karıştırıldığında AUC'deki ortalama düşüştür
(5 tekrar):

$I_j = AUC_{\text{orijinal}} - \frac{1}{R}\sum_{r=1}^{R} AUC_{\text{permüte},\,r}^{(j)}$

**Erken uyarı (D31).** Geciken her test projesinde *ilk işaret*, yöntemin gecikmeyi ilk doğru gördüğü kontrol noktasıdır.
*Uyarı süresi* gerçek bitiş haftası ile o noktanın haftası arasındaki farktır (hafta). Zamanında biten projede herhangi bir
noktada verilen işaret *yanlış alarmdır*. Yakalanan pay = en az bir noktada işaretlenen geciken proje / geciken proje.
Ortalamalar yalnız yakalanan projeler üzerinden alınır. **Çok tohumlu analiz:** 42–46 tohumlarıyla veri, ayrım ve öğrenici
birlikte değiştirilir. Sonuçlar ortalama ± örneklem standart sapması ($n-1$) olarak verilir.

### 3.7 What-if: Monte Carlo ve Brooks etkisi (D22)

Senaryolar veriyi değiştirmez. Desteklenen değişiklikler şunlardır: kişi ekle (sayı, beceri, kapasite, katılım tarihi), kişi
çıkar, kapasite değiştir, iş çıkar (alt işleriyle; $A \to B \to C$'den $B$ çıkarılınca $A \to C$) ve hedef tarihi değiştir.
Her turda aynı SSGS çizelgeleyicisi gün bazlı kapasite pencereleriyle çalıştırılır.

**Efor belirsizliği.** Her işin kalan eforu üçgen dağılımlı bir çarpanla ölçeklenir:
$m \sim \text{Tri}(a = 0{,}85;\ c = 1{,}0;\ b = 1{,}5)$. Örnekleme ters CDF ile yapılır. $F_c = (c-a)/(b-a)$ olmak üzere:

$F^{-1}(u) = \begin{cases} a + \sqrt{u\,(b-a)(c-a)}, & u < F_c \\ b - \sqrt{(1-u)(b-a)(b-c)}, & u \ge F_c \end{cases}$

Dağılım sağa çarpıktır, çünkü yazılımda aşım küçülmeden daha olasıdır.

**Ortak rastgele sayılar.** $u$ değeri $(\text{tohum}, \text{tur}, \text{iş})$ üçlüsünden SplitMix64 ile belirlenimci olarak
türetilir. Bu yüzden tüm senaryolar aynı turda aynı işe aynı çarpanı uygular ve senaryo farkı gürültüden değil
değişiklikten gelir. Varsayılan ayarlar 500 tur ve tohum 7'dir.

**Yüzdelikler.** Sıralı bitiş günleri dizisinde en yakın sıra yöntemi kullanılır: $P_p = x_{(\lceil p n \rceil)}$. P50, P80 ve
P90 bitiş tarihleri, P50/P80 maliyet, hedefe yetişme olasılığı $\Pr(\text{bitiş} \le \text{hedef})$ ve bitiş CDF'i raporlanır.
Senaryo karşılaştırma ölçütü **P80 bitiştir** ve mevcut plana göre P80 farkı verilir.

**Brooks modeli.** Yeni kişi katılım gününden önce sıfır kapasitelidir. Katıldıktan sonra $R = 4$ hafta boyunca %50 verimle
çalışır. Aynı pencerede mevcut ekibin her üyesinin kapasitesi şu çarpanla ölçeklenir:

$f = \max\!\left(0{,}5,\ 1 - 0{,}25 \cdot \dfrac{c_{\text{yeni}}}{\sum_{e} c_e}\right)$

Böylece toplam mentorluk yükü yeni kişinin günlük kapasitesinin %25'i kadardır ve mevcut ekibe kapasiteleri oranında dağılır.
Bir kişinin kapasitesi en fazla yarıya düşebilir. Model kasıtlı olarak basittir: bir iş tek kişiye atanır (bölünmez). Bu
nedenle kişi eklemek yalnız paralel iş varsa hızlandırır. Birim testi bu etkiyi elle doğrular: tek kişi ve iki adet 80 saatlik
iş için ısınmasız ek kişiyle bitiş 10 iş günüdür. Isınma modeliyle yeni kişi ilk dört haftada bitişi hızlandırmaz ve bitiş
20 iş günü olur.

### 3.8 Kapsam: hibrit eksik iş tespiti (D15, D26, D28)

**Kural katmanı (templates-v1).** Kodda sürümlü 19 şablon vardır ve proje tipine göre filtrelenir. Mevcut iş adları Türkçe
sadeleştirmeden geçirilir (ı→i, ş→s vb.) ve anahtar kelimeyle eşleştirilir. Çok kelimeli anahtar alt metin olarak, üç harf
ve altındaki anahtar tam kelime olarak, diğerleri kelime başı olarak eşleşir. Karşılığı bulunmayan şablon eksik iş olarak
önerilir. Öneriye "hangi işten önce bitmeli" bağımlılığı ve plana etkisi (ek iş günü, efor, maliyet) eklenir. Plana etki
simülasyonla hesaplanır.

**LLM katmanı.** LLM'e yalnız özet JSON gönderilir: proje adı, tipi ve açıklaması, mevcut iş adları, kural önerileri ve
kapsanan şablonlar. Model, şablonların kapsamadığı ek işleri `LlmMissingWorkSchema` şemasıyla döndürür. Şemanın alanları ad,
faz, tek beceri, gerekçe ve S/M/L büyüklüğüdür. **Sayı alanı yoktur.** Saatler C#'ta atanır: S 8, M 24, L 80. Cevap C#'ta
yeniden doğrulanır. Üst yapı bozuksa LLM önerisi kullanılmaz, tek bir madde bozuksa yalnız o madde düşer. Mevcut işlerle,
kural önerileriyle veya birbiriyle aynı olan öneriler ad benzerliğiyle elenir. Tek kelimelik adlar yalnız tam eşitlikte
elenir, kök eşleşmesinde ek en çok dört harf olabilir. En fazla 5 öneri gösterilir. Elenenler ve sınırı aşanlar kayda yazılır.

**Kaynak etiketi C#'ta belirlenir:** şablon kaynaklı öneri `Rule`, LLM kaynaklı öneri `Llm`, diğerleri `User` olarak
etiketlenir. Kabul oranı şöyle hesaplanır:

$\text{kabul} = \dfrac{\text{Uygulandı}}{\text{Uygulandı} + \text{Reddedildi}}$

Oran kaynağa göre, hem tüm kararlar hem yalnız tek tek verilen kararlar (toplu "hepsini uygula" hariç) için ayrı raporlanır.

### 3.9 LLM çıktısının denetimi: JSON şeması ve NumberGuard (D18, D23, D25)

**JSON şeması (D25).** Proje ve senaryo yorumlarında model araçsız ve JSON şemasıyla çağrılır. Gemini için
`responseMimeType` + `responseJsonSchema`, Claude için yapılandırılmış çıktı formatı kullanılır. Şemanın alanları `summary`,
`keyFindings[]`, `recommendedActions[]` ve `caveats[]`'dir. Ek alan, eksik alan ve aşırı uzunluk geçersizdir. Cevap C#'ta
yeniden doğrulanır ve geçersiz cevap kullanıcıya gösterilmez. Yorumun girdisi, salt okunur araçların deterministik JSON'udur
(`AnalysisPayloads`). Yorum yalnız buton ile üretilir. Girdi, prompt sürümü ve sağlayıcının SHA-256 özeti aynıysa önceki
yorum yeniden gösterilir.

**NumberGuard.** $N(r)$ cevaptaki sayı belirteçleri, $K$ kanıt kümesi olsun. Kanıt, bu turun mesajı, bağlam, önceki
**kullanıcı** mesajları ve bu turun araç sonuçlarından oluşur. Önceki asistan cevapları kanıt sayılmaz (D23).
Doğrulanamayan sayılar kümesi şöyle tanımlanır:

$U(r) = \{ x \in N(r) : \neg \exists\, k \in K,\ \text{eşleşir}(x, k) \}$

Eşleşme kuralları:

- Yuvarlama kabul edilir (cevaptaki ondalık basamağa yuvarlanmış kanıt değeri).
- Oran ↔ yüzde dönüşümü (×100) yalnız sayının yanında "%" veya "yüzde" varsa kabul edilir.
- Tarihler (05.12.2026, 5 Aralık 2026, ISO biçimi) gün bazında karşılaştırılır. Saat ifadeleri (14:30) sayı sayılmaz.
- Türkçe ondalık (0,87) ve binlik (1.250.000) biçimleri ile "bin/milyon/milyar" çarpanları tanınır (D28).
- 10 ve altındaki tam sayılar (adet, sıra) ve WBS kodları muaftır.
- Ayrıştırılamayan sayı benzeri ifade işaretlenir.

$U(r) \ne \emptyset$ olduğunda cevap reddedilmez. Bunun yerine altında "Doğrulanamayan sayılar" uyarısı gösterilir (D18).
Tam ret, adet ve sıra gibi zararsız sayılarda sohbeti kullanılamaz hâle getiriyordu. Görünür uyarı ise şeffaflığı korur.

**Denetim kaydı.** Her sohbet turu ve yorum `AiAnalysisLog` tablosuna yazılır. Kayıtta model, prompt sürümü, çağrılan
araçlar, bağlam uzunluğu, doğrulanamayan sayılar, şema geçerliliği, süre ve token sayıları bulunur. Tam bağlam ve API anahtarı
kaydedilmez.

### 3.10 Değerlendirme tasarımı

**Tablo 3.4** Araştırma sorularına göre değerlendirme tasarımı

| RQ | Veri | Yöntem | Ölçütler | Durum |
|---|---|---|---|---|
| RQ1 | Sentetik 400 proje (synthetic-v1), %70/%30 proje bazlı; ayrıca demo snapshot zaman serisi | ML ile EVM/ES kuralı aynı test satırlarında karşılaştırılır; 5 tohum (42–46) | F1, AUC, kontrol noktası bazında F1, yakalanan pay, ilk işaret, uyarı süresi, yanlış alarm, MAE | **Ölçüldü** (sentetik) |
| RQ2 | Aynı test seti | Permütasyon önemi (AUC düşüşü, 5 tekrar) | Özellik başına $I_j$ | Kısmi (tek tohumlu sıralama) |
| RQ3 | What-if çalıştırma kaydı (`WhatIfRunLog`), anket | Kullanıcı testinde iki karar görevi (yalnız deterministik plan, ardından what-if); Likert RQ3_1 (karar desteği), RQ3_2 (güven) | Çalıştırma sayısı, Likert ort. ± SS, 4–5 payı, karar değişimi | Altyapı hazır, veri bekleniyor |
| RQ4a | Demo projesi, tohumlanmış eksik iş deneyi (recall-v1: 4 şablon + 4 projeye özgü iş çıkarılır, tohum 11) | Yalnız kural ile hibrit karşılaştırması; hibrit 3 tekrar | Recall (grup başına), precision (alt sınır) | Kural ölçüldü, hibrit bekleniyor |
| RQ4b | Öneri kartları, yorum kartı geri bildirimi, anket | Kaynağa göre kabul oranı, "Bu açıklama anlaşılır mıydı?" (Evet/Hayır + 1–5), Likert RQ4_1, RQ4_2 | Kabul oranı (tümü / tek tek), anlaşılırlık, Likert | Altyapı hazır, veri bekleniyor |
| Sağlayıcı | eval-v1 görev seti (8 görev) × anahtarlı sağlayıcılar × 3 tekrar | Gemini ve Claude karşılaştırması | Hata oranı, şema geçerlilik, doğrulanamayan sayı payı, süre, token, beklenen araç oranı | Altyapı hazır, veri bekleniyor |
| Kullanılabilirlik | 5–10 katılımcı | SUS (10 madde), görev senaryosu | SUS ort. ± SS, Bangor sıfatı, 68 ile karşılaştırma | Form hazır, test bekleniyor |

**SUS hesabı (Brooke, 1996).** Madde cevapları $q_i \in \{1..5\}$ olmak üzere:

$SUS = 2{,}5 \times \left[ \sum_{i \in \{1,3,5,7,9\}} (q_i - 1) + \sum_{i \in \{2,4,6,8,10\}} (5 - q_i) \right]$

Sıfat eşikleri Bangor vd. (2009) ortalamalarına göredir: ≥ 90,9 en iyi akla gelebilir, ≥ 85,5 mükemmel, ≥ 71,4 iyi,
≥ 50,9 fena değil, ≥ 35,7 zayıf, ≥ 20,3 berbat. Anket uygulama içindedir (`/evaluation/survey`, survey-v1). Kayıttan önce
onam alınır, katılımcı yalnız anonim kodla (P + 2–3 rakam) tutulur ve ad/e-posta alanı yoktur (D30). Türkçe SUS metni, Brooke
(1996) orijinaline sadık kendi çevirimizdir. **Bu uyarlama için geçerlik çalışması yapılmamıştır.** Yayımlanmış bir Türkçe
uyarlama (ör. Demirkol & Şeneler, 2018 [KAYNAK DOĞRULANMALI]) ile değiştirilebilir. Kullanıcı testi protokolü Ek A'dadır.

**Ölçüm verisinin ayrılması.** Sağlayıcı karşılaştırma koşuları `EvaluationRunId` ile etiketlenir. Koşu sırasında oluşan
kartlar otomatik reddedilir ve RQ3/RQ4 kullanıcı özetlerinden hariç tutulur (D31). Böylece ölçüm koşuları katılımcı verisini
kirletmez.

---

## 4. Uygulama

### 4.1 Kullanım akışı

Sistem chat odaklıdır (D17). Proje, iş, kişi ve bağımlılık sohbetten yönetilir. Proje paneli sonuçları sekmeler hâlinde
gösterir.

1. **Proje tanımı ve iş listesi:** Kullanıcı sohbette projeyi anlatır. Asistan proje ve iş öneri kartları üretir. İşler WBS
   hiyerarşisindedir (1, 1.2, 1.2.1). Üst işin eforu ve ilerlemesi alt işlerden toplanır.
2. **Eksik iş önerisi:** "Eksik iş var mı?" kısayolu önce şablon önerilerini (`Rule`), ardından AI bağlıysa LLM ek önerilerini
   (`Llm`) plana etkileriyle birlikte kart olarak sunar.
3. **Kişiler:** Beceri, haftalık kapasite ve saatlik maliyet tanımlanır.
4. **Otomatik plan:** Plan (Gantt) sekmesi CPM + SSGS ile planı, kritik yolu, bolluğu ve kişi doluluğunu gösterir. "Planı
   uygula ve baseline kaydet" seçeneği vardır.
5. **Durum girişi:** Durum, % tamamlanma ve harcanan saat girilir. Her değişiklik `StatusUpdate` olarak, günlük özet ise
   `ProjectSnapshot` olarak saklanır.
6. **Analiz:** Durum sekmesinde sağlık skoru (AHP bileşenleri, CR), EVM kartları (SPI(t), CPI, tamamlanma, tahmini bitiş,
   EAC), risk uyarıları ve S-eğrisi (PV/EV/AC) bulunur.
7. **Gecikme tahmini:** ML gecikme olasılığı, ML ve EVM tahmini bitiş tarihleri ve model bilgisi gösterilir.
8. **What-if:** Senaryo kartları, karşılaştırma tablosu (plan/P50/P80, hedefe yetişme olasılığı, P80 farkı) ve bitiş
   olasılığı eğrisi sunulur.
9. **AI yorumu:** Durum ve What-if sekmelerinde şemalı, sayı doğrulamalı yorum kartı ve "Bu açıklama anlaşılır mıydı?"
   geri bildirimi yer alır.
10. **Deneyler ve Değerlendirme:** `/experiments` sayfasında ML deneyi, erken uyarı, çok tohumlu analiz ve CSV indirme;
    `/evaluation` sayfasında RQ özetleri, CSV'ler, sağlayıcı karşılaştırması ve eksik iş deneyi bulunur.

### 4.2 Ekranlar

- [ŞEKİL 4.1: Ana sayfa — "Demo projesini oluştur" butonu, kenar çubuğunda sohbetler]
- [ŞEKİL 4.2: Sohbet + proje paneli — öneri kartları (Uygula / Vazgeç, "Şablon"/"AI" rozetleri)]
- [ŞEKİL 4.3: İşler sekmesi — WBS ağacı, satır içi düzenleme]
- [ŞEKİL 4.4: Plan (Gantt) sekmesi — kritik yol (kırmızı), bolluklu işler, kişisiz işler, doluluk tablosu]
- [ŞEKİL 4.5: Durum sekmesi — sağlık kartı (AHP), EVM kartları, uyarılar, S-eğrisi]
- [ŞEKİL 4.6: Gecikme olasılığı (ML) kartı — olasılık, ML/EVM tahmini bitiş]
- [ŞEKİL 4.7: What-if sekmesi — senaryo karşılaştırma tablosu ve bitiş olasılığı eğrisi]
- [ŞEKİL 4.8: AI yorum kartı — özet, bulgular, öneriler, "Doğrulanamayan sayılar" uyarısı, anlaşılırlık geri bildirimi]
- [ŞEKİL 4.9: Deneyler sayfası — sonuç tablosu, özellik önemi, erken uyarı tablosu]
- [ŞEKİL 4.10: Değerlendirme sayfası ve anket formu]

### 4.3 Demo projesi: "Mobile Banking Modernization" (D29)

Demo, EVM, S-eğrisi, ML ve what-if'in anlamlı değer üretebilmesi için geçmişi olan bir projeye ihtiyaç duyar. Bu nedenle demo,
gerçek Application servislerinin geriye alınmış bir saatle (`DemoClock : TimeProvider`) yeniden oynatılmasıyla üretilir.
LLM çağrılmaz ve demo Mock modda da çalışır. Proje, bu haftanın pazartesisinden 9 hafta önce başlar. İlk gün baseline alınır,
her cuma ilerleme girilir ve durum hesaplanır. İlerleme baseline penceresi × ekip hızı (0,9 ± %5, tohum 2026) ile belirlenir.
Harcanan saat kazanılan saatin 1,05–1,20 katıdır.

**Tablo 4.1** Demo projesinin yapısı ve son durumu (08.10.2026 tarihli çalıştırma; PROGRESS (22))

| Gösterge | Değer |
|---|---|
| Ekip | 6 kişi (950–1.400 TRY/saat) |
| İşler | 35 (8 üst iş + 24 baseline yaprak işi + 3 sonradan eklenen), 30 bağımlılık |
| Baseline | 2.024 saat / 2.534.800 TRY; planlanan bitiş 25.12.2026 |
| Hedef / bütçe | 08.01.2027 / 2.900.000 TRY |
| Olaylar | Hafta 4: kapsam +%10,6 (QR ödeme + sadakat ekranı); hafta 5'ten itibaren bir bloke iş; hafta 6: bir iş iptal (kapsam dışı) |
| SPI(t) / SPI / CPI | **0,88** / 0,81 / **0,90** |
| Tamamlanma | **%41,4** |
| Sağlık skoru | **88,3 → "Dikkat"** (kritik uyarılar seviyeyi sınırlar: hedef tarih riski, planlanan bitişi geçmiş işler) |
| EVM tahmini bitiş | 15.01.2027 (hedefin ötesinde) |
| EAC maliyet | ≈ 2,72 milyon TRY |
| Snapshot sayısı | 10 |
| İş durumları | 7 bitti, 8 devam, 1 bloke, 1 iptal, 18 başlamadı |
| Kural katmanının bulduğu eksik işler | CI/CD, kullanıcı kabul testi (UAT), dokümantasyon (bilerek bırakılan üç işin tamamı) |

Tarihler çalıştırma gününe göre kaydığı için tablo 08.10.2026 tarihli çalıştırmanın değerlerini verir. Rapordaki ekran
görüntüleri alınırken çalıştırma tarihi not edilmelidir: [SONUÇ BEKLENİYOR: ekran görüntülerinin alındığı tarih ve o tarihteki
demo değerleri]. Demodaki ML olasılığı bir **gösterimdir, doğrulama değildir**, çünkü model sentetik veriyle eğitilmiştir.

Sağlık skorunun 88,3 olmasına karşın seviyenin "Dikkat" olması bilinçli bir tasarım kararıdır (D20). Ağırlıklı ortalama
bileşenlerdeki iyi değerler nedeniyle yüksek kalabilir. Kritik bir uyarı (EVM tahmini bitişin hedefi aşması) olduğunda ise
seviye en fazla "Dikkat" olur. Böylece tek bir sayının yanıltıcı güven vermesi önlenir.

---

## 5. Bulgular

### 5.1 RQ1: ML ve EVM/ES karşılaştırması (sentetik veri)

**Deney düzeni:** 400 sentetik proje (synthetic-v1), proje bazlı %70/%30 ayrım, ML eşiği 0,5, EVM toleransı %10, kontrol
noktaları %25/%50/%75. Çok tohumlu analizde tohumlar 42–46'dır. Değerler ortalama ± örneklem SS olarak verilir. "Seçilen ML",
her tohumda test AUC'si en yüksek sınıflandırıcıdır (PROGRESS (24)).

**Tablo 5.1** Sınıflandırma başarımı, 5 tohum

| Ölçüt | EVM (ES kuralı) | Seçilen ML |
|---|---|---|
| F1 (tüm kontrol noktaları) | 0,665 ± 0,058 | **0,873 ± 0,027** |
| AUC (tüm kontrol noktaları) | 0,891 ± 0,027 | 0,902 ± 0,018 |
| F1 (%25 kontrol noktası) | 0,607 ± 0,058 | **0,844 ± 0,046** |

**Tablo 5.2** Erken uyarı ölçütleri, 5 tohum

| Ölçüt | EVM (ES kuralı) | Seçilen ML |
|---|---|---|
| Yakalanan geciken proje payı | 0,646 ± 0,042 | **0,971 ± 0,022** |
| Ortalama ilk işaret (planlanan sürenin %'si) | %37,1 ± 3,2 | **%28,2 ± 0,6** |
| Ortalama uyarı süresi (hafta, yalnız yakalananlar) | 11,0 ± 1,1 | 10,3 ± 0,5 |
| Yanlış alarm payı (zamanında bitenler) | **0,055 ± 0,026** | 0,445 ± 0,056 |

Seçilen sınıflandırıcı tohuma göre değişmiştir: üç tohumda lojistik regresyon, iki tohumda FastForest seçilmiştir.

**Tablo 5.3** Tek tohum (42) ayrıntısı (test setinde 71 geciken, 49 zamanında proje)

| Ölçüt | EVM (ES kuralı) | Seçilen ML (lojistik regresyon) |
|---|---|---|
| Yakalanan geciken proje | %60,6 | %94,4 |
| Ortalama ilk işaret | %41,9 | %28,7 |
| Ortalama uyarı süresi | 10,6 hafta | 10,3 hafta |
| Yanlış alarm | %4,1 | %53,1 |

**Yorum.**

1. **Doğruluk (F1):** ML, 0,5 eşiğinde F1'de EVM kuralını yaklaşık 0,21 puan geçmektedir. Fark, tohumlar arası standart
   sapmaların birkaç katıdır. En erken nokta olan %25'te de fark korunmaktadır (0,844'e karşı 0,607). Faz 6'daki ilk tek
   tohumlu deneyde de aynı örüntü görülmüştür. EVM kuralının kesinliği yüksek (0,93–1,00), duyarlılığı düşüktür (0,37–0,54).
   Yani EVM gecikmeyi doğru ama geç ve eksik işaretlemektedir (PROGRESS (16)).
2. **Erkenlik:** ML geciken projelerin %97'sini yakalamış ve ilk işareti ortalama planın %28'inde vermiştir. EVM'de bu
   değerler %65 ve %37'dir. Ortalama uyarı süresinin EVM'de biraz daha uzun görünmesi (11,0'a karşı 10,3 hafta) bir seçim
   yanlılığıdır. EVM'nin yakaladığı projeler zaten ağır gecikmeli ve uzun süren projelerdir. Bu nedenle uyarı süresi, yakalama
   payıyla birlikte okunmalıdır.
3. **Eşik ve yanlış alarm:** ML'in kazancının bedeli, zamanında biten projelerin yaklaşık %45'inde en az bir yanlış alarm
   vermesidir. EVM'de bu oran yaklaşık %6'dır. Eşikten bağımsız ölçüt olan AUC'de fark küçüktür (0,902'ye karşı 0,891) ve
   standart sapmalar örtüşmektedir. **Bu nedenle F1 ve erkenlikteki farkın büyük bölümü sıralama yeteneğinden değil, karar
   eşiğinden ve kalibrasyondan kaynaklanmaktadır.** EVM kuralı tutucu bir eşikle (1/SPI(t) > 1,10), ML ise görece serbest bir
   eşikle (0,5) çalışmaktadır. Bu bulgu "ML daha iyi tahmin eder" iddiasını sınırlar. Daha doğru ifade şudur: ML, aynı
   göstergelerden benzer sıralama gücüyle, uygun eşikte daha erken uyarı üretmeye elverişlidir.
4. **Süre tahmini:** Tek tohumlu ilk deneyde süre oranı MAE'si ML'de 0,191, EVM'de ($PD/SPI(t)$) 0,264'tür (PROGRESS (16)).
   Bu değer çok tohumlu analizde tekrarlanmamıştır: [SONUÇ BEKLENİYOR: süre oranı MAE, 5 tohum, ort. ± SS].

**RQ1 cevabı (sentetik veri için):** Kısmen evet. Sabit 0,5 eşiğinde ML, gecikmeyi EVM/ES kuralından daha erken ve daha
yüksek F1 ile öngörmektedir. Ancak eşikten bağımsız ayırt etme gücü (AUC) yalnız marjinal olarak yüksektir ve yanlış alarm
maliyeti belirgindir. Sonuçlar sentetik veriyle sınırlıdır (Bölüm 6.3).

**Eksik analizler:**

- [SONUÇ BEKLENİYOR: eşik duyarlılığı — ML eşiği 0,5 / 0,6 / 0,7 ve EVM toleransı %5 / %10 / %15 için F1, yakalanan pay, yanlış alarm]

  | Eşik / tolerans | F1 | Yakalanan pay | Yanlış alarm |
  |---|---|---|---|
  | ML 0,5 | 0,873 ± 0,027 | 0,971 ± 0,022 | 0,445 ± 0,056 |
  | ML 0,6 / 0,7 | […] | […] | […] |
  | EVM %10 | 0,665 ± 0,058 | 0,646 ± 0,042 | 0,055 ± 0,026 |
  | EVM %5 / %15 | […] | […] | […] |

- [SONUÇ BEKLENİYOR: eşleştirilmiş AUC farkı için bootstrap güven aralığı (Fikir Havuzu)]
- [SONUÇ BEKLENİYOR: demo projesi zaman serisi — `rq1-evm-ml-zaman-serisi` CSV'sinden snapshot tarihi × SPI(t), EVM tahmini
  bitiş, ML olasılığı, ML tahmini bitiş grafiği; hangi yöntemin hangi snapshot'ta ilk uyardığı] (gösterim amaçlı)
- [ŞEKİL 5.1: Kontrol noktasına göre F1 (EVM ve ML, hata çubukları 5 tohum)]

### 5.2 RQ2: Gecikmeyi açıklayan faktörler

Faz 6'daki ilk tek tohumlu deneyde (tohum 42) permütasyon önemine göre en etkili üç gösterge sırasıyla **CPI, tamamlanma oranı
ve kapsam büyümesi** olmuştur (PROGRESS (16)). Önem değerlerinin sayısal tablosu dokümante edilmemiştir ve çok tohumlu analizde
permütasyon önemi atlanmıştır (D31).

[SONUÇ BEKLENİYOR: permütasyon önemi tablosu — `rq2-ozellik-onemi` CSV'si; mümkünse tohum başına tekrar]

| Sıra | Özellik | AUC düşüşü $I_j$ |
|---|---|---|
| 1 | CPI | […] |
| 2 | Tamamlanma (EV/BAC) | […] |
| 3 | Kapsam büyümesi | […] |
| 4–9 | SPI, SPI(t), son 2 hafta hızı, geçen süre, bloke payı, ekip büyüklüğü | […] |

**Ön yorum:** CPI'ın öne çıkması, sentetik üreticide verimlilik ve tahmin iyimserliğinin hem harcanan saati hem kazanılan
değeri etkilemesiyle tutarlıdır. Kapsam büyümesinin önemli çıkması, demo projesindeki kapsam uyarısının yönetsel değerini
destekler. Ancak bu sıralama üreticinin yapısal varsayımlarını yansıtabilir (döngüsellik, Bölüm 6.3). RQ2'nin cevabı sentetik
dünya için "üreticinin gizli değişkenlerine en yakın vekil göstergeler" biçiminde okunmalıdır.

### 5.3 RQ3: What-if analizinin karar desteği

Uygulama düzeyinde iki doğrulama yapılmıştır. Birim testleri belirsizliksiz Monte Carlo'nun deterministik planla aynı sonucu
verdiğini, $P50 \le P80 \le P90$ sırasını ve tekrarlanabilirliği doğrular. Brooks testi bölüm 3.7'de verilmiştir. Faz 7 görsel
kontrolünde küçük bir deneme projesinde +1 kişi senaryosu P80 bitişi 2 iş günü geciktirmiştir. Bunun nedeni ısınma süresinin
proje süresinden uzun olmasıdır ve sonuç Brooks etkisiyle tutarlıdır (PROGRESS (17)). Bu sonuç bir kullanıcı bulgusu değil,
modelin davranışının gösterimidir.

[SONUÇ BEKLENİYOR: kullanıcı testi RQ3 sonuçları]

| Ölçüt | n | Ortalama ± SS | 4–5 payı |
|---|---|---|---|
| RQ3_1: What-if analizi kararımı destekledi | […] | […] | […] |
| RQ3_2: What-if sonuçlarına güvendim | […] | […] | […] |
| Karar görevi: yalnız deterministik plan → what-if sonrası kararını değiştiren katılımcı | […] | — | — |
| Katılımcı başına what-if çalıştırması (panel / AI aracı) | […] | […] | — |

### 5.4 RQ4: Hibrit eksik iş tespiti ve açıklamaların anlaşılırlığı

**Kural katmanı (ölçüldü).**

- Eksiksiz bir web planından birer iş çıkarma (leave-one-out) deneyinde 15 şablonun 15'i doğru tespit edilmiş ve yanlış
  pozitif çıkmamıştır: precision = recall = 1,0 (PROGRESS (13)).
- Demo projesinde kural katmanı bilerek eksik bırakılan üç işin (CI/CD, UAT, dokümantasyon) tamamını bulmuştur.
- Tohumlanmış deneyde (recall-v1, tohum 11, 4 şablon işi + 4 projeye özgü iş çıkarılmıştır) kural-yalnız mod için şablon
  recall'u 1, projeye özgü recall 0 ve toplam recall 0,5'tir (PROGRESS (24)). Projeye özgü recall'un 0 olması tanım gereğidir:
  şablon dışı işleri kural katmanı bulamaz. Hibrit katmanın katkısı tam olarak bu grupta ölçülecektir.

**Hibrit katman (bekleniyor).**

[SONUÇ BEKLENİYOR: tohumlanmış eksik iş deneyi — gerçek sağlayıcıyla, 3 tekrar]

| Mod | Şablon recall | Projeye özgü recall | Toplam recall | Precision (alt sınır) | Elenen tekrar sayısı |
|---|---|---|---|---|---|
| Yalnız kural | 1,00 | 0,00 | 0,50 | […] | — |
| Hibrit (Gemini) | […] | […] ± […] | […] | […] | […] |
| Hibrit (Claude) | […] | […] ± […] | […] | […] | […] |

[SONUÇ BEKLENİYOR: kullanıcı testi öneri kabul oranları ve açıklama anlaşılırlığı]

| Ölçüt | Rule | Llm | User |
|---|---|---|---|
| Kart sayısı | […] | […] | […] |
| Kabul oranı (tümü) | […] | […] | […] |
| Kabul oranı (yalnız tek tek kararlar) | […] | […] | […] |

| Ölçüt | n | Değer |
|---|---|---|
| "Bu açıklama anlaşılır mıydı?" Evet payı | […] | […] |
| Anlaşılırlık puanı (1–5) ort. ± SS | […] | […] |
| RQ4_1: AI açıklamaları anlaşılırdı (Likert) | […] | […] |
| RQ4_2: Eksik iş önerileri yararlıydı (Likert) | […] | […] |
| Yorum kartı şema geçerlilik oranı | […] | […] |
| ≥1 doğrulanamayan sayı içeren yorum payı | […] | […] |

### 5.5 LLM sağlayıcı karşılaştırması

[SONUÇ BEKLENİYOR: eval-v1 koşusu — `saglayici-karsilastirma-*.csv`; Gemini ve Claude, görev başına 3 tekrar]

| Sağlayıcı / model | Hata oranı | Şema geçerlilik | Doğrulanamayan sayı payı (cevap başına sayı) | Süre ort. ± SS (ms) | Giriş / çıkış token ort. | Beklenen araç oranı |
|---|---|---|---|---|---|---|
| Gemini [model] | […] | […] | […] ([…]) | […] | […] / […] | […] |
| Claude [model] | […] | […] | […] ([…]) | […] | […] / […] | […] |

Görev başına kırılım (proje yorumu, senaryo yorumu, hibrit eksik iş, 5 sohbet sorusu) ek tabloda verilecektir:
[SONUÇ BEKLENİYOR: görev × sağlayıcı tablosu].

### 5.6 Kullanılabilirlik (SUS)

[SONUÇ BEKLENİYOR: 5–10 katılımcılı kullanıcı testi — `anket-sus-likert` CSV'si]

| Ölçüt | Değer |
|---|---|
| Katılımcı sayısı (n) | […] |
| SUS ortalama ± SS | […] ± […] |
| Min / maks | […] / […] |
| Bangor sıfatı (ortalamaya göre) | […] |
| 68 karşılaştırma noktasına göre | […] (üstünde / altında) |
| Görev tamamlama oranı | […] |

### 5.7 Yazılım doğrulaması

Son kayıtlı durumda 313 birim ve entegrasyon testinin tamamı geçmekte ve derleme uyarısız tamamlanmaktadır (PROGRESS (24)).
Hesaplama motorları elle doğrulanabilir küçük örneklerle test edilmiştir. Örnekler: CPM ders kitabı örneği; EVM/ES senaryosu
(PV 24, EV 22, AC 24, SPI(t) 0,92); zamanında biten projede SPI(t) = 1 ve iki gün geç bitende 5/7 ≈ 0,71; Saaty AHP örneği;
SUS için 0 / 50 / 100 ve elle hesaplanmış 75 ve 37,5; erken uyarı elle örneği (yakalanan 2/3, ilk işaret %37,5, uyarı
10 hafta, yanlış alarm 1/2). Testler gerçek LLM API'si çağırmaz, senaryolu sahte modeller kullanır.

---

## 6. Tartışma

### 6.1 Bulguların yorumu

RQ1 bulguları literatürle uyumludur. EVM/ES göstergeleri gecikmeyi yüksek kesinlikle ama geç gösterir (Vandevoorde &
Vanhoucke, 2006). Aynı göstergeleri birlikte ve doğrusal olmayan biçimde kullanan bir sınıflandırıcı ise erken evrede daha
çok gecikmeyi yakalar (Wauters & Vanhoucke, 2016). Bu çalışmanın katkısı farkı yalnız F1 ile değil, **erken uyarı** (ilk işaret,
uyarı süresi, yanlış alarm) ve **eşikten bağımsız AUC** ile birlikte raporlamasıdır. AUC farkının küçük olması, yöneticiye
sunulacak uyarının bir *karar eşiği* sorunu olduğunu gösterir. Yanlış alarmın maliyeti düşükse (ör. yalnız gözden geçirme
tetikleniyorsa) ML'in erken uyarısı değerlidir. Yanlış alarm pahalıysa (ör. kaynak ekleme kararı) eşik yükseltilmeli veya EVM
ile birlikte okunmalıdır. Uygulamada ML olasılığı, EVM tahmini bitişle yan yana gösterilerek bu ikili okumaya olanak tanınır.

Sistemin LLM tasarımı açısından en önemli gözlem, değerlendirme sırasında bulunan hataların büyük bölümünün LLM'den değil,
deterministik katmanın uç durumlarından kaynaklanmasıdır. Örnekler: bitmiş projede SPI(t)'nin düşmesi, silinen baseline işinin
EVM'yi bozması, NumberGuard'ın Türkçe sayı biçimini okuyamaması (TEST_PAZAR Q1, Q2, Q5). "LLM sayı hesaplamaz" ilkesi
hesaplama hatalarını tek, test edilebilir bir yere toplar. Bu sayede hatalar elle doğrulanabilir testlerle bulunup kalıcı
biçimde düzeltilebilmiştir (D24, D27, D28).

### 6.2 Katkılar

1. **Bütünleşik yapay nesne:** CPM, SSGS, EVM/ES, AHP, ML ve Monte Carlo + Brooks modelleri, tek kullanıcılı bir KDS'de aynı
   veri modeli ve test edilmiş kodla birleştirilmiştir.
2. **Sayı üretmeyen LLM deseni:** Salt okunur araç + JSON şema + C# yeniden doğrulama + NumberGuard + öneri kartı + denetim
   kaydı. Bu desen başka karar destek alanlarına aktarılabilir.
3. **Ölçülebilir AI:** Kaynak etiketinin C#'ta belirlenmesi, toplu ve tek tek kabulün ayrılması, ölçüm koşularının
   etiketlenmesi ve CSV dışa aktarımları sayesinde RQ'lar uygulamanın kendi kayıtlarından tekrarlanabilir biçimde
   üretilebilmektedir.
4. **Erken uyarı odaklı karşılaştırma:** ML ve EVM/ES karşılaştırması F1/AUC'ye ek olarak yakalama payı, ilk işaret ve
   yanlış alarm ile çok tohumlu raporlanmıştır.

### 6.3 Geçerlilik tehditleri

Sınıflandırma Wohlin vd. (2012) çerçevesini izler.

**İç geçerlilik ve yapı geçerliliği**

- **Sentetik veri döngüselliği:** ML modeli, üreticinin varsayımlarını (gizli değişkenler, "%90 sendromu", gürültü düzeyleri)
  öğrenmektedir. EVM'nin dezavantajı kısmen üreticinin iyimser raporlama modelinden kaynaklanabilir. Üretici parametrelerine
  duyarlılık analizi yapılmamıştır (Fikir Havuzu).
- **Ölçüt seçimi:** Gecikme tanımı (%10 tolerans), EVM karar kuralı (1/SPI(t) > 1,10) ve ML eşiği (0,5) sonuçları doğrudan
  etkiler. Farklı eşiklerde sıralama değişebilir (Bölüm 5.1, eşik duyarlılığı bekleniyor). Erken uyarı yalnız üç kontrol
  noktasında ölçülmüştür. Ara haftalar ölçülmediğinden ilk işaret zamanı kabadır.
- **Precision alt sınırı:** RQ4 deneyinde LLM önerilerinin eşleşmesi temkinlidir (ad benzerliği / şablon anahtar kelimesi).
  Eşleşmeyen ama ilgili öneriler yanlış sayılır. İki hakemli etiketleme (Cohen κ; Cohen, 1960) yapılmamıştır.
- **Kaynak etiketi:** Kullanıcının kendi yazdığı iş, adı bir kural önerisiyle aynıysa `Rule` sayılır. Bu durum kural kabul
  oranını şişirebilir (TEST_PAZAR Q7).
- **NumberGuard kapsamı:** NumberGuard yalnız sayının varlığını denetler, anlamını denetlemez. Yön hataları ("geride" yerine
  "ileride"), 10 ve altındaki birimli tam sayılar ve yazıyla yazılmış sayılar denetim dışında kalabilir (TEST_PAZAR Q3, Q4).
  Yuvarlama toleransı bilerek gevşektir. NumberGuard'ın precision/recall'u etiketli bir cevap setiyle ölçülmemiştir:
  [SONUÇ BEKLENİYOR: ≈30 yorumda sayısal iddiaların elle etiketlenmesi — doğru / yanlış değer / yanlış yön / yazıyla sayı].
- **SUS çevirisi:** Türkçe metnin geçerlik çalışması yapılmamıştır.

**Dış geçerlilik**

- **Gerçek proje verisi yok:** Model gerçek projelerde kalibre edilmemiştir. Demo projesi gösterimdir. Batselier ve
  Vanhoucke (2015) türü gerçek proje veri setleriyle doğrulama gelecek çalışmadır.
- **Tek kullanıcı ve bağlam:** Sistem tek proje yöneticisi için tasarlanmıştır. Ekip içi iş birliği, çoklu proje ve gerçek
  kurum süreçleri kapsam dışıdır.
- **Model basitlikleri:** Yalnız FS bağımlılık, bölünmeyen iş ataması ve tatil/izin takvimi olmaması (Fikir Havuzu).

**Sonuç geçerliliği**

- **Küçük örneklem:** 5–10 katılımcılı kullanıcı testi yalnız betimsel sonuç verir. Çıkarımsal istatistik yapılmayacaktır.
  Katılımcıların yöneticilik deneyimi sonuçları etkileyebilir.
- **LLM değişkenliği:** LLM cevapları deterministik değildir. Model sürümleri ve sağlayıcı davranışı zamanla değişir (ör.
  Gemini model adlarının kapatılması, PROGRESS (7)). Bu nedenle sağlayıcı sonuçları koşu tarihi, model adı ve prompt sürümüyle
  birlikte raporlanmalıdır. Tekrar sayısı (3) varyansı ancak kabaca yansıtır.
- **Tohum sayısı:** RQ1 için 5 tohum kullanılmıştır. SS tahminleri belirsizdir ve bootstrap güven aralığı verilmemiştir.

---

## 7. Sonuç ve Gelecek Çalışmalar

### 7.1 Sonuç

Bu çalışmada zamanlama, kaynak ve kapsam yönetimini tek bir karar akışında birleştiren, yapay zekâ destekli bir yazılım proje
planlama ve karar destek sistemi tasarım bilimi yaklaşımıyla geliştirilmiştir. Sistemde tüm sayılar deterministik algoritmalar
ve ML tarafından üretilir. LLM ise yalnız açıklar ve önerir. Bu iş bölümü JSON şema doğrulaması, NumberGuard ve öneri kartı
onayıyla güvence altına alınmıştır. Sentetik deneyde ML tabanlı gecikme tahmini, 0,5 eşiğinde EVM/ES kuralına göre daha yüksek
F1 ve daha erken uyarı sağlamıştır. Eşikten bağımsız ayırt etme gücündeki fark ise küçüktür ve yanlış alarm maliyeti
belirgindir. Bu sonuç, ML çıktısının EVM ile birlikte ve bilinçli bir karar eşiğiyle sunulması gerektiğini göstermektedir.
Kural tabanlı eksik iş katmanı, şablon kapsamındaki eksikleri eksiksiz bulmuştur. LLM katmanının şablon dışı işlere katkısı,
what-if analizinin karar desteği ve sistemin kullanılabilirliği kullanıcı testi ve gerçek sağlayıcı koşularıyla
ölçülecektir: [SONUÇ BEKLENİYOR: RQ2–RQ4 ve SUS özet cümleleri].

### 7.2 Gelecek çalışmalar

Fikir Havuzu'ndan (`docs/PLAN.md`) rapor açısından öne çıkan maddeler:

- **Tahmin:** gerçek proje snapshot'larıyla modelin ince ayarı ve doğrulanması; sentetik üretici parametrelerine duyarlılık
  analizi; haftalık kontrol noktaları; bootstrap güven aralığı ve eşleştirilmiş AUC testi; Durum kartında projeye özel yerel
  açıklama (tahmini en çok etkileyen göstergeler).
- **What-if:** Monte Carlo belirsizliğinin projenin gerçekleşen CPI dağılımından kalibre edilmesi; iş bazında en iyi / en kötü
  efor aralığı; senaryonun tek tıkla öneri kartlarına dönüştürülmesi; yöneticinin seçtiği senaryonun kaydı; paralel Monte Carlo
  turları.
- **AI katmanı:** yön ifadesinin C# tarafından hazır cümle olarak verilmesi (NumberGuard'ın sadeleştirilmesi); NumberGuard
  aramasının hızlandırılması; AI ile iş kırılımı; Hoffman vd. açıklama memnuniyeti ölçeğinin ankete eklenmesi; RQ4 ek
  önerilerinin iki hakemle etiketlenmesi (Cohen κ); sağlayıcı karşılaştırmasında token × birim fiyat maliyet tahmini;
  opsiyonel yerel model (Ollama) karşılaştırması.
- **Model kapsamı:** resmî tatil ve izin takvimi; çoklu baseline karşılaştırması ve VAC; deney raporlarının kalıcı saklanması.

---

## Kaynakça

> Biçim: APA 7. **[KAYNAK DOĞRULANMALI]** işaretli kaynakların künyesi (yazar, yıl, sayı, sayfa) son teslimden önce
> kontrol edilmelidir. İşaretsiz kaynakların da sayfa numaraları son kontrolden geçirilmelidir.

- Abdel-Hamid, T. K. (1988). Understanding the "90% syndrome" in software project management: A simulation-based case study. *Journal of Systems and Software, 8*(4), 319–330. [KAYNAK DOĞRULANMALI]
- Bangor, A., Kortum, P., & Miller, J. (2009). Determining what individual SUS scores mean: Adding an adjective rating scale. *Journal of Usability Studies, 4*(3), 114–123.
- Batselier, J., & Vanhoucke, M. (2015). Empirical evaluation of earned value management forecasting accuracy for time and cost. *Journal of Construction Engineering and Management, 141*(11), 05015010.
- Breiman, L. (2001). Random forests. *Machine Learning, 45*(1), 5–32.
- Brooke, J. (1996). SUS: A "quick and dirty" usability scale. In P. W. Jordan, B. Thomas, B. A. Weerdmeester, & I. L. McClelland (Eds.), *Usability evaluation in industry* (pp. 189–194). Taylor & Francis.
- Brooks, F. P., Jr. (1975). *The mythical man-month: Essays on software engineering*. Addison-Wesley.
- Cohen, J. (1960). A coefficient of agreement for nominal scales. *Educational and Psychological Measurement, 20*(1), 37–46.
- Demirkol, D., & Şeneler, Ç. (2018). Sistem Kullanılabilirlik Ölçeği'nin (SUS) Türkçe uyarlaması [Künye ve dergi bilgisi]. [KAYNAK DOĞRULANMALI]
- Fawcett, T. (2006). An introduction to ROC analysis. *Pattern Recognition Letters, 27*(8), 861–874.
- Fleming, Q. W., & Koppelman, J. M. (2010). *Earned value project management* (4th ed.). Project Management Institute.
- Friedman, J. H. (2001). Greedy function approximation: A gradient boosting machine. *The Annals of Statistics, 29*(5), 1189–1232.
- Google. (2026). *Gemini API: Structured output* [Çevrimiçi belge]. [KAYNAK DOĞRULANMALI]
- Anthropic. (2026). *Claude API: Structured outputs* [Çevrimiçi belge]. [KAYNAK DOĞRULANMALI]
- Hartmann, S., & Kolisch, R. (2000). Experimental evaluation of state-of-the-art heuristics for the resource-constrained project scheduling problem. *European Journal of Operational Research, 127*(2), 394–407.
- Hevner, A. R., March, S. T., Park, J., & Ram, S. (2004). Design science in information systems research. *MIS Quarterly, 28*(1), 75–105.
- Hoffman, R. R., Mueller, S. T., Klein, G., & Litman, J. (2018). *Metrics for explainable AI: Challenges and prospects* (arXiv:1812.04608). arXiv.
- Ji, Z., Lee, N., Frieske, R., Yu, T., Su, D., Xu, Y., Ishii, E., Bang, Y. J., Madotto, A., & Fung, P. (2023). Survey of hallucination in natural language generation. *ACM Computing Surveys, 55*(12), Article 248.
- Kaufman, S., Rosset, S., Perlich, C., & Stitelman, O. (2012). Leakage in data mining: Formulation, detection, and avoidance. *ACM Transactions on Knowledge Discovery from Data, 6*(4), Article 15.
- Kelley, J. E., Jr., & Walker, M. R. (1959). Critical-path planning and scheduling. In *Proceedings of the Eastern Joint IRE-AIEE-ACM Computer Conference* (pp. 160–173).
- Kolisch, R. (1996). Serial and parallel resource-constrained project scheduling methods revisited: Theory and computation. *European Journal of Operational Research, 90*(2), 320–333.
- Law, A. M. (2015). *Simulation modeling and analysis* (5th ed.). McGraw-Hill.
- Lewis, P., Perez, E., Piktus, A., Petroni, F., Karpukhin, V., Goyal, N., Küttler, H., Lewis, M., Yih, W., Rocktäschel, T., Riedel, S., & Kiela, D. (2020). Retrieval-augmented generation for knowledge-intensive NLP tasks. In *Advances in Neural Information Processing Systems, 33* (pp. 9459–9474).
- Lipke, W. (2003). Schedule is different. *The Measurable News*, (Summer), 31–34. [KAYNAK DOĞRULANMALI: sayı ve sayfa]
- Malcolm, D. G., Roseboom, J. H., Clark, C. E., & Fazar, W. (1959). Application of a technique for research and development program evaluation. *Operations Research, 7*(5), 646–669.
- Platt, J. C. (1999). Probabilistic outputs for support vector machines and comparisons to regularized likelihood methods. In A. J. Smola, P. Bartlett, B. Schölkopf, & D. Schuurmans (Eds.), *Advances in large margin classifiers* (pp. 61–74). MIT Press.
- Project Management Institute. (2021). *A guide to the project management body of knowledge (PMBOK guide)* (7th ed.). Project Management Institute.
- Saaty, T. L. (1980). *The analytic hierarchy process*. McGraw-Hill.
- Sauro, J., & Lewis, J. R. (2016). *Quantifying the user experience: Practical statistics for user research* (2nd ed.). Morgan Kaufmann.
- Schick, T., Dwivedi-Yu, J., Dessì, R., Raileanu, R., Lomeli, M., Zettlemoyer, L., Cancedda, N., & Scialom, T. (2023). Toolformer: Language models can teach themselves to use tools. In *Advances in Neural Information Processing Systems, 36*.
- Sprague, R. H., Jr. (1980). A framework for the development of decision support systems. *MIS Quarterly, 4*(4), 1–26.
- Vandevoorde, S., & Vanhoucke, M. (2006). A comparison of different project duration forecasting methods using earned value metrics. *International Journal of Project Management, 24*(4), 289–302.
- Vose, D. (2008). *Risk analysis: A quantitative guide* (3rd ed.). Wiley.
- Wauters, M., & Vanhoucke, M. (2016). A comparative study of Artificial Intelligence methods for project duration forecasting. *Expert Systems with Applications, 46*, 249–261.
- Wohlin, C., Runeson, P., Höst, M., Ohlsson, M. C., Regnell, B., & Wesslén, A. (2012). *Experimentation in software engineering*. Springer.
- Yao, S., Zhao, J., Yu, D., Du, N., Shafran, I., Narasimhan, K., & Cao, Y. (2023). ReAct: Synergizing reasoning and acting in language models. In *International Conference on Learning Representations (ICLR)*.
- Ziegler, A., Kalliamvakou, E., Li, X. A., Rice, A., Rifkin, D., Simister, S., Sittampalam, G., & Aftandilian, E. (2022). Productivity assessment of neural code completion. In *Proceedings of the 6th ACM SIGPLAN International Symposium on Machine Programming (MAPS '22)* (pp. 21–29). ACM.
- FaithJudge (2025) ve UniSumEval (2024) LLM sadakat değerlendirme çalışmaları (arXiv:2505.04847; arXiv:2409.19898). [KAYNAK DOĞRULANMALI: yazarlar ve tam başlıklar]
- Ticari ürün belgeleri (Microsoft Planner/Project, Jira, LiquidPlanner, Forecast, Asana, monday.com, ClickUp). [KAYNAK DOĞRULANMALI: satıcı sayfaları ve erişim tarihleri; bkz. `docs/team/TEST_PAZAR.md` ve git geçmişi `760f5df`]

---

## Ekler

### Ek A — Kullanıcı testi protokolü

Ayrıntılı protokol `docs/evaluation/KULLANICI_TESTI.md` dosyasındadır. Özet:

- **Katılımcılar:** 5–10 kişi, proje yönetimi veya yazılım geliştirme deneyimi olan gönüllüler. Her katılımcıya anonim kod
  (P01…) verilir. Kişisel veri tutulmaz ve uygulama içi onam metni onaylanır (D30).
- **Görev senaryosu:** demo projesi → sohbette öneri kartı uygulama/reddetme → "Eksik iş var mı?" → plan ve baseline → Durum
  sekmesi ve AI yorumu ("anlaşılır mıydı?") → what-if (iki karar sorusu: kişi eklenmeli mi, hedef tarih uzatılmalı mı; önce
  yalnız deterministik planla, sonra Monte Carlo ile).
- **Ölçüm:** uygulama içi anket (10 SUS maddesi + RQ3_1, RQ3_2, RQ4_1, RQ4_2; 1–5 ölçek), kayıtlardan kabul oranı ve what-if
  çalıştırmaları.
- **Analiz:** betimsel istatistik (n, ortalama, örneklem SS, min/maks, 4–5 payı), Bangor sıfatı, 68 ile karşılaştırma.
- Geliştirici ve deneme kayıtları katılımcı verisinden ayrılmalıdır: [SONUÇ BEKLENİYOR: test oturumlarının tarih aralığı ve
  ayrım yöntemi].

### Ek B — Dışa aktarılan veri dosyaları (CSV)

Tüm CSV'ler RFC 4180 uyumludur: virgül ayırıcı, CRLF, nokta ondalık (InvariantCulture), ISO tarih, UTC zaman, UTF-8 BOM (D30).

**Tablo B.1** CSV dosyaları

| Dosya | Sayfa | İçerik | RQ |
|---|---|---|---|
| `rq1-evm-ml-zaman-serisi` | `/evaluation` | project_id, project, date, baseline_id, percent_complete, spi, spi_t, cpi, health_score, evm_forecast, ml_probability, ml_forecast | RQ1 |
| `gecikme-deneyi-{tarih}.csv` | `/experiments` | Tek tohumlu deney: yöntem × kontrol noktası ölçütleri, özellik önemi, erken uyarı | RQ1, RQ2 |
| `cok-tohumlu-deney-{tarih}.csv` | `/experiments` | 5 tohum ort. ± SS (F1, AUC, doğruluk, erken uyarı) | RQ1 |
| `rq2-ozellik-onemi` | `/evaluation` | Son deneyin permütasyon önemi | RQ2 |
| `rq3-whatif-calistirmalari` | `/evaluation` | Senaryo başına satır: değişiklikler, plan/P50/P80, hedef, olasılık, P80 farkı, kaynak (Panel/AiTool) | RQ3 |
| `rq4-oneri-kabul` | `/evaluation` | Kaynak × araç × durum × toplu karar | RQ4 |
| `rq4-ai-kayitlari` | `/evaluation` | AiAnalysisLog: tür, model, prompt sürümü, şema geçerliliği, doğrulanamayan sayı, süre, token, koşu etiketi (yorum metni ve bağlam yok) | RQ4, sağlayıcı |
| `rq4-eksik-is-bulma-{tarih}.csv` | `/evaluation` | Tohumlanmış eksik iş deneyi: kural ve hibrit recall/precision | RQ4 |
| `saglayici-karsilastirma-{koşu}-{tarih}.csv` | `/evaluation` | Sağlayıcı × görev ölçütleri | Sağlayıcı |
| `anket-sus-likert` | `/evaluation` | Katılımcı başına satır: SUS maddeleri, SUS puanı, Likert maddeleri | SUS, RQ3, RQ4 |

### Ek C — Kesinleşmiş kararlar (D1–D31, kısa)

Tam metin `docs/DECISIONS.md` dosyasındadır.

**Tablo C.1** Kararların özeti

| # | Karar (kısa) |
|---|---|
| D1 | Ürün adı ProjectMind AI |
| D2 | Tek kullanıcı (proje yöneticisi); login/rol yok |
| D3 | Tek .NET 10 web uygulaması: Blazor Server; ayrı API yok |
| D4 | SQL Server + EF Core migrations |
| D5 | Katmanlar Domain / Application / Infrastructure / Web + Tests; mikroservis yok |
| D6 | Arayüz Blazor bileşenleri + kendi CSS; npm/SPA yok |
| D7 | ML uygulama içinde ML.NET ile; Python servis yok |
| D8 | LLM `IChatModel` soyutlaması; ana sağlayıcı Gemini, alternatif Claude; anahtar yoksa Mock |
| D9 | LLM yalnız açıklar/önerir; tüm sayılar deterministik kod veya ML'den |
| D10 | Karşılaştırma tabanı EVM / Earned Schedule |
| D11 | Sağlık skoru ağırlıkları AHP ile |
| D12 | What-if: Monte Carlo + Brooks etkisi |
| D13 | Yalnız Finish-to-Start bağımlılık |
| D14 | Kişiler projeye bağlı |
| D15 | Eksik iş önerisi hibrit (kural + LLM); onaysız eklenmez |
| D16 | Kod İngilizce, dokümantasyon Türkçe |
| D17 | Chat odaklı kullanım; AI araç çağrısı = öneri kartı; kabul/red loglanır |
| D18 | NumberGuard cevabı reddetmez, doğrulanamayan sayıları işaretler |
| D19 | EVM efor (saat) bazlı; baseline tam eforu saklar; sonradan eklenen işler kapsam büyümesi |
| D20 | Sağlık skoru bileşenleri (takvim, maliyet, kapsam, kaynak, risk); CR < 0,10; kritik uyarıda en fazla "Dikkat" |
| D21 | Gecikme modeli: sentetik veri, proje bazlı ayrım, gecikme = süre > plan × 1,10, karşılaştırma = PD/SPI(t) |
| D22 | What-if: üçgen(0,85; 1,0; 1,5), 500 tur, ortak rastgele sayılar; Brooks 4 hafta %50, mentorluk %25; ölçüt P80 |
| D23 | NumberGuard kanıtı ve eşleşme kuralları (önceki asistan cevapları kanıt değil; yüzde ve tarih kuralları) |
| D24 | EVM'de iptal = kapsam dışı; yeniden baseline'da kazanılmış/kalan ayrımı |
| D25 | AI analiz yorumları: araç JSON'u girdisi, JSON şema, C# doğrulama, denetim kaydı, önbellek |
| D26 | Hibrit eksik iş: kural önce, LLM şemalı ve sayısız, saat C#'ta, kaynak etiketi C#'ta |
| D27 | Silme → iptal; eksik baseline işi kapsam dışı; biten projede AT donar; BAC = 0 → endeksler yok |
| D28 | AI kayıt ve ölçüm bütünlüğü (kararlı kartlar korunur, toplu karar ayrımı, Türkçe sayı kanıtı, hibrit eleme, önbellekte sağlayıcı) |
| D29 | Demo projesi gerçek servislerin geriye alınmış saatle yeniden oynatılmasıyla; snapshot'ta ML tahmini |
| D30 | Değerlendirme altyapısı: `/evaluation`, anonim anket, SUS/Bangor, what-if kaydı, yorum geri bildirimi, CSV biçimi |
| D31 | Ölçüm koşucuları: sağlayıcı karşılaştırması (eval-v1), tohumlanmış eksik iş (recall-v1), RQ1 erken uyarı, çok tohumlu analiz; ölçüm verisi kullanıcı verisinden ayrı |
