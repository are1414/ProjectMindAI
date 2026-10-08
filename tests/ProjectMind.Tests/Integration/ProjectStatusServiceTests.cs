using Microsoft.EntityFrameworkCore;
using ProjectMind.Application.Ai;
using ProjectMind.Application.Analytics;
using ProjectMind.Application.People;
using ProjectMind.Application.Projects;
using ProjectMind.Application.WorkItems;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Tests.Integration;

public class ProjectStatusServiceTests : ServiceTestBase
{
    private readonly CancellationToken _ct = CancellationToken.None;

    [Fact]
    public async Task Progress_is_recorded_evm_is_computed_and_snapshot_is_upserted_daily()
    {
        // Proje 2–6 Kasım; tek kişi; A (16 s) → plan Pzt-Sal, B (24 s) → Çar-Cum.
        var project = await new ProjectService(Db).CreateAsync(new ProjectRequest
        {
            Name = "EVM", Type = ProjectType.WebApplication, Currency = "TRY", HoursPerDay = 8,
            StartDate = new DateOnly(2026, 11, 2), TargetEndDate = new DateOnly(2026, 11, 6)
        }, _ct);
        await new PersonService(Db).CreateAsync(project.Id,
            new PersonRequest { Name = "Ayşe", Skills = [Skill.Backend], WeeklyCapacityHours = 40, HourlyCost = 100 }, _ct);
        var items = new WorkItemService(Db);
        var a = await items.CreateAsync(project.Id, new WorkItemRequest { Name = "A", RequiredSkill = Skill.Backend, EstimatedHours = 16 }, _ct);
        var b = await items.CreateAsync(project.Id, new WorkItemRequest { Name = "B", RequiredSkill = Skill.Backend, EstimatedHours = 24 }, _ct);
        await new ProjectMind.Application.Dependencies.DependencyService(Db).CreateAsync(project.Id,
            new ProjectMind.Application.Dependencies.DependencyRequest { PredecessorId = a.Id, SuccessorId = b.Id }, _ct);
        Db.ChangeTracker.Clear();
        await NewScheduleService().ApplyAsync(project.Id, _ct);
        Db.ChangeTracker.Clear();

        // 4 Kasım: A bitti (20 s), B %25 (4 s) — EarnedValueTests'teki elle hesaplanan senaryo.
        var reqA = WorkItemRequest.From(await items.GetAsync(project.Id, a.Id, _ct));
        reqA.Status = WorkItemStatus.Done; reqA.PercentComplete = 100; reqA.ActualHours = 20;
        await items.UpdateAsync(project.Id, a.Id, reqA, _ct);
        var reqB = WorkItemRequest.From(await items.GetAsync(project.Id, b.Id, _ct));
        reqB.Status = WorkItemStatus.InProgress; reqB.PercentComplete = 25; reqB.ActualHours = 4;
        await items.UpdateAsync(project.Id, b.Id, reqB, _ct);
        Db.ChangeTracker.Clear();

        var nov4 = new DateTimeOffset(2026, 11, 4, 18, 0, 0, TimeSpan.Zero);
        var status = await NewStatusService(nov4).GetAsync(project.Id, _ct);

        Assert.Equal(2, await Db.StatusUpdates.CountAsync(_ct));
        Assert.NotNull(status.Evm);
        Assert.Equal(24, status.Evm.PlannedValue);
        Assert.Equal(22, status.Evm.EarnedValue);
        Assert.Equal(0.92m, status.Evm.SpiTime);
        Assert.Equal(new DateOnly(2026, 11, 9), status.Evm.ForecastFinish);
        Assert.Contains(status.Alerts, x => x.Title == "Hedef tarih riski");
        Assert.NotNull(status.Health.Score);
        Assert.NotEqual(HealthLevel.Good, status.Health.Level);   // kritik uyarı varken "İyi" gösterilmez

        // ML tahmini: EVM'den türetilen özellikler tahmin servisine gider.
        Assert.NotNull(status.Delay);
        Assert.Equal(0.92f, Predictor.LastFeatures!.SpiTime, 3);
        Assert.Equal(22f / 40f, Predictor.LastFeatures.PercentComplete, 3);

        await NewStatusService(nov4).GetAsync(project.Id, _ct);   // aynı gün ikinci kez → tek snapshot
        var snapshot = await Db.ProjectSnapshots.SingleAsync(_ct);
        Assert.Equal(22, snapshot.EarnedValue);
        Assert.Equal(new DateOnly(2026, 11, 4), snapshot.Date);
    }

    /// <summary>Proje 2 Kasım Pzt; tek backendci (8 s/gün, 100/saat); A (16 s) → B (40 s). İlk plan: A 2–3, B 4–10 Kasım.</summary>
    private async Task<(int ProjectId, int A, int B)> SeedTwoTasksAsync()
    {
        var project = await new ProjectService(Db).CreateAsync(new ProjectRequest
        {
            Name = "Yeniden baseline", Type = ProjectType.WebApplication, Currency = "TRY", HoursPerDay = 8,
            StartDate = new DateOnly(2026, 11, 2), TargetEndDate = new DateOnly(2026, 11, 20)
        }, _ct);
        await new PersonService(Db).CreateAsync(project.Id,
            new PersonRequest { Name = "Ayşe", Skills = [Skill.Backend], WeeklyCapacityHours = 40, HourlyCost = 100 }, _ct);
        var items = new WorkItemService(Db);
        var a = await items.CreateAsync(project.Id, new WorkItemRequest { Name = "A", RequiredSkill = Skill.Backend, EstimatedHours = 16 }, _ct);
        var b = await items.CreateAsync(project.Id, new WorkItemRequest { Name = "B", RequiredSkill = Skill.Backend, EstimatedHours = 40 }, _ct);
        await new ProjectMind.Application.Dependencies.DependencyService(Db).CreateAsync(project.Id,
            new ProjectMind.Application.Dependencies.DependencyRequest { PredecessorId = a.Id, SuccessorId = b.Id }, _ct);
        Db.ChangeTracker.Clear();
        await NewScheduleService().ApplyAsync(project.Id, _ct);
        Db.ChangeTracker.Clear();
        return (project.Id, a.Id, b.Id);
    }

    private async Task SetProgressAsync(int projectId, int id, WorkItemStatus status, int percent, decimal actual)
    {
        var items = new WorkItemService(Db);
        var req = WorkItemRequest.From(await items.GetAsync(projectId, id, _ct));
        req.Status = status; req.PercentComplete = percent; req.ActualHours = actual;
        await items.UpdateAsync(projectId, id, req, _ct);
        Db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Cancelled_baseline_work_is_descoped_so_finished_project_is_complete()
    {
        var (projectId, a, b) = await SeedTwoTasksAsync();
        await SetProgressAsync(projectId, a, WorkItemStatus.Done, 100, 16);
        await SetProgressAsync(projectId, b, WorkItemStatus.Cancelled, 0, 0);

        // 4 Kasım: baseline'da kalan tek iş A (16 s) bitti → BAC 16, PV 16, EV 16 → SPI 1, %100.
        var status = await NewStatusService(new DateTimeOffset(2026, 11, 4, 18, 0, 0, TimeSpan.Zero)).GetAsync(projectId, _ct);

        Assert.NotNull(status.Evm);
        Assert.Equal(16, status.Evm.BudgetAtCompletion);
        Assert.Equal(40, status.Evm.DescopedHours);
        Assert.Equal(1m, status.Evm.Spi);
        Assert.Equal(100, status.Evm.PercentComplete);
        Assert.Equal(0, status.ScopeGrowthPercent);
        Assert.DoesNotContain(status.Alerts, x => x.Title == "Hedef tarih riski");
    }

    [Fact]
    public async Task Rebaseline_keeps_done_dates_and_puts_earned_effort_before_new_plan()
    {
        var (projectId, a, b) = await SeedTwoTasksAsync();
        await SetProgressAsync(projectId, a, WorkItemStatus.Done, 100, 16);
        await SetProgressAsync(projectId, b, WorkItemStatus.InProgress, 50, 20);

        // 9 Kasım Pzt yeniden planla: kalan B 20 s → 9–11 Kasım. A bitti, tarihleri (2–3 Kasım) korunur; B'nin başlangıcı (4 Kasım) korunur.
        var nov9 = new DateTimeOffset(2026, 11, 9, 9, 0, 0, TimeSpan.Zero);
        await NewScheduleService(nov9).ApplyAsync(projectId, _ct);
        Db.ChangeTracker.Clear();

        var items = await Db.WorkItems.AsNoTracking().Where(w => w.ProjectId == projectId).ToDictionaryAsync(w => w.Name, _ct);
        Assert.Equal((new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 3)), (items["A"].PlannedStart, items["A"].PlannedEnd));
        Assert.Equal((new DateOnly(2026, 11, 4), new DateOnly(2026, 11, 11)), (items["B"].PlannedStart, items["B"].PlannedEnd));

        // Yeni baseline: A 16 s [2–3 Kas], B kazanılmış 20 s [4–6 Kas], B kalan 20 s [9–11 Kas]; toplam (tam efor) 56 s.
        var baseline = await Db.Baselines.Include(x => x.Items).OrderByDescending(x => x.Id).FirstAsync(_ct);
        Assert.Equal(56, baseline.TotalHours);
        Assert.Equal(
            [("A", 16m, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 3)),
             ("B", 20m, new DateOnly(2026, 11, 4), new DateOnly(2026, 11, 6)),
             ("B", 20m, new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 11))],
            baseline.Items.OrderBy(i => i.PlannedStart).Select(i => (i.Name, i.Hours, i.PlannedStart, i.PlannedEnd)));

        // 9 Kasım akşamı: PV eğrisi 8, 16, 22,67, 29,33, 36 | 42,67, 49,33, 56 → PV = 42,67; EV = 16 + 40 × %50 = 36;
        // AC = 36 (her iş bir kez). SPI = 0,84 (bugünün planlanan işi henüz yapılmadı); eski hesapta PV 29,33 → SPI 1,23 idi.
        var day0 = await NewStatusService(new DateTimeOffset(2026, 11, 9, 18, 0, 0, TimeSpan.Zero)).GetAsync(projectId, _ct);
        Assert.Equal(42.67m, day0.Evm!.PlannedValue);
        Assert.Equal(36, day0.Evm.EarnedValue);
        Assert.Equal(36, day0.Evm.ActualCost);
        Assert.Equal(0.84m, day0.Evm.Spi);
        Assert.Equal(1m, day0.Evm.Cpi);

        // Yeni plan aynen uygulanırsa (B 11 Kasım'da biter): PV = EV = 56 → SPI = SPI(t) = 1, tahmini bitiş 11 Kasım.
        await SetProgressAsync(projectId, b, WorkItemStatus.Done, 100, 40);
        var end = await NewStatusService(new DateTimeOffset(2026, 11, 11, 18, 0, 0, TimeSpan.Zero)).GetAsync(projectId, _ct);
        Assert.Equal(1m, end.Evm!.Spi);
        Assert.Equal(1m, end.Evm.SpiTime);
        Assert.Equal(new DateOnly(2026, 11, 11), end.Evm.ForecastFinish);
    }

    private async Task SetProgressOnAsync(DateTimeOffset day, int projectId, int id, WorkItemStatus status, int percent, decimal actual)
    {
        var items = new WorkItemService(Db, new FixedClock(day));
        var req = WorkItemRequest.From(await items.GetAsync(projectId, id, _ct));
        req.Status = status; req.PercentComplete = percent; req.ActualHours = actual;
        await items.UpdateAsync(projectId, id, req, _ct);
        Db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Finished_project_spi_t_is_frozen_at_completion_day()
    {
        // Tur 4a H1. Plan: A 2–3, B 4–10 Kasım (PD 7 iş günü). A 3 Kasım'da, B 10 Kasım'da (planda) bitti; durum 20 Kasım:
        // AT tamamlanma gününde (7. iş günü) donar → SPI(t) = 7 / 7 = 1; takvim uyarısı yok, tahmini bitiş 10 Kasım.
        var (projectId, a, b) = await SeedTwoTasksAsync();
        await SetProgressOnAsync(new DateTimeOffset(2026, 11, 3, 17, 0, 0, TimeSpan.Zero), projectId, a, WorkItemStatus.Done, 100, 16);
        await SetProgressOnAsync(new DateTimeOffset(2026, 11, 6, 17, 0, 0, TimeSpan.Zero), projectId, b, WorkItemStatus.InProgress, 60, 24);
        await SetProgressOnAsync(new DateTimeOffset(2026, 11, 10, 17, 0, 0, TimeSpan.Zero), projectId, b, WorkItemStatus.Done, 100, 40);
        await SetProgressOnAsync(new DateTimeOffset(2026, 11, 12, 17, 0, 0, TimeSpan.Zero), projectId, b, WorkItemStatus.Done, 100, 42);

        var status = await NewStatusService(new DateTimeOffset(2026, 11, 20, 18, 0, 0, TimeSpan.Zero)).GetAsync(projectId, _ct);

        Assert.Equal(7, status.Evm!.ActualTime);
        Assert.Equal(1m, status.Evm.SpiTime);
        Assert.Equal(new DateOnly(2026, 11, 10), status.Evm.ForecastFinish);
        Assert.DoesNotContain(status.Alerts, x => x.Title.Contains("SPI(t)"));
        Assert.Equal(1m, (await Db.ProjectSnapshots.SingleAsync(_ct)).SpiTime);
    }

    private async Task<AiActionResponse> ProposeRemovalAsync(int projectId, int workItemId)
    {
        var session = new ProjectMind.Domain.Entities.ChatSession { Title = "Sil", ProjectId = projectId };
        Db.ChatSessions.Add(session);
        await Db.SaveChangesAsync(_ct);
        return await NewActionService().ProposeAsync(session.Id, AiTools.RemoveWorkItem,
            System.Text.Json.JsonSerializer.SerializeToElement(new { workItemId }), _ct);
    }

    [Fact]
    public async Task Removing_baselined_work_with_progress_cancels_it_instead_of_deleting()
    {
        // Tur 4a H2 (TEST_PAZAR Q2). A bitti; B %25, 10 s harcandı. "B'yi sil" kartı iptal edeceğini söyler; uygulanınca B iptal
        // olur, harcanan saat ve geçmiş kalır. 4 Kasım: BAC 16 (B'nin 40 s'i kapsam dışı), EV 16, AC 16 + 10 = 26, %100.
        var (projectId, a, b) = await SeedTwoTasksAsync();
        await SetProgressAsync(projectId, a, WorkItemStatus.Done, 100, 16);
        await SetProgressAsync(projectId, b, WorkItemStatus.InProgress, 25, 10);

        var card = await ProposeRemovalAsync(projectId, b);
        Assert.StartsWith("İşi iptal et (silinmez): B", card.Summary);

        var applied = await NewActionService().ApplyAsync(card.Id, _ct);
        Assert.Equal(AiActionStatus.Applied, applied.Status);
        Assert.StartsWith("İş iptal edildi", applied.ResultMessage);
        Db.ChangeTracker.Clear();

        var itemB = await Db.WorkItems.AsNoTracking().SingleAsync(w => w.Id == b, _ct);
        Assert.Equal(WorkItemStatus.Cancelled, itemB.Status);
        Assert.Equal(10, itemB.ActualHours);
        Assert.Equal(2, await Db.StatusUpdates.CountAsync(u => u.WorkItemId == b, _ct));   // %25 kaydı + iptal kaydı

        var status = await NewStatusService(new DateTimeOffset(2026, 11, 4, 18, 0, 0, TimeSpan.Zero)).GetAsync(projectId, _ct);
        Assert.Equal(16, status.Evm!.BudgetAtCompletion);
        Assert.Equal(40, status.Evm.DescopedHours);
        Assert.Equal(16, status.Evm.EarnedValue);
        Assert.Equal(26, status.Evm.ActualCost);
        Assert.Equal(100, status.Evm.PercentComplete);

        // Bitmiş ve baseline'daki A için silme kartı oluşmaz (geçmiş korunur).
        await Assert.ThrowsAsync<ProjectMind.Application.Common.BusinessRuleException>(() => ProposeRemovalAsync(projectId, a));
    }

    [Fact]
    public async Task Deleted_baseline_work_is_treated_as_descoped()
    {
        // B hiç başlamadı → kart "İşi sil", iş silinir. Baseline'daki B satırı artık eşleşmez → kapsam dışı (iptal gibi):
        // 4 Kasım: BAC 16, EV 16 → SPI 1, %100 (eskiden BAC 56, %29 ve SPI < 1 kalıyordu).
        var (projectId, a, b) = await SeedTwoTasksAsync();
        await SetProgressAsync(projectId, a, WorkItemStatus.Done, 100, 16);

        var card = await ProposeRemovalAsync(projectId, b);
        Assert.Equal("İşi sil: B", card.Summary);
        Assert.Equal("İş silindi.", (await NewActionService().ApplyAsync(card.Id, _ct)).ResultMessage);
        Db.ChangeTracker.Clear();
        Assert.False(await Db.WorkItems.AnyAsync(w => w.Id == b, _ct));

        var status = await NewStatusService(new DateTimeOffset(2026, 11, 4, 18, 0, 0, TimeSpan.Zero)).GetAsync(projectId, _ct);
        Assert.Equal(16, status.Evm!.BudgetAtCompletion);
        Assert.Equal(40, status.Evm.DescopedHours);
        Assert.Equal(1m, status.Evm.Spi);
        Assert.Equal(100, status.Evm.PercentComplete);
        Assert.Equal(0, status.ScopeGrowthPercent);
    }
}
