using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectMind.Application.Analytics;
using ProjectMind.Application.Common;
using ProjectMind.Application.Demo;
using ProjectMind.Application.MissingWork;
using ProjectMind.Domain.Enums;
using Xunit.Abstractions;

namespace ProjectMind.Tests.Integration;

public class DemoSeederTests(ITestOutputHelper output) : ServiceTestBase
{
    private readonly CancellationToken _ct = CancellationToken.None;

    /// <summary>"Bugün": 8 Ekim 2026 Perşembe. Proje 9 hafta önceki pazartesi (3 Ağustos 2026) başlar.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 8);

    private DemoProjectSeeder NewSeeder(DateTimeOffset? now = null) =>
        new(Db, new FixedClock(now ?? Now), Options.Create(new HealthOptions()), Predictor, Options.Create(new DemoOptions()));

    [Fact]
    public async Task Demo_replays_weekly_history_with_a_mildly_late_story()
    {
        var result = await NewSeeder().CreateAsync(_ct);
        Db.ChangeTracker.Clear();

        var project = await Db.Projects.SingleAsync(_ct);
        Assert.Equal(DemoScenario.ProjectName, project.Name);
        Assert.Equal(new DateOnly(2026, 8, 3), project.StartDate);
        var session = await Db.ChatSessions.SingleAsync(_ct);
        Assert.Equal(result.ChatSessionId, session.Id);
        Assert.Equal(project.Id, session.ProjectId);
        Assert.Equal(DemoScenario.ChatTitle, session.Title);

        // Tek baseline (proje başında), 9 cuma + bugün = 10 snapshot, hepsi aynı baseline ile ve ML tahminiyle.
        var baseline = await Db.Baselines.SingleAsync(_ct);
        var snapshots = await Db.ProjectSnapshots.OrderBy(s => s.Date).ToListAsync(_ct);
        Assert.Equal(10, snapshots.Count);
        Assert.Equal(10, result.Snapshots);
        Assert.All(snapshots, s => Assert.Equal(baseline.Id, s.BaselineId));
        Assert.Equal(new DateOnly(2026, 8, 7), snapshots[0].Date);
        Assert.Equal(Today, snapshots[^1].Date);
        Assert.All(snapshots.Where(s => s.EarnedValue > 0), s => Assert.Equal(0.7m, s.DelayProbability));

        // Ekip ve iş yapısı
        Assert.Equal(DemoScenario.People.Count, await Db.People.CountAsync(_ct));
        var items = await Db.WorkItems.ToListAsync(_ct);
        Assert.InRange(items.Count, 25, 40);
        Assert.Single(items, w => w.Status == WorkItemStatus.Blocked);
        Assert.Single(items, w => w.Status == WorkItemStatus.Cancelled);
        Assert.Contains(items, w => w.Status == WorkItemStatus.Done);
        Assert.True(await Db.StatusUpdates.CountAsync(_ct) > items.Count);

        var status = await NewStatusService(Now).GetAsync(project.Id, _ct);
        output.WriteLine($"SPI {status.Evm!.Spi} SPI(t) {status.Evm.SpiTime} CPI {status.Evm.Cpi} %{status.Evm.PercentComplete} " +
                         $"health {status.Health.Score} {status.Health.Level} scope {status.ScopeGrowthPercent} " +
                         $"forecast {status.Evm.ForecastFinish} planned {status.Evm.PlannedFinish} target {project.TargetEndDate} " +
                         $"budget {project.Budget} bacCost {status.Evm.BudgetAtCompletionCost} eacCost {status.Evm.EstimateAtCompletionCost}");
        foreach (var s in snapshots)
            output.WriteLine($"{s.Date} PV {s.PlannedValue} EV {s.EarnedValue} AC {s.ActualCost} SPI(t) {s.SpiTime} H {s.HealthScore}");
        foreach (var a in status.Alerts)
            output.WriteLine($"{a.Severity} {a.Title}: {a.Detail}");

        Assert.InRange(status.Evm.SpiTime!.Value, 0.85m, 0.90m);
        Assert.True(status.ScopeGrowthPercent > 0);
        Assert.True(status.Evm.DescopedHours > 0);
        Assert.NotNull(status.Delay);
        Assert.Contains(status.Alerts, a => a.Title.Contains("Bloke", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(status.Alerts, a => a.Title.Contains("Kapsam", StringComparison.OrdinalIgnoreCase));

        // "Eksik iş var mı?" kural katmanı bilerek bırakılan işleri bulur (CI/CD, UAT, dokümantasyon).
        var overview = await NewOverviewService().GetAsync(project.Id, _ct);
        var missing = MissingWorkDetector.Detect(project.Type, overview.WorkItems);
        Assert.Equal(["ci-cd", "uat", "documentation"], missing.Missing.Select(m => m.TemplateKey).ToArray());
    }

    [Fact]
    public async Task Demo_is_deterministic_and_is_not_created_twice()
    {
        await NewSeeder().CreateAsync(_ct);
        var first = await Db.ProjectSnapshots.OrderBy(s => s.Date)
            .Select(s => new { s.Date, s.PlannedValue, s.EarnedValue, s.ActualCost, s.SpiTime }).ToListAsync(_ct);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => NewSeeder().CreateAsync(_ct));
        Assert.Equal(DemoProjectSeeder.AlreadyExistsMessage, error.Message);
        Assert.Equal(1, await Db.Projects.CountAsync(_ct));
        Assert.NotNull(await NewSeeder().FindExistingAsync(_ct));

        // Projeyi silip aynı günde yeniden üretince EVM değerleri aynı (uygulamada her işlem kendi DbContext'iyle çalışır).
        Db.ChangeTracker.Clear();
        await new ProjectMind.Application.Projects.ProjectService(Db).DeleteAsync((await Db.Projects.SingleAsync(_ct)).Id, _ct);
        Db.ChangeTracker.Clear();
        await NewSeeder().CreateAsync(_ct);
        var second = await Db.ProjectSnapshots.OrderBy(s => s.Date)
            .Select(s => new { s.Date, s.PlannedValue, s.EarnedValue, s.ActualCost, s.SpiTime }).ToListAsync(_ct);
        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(0, 9, 4, 0.8, 40)]     // efektif 5 × 0,8 = 4 gün / 10 → %40
    [InlineData(0, 9, 4, 1.0, 50)]
    [InlineData(5, 9, 4, 1.0, 0)]      // pencere henüz başlamadı
    [InlineData(5, 9, 6, 1.0, 40)]     // efektif 7 gün: 2/5 → %40
    [InlineData(0, 9, 9, 1.0, 100)]
    [InlineData(0, 9, 11, 0.9, 100)]   // 12 × 0,9 = 10,8 ≥ 10
    [InlineData(0, 2, 1, 0.9, 60)]     // 1,8 / 3 = 0,6
    public void PercentAt_spreads_progress_over_the_baseline_window(int start, int end, int status, double velocity, int expected) =>
        Assert.Equal(expected, DemoProgress.PercentAt(start, end, status, velocity));

    [Fact]
    public void ActualHours_scales_earned_hours_by_cost_factor()
    {
        Assert.Equal(55, DemoProgress.ActualHours(100, 50, 1.1));       // 50 × 1,1
        Assert.Equal(27, DemoProgress.ActualHours(48, 50, 1.125));      // 24 × 1,125 = 27
        Assert.Equal(14, DemoProgress.ActualHours(25, 50, 1.1));        // 13,75 → 14
        Assert.Equal(0, DemoProgress.ActualHours(80, 0, 1.2));
    }

    [Theory]
    [InlineData(5)]    // pazartesi
    [InlineData(9)]    // cuma
    [InlineData(10)]   // cumartesi (bu haftanın cuması da geçmiş: 11 snapshot)
    public async Task Demo_story_holds_on_any_weekday(int day)
    {
        var now = new DateTimeOffset(2026, 10, day, 10, 0, 0, TimeSpan.Zero);
        var result = await NewSeeder(now).CreateAsync(_ct);
        Db.ChangeTracker.Clear();

        var status = await NewStatusService(now).GetAsync(result.ProjectId, _ct);
        Assert.InRange(status.Evm!.SpiTime!.Value, 0.85m, 0.90m);
        Assert.InRange(await Db.ProjectSnapshots.CountAsync(_ct), 10, 11);
        Assert.Equal(new DateOnly(2026, 8, 3), (await Db.Projects.SingleAsync(_ct)).StartDate);
    }
}
