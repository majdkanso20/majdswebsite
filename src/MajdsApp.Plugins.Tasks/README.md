# MajdsApp.Plugins.Tasks

A sample **runtime plugin** (SRS P5) and the template for writing your own. It adds a Tasks feature — a table, CRUD API, four permissions and a menu entry — to a running platform **without the host being rebuilt or referencing this project**.

## What installing it does

Drop the built package into the host's plugins folder and restart the API. Then, with no host code changes:

| Contribution | How it appears |
|---|---|
| **API** | `GET /api/tasks/list`, `GET /api/tasks/get`, `GET /api/tasks/ui-schema`, `POST /api/tasks/create`, `POST /api/tasks/update`, `POST /api/tasks/delete` |
| **Permissions** | `Tasks.View`, `Tasks.Create`, `Tasks.Edit`, `Tasks.Delete` — grouped under *Tasks* in the role editor, ready to grant to roles |
| **Menu entry** | *Tasks* in the navigation, shown only to users holding `Tasks.View` |
| **UI** | A generic, schema-driven page: the plugin serves a JSON description of its columns and form fields (`ui-schema`) and the host's shared grid and dialog render it. Create/Edit/Delete buttons follow the permissions. |
| **Data** | Table `Plugin_Tasks_Items` (soft delete and audit fields, audited commands) |

Users open it from the navigation at `/tasks`. Administrators can disable and re-enable it under **Administration > Plugins**; while disabled its API answers 403 and its menu entry is gone, and its data is kept.

## Package layout

```
plugins/
  MajdsApp.Plugins.Tasks/
    plugin.json                       manifest
    backend/
      MajdsApp.Plugins.Tasks.dll      the plugin assembly (and .pdb)
```

`plugin.json`:

```json
{
  "id": "MajdsApp.Plugins.Tasks",
  "name": "Tasks",
  "version": "1.0.0",
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
# copy plugin.json to plugins/MajdsApp.Plugins.Tasks/ and the built MajdsApp.Plugins.Tasks.* files to .../backend/
```

Because the plugin owns a table, generate the migration once with the plugin already in the plugins folder (`dotnet ef migrations add ... --project src/MajdsApp.Core --startup-project src/MajdsApp.Api`) and apply it; the host discovers the plugin's entity configuration when it starts.

## Writing your own plugin

1. New class library named `MajdsApp.Plugins.<Name>` targeting `net10.0` with the ASP.NET Core framework reference. **Keep that prefix**: permission, entity and permission-tree discovery key off it.
2. Reference `MajdsApp.SharedKernel` and `MajdsApp.Core` with **`Private="false"`**:
   ```xml
   <ProjectReference Include="..\MajdsApp.SharedKernel\MajdsApp.SharedKernel.csproj" Private="false" />
   <ProjectReference Include="..\MajdsApp.Core\MajdsApp.Core.csproj" Private="false" />
   ```
   This is load-bearing. It stops those assemblies (and MediatR, FluentValidation, EF Core) being copied next to your dll. If they were, the plugin's isolated load context would load a second copy, its `IFeatureModule` would be a different type from the host's, and every discovery mechanism would silently miss your plugin.
3. Add a class implementing `IFeatureModule` (its `ConfigureServices` can be empty) and name it in `plugin.json` as `moduleType`.
4. Write commands, queries, validators and a controller exactly as in a core module: `[RequiresPermission]` on requests, `IAuditableCommand` on changes, a controller deriving from `ApiControllerBase`, `IEntityTypeConfiguration<T>` for entities, and a `Permissions` class of nested `const string`s.
5. For a CRUD screen, serve a `ui-schema` endpoint like this project's `UiSchema.cs`; the host page needs no changes. (A plugin needing a bespoke UI is not supported yet; see the limits.)
6. Add `plugin.json` to the project with `CopyToOutputDirectory`, build, and deploy as above.

## Limits (be honest with stakeholders)

- Plugins are loaded **at startup**; the folder is not watched, and installing through the API is not implemented. Disable/enable is live, unloading is not (the assembly stays in memory until restart).
- The plugin's code runs inside the API process with full trust. There is no signature or checksum verification and no sandbox: install only reviewed code.
- The plugin's table is created by a migration in `MajdsApp.Core`, not by the plugin; per-plugin schema isolation, upgrades, rollback and uninstall are not implemented.
- The UI is the metadata-driven page only; Native Federation or web-component bundles for rich custom screens are not supported.

## Tests

The integration tests build this project, copy it into a temporary plugins folder and load it at runtime, then exercise discovery, permissions, menu, CRUD, validation and enable/disable (`src/MajdsApp.Tests/Integration/AccountAndPluginTests.cs`).
