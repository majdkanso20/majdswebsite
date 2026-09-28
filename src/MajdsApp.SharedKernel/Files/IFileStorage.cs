namespace MajdsApp.SharedKernel.Files;

/// <summary>
/// Where the bytes of stored files live (F-Files FR-FILE-001). Everything that keeps a file (uploads, exports, profile pictures) talks to this, never to a disk path, so
/// the store can be the local disk today and a cloud blob store (Azure Blob, S3) tomorrow by adding a provider and changing one setting. Names are server-generated
/// and opaque; a file's client-supplied name is kept in its record, never used here.
/// </summary>
public interface IFileStorage
{
    /// <summary>Streams <paramref name="content"/> into the store under <paramref name="storedName"/> without holding all of it in memory.</summary>
    Task SaveAsync(string storedName, Stream content, CancellationToken ct);

    /// <summary>A forward stream over the stored bytes, for streaming a download of any size. The caller disposes it.</summary>
    Stream Open(string storedName);

    /// <summary>Removes the bytes. Removing something that is not there is not an error.</summary>
    void Delete(string storedName);

    /// <summary>Every stored file with its last write time (UTC), for the sweep that removes files no record points to.</summary>
    IEnumerable<(string Name, DateTime LastWriteUtc)> Enumerate();
}

/// <summary>
/// A kind of file store that can be chosen with <c>Files:Storage:Provider</c>. The local disk (<c>Disk</c>) is built in; a module adds another by registering one of these.
/// </summary>
public interface IFileStorageProvider
{
    /// <summary>The name used in configuration, for example <c>Disk</c> or <c>AzureBlob</c>.</summary>
    string Name { get; }

    IFileStorage Create(IServiceProvider services);
}
