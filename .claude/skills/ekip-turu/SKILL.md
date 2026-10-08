---
name: ekip-turu
description: ProjectMind AI geliştirme ekibinin bir turunu çalıştırır — analist ve test/pazar uzmanı paralel inceler, geliştirici bulguları uygular, lider doğrulayıp push eder. Kullanıcı "ekip turu", "ekibi çalıştır", "/ekip-turu" dediğinde kullan.
---

# Ekip turu

Rollerin tanımları `.claude/agents/` altında: `pm-analyst`, `pm-qa-market`, `pm-developer`. Ajan tipi bu oturumda
yüklenmemişse genel amaçlı ajana ilgili `.md` dosyasını okuyup ona göre çalışmasını söyle.

1. **İnceleme (paralel):** `pm-analyst` ve `pm-qa-market` ajanlarını aynı anda başlat. Analist, test/pazar raporunun
   önceki sürümünü de okur.
2. **Birleştirme (lider):** İki raporu oku. Test/pazar uzmanının [aktif faz] ve kritik/yüksek problemlerini analiz
   listesinde yoksa `docs/team/ANALIZ.md`'ye ekle. Aktif faz dışına çıkacak bir şey varsa kullanıcıya sor.
3. **Geliştirme:** `pm-developer` ajanını başlat (commit attribution satırlarını göreve ekle).
4. **Doğrulama (lider):** `dotnet build` ve `dotnet test`i kendin çalıştır, diff'i oku. Sorun varsa geliştiriciye geri gönder.
5. **Push** (tasarlanan dala) ve kullanıcıya kısa tur raporu: yapılanlar, bulunan problemler, pazar notları, sıradaki tur.
