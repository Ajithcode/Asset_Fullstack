using System.Security.Cryptography;
using System.Text;
using A360.Security;
using QRCoder;

namespace A360.UserAccount.Api.Security;

/// <summary>
/// Authenticator-app codes (TOTP, RFC 6238: HMAC-SHA1, 6 digits, 30-second steps), as used by
/// Google Authenticator, Microsoft Authenticator and similar apps.
/// Secrets are stored encrypted (AES-GCM). The key is TwoFactor:EncryptionKey (base64, 32 bytes) when set,
/// otherwise derived from Jwt:SigningKey, so changing either key makes existing users set up their app again.
/// </summary>
public sealed class TotpService
{
    public const int CodeDigits = 6;
    public const int StepSeconds = 30;
    /// <summary>Codes from one step before or after are accepted, to allow for phone clock drift.</summary>
    private const int AllowedDriftSteps = 1;
    private const int SecretBytes = 20;
    private const string ProtectedPrefix = "v1:";
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    private readonly byte[] _encryptionKey;

    public TotpService(IConfiguration configuration, JwtSettings jwtSettings)
    {
        Issuer = configuration["TwoFactor:Issuer"] is { Length: > 0 } issuer ? issuer : "Asset360";
        var configuredKey = configuration["TwoFactor:EncryptionKey"];
        _encryptionKey = string.IsNullOrWhiteSpace(configuredKey)
            ? HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(jwtSettings.SigningKey), 32, info: Encoding.UTF8.GetBytes("A360.UserAccount.TotpSecret"))
            : Convert.FromBase64String(configuredKey);

        if (_encryptionKey.Length != 32)
        {
            throw new InvalidOperationException("TwoFactor:EncryptionKey must be 32 bytes, base64 encoded.");
        }
    }

    /// <summary>Name shown for the account in the authenticator app.</summary>
    public string Issuer { get; }

    public static long CurrentStep => DateTimeOffset.UtcNow.ToUnixTimeSeconds() / StepSeconds;

    /// <summary>A new random secret, base32 encoded (the form authenticator apps expect).</summary>
    public string GenerateSecret() => ToBase32(RandomNumberGenerator.GetBytes(SecretBytes));

    /// <summary>
    /// Checks a code against the secret. Returns the matched time step, or null when the code is wrong or its
    /// step is not newer than <paramref name="lastUsedStep"/> (already used).
    /// </summary>
    public long? Verify(string base32Secret, string code, long lastUsedStep)
    {
        code = code.Replace(" ", string.Empty).Trim();
        if (code.Length != CodeDigits || !code.All(char.IsAsciiDigit))
        {
            return null;
        }

        var key = FromBase32(base32Secret);
        var current = CurrentStep;
        for (var step = current - AllowedDriftSteps; step <= current + AllowedDriftSteps; step++)
        {
            if (step > lastUsedStep
                && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(ComputeCode(key, step)), Encoding.ASCII.GetBytes(code)))
            {
                return step;
            }
        }

        return null;
    }

    /// <summary>otpauth:// link that the QR code carries.</summary>
    public string BuildOtpAuthUri(string accountName, string base32Secret)
    {
        var label = Uri.EscapeDataString($"{Issuer}:{accountName}");
        return $"otpauth://totp/{label}?secret={base32Secret}&issuer={Uri.EscapeDataString(Issuer)}&algorithm=SHA1&digits={CodeDigits}&period={StepSeconds}";
    }

    /// <summary>QR code for the otpauth link, as a PNG data URI the page can show directly.</summary>
    public static string BuildQrCodeDataUri(string otpAuthUri)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(otpAuthUri, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(8);
        return "data:image/png;base64," + Convert.ToBase64String(png);
    }

    /// <summary>The secret in groups of four, for typing into the app when the QR code cannot be scanned.</summary>
    public static string FormatForManualEntry(string base32Secret) =>
        string.Join(' ', base32Secret.Chunk(4).Select(chunk => new string(chunk)));

    public string Protect(string base32Secret)
    {
        var plain = Encoding.UTF8.GetBytes(base32Secret);
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        var cipher = new byte[plain.Length];
        using (var aes = new AesGcm(_encryptionKey, tag.Length))
        {
            aes.Encrypt(nonce, plain, cipher, tag);
        }

        return ProtectedPrefix + Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    /// <summary>The base32 secret, or null when the value is empty or was encrypted with another key.</summary>
    public string? Unprotect(string protectedSecret)
    {
        if (string.IsNullOrWhiteSpace(protectedSecret) || !protectedSecret.StartsWith(ProtectedPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var bytes = Convert.FromBase64String(protectedSecret[ProtectedPrefix.Length..]);
            var nonceSize = AesGcm.NonceByteSizes.MaxSize;
            var tagSize = AesGcm.TagByteSizes.MaxSize;
            var cipher = bytes.AsSpan(nonceSize + tagSize);
            var plain = new byte[cipher.Length];
            using var aes = new AesGcm(_encryptionKey, tagSize);
            aes.Decrypt(bytes.AsSpan(0, nonceSize), cipher, bytes.AsSpan(nonceSize, tagSize), plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException or ArgumentException)
        {
            return null;
        }
    }

    private static string ComputeCode(byte[] key, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(counter, step);
        Span<byte> hash = stackalloc byte[20];
        HMACSHA1.HashData(key, counter, hash);

        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6");
    }

    private static string ToBase32(byte[] data)
    {
        var builder = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                builder.Append(Base32Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0)
        {
            builder.Append(Base32Alphabet[(buffer << (5 - bits)) & 31]);
        }

        return builder.ToString();
    }

    private static byte[] FromBase32(string base32)
    {
        var output = new List<byte>(base32.Length * 5 / 8);
        int buffer = 0, bits = 0;
        foreach (var c in base32.TrimEnd('=').ToUpperInvariant())
        {
            var value = Base32Alphabet.IndexOf(c);
            if (value < 0)
            {
                throw new FormatException("Invalid base32 secret.");
            }

            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }

        return [.. output];
    }
}
