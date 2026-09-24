using FluentAssertions;
using MajdsApp.Modules.Dashboard;
using MajdsApp.SharedKernel.Dashboard;
using MajdsApp.SharedKernel.Security;
using MajdsApp.SharedKernel.Settings;
using Xunit;

namespace MajdsApp.Tests.Unit;

public class DashboardHandlerTests
{
    private sealed class FakeWidget(string key, int order, string? permission = null) : IDashboardWidget
    {
        public string Key => key;
        public string Title => key;
        public DashboardWidgetKind Kind => DashboardWidgetKind.Kpi;
        public string? Permission => permission;
        public int Order => order;
        public bool DataWasRequested { get; private set; }

        public Task<object> GetDataAsync(CancellationToken ct)
        {
            DataWasRequested = true;
            return Task.FromResult<object>(new KpiWidgetData("1"));
        }
    }

    private sealed class Grants(params string[] granted) : IPermissionChecker
    {
        public Task<bool> HasPermissionAsync(string permission, CancellationToken ct = default) => Task.FromResult(granted.Contains(permission));
    }

    private sealed class Caller : ICurrentUser
    {
        public string? UserId => "u1";
        public string? UserName => "u1";
        public bool IsAuthenticated => true;
    }

    private static GetDashboardWidgetsQueryHandler Handler(IEnumerable<IDashboardWidget> widgets, params string[] granted) =>
        new(widgets, new Grants(granted), new DefaultSettingsProvider(), new Caller());

    [Fact]
    public async Task A_newly_registered_widget_appears_without_any_change_to_the_dashboard()
    {
        var widgets = new IDashboardWidget[] { new FakeWidget("a", 1), new FakeWidget("brand-new", 2) };

        var result = await Handler(widgets).Handle(new GetDashboardWidgetsQuery(), default);

        result.Select(w => w.Key).Should().Equal("a", "brand-new");
    }

    [Fact]
    public async Task A_widget_needing_a_permission_is_left_out_when_it_is_not_granted()
    {
        var widgets = new IDashboardWidget[] { new FakeWidget("open", 1), new FakeWidget("secret", 2, "Secret.View") };

        (await Handler(widgets).Handle(new GetDashboardWidgetsQuery(), default)).Select(w => w.Key).Should().Equal("open");
        (await Handler(widgets, "Secret.View").Handle(new GetDashboardWidgetsQuery(), default)).Select(w => w.Key).Should().Equal("open", "secret");
    }

    [Fact]
    public async Task A_refused_widget_never_runs_its_data_query()
    {
        var secret = new FakeWidget("secret", 1, "Secret.View");
        var handler = new GetDashboardWidgetDataQueryHandler([secret], new Grants());

        var act = () => handler.Handle(new GetDashboardWidgetDataQuery("secret"), default);

        await act.Should().ThrowAsync<MajdsApp.SharedKernel.Exceptions.ForbiddenException>();
        secret.DataWasRequested.Should().BeFalse();
    }
}
