# Geliştirme Ekibi (AI ajanları)

| Rol | Tanım dosyası | Ne yapar | Yazdığı yer |
|---|---|---|---|
| Analist | `.claude/agents/pm-analyst.md` | Kod/doküman/test incelemesi → önceliklendirilmiş iş listesi | `docs/team/ANALIZ.md` |
| Geliştirici | `.claude/agents/pm-developer.md` | [hata] + [aktif faz] maddelerini uygular, test yazar, commit eder | kod, PLAN, PROGRESS |
| Test ve Pazar uzmanı | `.claude/agents/pm-qa-market.md` | Kenar durum testleri, kullanıcı akışı, rakip ürün karşılaştırması, çözüm önerileri | `docs/team/TEST_PAZAR.md` |

**Tur:** Analist ‖ Test-Pazar (paralel) → lider birleştirir → Geliştirici → lider build/test doğrular → push.
Claude Code'da `/ekip-turu` yazarak (veya "ekip turu çalıştır" diyerek) başlatılır.

Ekip de `CLAUDE.md` kurallarına bağlıdır: sadece aktif faz, kapsam dışı fikirler Fikir Havuzu'na, kanıtsız "çalışıyor" yok.
