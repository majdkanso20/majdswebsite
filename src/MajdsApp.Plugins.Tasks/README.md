# MajdsApp.Plugins.Tasks

A sample **runtime plugin** (SRS P5) and the template for writing your own. It adds a Tasks feature — its own database table, CRUD API, four permissions, a menu entry and a pre-built overview screen — to a running platform **without the host being rebuilt or referencing this project**.

## What installing it does

Drop the built package into the host's plugins folder (or upload it under **Administration > Plugins > Install plugin**) and restart the API. Then, with no host code changes:

| Contribution | How it appears |
|---|---|
| **API** | `GET /api/tasks/list`, `GET /api/tasks/get`, `GET /api/tasks/ui-schema`, `POST /api/tasks/create`, `POST /api/tasks/update`, `POST /api/tasks/delete` |
| **Permissions** | `Tasks.View`, `Tasks.Create`, `Tasks.Edit`, `Tasks.Delete` — grouped under *Tasks* in the role editor, ready to grant to roles |
| **Menu entries** | *Tasks* (the metadata-driven grid) and *Tasks overview* (a pre-built custom element), shown only to users holding `Tasks.View` |
| **UI** | *Tasks* is schema-driven: the plugin serves a JSON description of its columns and form fields (`ui-schema`) and the host's shared grid and dialog render it. *Tasks overview* is a pre-built ES-module bundle (`frontend/`), mounted in a shadow root. |
| **Data** | Its own table, `Plugin_Tasks_Items` (soft delete and audit fields, audited commands) in its **own database** — see below |

Users open it from the navigation at `/tasks`. Administrators can disable and re-enable it under **Administration > Plugins**; while disabled its API answers 403 and its menu entries are gone, and its data is kept.

## Its own database (P5 FR-PLUG-011)

This plugin's assembly is marked `[assembly: PluginOwnsItsDatabase]` (in `TasksModule.cs`): its entity (`TaskItem`) is **not** part of the host's shared `ApplicationDbContext` model, and its migrations are compiled into this project, not into `MajdsApp.Core`. `TasksDbContext` is the plugin's own `DbContext`, registered in `TasksModule.ConfigureServices`, pointed at the same physical database (the host's `ConnectionStrings:DefaultConnection`) but tracked in its own `__MajdsApp_Plugins_TasksMigrationsHistory` table — entirely independent of the host's migrations and of any other plugin's.

`TasksModule.OnInstallAsync`/`OnUpgradeAsync` call `PluginMigrations.ApplyAsync<TasksDbContext>(context)` (in `MajdsApp.SharedKernel.Plugins`) to bring the database up to date — the host never runs this plugin's migrations itself, and never even sees `TaskItem` in its own model. This is the fuller isolation option FR-PLUG-011 names; a plugin with one small table can skip all of it and just add a normal `IEntityTypeConfiguration<T>` to the host's shared context with a prefixed table name instead (simpler, and what most plugins should probably do — this sample demonstrates the alternative on purpose).

**Generating a migration for this plugin** (its `DbContext` lives in a class library with no host of its own, so `dotnet ef` needs its design-time factory, `TasksDbContextFactory.cs`, and must be pointed at this project both ways):

```bash
dotnet ef migrations add <Name> --project src/MajdsApp.Plugins.Tasks --startup-project src/MajdsApp.Plugins.Tasks --context TasksDbContext
```

Deploy the result the normal way (rebuild, copy `backend/` and `plugin.json` over, restart, or upload a new package) — remember to **bump the version in `plugin.json`**, or `OnUpgradeAsync` (where the migration is applied) never runs for an installation that already has this plugin registered.

## Package layout

```
plugins/
  MajdsApp.Plugins.Tasks/
    plugin.json                       manifest
    backend/
      MajdsApp.Plugins.Tasks.dll      the plugin assembly (and .pdb) — includes its own compiled migrations
    frontend/
      main.js, styles.css             the pre-built Tasks overview bundle
    localization/
      ar.json                         Arabic text for this plugin's menu labels and messages
```

`plugin.json`:

```json
{
  "id": "MajdsApp.Plugins.Tasks",
  "name": "Tasks",
  "version": "1.0.1",
  "author": "Demo",
  "assembly": "MajdsApp.Plugins.Tasks.dll",
  "moduleType": "MajdsApp.Plugins.Tasks.TasksModule",
  "menu": [
    { "label": "Tasks", "icon": "checklist", "route": "tasks", "permission": "Tasks.View", "order": 45 }
  ]
}
```

The menu `route` doubles as the API path segment (`api/tasks/...`), so name the controller to match.

## Build and deploy

```bash
dotnet build src/MajdsApp.Plugins.Tasks -c Release
# copy plugin.json, the built MajdsApp.Plugins.Tasks.* files (to backend/), frontend/ and localization/ into
# plugins/MajdsApp.Plugins.Tasks/, or package them into a .zip and use Administration > Plugins > Install plugin.
```

## Writing your own plugin

1. New class library named `MajdsApp.Plugins.<Name>` targeting `net10.0` with the ASP.NET Core framework reference. **Keep that prefix**: permission, entity and permission-tree discovery key off it.
2. Reference `MajdsApp.SharedKernel` and `MajdsApp.Core` with **`Private="false"`**:
   ```xml
   <ProjectReference Include="..\MajdsApp.SharedKernel\MajdsApp.SharedKernel.csproj" Private="false" />
   <ProjectReference Include="..\MajdsApp.Core\MajdsApp.Core.csproj" Private="false" />
   ```
   This is load-bearing. It stops those assemblies (and MediatR, FluentValidation, EF Core) being copied next to your dll. If they were, the plugin's isolated load context would load a second copy, its `IFeatureModule` would be a different type from the host's, and every discovery mechanism would silently miss your plugin.
3. Add a class implementing `IFeatureModule` and name it in `plugin.json` as `moduleType`.
4. Write commands, queries, validators and a controller exactly as in a core module: `[RequiresPermission]` on requests, `IAuditableCommand` on changes, a controller deriving from `ApiControllerBase`, and a `Permissions` class of nested `const string`s.
5. For your data, pick one:
   - **Simplest:** an `IEntityTypeConfiguration<T>` on the shared `ApplicationDbContext`, with a table name prefixed by your plugin id, and let its migration live in `MajdsApp.Core` (`dotnet ef migrations add ... --project src/MajdsApp.Core --startup-project src/MajdsApp.Api`, with your plugin already deployed to the plugins folder so the host's design-time model picks up your entity).
   - **Fully isolated (FR-PLUG-011), like this sample:** your own `DbContext`, marked `[assembly: PluginOwnsItsDatabase]`, registered in `ConfigureServices`, with `PluginMigrations.ApplyAsync<YourContext>(context)` called from `OnInstallAsync`/`OnUpgradeAsync`. See `TasksDbContext.cs`, `TasksDbContextFactory.cs` and `TasksModule.cs` for the full pattern, including reusing the host's own soft-delete/audit-stamping interceptor.
6. For a CRUD screen, serve a `ui-schema` endpoint like this project's `UiSchema.cs`; the host page needs no changes. For a richer custom UI, see the `frontend` manifest entry and `frontend/main.js`/`styles.css` here.
7. Add `plugin.json` to the project with `CopyToOutputDirectory`, build, and deploy as above.

## Limits (be honest with stakeholders)

- Plugins are loaded **at startup**; the folder is not watched between restarts (installing through the admin API stages a change that is applied at the *next* start).
- The plugin's code runs inside the API process with full trust; `Plugins:Trust:RequireSignature`/`Plugins:Trust:RequireAllowList` can refuse an unsigned or unlisted package, but neither is required by default — install only reviewed code.
- Rollback undoes an upgrade's *files*; it does not automatically reverse a migration a newer version applied (a real down-migration path is not implemented). Uninstall never drops this plugin's tables — its data is always kept.
- The UI options are the metadata-driven page and a pre-built Web Components bundle (this sample uses both); Native Federation is not supported.

## Tests

`src/MajdsApp.Tests.Plugins` builds this project, copies it into a temporary plugins folder and loads it at runtime, then exercises discovery, permissions, menu, CRUD, validation, enable/disable, install/upgrade/rollback/uninstall, signatures, settings, localization and (`PluginDatabaseIsolationTests` in `src/MajdsApp.Tests`, run against the *host* alone) that the host's own database and model never mention this plugin's table.
