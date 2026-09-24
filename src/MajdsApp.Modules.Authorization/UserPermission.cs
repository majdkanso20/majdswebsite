using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MajdsApp.Modules.Authorization;

/// <summary>A direct grant/deny override for one user, applied on top of their roles (F1 data model).</summary>
public class UserPermission
{
    public string UserId { get; set; } = default!;
    public string PermissionName { get; set; } = default!;
    public bool IsGranted { get; set; }
}

public class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> builder)
    {
        builder.ToTable("UserPermissions");
        builder.HasKey(up => new { up.UserId, up.PermissionName });
        builder.Property(up => up.PermissionName).HasMaxLength(128);
    }
}
