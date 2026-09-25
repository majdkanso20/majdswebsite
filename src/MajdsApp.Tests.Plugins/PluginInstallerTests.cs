using System.Security.Cryptography;
using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using MajdsApp.SharedKernel.Plugins;
using Xunit;

namespace MajdsApp.Tests.Plugins;

/// <summary>P5 install, upgrade, rollback and uninstall on disk: verification, trust policy, staging and applying at start.</summary>
[Collection(PluginHostCollection.Name)] // serialized with the host tests: it briefly loads plugin assemblies
public class PluginInstallerTests : IDisposable
{
    private const string Id = "MajdsApp.Plugins.Tasks";
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "majds-plugin-installer-" + Guid.NewGuid().ToString("N"));

    public PluginInstallerTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        // A plugin assembly loaded by a test stays locked until the process ends (Windows), so a failed delete of this temp folder is harmless.
        try { Directory.Delete(_folder, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private PluginHostOptions Options(bool requireAllowList = false, params AllowedPlugin[] allowed) => new(_folder, requireAllowList, allowed);

    private StagedPlugin Stage(byte[] package, string? checksum = null, PluginHostOptions? options = null) =>
        PluginInstaller.Stage(new MemoryStream(package), checksum, options ?? Options());

    private static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private string Leftovers() => Directory.Exists(Path.Combine(_folder, ".staging")) ? string.Join(",", Directory.GetDirectories(Path.Combine(_folder, ".staging"))) : "";

    // ---- install -------------------------------------------------------------------------------------------------

    [Fact]
    public void A_valid_package_is_staged_not_installed_and_reports_its_checksum()
    {
        var package = PluginPackages.Create();

        var staged = Stage(package);

        staged.Action.Should().Be("Install");
        staged.Manifest.Id.Should().Be(Id);
        staged.Sha256.Should().Be(Sha(package));
        Directory.Exists(Path.Combine(_folder, Id)).Should().BeFalse();                 // nothing running is touched
        PluginInstaller.ListPending(_folder).Should().ContainSingle(c => c.Id == Id && c.Action == "Install");
        Leftovers().Should().BeEmpty();
    }

    [Fact]
    public void Applying_the_pending_change_at_start_installs_a_plugin_the_host_can_then_load()
    {
        Stage(PluginPackages.Create());

        var messages = PluginInstaller.ApplyPendingChanges(_folder);

        messages.Should().ContainSingle().Which.Should().Contain("Install");
        PluginInstaller.ListPending(_folder).Should().BeEmpty();
        InstalledVersion().Should().Be("1.0.0");
        File.Exists(Path.Combine(_folder, Id, "backend", "MajdsApp.Plugins.Tasks.dll")).Should().BeTrue();
        File.Exists(Path.Combine(_folder, Id, ".meta.json")).Should().BeFalse();        // the staging marker does not travel into the install
    }

    [Fact]
    public void A_checksum_that_does_not_match_is_refused_and_one_that_does_is_accepted_in_any_case()
    {
        var package = PluginPackages.Create();

        ((Action)(() => Stage(package, new string('0', 64)))).Should().Throw<ValidationException>().WithMessage("*checksum*");
        PluginInstaller.ListPending(_folder).Should().BeEmpty();

        Stage(package, Sha(package).ToUpperInvariant()).Manifest.Id.Should().Be(Id);
    }

    [Fact]
    public void With_an_allow_list_only_the_approved_id_and_checksum_can_be_installed()
    {
        var package = PluginPackages.Create();
        var other = PluginPackages.Create(m => m["version"] = "1.0.1");

        var options = Options(true, new AllowedPlugin(Id, Sha(package)));

        ((Action)(() => Stage(other, options: options))).Should().Throw<ValidationException>().WithMessage("*not on the approved list*");
        ((Action)(() => Stage(package, options: Options(true)))).Should().Throw<ValidationException>().WithMessage("*not on the approved list*");
        Stage(package, options: options).Manifest.Id.Should().Be(Id);
    }

    [Theory]
    [InlineData("../evil.dll")]
    [InlineData("backend/../../evil.dll")]
    public void A_package_that_tries_to_write_outside_its_folder_is_refused(string entryName)
    {
        var package = PluginPackages.Create(extra: zip => PluginPackages.Add(zip, entryName, [1, 2, 3]));

        ((Action)(() => Stage(package))).Should().Throw<ValidationException>().WithMessage("*unsafe path*");
        File.Exists(Path.Combine(_folder, "evil.dll")).Should().BeFalse();
        Leftovers().Should().BeEmpty();
    }

    [Fact]
    public void A_package_with_a_file_type_that_is_not_allowed_is_refused()
    {
        var package = PluginPackages.Create(extra: zip => PluginPackages.Add(zip, "backend/run.exe", [1]));

        ((Action)(() => Stage(package))).Should().Throw<ValidationException>().WithMessage("*not allowed*");
    }

    [Fact]
    public void Broken_packages_are_refused_with_a_reason_and_leave_nothing_behind()
    {
        ((Action)(() => Stage([1, 2, 3, 4]))).Should().Throw<ValidationException>().WithMessage("*not a valid .zip*");
        ((Action)(() => PluginInstaller.Stage(new MemoryStream([]), null, Options()))).Should().Throw<ValidationException>().WithMessage("*empty*");
        ((Action)(() => Stage(PluginPackages.Create(includeAssembly: false)))).Should().Throw<ValidationException>().WithMessage("*no backend/*");
        ((Action)(() => Stage(PluginPackages.Create(m => m["id"] = "../up")))).Should().Throw<ValidationException>().WithMessage("*needs an id*");
        ((Action)(() => Stage(PluginPackages.Create(m => m["version"] = "banana")))).Should().Throw<ValidationException>().WithMessage("*not a valid version*");
        ((Action)(() => Stage(PluginPackages.Create(m => m["moduleType"] = "Not.A.Real.Type")))).Should().Throw<ValidationException>().WithMessage("*was not found*");
        ((Action)(() => Stage(PluginPackages.Create(m => m.Remove("assembly"))))).Should().Throw<ValidationException>().WithMessage("*must declare*");

        PluginInstaller.ListPending(_folder).Should().BeEmpty();
        Leftovers().Should().BeEmpty();
    }

    [Fact]
    public void A_package_larger_than_the_limit_is_refused()
    {
        using var huge = new MemoryStream(new byte[PluginInstaller.MaxPackageBytes + 1]);

        ((Action)(() => PluginInstaller.Stage(huge, null, Options()))).Should().Throw<ValidationException>().WithMessage("*larger than*");
    }

    [Fact]
    public void A_plugin_built_for_another_platform_version_is_refused_before_it_is_staged()
    {
        ((Action)(() => Stage(PluginPackages.Create(m => m["minHostVersion"] = "99.0.0")))).Should().Throw<ValidationException>().WithMessage("*needs platform version*");
        ((Action)(() => Stage(PluginPackages.Create(m => m["maxHostVersion"] = "0.1.0")))).Should().Throw<ValidationException>().WithMessage("*supports platform versions up to*");
        ((Action)(() => Stage(PluginPackages.Create(m => m["minHostVersion"] = "x")))).Should().Throw<ValidationException>().WithMessage("*not a valid version*");

        Stage(PluginPackages.Create(m => { m["minHostVersion"] = "0.1.0"; m["maxHostVersion"] = "99.0.0"; })).Manifest.Id.Should().Be(Id);
    }

    [Fact]
    public void An_incompatible_plugin_already_on_disk_is_not_loaded_and_says_why()
    {
        PluginPackages.InstallFolder(_folder);
        var manifestPath = Path.Combine(_folder, Id, "plugin.json");
        File.WriteAllText(manifestPath, File.ReadAllText(manifestPath).Replace("\"version\"", "\"minHostVersion\":\"99.0.0\",\"version\""));

        var loaded = PluginManager.LoadAll(_folder);

        loaded.Should().ContainSingle().Which.Succeeded.Should().BeFalse();
        loaded[0].LoadError.Should().Contain("needs platform version");
    }

    // ---- upgrade and rollback ------------------------------------------------------------------------------------------

    [Fact]
    public void An_installed_version_can_only_be_replaced_by_a_newer_one()
    {
        PluginPackages.InstallFolder(_folder, "1.2.0");

        ((Action)(() => Stage(PluginPackages.Create(m => m["version"] = "1.2.0")))).Should().Throw<ValidationException>().WithMessage("*already installed*");
        ((Action)(() => Stage(PluginPackages.Create(m => m["version"] = "1.1.0")))).Should().Throw<ValidationException>().WithMessage("*already installed*");

        var staged = Stage(PluginPackages.Create(m => m["version"] = "1.3.0"));
        staged.Action.Should().Be("Upgrade");
        staged.PreviousVersion.Should().Be("1.2.0");
    }

    [Fact]
    public void An_upgrade_keeps_the_previous_version_and_a_rollback_restores_it()
    {
        PluginPackages.InstallFolder(_folder, "1.0.0");
        Stage(PluginPackages.Create(m => m["version"] = "2.0.0"));
        PluginInstaller.HasPreviousVersion(_folder, Id).Should().BeFalse();

        PluginInstaller.ApplyPendingChanges(_folder).Should().ContainSingle().Which.Should().Contain("Upgrade");
        InstalledVersion().Should().Be("2.0.0");
        PluginInstaller.HasPreviousVersion(_folder, Id).Should().BeTrue();

        var rollback = PluginInstaller.StageRollback(Id, Options());
        rollback.Manifest.Version.Should().Be("1.0.0");
        PluginInstaller.ListPending(_folder).Should().ContainSingle(c => c.Action == "Rollback");
        PluginInstaller.ApplyPendingChanges(_folder);

        InstalledVersion().Should().Be("1.0.0");
    }

    [Fact]
    public void Rolling_back_with_no_earlier_version_is_refused()
    {
        ((Action)(() => PluginInstaller.StageRollback(Id, Options()))).Should().Throw<ValidationException>().WithMessage("*no earlier version*");
    }

    [Fact]
    public void A_staged_change_can_be_cancelled_before_it_is_applied()
    {
        Stage(PluginPackages.Create());

        PluginInstaller.CancelPending(Id, Options()).Should().BeTrue();

        PluginInstaller.ListPending(_folder).Should().BeEmpty();
        PluginInstaller.ApplyPendingChanges(_folder).Should().BeEmpty();
        Directory.Exists(Path.Combine(_folder, Id)).Should().BeFalse();
        PluginInstaller.CancelPending(Id, Options()).Should().BeFalse();
    }

    // ---- uninstall -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Uninstalling_marks_the_plugin_and_its_files_are_removed_at_the_next_start()
    {
        PluginPackages.InstallFolder(_folder);

        PluginInstaller.MarkForUninstall(Id, Options());

        PluginInstaller.ListPending(_folder).Should().ContainSingle(c => c.Id == Id && c.Action == "Uninstall");
        Directory.Exists(Path.Combine(_folder, Id)).Should().BeTrue();                   // still there: it may be loaded and locked
        PluginInstaller.ApplyPendingChanges(_folder).Should().ContainSingle().Which.Should().Contain("Uninstalled");
        Directory.Exists(Path.Combine(_folder, Id)).Should().BeFalse();
        PluginManager.LoadAll(_folder).Should().BeEmpty();
    }

    [Fact]
    public void A_hand_placed_pending_folder_that_the_installer_did_not_stage_is_discarded_not_installed()
    {
        var rogue = Path.Combine(_folder, ".pending", "Rogue.Plugin");
        Directory.CreateDirectory(rogue);
        File.WriteAllText(Path.Combine(rogue, "plugin.json"), "{ \"id\": \"Rogue.Plugin\" }");

        var messages = PluginInstaller.ApplyPendingChanges(_folder);

        messages.Should().ContainSingle().Which.Should().Contain("not staged by the installer");
        Directory.Exists(Path.Combine(_folder, "Rogue.Plugin")).Should().BeFalse();
        Directory.Exists(rogue).Should().BeFalse();
    }

    [Fact]
    public void The_trust_policy_is_read_from_configuration()
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Plugins:Trust:RequireAllowList"] = "true",
            ["Plugins:Trust:Allowed:0:Id"] = "Acme.Invoicing",
            ["Plugins:Trust:Allowed:0:Sha256"] = "abc123",
            ["Plugins:Trust:Allowed:1:Id"] = "Acme.Other",
            ["Plugins:Trust:Allowed:1:Sha256"] = "def456"
        }).Build();

        var options = PluginHostOptions.FromConfiguration(config, _folder);

        options.RequireAllowList.Should().BeTrue();
        options.Allowed.Should().Equal(new AllowedPlugin("Acme.Invoicing", "abc123"), new AllowedPlugin("Acme.Other", "def456"));
        PluginHostOptions.FromConfiguration(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(), _folder)
            .Should().Match<PluginHostOptions>(o => !o.RequireAllowList && o.Allowed.Count == 0);   // off by default
    }

    private string InstalledVersion() =>
        System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(_folder, Id, "plugin.json")))!["version"]!.GetValue<string>();

    [Fact]
    public void An_uninstall_can_be_cancelled_and_an_unknown_plugin_is_not_found()
    {
        PluginPackages.InstallFolder(_folder);
        PluginInstaller.MarkForUninstall(Id, Options());

        PluginInstaller.CancelPending(Id, Options()).Should().BeTrue();

        PluginInstaller.ListPending(_folder).Should().BeEmpty();
        PluginInstaller.ApplyPendingChanges(_folder).Should().BeEmpty();
        ((Action)(() => PluginInstaller.MarkForUninstall("Nope.Nothing", Options()))).Should().Throw<MajdsApp.SharedKernel.Exceptions.NotFoundException>();
    }

    [Fact]
    public void The_installers_own_folders_are_never_treated_as_plugins()
    {
        // A plugin folder that cannot load (no assembly) still shows up as a failed entry, so the count proves what was scanned.
        Directory.CreateDirectory(Path.Combine(_folder, "Real.Plugin"));
        File.WriteAllText(Path.Combine(_folder, "Real.Plugin", "plugin.json"),
            "{ \"id\": \"Real.Plugin\", \"name\": \"R\", \"version\": \"1.0.0\", \"assembly\": \"R.dll\", \"moduleType\": \"R.Module\" }");
        foreach (var hidden in new[] { ".previous", ".staging", ".pending" })
        {
            Directory.CreateDirectory(Path.Combine(_folder, hidden, "Leftover.Thing"));
            File.WriteAllText(Path.Combine(_folder, hidden, "Leftover.Thing", "plugin.json"), "{ not json");
        }

        var loaded = PluginManager.LoadAll(_folder);

        loaded.Should().ContainSingle().Which.Manifest.Id.Should().Be("Real.Plugin");
    }
}
