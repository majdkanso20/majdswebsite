namespace MajdsApp.SharedKernel.Plugins;

/// <summary>One approved plugin package: its id and the SHA-256 of the exact package file (P5 FR-PLUG-036).</summary>
public record AllowedPlugin(string Id, string Sha256);

/// <summary>Where plugins live and the trust policy applied before a package can be installed (P5 FR-PLUG-036).
/// With <see cref="RequireAllowList"/> on, only a package whose id and SHA-256 are listed can be installed. With <see cref="RequireSignature"/> on, only a package signed by a key in <see cref="Signers"/> can be.</summary>
public record PluginHostOptions(
    string Directory, bool RequireAllowList, IReadOnlyList<AllowedPlugin> Allowed,
    bool RequireSignature = false, IReadOnlyDictionary<string, string>? Signers = null)
{
    /// <summary>The publishers whose signatures are trusted: key id to base64 public key (<c>Plugins:Trust:Signers:&lt;keyId&gt;</c>).</summary>
    public IReadOnlyDictionary<string, string> TrustedSigners => Signers ?? new Dictionary<string, string>();

    /// <summary>Reads <c>Plugins:Trust:RequireAllowList</c> and the <c>Plugins:Trust:Allowed</c> list (each <c>Id</c> and <c>Sha256</c>).</summary>
    public static PluginHostOptions FromConfiguration(Microsoft.Extensions.Configuration.IConfiguration configuration, string directory) => new(
        directory,
        bool.TryParse(configuration["Plugins:Trust:RequireAllowList"], out var require) && require,
        configuration.GetSection("Plugins:Trust:Allowed").GetChildren()
            .Select(c => new AllowedPlugin(c["Id"] ?? "", c["Sha256"] ?? "")).ToList(),
        bool.TryParse(configuration["Plugins:Trust:RequireSignature"], out var signed) && signed,
        configuration.GetSection("Plugins:Trust:Signers").GetChildren().Where(c => !string.IsNullOrWhiteSpace(c.Value))
            .ToDictionary(c => c.Key, c => c.Value!, StringComparer.Ordinal));
}

/// <summary>The platform version a plugin's <c>minHostVersion</c> / <c>maxHostVersion</c> are compared with (P5 FR-PLUG-009).</summary>
public static class PluginHost
{
    /// <summary>The version of the plugin UI contract the shell speaks (see the frontend's <c>HOST_UI_CONTRACT</c>); a plugin bundle built for another one is not shown (FR-PLUG-019).</summary>
    public const int UiContract = 1;

    public static Version Version { get; } = typeof(PluginHost).Assembly.GetName().Version ?? new Version(1, 0, 0, 0);
}
