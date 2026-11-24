using System.Security.Cryptography;

namespace A360.UserAccount.Api.Security;

/// <summary>Same rule as User Management (Administration): at least 8 characters with letters and numbers.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    // No look-alikes (0/O, 1/l/I) so a temporary password can be read out or typed without mistakes
    private const string Letters = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Digits = "23456789";

    /// <summary>What is wrong with the password, or null when it is acceptable.</summary>
    public static string? Problem(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            return $"Password must be at least {MinLength} characters and contain letters and numbers.";
        }

        return password.Length > MaxLength ? $"Password must be at most {MaxLength} characters." : null;
    }

    /// <summary>A random 12-character password with letters and digits.</summary>
    public static string GenerateTemporary()
    {
        const int length = 12;
        var all = Letters + Digits;
        var chars = new char[length];
        chars[0] = Letters[RandomNumberGenerator.GetInt32(Letters.Length)];
        chars[1] = Digits[RandomNumberGenerator.GetInt32(Digits.Length)];
        for (var i = 2; i < length; i++)
        {
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
    }
}
