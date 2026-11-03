using System.Security.Cryptography;
using System.Text;

namespace A360.UserAccount.Api.Security;

public sealed class OneTimeCodeService
{
    public const int CodeLength = 6;
    public const int MaxAttempts = 5;
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);

    public string GenerateCode()
    {
        return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    }

    public string Hash(string code)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim())));
    }

    public bool Matches(string code, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Hash(code)),
            Encoding.UTF8.GetBytes(storedHash));
    }
}
