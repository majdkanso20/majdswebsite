# plugins/

The folder the API scans at startup for runtime plugins (`Plugins:Directory` overrides the location). Each subfolder is one plugin:

```
plugins/
  <PluginId>/
    plugin.json          manifest: id, name, version, author, assembly, moduleType, menu
    backend/
      <Plugin>.dll       the plugin assembly (plus .pdb)
```

- **Load:** at API startup. A folder that fails to load (missing manifest, missing or wrong assembly) is recorded with its error, shown under *Administration > Plugins*, and skipped; it never stops the host or other plugins.
- **Change:** use *Administration > Plugins > Install plugin* to upload a `.zip` package (it contains `plugin.json` and `backend/`). It is verified and applied the next time the API starts; the previous version is kept so it can be rolled back. Copying a build over the folder and restarting still works. Enable and disable are live from the admin screen.
- **Installer folders:** `.pending/` (staged, waiting for the next start), `.previous/` (the version before the last change) and `.staging/` (scratch). Folders starting with a dot are never treated as plugins.
- **Uninstall:** stops the plugin at once and deletes its folder at the next start. Its data is kept.
- **Compatibility:** an optional `minHostVersion` / `maxHostVersion` in `plugin.json` is checked against the platform version.
- **Trust:** set `Plugins:Trust:RequireAllowList` to `true` and list approved packages (id and SHA-256) under `Plugins:Trust:Allowed` so only those can be installed.
- **Trust:** plugin code runs in the API process with full trust. Only put reviewed code here.

`MajdsApp.Plugins.Tasks/` is the included sample. See `src/MajdsApp.Plugins.Tasks/README.md` for how to build and package one.

## Dependencies and hooks

`plugin.json` may add `"dependencies": [{ "id": "Other.Plugin", "minVersion": "1.0.0" }]`; the plugin is loaded only when they are installed and in range. Implement `IPluginLifecycle` on the module class named in `moduleType` to run code on install, upgrade, enable, disable and uninstall (see `MajdsApp.Plugins.Tasks/TasksModule.cs` and `src/MajdsApp.Modules.Plugins/README.md`).

Settings: declare `SettingDefinition`s (names starting with your plugin key, for example `Tasks.MaxTitleLength`) and they show on the settings page; add a `validator` to check values. See `MajdsApp.Plugins.Tasks/TaskSettings.cs`.

Translations: put `localization/ar.json` (English text to Arabic text) in the package; it is picked up for menu labels and server messages.

Frontend: put pre-built files in `frontend/`; they are served at `/plugins/<id>/<file>` while the plugin is enabled.
