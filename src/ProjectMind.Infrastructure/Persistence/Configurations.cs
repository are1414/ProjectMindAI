using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectMind.Domain.Entities;

namespace ProjectMind.Infrastructure.Persistence;

// Not: SQL Server "multiple cascade paths" hatasını önlemek için iş/kişi/bağımlılık arası
// ilişkiler NO ACTION; ilgili temizlik Application servislerinde yapılır.

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> b)
    {
        b.Property(p => p.Name).HasMaxLength(200).IsRequired();
        b.Property(p => p.Description).HasMaxLength(2000);
        b.Property(p => p.Currency).HasMaxLength(3).IsRequired();
        b.Property(p => p.Budget).HasPrecision(18, 2);
        b.Property(p => p.HoursPerDay).HasPrecision(5, 2);
        b.Property(p => p.Type).HasConversion<string>().HasMaxLength(50);
        b.Property(p => p.Status).HasConversion<string>().HasMaxLength(50);
    }
}

internal sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> b)
    {
        b.Property(p => p.Name).HasMaxLength(200).IsRequired();
        b.Property(p => p.WeeklyCapacityHours).HasPrecision(6, 2);
        b.Property(p => p.HourlyCost).HasPrecision(18, 2);
        b.HasOne(p => p.Project).WithMany(p => p.People)
            .HasForeignKey(p => p.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> b)
    {
        b.Property(w => w.Name).HasMaxLength(300).IsRequired();
        b.Property(w => w.Description).HasMaxLength(4000);
        b.Property(w => w.EstimatedHours).HasPrecision(8, 2);
        b.Property(w => w.ActualHours).HasPrecision(8, 2);
        b.Property(w => w.Phase).HasConversion<string>().HasMaxLength(50);
        b.Property(w => w.Priority).HasConversion<string>().HasMaxLength(50);
        b.Property(w => w.Status).HasConversion<string>().HasMaxLength(50);
        b.HasOne(w => w.Project).WithMany(p => p.WorkItems)
            .HasForeignKey(w => w.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(w => w.Assignee).WithMany()
            .HasForeignKey(w => w.AssigneeId).OnDelete(DeleteBehavior.NoAction);
        // Alt işler servis tarafından (önce çocuklar) silinir; SQL Server'da kendine cascade yolu yok.
        b.HasOne(w => w.Parent).WithMany()
            .HasForeignKey(w => w.ParentId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class WorkItemDependencyConfiguration : IEntityTypeConfiguration<WorkItemDependency>
{
    public void Configure(EntityTypeBuilder<WorkItemDependency> b)
    {
        b.HasIndex(d => new { d.PredecessorId, d.SuccessorId }).IsUnique();
        b.HasOne<Project>().WithMany()
            .HasForeignKey(d => d.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(d => d.Predecessor).WithMany()
            .HasForeignKey(d => d.PredecessorId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(d => d.Successor).WithMany()
            .HasForeignKey(d => d.SuccessorId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class ChatSessionConfiguration : IEntityTypeConfiguration<ChatSession>
{
    public void Configure(EntityTypeBuilder<ChatSession> b)
    {
        b.Property(s => s.Title).HasMaxLength(200).IsRequired();
        // Proje silinince sohbet kaydı kalır (denetim izi), sadece bağlantı kopar.
        b.HasOne(s => s.Project).WithMany()
            .HasForeignKey(s => s.ProjectId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> b)
    {
        b.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);
        b.Property(m => m.Content).IsRequired();
        b.Property(m => m.Model).HasMaxLength(100);
        b.Property(m => m.PromptVersion).HasMaxLength(50);
        b.HasOne(m => m.ChatSession).WithMany(s => s.Messages)
            .HasForeignKey(m => m.ChatSessionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AiActionConfiguration : IEntityTypeConfiguration<AiAction>
{
    public void Configure(EntityTypeBuilder<AiAction> b)
    {
        b.Property(a => a.ToolName).HasMaxLength(100).IsRequired();
        b.Property(a => a.PayloadJson).IsRequired();
        b.Property(a => a.Summary).HasMaxLength(1000).IsRequired();
        b.Property(a => a.ResultMessage).HasMaxLength(1000);
        b.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        b.HasOne(a => a.ChatSession).WithMany()
            .HasForeignKey(a => a.ChatSessionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ChatMessage>().WithMany()
            .HasForeignKey(a => a.ChatMessageId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class BaselineConfiguration : IEntityTypeConfiguration<Baseline>
{
    public void Configure(EntityTypeBuilder<Baseline> b)
    {
        b.Property(x => x.TotalHours).HasPrecision(10, 2);
        b.Property(x => x.PlannedCost).HasPrecision(18, 2);
        b.HasOne<Project>().WithMany()
            .HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Items).WithOne()
            .HasForeignKey(i => i.BaselineId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class BaselineItemConfiguration : IEntityTypeConfiguration<BaselineItem>
{
    public void Configure(EntityTypeBuilder<BaselineItem> b)
    {
        b.Property(x => x.Name).HasMaxLength(300).IsRequired();
        b.Property(x => x.Hours).HasPrecision(8, 2);
        b.Property(x => x.HourlyCost).HasPrecision(18, 2);
    }
}

internal sealed class StatusUpdateConfiguration : IEntityTypeConfiguration<StatusUpdate>
{
    public void Configure(EntityTypeBuilder<StatusUpdate> b)
    {
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
        b.Property(x => x.ActualHours).HasPrecision(8, 2);
        b.HasIndex(x => new { x.ProjectId, x.Date });
        // İş silinince geçmişi de silinir. ProjectId sadece indeks (proje silmede işler zaten siliniyor).
        b.HasOne<WorkItem>().WithMany()
            .HasForeignKey(x => x.WorkItemId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ProjectSnapshotConfiguration : IEntityTypeConfiguration<ProjectSnapshot>
{
    public void Configure(EntityTypeBuilder<ProjectSnapshot> b)
    {
        b.HasIndex(x => new { x.ProjectId, x.Date }).IsUnique();
        foreach (var p in new[] { nameof(ProjectSnapshot.PlannedValue), nameof(ProjectSnapshot.EarnedValue),
                     nameof(ProjectSnapshot.ActualCost), nameof(ProjectSnapshot.BudgetAtCompletion),
                     nameof(ProjectSnapshot.EstimateAtCompletion), nameof(ProjectSnapshot.ScopeHours) })
            b.Property(p).HasPrecision(12, 2);
        foreach (var p in new[] { nameof(ProjectSnapshot.Spi), nameof(ProjectSnapshot.Cpi), nameof(ProjectSnapshot.SpiTime) })
            b.Property(p).HasPrecision(6, 3);
        b.Property(x => x.PercentComplete).HasPrecision(5, 1);
        b.Property(x => x.HealthScore).HasPrecision(5, 1);
        b.HasOne<Project>().WithMany()
            .HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
