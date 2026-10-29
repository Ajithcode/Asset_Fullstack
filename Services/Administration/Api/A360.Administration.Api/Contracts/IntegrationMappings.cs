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

    private static string? FirstNonEmpty(string? requested, string? fallback)
    {
        return string.IsNullOrWhiteSpace(requested) ? fallback : requested.Trim();
    }
}
