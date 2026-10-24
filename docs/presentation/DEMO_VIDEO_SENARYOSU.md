# Demo Videosu Senaryosu (6–8 dakika)

ProjectMind AI'nın ürün akışını demo projesi "Mobile Banking Modernization" üzerinde gösteren ekran kaydı senaryosu.
Faz 10 (rapor ve sunum) için hazırlık belgesidir; video henüz çekilmedi.

Akış: Proje → İşler → Eksik iş önerisi → Ekip → Plan → Durum (EVM, sağlık, ML) → What-if → AI yorumu / sohbet →
Değerlendirme.

## Çekim öncesi hazırlık

- [ ] Gemini anahtarı **⚙ Ayarlar**'da kayıtlı; sohbet başlığında model adı görünüyor (Mock modda AI sahneleri çekilemez).
- [ ] Önceki demo silinmiş (demo sohbeti → **Projeyi sil**), ana sayfa açık. ML modeli önceden eğitilmiş (bir kez demo
  oluşturup silmek yeter; aksi hâlde ilk oluşturma birkaç saniye bekletir).
- [ ] Prova çekimi yapıldı; **AI yorumu üret** ve sohbet cevaplarının süresi ölçüldü (uzun beklemeler kurguda kesilir —
  kesilen yer videoda "⏩" ile belirtilir, dürüstlük için).
- [ ] Ekrandaki gerçek değerler prova sırasında not edildi; anlatımdaki `[…]` yerleri o değerlerle doldurulur.
  Demo tarihleri çekim gününe göre üretilir; README'deki örnekler (SPI(t) ≈ 0,88, sağlık "Dikkat", bir bloke iş,
  ≈ %10 kapsam artışı) yalnız yönlendiricidir.
- [ ] Çözünürlük 1920×1080, tarayıcı yakınlaştırması %110–125 (okunurluk), bildirimler kapalı, yer imleri çubuğu gizli.
- [ ] Mikrofon testi; anlatım ayrı kaydedilip sonradan eklenebilir.
- [ ] Sohbete kişisel veri yazılmayacak; anahtar içeren Ayarlar alanı kayıtta görünmeyecek (Ayarlar sahnesi yalnız
  "Şu an kullanılan" satırını gösterir).

## Sahne tablosu

| # | Zaman | Ekran | Eylem | Anlatım (Türkçe) |
|---|---|---|---|---|
| 1 | 0:00–0:30 | Ana sayfa (`/`) | Açılış; imleç başlığın üstünde durur. | "ProjectMind AI, yazılım projeleri için yapay zekâ destekli bir planlama ve karar destek sistemi. Tek kullanıcı vardır: proje yöneticisi. Temel ilkemiz şu: hesapları kod yapar, yapay zekâ yalnızca açıklar ve önerir; hiçbir öneri yöneticinin onayı olmadan veriye girmez." |
| 2 | 0:30–0:55 | Ana sayfa → sohbet | **Demo projesini oluştur** → demo sohbeti açılır. | "Gösterim için hazır bir demo projesi oluşturuyorum: dokuz hafta önce başlamış, altı kişilik ekibi olan bir mobil bankacılık modernizasyonu. Bu geçmiş, uygulamanın kendi servisleriyle hafta hafta yeniden oynatılarak üretildi; yani gördüğünüz her sayı gerçek iş kurallarından geçti." |
| 3 | 0:55–1:25 | Proje paneli: başlık, istatistikler, **İşler** sekmesi | **İşler** sekmesi → **Tümünü aç** → WBS numaralarını gezdirme → **Tümünü kapat**. Ardından **Ekip** sekmesi. | "Sol tarafta proje: başlangıç, hedef bitiş, bütçe ve toplam efor. İşler WBS hiyerarşisiyle tutuluyor; üst işin eforu ve ilerlemesi alt işlerden toplanıyor. Ekip sekmesinde her kişinin becerisi, haftalık kapasitesi ve saatlik maliyeti var. Planlama bu bilgilerle yapılıyor." |
| 4 | 1:25–2:00 | **Plan (Gantt)** sekmesi | Gantt'ı yavaşça kaydırma; kritik yol ve bolluk işaretlerini, kişi doluluk tablosunu gösterme. | "Plan, kritik yol yöntemi ve kaynak kısıtlı çizelgeleme ile otomatik çıkarılıyor: bağımlılıklar, beceriler ve kapasite birlikte dikkate alınıyor. Kırmızı işler kritik yolda; gecikirlerse proje gecikir. Projenin ilk günü bu plan uygulanıp baseline olarak kaydedildi; ilerleme ölçümü bu baseline'a göre yapılıyor." |
| 5 | 2:00–3:00 | **Durum** sekmesi | Proje sağlığı kartı → SPI(t), CPI, Tamamlanma, Tahmini bitiş → **Gecikme olasılığı (ML)** kartı → **Uyarılar** → S-eğrisi (imleçle bir noktanın üzerine gelme). | "Durum sekmesi projenin bugününü gösteriyor. Takvim performansı SPI(t) [değer]: birin altında, yani plandan geride. Earned Schedule'a göre tahmini bitiş [tarih], hedef [tarih]. Sağlık skoru, AHP ile ağırlıklandırılmış beş bileşenden geliyor: [skor], seviye '[Dikkat]'. Uyarılar nedenleri açıkça söylüyor: bloke bir iş, baseline'dan sonra eklenen kapsam ve hedef tarih riski. Sağdaki kart ise ML modelinin gecikme olasılığı: [yüzde]. Model sentetik projelerle eğitildi; EVM tahminiyle yan yana gösteriyoruz. S-eğrisinde planlanan, kazanılan ve harcanan eforun zaman içindeki seyri var." |
| 6 | 3:00–3:50 | Sohbet paneli | Mesaj yaz: "Proje hedef tarihe yetişir mi? Neden?" → **Gönder** → cevap (bekleme kesilebilir ⏩). | "Aynı bilgiyi sohbetten de sorabilirim. Asistan cevap verirken sayıları kendisi üretmiyor; salt okunur bir araçla sistemin hesapladığı durumu çağırıyor ve onu açıklıyor. Cevaptaki her sayı bu araç sonucunda aranıyor; bulunamayan bir sayı olursa cevabın altında 'Doğrulanamayan sayılar' uyarısı çıkıyor." (Uyarı çıkarsa: "Burada bir uyarı görüyoruz; yönetici bu değeri kontrol etmeli.") |
| 7 | 3:50–4:50 | Sohbet paneli: **Eksik iş var mı?** çipi | Çipe tıklama → kartlar gelir; **Şablon** ve **AI** rozetlerini gösterme → bir kartta **Uygula**, bir kartta **Vazgeç** → **İşler** sekmesinde eklenen işi gösterme. | "Planda unutulan işler var mı? Bu kontrol hibrit çalışıyor: önce proje tipine göre iş şablonları, ardından yapay zekâ şablonların kapsamadığı ek işleri öneriyor. Rozetler önerinin kaynağını gösteriyor: 'Şablon' kural katmanından, 'AI' dil modelinden. Demo bilerek CI/CD, kullanıcı kabul testi ve dokümantasyon işleri olmadan kuruldu; şablon katmanı bunları yakaladı. Her kartı tek tek değerlendiriyorum: bunu uyguluyorum, bunu gereksiz bulduğum için reddediyorum. Kabul ve red kaydediliyor; öneri kabul oranını kaynağa göre ölçüyoruz." |
| 8 | 4:50–5:50 | **What-if** sekmesi | "Senaryo 1" adını "+1 Backend" yap; **Kişi ekle**, 1 kişi, **Backend**, 40 saat/hafta → **Simüle et ve karşılaştır** → **Karşılaştırma** tablosunda "Mevcut plan" ile **P80 bitiş**, **Hedefe yetişme**, **P80 farkı (iş günü)** → **Bitiş tarihi olasılığı** grafiği. | "Peki ekibe bir backend geliştirici eklesem? What-if senaryoları veriyi değiştirmeden çalışıyor. Her senaryo kaynak kısıtlı planla ve Monte Carlo simülasyonuyla hesaplanıyor: iş eforlarındaki belirsizlik yüzlerce kez örnekleniyor. Yeni kişinin ısınma süresi ve ekibe getirdiği mentorluk yükü — Brooks etkisi — da modelde. Karşılaştırmayı P80 üzerinden yapıyoruz, yani yüzde seksen olasılıkla bitiş tarihi: mevcut planda [tarih], senaryoda [tarih], fark [n] iş günü; hedefe yetişme olasılığı [yüzde]'den [yüzde]'ye çıkıyor. Bu bilgiyle maliyet artışına değip değmeyeceğine yönetici karar veriyor." |
| 9 | 5:50–6:40 | What-if → **AI yorumu**; ardından **Durum** → **AI yorumu üret** | What-if'teki **AI yorumu üret** → yorumu kaydırarak gösterme (Önemli bulgular, Önerilen aksiyonlar, Varsayımlar ve sınırlar) → **Bu açıklama anlaşılır mıydı?** → **Evet** → puan **4** veya **5**. | "Sonuçları sade bir dille özetlemesi için AI yorumu üretiyorum. Yorum JSON şemasıyla isteniyor ve kod tarafında doğrulanıyor; geçersiz bir cevap kullanıcıya hiç gösterilmiyor. Girdisi yalnızca sistemin hesapladığı sonuçlar; bu yüzden sayılar doğrulanabiliyor. Altta, açıklamanın anlaşılır olup olmadığını soruyoruz; bu geri bildirim değerlendirmenin bir parçası." |
| 10 | 6:40–7:20 | **🧪 Deneyler (ML)** → **📊 Değerlendirme** | Deneyler sayfasında **Sonuçlar (test seti)** ve **Erken uyarı (RQ1, test seti)** tablolarını kaydırma (deney önceden çalıştırılmış olmalı). Sonra Değerlendirme sayfasında RQ başlıkları, **CSV indir** butonları ve **Anketi aç (katılımcı formu)**. | "Araştırma soruları için ölçümler uygulamanın içinde. Deneyler sayfasında ML modelini aynı sentetik test projelerinde EVM kuralıyla karşılaştırıyoruz: hangisi gecikmeyi daha çok ve daha erken yakalıyor, yanlış alarm oranı ne. Değerlendirme sayfası dört araştırma sorusunun verisini özetliyor ve her birini CSV olarak dışa aktarıyor; kullanıcı testi için anonim bir SUS anketi de burada." |
| 11 | 7:20–7:45 | Ana sayfa ya da Durum sekmesi (sabit) | Kapanış; ekran sabit. | "Özetle ProjectMind AI, planlama, izleme, tahmin ve senaryo analizini tek bir akışta birleştiriyor. Hesaplar deterministik ve test edilmiş kodda; yapay zekâ ise açıklayan ve öneren, ama son kararı her zaman yöneticiye bırakan bir yardımcı. İzlediğiniz için teşekkürler." |

Toplam: ≈ 7:45 (hedef aralık 6–8 dk). Süre aşılırsa önce sahne 4 (Gantt) ve sahne 10 (Deneyler) kısaltılır; 7 dakikanın
altında kalmak gerekirse sahne 6 (sohbet) 30 saniyeye indirilir.

## Kurgu notları

- AI bekleme süreleri kesilebilir; kesilen her yerde kısa bir "⏩" işareti gösterilir (sonuç hızı abartılmasın).
- Sahne 7'de **Bekleyen n öneriyi uygula** butonu kullanılmaz (toplu kabul, kabul oranı ölçümünü bozar).
- Sahne 4'te **Planı uygula ve baseline kaydet** butonuna basılmaz (yeni baseline alınır, Durum sekmesindeki hikâye değişir).
- Anlatımdaki `[…]` alanları yalnız ekranda görünen değerlerle doldurulur; ekranda olmayan bir sayı söylenmez.
- Ekranda hata (kırmızı kutu) çıkarsa sahne yeniden çekilir; düzeltilmiş hâli gerçekmiş gibi kurgulanmaz.
- Altyazı: Türkçe; terimler ilk geçişte açılır (SPI(t): takvim performans indeksi, P80: %80 olasılıklı bitiş tarihi).
- Çekim sonrası demo verisi kullanıcı testinde kullanılacaksa: demo sohbeti → **Projeyi sil** (çekimde verilen kart
  kararları kayıtta kalır; kullanıcı testi için ayrı veritabanı önerilir — `docs/evaluation/KULLANICI_TESTI.md` §3.1).
