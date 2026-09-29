using System.ComponentModel.DataAnnotations;
using MajdsApp.SharedKernel.Configuration;
using MajdsApp.SharedKernel.Files;
using MajdsApp.SharedKernel.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MajdsApp.Modules.Files;

/// <summary>The <c>Files:Storage</c> section: which kind of store keeps the bytes. <c>Disk</c> is built in; a module adds others (an Azure Blob or S3 provider).</summary>
public class FileStorageOptions
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Files:Storage:Provider must not be empty.")]
    public string Provider { get; set; } = "Disk";
}

/// <summary>Refuses to start when <c>Files:Storage:Provider</c> names a provider nobody registered, instead of failing on the first upload.</summary>
public class FileStorageProviderExists(IEnumerable<IFileStorageProvider> providers) : IValidateOptions<FileStorageOptions>
{
    public ValidateOptionsResult Validate(string? name, FileStorageOptions options) =>
        providers.Any(p => p.Name.Equals(options.Provider, StringComparison.OrdinalIgnoreCase))
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"Files:Storage:Provider '{options.Provider}' is not available. Choose one of: {string.Join(", ", providers.Select(p => p.Name))}.");
}

public class DiskFileStorageProvider : IFileStorageProvider
{
    public string Name => "Disk";
    public IFileStorage Create(IServiceProvider services) => ActivatorUtilities.CreateInstance<FileStorage>(services);
}

public class FilesModule : IFeatureModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleOptions<FileStorageOptions>(configuration, "Files:Storage");
        services.AddSingleton<IValidateOptions<FileStorageOptions>, FileStorageProviderExists>();
        services.AddSingleton<IFileStorageProvider, DiskFileStorageProvider>();
        services.AddScoped<FileStorage>();

        // A second built-in provider (an Azure Blob container), proving the abstraction really does let a store be
        // added with no change to anything that already talks to IFileStorage.
        services.AddModuleOptions<AzureBlobStorageOptions>(configuration, "Files:Storage:AzureBlob");
        services.AddSingleton<IValidateOptions<AzureBlobStorageOptions>, AzureBlobStorageOptionsValidator>();
        services.AddSingleton<IFileStorageProvider, AzureBlobFileStorageProvider>();
        services.AddScoped<AzureBlobFileStorage>();

        // Everything that keeps a file asks for IFileStorage; the configured provider decides where the bytes go.
        services.AddScoped<IFileStorage>(sp =>
        {
            var name = sp.GetRequiredService<IOptions<FileStorageOptions>>().Value.Provider;
            return sp.GetServices<IFileStorageProvider>().First(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Create(sp);
        });
    }
}
