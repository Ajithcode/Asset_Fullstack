using A360.Administration.Api.Security;
using A360.Administration.Domain.Entities;
using A360.Security;

namespace A360.Administration.Api.Contracts;

internal static class IntegrationMappings
{
    public static Integration ToEntity(
        this CreateIntegrationRequest request,
        CurrentUser? currentUser,
        ISecretProtector secretProtector)
    {
        return new Integration
        {
            // Generated on the server so every integration gets a stable id independent of the Mongo _id.
            IntegrationId = Guid.NewGuid().ToString(),
            IntegrationName = request.IntegrationName!.Trim(),
            IntegrationCode = request.IntegrationCode!.Trim().ToUpperInvariant(),
            Provider = request.Provider!.Trim(),
            Environment = request.Environment!.Trim(),
            BaseUrl = request.BaseUrl!.Trim(),
            AuthenticationMethod = request.AuthenticationMethod!.Trim(),
            AuthClientId = request.AuthClientId!.Trim(),
            // Not trimmed: secrets are stored exactly as issued.
            SecretKeyEncrypted = secretProtector.Protect(request.SecretKey!),
            SyncFrequency = request.SyncFrequency!.Trim(),
            TimeoutSeconds = request.TimeoutSeconds!.Value,
            RetryPolicy = request.RetryPolicy!.Trim(),
            Status = request.Status!.Trim(),
            CreatedBy = FirstNonEmpty(request.CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            TenantId = FirstNonEmpty(request.TenantId, currentUser?.TenantId),
            ClientId = FirstNonEmpty(request.ClientId, currentUser?.ClientId),
            IsDeleted = false
        };
    }

    public static void ApplyTo(
        this UpdateIntegrationRequest request,
        Integration integration,
        CurrentUser? currentUser,
        ISecretProtector secretProtector)
    {
        integration.IntegrationName = request.IntegrationName!.Trim();
        integration.IntegrationCode = request.IntegrationCode!.Trim().ToUpperInvariant();
        integration.Provider = request.Provider!.Trim();
        integration.Environment = request.Environment!.Trim();
        integration.BaseUrl = request.BaseUrl!.Trim();
        integration.AuthenticationMethod = request.AuthenticationMethod!.Trim();
        integration.AuthClientId = request.AuthClientId!.Trim();
        // Only re-encrypted when a new secret is sent; otherwise the stored secret is kept.
        if (!string.IsNullOrWhiteSpace(request.SecretKey))
        {
            integration.SecretKeyEncrypted = secretProtector.Protect(request.SecretKey);
        }

        integration.SyncFrequency = request.SyncFrequency!.Trim();
        integration.TimeoutSeconds = request.TimeoutSeconds!.Value;
        integration.RetryPolicy = request.RetryPolicy!.Trim();
        integration.Status = request.Status!.Trim();
        integration.UpdatedBy = FirstNonEmpty(request.UpdatedBy, currentUser?.UserName);
        integration.UpdatedAt = DateTime.UtcNow;
    }

    public static void MarkDeleted(this Integration integration, CurrentUser? currentUser)
    {
        integration.IsDeleted = true;
        integration.UpdatedBy = currentUser?.UserName ?? integration.UpdatedBy;
        integration.UpdatedAt = DateTime.UtcNow;
    }

    private static string? FirstNonEmpty(string? requested, string? fallback)
    {
        return string.IsNullOrWhiteSpace(requested) ? fallback : requested.Trim();
    }
}
