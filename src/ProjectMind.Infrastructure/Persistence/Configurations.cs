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
