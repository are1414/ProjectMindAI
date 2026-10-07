using Microsoft.EntityFrameworkCore;
using ProjectMind.Application.Ai;
using ProjectMind.Application.Dependencies;
using ProjectMind.Application.People;
using ProjectMind.Application.Projects;
using ProjectMind.Application.WorkItems;
using ProjectMind.Domain.Entities;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Tests.Integration;

public class ScheduleServiceTests : ServiceTestBase
{
    private readonly CancellationToken _ct = CancellationToken.None;

    /// <summary>
    /// Proje 2 Kasım 2026 Pazartesi başlar, hedef 6 Kasım Cuma. Bir backend (8 s/gün) ve bir testçi var.
    /// Backend (üst iş) → 1.1 API (16 s), 1.2 DB (8 s, %50 bitmiş → kalan 4 s); Test (8 s) Backend'e bağlı.
    /// </summary>
    private async Task<(int ProjectId, Dictionary<string, int> Ids)> SeedAsync()
    {
        var projects = new ProjectService(Db);
        var project = await projects.CreateAsync(new ProjectRequest
        {
            Name = "Plan testi", Type = ProjectType.WebApplication,
            StartDate = new DateOnly(2026, 11, 2), TargetEndDate = new DateOnly(2026, 11, 6), Currency = "TRY", HoursPerDay = 8
        }, _ct);

        var people = new PersonService(Db);
        await people.CreateAsync(project.Id, new PersonRequest { Name = "Ayşe", Skills = [Skill.Backend], WeeklyCapacityHours = 40, HourlyCost = 100 }, _ct);
        await people.CreateAsync(project.Id, new PersonRequest { Name = "Can", Skills = [Skill.Test], WeeklyCapacityHours = 40, HourlyCost = 50 }, _ct);

        var items = new WorkItemService(Db);
        var backend = await items.CreateAsync(project.Id, Work("Backend", Skill.Backend, 0), _ct);
        var api = await items.CreateAsync(project.Id, Work("API", Skill.Backend, 16, backend.Id), _ct);
        var dbItem = Work("DB", Skill.Backend, 8, backend.Id);
        dbItem.PercentComplete = 50;
        var db = await items.CreateAsync(project.Id, dbItem, _ct);
        var test = await items.CreateAsync(project.Id, Work("Test", Skill.Test, 8), _ct);

        // Üst işe bağımlılık: Test, Backend'in tüm alt işleri bitmeden başlayamaz.
        await new DependencyService(Db).CreateAsync(project.Id, new DependencyRequest { PredecessorId = backend.Id, SuccessorId = test.Id }, _ct);
        Db.ChangeTracker.Clear();
        return (project.Id, new() { ["Backend"] = backend.Id, ["API"] = api.Id, ["DB"] = db.Id, ["Test"] = test.Id });
    }

    private static WorkItemRequest Work(string name, Skill skill, decimal hours, int? parent = null) =>
        new() { Name = name, RequiredSkill = skill, EstimatedHours = hours, ParentId = parent };

    [Fact]
    public async Task Preview_expands_parent_dependency_and_uses_remaining_hours()
    {
        var (projectId, ids) = await SeedAsync();

        var p = await NewScheduleService().PreviewAsync(projectId, _ct);
        var a = p.Plan.Activities.ToDictionary(x => x.Name);

        // Ayşe: API 16 s (Pzt-Sal) ve DB kalan 4 s; kritik iş önce → API gün 0-1, DB gün 2; Test DB ve API'den sonra gün 3.
        Assert.Equal(3, p.Plan.Activities.Count);                 // üst iş planlanmaz
        Assert.Equal(4, a["DB"].Hours);                          // %50 tamamlanmış → kalan 4 saat
        Assert.True(a["Test"].StartDay > a["API"].FinishDay);
        Assert.True(a["Test"].StartDay > a["DB"].FinishDay);
        Assert.Equal(new DateOnly(2026, 11, 5), p.Plan.Finish);   // Perşembe
        Assert.Equal(-1, p.VarianceWorkdays);                     // hedef Cuma → 1 gün erken
        Assert.Equal(16 * 100 + 4 * 100 + 8 * 50, p.Plan.PlannedCost);  // API + DB (kalan) + Test
        Assert.Contains(a["Test"].Name, p.Plan.Activities.Where(x => x.IsCritical).Select(x => x.Name));
        _ = ids;
    }

    [Fact]
    public async Task Apply_writes_dates_assignees_parent_span_and_baseline()
    {
        var (projectId, ids) = await SeedAsync();

        await NewScheduleService().ApplyAsync(projectId, _ct);

        var items = await Db.WorkItems.AsNoTracking().Where(w => w.ProjectId == projectId).ToDictionaryAsync(w => w.Name, _ct);
        var ayse = await Db.People.AsNoTracking().SingleAsync(p => p.Name == "Ayşe", _ct);
        Assert.Equal(ayse.Id, items["API"].AssigneeId);
        Assert.Equal(new DateOnly(2026, 11, 2), items["API"].PlannedStart);
        Assert.Equal(items["API"].PlannedStart, items["Backend"].PlannedStart);   // üst iş = alt işlerin aralığı
        Assert.Equal(items["DB"].PlannedEnd, items["Backend"].PlannedEnd);

        var baseline = await Db.Baselines.Include(b => b.Items).SingleAsync(_ct);
        Assert.Equal(new DateOnly(2026, 11, 5), baseline.PlannedFinish);
        Assert.Equal(3, baseline.Items.Count);
        Assert.Equal(28, baseline.TotalHours);
        _ = ids;
    }

    [Fact]
    public async Task Missing_work_impact_is_simulated_without_changing_data()
    {
        var (projectId, _) = await SeedAsync();

        var (result, impact) = await new ProjectMind.Application.MissingWork.MissingWorkService(
            NewOverviewService(), NewScheduleService()).CheckAsync(projectId, _ct);

        Assert.NotEmpty(result.Missing);
        Assert.NotNull(impact);
        Assert.True(impact.ExtraWorkdays > 0);
        Assert.Equal(result.TotalDefaultHours, impact.ExtraHours);
        Assert.Equal(4, await Db.WorkItems.CountAsync(_ct));   // veri değişmedi
    }

    [Fact]
    public async Task Apply_schedule_card_from_chat_applies_plan()
    {
        var (projectId, _) = await SeedAsync();
        var session = new ChatSession { Title = "Plan", ProjectId = projectId };
        Db.ChatSessions.Add(session);
        await Db.SaveChangesAsync(_ct);
        var actions = NewActionService();

        var card = await actions.ProposeAsync(session.Id, AiTools.ApplySchedule, System.Text.Json.JsonSerializer.SerializeToElement(new { }), _ct);
        Assert.Equal("Otomatik planı uygula ve baseline kaydet: 02.11.2026 → 05.11.2026 · 3 iş · 28 saat", card.Summary);

        var applied = await actions.ApplyAsync(card.Id, _ct);
        Assert.Equal(AiActionStatus.Applied, applied.Status);
        Assert.Single(Db.Baselines);
    }
}
