using MajdsApp.SharedKernel.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MajdsApp.Plugins.Tasks;

// Named TaskItemStatus, not TaskStatus: System.Threading.Tasks.TaskStatus is ambiently in scope here
// (implicit usings include System.Threading.Tasks) and would make "TaskStatus" ambiguous.
public enum TaskItemStatus { Todo = 0, InProgress = 1, Done = 2 }

/// <summary>Sample business-domain entity proving the whole P5 chain end to end: a runtime-loaded
/// plugin contributing its own table, permissions, menu entry, and CRUD API.</summary>
public class TaskItem : AuditableEntity<Guid>
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;
    public DateTime? DueDate { get; set; }
}

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        // Table-name-prefixed rather than a dedicated schema (full FR-PLUG-011 per-plugin schema
        // isolation is not implemented) — but still clearly namespaced and independently identifiable.
        builder.ToTable("Plugin_Tasks_Items");
        builder.Property(t => t.Title).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(2000);
        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
