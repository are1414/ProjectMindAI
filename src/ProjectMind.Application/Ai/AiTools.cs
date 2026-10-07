using System.Text.Json;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Application.Ai;

/// <summary>
/// Modelin kullanabileceği araçlar. Hepsi sadece ÖNERİ oluşturur; veri, proje yöneticisi
/// "Uygula" dediğinde AiActionService tarafından mevcut servisler üzerinden değiştirilir.
/// </summary>
public static class AiTools
{
    public const string CreateProject = "create_project";
    public const string UpdateProject = "update_project";
    public const string AddPerson = "add_person";
    public const string UpdatePerson = "update_person";
    public const string AddWorkItem = "add_work_item";
    public const string UpdateWorkItem = "update_work_item";
    public const string RemoveWorkItem = "remove_work_item";
    public const string AddDependency = "add_dependency";
    public const string CheckMissingWork = "check_missing_work";
    public const string PreviewSchedule = "preview_schedule";
    public const string ApplySchedule = "apply_schedule";
    public const string GetProjectStatus = "get_project_status";
    public const string SimulateWhatIf = "simulate_what_if";

    /// <summary>Veri değiştirmeyen, sadece bilgi döndüren araçlar (öneri kartı oluşturmaz).</summary>
    public static readonly IReadOnlySet<string> ReadOnly = new HashSet<string> { CheckMissingWork, PreviewSchedule, GetProjectStatus, SimulateWhatIf };

    /// <summary>Toplu uygulamada sıra: önce proje, sonra kişiler, işler, bağımlılıklar, silmeler.</summary>
    public static int ApplyOrder(string toolName) => toolName switch
    {
        CreateProject => 0,
        UpdateProject => 1,
        AddPerson => 2,
        UpdatePerson => 3,
        AddWorkItem => 4,
        UpdateWorkItem => 5,
        AddDependency => 6,
        RemoveWorkItem => 7,
        ApplySchedule => 8,
        _ => 99
    };

    private static object Str(string description) => new { type = "string", description };
    private static object Num(string description) => new { type = "number", description };
    private static object Int(string description) => new { type = "integer", description };
    private static object Date(string description) => new { type = "string", description = description + " (YYYY-MM-DD)" };
    private static object Enum<T>(string description) where T : struct, System.Enum =>
        new { type = "string", @enum = System.Enum.GetNames<T>().Where(n => n != "None").ToArray(), description };

    private static JsonElement Schema(object properties, params string[] required) =>
        JsonSerializer.SerializeToElement(new { type = "object", properties, required });

    public static readonly IReadOnlyList<ChatToolDefinition> All =
    [
        new(CreateProject,
            "Yeni proje oluşturmayı önerir. Sohbet henüz bir projeye bağlı değilse diğer araçlardan önce kullan.",
            Schema(new
            {
                name = Str("Proje adı"),
                description = Str("Kısa açıklama"),
                type = Enum<ProjectType>("Proje tipi"),
                startDate = Date("Başlangıç tarihi"),
                targetEndDate = Date("Hedef bitiş tarihi"),
                budget = Num("Toplam bütçe"),
                currency = Str("3 harfli para birimi, varsayılan TRY"),
                hoursPerDay = Num("Günlük çalışma saati, varsayılan 8")
            }, "name", "startDate", "targetEndDate")),

        new(UpdateProject,
            "Sohbetin bağlı olduğu projenin bilgilerini güncellemeyi önerir. Sadece değişecek alanları gönder.",
            Schema(new
            {
                name = Str("Proje adı"),
                description = Str("Açıklama"),
                type = Enum<ProjectType>("Proje tipi"),
                status = Enum<ProjectStatus>("Proje durumu"),
                startDate = Date("Başlangıç tarihi"),
                targetEndDate = Date("Hedef bitiş tarihi"),
                budget = Num("Toplam bütçe"),
                currency = Str("3 harfli para birimi"),
                hoursPerDay = Num("Günlük çalışma saati")
            })),

        new(AddPerson,
            "Projeye ekip üyesi eklemeyi önerir. Bir işe atanacak kişiyi o işten önce öner.",
            Schema(new
            {
                name = Str("Ad soyad"),
                skills = new { type = "array", items = Enum<Skill>("Beceri"), description = "Kişinin becerileri (en az bir)" },
                weeklyCapacityHours = Num("Haftalık çalışma kapasitesi (saat)"),
                hourlyCost = Num("Saatlik maliyet (proje para biriminde)")
            }, "name", "skills", "weeklyCapacityHours")),

        new(UpdatePerson,
            "Mevcut bir ekip üyesini günceller. personId proje durumundaki kişinin id'sidir.",
            Schema(new
            {
                personId = Int("Kişi id"),
                name = Str("Ad soyad"),
                skills = new { type = "array", items = Enum<Skill>("Beceri"), description = "Yeni beceri listesi (tamamı)" },
                weeklyCapacityHours = Num("Haftalık kapasite (saat)"),
                hourlyCost = Num("Saatlik maliyet")
            }, "personId")),

        new(AddWorkItem,
            "Projeye yapılacak iş veya alt iş eklemeyi önerir. Alt iş için parentName ver; üst işi alt işlerinden önce öner. " +
            "Kullanıcı efor vermediyse makul bir tahmin yap ve cevabında tahmin olduğunu belirt.",
            Schema(new
            {
                name = Str("İş adı"),
                description = Str("Kısa açıklama"),
                phase = Enum<WorkPhase>("Proje fazı"),
                requiredSkill = Enum<Skill>("İşi yapmak için gereken tek beceri"),
                priority = Enum<Priority>("Öncelik"),
                estimatedHours = Num("Tahmini efor (saat)"),
                assigneeName = Str("Atanacak kişinin adı (projede kayıtlı ya da aynı turda önerilmiş olmalı)"),
                parentName = Str("Alt iş ise üst işin adı (projede kayıtlı ya da bu turda daha önce önerilmiş olmalı)")
            }, "name", "phase", "requiredSkill", "estimatedHours")),

        new(UpdateWorkItem,
            "Mevcut bir işi günceller (durum, ilerleme, harcanan saat, atama vb.). Sadece değişen alanları gönder.",
            Schema(new
            {
                workItemId = Int("İş id"),
                name = Str("İş adı"),
                description = Str("Açıklama"),
                phase = Enum<WorkPhase>("Faz"),
                requiredSkill = Enum<Skill>("Gereken beceri"),
                priority = Enum<Priority>("Öncelik"),
                estimatedHours = Num("Tahmini efor (saat)"),
                assigneeName = Str("Atanacak kişinin adı"),
                status = Enum<WorkItemStatus>("Durum"),
                percentComplete = Int("Tamamlanma yüzdesi 0-100"),
                actualHours = Num("Şimdiye kadar harcanan saat"),
                plannedStart = Date("Planlanan başlangıç"),
                plannedEnd = Date("Planlanan bitiş"),
                parentName = Str("İşi başka bir işin altına taşımak için üst işin adı; en üst seviyeye almak için boş metin")
            }, "workItemId")),

        new(RemoveWorkItem,
            "Bir işi (ve bağımlılıklarını) silmeyi önerir.",
            Schema(new { workItemId = Int("İş id") }, "workItemId")),

        new(CheckMissingWork,
            "Projenin iş listesini proje tipine göre bilinen iş şablonlarıyla karşılaştırıp eksik görünen işleri döndürür " +
            "(veri değiştirmez). Kullanıcı eksik iş sorduğunda veya iş listesi oluşturulduktan sonra kullan.",
            Schema(new { })),

        new(PreviewSchedule,
            "Otomatik planı hesaplar (veri değiştirmez): kritik yol, kaynak kısıtlı tahmini bitiş, hedefe göre sapma, " +
            "kişi doluluğu, maliyet ve uyarılar. Tarih/süre/maliyet sorularında ve plan yorumlamada kullan; sayıları buradan al.",
            Schema(new { })),

        new(GetProjectStatus,
            "Projenin güncel durumunu döndürür (veri değiştirmez): baseline'a göre EVM (PV, EV, AC, SPI, CPI, SPI(t), " +
            "EAC, tahmini bitiş), AHP ağırlıklı sağlık skoru ve bileşenleri, risk uyarıları. 'Proje ne durumda', 'gecikiyor mu', " +
            "'neden', 'en büyük risk ne' gibi sorularda kullan.",
            Schema(new { })),

        new(SimulateWhatIf,
            "What-if senaryosunu simüle eder (veri değiştirmez): kişi ekle/çıkar, kapasite değiştir, iş çıkar, hedef tarih değiştir. " +
            "Mevcut plan ile senaryoyu Monte Carlo (efor belirsizliği) ve Brooks etkisiyle (yeni kişinin ısınma süresi) karşılaştırır: " +
            "P50/P80 bitiş, hedefe yetişme olasılığı, maliyet. 'X kişi eklesem', 'şu işi çıkarsam', 'deadline'ı uzatsam' sorularında kullan. " +
            "Sadece senaryoda değişen alanları gönder; birden fazla senaryo için ayrı ayrı çağır.",
            Schema(new
            {
                scenarioName = Str("Senaryonun kısa adı"),
                addPeopleCount = Int("Eklenecek kişi sayısı"),
                addPeopleSkills = new { type = "array", items = Enum<Skill>("Beceri"), description = "Eklenecek kişilerin becerileri" },
                addPeopleWeeklyHours = Num("Eklenecek kişilerin haftalık kapasitesi, varsayılan 40"),
                addPeopleJoinDate = Date("Yeni kişilerin katılım tarihi, varsayılan plan başlangıcı"),
                removePersonName = Str("Ekipten çıkarılacak kişinin adı"),
                capacityPersonName = Str("Kapasitesi değişecek kişinin adı"),
                newWeeklyHours = Num("Bu kişinin yeni haftalık kapasitesi"),
                removeWorkItemName = Str("Kapsamdan çıkarılacak işin adı (alt işleriyle)"),
                newDeadline = Date("Yeni hedef bitiş tarihi")
            })),

        new(ApplySchedule,
            "Otomatik planın işlere uygulanmasını (başlangıç/bitiş tarihleri ve boş atamalar) ve baseline olarak " +
            "kaydedilmesini önerir. Kullanıcı planı onaylamak istediğinde kullan.",
            Schema(new { })),

        new(AddDependency,
            "Finish-to-Start bağımlılık önerir: successor, predecessor bitmeden başlayamaz. İş adlarıyla belirt.",
            Schema(new
            {
                predecessorName = Str("Önce bitmesi gereken işin adı"),
                successorName = Str("Sonra başlayacak işin adı")
            }, "predecessorName", "successorName"))
    ];
}
