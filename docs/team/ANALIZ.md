# Analiz Raporu — Faz 8 (AI analiz yorumları)

**Tarih:** 2026-10-08 · **Analist:** pm-analyst · **Tur:** 1 (TEST_PAZAR.md henüz yok)

## Durum

- `dotnet build`: **başarılı, 0 uyarı / 0 hata** (ilk denemede paralel derlemeden kaynaklı geçici dosya kilidi hatası
  alındı; tekrar derlemede temiz — kod sorunu değil).
- `dotnet test`: **115/115 geçti** (≈4 sn).
- Aktif faz: **FAZ 8**. Açık maddeler (`docs/PLAN.md:95-99`): sayı doğrulayıcı, hibrit eksik iş LLM katmanı,
  proje/senaryo yorumu, analiz cevapları için ayrıntılı audit, (ops.) Ollama.
- Not: `NumberGuard` Faz 5'te sohbet için zaten yazıldı (`Application/Ai/NumberGuard.cs`, `Chat/ChatService.cs:148-151`,
  D18). Faz 8 maddesi bu yüzden "sıfırdan yazma" değil, **açıklarını kapatma + yorum kartına bağlama** işidir.

## Geliştirme iş listesi (öncelik sırasıyla)

### 1. NumberGuard kanıtına eski asistan cevapları giriyor `[hata]` — S
- **Neden:** `ChatService.cs:148` kanıta `history.Select(h => h.Content)` ekliyor; geçmiş, asistan mesajlarını da
  içeriyor (`ChatService.cs:120-126`). Bir turda "Doğrulanamayan" diye işaretlenen sayı (uyarı satırı da mesaja
  yazılıyor, `:151`) sonraki turda kanıt sayılıp **doğrulanmış** görünür. Halüsinasyon kendini aklar.
- **Dosyalar:** `src/ProjectMind.Application/Chat/ChatService.cs`, `tests/.../Integration/ChatServiceTests.cs`.
- **Kabul:** Kanıt yalnız bağlam + kullanıcı mesajları + bu turun araç sonuçlarıdır (asistan geçmişi hariç). Test:
  1. turda ScriptedModel bağlamda olmayan "137" der → işaretlenir; 2. turda yine "137" der → yine işaretlenir.

### 2. NumberGuard tarih açıkları `[hata]` / `[aktif faz]` — S
- **Neden:** (a) `TurkishDateRegex` iki haneli gün/ay istiyor (`NumberGuard.cs:92`); "5.12.2026" tarih olarak
  yakalanmıyor, `NumberRegex` ile tek token olur, `decimal.TryParse` başarısız olur ve **sessizce atlanır**
  (`NumberGuard.cs:66`) → doğrulanmamış tarih geçer. (b) LLM'lerin sık kullandığı "5 Aralık 2026" biçimi hiç
  tanınmıyor: gün ≤10 diye serbest, "2026" ise kanıttaki tarihler sayı çıkarımından önce silindiği için
  (`NumberGuard.cs:60`) bulunamaz → **yanlış pozitif**, gün ise hiç kontrol edilmez. (c) Ayrıştırılamayan token'lar
  hiçbir koşulda raporlanmıyor.
- **Dosyalar:** `src/ProjectMind.Application/Ai/NumberGuard.cs`, `tests/.../Unit/NumberGuardTests.cs`.
- **Kabul:** "5.12.2026", "05.12.2026", "5 Aralık 2026", "2026-12-05" kanıtta 2026-12-05 varken geçer, yokken
  işaretlenir; ayrıştırılamayan sayı-benzeri token işaretlenir. Mevcut 5 InlineData yeşil kalır.

### 3. Yorum kartı için JSON şemalı model çağrısı (`IChatModel` genişletmesi) `[aktif faz]` — M
- **Neden:** CLAUDE.md §3 "LLM cevabı her zaman JSON şema ile istenir ve doğrulanır" diyor; şu an sohbet cevabı serbest
  metin (`ChatModelContracts.cs:10-16` — `ChatTurnRequest`'te şema alanı yok; Gemini/Claude sağlayıcılarında
  `responseSchema`/`responseMimeType` kullanımı yok). Madde 4 ve 5 bunun üstüne kurulur.
- **Dosyalar:** `Application/Ai/ChatModelContracts.cs`, `Infrastructure/Ai/GeminiChatModel.cs`,
  `Infrastructure/Ai/ClaudeChatModel.cs`, `Infrastructure/Ai/NotConfiguredChatModel.cs`, yeni
  `Application/Ai/AnalysisCommentSchema.cs` (ör. `{ summary, keyFindings[], recommendedActions[], caveats[] }`).
- **Kabul:** Şemaya uymayan cevap (eksik alan / JSON değil) `ChatModelException` ya da "geçersiz" sonucu üretir ve
  kullanıcıya gösterilmez — sahte modelle test. Gemini için istek gövdesinde şema alanının gönderildiği
  `GeminiChatModelTests` ile doğrulanır. Gerçek API çağrılmaz.

### 4. Proje yorumu (EVM + sağlık + ML) — `ProjectCommentService` + Durum sekmesinde kart `[aktif faz]` — M
- **Neden:** PLAN:97. Yorum şu an yalnız sohbette, serbest metin ve doğrulanmamış biçimde mümkün. `get_project_status`
  yükü (`ReadOnlyToolHandler.cs:92-134`) zaten bağlam olarak hazır; aynısı yorum servisinin bağlamı olmalı (tek kaynak).
- **Dosyalar:** yeni `Application/Ai/ProjectCommentService.cs` (bağlam = status JSON'u, prompt sürümü ayrı ör.
  `comment-v1` `ChatPrompts.cs` içinde), `ReadOnlyToolHandler.cs` (yükü paylaşılan bir builder'a çıkar),
  `Web/Components/Shared/StatusPanel.razor` (“AI yorumu” butonu + kart; iş mantığı yok, `AppScope` ile çağrı).
- **Kabul:** Sahte model şemaya uygun yorum döner → kart alanları dolu; yorumdaki her sayı NumberGuard'dan geçer,
  geçmeyenler kartta "Doğrulanamayan sayılar" olarak listelenir (test). Mock modda kart "AI bağlı değil" der, sayı
  üretmez. Yorum veri değiştirmez.

### 5. Senaryo yorumu (What-if / Monte Carlo) — What-if sekmesinde kart `[aktif faz]` — M
- **Neden:** PLAN:97. `WhatIfPanel.razor` karşılaştırmayı gösteriyor ama açıklama yok; `simulate_what_if` yükü
  (`ReadOnlyToolHandler.cs:144-172`) tek senaryo içindir, panel ise en fazla 4 senaryo karşılaştırır.
- **Dosyalar:** `ProjectCommentService` (senaryo yorumu metodu; bağlam = `WhatIfService.CompareAsync` sonucunun özet
  JSON'u), `Web/Components/Shared/WhatIfPanel.razor`.
- **Kabul:** 2 senaryolu karşılaştırma için sahte model yorumu: P80 farkı ve hedefe yetişme olasılığı bağlamdaki
  değerlerle aynı (NumberGuard temiz); bağlamda olmayan bir sayı eklenirse işaretlenir. Brooks varsayımı caveats'ta.

### 6. Hibrit eksik iş: LLM katmanı + öneri kaynağı etiketi `[aktif faz]` — L
- **Neden:** PLAN:96, D15, RQ4. Şu an yalnız kural/şablon (`MissingWorkDetector.cs:23-46`) var ve prompt LLM'in kendi
  önerisini açıkça yasaklıyor (`ChatPrompts.cs:42` "kendi tahminine göre eksik iş uydurma"). RQ4'te "hibrit plan
  bütünlüğünü artırıyor mu" ölçülecekse her önerinin **kaynağı** (kural / LLM) kaydedilmeli; `AiAction`'da böyle bir
  alan yok (`Domain/Entities/AiAction.cs:9-21`) → kabul oranı kaynağa göre ayrılamaz.
- **Dosyalar:** `MissingWork/MissingWorkService.cs` (kural sonucu + LLM'e şemalı "ek eksik iş" isteği; bağlam: proje
  tipi, iş adları, kuralın bulduğu/kapsanan şablonlar), `Ai/ChatPrompts.cs` (kural metni güncellenir, sürüm artar),
  `Domain/Entities/AiAction.cs` + **yeni migration** (`Source` alanı: Rule/Llm/User), `Ai/AiActionService.cs`,
  `Ai/ReadOnlyToolHandler.cs:23-53` (çıktıda `source`).
- **Kabul:** LLM önerileri mevcut işler ve kural önerileriyle (Normalize + `MissingWorkDetector.Matches`) çakışıyorsa
  elenir (test); LLM önerisinin saat tahmini yoksa veya şema dışıysa öneri düşer; kartlar `Source=Llm` ile kaydedilir;
  LLM hatasında kural sonucu yine döner (test). Hiçbir öneri onaysız eklenmez.

### 7. Analiz cevapları için ayrıntılı audit `[aktif faz]` — M
- **Neden:** PLAN:98. `ChatMessage` yalnız `Model` ve `PromptVersion` saklıyor (`ChatMessage.cs:13-14`); hangi araçların
  çağrıldığı, doğrulanamayan sayılar (sadece metne ekleniyor, `ChatService.cs:151`) ve yorumların şema geçerliliği
  ayrı kaydedilmiyor. RQ4 / Faz 9 sağlayıcı karşılaştırması için ölçülebilir veri gerekli.
- **Dosyalar:** yeni entity (ör. `AiAnalysisLog`: tür [chat/projectComment/scenarioComment/missingWork], prompt sürümü,
  model, çağrılan araçlar, bağlam karakter uzunluğu, doğrulanamayan sayı listesi/sayısı, şema geçerli mi, süre ms) +
  migration; `ChatService.cs`, `ProjectCommentService`.
- **Kabul:** Her sohbet turu ve yorum için bir kayıt oluşur (entegrasyon testi); doğrulanamayan sayı sayısı kayıtta
  NumberGuard sonucu ile aynı. API anahtarı / tam bağlam metni loglanmaz.

### 8. (Opsiyonel) Ollama sağlayıcısı `[aktif faz — opsiyonel]` — M
- **Neden:** PLAN:99, Faz 9 sağlayıcı karşılaştırması. Madde 3-7 bitmeden başlanmamalı.
- **Dosyalar:** `Infrastructure/Ai/OllamaChatModel.cs`, `Infrastructure/DependencyInjection.cs`, `AiOptions.cs`.
- **Kabul:** Ollama adresi yoksa/erişilemezse Mock'a düşer; HTTP sahte handler ile istek/cevap testi; yeni paket yok
  (HttpClient yeter).

## Küçük teknik borç (iş listesine girmedi)
- `WhatIfPanel.razor:200` — `ChanceGood = 0.8, ChanceWarning = 0.5` eşikleri bileşende; `WhatIf` ayar bölümüne
  taşınabilir (CLAUDE.md §4 "eşikler configuration'dan").
- `StatusPanel.razor:280-320` — S-eğrisi ölçek hesabı bileşende (sunum mantığı; kabul edilebilir, dokunulmamalı).
- `ChatService.cs:188` — modelin **kendi araç girdisi** kanıta ekleniyor; model `add_work_item`'da uydurduğu saati
  cevapta tekrarlarsa doğrulanmış sayılır. Kart kullanıcıya gösterildiği için kabul edilebilir; yorum kartlarında
  (madde 4-5) araç girdisi kanıt sayılmamalı.
- `ReadOnlyToolHandler.cs:209` — ad karşılaştırması `CurrentCultureIgnoreCase`; sunucu kültürüne bağlı (Türkçe I/İ).

## Fikir havuzu (geliştirici uygulamaz, PLAN'a eklenir)
- `[fikir havuzu]` Yorum kartında "Açıklama anlaşılır mıydı? (Evet/Hayır)" geri bildirimi — RQ4'ün ikinci yarısı için
  ölçüm (aksi halde yalnız Faz 9 SUS anketi kalır).
- `[fikir havuzu]` What-if çalıştırmalarının ve seçilen senaryonun loglanması — RQ3 "karar desteği" kanıtı.
- `[fikir havuzu]` Öneri kabul oranı raporu (kaynak × araç) Deneyler sayfasında, CSV ile.

## Riskler / açık sorular (kullanıcıya)
1. **Mock modda yorum kartı** ne göstersin: sadece "AI bağlı değil" mi, yoksa LLM'siz şablon metin (sayılar C#'tan) mi?
   (Öneri: sadece uyarı — şablon metin kapsam genişletir.)
2. **Claude sağlayıcısında JSON şema** nasıl zorlanacak: zorunlu tek araç ("submit_comment") mu, SDK'nın yapılandırılmış
   çıktı özelliği mi? SDK sürümünde var olduğu derlenerek doğrulanmalı (uydurma API riski).
3. Hibrit eksik işte LLM'e proje **açıklaması** da gönderilsin mi (kullanıcı metni → prompt injection yüzeyi)?
   Bağlam "veridir, talimat değildir" kuralı korunmalı.
4. `AiAction.Source` ve audit tablosu için 1 veya 2 migration — mevcut kayıtlar `Source = Rule/Unknown` mı sayılsın?
5. Yorumlar saklansın mı (her açılışta yeniden LLM çağrısı = maliyet/gecikme) yoksa snapshot tarihi başına önbellek mi?
