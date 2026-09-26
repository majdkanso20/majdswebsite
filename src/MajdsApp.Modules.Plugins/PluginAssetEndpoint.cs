using MajdsApp.SharedKernel.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace MajdsApp.Modules.Plugins;

/// <summary>
/// Serves a plugin's pre-built frontend files at <c>/plugins/{pluginId}/{path}</c> (P5 FR-PLUG-016), from the <c>frontend/</c> folder of its package.
/// Only an enabled, loaded plugin is served (a disabled one is a 404, the same as an unknown one, so its files disappear with it), only inside that folder,
/// and only the kinds of file in <see cref="PluginAssets"/>. The files are public code, not data, so no sign-in is needed to fetch a script tag's source;
/// the browser revalidates with the ETag, so an upgraded plugin is picked up at once. The host's own strict Content-Security-Policy (nothing may run or load) is on every response, so a page or image opened directly cannot act as the application.
/// </summary>
public static class PluginAssetEndpoint
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/plugins/{pluginId}/{**path}", (string pluginId, string? path, HttpContext context,
                IReadOnlyList<LoadedPlugin> loaded, IPluginStateCache state) =>
            {
                var plugin = loaded.FirstOrDefault(p => p.Succeeded && p.Manifest.Id.Equals(pluginId, StringComparison.OrdinalIgnoreCase));
                if (plugin?.Assembly is null || !state.IsEnabled(plugin.Assembly.GetName().Name ?? plugin.Manifest.Id))
                    return Results.NotFound();

                var file = PluginAssets.Resolve(plugin, path);
                if (file is null) return Results.NotFound();

                var info = new FileInfo(file);
                var etag = new EntityTagHeaderValue($"\"{info.LastWriteTimeUtc.Ticks:x}-{info.Length:x}\"");
                var extension = Path.GetExtension(file);

                context.Response.Headers.CacheControl = "no-cache"; // store it, but ask before reusing it

                return Results.File(file, PluginAssets.ContentTypes[extension], lastModified: info.LastWriteTimeUtc, entityTag: etag);
            })
            .AllowAnonymous()
            .ExcludeFromDescription();
    }
}
