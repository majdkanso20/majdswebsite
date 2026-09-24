using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MajdsApp.Modules.Plugins;

/// <summary>
/// Persisted registry row for a discovered plugin (P5 FR-PLUG-030), keyed by the plugin's own id
/// (namespaced, e.g. <c>MajdsApp.Plugins.Tasks</c>). Enabled/disabled state survives restarts; the
/// rest (name/version/author/last error) is refreshed from the manifest on every startup scan.
/// </summary>
public class InstalledPlugin
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string AssemblyName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string? LastError { get; set; }
    public DateTime DiscoveredAt { get; set; }
    public DateTime LastSeenAt { get; set; }
}

public class InstalledPluginConfiguration : IEntityTypeConfiguration<InstalledPlugin>
{
    public void Configure(EntityTypeBuilder<InstalledPlugin> builder)
    {
        builder.ToTable("InstalledPlugins");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasMaxLength(200);
        builder.Property(p => p.Name).HasMaxLength(200);
        builder.Property(p => p.Version).HasMaxLength(50);
        builder.Property(p => p.Author).HasMaxLength(200);
        builder.Property(p => p.AssemblyName).HasMaxLength(200);
        builder.Property(p => p.LastError).HasMaxLength(2000);
    }
}
