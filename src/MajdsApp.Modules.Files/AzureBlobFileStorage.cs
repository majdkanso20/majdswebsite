using Azure.Storage.Blobs;
using MajdsApp.SharedKernel.Files;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MajdsApp.Modules.Files;

/// <summary>The <c>Files:Storage:AzureBlob</c> section: only checked (<see cref="AzureBlobStorageOptionsValidator"/>) when
/// <c>Files:Storage:Provider</c> is actually <c>AzureBlob</c>, the same way <c>Cache:Redis:ConnectionString</c> is only
/// required when <c>Cache:Provider</c> is <c>Redis</c> (F-Files FR-FILE-001).</summary>
public class AzureBlobStorageOptions
{
    /// <summary>A full Azure Storage account connection string.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Created automatically on first use if it does not already exist.</summary>
    public string ContainerName { get; set; } = "files";
}

public class AzureBlobStorageOptionsValidator(IConfiguration configuration) : IValidateOptions<AzureBlobStorageOptions>
{
    public ValidateOptionsResult Validate(string? name, AzureBlobStorageOptions options)
    {
        // Reads the raw setting rather than IOptions<FileStorageOptions>.Value: that one has its own validator (is the
        // name even known?), and re-triggering it here would surface two exceptions when the provider name is bad.
        var provider = configuration["Files:Storage:Provider"];
        if (!AzureBlobFileStorageProvider.ProviderName.Equals(provider, StringComparison.OrdinalIgnoreCase))
            return ValidateOptionsResult.Success; // not selected: nothing to check

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            return ValidateOptionsResult.Fail($"Files:Storage:Provider is {AzureBlobFileStorageProvider.ProviderName}, but Files:Storage:AzureBlob:ConnectionString is not set.");
        if (string.IsNullOrWhiteSpace(options.ContainerName))
            return ValidateOptionsResult.Fail("Files:Storage:AzureBlob:ContainerName must not be empty.");

        return ValidateOptionsResult.Success;
    }
}

/// <summary>
/// An Azure Blob Storage-backed <see cref="IFileStorage"/> (F-Files FR-FILE-001): every file the platform keeps
/// (uploads, exports, profile pictures) is a blob in one container, named the same opaque, server-generated
/// name the Disk provider uses. Chosen with <c>Files:Storage:Provider=AzureBlob</c>; wired and validated the
/// same way Redis is for caching, but — like Redis — not run against a live Azure account here.
/// </summary>
public class AzureBlobFileStorage(IOptions<AzureBlobStorageOptions> options) : IFileStorage
{
    private BlobContainerClient? _container;

    private BlobContainerClient Container => _container ??= new BlobContainerClient(options.Value.ConnectionString, options.Value.ContainerName);

    public async Task SaveAsync(string storedName, Stream content, CancellationToken ct)
    {
        await Container.CreateIfNotExistsAsync(cancellationToken: ct);
        await Container.UploadBlobAsync(Validated(storedName), content, ct);
    }

    public Stream Open(string storedName) => Container.GetBlobClient(Validated(storedName)).OpenRead();

    public void Delete(string storedName) => Container.GetBlobClient(Validated(storedName)).DeleteIfExists();

    public IEnumerable<(string Name, DateTime LastWriteUtc)> Enumerate() =>
        Container.Exists()
            ? Container.GetBlobs().Select(b => (b.Name, (b.Properties.LastModified ?? DateTimeOffset.UtcNow).UtcDateTime)).ToList()
            : [];

    // storedName is always a server-generated GUID, but reject anything path-like regardless (same guard as the Disk provider).
    private static string Validated(string storedName) =>
        storedName.IndexOfAny(['/', '\\', ':']) >= 0 || storedName.Contains("..")
            ? throw new ArgumentException("Invalid stored file name.")
            : storedName;
}

public class AzureBlobFileStorageProvider : IFileStorageProvider
{
    public const string ProviderName = "AzureBlob";
    public string Name => ProviderName;
    public IFileStorage Create(IServiceProvider services) => ActivatorUtilities.CreateInstance<AzureBlobFileStorage>(services);
}
