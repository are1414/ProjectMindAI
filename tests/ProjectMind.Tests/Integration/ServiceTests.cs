using ProjectMind.Application.Common;
using ProjectMind.Application.Dependencies;
using ProjectMind.Application.People;
using ProjectMind.Application.Projects;
using ProjectMind.Application.WorkItems;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Tests.Integration;

public class ServiceTests : ServiceTestBase
{
    private readonly ProjectService _projects;
    private readonly PersonService _people;
    private readonly WorkItemService _workItems;
    private readonly DependencyService _dependencies;
    private readonly CancellationToken _ct = CancellationToken.None;

    public ServiceTests()
    {
        _projects = new ProjectService(Db);
        _people = new PersonService(Db);
        _workItems = new WorkItemService(Db);
        _dependencies = new DependencyService(Db);
    }

    [Fact]
    public async Task Full_flow_project_people_work_items_dependencies()
    {
        var project = await _projects.CreateAsync(NewProject(), _ct);
        Assert.Equal("TRY", project.Currency);

        var person = await _people.CreateAsync(project.Id,
            new PersonRequest { Name = "Ayşe", Skills = [Skill.Backend, Skill.Database], WeeklyCapacityHours = 40 }, _ct);
        Assert.Equal(Skill.Backend | Skill.Database, person.Skills);

        var db = await _workItems.CreateAsync(project.Id, WorkItem("Veritabanı kurulumu", Skill.Database, person.Id), _ct);
        var api = await _workItems.CreateAsync(project.Id, WorkItem("Backend", Skill.Backend, person.Id), _ct);

        await _dependencies.CreateAsync(project.Id, new DependencyRequest { PredecessorId = db.Id, SuccessorId = api.Id }, _ct);

        // Ters yön döngü oluşturur
        var cycle = await Assert.ThrowsAsync<BusinessRuleException>(() => _dependencies.CreateAsync(project.Id,
            new DependencyRequest { PredecessorId = api.Id, SuccessorId = db.Id }, _ct));
        Assert.Contains("döngü", cycle.Message);

        // Kişi silinince işler kalır, atama boşalır
        await _people.DeleteAsync(project.Id, person.Id, _ct);
        Db.ChangeTracker.Clear();
        Assert.All(await _workItems.ListAsync(project.Id, _ct), w => Assert.Null(w.AssigneeId));

        // İş silinince bağımlılıkları da silinir
        await _workItems.DeleteAsync(project.Id, db.Id, _ct);
        Assert.Empty(await _dependencies.ListAsync(project.Id, _ct));

        // Proje silinince her şey gider
        await _projects.DeleteAsync(project.Id, _ct);
        await Assert.ThrowsAsync<NotFoundException>(() => _projects.GetAsync(project.Id, _ct));
        Assert.Empty(Db.WorkItems);
    }

    [Fact]
    public async Task Duplicate_dependency_is_rejected()
    {
        var project = await _projects.CreateAsync(NewProject(), _ct);
        var a = await _workItems.CreateAsync(project.Id, WorkItem("A", Skill.Analysis, null), _ct);
        var b = await _workItems.CreateAsync(project.Id, WorkItem("B", Skill.Design, null), _ct);
        var request = new DependencyRequest { PredecessorId = a.Id, SuccessorId = b.Id };

        await _dependencies.CreateAsync(project.Id, request, _ct);
        await Assert.ThrowsAsync<BusinessRuleException>(() => _dependencies.CreateAsync(project.Id, request, _ct));
    }

    [Fact]
    public async Task Project_with_end_before_start_is_rejected()
    {
        var request = NewProject();
        request.TargetEndDate = request.StartDate.AddDays(-1);
        await Assert.ThrowsAsync<BusinessRuleException>(() => _projects.CreateAsync(request, _ct));
    }

    [Fact]
    public async Task Person_without_skills_is_rejected()
    {
        var project = await _projects.CreateAsync(NewProject(), _ct);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _people.CreateAsync(project.Id, new PersonRequest { Name = "Boş" }, _ct));
    }

    [Fact]
    public async Task Done_work_item_must_be_100_percent()
    {
        var project = await _projects.CreateAsync(NewProject(), _ct);
        var request = WorkItem("Bitti ama %50", Skill.Test, null);
        request.Status = WorkItemStatus.Done;
        request.PercentComplete = 50;
        await Assert.ThrowsAsync<BusinessRuleException>(() => _workItems.CreateAsync(project.Id, request, _ct));
    }

    [Fact]
    public async Task Assignee_from_another_project_is_rejected()
    {
        var p1 = await _projects.CreateAsync(NewProject(), _ct);
        var p2 = await _projects.CreateAsync(NewProject(), _ct);
        var other = await _people.CreateAsync(p2.Id,
            new PersonRequest { Name = "Mehmet", Skills = [Skill.Frontend], WeeklyCapacityHours = 40 }, _ct);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _workItems.CreateAsync(p1.Id, WorkItem("Ekran", Skill.Frontend, other.Id), _ct));
    }

    [Fact]
    public async Task Sub_items_cannot_form_cycle_and_are_deleted_with_their_parent()
    {
        var project = await _projects.CreateAsync(NewProject(), _ct);
        var backend = await _workItems.CreateAsync(project.Id, WorkItem("Backend", Skill.Backend, null), _ct);
        var login = await _workItems.CreateAsync(project.Id, Child("Login API", backend.Id), _ct);
        var token = await _workItems.CreateAsync(project.Id, Child("Token", login.Id), _ct);
        var test = await _workItems.CreateAsync(project.Id, WorkItem("Test", Skill.Test, null), _ct);
        await _dependencies.CreateAsync(project.Id, new DependencyRequest { PredecessorId = token.Id, SuccessorId = test.Id }, _ct);

        // Backend'i kendi torununun altına taşımak döngü oluşturur
        var move = WorkItemRequest.From(backend);
        move.ParentId = token.Id;
        await Assert.ThrowsAsync<BusinessRuleException>(() => _workItems.UpdateAsync(project.Id, backend.Id, move, _ct));

        await _workItems.DeleteAsync(project.Id, backend.Id, _ct);

        Assert.Equal(["Test"], (await _workItems.ListAsync(project.Id, _ct)).Select(w => w.Name));
        Assert.Empty(await _dependencies.ListAsync(project.Id, _ct));
    }

    [Fact]
    public async Task Parent_from_another_project_is_rejected_and_project_delete_handles_hierarchy()
    {
        var p1 = await _projects.CreateAsync(NewProject(), _ct);
        var p2 = await _projects.CreateAsync(NewProject(), _ct);
        var foreign = await _workItems.CreateAsync(p2.Id, WorkItem("Yabancı", Skill.Backend, null), _ct);
        await Assert.ThrowsAsync<BusinessRuleException>(() => _workItems.CreateAsync(p1.Id, Child("X", foreign.Id), _ct));

        await _workItems.CreateAsync(p2.Id, Child("Alt", foreign.Id), _ct);
        await _projects.DeleteAsync(p2.Id, _ct);
        Assert.Empty(Db.WorkItems);
    }

    private static WorkItemRequest Child(string name, int parentId)
    {
        var request = WorkItem(name, Skill.Backend, null);
        request.ParentId = parentId;
        return request;
    }

    private static WorkItemRequest WorkItem(string name, Skill skill, int? assigneeId) => new()
    {
        Name = name, RequiredSkill = skill, EstimatedHours = 16, AssigneeId = assigneeId
    };
}
