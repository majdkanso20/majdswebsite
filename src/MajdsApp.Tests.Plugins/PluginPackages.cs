using System.IO.Compression;
using System.Text.Json.Nodes;

namespace MajdsApp.Tests.Plugins;

/// <summary>Builds real plugin packages (.zip) from the sample plugin's build output, with the manifest changed as a test needs.</summary>
public static class PluginPackages
{
    public static string BuiltFolder()
    {
        var baseDir = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var configuration = baseDir.Parent!.Name; // Debug / Release
        return Path.GetFullPath(Path.Combine(baseDir.FullName, "..", "..", "..", "..",
            "MajdsApp.Plugins.Tasks", "bin", configuration, baseDir.Name));
    }

    /// <summary>A valid package of the sample plugin. <paramref name="manifest"/> edits plugin.json (id, version, host range, ...);
    /// <paramref name="extra"/> adds arbitrary entries (for example an unsafe path).</summary>
    public static byte[] Create(Action<JsonObject>? manifest = null, Action<ZipArchive>? extra = null, bool includeAssembly = true)
    {
        var built = BuiltFolder();
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(built, "plugin.json")))!.AsObject();
        manifest?.Invoke(json);

        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "plugin.json", System.Text.Encoding.UTF8.GetBytes(json.ToJsonString()));
            if (includeAssembly)
                foreach (var file in Directory.GetFiles(built, "MajdsApp.Plugins.Tasks.*"))
                    Add(zip, "backend/" + Path.GetFileName(file), File.ReadAllBytes(file));
            foreach (var file in Directory.Exists(Path.Combine(built, "localization")) ? Directory.GetFiles(Path.Combine(built, "localization")) : [])
                Add(zip, "localization/" + Path.GetFileName(file), File.ReadAllBytes(file));
            foreach (var file in Directory.Exists(Path.Combine(built, "frontend")) ? Directory.GetFiles(Path.Combine(built, "frontend"), "*", SearchOption.AllDirectories) : [])
                Add(zip, "frontend/" + Path.GetRelativePath(Path.Combine(built, "frontend"), file).Replace(Path.DirectorySeparatorChar, '/'), File.ReadAllBytes(file));
            extra?.Invoke(zip);
        }

        return stream.ToArray();
    }

    public static void Add(ZipArchive zip, string name, byte[] content)
    {
        using var entry = zip.CreateEntry(name).Open();
        entry.Write(content);
    }

    /// <summary>The sample plugin as an installed folder, exactly as the host expects to find it.</summary>
    public static void InstallFolder(string pluginsFolder, string version = "1.0.0")
    {
        var built = BuiltFolder();
        var target = Path.Combine(pluginsFolder, "MajdsApp.Plugins.Tasks");
        Directory.CreateDirectory(Path.Combine(target, "backend"));
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(built, "plugin.json")))!.AsObject();
        json["version"] = version;
        File.WriteAllText(Path.Combine(target, "plugin.json"), json.ToJsonString());
        foreach (var file in Directory.GetFiles(built, "MajdsApp.Plugins.Tasks.*"))
            File.Copy(file, Path.Combine(target, "backend", Path.GetFileName(file)), overwrite: true);
        var frontend = Path.Combine(built, "frontend");
        if (Directory.Exists(frontend))
            foreach (var file in Directory.GetFiles(frontend, "*", SearchOption.AllDirectories))
            {
                var to = Path.Combine(target, "frontend", Path.GetRelativePath(frontend, file));
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                File.Copy(file, to, overwrite: true);
            }
        if (Directory.Exists(Path.Combine(built, "localization")))
        {
            Directory.CreateDirectory(Path.Combine(target, "localization"));
            foreach (var file in Directory.GetFiles(Path.Combine(built, "localization")))
                File.Copy(file, Path.Combine(target, "localization", Path.GetFileName(file)), overwrite: true);
        }
    }
}
