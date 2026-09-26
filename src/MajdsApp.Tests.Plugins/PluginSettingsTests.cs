using System.Net;
using FluentAssertions;
using MajdsApp.SharedKernel.Settings;
using MajdsApp.Tests.Support;
using Xunit;

namespace MajdsApp.Tests.Plugins;

/// <summary>P5 FR-PLUG-013: a plugin defines its own settings, edited on the standard settings page and validated by the plugin's rule.</summary>
[Collection(PluginHostCollection.Name)]
public class PluginSettingsTests(PluginHostFactory factory)
{
    private const string Name = "Tasks.MaxTitleLength";
    private record Row(string Name, string Group, string Value, string DefaultValue);

    private static object Update(string value) => new { items = new[] { new { name = Name, value } } };

    [Fact]
    public async Task The_plugins_setting_appears_on_the_standard_settings_page_under_its_own_group()
    {
        var admin = await factory.SignInAsync("ps.admin1@example.com", "Admin");

        var settings = (await admin.GetAsync<List<Row>>("/api/settings/list")).Data!;

        settings.Should().ContainSingle(s => s.Name == Name).Which.Should().Match<Row>(s => s.Group == "Tasks" && s.Value == "200");
    }

    [Fact]
    public async Task The_plugins_own_rule_rejects_a_bad_value_and_the_plugin_uses_a_good_one()
    {
        var admin = await factory.SignInAsync("ps.admin2@example.com", "Admin");

        (await admin.PostAsync("/api/settings/update", Update("0"))).Status.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsync("/api/settings/update", Update("501"))).Status.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsync("/api/settings/update", Update("abc"))).Status.Should().Be(HttpStatusCode.BadRequest);

        try
        {
            (await admin.PostAsync("/api/settings/update", Update("10"))).Status.Should().Be(HttpStatusCode.OK);

            // The plugin reads the setting when it validates a task.
            var tooLong = await admin.PostAsync<Guid>("/api/tasks/create", new { title = new string('x', 11), description = "d", status = "Todo", dueDate = (string?)null });
            var fits = await admin.PostAsync<Guid>("/api/tasks/create", new { title = new string('x', 10), description = "d", status = "Todo", dueDate = (string?)null });
            tooLong.Status.Should().Be(HttpStatusCode.BadRequest);
            fits.Status.Should().Be(HttpStatusCode.OK);
            await admin.PostAsync("/api/tasks/delete", new { id = fits.Data });
        }
        finally
        {
            await admin.PostAsync("/api/settings/update", Update("200"));
        }
    }

    [Fact]
    public void A_plugin_can_only_define_settings_in_its_own_namespace_and_cannot_replace_a_platform_setting()
    {
        var all = SettingDefinitionRegistry.GetAll();

        all.Where(d => d.Name.StartsWith("Tasks.")).Should().OnlyContain(d => d.Group == "Tasks");
        all.GroupBy(d => d.Name).Should().OnlyContain(g => g.Count() == 1);
        all.Should().Contain(d => d.Name == "General.ApplicationName"); // the platform's own are untouched
    }
}
