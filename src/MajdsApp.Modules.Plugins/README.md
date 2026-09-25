# MajdsApp.Modules.Plugins

**Plugins management (P5)** — SRS FR-PLUG-009, 028, 030, 032-034, 036, 038-042

The host side of runtime plugins: the persisted registry of plugins, enable/disable, installing and upgrading from an uploaded package, rollback, uninstall, the menu contributions the Angular shell reads, and the gate that blocks a disabled plugin's API. Loading itself and the on-disk installer live in the shared kernel (`PluginManager`, `PluginInstaller`).

## API

All responses use the `ResponseDto<T>` envelope unless noted.

- `GET /api/plugins/list` — installed plugins with version, state, load error, declared permissions, menu entries, whether a rollback is available, whether an uninstall is pending, and the platform range they declare.
- `GET /api/plugins/pending` — changes that will be applied at the next start (install, upgrade, rollback, uninstall).
- `GET /api/plugins/manifest` — menu entries of enabled plugins (sign-in only).
- `POST /api/plugins/set-enabled` — enable or disable, live.
- `POST /api/plugins/install` — multipart `file` (.zip, up to 50 MB) and optional `sha256` (the publisher's checksum). Verifies and stages the package; returns the action (`Install` or `Upgrade`), the version it replaces and the package's SHA-256.
- `POST /api/plugins/rollback` — stages the version that was installed before the last change.
- `POST /api/plugins/uninstall` — see below.
- `POST /api/plugins/cancel-pending` — drops a staged change or an uninstall before it is applied.

## Permissions

`Plugins.View` (list, pending) and `Plugins.Manage` (everything that changes something). Every install, upgrade, rollback, uninstall and enable/disable is audited.

Declared here: `Plugins.Manage`, `Plugins.View`.

## How install, upgrade, rollback and uninstall work

A plugin is loaded once, at startup, because its endpoints, handlers and entity configuration are part of the host's startup wiring, and a loaded assembly is locked on Windows. So a change is **verified now and applied at the next start**, before anything is loaded:

1. **Install / upgrade.** The package is checked: size, checksum (when supplied), zip safety (no paths outside the folder, only `.dll .pdb .json .txt .md .xml`, limits on entries and unpacked size), the manifest (id, version, assembly, module type), platform compatibility, the trust policy, the version rule (an installed version is only replaced by a newer one), and that its module type loads and implements `IFeatureModule`. Only then is it staged under `plugins/.pending/<id>`. Any failure leaves the plugins folder exactly as it was.
2. **At the next start** staged changes are applied: the current version moves to `.previous/<id>` (kept for rollback) and the new one takes its place. If that cannot be done, the old version is put back. Only folders the installer itself staged are applied; a hand-placed folder in `.pending` is discarded.
3. **Uninstall** stops the plugin immediately (its API answers 403, its menu entry disappears), removes its registry entry, and removes its permissions from every role and user that held them. A marker is written and the files are deleted at the next start. **Its data is always kept**; dropping a plugin's tables needs a reviewed migration and is not offered.
4. **Rollback** stages the `.previous` version to be restored at the next start.

## Trust policy

`Plugins:Trust:RequireAllowList` (default `false`) and `Plugins:Trust:Allowed` (a list of `{ "Id": "...", "Sha256": "..." }`). With the allow-list required, only a package whose id **and** SHA-256 are listed can be installed. Turn it on in production. The response to an install includes the package's SHA-256 so it can be compared with what the publisher published. There is no digital-signature check.

## Recurring jobs

- *Plugin staging cleanup* (daily) removes `.staging` scratch folders left behind for a day. Staged changes in `.pending` are never touched.

## Data

Tables: `InstalledPlugins`. Migrations live in `MajdsApp.Core`.

## Notes

- Enable and disable take effect immediately; the assembly stays loaded until the process restarts.
- Not implemented: digital signatures, dependency resolution, per-plugin settings, lifecycle hooks, applying changes without a restart.
- See `plugins/README.md` and `MajdsApp.Plugins.Tasks` for how to author and package a plugin.

## Configuration keys

- `Plugins:Directory` (default `<repo>/plugins`)
- `Plugins:Trust:RequireAllowList`, `Plugins:Trust:Allowed`

## Tests

`src/MajdsApp.Tests.Plugins` (its own process, because EF builds its model once per process): the installer on disk (`PluginInstallerTests`), and install, upgrade, uninstall and permission cleanup through the API against a running host (`PluginLifecycleTests`).
