using System.Text.Json;
using ProjectMind.Application.Ai;
using ProjectMind.Application.Common;
using ProjectMind.Application.People;
using ProjectMind.Application.Projects;
using ProjectMind.Application.WhatIf;
using ProjectMind.Application.WorkItems;
using ProjectMind.Domain.Entities;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Tests.Integration;

public class WhatIfServiceTests : ServiceTestBase
{
    private readonly CancellationToken _ct = CancellationToken.None;

    /// <summary>2 Kasım 2026 başlar, hedef 20 Kasım. Tek backendci; üç bağımsız 40 saatlik backend işi (15 iş günü).</summary>
    private async Task<int> SeedAsync()
    {
        var project = await new ProjectService(Db).CreateAsync(new ProjectRequest
        {
            Name = "What-if", Type = ProjectType.WebApplication, StartDate = new DateOnly(2026, 11, 2),
            TargetEndDate = new DateOnly(2026, 11, 20), Currency = "TRY", HoursPerDay = 8
        }, _ct);
        await new PersonService(Db).CreateAsync(project.Id,
            new PersonRequest { Name = "Ayşe", Skills = [Skill.Backend], WeeklyCapacityHours = 40, HourlyCost = 100 }, _ct);
        var items = new WorkItemService(Db);
        foreach (var name in new[] { "Ödeme", "Kampanya", "Raporlama" })
            await items.CreateAsync(project.Id, new WorkItemRequest { Name = name, RequiredSkill = Skill.Backend, EstimatedHours = 40 }, _ct);
        Db.ChangeTracker.Clear();
        return project.Id;
    }

    [Fact]
    public async Task Removing_scope_and_extending_deadline_improve_on_time_probability()
    {
        var projectId = await SeedAsync();
        var raporlama = Db.WorkItems.Single(w => w.Name == "Raporlama").Id;

        var result = await NewWhatIfService().CompareAsync(projectId,
        [
            new WhatIfScenario("Kapsam azalt", [new ScenarioChange(ScenarioChangeKind.RemoveWorkItem, WorkItemId: raporlama)]),
            new WhatIfScenario("Süre uzat", [new ScenarioChange(ScenarioChangeKind.ChangeDeadline, Date: new DateOnly(2026, 12, 31))])
        ], _ct);

        Assert.Equal(new DateOnly(2026, 11, 20), result.Current.Plan.Finish);   // 120 saat / 8 = 15 iş günü
        var scope = result.Scenarios[0];
        Assert.Equal(new DateOnly(2026, 11, 13), scope.Plan.Finish);            // 80 saat → 10 iş günü
        Assert.True(scope.P80DeltaWorkdays < 0);
        Assert.True(scope.MonteCarlo.OnTimeProbability > result.Current.MonteCarlo.OnTimeProbability);
        Assert.Equal(1.0, result.Scenarios[1].MonteCarlo.OnTimeProbability);
        Assert.Equal(0, result.Scenarios[1].P80DeltaWorkdays);
    }

    [Fact]
    public async Task Ai_tool_resolves_names_and_returns_comparison()
    {
        var projectId = await SeedAsync();
        var session = new ChatSession { Title = "t", ProjectId = projectId };
        Db.ChatSessions.Add(session);
        await Db.SaveChangesAsync(_ct);

        var handler = new ReadOnlyToolHandler(Db, null!, NewScheduleService(), NewStatusService(), NewWhatIfService());
        var input = JsonSerializer.SerializeToElement(new
        {
            scenarioName = "2 backend ekle", addPeopleCount = 2, addPeopleSkills = new[] { "Backend" }, capacityPersonName = "ayşe", newWeeklyHours = 32
        });
        var result = await handler.ExecuteAsync(session.Id, AiTools.SimulateWhatIf, input, _ct);

        Assert.False(result.IsError);
        using var doc = JsonDocument.Parse(result.Content);
        var scenario = doc.RootElement.GetProperty("scenario");
        Assert.Equal("2 backend ekle", scenario.GetProperty("name").GetString());
        Assert.Equal(2, scenario.GetProperty("changes").GetArrayLength());
        Assert.True(doc.RootElement.GetProperty("current").TryGetProperty("p80Finish", out _));

        var unknown = JsonSerializer.SerializeToElement(new { removePersonName = "Zeynep" });
        await Assert.ThrowsAsync<BusinessRuleException>(() => handler.ExecuteAsync(session.Id, AiTools.SimulateWhatIf, unknown, _ct));
    }
}
