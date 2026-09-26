using FluentAssertions;
using MajdsApp.Modules.Localization;
using MajdsApp.SharedKernel.Plugins;
using MajdsApp.Tests.Support;
using Xunit;

namespace MajdsApp.Tests.Plugins;

/// <summary>P5 FR-PLUG-023: a plugin ships its own translations; they add to the platform's and never replace them.</summary>
[Collection(PluginHostCollection.Name)]
public class PluginLocalizationTests(PluginHostFactory factory)
{
    private record Resources(string Culture, Dictionary<string, string> Messages);

    private static LoadedPlugin PluginWith(string folder) =>
        new(new PluginManifest("Acme.Test", "Test", "1.0.0", "t", "T", []), null, null, null, folder);

    private static string FolderWith(string? json)
    {
        var folder = Path.Combine(Path.GetTempPath(), "majds-plugin-l10n-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(folder, "localization"));
        if (json is not null) File.WriteAllText(Path.Combine(folder, "localization", "ar.json"), json);
        return folder;
    }

    [Fact]
    public async Task The_installed_plugins_translations_are_served_with_the_platforms_so_its_menu_label_can_be_shown_in_arabic()
    {
        var anonymous = factory.Anonymous();

        var resources = (await anonymous.GetAsync<Resources>("/api/localization/resources?culture=ar")).Data!;

        resources.Messages.Should().Contain("Tasks", "المهام");                          // from the plugin
        resources.Messages.Should().ContainKey("Plugin '{0}' was not found.");            // the platform's own are still there
    }

    [Fact]
    public void A_plugin_adds_entries_but_cannot_replace_one_the_platform_translated()
    {
        var folder = FolderWith("""{ "Plugin '{0}' was not found.": "مزيّف", "Acme thing": "شيء" }""");
        var catalog = new MessageCatalog([PluginWith(folder)]);

        catalog.Translate("Acme thing", "ar").Should().Be("شيء");
        catalog.Translate("Plugin 'x' was not found.", "ar").Should().Be("الإضافة 'x' غير موجودة.");
    }

    [Fact]
    public void A_plugin_can_translate_a_message_with_a_placeholder()
    {
        var catalog = new MessageCatalog([PluginWith(FolderWith("""{ "Task {0} is late": "المهمة {0} متأخرة" }"""))]);

        catalog.Translate("Task Report is late", "ar").Should().Be("المهمة Report متأخرة");
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""["a","b"]""")]
    [InlineData("""{ "k": 5 }""")]
    public void A_broken_translation_file_is_ignored_and_the_host_still_starts(string json)
    {
        var act = () => new MessageCatalog([PluginWith(FolderWith(json))]);

        act.Should().NotThrow();
        act().Translate("Plugin 'x' was not found.", "ar").Should().Be("الإضافة 'x' غير موجودة.");
    }

    [Fact]
    public void A_plugin_with_no_translation_file_shows_its_english_text()
    {
        var catalog = new MessageCatalog([PluginWith(FolderWith(null))]);

        catalog.Translate("Something only the plugin says", "ar").Should().Be("Something only the plugin says");
    }
}
