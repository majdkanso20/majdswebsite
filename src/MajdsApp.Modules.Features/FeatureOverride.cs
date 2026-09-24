using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MajdsApp.Modules.Features;

/// <summary>An admin's on/off override of a feature's default (F-Features data model). A feature the
/// admin hasn't touched — or set back to its default — has no row.</summary>
public class FeatureOverride
{
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}

public class FeatureOverrideConfiguration : IEntityTypeConfiguration<FeatureOverride>
{
    public void Configure(EntityTypeBuilder<FeatureOverride> builder)
    {
        builder.ToTable("FeatureOverrides");
        builder.HasKey(f => f.Name);
        builder.Property(f => f.Name).HasMaxLength(128);
    }
}
