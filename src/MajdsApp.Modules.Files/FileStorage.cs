using MajdsApp.SharedKernel.Files;
using Microsoft.AspNetCore.Hosting;

namespace MajdsApp.Modules.Files;

/// <summary>The local-disk file store (the built-in <c>Disk</c> provider): disk storage for file bytes, under App_Data/files in the API's content root (outside any
/// served static path, so files are only reachable through the authorized download endpoint).</summary>
public class FileStorage(IWebHostEnvironment env) : IFileStorage
{
    private string Root => Path.Combine(env.ContentRootPath, "App_Data", "files");

    public async Task SaveAsync(string storedName, Stream content, CancellationToken ct)
    {
        Directory.CreateDirectory(Root);
        await using var target = File.Create(PathFor(storedName));
        await content.CopyToAsync(target, ct);
    }

    public Stream Open(string storedName) => File.OpenRead(PathFor(storedName));

    /// <summary>Every stored file with its last write time, for the orphan sweep.</summary>
    public IEnumerable<(string Name, DateTime LastWriteUtc)> Enumerate() =>
        Directory.Exists(Root)
            ? new DirectoryInfo(Root).EnumerateFiles().Select(f => (f.Name, f.LastWriteTimeUtc)).ToList()
            : [];

    public void Delete(string storedName)
    {
        var path = PathFor(storedName);
        if (File.Exists(path))
            File.Delete(path);
    }

    // storedName is always a server-generated GUID, but reject anything path-like regardless.
    private string PathFor(string storedName) =>
        storedName.IndexOfAny(['/', '\\', ':']) >= 0 || storedName.Contains("..")
            ? throw new ArgumentException("Invalid stored file name.")
            : Path.Combine(Root, storedName);
}
