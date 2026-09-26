namespace MajdsApp.SharedKernel.Plugins;

/// <summary>
/// The pre-built frontend files a plugin ships in its <c>frontend/</c> folder (P5 FR-PLUG-014/016): a compiled bundle, templates, styles, images and
/// fonts. Only these kinds are accepted in a package and served, so a package cannot smuggle in anything else under that folder.
/// </summary>
public static class PluginAssets
{
    public const string Folder = "frontend";

    public static readonly IReadOnlyDictionary<string, string> ContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".js"] = "text/javascript",
        [".mjs"] = "text/javascript",
        [".css"] = "text/css",
        [".html"] = "text/html",
        [".json"] = "application/json",
        [".map"] = "application/json",
        [".svg"] = "image/svg+xml",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".ico"] = "image/x-icon",
        [".woff"] = "font/woff",
        [".woff2"] = "font/woff2",
        [".txt"] = "text/plain"
    };

    /// <summary>The full path of a requested file inside the plugin's frontend folder, or null when it is not there, is outside the folder,
    /// or is not an allowed kind of file.</summary>
    public static string? Resolve(LoadedPlugin plugin, string? relativePath)
    {
        if (!plugin.Succeeded || plugin.Folder is null || string.IsNullOrWhiteSpace(relativePath)) return null;
        if (!ContentTypes.ContainsKey(Path.GetExtension(relativePath))) return null;

        var root = Path.GetFullPath(Path.Combine(plugin.Folder, Folder)) + Path.DirectorySeparatorChar;
        string full;
        try { full = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar))); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return null; }

        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(full) ? full : null;
    }
}
