using MajdsApp.SharedKernel.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MajdsApp.Modules.Settings;

/// <summary>An admin override of a setting's default value (F-Settings data model). Only settings the
/// admin has actually changed get a row here — everything else falls back to the code-defined default.</summary>
public class SettingValue : AuditableEntity<string>
{
    public string Value { get; set; } = string.Empty;
}

public class SettingValueConfiguration : IEntityTypeConfiguration<SettingValue>
{
    public void Configure(EntityTypeBuilder<SettingValue> builder)
    {
        builder.ToTable("SettingValues");
        builder.Property(s => s.Id).HasMaxLength(128);
    }
}
