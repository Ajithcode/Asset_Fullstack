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
    string? BrandColor,
    StructureAddressDto? Address,
    string? AlternateContact,
    string? AlternateEmail,
    string? AlternatePhone,
    List<VendorDocumentDto>? Documents,
    decimal? Rating,
    List<string>? Products,
    List<string>? AssetCategories,
    List<string>? Brands,
    string? Currency,
    string? PaymentTerms,
    decimal? CreditLimit,
    string? Trn,
    string? BankName,
    string? Iban,
    bool? Preferred,
    bool? Approved,
    bool? PortalAccess,
    bool? EmailPurchaseOrders,
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
            BrandColor = StructureText.Clean(BrandColor),
            Address = Address?.ToValue(),
            AlternateContact = StructureText.Clean(AlternateContact),
            AlternateEmail = StructureText.Clean(AlternateEmail),
            AlternatePhone = StructureText.Clean(AlternatePhone),
            Documents = VendorDocumentDto.ToValues(Documents),
            Rating = Rating ?? 0,
            Products = StructureText.CleanList(Products),
            AssetCategories = StructureText.CleanList(AssetCategories),
            Brands = StructureText.CleanList(Brands),
            Currency = StructureText.Clean(Currency),
            PaymentTerms = StructureText.Clean(PaymentTerms),
            CreditLimit = CreditLimit,
            Trn = StructureText.Clean(Trn),
            BankName = StructureText.Clean(BankName),
            Iban = StructureText.Clean(Iban),
            Preferred = Preferred ?? false,
            Approved = Approved ?? false,
            PortalAccess = PortalAccess ?? false,
            EmailPurchaseOrders = EmailPurchaseOrders ?? false,

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
    string? BrandColor,
    StructureAddressDto? Address,
    string? AlternateContact,
    string? AlternateEmail,
    string? AlternatePhone,
    List<VendorDocumentDto>? Documents,
    decimal? Rating,
    List<string>? Products,
    List<string>? AssetCategories,
    List<string>? Brands,
    string? Currency,
    string? PaymentTerms,
    decimal? CreditLimit,
    string? Trn,
    string? BankName,
    string? Iban,
    bool? Preferred,
    bool? Approved,
    bool? PortalAccess,
    bool? EmailPurchaseOrders,
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
        supplier.BrandColor = StructureText.Clean(BrandColor);
        supplier.Address = Address?.ToValue();
        supplier.AlternateContact = StructureText.Clean(AlternateContact);
        supplier.AlternateEmail = StructureText.Clean(AlternateEmail);
        supplier.AlternatePhone = StructureText.Clean(AlternatePhone);
        supplier.Documents = VendorDocumentDto.ToValues(Documents);
        supplier.Rating = Rating ?? 0;
        supplier.Products = StructureText.CleanList(Products);
        supplier.AssetCategories = StructureText.CleanList(AssetCategories);
        supplier.Brands = StructureText.CleanList(Brands);
        supplier.Currency = StructureText.Clean(Currency);
        supplier.PaymentTerms = StructureText.Clean(PaymentTerms);
        supplier.CreditLimit = CreditLimit;
        supplier.Trn = StructureText.Clean(Trn);
        supplier.BankName = StructureText.Clean(BankName);
        supplier.Iban = StructureText.Clean(Iban);
        supplier.Preferred = Preferred ?? false;
        supplier.Approved = Approved ?? false;
        supplier.PortalAccess = PortalAccess ?? false;
        supplier.EmailPurchaseOrders = EmailPurchaseOrders ?? false;

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
    string? BrandColor,
    StructureAddressDto? Address,
    string? AlternateContact,
    string? AlternateEmail,
    string? AlternatePhone,
    IReadOnlyList<VendorDocumentDto> Documents,
    decimal Rating,
    IReadOnlyList<string> Products,
    IReadOnlyList<string> AssetCategories,
    IReadOnlyList<string> Brands,
    string? Currency,
    string? PaymentTerms,
    decimal? CreditLimit,
    string? Trn,
    string? BankName,
    string? Iban,
    bool Preferred,
    bool Approved,
    bool PortalAccess,
    bool EmailPurchaseOrders,
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
            supplier.BrandColor,
            StructureAddressDto.FromValue(supplier.Address),
            supplier.AlternateContact,
            supplier.AlternateEmail,
            supplier.AlternatePhone,
            VendorDocumentDto.FromValues(supplier.Documents),
            supplier.Rating,
            supplier.Products ?? [],
            supplier.AssetCategories ?? [],
            supplier.Brands ?? [],
            supplier.Currency,
            supplier.PaymentTerms,
            supplier.CreditLimit,
            supplier.Trn,
            supplier.BankName,
            supplier.Iban,
            supplier.Preferred,
            supplier.Approved,
            supplier.PortalAccess,
            supplier.EmailPurchaseOrders,
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
