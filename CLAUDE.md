# CLAUDE.md — ProjectMind AI Geliştirme Kuralları

Bu dosya, bu repoda çalışan **her AI kodlama ajanı** (Claude Code, Cursor, ChatGPT vb.) için bağlayıcı kurallardır.
Başka bir AI kullanıyorsan bu dosyanın içeriğini ilk mesaj olarak ver.

## 0. Her oturumun başında (zorunlu)

1. `docs/PLAN.md` oku → **aktif faz** ve o fazın açık maddeleri neler?
2. `docs/DECISIONS.md` oku → kesinleşmiş kararlar. Bunlarla çelişen hiçbir şey yapma.
3. `docs/PROGRESS.md` son kaydı oku → en son ne yapıldı, ne yarım kaldı?
4. Kullanıcının isteği aktif fazın dışındaysa: **önce bunu söyle**, onay almadan faz atlama.

## 1. Kapsam kuralları (konudan sapmama)

- Proje: **tek kullanıcılı** (proje yöneticisi) AI destekli proje planlama ve karar destek sistemi.
- Akış: Proje tanımı → İşler → Eksik iş önerisi → Kişiler → Otomatik plan → Durum girişi → Analiz/Tahmin → What-if → AI yorumu.
- **Sadece aktif fazın maddeleri** uygulanır. Aklına gelen iyi fikirleri uygulama; `docs/PLAN.md` → "Fikir Havuzu" bölümüne bir satır olarak yaz.
- `DECISIONS.md` içindeki "YAPILMAYACAKLAR" listesindeki hiçbir şeyi ekleme (login, mikroservis, Redis vb.).
- Yeni NuGet/npm/pip paketi eklemeden önce: neden gerekli, alternatifi neden yetmiyor — PROGRESS'e yaz. Gereksiz bağımlılık ekleme.
- Mevcut çalışan kodu gerekçesiz yeniden yazma / refactor etme.

## 2. Halüsinasyon önleme (kod yazarken)

- Var olduğundan emin olmadığın bir paket, sınıf, metot veya API kullanma. Emin değilsen derle ve gör.
- "Çalışıyor" demeden önce **kanıt**: `dotnet build` ve `dotnet test` çıktısı temiz olmalı. Çalıştırmadığın şeyi çalışıyor diye raporlama.
- Test yazmadan hesaplama kodu (CPM, EVM, skor, Monte Carlo, risk) commit etme. Hesaplamalar elle doğrulanabilir küçük örneklerle test edilir.
- Bilmediğin/belirsiz bir gereksinimde uydurma; makul bir varsayım yap ve PROGRESS'e "Varsayım:" olarak yaz.
- Dosya/klasör yapısını `docs/PLAN.md` → "Kod Yapısı" bölümüne göre koru.

## 3. Ürün içindeki AI kuralları (runtime)

- **LLM hiçbir sayıyı hesaplamaz.** Tarih, süre, maliyet, skor, olasılık → C# / ML.NET hesaplar.
- LLM'e veritabanı gönderilmez; `ContextBuilder` sadece gerekli özet JSON'u hazırlar.
- LLM cevabı her zaman **JSON şema** ile istenir ve doğrulanır. Geçersiz cevap kullanıcıya gösterilmez.
- LLM cevabındaki her sayı, gönderilen context'te bulunmalıdır (sayı doğrulayıcı). Bulunmayan sayı → cevap reddedilir.
- AI önerileri doğrudan veriyi değiştirmez; proje yöneticisi **Kabul/Reddet** der. Her öneri ve karar loglanır.
- API anahtarları sadece backend'de: git'e girmeyen `appsettings.Local.json`, `user-secrets` veya ortam değişkeni. `appsettings.json`'a ve repoya asla yazılmaz.
- API anahtarı yoksa sağlayıcı otomatik `Mock`'tur (ücretsiz). Testler gerçek API çağırmaz (senaryolu sahte model).

## 4. Teknik kurallar

- .NET 10, C#, nullable açık, async + `CancellationToken`.
- **Tek web uygulaması** (Blazor Server). Ayrı Web API, React/npm, Python servisi yok.
- Katmanlar: `Domain` ← `Application` ← `Infrastructure` ← `Web`. Blazor bileşeninde iş mantığı ve EF sorgusu yok; bileşen Application servislerini `AppScope` ile (her işlem kendi DbContext'iyle) çağırır.
- Veri değiştiren her AI yeteneği bir **araç (AiTools) + AiActionService** üzerinden öneri kartı olarak eklenir; araç veriyi doğrudan değiştirmez.
- Veritabanı: SQL Server + EF Core migrations. Şema değişikliği = yeni migration (eski migration düzenlenmez).
- Kod ve tanımlayıcılar İngilizce; dokümantasyon ve kullanıcıya dönük metinler Türkçe.
- Magic number yok; eşikler/ağırlıklar configuration'dan veya isimli sabitlerden gelir.

## 5. Bir iş ne zaman "bitti" sayılır (Definition of Done)

- [ ] Aktif fazın ilgili maddesi karşılandı, kapsam dışı ekleme yok
- [ ] `dotnet build` uyarısız/hatasız, `dotnet test` yeşil 
- [ ] Hesaplama içeren kodun birim testi var
- [ ] `docs/PLAN.md` checkbox'ı işaretlendi
- [ ] `docs/PROGRESS.md`'ye kayıt eklendi (ne yapıldı, varsayımlar, bilinen sorunlar, sıradaki adım)
- [ ] Anlamlı bir commit mesajı ile commit edildi

## 6. Oturum sonu raporu (her oturumda)

Kısa liste: yapılanlar · değişen dosyalar · yeni sayfalar · DB değişiklikleri · test sonucu · bilinen sorunlar · sıradaki adım.
