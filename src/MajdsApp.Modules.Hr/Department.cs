using MajdsApp.SharedKernel.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MajdsApp.Modules.Hr;

/// <summary>An organizational unit employees belong to. Deleting one that still has employees is refused
/// (an employee needing a home is a data-integrity question a cascading delete would hide).</summary>
public class Department : AuditableEntity<int>
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("HrDepartments");
        builder.Property(d => d.Name).HasMaxLength(200).IsRequired();
        builder.Property(d => d.Description).HasMaxLength(2000);
    }
}
