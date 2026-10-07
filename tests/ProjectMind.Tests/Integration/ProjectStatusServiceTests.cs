using Microsoft.EntityFrameworkCore;
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
}
