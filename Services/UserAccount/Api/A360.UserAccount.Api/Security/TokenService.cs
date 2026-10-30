using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using A360.Security;
using Microsoft.IdentityModel.Tokens;
using UserEntity = A360.UserAccount.Domain.Entities.User;

namespace A360.UserAccount.Api.Security;

public sealed record IssuedAccessToken(string Token, DateTime ExpiresAt);

public sealed record IssuedRefreshToken(string Token, string TokenHash, DateTime ExpiresAt);

public sealed class TokenService
{
    private readonly JwtSettings _settings;
    private readonly SigningCredentials _signingCredentials;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public TokenService(JwtSettings settings)
    {
        _settings = settings;
        _signingCredentials = new SigningCredentials(settings.CreateSigningKey(), SecurityAlgorithms.HmacSha256);
    }

    public IssuedAccessToken CreateAccessToken(UserEntity user)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(_settings.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(A360ClaimTypes.Subject, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(A360ClaimTypes.UserId, user.UserId),
            new(A360ClaimTypes.UserName, user.UserName),
            new(A360ClaimTypes.Email, user.Email),
            new(A360ClaimTypes.RoleId, user.UserRoleId)
        };

        AddIfPresent(claims, A360ClaimTypes.Role, user.RoleName);
        AddIfPresent(claims, A360ClaimTypes.ClientId, user.ClientId);
        AddIfPresent(claims, A360ClaimTypes.TenantId, user.TenantId);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: _signingCredentials);

        return new IssuedAccessToken(_tokenHandler.WriteToken(token), expiresAt);
    }

    public IssuedRefreshToken CreateRefreshToken()
    {
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
        return new IssuedRefreshToken(token, HashRefreshToken(token), DateTime.UtcNow.AddDays(_settings.RefreshTokenDays));
    }

    public string HashRefreshToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
    }

    private static void AddIfPresent(List<Claim> claims, string type, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            claims.Add(new Claim(type, value));
        }
    }
}
