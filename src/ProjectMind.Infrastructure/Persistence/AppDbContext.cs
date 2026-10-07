using Microsoft.EntityFrameworkCore;
using ProjectMind.Application.Abstractions;
using ProjectMind.Domain.Entities;

namespace ProjectMind.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<WorkItemDependency> WorkItemDependencies => Set<WorkItemDependency>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<AiAction> AiActions => Set<AiAction>();
    public DbSet<Baseline> Baselines => Set<Baseline>();
    public DbSet<BaselineItem> BaselineItems => Set<BaselineItem>();
    public DbSet<StatusUpdate> StatusUpdates => Set<StatusUpdate>();
    public DbSet<ProjectSnapshot> ProjectSnapshots => Set<ProjectSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = now;
            if (entry.State is EntityState.Added or EntityState.Modified)
                entry.Entity.UpdatedAt = now;
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
