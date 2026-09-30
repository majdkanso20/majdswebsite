using MajdsApp.SharedKernel.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MajdsApp.Modules.Hr;

public enum EmployeeStatus { Active = 0, OnLeave = 1, Terminated = 2 }

public class Employee : AuditableEntity<int>
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public int? DepartmentId { get; set; }
    public DateTime? HireDate { get; set; }
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
}

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("HrEmployees");
        builder.Property(e => e.FullName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(320).IsRequired();
        builder.Property(e => e.JobTitle).HasMaxLength(200);
        // Never cascades: leaving a department on file for its former employees is more useful than a database
        // error when someone tries to delete a department, and DeleteDepartmentCommand refuses that case anyway.
        builder.HasOne<Department>().WithMany().HasForeignKey(e => e.DepartmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
