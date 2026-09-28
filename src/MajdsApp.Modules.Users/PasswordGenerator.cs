using System.Security.Cryptography;

namespace MajdsApp.Modules.Users;

/// <summary>
/// A password nobody is meant to type: used when an account is created without one (an import row with
/// no password column, or an admin-created user who will set their own via an emailed link, FR-USER-002).
/// Random and long enough to satisfy any password policy; the account owner never sees it and always
/// reaches their real password through Forgot password or the set-password link instead.
/// </summary>
internal static class PasswordGenerator
{
    private const string Lower = "abcdefghijkmnopqrstuvwxyz", Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ", Digits = "23456789", Symbols = "!@#$%^&*";

    public static string Generate()
    {
        const string all = Lower + Upper + Digits + Symbols;
        var chars = new List<char>
        {
            Lower[RandomNumberGenerator.GetInt32(Lower.Length)], Upper[RandomNumberGenerator.GetInt32(Upper.Length)],
            Digits[RandomNumberGenerator.GetInt32(Digits.Length)], Symbols[RandomNumberGenerator.GetInt32(Symbols.Length)]
        };
        while (chars.Count < 24) chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);
        return new string(chars.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToArray());
    }
}
