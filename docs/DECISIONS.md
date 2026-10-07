# Kesinleşmiş Kararlar

Bu kararlar ancak proje sahibinin açık onayıyla değişir. Değişirse eski kararın üstü çizilir, yenisi tarihle eklenir.

| # | Karar | Gerekçe |
|---|---|---|
| D1 | Ürün adı **ProjectMind AI** | — |
| D2 | **Tek kullanıcı** (proje yöneticisi). Login / rol / JWT yok. | Akademik değer katmıyor, süre yiyor. |
| D3 | Backend: **.NET 10**, ASP.NET Core Web API (controller tabanlı) | Proje sahibinin uzmanlık alanı. |
| D4 | Veritabanı: **Microsoft SQL Server**, EF Core migrations. Lokal ortam için `docker-compose` ile SQL Server. | Proje sahibi tercihi. |
| D5 | Katmanlar: Domain / Application / Infrastructure / Api + Tests. Mikroservis yok. | Sade Clean Architecture yeterli. |
| D6 | Frontend: React + TypeScript + Vite | Yaygın, hızlı. |
| D7 | ML: Python + scikit-learn, küçük FastAPI servisi (Faz 6'da) | Akademik standart araçlar. |
| D8 | LLM: `IAIService` soyutlaması; sağlayıcılar **Mock** (varsayılan), Ollama, Claude/OpenAI | Maliyet kontrolü, karşılaştırma imkânı. |
| D9 | LLM sadece **açıklar/önerir**; tüm sayısal hesaplar deterministik kod veya ML servisinde. | Halüsinasyonu önler, savunulabilir. |
| D10 | Baseline yöntem: **EVM / Earned Schedule**. ML modeli buna karşı kıyaslanır. | Mühendislik yönetimi bağlantısı. |
| D11 | Sağlık skoru ağırlıkları **AHP** ile belirlenir. | Ağırlıklar keyfi olmasın. |
| D12 | What-if senaryoları **Monte Carlo** + Brooks etkisi (yeni kişide ısınma süresi) | Gerçekçi senaryo sonuçları. |
| D13 | Bağımlılık türü sadece **Finish-to-Start**. | MVP için yeterli. |
| D14 | Kişiler projeye bağlıdır (proje bazlı ekip). | Basitlik. |
| D15 | Eksik iş önerisi **hibrit**: kural/şablon tabanlı + LLM. AI önerisi yönetici onayı olmadan eklenmez. | Açıklanabilirlik + kontrol. |
| D16 | Kod İngilizce, dokümantasyon Türkçe. | — |

## YAPILMAYACAKLAR (eklenmesi yasak)

Login/Identity/JWT · çoklu kullanıcı/rol · mikroservis · Kubernetes · Redis · Kafka · event sourcing · CQRS/MediatR framework'ü · Hangfire · e-posta/bildirim sistemi · Jira/Azure DevOps entegrasyonu · deep learning.
