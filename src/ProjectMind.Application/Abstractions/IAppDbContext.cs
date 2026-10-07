using Microsoft.EntityFrameworkCore;
using ProjectMind.Domain.Entities;

namespace ProjectMind.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<Project> Projects { get; }
    DbSet<Person> People { get; }
    DbSet<WorkItem> WorkItems { get; }
    DbSet<WorkItemDependency> WorkItemDependencies { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
