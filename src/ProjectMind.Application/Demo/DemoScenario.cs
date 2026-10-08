using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.Demo;

/// <summary>Demo ekibindeki bir kişi (saatlik maliyet TRY).</summary>
public sealed record DemoPerson(string Name, Skill[] Skills, decimal WeeklyCapacityHours, decimal HourlyCost);

/// <summary>
/// Demo işi. <paramref name="ParentKey"/> WBS üst işi; <paramref name="After"/> önce bitmesi gereken işler (üst iş de olabilir).
/// Üst işlerin saati alt işlerinin toplamıdır (EstimatedHours = 0 verilir, oluştururken hesaplanır).
/// </summary>
public sealed record DemoTask(
    string Key, string Name, string? ParentKey, WorkPhase Phase, Skill Skill, decimal Hours,
    Priority Priority = Priority.Medium, string[]? After = null, string? Description = null, string? Assignee = null);

/// <summary>
/// "Mobile Banking Modernization" demo senaryosunun sabit verisi (kod içinde, sürümlü; LLM üretmez).
/// Hikâye: analiz ve tasarım biraz uzadı, çekirdek bankacılık bağlantısı tedarikçi erişimi beklediği için bloke,
/// baseline'dan sonra QR ile ödeme kapsamı eklendi, akıllı saat eklentisi iş birimi kararıyla iptal edildi.
/// CI/CD hattı, kullanıcı kabul testi (UAT) ve dokümantasyon işleri bilerek yok ("Eksik iş var mı?" gösterimi için).
/// </summary>
public static class DemoScenario
{
    public const string Version = "demo-v1";
    public const string ProjectName = "Mobile Banking Modernization";
    public const string ChatTitle = "Demo · Mobile Banking Modernization";

    public const string Description =
        "Demo projesi. Bankanın mevcut mobil uygulaması yeni mimariyle (API ağ geçidi, modüler mobil istemci) yeniden " +
        "yazılıyor: güvenli giriş, hesaplar, para transferi, kart ve fatura ödeme. Proje birkaç haftadır sürüyor; " +
        "ilerleme haftalık olarak girildi.";

    public const ProjectType Type = ProjectType.MobileApplication;
    public const string Currency = "TRY";
    public const decimal HoursPerDay = 8;
    public const decimal Budget = 2_900_000m;

    /// <summary>Hedef bitiş: proje başlangıcından bu kadar hafta sonraki cuma.</summary>
    public const int TargetWeeks = 23;

    /// <summary>Hikâyedeki olayların haftaları (0 = ilk hafta).</summary>
    public const int AddedScopeWeek = 4;
    public const int BlockedFromWeek = 5;
    public const int CancelledWeek = 6;

    public const string BlockedTaskKey = "B4";
    public const string CancelledTaskKey = "M7";

    /// <summary>Baseline sonrası eklenen işlerin haftalık ilerlemesi (%).</summary>
    public const int AddedScopeWeeklyPercent = 10;

    public static readonly IReadOnlyList<DemoPerson> People =
    [
        new("Elif Kaya", [Skill.Analysis, Skill.ProjectManagement, Skill.Documentation], 40, 1_150),
        new("Murat Demir", [Skill.Backend, Skill.Database], 40, 1_350),
        new("Zeynep Arslan", [Skill.Backend, Skill.DevOps], 40, 1_400),
        new("Can Yıldız", [Skill.Frontend], 40, 1_250),
        new("Selin Aydın", [Skill.Design, Skill.Frontend], 40, 1_200),
        new("Burak Şahin", [Skill.Test], 40, 950)
    ];

    /// <summary>Baseline'daki işler (oluşturma sırası: üst iş alt işlerinden önce).</summary>
    public static readonly IReadOnlyList<DemoTask> Tasks =
    [
        new("P-AN", "Analiz ve kapsam", null, WorkPhase.Analysis, Skill.Analysis, 0),
        new("A1", "Mevcut mobil uygulamanın ve gereksinimlerin analizi", "P-AN", WorkPhase.Analysis, Skill.Analysis, 120,
            Priority.High),
        new("A2", "Regülasyon ve güvenlik gereksinimleri (BDDK, KVKK)", "P-AN", WorkPhase.Analysis, Skill.Analysis, 48,
            Priority.High),

        new("P-DS", "Tasarım", null, WorkPhase.Design, Skill.Design, 0),
        new("D1", "Teknik mimari tasarımı (API ağ geçidi, modüler istemci)", "P-DS", WorkPhase.Design, Skill.Backend, 64,
            Priority.High, ["A1"]),
        new("D2", "Veri modeli ve veritabanı tasarımı", "P-DS", WorkPhase.Design, Skill.Database, 48, After: ["A1"]),
        new("D3", "Mobil arayüz (UI/UX) tasarımı ve prototip", "P-DS", WorkPhase.Design, Skill.Design, 136,
            Priority.High, ["A1"]),

        new("P-IN", "Altyapı", null, WorkPhase.Infrastructure, Skill.DevOps, 0),
        new("I1", "Test ve üretim ortamlarının bulut üzerinde kurulumu", "P-IN", WorkPhase.Infrastructure, Skill.DevOps, 64,
            After: ["D1"]),
        new("I2", "Veritabanı kurulumu ve veri taşıma betikleri", "P-IN", WorkPhase.Infrastructure, Skill.Database, 56,
            After: ["D2"]),

        new("P-DV", "Geliştirme", null, WorkPhase.Development, Skill.Backend, 0),
        new("P-BE", "Backend API", "P-DV", WorkPhase.Development, Skill.Backend, 0),
        new("B1", "Kimlik doğrulama ve güvenli oturum (OTP, biyometrik)", "P-BE", WorkPhase.Development, Skill.Backend, 120,
            Priority.Critical, ["D1", "A2", "I2"]),
        new("B2", "Hesap ve bakiye servisleri", "P-BE", WorkPhase.Development, Skill.Backend, 112, Priority.High, ["D1", "I2"]),
        new("B3", "Para transferi (EFT, FAST, havale) servisleri", "P-BE", WorkPhase.Development, Skill.Backend, 160,
            Priority.High, ["B2"]),
        new("B4", "Kart işlemleri ve çekirdek bankacılık bağlantısı", "P-BE", WorkPhase.Development, Skill.Backend, 120,
            After: ["B2"],
            Description: "Çekirdek bankacılık sağlayıcısının test ortamı erişimi bekleniyor."),
        new("B5", "Fatura ödeme servisleri", "P-BE", WorkPhase.Development, Skill.Backend, 88, After: ["B2"]),

        new("P-MB", "Mobil uygulama", "P-DV", WorkPhase.Development, Skill.Frontend, 0),
        new("M1", "Giriş ve onboarding ekranları", "P-MB", WorkPhase.Development, Skill.Frontend, 96, Priority.High, ["D3"]),
        new("M2", "Hesaplar ve hareketler ekranları", "P-MB", WorkPhase.Development, Skill.Frontend, 112, Priority.High, ["D3"]),
        new("M3", "Para transferi ekranları", "P-MB", WorkPhase.Development, Skill.Frontend, 128, Priority.High, ["D3"]),
        new("M4", "Kart yönetimi ekranları", "P-MB", WorkPhase.Development, Skill.Frontend, 88, After: ["D3"]),
        new("M5", "Fatura ödeme ekranları", "P-MB", WorkPhase.Development, Skill.Frontend, 72, After: ["D3"]),
        new("M6", "Anlık bildirimler (push)", "P-MB", WorkPhase.Development, Skill.Frontend, 48, After: ["D3"]),
        new("M7", "Akıllı saat (Apple Watch) eklentisi", "P-MB", WorkPhase.Development, Skill.Frontend, 64, Priority.Low, ["D3"]),

        new("P-TS", "Test", null, WorkPhase.Test, Skill.Test, 0),
        new("T1", "Birim ve entegrasyon testleri", "P-TS", WorkPhase.Test, Skill.Test, 120, Priority.High, ["P-BE", "P-MB"]),
        new("T2", "Güvenlik ve sızma testi", "P-TS", WorkPhase.Test, Skill.Test, 56, Priority.High, ["T1"]),
        new("T3", "Performans ve yük testi", "P-TS", WorkPhase.Test, Skill.Test, 48, After: ["T1"]),

        new("P-CL", "Kapanış", null, WorkPhase.Closing, Skill.DevOps, 0),
        new("C1", "Mağaza yayını ve canlıya alma", "P-CL", WorkPhase.Closing, Skill.DevOps, 32, Priority.Critical, ["T2", "T3"]),
        new("C2", "Çağrı merkezi ekibi eğitimi", "P-CL", WorkPhase.Closing, Skill.ProjectManagement, 24, After: ["T2"])
    ];

    /// <summary>Baseline'dan sonra (hafta <see cref="AddedScopeWeek"/>) eklenen kapsam: QR ile ödeme, sadakat ekranı (≈ %10).</summary>
    public static readonly IReadOnlyList<DemoTask> AddedScope =
    [
        new("Q1", "QR kod ile ödeme servisi", "P-BE", WorkPhase.Development, Skill.Backend, 80, Priority.High, ["B2"],
            "İş biriminin baseline sonrası talebi.", Assignee: "Murat Demir"),
        new("Q2", "QR kod ile ödeme ekranı", "P-MB", WorkPhase.Development, Skill.Frontend, 64, Priority.High, ["D3"],
            "İş biriminin baseline sonrası talebi.", Assignee: "Can Yıldız"),
        new("Q3", "Sadakat puanları ve kampanyalar ekranı", "P-MB", WorkPhase.Development, Skill.Frontend, 64,
            After: ["D3"], Description: "Pazarlama biriminin baseline sonrası talebi.", Assignee: "Selin Aydın")
    ];
}
