using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;

namespace MajdsApp.SharedKernel.Plugins;

/// <summary>
/// Digital signatures for plugin packages (P5 FR-PLUG-036). A publisher signs the <i>contents</i> of a package with an ECDSA P-256 private key and
/// adds the result as <c>plugin.sig</c>; the platform verifies it against the public keys an administrator has trusted
/// (<c>Plugins:Trust:Signers:&lt;keyId&gt;</c> = base64 of the key's SubjectPublicKeyInfo). A package with a signature that does not verify is always refused,
/// so a tampered file can never pass as signed; <c>Plugins:Trust:RequireSignature</c> additionally refuses packages with no signature at all.
/// What is signed is a digest of every file except the signature, by name and content, so adding, removing, renaming or changing any file breaks it.
/// </summary>
public static class PluginSignature
{
    public const string FileName = "plugin.sig";

    private record SignatureFile(string KeyId, string Algorithm, string Signature);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The bytes that are signed: each entry's name and SHA-256, in name order, excluding the signature file itself.</summary>
    public static byte[] Digest(ZipArchive zip)
    {
        var builder = new StringBuilder();
        foreach (var entry in zip.Entries.Where(e => !e.FullName.EndsWith('/') && !e.FullName.Equals(FileName, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(e => e.FullName, StringComparer.Ordinal))
        {
            using var stream = entry.Open();
            builder.Append(entry.FullName).Append('\n').Append(Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant()).Append('\n');
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    /// <summary>Signs a package and returns it with <c>plugin.sig</c> added. For publishers' build scripts and tests.</summary>
    public static byte[] Sign(byte[] package, ECDsa privateKey, string keyId)
    {
        using var input = new MemoryStream(package);
        using var output = new MemoryStream();
        using (var source = new ZipArchive(input, ZipArchiveMode.Read))
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in source.Entries.Where(e => !e.FullName.Equals(FileName, StringComparison.OrdinalIgnoreCase)))
            {
                var copy = target.CreateEntry(entry.FullName);
                using var from = entry.Open();
                using var to = copy.Open();
                from.CopyTo(to);
            }

            var signature = privateKey.SignData(Digest(source), HashAlgorithmName.SHA256);
            using var sig = target.CreateEntry(FileName).Open();
            sig.Write(JsonSerializer.SerializeToUtf8Bytes(new SignatureFile(keyId, "ECDSA-P256-SHA256", Convert.ToBase64String(signature)), Json));
        }

        return output.ToArray();
    }

    /// <summary>
    /// Null when the package carries no signature. Otherwise the id of the trusted key that signed it; a signature that names an untrusted key, is malformed,
    /// or does not match the contents throws a <see cref="ValidationException"/>.
    /// </summary>
    public static string? Verify(ZipArchive zip, IReadOnlyDictionary<string, string> trustedSigners)
    {
        var entry = zip.Entries.FirstOrDefault(e => e.FullName.Equals(FileName, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return null;

        SignatureFile? file;
        try
        {
            using var reader = new StreamReader(entry.Open());
            file = JsonSerializer.Deserialize<SignatureFile>(reader.ReadToEnd(), Json);
        }
        catch (JsonException) { file = null; }

        if (file is null || string.IsNullOrWhiteSpace(file.KeyId) || file.Algorithm != "ECDSA-P256-SHA256")
            throw new ValidationException("The package's signature file is not valid.");

        if (!trustedSigners.TryGetValue(file.KeyId, out var publicKey))
            throw new ValidationException($"The package is signed by '{file.KeyId}', which is not a trusted publisher.");

        bool valid;
        try
        {
            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
            valid = key.VerifyData(Digest(zip), Convert.FromBase64String(file.Signature), HashAlgorithmName.SHA256);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            valid = false;
        }

        if (!valid) throw new ValidationException($"The package's signature by '{file.KeyId}' does not match its contents. It was changed after it was signed, or is not from that publisher.");
        return file.KeyId;
    }
}
