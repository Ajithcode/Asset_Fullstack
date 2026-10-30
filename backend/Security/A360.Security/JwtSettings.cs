using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace A360.Security;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";
    private const int MinimumSigningKeyBytes = 32;

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string SigningKey { get; init; } = string.Empty;

    public int AccessTokenMinutes { get; init; } = 30;

    public int RefreshTokenDays { get; init; } = 7;

    public static JwtSettings FromConfiguration(IConfiguration configuration)
    {
        var settings = new JwtSettings
        {
            Issuer = configuration[$"{SectionName}:Issuer"] ?? string.Empty,
            Audience = configuration[$"{SectionName}:Audience"] ?? string.Empty,
            SigningKey = configuration[$"{SectionName}:SigningKey"] ?? string.Empty,
            AccessTokenMinutes = int.TryParse(configuration[$"{SectionName}:AccessTokenMinutes"], out var accessMinutes) ? accessMinutes : 30,
            RefreshTokenDays = int.TryParse(configuration[$"{SectionName}:RefreshTokenDays"], out var refreshDays) ? refreshDays : 7
        };

        settings.Validate();
        return settings;
    }

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer))
        {
            throw new InvalidOperationException("JWT issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(Audience))
        {
            throw new InvalidOperationException("JWT audience is required.");
        }

        if (Encoding.UTF8.GetByteCount(SigningKey) < MinimumSigningKeyBytes)
        {
            throw new InvalidOperationException($"JWT signing key must be at least {MinimumSigningKeyBytes} bytes.");
        }

        if (AccessTokenMinutes <= 0)
        {
            throw new InvalidOperationException("JWT access token lifetime must be greater than zero.");
        }

        if (RefreshTokenDays <= 0)
        {
            throw new InvalidOperationException("JWT refresh token lifetime must be greater than zero.");
        }
    }
}
