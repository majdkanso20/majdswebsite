using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MajdsApp.Modules.Authorization;

/// <summary>A permission granted (or explicitly denied) to a role (F1 data model).</summary>
public class RolePermission
{
    public string RoleId { get; set; } = default!;
    public string PermissionName { get; set; } = default!;
    public bool IsGranted { get; set; }
}

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");
        builder.HasKey(rp => new { rp.RoleId, rp.PermissionName });
        builder.Property(rp => rp.PermissionName).HasMaxLength(128);
    }
}
