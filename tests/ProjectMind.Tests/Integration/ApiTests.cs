using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectMind.Application.Dependencies;
using ProjectMind.Application.People;
using ProjectMind.Application.Projects;
using ProjectMind.Application.WorkItems;
using ProjectMind.Domain.Enums;

namespace ProjectMind.Tests.Integration;

public class ApiTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;

    public ApiTests(ApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Full_flow_project_people_work_items_dependencies()
    {
        var project = await CreateProjectAsync();

        var person = await PostAsync<PersonResponse>($"/api/projects/{project.Id}/people",
            new PersonRequest("Ayşe", Skill.Backend | Skill.Database, 40, 750));
        Assert.Equal(Skill.Backend | Skill.Database, person.Skills);

        var db = await PostAsync<WorkItemResponse>($"/api/projects/{project.Id}/work-items",
            WorkItem("Veritabanı kurulumu", Skill.Database, person.Id));
        var api = await PostAsync<WorkItemResponse>($"/api/projects/{project.Id}/work-items",
            WorkItem("Backend API", Skill.Backend, person.Id));

        await PostAsync<DependencyResponse>($"/api/projects/{project.Id}/dependencies",
            new DependencyRequest(db.Id, api.Id));

        // Ters bağımlılık döngü oluşturur → 400
        var cycle = await _client.PostAsJsonAsync($"/api/projects/{project.Id}/dependencies",
            new DependencyRequest(api.Id, db.Id), Json);
        Assert.Equal(HttpStatusCode.BadRequest, cycle.StatusCode);

        // Kişi silinince işler kalır ama ataması boşalır
        var deletePerson = await _client.DeleteAsync($"/api/projects/{project.Id}/people/{person.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deletePerson.StatusCode);
        var items = await GetAsync<List<WorkItemResponse>>($"/api/projects/{project.Id}/work-items");
        Assert.All(items, i => Assert.Null(i.AssigneeId));

        // İş silinince bağımlılıkları da silinir
        await _client.DeleteAsync($"/api/projects/{project.Id}/work-items/{db.Id}");
        var deps = await GetAsync<List<DependencyResponse>>($"/api/projects/{project.Id}/dependencies");
        Assert.Empty(deps);

        // Proje silinir
        var deleteProject = await _client.DeleteAsync($"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteProject.StatusCode);
        var missing = await _client.GetAsync($"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Project_with_end_before_start_is_rejected()
    {
        var request = NewProject() with { TargetEndDate = new DateOnly(2026, 1, 1) };
        var response = await _client.PostAsJsonAsync("/api/projects", request, Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Work_item_with_multiple_required_skills_is_rejected()
    {
        var project = await CreateProjectAsync();
        var response = await _client.PostAsJsonAsync($"/api/projects/{project.Id}/work-items",
            WorkItem("Karışık iş", Skill.Backend | Skill.Frontend, null), Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Assignee_from_another_project_is_rejected()
    {
        var p1 = await CreateProjectAsync();
        var p2 = await CreateProjectAsync();
        var other = await PostAsync<PersonResponse>($"/api/projects/{p2.Id}/people",
            new PersonRequest("Mehmet", Skill.Frontend, 40, 600));

        var response = await _client.PostAsJsonAsync($"/api/projects/{p1.Id}/work-items",
            WorkItem("Ekran", Skill.Frontend, other.Id), Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_project_returns_404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/projects/99999/work-items")).StatusCode);

    private static ProjectRequest NewProject() => new(
        "Mobile Banking Modernization", "Demo", ProjectType.MobileApplication, ProjectStatus.Planning,
        new DateOnly(2026, 11, 1), new DateOnly(2027, 6, 30), 8_000_000, "try", 8);

    private static WorkItemRequest WorkItem(string name, Skill skill, int? assigneeId) => new(
        name, null, WorkPhase.Development, skill, Priority.High, 16, assigneeId,
        null, null, WorkItemStatus.NotStarted, 0, 0);

    private async Task<ProjectResponse> CreateProjectAsync()
    {
        var project = await PostAsync<ProjectResponse>("/api/projects", NewProject());
        Assert.Equal("TRY", project.Currency);
        return project;
    }

    private async Task<T> PostAsync<T>(string url, object body)
    {
        var response = await _client.PostAsJsonAsync(url, body, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task<T> GetAsync<T>(string url) => (await _client.GetFromJsonAsync<T>(url, Json))!;
}
