using ProjectMind.Application.MissingWork;
using ProjectMind.Application.WorkItems;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Tests.Unit;

public class MissingWorkDetectorTests
{
    private static int _nextId;

    private static WorkItemResponse Item(string name, string? description = null) => new(
        ++_nextId, 1, name, description, WorkPhase.Development, Skill.Backend, Priority.Medium, 8,
        null, null, null, null, WorkItemStatus.NotStarted, 0, 0);

    /// <summary>Gerçekçi ve eksiksiz bir web projesi planı (kullanıcının yazabileceği doğal adlarla).</summary>
    private static List<WorkItemResponse> CompleteWebPlan() =>
    [
        Item("Gereksinimlerin toplanması"),
        Item("Sistem mimarisi"),
        Item("Veri modeli ve ER diyagramı"),
        Item("Ekran tasarımları (Figma)"),
        Item("SQL Server kurulumu"),
        Item("Azure ortamının hazırlanması"),
        Item("CI/CD pipeline"),
        Item("Sipariş API'si"),
        Item("React ile ana sayfa geliştirme"),
        Item("Login ve yetkilendirme"),
        Item("Birim testleri"),
        Item("Kullanıcı kabul testi"),
        Item("Kullanım kılavuzu"),
        Item("Son kullanıcı eğitimi"),
        Item("Canlıya geçiş")
    ];

    [Fact]
    public void Complete_plan_has_no_missing_work()
    {
        var result = MissingWorkDetector.Detect(ProjectType.WebApplication, CompleteWebPlan());
        Assert.Empty(result.Missing);
    }

    /// <summary>
    /// Değerlendirme (rapordaki RQ4 için): eksiksiz plandan her seferinde bir iş çıkarılır;
    /// dedektörün tam olarak o işin şablonunu eksik bulması beklenir. Recall ve precision 1.0 olmalı.
    /// </summary>
    [Fact]
    public void Leave_one_out_evaluation_has_full_recall_and_precision()
    {
        var plan = CompleteWebPlan();
        var expectedKeys = MissingWorkDetector.Detect(ProjectType.WebApplication, plan).Covered.Keys.ToHashSet();
        int truePositive = 0, falsePositive = 0, falseNegative = 0;

        for (var i = 0; i < plan.Count; i++)
        {
            var reduced = plan.Where((_, index) => index != i).ToList();
            var coveredBefore = MissingWorkDetector.Detect(ProjectType.WebApplication, plan).Covered;
            var removedKeys = coveredBefore.Where(c => c.Value.SequenceEqual([plan[i].Name])).Select(c => c.Key).ToHashSet();

            var found = MissingWorkDetector.Detect(ProjectType.WebApplication, reduced).Missing.Select(m => m.TemplateKey).ToHashSet();
            truePositive += found.Intersect(removedKeys).Count();
            falsePositive += found.Except(removedKeys).Count();
            falseNegative += removedKeys.Except(found).Count();
        }

        Assert.Equal(15, expectedKeys.Count);
        Assert.Equal(0, falsePositive);   // precision = 1.0
        Assert.Equal(0, falseNegative);   // recall = 1.0
        Assert.Equal(15, truePositive);
    }

    [Fact]
    public void Typical_developer_only_plan_reports_infrastructure_test_and_closing_work()
    {
        List<WorkItemResponse> plan = [Item("Backend"), Item("Frontend")];

        var missing = MissingWorkDetector.Detect(ProjectType.WebApplication, plan).Missing.Select(m => m.TemplateKey).ToList();

        Assert.Contains("db-setup", missing);
        Assert.Contains("server-setup", missing);
        Assert.Contains("unit-test", missing);
        Assert.Contains("documentation", missing);
        Assert.Contains("go-live", missing);
        Assert.DoesNotContain("backend", missing);
        Assert.DoesNotContain("mobile", missing);     // web projesinde mobil şablonu yok
    }

    [Fact]
    public void Missing_item_suggests_dependency_to_existing_work()
    {
        var backend = Item("Ödeme API");
        var result = MissingWorkDetector.Detect(ProjectType.WebApplication, [backend]);

        var dbSetup = result.Missing.Single(m => m.TemplateKey == "db-setup");
        Assert.Equal(["Ödeme API"], dbSetup.SuggestedSuccessors);
    }

    [Fact]
    public void Design_item_does_not_count_as_setup_and_short_keywords_need_whole_word()
    {
        var result = MissingWorkDetector.Detect(ProjectType.WebApplication, [Item("Veritabanı tasarımı"), Item("Build guide")]);

        Assert.Contains(result.Missing, m => m.TemplateKey == "db-setup");   // tasarım ≠ kurulum
        Assert.Contains(result.Missing, m => m.TemplateKey == "ui-design"); // "guide" içindeki "ui" sayılmaz
    }

    [Theory]
    [InlineData("Kullanıcı Kabul TESTİ", "kullanici kabul testi")]
    [InlineData("Şifre değişikliği & güvenlik", "sifre degisikligi guvenlik")]
    [InlineData("CI/CD hattı", "ci/cd hatti")]
    public void Normalize_handles_turkish_characters(string input, string expected) =>
        Assert.Equal(expected, MissingWorkDetector.Normalize(input));
}
