
using VendorAmcEntity = A360.Maintenance.Domain.Entities.VendorAmc;

namespace A360.Maintenance.Api.Contracts;

public sealed record CreateVendorAmcRequest(
    string? VendorName,
    string? ContractId,
    string? AssetId,
    string? AssetName,
    List<string>? AssetsCovered,
    DateTime? StartDate,
    DateTime? EndDate,
    string? SlaTerms,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public VendorAmcEntity ToEntity()
    {
        return new VendorAmcEntity
        {
            VendorName = VendorName,
            ContractId = ContractId,
            AssetId = AssetId,
            AssetName = AssetName,
            AssetsCovered = AssetsCovered ?? new List<string>(),
            StartDate = StartDate,
            EndDate = EndDate,
            SlaTerms = SlaTerms,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateVendorAmcRequest(
    string? VendorName,
    string? ContractId,
    string? AssetId,
    string? AssetName,
    List<string>? AssetsCovered,
    DateTime? StartDate,
    DateTime? EndDate,
    string? SlaTerms,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(VendorAmcEntity vendorAmc)
    {
        vendorAmc.VendorName = VendorName;
        vendorAmc.ContractId = ContractId;
        vendorAmc.AssetId = AssetId;
        vendorAmc.AssetName = AssetName;
        vendorAmc.AssetsCovered = AssetsCovered ?? new List<string>();
        vendorAmc.StartDate = StartDate;
        vendorAmc.EndDate = EndDate;
        vendorAmc.SlaTerms = SlaTerms;

        vendorAmc.UpdatedBy = UpdatedBy;
        vendorAmc.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            vendorAmc.Status = Status;
        }
    }
}

public sealed record VendorAmcResponse(
    string Id,
    string VendorName,
    string ContractId,
    string? AssetId,
    string? AssetName,
    List<string>? AssetsCovered,
    DateTime? StartDate,
    DateTime? EndDate,
    string? SlaTerms,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static VendorAmcResponse FromEntity(VendorAmcEntity vendorAmc)
    {
        return new VendorAmcResponse(
            vendorAmc.Id ?? string.Empty,
            vendorAmc.VendorName ?? string.Empty,
            vendorAmc.ContractId ?? string.Empty,
            vendorAmc.AssetId,
            vendorAmc.AssetName,
            vendorAmc.AssetsCovered,
            vendorAmc.StartDate,
            vendorAmc.EndDate,
            vendorAmc.SlaTerms,
            vendorAmc.CreatedBy,
            vendorAmc.CreatedAt,
            vendorAmc.UpdatedBy,
            vendorAmc.UpdatedAt,
            vendorAmc.ClientId,
            vendorAmc.TenantId,
            vendorAmc.Status,
            vendorAmc.IsDeleted
        );
    }
}
