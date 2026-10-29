using A360.Administration.Domain.Entities;

namespace A360.Administration.Api.Contracts;

public sealed record CreateIntegrationRequest(
    string? IntegrationName,
    string? IntegrationCode,
    string? Provider,
    string? Environment,
    string? BaseUrl,
    string? AuthenticationMethod,
    string? AuthClientId,
    string? SecretKey,
    string? SyncFrequency,
    int? TimeoutSeconds,
    string? RetryPolicy,
    string? Status,
    string? CreatedBy,
    string? TenantId,
    string? ClientId);

// SecretKey is deliberately absent: it is write-only and never leaves the service.
public sealed record IntegrationResponse(
    string IntegrationId,
    string IntegrationName,
    string IntegrationCode,
    string Provider,
    string Environment,
    string BaseUrl,
    string AuthenticationMethod,
    string AuthClientId,
    bool SecretKeyConfigured,
    string SyncFrequency,
    int TimeoutSeconds,
    string RetryPolicy,
    string? Status,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? TenantId,
    string? ClientId)
{
    public static IntegrationResponse FromEntity(Integration integration)
    {
        return new IntegrationResponse(
            integration.IntegrationId,
            integration.IntegrationName,
            integration.IntegrationCode,
            integration.Provider,
            integration.Environment,
            integration.BaseUrl,
            integration.AuthenticationMethod,
            integration.AuthClientId,
            !string.IsNullOrEmpty(integration.SecretKeyEncrypted),
            integration.SyncFrequency,
            integration.TimeoutSeconds,
            integration.RetryPolicy,
            integration.Status,
            integration.CreatedBy,
            integration.CreatedAt,
            integration.UpdatedBy,
            integration.UpdatedAt,
            integration.TenantId,
            integration.ClientId);
    }
}
