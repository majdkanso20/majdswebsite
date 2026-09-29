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

## Signatures (FR-PLUG-036)

A publisher can sign a package: build the zip, then `PluginSignature.Sign(bytes, ecdsaPrivateKey, "acme")` (in `MajdsApp.SharedKernel.Plugins`) returns it with a `plugin.sig` added, holding the key id and an ECDSA P-256 signature over a digest of every file's name and SHA-256 (so adding, removing, renaming or changing a file breaks it). The administrator trusts a publisher by adding the base64 of its public key (`ExportSubjectPublicKeyInfo`) as `Plugins:Trust:Signers:acme`. When a package carries a signature it must verify against a trusted key or the upload is refused; `Plugins:Trust:RequireSignature=true` also refuses unsigned packages. The upload result and the audit trail show who signed it. The signature file itself is checked and never installed.

## Trust policy

`Plugins:Trust:RequireAllowList` (default `false`) and `Plugins:Trust:Allowed` (a list of `{ "Id": "...", "Sha256": "..." }`). With the allow-list required, only a package whose id **and** SHA-256 are listed can be installed. Turn it on in production. The response to an install includes the package's SHA-256 so it can be compared with what the publisher published. There is no digital-signature check.

## Dependencies and lifecycle hooks

- **Dependencies.** A manifest may list `"dependencies": [{ "id": "Acme.Base", "minVersion": "1.2.0", "maxVersion": "2.0.0" }]` (both versions optional). At start plugins are ordered so each comes after what it needs. A plugin whose dependency is not installed, is outside the range, failed to load, or is part of a cycle is **not loaded**: it is listed with the reason in `LastError`, and so are the plugins that depend on it. Later, a plugin cannot be enabled while a dependency is disabled, and cannot be disabled or uninstalled while an enabled plugin needs it (409 with the names).
- **Hooks.** A plugin's module may also implement `IPluginLifecycle` (in `MajdsApp.SharedKernel.Plugins`; every method has an empty default): `OnInstall` and `OnUpgrade(fromVersion)` run at the first start that sees the plugin, or a different version of it (a rollback counts), and a failure is shown as the plugin's error; `OnEnable` runs before the plugin is switched on and, if it throws, the plugin stays off and the administrator sees why; `OnDisable` and `OnUninstall` run after the change has happened, and a failure is only logged. A hook gets a service scope, so it can use the database, files and settings. Write each one so it is safe to run twice. The sample Tasks plugin implements them and logs.

## Plugin settings

A plugin declares settings the same way a module does (nested static classes holding `SettingDefinition` fields; see `MajdsApp.Plugins.Tasks/TaskSettings.cs`). They appear on the standard settings page under the plugin's group and are read through `ISettingsProvider`. A definition may carry a `validator` (returns a message, or null when the value is fine) that runs when an administrator saves it. A plugin's setting names must start with its key (the assembly name after `MajdsApp.Plugins.`, so `Tasks.` for `MajdsApp.Plugins.Tasks`); others are ignored, and a platform setting always wins a name clash.

## Frontend files

A package may hold pre-built frontend files in `frontend/` (a compiled bundle, styles, images, fonts; `.js .mjs .css .html .json .map .svg .png .jpg .gif .webp .ico .woff .woff2 .txt` only). They are served at `GET /plugins/{pluginId}/{path}` (for example `/plugins/MajdsApp.Plugins.Tasks/main.js`) without sign-in, because they are code and not data. Only an enabled plugin is served (a disabled one is a 404), nothing outside `frontend/` is reachable, and each response carries an ETag and `Cache-Control: no-cache`, so the browser asks before reusing a file and an upgrade shows up at once. The shell loads it as described below.

### Pre-built UI

`plugin.json` may declare `"frontend": { "entry": "main.js", "styles": "styles.css", "contract": 1, "angular": 22 }` (`styles` and `angular` optional), and a menu entry may add `"element": "my-plugin-view"`. The bundle is an ES module that defines that custom element (Web Components, no framework needed). `GET /api/plugins/manifest` then gives the shell the element, the bundle and stylesheet addresses and the versions. The shell loads the bundle only when the user opens the entry, mounts the element inside a shadow root (so styles cannot leak either way; theme colours are CSS custom properties and pass through), and sets `element.hostContext = { contract, pluginId, apiBaseUrl, language, getAccessToken() }` before it is attached. A plugin built for a different `contract` (this shell speaks 1) or Angular major version is not shown: an inline message replaces it. A bundle that fails to load or does not define its element shows an error with "Try again". A menu entry without `element` still uses the metadata-driven page. At load, a plugin that names an element without a frontend entry, or lists a frontend file that is not in the package, is refused. See `MajdsApp.Plugins.Tasks/frontend/main.js`.

## Plugin translations

A plugin may ship `localization/<language>.json` (for example `localization/ar.json`) next to `plugin.json`: a flat object keyed by the English text, in the platform's format, with `{0}` placeholders allowed. The platform adds these entries to its own for server messages, notifications and emails, and serves them with the rest at `GET /api/localization/resources`, so the shell translates the plugin's menu labels. A plugin can add entries but never replace one the platform already translates; a missing or malformed file leaves English. See `MajdsApp.Plugins.Tasks/localization/ar.json`.

## Recurring jobs

- *Plugin staging cleanup* (daily) removes `.staging` scratch folders left behind for a day. Staged changes in `.pending` are never touched.

## Data

Tables: `InstalledPlugins`. Migrations live in `MajdsApp.Core`.

## Notes

- Enable and disable take effect immediately; the assembly stays loaded until the process restarts.
- Plugin-shipped database migrations (FR-PLUG-011) are supported: a plugin can mark its assembly `[PluginOwnsItsDatabase]`, keep its entities in its own `DbContext`, and apply its own migrations from its `OnInstall`/`OnUpgrade` hook through `PluginMigrations.ApplyAsync<TContext>` (`MajdsApp.SharedKernel.Plugins`) — a separate migrations-history table keeps it independent of the host's and of every other plugin's. `MajdsApp.Plugins.Tasks` does this; see its README for the exact `dotnet ef` command (its `DbContext` needs its own design-time factory, since the project has no host of its own). Not implemented: applying a change without a restart.
- See `plugins/README.md` and `MajdsApp.Plugins.Tasks` for how to author and package a plugin.

## Configuration keys

- `Plugins:Directory` (default `<repo>/plugins`)
- `Plugins:Trust:RequireAllowList`, `Plugins:Trust:Allowed`
- `Plugins:Trust:RequireSignature`, `Plugins:Trust:Signers:<keyId>`

## Tests

`src/MajdsApp.Tests.Plugins` (its own process, because EF builds its model once per process): the installer on disk (`PluginInstallerTests`), and install, upgrade, uninstall and permission cleanup through the API against a running host (`PluginLifecycleTests`), and dependency ordering, its rejections and the hook runner (`PluginDependencyTests`), settings (`PluginSettingsTests`), translations (`PluginLocalizationTests`), frontend files (`PluginAssetTests`), the frontend declaration (`PluginFrontendTests`), signatures (`PluginSignatureTests`) and state, checks and permissions (`PluginDiagnosticsTests`).
