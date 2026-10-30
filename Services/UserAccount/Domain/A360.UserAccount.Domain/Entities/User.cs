using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.UserAccount.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class User : BaseEntity
{
    [BsonElement("user_id")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("user_name")]
    public string UserName { get; set; } = string.Empty;

    [BsonElement("short_name")]
    public string ShortName { get; set; } = string.Empty;



    [BsonElement("role_name")]
    public string RoleName { get; set; } = string.Empty;


    [BsonElement("contact_no")]
    public string ContactNo { get; set; } = string.Empty;

    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("login_password")]
    public string LoginPassword { get; set; } = string.Empty;

    [BsonElement("active_directory_user_name")]
    public string ActiveDirectoryUserName { get; set; } = string.Empty;

    [BsonElement("user_role_id")]
    public string UserRoleId { get; set; } = string.Empty;

    [BsonElement("two_factor_code")]
    public string TwoFactorCode { get; set; } = string.Empty;

    [BsonElement("two_factor_expiration")]
    public DateTime? TwoFactorExpiration { get; set; }

    [BsonElement("two_factor_attempts")]
    public int TwoFactorAttempts { get; set; }

    /// <summary>True once the user has scanned the QR code and confirmed a code from the authenticator app.</summary>
    [BsonElement("totp_enabled")]
    public bool TotpEnabled { get; set; }

    /// <summary>Authenticator (TOTP) secret, encrypted; see TotpService.</summary>
    [BsonElement("totp_secret")]
    public string TotpSecret { get; set; } = string.Empty;

    /// <summary>Secret shown as a QR code during setup, encrypted; becomes TotpSecret when the first code is confirmed.</summary>
    [BsonElement("totp_pending_secret")]
    public string TotpPendingSecret { get; set; } = string.Empty;

    /// <summary>Last accepted 30-second time step, so a code cannot be used twice.</summary>
    [BsonElement("totp_last_used_step")]
    public long TotpLastUsedStep { get; set; }

    [BsonElement("password_reset_token")]
    public string PasswordResetToken { get; set; } = string.Empty;

    [BsonElement("password_reset_token_expiration")]
    public DateTime? PasswordResetTokenExpiration { get; set; }

    [BsonElement("forget_password_otp_code")]
    public string ForgetPasswordOtpCode { get; set; } = string.Empty;

    [BsonElement("forget_password_otp_code_expiration")]
    public DateTime? ForgetPasswordOtpCodeExpiration { get; set; }

    [BsonElement("last_login")]
    public DateTime? LastLogin { get; set; }

    [BsonElement("login_status")]
    public string LoginStatus { get; set; } = string.Empty;
}
