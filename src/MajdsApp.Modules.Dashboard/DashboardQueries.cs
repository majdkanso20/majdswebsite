using System.Text.Json;
using MajdsApp.SharedKernel.Dashboard;
using MajdsApp.SharedKernel.Exceptions;
using MajdsApp.SharedKernel.Security;
using MajdsApp.SharedKernel.Settings;
using MediatR;

namespace MajdsApp.Modules.Dashboard;

public record DashboardWidgetDto(string Key, string Title, string Kind, int Columns, bool Visible);

/// <summary>The widgets the caller is permitted to see, in their own order, with the ones they hid flagged
/// (FR-DASH-002/004). A widget the caller lacks permission for is absent entirely (AC-DASH-1).</summary>
public record GetDashboardWidgetsQuery : IRequest<IReadOnlyList<DashboardWidgetDto>>;

public class GetDashboardWidgetsQueryHandler(
    IEnumerable<IDashboardWidget> widgets, IPermissionChecker permissions, ISettingsProvider settings, ICurrentUser currentUser)
    : IRequestHandler<GetDashboardWidgetsQuery, IReadOnlyList<DashboardWidgetDto>>
{
    public async Task<IReadOnlyList<DashboardWidgetDto>> Handle(GetDashboardWidgetsQuery request, CancellationToken ct)
    {
        var permitted = new List<IDashboardWidget>();
        foreach (var widget in widgets.OrderBy(w => w.Order).ThenBy(w => w.Title))
        {
            if (widget.Permission is null || await permissions.HasPermissionAsync(widget.Permission, ct))
                permitted.Add(widget);
        }

        var layout = Layout.Parse((await settings.GetAllForUserAsync(currentUser.UserId, ct)).GetValueOrDefault("Dashboard.Layout"));

        // Widgets the user has placed come first, in their order; newer ones keep their default position after them.
        var ordered = permitted
            .Select((w, index) => (Widget: w, Index: index))
            .OrderBy(x => layout.Order.IndexOf(x.Widget.Key) is var i and >= 0 ? i : int.MaxValue)
            .ThenBy(x => x.Index)
            .Select(x => x.Widget);

        return ordered.Select(w => new DashboardWidgetDto(w.Key, w.Title, w.Kind.ToString().ToLowerInvariant(),
            Math.Clamp(w.Columns, 1, 2), !layout.Hidden.Contains(w.Key))).ToList();
    }
}

/// <summary>One widget's data. Refused for a widget the caller may not see, so hiding it in the UI is never the only guard.</summary>
public record GetDashboardWidgetDataQuery(string Key) : IRequest<object>;

public class GetDashboardWidgetDataQueryHandler(IEnumerable<IDashboardWidget> widgets, IPermissionChecker permissions)
    : IRequestHandler<GetDashboardWidgetDataQuery, object>
{
    public async Task<object> Handle(GetDashboardWidgetDataQuery request, CancellationToken ct)
    {
        var widget = widgets.FirstOrDefault(w => w.Key == request.Key) ?? throw new NotFoundException("Widget not found.");

        if (widget.Permission is not null && !await permissions.HasPermissionAsync(widget.Permission, ct))
            throw new ForbiddenException($"Missing permission: {widget.Permission}");

        return await widget.GetDataAsync(ct);
    }
}

internal record Layout(List<string> Order, HashSet<string> Hidden)
{
    public static Layout Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Layout([], []);
        try
        {
            var stored = JsonSerializer.Deserialize<StoredLayout>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return new Layout(stored?.Order ?? [], (stored?.Hidden ?? []).ToHashSet());
        }
        catch (JsonException)
        {
            return new Layout([], []); // a corrupt preference must never break the dashboard
        }
    }

    private record StoredLayout(List<string>? Order, List<string>? Hidden);
}
