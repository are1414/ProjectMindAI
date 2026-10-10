using Microsoft.EntityFrameworkCore;
using ProjectMind.Domain.Entities;

namespace ProjectMind.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<Project> Projects { get; }
    DbSet<Person> People { get; }
    DbSet<WorkItem> WorkItems { get; }
    DbSet<WorkItemDependency> WorkItemDependencies { get; }
    DbSet<ChatSession> ChatSessions { get; }
    DbSet<ChatMessage> ChatMessages { get; }
    DbSet<AiAction> AiActions { get; }
    DbSet<AiAnalysisLog> AiAnalysisLogs { get; }
    DbSet<Baseline> Baselines { get; }
    DbSet<BaselineItem> BaselineItems { get; }
    DbSet<StatusUpdate> StatusUpdates { get; }
    DbSet<ProjectSnapshot> ProjectSnapshots { get; }
    DbSet<WhatIfRunLog> WhatIfRunLogs { get; }
    DbSet<SurveyResponse> SurveyResponses { get; }
    DbSet<SurveyAnswer> SurveyAnswers { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
