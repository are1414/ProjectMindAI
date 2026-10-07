using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectMind.Application.Ai;
using ProjectMind.Application.Chat;
using ProjectMind.Application.Dependencies;
using ProjectMind.Application.Overview;
using ProjectMind.Application.People;
using ProjectMind.Application.Projects;
using ProjectMind.Application.WorkItems;
using ProjectMind.Domain.Enums;
using ProjectMind.Infrastructure.Persistence;

namespace ProjectMind.Tests.Integration;

/// <summary>Application servislerini bellek içi SQLite veritabanı ile test eder.</summary>
public abstract class ServiceTestBase : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected ServiceTestBase()
    {
        _connection.Open();
        Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        Db.Database.EnsureCreated();
    }

    protected AppDbContext Db { get; }

    protected AiActionService NewActionService() =>
        new(Db, new ProjectService(Db), new PersonService(Db), new WorkItemService(Db), new DependencyService(Db));

    protected ChatService NewChatService(IChatModel model)
    {
        var overview = new ProjectOverviewService(
            new ProjectService(Db), new PersonService(Db), new WorkItemService(Db), new DependencyService(Db));
        return new ChatService(Db, model, NewActionService(), new ProjectService(Db),
            new ProjectContextBuilder(Db, overview, TimeProvider.System), Options.Create(new AiOptions()));
    }

    protected static ProjectRequest NewProject() => new()
    {
        Name = "Mobile Banking Modernization",
        Type = ProjectType.MobileApplication,
        StartDate = new DateOnly(2026, 11, 1),
        TargetEndDate = new DateOnly(2027, 6, 30),
        Budget = 8_000_000,
        Currency = "try",
        HoursPerDay = 8
    };

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
