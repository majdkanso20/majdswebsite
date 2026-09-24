# MajdsApp.Modules.Dashboard

**Dashboard (F-Dashboard)** — SRS FR-DASH-001..004

The home page's widget shell. This module owns no widgets: any module contributes one by registering an `IDashboardWidget` (defined in `MajdsApp.SharedKernel/Dashboard`), and the dashboard lists whatever is registered, so a new widget appears with no change here or in the Angular shell (AC-DASH-2).

## API

All responses use the `ResponseDto<T>` envelope. Any signed-in user may call both; what they get back depends on their permissions.

- `GET /api/dashboard/widgets` — the widgets the caller may see, in their own order, each with `key`, `title`, `kind` (`kpi`, `chart` or `feed`), `columns` and `visible`. Widgets the caller hid are still listed (`visible: false`) so the Customize panel can bring them back. A widget the caller lacks permission for is absent (AC-DASH-1).
- `GET /api/dashboard/data?key=…` — one widget's data. `403` if the caller lacks the widget's permission (the widget's query never runs), `404` for an unknown key.

## Baseline widgets

| Key | Kind | Permission | Registered by |
|---|---|---|---|
| `notifications.unread` | KPI | none (own data) | Notifications |
| `users.total` | KPI | `Users.View` | Users |
| `audit.failed-24h` | KPI | `Audit.View` | Audit |
| `audit.activity-7d` | chart | `Audit.View` | Audit |
| `audit.recent` | feed | `Audit.View` | Audit |

## Adding a widget

```csharp
public class OpenTasksWidget(ApplicationDbContext db) : IDashboardWidget
{
    public string Key => "tasks.open";
    public string Title => "Open tasks";
    public DashboardWidgetKind Kind => DashboardWidgetKind.Kpi;
    public string? Permission => "Tasks.View";      // null = any signed-in user
    public int Order => 50;
    public async Task<object> GetDataAsync(CancellationToken ct) =>
        new KpiWidgetData((await db.Set<TaskItem>().CountAsync(t => !t.Done, ct)).ToString());
}

// in the module's ConfigureServices:
services.AddScoped<IDashboardWidget, OpenTasksWidget>();
```

Return `KpiWidgetData`, `ChartWidgetData` or `FeedWidgetData` to match `Kind`.

## Settings defined

- `Dashboard.Layout` — per-user JSON `{"order":[…],"hidden":[…]}` (FR-DASH-004). It is a User-scope setting written by the dashboard's Customize panel, so it is marked internal and not listed on the settings screens. A corrupt value is ignored rather than breaking the page.

## Frontend

`features/dashboard`: the shell reads `/widgets` and renders a tile per widget inside `@defer (on viewport)`, so each tile loads its own data only when scrolled into view and a slow widget never blocks the rest. Tiles are styled with theme tokens only, so every skin applies.
