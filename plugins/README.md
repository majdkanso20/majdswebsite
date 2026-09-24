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
- **Change:** copy a new build over the old one and restart the API. Enable and disable are live from the admin screen.
- **Trust:** plugin code runs in the API process with full trust. Only put reviewed code here.

`MajdsApp.Plugins.Tasks/` is the included sample. See `src/MajdsApp.Plugins.Tasks/README.md` for how to build and package one.
