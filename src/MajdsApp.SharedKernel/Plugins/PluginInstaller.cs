using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentValidation;

namespace MajdsApp.SharedKernel.Plugins;

public record StagedPlugin(PluginManifest Manifest, string Action, string Sha256, string? PreviousVersion);

/// <summary>A change waiting for the next start: a staged install, upgrade or rollback, or an uninstall.</summary>
public record PendingPluginChange(string Id, string Name, string Version, string Action, DateTime StagedAt);

/// <summary>
/// Installs, upgrades, rolls back and uninstalls plugin packages on disk (P5 FR-PLUG-032/034/036/038).
///
/// Plugin assemblies are loaded once at startup (their controllers, handlers and entity configurations are part of the
/// host's startup wiring) and a loaded assembly is locked on Windows, so every change is <b>staged</b> and <b>applied at
/// the next start</b>, before anything is loaded. That is also what makes it safe: a package is fully verified before it
/// is staged, the previous version is kept for rollback, and a change that fails to apply is undone, so the host is
/// never left half-installed.
///
/// Layout under the plugins folder: <c>&lt;id&gt;/</c> installed, <c>.pending/&lt;id&gt;/</c> staged, <c>.previous/&lt;id&gt;/</c>
/// the version before the last change, <c>.staging/</c> scratch space, and <c>&lt;id&gt;/.uninstall</c> the uninstall marker.
/// </summary>
public static class PluginInstaller
{
    public const long MaxPackageBytes = 50L * 1024 * 1024;
    private const long MaxUnpackedBytes = 200L * 1024 * 1024;
    private const int MaxEntries = 500;
    private const string MetaFile = ".meta.json";
    private const string UninstallMarker = ".uninstall";

    private static readonly Regex SafeId = new(@"^[A-Za-z][A-Za-z0-9._-]{1,99}$", RegexOptions.Compiled);
    private static readonly string[] AllowedExtensions = [".dll", ".pdb", ".json", ".txt", ".md", ".xml"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    private record StageMeta(string Action, string Sha256, DateTime StagedAt);

    // ---- verify and stage ---------------------------------------------------------------------------------------

    /// <summary>
    /// Verifies a package and stages it for the next start. Nothing on disk that is in use is touched; any failure leaves the
    /// plugins folder exactly as it was. Checks, in order: size, checksum (when the publisher's is supplied), zip safety,
    /// manifest, host compatibility, the trust policy, the version rule, and that the plugin's module can actually be loaded.
    /// </summary>
    public static StagedPlugin Stage(Stream package, string? expectedSha256, PluginHostOptions options)
    {
        var bytes = ReadBounded(package);
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(expectedSha256) && !FixedTimeEqualsHex(expectedSha256, sha256))
            throw new ValidationException("The package does not match the checksum you provided. It may be corrupted or not the publisher's file.");

        var staging = Path.Combine(options.Directory, ".staging", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(staging);
            var manifest = ExtractAndReadManifest(bytes, staging);

            CheckHostCompatibility(manifest);
            CheckTrustPolicy(manifest.Id, sha256, options);

            var installedManifest = ReadInstalledManifest(options.Directory, manifest.Id);
            var action = "Install";
            if (installedManifest is not null)
            {
                if (!IsNewer(manifest.Version, installedManifest.Version))
                    throw new ValidationException($"'{manifest.Id}' {installedManifest.Version} is already installed. Upload a newer version to upgrade it.");
                action = "Upgrade";
            }

            VerifyModuleLoads(staging, manifest);

            var pending = Path.Combine(options.Directory, ".pending", manifest.Id);
            if (Directory.Exists(pending)) Directory.Delete(pending, recursive: true);
            Directory.CreateDirectory(Path.GetDirectoryName(pending)!);
            File.WriteAllText(Path.Combine(staging, MetaFile), JsonSerializer.Serialize(new StageMeta(action, sha256, DateTime.UtcNow), Json));
            Directory.Move(staging, pending);

            return new StagedPlugin(manifest, action, sha256, installedManifest?.Version);
        }
        finally
        {
            if (Directory.Exists(staging)) TryDelete(staging);
        }
    }

    /// <summary>Stages the version that was installed before the last change, to be restored at the next start.</summary>
    public static StagedPlugin StageRollback(string pluginId, PluginHostOptions options)
    {
        EnsureSafeId(pluginId);
        var previous = Path.Combine(options.Directory, ".previous", pluginId);
        var manifest = ReadManifestFile(Path.Combine(previous, "plugin.json"))
            ?? throw new ValidationException($"There is no earlier version of '{pluginId}' to roll back to.");

        var pending = Path.Combine(options.Directory, ".pending", pluginId);
        if (Directory.Exists(pending)) Directory.Delete(pending, recursive: true);
        CopyDirectory(previous, pending);
        File.WriteAllText(Path.Combine(pending, MetaFile), JsonSerializer.Serialize(new StageMeta("Rollback", "", DateTime.UtcNow), Json));

        return new StagedPlugin(manifest, "Rollback", "", ReadInstalledManifest(options.Directory, pluginId)?.Version);
    }

    // ---- uninstall ------------------------------------------------------------------------------------------------

    /// <summary>Marks an installed plugin for removal at the next start (its files cannot be deleted while it is loaded).</summary>
    public static void MarkForUninstall(string pluginId, PluginHostOptions options)
    {
        EnsureSafeId(pluginId);
        var folder = Path.Combine(options.Directory, pluginId);
        if (!Directory.Exists(folder))
            throw new Exceptions.NotFoundException($"Plugin '{pluginId}' is not installed.");

        File.WriteAllText(Path.Combine(folder, UninstallMarker), DateTime.UtcNow.ToString("O"));
        var pending = Path.Combine(options.Directory, ".pending", pluginId);
        if (Directory.Exists(pending)) Directory.Delete(pending, recursive: true);
    }

    /// <summary>Drops a staged change or an uninstall marker before it is applied.</summary>
    public static bool CancelPending(string pluginId, PluginHostOptions options)
    {
        EnsureSafeId(pluginId);
        var cancelled = false;
        var pending = Path.Combine(options.Directory, ".pending", pluginId);
        if (Directory.Exists(pending)) { Directory.Delete(pending, recursive: true); cancelled = true; }

        var marker = Path.Combine(options.Directory, pluginId, UninstallMarker);
        if (File.Exists(marker)) { File.Delete(marker); cancelled = true; }

        return cancelled;
    }

    public static IReadOnlyList<PendingPluginChange> ListPending(string pluginsDirectory)
    {
        var changes = new List<PendingPluginChange>();
        if (!Directory.Exists(pluginsDirectory)) return changes;

        var pendingRoot = Path.Combine(pluginsDirectory, ".pending");
        if (Directory.Exists(pendingRoot))
        {
            foreach (var dir in Directory.GetDirectories(pendingRoot))
            {
                var manifest = ReadManifestFile(Path.Combine(dir, "plugin.json"));
                var meta = ReadMeta(dir);
                if (manifest is not null)
                    changes.Add(new PendingPluginChange(manifest.Id, manifest.Name, manifest.Version, meta?.Action ?? "Install", meta?.StagedAt ?? Directory.GetCreationTimeUtc(dir)));
            }
        }

        foreach (var dir in Directory.GetDirectories(pluginsDirectory).Where(d => !Path.GetFileName(d).StartsWith('.')))
        {
            var marker = Path.Combine(dir, UninstallMarker);
            if (!File.Exists(marker)) continue;
            var manifest = ReadManifestFile(Path.Combine(dir, "plugin.json"));
            changes.Add(new PendingPluginChange(manifest?.Id ?? Path.GetFileName(dir), manifest?.Name ?? Path.GetFileName(dir),
                manifest?.Version ?? "", "Uninstall", File.GetLastWriteTimeUtc(marker)));
        }

        return changes.OrderBy(c => c.StagedAt).ToList();
    }

    public static bool HasPreviousVersion(string pluginsDirectory, string pluginId) =>
        SafeId.IsMatch(pluginId) && File.Exists(Path.Combine(pluginsDirectory, ".previous", pluginId, "plugin.json"));

    // ---- apply at startup -----------------------------------------------------------------------------------------

    /// <summary>
    /// Applies staged installs, upgrades, rollbacks and uninstalls. Runs before any plugin is loaded. Each change is applied on its
    /// own: if one fails, that plugin is put back as it was (FR-PLUG-032) and the others are unaffected.
    /// </summary>
    public static IReadOnlyList<string> ApplyPendingChanges(string pluginsDirectory)
    {
        var messages = new List<string>();
        if (!Directory.Exists(pluginsDirectory)) return messages;

        foreach (var dir in Directory.GetDirectories(pluginsDirectory).Where(d => !Path.GetFileName(d).StartsWith('.')).ToList())
        {
            if (!File.Exists(Path.Combine(dir, UninstallMarker))) continue;
            try
            {
                Directory.Delete(dir, recursive: true);
                messages.Add($"Uninstalled '{Path.GetFileName(dir)}'.");
            }
            catch (Exception ex)
            {
                messages.Add($"Could not remove '{Path.GetFileName(dir)}': {ex.Message}");
            }
        }

        var pendingRoot = Path.Combine(pluginsDirectory, ".pending");
        if (!Directory.Exists(pendingRoot)) return messages;

        foreach (var pending in Directory.GetDirectories(pendingRoot))
        {
            var id = Path.GetFileName(pending);
            var target = Path.Combine(pluginsDirectory, id);
            var previous = Path.Combine(pluginsDirectory, ".previous", id);
            var backup = Path.Combine(pluginsDirectory, ".staging", "undo-" + id);
            try
            {
                if (!SafeId.IsMatch(id)) throw new InvalidOperationException("Invalid plugin id.");

                // Only what Stage() verified is applied: it leaves a meta file and a manifest whose id is the folder name.
                // Anything else in .pending (a hand-placed folder, a half-written copy) is discarded, never installed.
                var meta = ReadMeta(pending);
                var staged = ReadManifestFile(Path.Combine(pending, "plugin.json"));
                if (meta is null || staged is null || !staged.Id.Equals(id, StringComparison.Ordinal))
                    throw new InvalidOperationException("It was not staged by the installer.");
                File.Delete(Path.Combine(pending, MetaFile));

                Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                if (Directory.Exists(backup)) Directory.Delete(backup, recursive: true);
                var hadInstalled = Directory.Exists(target);
                if (hadInstalled) Directory.Move(target, backup);

                try
                {
                    Directory.Move(pending, target);
                }
                catch
                {
                    if (hadInstalled) Directory.Move(backup, target); // undo: the old version is back exactly as it was
                    throw;
                }

                if (hadInstalled)
                {
                    if (Directory.Exists(previous)) Directory.Delete(previous, recursive: true);
                    Directory.CreateDirectory(Path.GetDirectoryName(previous)!);
                    Directory.Move(backup, previous); // kept for rollback (FR-PLUG-038)
                }

                messages.Add($"{meta?.Action ?? "Installed"} '{id}'.");
            }
            catch (Exception ex)
            {
                messages.Add($"Could not apply the pending change for '{id}': {ex.Message}");
                TryDelete(pending);
            }
        }

        return messages;
    }

    // ---- helpers --------------------------------------------------------------------------------------------------

    private static byte[] ReadBounded(Stream package)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = package.Read(chunk, 0, chunk.Length)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxPackageBytes)
                throw new ValidationException($"The package is larger than {MaxPackageBytes / 1024 / 1024} MB.");
        }

        if (buffer.Length == 0) throw new ValidationException("The package is empty.");
        return buffer.ToArray();
    }

    private static PluginManifestJson ExtractAndReadManifestJson(ZipArchive zip)
    {
        var entry = zip.GetEntry("plugin.json") ?? throw new ValidationException("The package has no plugin.json at its root.");
        using var reader = new StreamReader(entry.Open());
        return JsonSerializer.Deserialize<PluginManifestJson>(reader.ReadToEnd(), Json)
            ?? throw new ValidationException("plugin.json is empty or invalid.");
    }

    private static PluginManifest ExtractAndReadManifest(byte[] package, string destination)
    {
        ZipArchive zip;
        try { zip = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read); }
        catch (InvalidDataException) { throw new ValidationException("The file is not a valid .zip package."); }

        using (zip)
        {
            if (zip.Entries.Count > MaxEntries) throw new ValidationException("The package has too many files.");
            if (zip.Entries.Sum(e => e.Length) > MaxUnpackedBytes) throw new ValidationException("The package is too large once unpacked.");

            var root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.EndsWith('/')) continue; // directory entry
                var target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
                if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw new ValidationException($"The package contains an unsafe path ('{entry.FullName}').");
                if (!AllowedExtensions.Contains(Path.GetExtension(entry.Name).ToLowerInvariant()))
                    throw new ValidationException($"The package contains a file type that is not allowed ('{entry.Name}').");

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, overwrite: false);
            }

            var raw = ExtractAndReadManifestJson(zip);
            if (!SafeId.IsMatch(raw.Id ?? ""))
                throw new ValidationException("plugin.json needs an id of letters, digits, dots, dashes or underscores (for example Acme.Invoicing).");
            if (string.IsNullOrWhiteSpace(raw.Assembly) || string.IsNullOrWhiteSpace(raw.ModuleType) || string.IsNullOrWhiteSpace(raw.Version))
                throw new ValidationException("plugin.json must declare version, assembly and moduleType.");
            if (!Version.TryParse(raw.Version, out _))
                throw new ValidationException($"'{raw.Version}' is not a valid version (use, for example, 1.2.0).");
            if (!File.Exists(Path.Combine(destination, "backend", raw.Assembly)))
                throw new ValidationException($"The package has no backend/{raw.Assembly}.");

            return PluginManager.ToManifest(raw);
        }
    }

    private static void CheckHostCompatibility(PluginManifest manifest)
    {
        var problem = PluginManager.HostCompatibilityProblem(manifest);
        if (problem is not null) throw new ValidationException(problem);
    }

    private static void CheckTrustPolicy(string id, string sha256, PluginHostOptions options)
    {
        if (!options.RequireAllowList) return;

        var approved = options.Allowed.Any(a =>
            a.Id.Equals(id, StringComparison.OrdinalIgnoreCase) && FixedTimeEqualsHex(a.Sha256, sha256));
        if (!approved)
            throw new ValidationException($"This package is not on the approved list (id {id}, SHA-256 {sha256}). Ask an administrator to approve it in Plugins:Trust.");
    }

    /// <summary>Loads the module type in a throwaway context, without running any plugin code, so a package whose module cannot load is
    /// rejected now rather than after the restart.</summary>
    private static void VerifyModuleLoads(string stagedFolder, PluginManifest manifest)
    {
        var raw = ReadManifestJson(Path.Combine(stagedFolder, "plugin.json"))!;
        var assemblyPath = Path.Combine(stagedFolder, "backend", raw.Assembly);

        var unloaded = LoadAndCheck(assemblyPath, raw.Assembly, manifest.ModuleType);

        // Let go of the throwaway assembly now (collectible contexts are freed by a collection), so it does not sit in the process
        // beside the real one and get picked up by anything that scans loaded assemblies.
        for (var i = 0; i < 3 && unloaded.TryGetTarget(out _); i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference<PluginLoadContext> LoadAndCheck(string assemblyPath, string assemblyFile, string moduleType)
    {
        var context = new PluginLoadContext(assemblyPath);
        var reference = new WeakReference<PluginLoadContext>(context);
        try
        {
            // From memory, not by path: a path load keeps the file locked on Windows, which would block moving the folder afterwards.
            using var image = new MemoryStream(File.ReadAllBytes(assemblyPath));
            var assembly = context.LoadFromStream(image);
            var type = assembly.GetType(moduleType)
                ?? throw new ValidationException($"Type '{moduleType}' was not found in {assemblyFile}.");
            if (!typeof(Modules.IFeatureModule).IsAssignableFrom(type))
                throw new ValidationException($"'{moduleType}' does not implement IFeatureModule.");
        }
        catch (ValidationException) { throw; }
        catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or FileNotFoundException or TypeLoadException)
        {
            throw new ValidationException($"The plugin assembly could not be loaded: {ex.Message}");
        }
        finally
        {
            context.Unload();
        }

        return reference;
    }

    private static PluginManifest? ReadInstalledManifest(string pluginsDirectory, string id) =>
        ReadManifestFile(Path.Combine(pluginsDirectory, id, "plugin.json"));

    private static PluginManifestJson? ReadManifestJson(string path)
    {
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<PluginManifestJson>(File.ReadAllText(path), Json); }
        catch (JsonException) { return null; }
    }

    private static PluginManifest? ReadManifestFile(string path)
    {
        var raw = ReadManifestJson(path);
        return raw is null ? null : PluginManager.ToManifest(raw);
    }

    private static StageMeta? ReadMeta(string folder)
    {
        var path = Path.Combine(folder, MetaFile);
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<StageMeta>(File.ReadAllText(path), Json); }
        catch (JsonException) { return null; }
    }

    private static bool IsNewer(string candidate, string installed) =>
        Version.TryParse(candidate, out var c) && Version.TryParse(installed, out var i) ? c > i : !string.Equals(candidate, installed, StringComparison.OrdinalIgnoreCase);

    private static bool FixedTimeEqualsHex(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.ASCII.GetBytes(a.Trim().ToLowerInvariant()), System.Text.Encoding.ASCII.GetBytes(b.Trim().ToLowerInvariant()));

    private static void EnsureSafeId(string id)
    {
        if (!SafeId.IsMatch(id)) throw new ValidationException("That is not a valid plugin id.");
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        foreach (var dir in Directory.GetDirectories(source)) CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }

    private static void TryDelete(string path)
    {
        try { Directory.Delete(path, recursive: true); }
        catch (IOException) { /* scratch space: a locked leftover is harmless */ }
        catch (UnauthorizedAccessException) { }
    }
}
