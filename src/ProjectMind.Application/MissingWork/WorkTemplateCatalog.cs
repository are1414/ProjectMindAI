using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.MissingWork;

/// <summary>
/// Bir yazılım projesinde bulunması beklenen iş. Anahtar kelimeler Türkçe karakterler sadeleştirilmiş
/// (ı→i, ş→s…) ve küçük harfle yazılır; kısa (≤3 harf) kelimeler tam kelime, diğerleri kelime başı olarak eşleşir.
/// </summary>
public sealed record WorkTemplate(
    string Key,
    string Name,
    WorkPhase Phase,
    Skill Skill,
    decimal DefaultHours,
    string[] Keywords,
    ProjectType[]? OnlyFor = null,
    string[]? Before = null,
    string Reason = "");

/// <summary>
/// Kural tabanlı eksik iş tespiti için bilgi tabanı (WBS "%100 kuralı": plan işin tamamını kapsamalı).
/// Kod içinde tutulur: kullanıcı verisi değil, sürümlenen ve test edilen sabit bilgidir.
/// </summary>
public static class WorkTemplateCatalog
{
    public const string Version = "templates-v1";

    private static readonly ProjectType[] WithUserInterface =
        [ProjectType.WebApplication, ProjectType.MobileApplication, ProjectType.DesktopApplication];

    private static readonly ProjectType[] WithBackend =
        [ProjectType.WebApplication, ProjectType.MobileApplication, ProjectType.DesktopApplication, ProjectType.Integration];

    public static readonly IReadOnlyList<WorkTemplate> All =
    [
        new("requirements", "Gereksinim analizi", WorkPhase.Analysis, Skill.Analysis, 24,
            ["gereksinim", "analiz", "ihtiyac", "requirement", "kapsam", "is analiz"],
            Before: ["architecture", "ui-design"],
            Reason: "Kapsam netleşmeden yapılan geliştirme, sonradan değişiklik ve gecikme riskini artırır."),

        new("architecture", "Teknik mimari tasarımı", WorkPhase.Design, Skill.Backend, 16,
            ["mimari", "architecture", "teknik tasarim", "sistem tasarim"],
            Before: ["backend"],
            Reason: "Katmanlar, teknolojiler ve entegrasyon noktaları kodlamadan önce belirlenmelidir."),

        new("db-design", "Veritabanı tasarımı", WorkPhase.Design, Skill.Database, 12,
            ["veritabani tasarim", "db tasarim", "sema", "schema", "er diyagram", "veri modeli", "tablo tasarim"],
            OnlyFor: WithBackend, Before: ["db-setup", "backend"],
            Reason: "Veri modeli backend geliştirmenin temelidir."),

        new("ui-design", "Arayüz (UI/UX) tasarımı", WorkPhase.Design, Skill.Design, 24,
            ["ui", "ux", "arayuz tasarim", "ekran tasarim", "wireframe", "figma", "mockup", "prototip"],
            OnlyFor: WithUserInterface, Before: ["frontend", "mobile"],
            Reason: "Ekranlar tasarlanmadan frontend geliştirme yeniden işe yol açar."),

        new("db-setup", "Veritabanı kurulumu", WorkPhase.Infrastructure, Skill.Database, 8,
            ["veritabani kurulum", "veritabani olustur", "veritabani ortam", "db kurulum", "db setup", "database setup", "sql server kurulum"],
            OnlyFor: WithBackend, Before: ["backend"],
            Reason: "Backend geliştirme ve test için veritabanı ortamı hazır olmalıdır."),

        new("server-setup", "Sunucu / barındırma ortamı kurulumu", WorkPhase.Infrastructure, Skill.DevOps, 12,
            ["sunucu", "web server", "uygulama sunucu", "hosting", "barindirma", "iis", "azure", "aws", "bulut", "cloud", "ortam kurulum"],
            Before: ["go-live"],
            Reason: "Canlıya alma için test ve üretim ortamları önceden hazırlanmalıdır."),

        new("ci-cd", "CI/CD hattı (otomatik derleme ve dağıtım)", WorkPhase.Infrastructure, Skill.DevOps, 8,
            ["ci", "cd", "ci/cd", "pipeline", "devops", "otomatik dagitim", "build hatti"],
            Before: ["go-live"],
            Reason: "Elle dağıtım hata ve zaman kaybı riskini artırır."),

        new("backend", "Backend / API geliştirme", WorkPhase.Development, Skill.Backend, 80,
            ["backend", "api", "servis", "service", "sunucu tarafi", "web servis"],
            OnlyFor: WithBackend, Before: ["unit-test"],
            Reason: "Uygulamanın iş mantığı ve veri erişimi için gereklidir."),

        new("frontend", "Frontend / arayüz geliştirme", WorkPhase.Development, Skill.Frontend, 60,
            ["frontend", "arayuz gelistirme", "ekran gelistirme", "web sayfa", "ana sayfa", "react", "angular", "blazor", "razor"],
            OnlyFor: [ProjectType.WebApplication, ProjectType.DesktopApplication], Before: ["unit-test"],
            Reason: "Kullanıcının etkileşime gireceği ekranlar geliştirilmelidir."),

        new("mobile", "Mobil uygulama geliştirme", WorkPhase.Development, Skill.Frontend, 80,
            ["mobil", "mobile", "ios", "android", "flutter", "react native", "xamarin", "maui"],
            OnlyFor: [ProjectType.MobileApplication], Before: ["unit-test"],
            Reason: "Mobil istemci ayrı bir geliştirme işidir."),

        new("integration", "Dış sistem entegrasyonu", WorkPhase.Development, Skill.Backend, 40,
            ["entegrasyon", "integration", "harici sistem", "dis sistem", "webhook"],
            OnlyFor: [ProjectType.Integration], Before: ["unit-test"],
            Reason: "Entegrasyon projesinin ana işidir."),

        new("data-pipeline", "Veri aktarımı / ETL", WorkPhase.Development, Skill.Database, 40,
            ["etl", "veri aktarim", "data pipeline", "veri hatti", "veri toplama"],
            OnlyFor: [ProjectType.DataAnalytics], Before: ["reporting"],
            Reason: "Analiz için veri kaynaklardan toplanıp dönüştürülmelidir."),

        new("reporting", "Raporlama / gösterge paneli", WorkPhase.Development, Skill.Frontend, 32,
            ["rapor", "report", "dashboard", "gosterge paneli", "power bi", "grafik"],
            OnlyFor: [ProjectType.DataAnalytics], Before: ["unit-test"],
            Reason: "Veri analizinin çıktısı kullanıcıya sunulmalıdır."),

        new("security", "Kimlik doğrulama ve güvenlik", WorkPhase.Development, Skill.Backend, 24,
            ["guvenlik", "security", "kimlik dogrulama", "yetkilendirme", "login", "giris", "auth", "authentication", "kvkk"],
            OnlyFor: WithBackend, Before: ["unit-test"],
            Reason: "Kullanıcı verisi olan sistemlerde güvenlik ve yetkilendirme zorunludur."),

        new("unit-test", "Birim ve entegrasyon testleri", WorkPhase.Test, Skill.Test, 40,
            ["birim test", "unit test", "entegrasyon testi", "integration test", "test otomasyon", "testler",
             "yazilim testi", "test senaryo", "qa", "kalite guvence"],
            Before: ["uat"],
            Reason: "Hatalar canlıya çıkmadan yakalanmalıdır."),

        new("uat", "Kullanıcı kabul testi (UAT)", WorkPhase.Test, Skill.Test, 16,
            ["kabul test", "uat", "kullanici test", "acceptance"],
            Before: ["go-live"],
            Reason: "Teslimden önce iş birimi sistemi onaylamalıdır."),

        new("documentation", "Kullanıcı ve teknik dokümantasyon", WorkPhase.Closing, Skill.Documentation, 16,
            ["dokumantasyon", "dokuman", "documentation", "kilavuz", "kullanim kilavuzu", "readme", "teknik dokuman"],
            Reason: "Bakım ve kullanım için dokümantasyon gereklidir."),

        new("training", "Kullanıcı eğitimi", WorkPhase.Closing, Skill.ProjectManagement, 8,
            ["egitim", "training", "bilgilendirme"],
            Reason: "Kullanıcılar sistemi kullanmayı öğrenmelidir."),

        new("go-live", "Canlıya alma (deployment)", WorkPhase.Closing, Skill.DevOps, 8,
            ["canliya", "canli", "yayin", "deploy", "deployment", "go-live", "golive", "release", "teslim"],
            Reason: "Sistemin üretim ortamına alınması ayrı ve planlanması gereken bir iştir.")
    ];

    public static IEnumerable<WorkTemplate> For(ProjectType type) =>
        All.Where(t => t.OnlyFor is null || t.OnlyFor.Contains(type));
}
