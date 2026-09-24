# MajdsApp.Modules.Plugins

**Plugins management (P5)** — SRS FR-PLUG-030, 033, 039, 040

The host side of runtime plugins: the persisted registry of discovered plugins, enable/disable, the menu contributions the Angular shell reads, and the gate that blocks a disabled plugin's API. Loading itself lives in the shared kernel (`PluginManager`).

## API

All responses use the `ResponseDto<T>` envelope unless noted.

- `GET /api/plugins/list`
- `GET /api/plugins/manifest`
- `POST /api/plugins/set-enabled`

## Permissions

`Plugins.View` (list) and `Plugins.Manage` (enable/disable). `GET /api/plugins/manifest` needs only sign-in and returns menu entries for enabled plugins.

Declared here: `Plugins.Manage`, `Plugins.View`.

## Data

Tables: `InstalledPlugins`. Migrations live in `MajdsApp.Core`.

## Notes

- Enable and disable take effect immediately: the plugin's endpoints answer 403 and its menu entry disappears without a restart. The assembly stays loaded until the process restarts.
- Not implemented: uploading or installing packages through the API, uninstall, upgrade or rollback, signature checks, dependency resolution, per-plugin settings.
- See `plugins/README.md` and `MajdsApp.Plugins.Tasks` for how to author and deploy a plugin.

## Configuration keys

- `Plugins:Directory` (default `<repo>/plugins`)

## Tests

Covered by the integration tests in `src/MajdsApp.Tests` (run `dotnet test src/MajdsApp.Tests`).
