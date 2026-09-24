using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MajdsApp.Modules.Settings;

/// <summary>A user's own override of a setting whose definition allows it (F-Settings User scope).
/// Resolution is User > Application > code default (FR-SET-002).</summary>
public class SettingUserValue
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class SettingUserValueConfiguration : IEntityTypeConfiguration<SettingUserValue>
{
    public void Configure(EntityTypeBuilder<SettingUserValue> builder)
    {
        builder.ToTable("SettingUserValues");
        builder.Property(s => s.Name).HasMaxLength(128);
        builder.Property(s => s.Value).HasMaxLength(1000);
        builder.HasIndex(s => new { s.UserId, s.Name }).IsUnique();
    }
}
