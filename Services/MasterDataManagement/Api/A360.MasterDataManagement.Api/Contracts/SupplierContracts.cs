using SupplierEntity = A360.MasterDataManagement.Domain.Entities.Supplier;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateSupplierRequest(
    string? SupplierName,
    string? Code,
    string? Website,
    string? SupplierLogo,
    string? PrimaryContact,
    string? Category,
    string? Email,
    string? Country,
    string? Phone,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public SupplierEntity ToEntity()
    {
        return new SupplierEntity
        {
            SupplierName = SupplierName!.Trim(),
            Code = Code!.Trim(),
            Website = string.IsNullOrWhiteSpace(Website) ? null : Website.Trim(),
            SupplierLogo = string.IsNullOrWhiteSpace(SupplierLogo) ? null : SupplierLogo.Trim(),
            PrimaryContact = string.IsNullOrWhiteSpace(PrimaryContact) ? null : PrimaryContact.Trim(),
            Category = string.IsNullOrWhiteSpace(Category) ? null : Category.Trim(),
            Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
            Country = string.IsNullOrWhiteSpace(Country) ? null : Country.Trim(),
            Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim(),

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateSupplierRequest(
    string? SupplierName,
    string? Code,
    string? Website,
    string? SupplierLogo,
    string? PrimaryContact,
    string? Category,
    string? Email,
    string? Country,
    string? Phone,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(SupplierEntity supplier)
    {
        supplier.SupplierName = SupplierName!.Trim();
        supplier.Code = Code!.Trim();
        supplier.Website = string.IsNullOrWhiteSpace(Website) ? null : Website.Trim();
        supplier.SupplierLogo = string.IsNullOrWhiteSpace(SupplierLogo) ? null : SupplierLogo.Trim();
        supplier.PrimaryContact = string.IsNullOrWhiteSpace(PrimaryContact) ? null : PrimaryContact.Trim();
        supplier.Category = string.IsNullOrWhiteSpace(Category) ? null : Category.Trim();
        supplier.Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();
        supplier.Country = string.IsNullOrWhiteSpace(Country) ? null : Country.Trim();
        supplier.Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim();

        supplier.UpdatedBy = UpdatedBy;
        supplier.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            supplier.Status = Status;
        }
    }
}

public sealed record UpdateSupplierStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record SupplierResponse(
    string Id,
    string SupplierName,
    string Code,
    string? Website,
    string? SupplierLogo,
    string? PrimaryContact,
    string? Category,
    string? Email,
    string? Country,
    string? Phone,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static SupplierResponse FromEntity(SupplierEntity supplier)
    {
        return new SupplierResponse(
            supplier.Id ?? string.Empty,
            supplier.SupplierName ?? string.Empty,
            supplier.Code ?? string.Empty,
            supplier.Website,
            supplier.SupplierLogo,
            supplier.PrimaryContact,
            supplier.Category,
            supplier.Email,
            supplier.Country,
            supplier.Phone,
            supplier.CreatedBy,
            supplier.CreatedAt,
            supplier.UpdatedBy,
            supplier.UpdatedAt,
            supplier.ClientId,
            supplier.TenantId,
            supplier.Status,
            supplier.IsDeleted
        );
    }
}
