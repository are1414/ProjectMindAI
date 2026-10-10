using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProjectMind.Application.Ai;
using ProjectMind.Application.Chat;
using ProjectMind.Application.Dependencies;
using ProjectMind.Application.Overview;
using ProjectMind.Application.People;
using ProjectMind.Application.Planning;
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

    protected ProjectOverviewService NewOverviewService() =>
        new(new ProjectService(Db), new PersonService(Db), new WorkItemService(Db), new DependencyService(Db));

    /// <summary>Testlerde "bugün" proje başlangıcından önce: plan proje başlangıç tarihinden başlar.</summary>
    private static readonly DateTimeOffset Today = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    protected ScheduleService NewScheduleService(DateTimeOffset? now = null) =>
        new(Db, NewOverviewService(), new FixedClock(now ?? Today));

    protected ProjectMind.Application.Analytics.ProjectStatusService NewStatusService(DateTimeOffset? now = null) =>
        new(Db, NewScheduleService(now), new FixedClock(now ?? Today),
            Microsoft.Extensions.Options.Options.Create(new ProjectMind.Application.Analytics.HealthOptions()), Predictor);

    /// <summary>ML modeli eğitmeden sabit tahmin döndürür; son aldığı özellikleri saklar.</summary>
    protected FakeDelayPredictor Predictor { get; } = new();

    protected sealed class FakeDelayPredictor : ProjectMind.Application.Ml.IDelayPredictor
    {
        public static readonly ProjectMind.Application.Ml.DelayModelInfo Info =
            new("FastTree", "FastTreeRegression", "test", 1, 1, 1, 0.9, 0.8, 0.1, DateTimeOffset.UnixEpoch);

        public ProjectMind.Application.Ml.DelayFeatures? LastFeatures { get; private set; }

        /// <summary>Döndürülecek ML bitiş tarihi (varsayılan yok).</summary>
        public DateOnly? Finish { get; set; }
        public ProjectMind.Application.Ml.DelayModelInfo? CurrentModel => Info;
        /// <summary>Son deney raporu (varsayılan yok; değerlendirme testleri elle verir).</summary>
        public ProjectMind.Application.Ml.DelayExperimentReport? LastReport { get; set; }

        public Task<ProjectMind.Application.Ml.DelayPrediction?> PredictAsync(ProjectMind.Application.Ml.DelayFeatures features,
            ProjectMind.Application.Analytics.EvmResult evm, CancellationToken ct)
        {
            LastFeatures = features;
            return Task.FromResult<ProjectMind.Application.Ml.DelayPrediction?>(new(0.7f, 1.2f, Finish, false, Info));
        }

        public Task<ProjectMind.Application.Ml.DelayExperimentReport> RunExperimentAsync(CancellationToken ct) =>
            throw new NotSupportedException();
    }

    protected ProjectMind.Application.WhatIf.WhatIfService NewWhatIfService(DateTimeOffset? now = null) =>
        new(NewScheduleService(now), Db, Microsoft.Extensions.Options.Options.Create(new ProjectMind.Application.WhatIf.WhatIfOptions { Iterations = 100 }));

    protected AiActionService NewActionService() =>
        new(Db, new ProjectService(Db), new PersonService(Db), new WorkItemService(Db), new DependencyService(Db),
            NewScheduleService());

    protected sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }


    protected ProjectMind.Application.MissingWork.MissingWorkService NewMissingWorkService(
        IChatModel model, ProjectMind.Application.MissingWork.MissingWorkOptions? options = null) =>
        new(NewOverviewService(), NewScheduleService(), Db, model,
            Microsoft.Extensions.Options.Options.Create(options ?? new ProjectMind.Application.MissingWork.MissingWorkOptions()));

    protected ChatService NewChatService(IChatModel model)
    {
        var overview = NewOverviewService();
        var missingWork = NewMissingWorkService(model);
        return new ChatService(Db, model, NewActionService(),
            new ReadOnlyToolHandler(Db, missingWork, NewScheduleService(), NewStatusService(), NewWhatIfService()), new ProjectService(Db),
            new ProjectContextBuilder(Db, overview, TimeProvider.System), new AiOptions(), missingWork);
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
