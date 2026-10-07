using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectMind.Application.Ai;
using ProjectMind.Application.Common;
using ProjectMind.Domain.Entities;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Tests.Integration;

public class AiActionServiceTests : ServiceTestBase
{
    private readonly CancellationToken _ct = CancellationToken.None;
    private readonly AiActionService _actions;
    private readonly int _sessionId;

    public AiActionServiceTests()
    {
        _actions = NewActionService();
        var session = new ChatSession { Title = "Test" };
        Db.ChatSessions.Add(session);
        Db.SaveChanges();
        _sessionId = session.Id;
    }

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    private Task<AiActionResponse> Propose(string tool, object input) =>
        _actions.ProposeAsync(_sessionId, tool, Json(input), _ct);

    private static readonly object Project = new
    {
        name = "Mobil Bankacılık", type = "MobileApplication",
        startDate = "2026-11-01", targetEndDate = "2027-04-30", budget = 8000000
    };

    [Fact]
    public async Task Proposals_do_not_change_data_until_applied()
    {
        var proposal = await Propose(AiTools.CreateProject, Project);

        Assert.Equal(AiActionStatus.Pending, proposal.Status);
        Assert.Equal("Proje oluştur: Mobil Bankacılık · 01.11.2026 → 30.04.2027 · 8.000.000 TRY", proposal.Summary);
        Assert.Empty(Db.Projects);
    }

    [Fact]
    public async Task Apply_all_runs_in_logical_order_and_links_session_to_project()
    {
        // Model sırayı karıştırsa da (iş → kişi → proje) toplu uygulama doğru sırayla çalışır.
        await Propose(AiTools.CreateProject, Project);
        await Propose(AiTools.AddWorkItem, new
        {
            name = "Backend API", phase = "Development", requiredSkill = "Backend",
            estimatedHours = 120, assigneeName = "Ayşe"
        });
        await Propose(AiTools.AddWorkItem, new { name = "Test", phase = "Test", requiredSkill = "Test", estimatedHours = 40 });
        await Propose(AiTools.AddDependency, new { predecessorName = "Backend API", successorName = "Test" });
        await Propose(AiTools.AddPerson, new { name = "Ayşe", skills = new[] { "Backend", "Database" }, weeklyCapacityHours = 40 });

        var results = await _actions.ApplyAllPendingAsync(_sessionId, _ct);

        Assert.All(results, r => Assert.Equal(AiActionStatus.Applied, r.Status));
        var session = await Db.ChatSessions.AsNoTracking().SingleAsync(s => s.Id == _sessionId);
        Assert.NotNull(session.ProjectId);
        Assert.Equal("Mobil Bankacılık", session.Title);

        var backend = await Db.WorkItems.AsNoTracking().SingleAsync(w => w.Name == "Backend API");
        var ayse = await Db.People.AsNoTracking().SingleAsync();
        Assert.Equal(ayse.Id, backend.AssigneeId);
        Assert.Equal(Skill.Backend | Skill.Database, ayse.Skills);
        Assert.Single(Db.WorkItemDependencies);
    }

    [Fact]
    public async Task Work_item_without_project_is_rejected_at_proposal_time()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Propose(AiTools.AddWorkItem, new { name = "X", phase = "Development", requiredSkill = "Backend", estimatedHours = 8 }));
        Assert.Contains("create_project", ex.Message);
    }

    [Fact]
    public async Task Invalid_enum_value_is_rejected_at_proposal_time()
    {
        await Propose(AiTools.CreateProject, Project);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Propose(AiTools.AddWorkItem, new { name = "X", phase = "Coding", requiredSkill = "Backend", estimatedHours = 8 }));
    }

    [Fact]
    public async Task Business_rule_violation_marks_action_failed_with_reason()
    {
        await Propose(AiTools.CreateProject, new { name = "Ters tarih", startDate = "2027-01-01", targetEndDate = "2026-01-01" });

        var result = (await _actions.ApplyAllPendingAsync(_sessionId, _ct)).Single();

        Assert.Equal(AiActionStatus.Failed, result.Status);
        Assert.Contains("Hedef bitiş", result.ResultMessage);
        Assert.Empty(Db.Projects);
    }

    [Fact]
    public async Task Update_work_item_changes_only_given_fields()
    {
        await Propose(AiTools.CreateProject, Project);
        await Propose(AiTools.AddWorkItem, new { name = "Analiz", phase = "Analysis", requiredSkill = "Analysis", estimatedHours = 24 });
        await _actions.ApplyAllPendingAsync(_sessionId, _ct);
        var item = await Db.WorkItems.AsNoTracking().SingleAsync();

        var proposal = await Propose(AiTools.UpdateWorkItem,
            new { workItemId = item.Id, status = "InProgress", percentComplete = 40, actualHours = 10 });
        Assert.Equal("İşi güncelle (Analiz): durum = Devam ediyor, ilerleme = %40, harcanan saat = 10", proposal.Summary);
        await _actions.ApplyAsync(proposal.Id, _ct);

        var updated = await Db.WorkItems.AsNoTracking().SingleAsync();
        Assert.Equal(WorkItemStatus.InProgress, updated.Status);
        Assert.Equal(40, updated.PercentComplete);
        Assert.Equal(24, updated.EstimatedHours);
    }

    [Fact]
    public async Task Sub_items_proposed_before_their_parent_are_still_applied_under_it()
    {
        await Propose(AiTools.CreateProject, Project);
        var child = await Propose(AiTools.AddWorkItem, new
        {
            name = "Login API", phase = "Development", requiredSkill = "Backend", estimatedHours = 16, parentName = "Backend"
        });
        await Propose(AiTools.AddWorkItem, new { name = "Backend", phase = "Development", requiredSkill = "Backend", estimatedHours = 0 });
        Assert.StartsWith("Alt iş ekle (Backend altına): Login API", child.Summary);

        var results = await _actions.ApplyAllPendingAsync(_sessionId, _ct);

        Assert.All(results, r => Assert.Equal(AiActionStatus.Applied, r.Status));
        var backend = await Db.WorkItems.AsNoTracking().SingleAsync(w => w.Name == "Backend");
        var login = await Db.WorkItems.AsNoTracking().SingleAsync(w => w.Name == "Login API");
        Assert.Equal(backend.Id, login.ParentId);
    }

    [Fact]
    public async Task Rejected_action_cannot_be_applied_later()
    {
        var proposal = await Propose(AiTools.CreateProject, Project);
        await _actions.RejectAsync(proposal.Id, _ct);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _actions.ApplyAsync(proposal.Id, _ct));
        Assert.Empty(Db.Projects);
    }
}
