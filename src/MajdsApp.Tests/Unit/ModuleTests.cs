using FluentAssertions;
using MajdsApp.Modules.Authorization;
using MajdsApp.Modules.Settings;
using MajdsApp.SharedKernel.Plugins;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace MajdsApp.Tests.Unit;

public class PermissionRegistryTests
{
    public PermissionRegistryTests() => MajdsApp.Tests.Support.ModuleAssemblies.LoadAll();

    [Fact]
    public void Permissions_are_grouped_by_module_and_include_the_core_set()
    {
        var groups = PermissionRegistry.GetAllGroups().ToDictionary(g => g.Name);

        groups["Users"].Permissions.Should().Contain(["Users.View", "Users.Create", "Users.Edit", "Users.Delete"]);
        groups["Roles"].Permissions.Should().Contain("Roles.View");
        groups.Should().ContainKeys("Audit", "Settings", "Plugins");
    }

    [Fact]
    public void Every_permission_name_is_unique_and_prefixed_by_its_group()
    {
        var names = PermissionRegistry.GetAllPermissionNames();
        names.Should().OnlyHaveUniqueItems();

        foreach (var group in PermissionRegistry.GetAllGroups())
            group.Permissions.Should().OnlyContain(p => p.StartsWith(group.Name + ".", StringComparison.Ordinal));
    }
}

/// <summary>FR-SET-006: secrets are stored encrypted and fail closed.</summary>
public class SettingSecretsTests
{
    private readonly IDataProtectionProvider _provider = new EphemeralDataProtectionProvider();

    [Fact]
    public void A_secret_round_trips_and_is_not_stored_in_the_clear()
    {
        var stored = SettingSecrets.Protect(_provider, "s3cret-app-password");

        stored.Should().StartWith("enc:").And.NotContain("s3cret-app-password");
        SettingSecrets.Unprotect(_provider, stored).Should().Be("s3cret-app-password");
    }

    [Fact]
    public void The_same_secret_encrypts_differently_each_time() =>
        SettingSecrets.Protect(_provider, "same").Should().NotBe(SettingSecrets.Protect(_provider, "same"));

    [Fact]
    public void A_value_that_was_never_encrypted_reads_back_as_not_set() =>
        SettingSecrets.Unprotect(_provider, "plain text").Should().BeEmpty();

    [Fact]
    public void A_tampered_or_foreign_payload_reads_back_as_not_set()
    {
        var stored = SettingSecrets.Protect(_provider, "abc");
        SettingSecrets.Unprotect(_provider, stored[..^4] + "AAAA").Should().BeEmpty();
        SettingSecrets.Unprotect(new EphemeralDataProtectionProvider(), stored).Should().BeEmpty(); // different key ring
    }
}

/// <summary>P5: a broken plugin is rejected with a diagnostic, never a host crash (FR-PLUG-003/008).</summary>
public class PluginManagerTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "majds-plugin-unit-" + Guid.NewGuid().ToString("N"));

    public PluginManagerTests() => Directory.CreateDirectory(_folder);
    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void A_missing_plugins_folder_simply_means_no_plugins() =>
        PluginManager.LoadAll(Path.Combine(_folder, "nope")).Should().BeEmpty();

    [Fact]
    public void A_folder_without_a_manifest_is_reported_not_thrown()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "broken"));

        var result = PluginManager.LoadAll(_folder).Should().ContainSingle().Subject;

        result.Succeeded.Should().BeFalse();
        result.LoadError.Should().Contain("plugin.json");
        result.Manifest.Id.Should().Be("broken");
    }

    [Fact]
    public void A_manifest_missing_required_fields_is_rejected()
    {
        var dir = Directory.CreateDirectory(Path.Combine(_folder, "incomplete"));
        File.WriteAllText(Path.Combine(dir.FullName, "plugin.json"), """{ "id": "Acme.Thing" }""");

        var result = PluginManager.LoadAll(_folder).Single();

        result.Succeeded.Should().BeFalse();
        result.LoadError.Should().Contain("id, assembly, and moduleType");
    }

    [Fact]
    public void A_manifest_pointing_at_a_missing_assembly_is_rejected()
    {
        var dir = Directory.CreateDirectory(Path.Combine(_folder, "noassembly"));
        File.WriteAllText(Path.Combine(dir.FullName, "plugin.json"),
            """{ "id": "Acme.Thing", "name": "Thing", "version": "1.0.0", "assembly": "Acme.Thing.dll", "moduleType": "Acme.ThingModule" }""");

        var result = PluginManager.LoadAll(_folder).Single();

        result.Succeeded.Should().BeFalse();
        result.LoadError.Should().Contain("Acme.Thing.dll");
    }

    [Fact]
    public void One_bad_plugin_does_not_stop_the_others_from_being_scanned()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "a-bad"));
        Directory.CreateDirectory(Path.Combine(_folder, "b-bad"));

        PluginManager.LoadAll(_folder).Should().HaveCount(2).And.OnlyContain(p => !p.Succeeded);
    }
}
