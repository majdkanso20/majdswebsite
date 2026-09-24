using System.Net;
using System.Text.Json;
using FluentAssertions;
using MajdsApp.Tests.Support;
using Xunit;

namespace MajdsApp.Tests.Integration;

public record WidgetRow(string Key, string Title, string Kind, int Columns, bool Visible);

/// <summary>F-Dashboard end to end: permission-filtered widgets, independent data calls, per-user layout.</summary>
public class DashboardTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly string[] AdminWidgets =
        ["notifications.unread", "users.total", "audit.failed-24h", "audit.activity-7d", "audit.recent"];

    [Fact]
    public async Task An_administrator_sees_every_baseline_widget_a_KPI_a_chart_and_a_feed()
    {
        var admin = await factory.SignInAsync("dash.admin@example.com", "Admin");

        var widgets = (await admin.GetAsync<List<WidgetRow>>("/api/dashboard/widgets")).Data!;

        widgets.Select(w => w.Key).Should().Equal(AdminWidgets);
        widgets.Select(w => w.Kind).Distinct().Should().BeEquivalentTo("kpi", "chart", "feed");
    }

    [Fact]
    public async Task A_user_without_the_permission_does_not_get_the_widget_and_cannot_call_its_data()
    {
        var plain = await factory.SignInAsync("dash.plain@example.com");

        var widgets = (await plain.GetAsync<List<WidgetRow>>("/api/dashboard/widgets")).Data!;
        widgets.Select(w => w.Key).Should().Equal("notifications.unread");            // only the one that needs no permission

        var refused = await plain.GetAsync<JsonElement>("/api/dashboard/data?key=audit.recent");
        refused.Status.Should().Be(HttpStatusCode.Forbidden);
        refused.Body!.Message.Should().Contain("Audit.View");
    }

    [Fact]
    public async Task Each_widget_returns_data_in_the_shape_of_its_kind()
    {
        var admin = await factory.SignInAsync("dash.data@example.com", "Admin");

        var kpi = (await admin.GetAsync<JsonElement>("/api/dashboard/data?key=users.total")).Data;
        kpi.GetProperty("value").GetString().Should().NotBeNullOrEmpty();

        var chart = (await admin.GetAsync<JsonElement>("/api/dashboard/data?key=audit.activity-7d")).Data;
        chart.GetProperty("points").GetArrayLength().Should().Be(7);

        var feed = (await admin.GetAsync<JsonElement>("/api/dashboard/data?key=audit.recent")).Data;
        feed.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task An_unknown_widget_is_not_found()
    {
        var admin = await factory.SignInAsync("dash.unknown@example.com", "Admin");

        (await admin.GetAsync<JsonElement>("/api/dashboard/data?key=nope")).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_users_layout_is_saved_as_their_own_setting_and_does_not_affect_others()
    {
        var mine = await factory.SignInAsync("dash.mine@example.com", "Admin");
        var other = await factory.SignInAsync("dash.other@example.com", "Admin");

        var layout = JsonSerializer.Serialize(new { order = new[] { "audit.recent", "users.total" }, hidden = new[] { "audit.failed-24h" } });
        (await mine.PostAsync("/api/settings/update-mine", new { items = new[] { new { name = "Dashboard.Layout", value = layout } } }))
            .Status.Should().Be(HttpStatusCode.OK);

        var widgets = (await mine.GetAsync<List<WidgetRow>>("/api/dashboard/widgets")).Data!;
        widgets.Select(w => w.Key).Take(2).Should().Equal("audit.recent", "users.total");   // their order first
        widgets.Single(w => w.Key == "audit.failed-24h").Visible.Should().BeFalse();          // hidden, still listed for Customize
        widgets.Count.Should().Be(AdminWidgets.Length);                                        // hiding never removes a permitted widget

        var others = (await other.GetAsync<List<WidgetRow>>("/api/dashboard/widgets")).Data!;
        others.Select(w => w.Key).Should().Equal(AdminWidgets);
        others.Should().OnlyContain(w => w.Visible);
    }

    [Fact]
    public async Task A_corrupt_saved_layout_never_breaks_the_dashboard()
    {
        var user = await factory.SignInAsync("dash.corrupt@example.com", "Admin");
        await user.PostAsync("/api/settings/update-mine", new { items = new[] { new { name = "Dashboard.Layout", value = "{not json" } } });

        var widgets = await user.GetAsync<List<WidgetRow>>("/api/dashboard/widgets");

        widgets.Status.Should().Be(HttpStatusCode.OK);
        widgets.Data!.Select(w => w.Key).Should().Equal(AdminWidgets);
    }

    [Fact]
    public async Task The_layout_setting_is_not_listed_on_the_settings_screens()
    {
        var admin = await factory.SignInAsync("dash.settings@example.com", "Admin");

        (await admin.GetAsync<List<SettingRow>>("/api/settings/list")).Data!.Should().NotContain(s => s.Name == "Dashboard.Layout");
        (await admin.GetAsync<List<SettingRow>>("/api/settings/my")).Data!.Should().NotContain(s => s.Name == "Dashboard.Layout");
    }
}
