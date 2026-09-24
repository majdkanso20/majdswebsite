using Microsoft.AspNetCore.DataProtection;

namespace MajdsApp.Modules.Settings;

/// <summary>
/// Encrypts sensitive setting values at rest (F-Settings FR-SET-006) with ASP.NET Core Data Protection.
/// The stored form is <c>enc:</c> + the protected payload, so a plain value can never be mistaken for
/// a secret. If the key ring is lost the payload can't be read; that reads back as "not set" rather than
/// failing the whole settings load (the admin simply re-enters the secret).
/// </summary>
public static class SettingSecrets
{
    private const string Prefix = "enc:";
    private const string Purpose = "MajdsApp.Settings.v1";

    public static string Protect(IDataProtectionProvider provider, string plain) =>
        Prefix + provider.CreateProtector(Purpose).Protect(plain);

    public static string Unprotect(IDataProtectionProvider provider, string stored)
    {
        if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
            return string.Empty;

        try
        {
            return provider.CreateProtector(Purpose).Unprotect(stored[Prefix.Length..]);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return string.Empty;
        }
    }
}
