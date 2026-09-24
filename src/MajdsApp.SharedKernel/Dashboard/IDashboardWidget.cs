namespace MajdsApp.SharedKernel.Dashboard;

public enum DashboardWidgetKind { Kpi, Chart, Feed }

/// <summary>
/// One tile of the dashboard (F-Dashboard FR-DASH-001). A module contributes a widget by registering an
/// implementation (<c>services.AddScoped&lt;IDashboardWidget, MyWidget&gt;()</c>); the dashboard module and the
/// Angular shell discover it, so a new widget appears without any change to the dashboard itself (AC-DASH-2).
/// Each widget loads its own data through <see cref="GetDataAsync"/>, independently of the others.
/// </summary>
public interface IDashboardWidget
{
    /// <summary>Stable identifier, also the key stored in a user's layout preference.</summary>
    string Key { get; }

    string Title { get; }

    DashboardWidgetKind Kind { get; }

    /// <summary>Permission needed to see the widget and to call its data (FR-DASH-002); null = any signed-in user.</summary>
    string? Permission { get; }

    /// <summary>Default position (ascending) before a user reorders.</summary>
    int Order { get; }

    /// <summary>Grid columns the tile spans on a wide screen (1 or 2).</summary>
    int Columns => 1;

    /// <summary>The data for this widget: a <see cref="KpiWidgetData"/>, <see cref="ChartWidgetData"/> or
    /// <see cref="FeedWidgetData"/> matching <see cref="Kind"/>.</summary>
    Task<object> GetDataAsync(CancellationToken ct);
}

public record KpiWidgetData(string Value, string? Caption = null);

public record ChartPoint(string Label, double Value);

public record ChartWidgetData(IReadOnlyList<ChartPoint> Points);

public record FeedItem(string Title, string? Subtitle, DateTime? At);

public record FeedWidgetData(IReadOnlyList<FeedItem> Items);
