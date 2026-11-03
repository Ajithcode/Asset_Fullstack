using System.Security.Cryptography;
using System.Text;

namespace A360.Administration.Api.Security;

public interface ISecretProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedValue);
}

/// <summary>
/// Encrypts integration secrets at rest with AES-256-GCM. Output format: "v1:" + base64(nonce | tag | ciphertext).
/// </summary>
public sealed class AesGcmSecretProtector : ISecretProtector
{
    public const string ConfigurationKey = "Integrations:SecretEncryptionKey";

    private const string Version = "v1:";
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public AesGcmSecretProtector(IConfiguration configuration)
    {
        var configuredKey = configuration[ConfigurationKey];
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            throw new InvalidOperationException($"{ConfigurationKey} must be configured.");
        }

        try
        {
            _key = Convert.FromBase64String(configuredKey);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException($"{ConfigurationKey} must be base64.", exception);
        }

        if (_key.Length != KeySize)
        {
            throw new InvalidOperationException($"{ConfigurationKey} must decode to {KeySize} bytes.");
        }
    }

    public string Protect(string plaintext)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var payload = new byte[NonceSize + TagSize + plaintextBytes.Length];
        var nonce = payload.AsSpan(0, NonceSize);
        var tag = payload.AsSpan(NonceSize, TagSize);
        var ciphertext = payload.AsSpan(NonceSize + TagSize);

        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        return Version + Convert.ToBase64String(payload);
    }

    public string Unprotect(string protectedValue)
    {
        if (!protectedValue.StartsWith(Version, StringComparison.Ordinal))
        {
            throw new CryptographicException("Unsupported secret format.");
        }

        var payload = Convert.FromBase64String(protectedValue[Version.Length..]);
        if (payload.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("Secret payload is too short.");
        }

        var plaintextBytes = new byte[payload.Length - NonceSize - TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(
            payload.AsSpan(0, NonceSize),
            payload.AsSpan(NonceSize + TagSize),
            payload.AsSpan(NonceSize, TagSize),
            plaintextBytes);

        return Encoding.UTF8.GetString(plaintextBytes);
    }
}
