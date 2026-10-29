using ServiceProviderEntity = A360.MasterDataManagement.Domain.Entities.ServiceProvider;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateServiceProviderRequest(
    string? ServiceProviderName,
    string? Code,
    string? Website,
    string? PrimaryContact,
    string? ServiceType,
    string? Email,
    string? Country,
    string? Phone,
    string? Description,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public ServiceProviderEntity ToEntity()
    {
        return new ServiceProviderEntity
        {
            ServiceProviderName = ServiceProviderName!.Trim(),
            Code = Code!.Trim(),
            Website = string.IsNullOrWhiteSpace(Website) ? null : Website.Trim(),
            PrimaryContact = string.IsNullOrWhiteSpace(PrimaryContact) ? null : PrimaryContact.Trim(),
            ServiceType = string.IsNullOrWhiteSpace(ServiceType) ? null : ServiceType.Trim(),
            Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
            Country = string.IsNullOrWhiteSpace(Country) ? null : Country.Trim(),
            Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateServiceProviderRequest(
    string? ServiceProviderName,
    string? Code,
    string? Website,
    string? PrimaryContact,
    string? ServiceType,
    string? Email,
    string? Country,
    string? Phone,
    string? Description,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(ServiceProviderEntity serviceProvider)
    {
        serviceProvider.ServiceProviderName = ServiceProviderName!.Trim();
        serviceProvider.Code = Code!.Trim();
        serviceProvider.Website = string.IsNullOrWhiteSpace(Website) ? null : Website.Trim();
        serviceProvider.PrimaryContact = string.IsNullOrWhiteSpace(PrimaryContact) ? null : PrimaryContact.Trim();
        serviceProvider.ServiceType = string.IsNullOrWhiteSpace(ServiceType) ? null : ServiceType.Trim();
        serviceProvider.Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();
        serviceProvider.Country = string.IsNullOrWhiteSpace(Country) ? null : Country.Trim();
        serviceProvider.Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim();
        serviceProvider.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();

        serviceProvider.UpdatedBy = UpdatedBy;
        serviceProvider.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            serviceProvider.Status = Status;
        }
    }
}

public sealed record UpdateServiceProviderStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record ServiceProviderResponse(
    string Id,
    string ServiceProviderName,
    string Code,
    string? Website,
    string? PrimaryContact,
    string? ServiceType,
    string? Email,
    string? Country,
    string? Phone,
    string? Description,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static ServiceProviderResponse FromEntity(ServiceProviderEntity serviceProvider)
    {
        return new ServiceProviderResponse(
            serviceProvider.Id ?? string.Empty,
            serviceProvider.ServiceProviderName ?? string.Empty,
            serviceProvider.Code ?? string.Empty,
            serviceProvider.Website,
            serviceProvider.PrimaryContact,
            serviceProvider.ServiceType,
            serviceProvider.Email,
            serviceProvider.Country,
            serviceProvider.Phone,
            serviceProvider.Description,
            serviceProvider.CreatedBy,
            serviceProvider.CreatedAt,
            serviceProvider.UpdatedBy,
            serviceProvider.UpdatedAt,
            serviceProvider.ClientId,
            serviceProvider.TenantId,
            serviceProvider.Status,
            serviceProvider.IsDeleted
        );
    }
}
