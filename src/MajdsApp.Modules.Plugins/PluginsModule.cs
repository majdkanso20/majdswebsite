using System.ComponentModel.DataAnnotations;
using MajdsApp.SharedKernel.Configuration;
using MajdsApp.SharedKernel.Modules;
using MajdsApp.SharedKernel.Plugins;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MajdsApp.Modules.Plugins;

/// <summary>The <c>Plugins:Trust</c> section (P2 FR-MOD-004, P5 FR-PLUG-036), checked at start: a malformed checksum or key would otherwise only show up when someone tried to install.</summary>
public class PluginTrustOptions : IValidatableObject
{
    public bool RequireAllowList { get; set; }
    public bool RequireSignature { get; set; }
    public List<AllowedPluginOptions> Allowed { get; set; } = [];
    public Dictionary<string, string> Signers { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        foreach (var (allowed, index) in Allowed.Select((a, i) => (a, i)))
        {
            if (string.IsNullOrWhiteSpace(allowed.Id))
                yield return new ValidationResult($"Plugins:Trust:Allowed:{index}:Id must not be empty.");
            if (allowed.Sha256 is null || allowed.Sha256.Length != 64 || !allowed.Sha256.All(Uri.IsHexDigit))
                yield return new ValidationResult($"Plugins:Trust:Allowed:{index}:Sha256 must be 64 hexadecimal characters (the SHA-256 of the package file).");
        }

        foreach (var (keyId, key) in Signers)
        {
            var valid = true;
            try
            {
                using var ecdsa = System.Security.Cryptography.ECDsa.Create();
                ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(key), out _);
            }
            catch (Exception ex) when (ex is FormatException or System.Security.Cryptography.CryptographicException) { valid = false; }

            if (!valid) yield return new ValidationResult($"Plugins:Trust:Signers:{keyId} is not a base64 ECDSA public key (SubjectPublicKeyInfo).");
        }
    }
}

public class AllowedPluginOptions
{
    public string Id { get; set; } = "";
    public string? Sha256 { get; set; }
}

public class PluginsModule : IFeatureModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleOptions<PluginTrustOptions>(configuration, "Plugins:Trust");
        // Registered after the SharedKernel's TryAddSingleton default (AllowAllPluginStateCache) runs,
        // so this wins on resolution — same override idiom as every other cross-cutting default.
        services.AddSingleton<PluginStateCache>();
        services.AddSingleton<IPluginStateCache>(sp => sp.GetRequiredService<PluginStateCache>());
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => PluginAssetEndpoint.Map(endpoints);
}
