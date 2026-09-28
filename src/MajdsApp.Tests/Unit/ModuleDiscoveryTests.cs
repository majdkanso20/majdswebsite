using FluentAssertions;
using MajdsApp.SharedKernel.Modules;
using Xunit;

namespace MajdsApp.Tests.Unit;

/// <summary>P2 FR-MOD-002/008: the host finds its modules by name instead of keeping a list.</summary>
public class ModuleDiscoveryTests
{
    [Fact]
    public void Every_module_project_next_to_the_application_is_found_in_name_order()
    {
        var found = ModuleDiscovery.LoadModuleAssemblies().Select(a => a.GetName().Name!).ToList();

        found.Should().OnlyContain(n => n.StartsWith(ModuleDiscovery.Prefix));
        found.Should().Contain(["MajdsApp.Modules.Users", "MajdsApp.Modules.Files", "MajdsApp.Modules.Notifications", "MajdsApp.Modules.Plugins"]);
        found.Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
        found.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void A_folder_without_modules_gives_none_and_other_assemblies_are_ignored()
    {
        var folder = Directory.CreateTempSubdirectory();
        try
        {
            File.WriteAllText(Path.Combine(folder.FullName, "Other.Library.dll"), "not a module");
            ModuleDiscovery.LoadModuleAssemblies(folder.FullName).Should().BeEmpty();
        }
        finally { folder.Delete(true); }
    }
}
