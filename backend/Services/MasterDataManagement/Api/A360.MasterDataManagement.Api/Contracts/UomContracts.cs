using UomEntity = A360.MasterDataManagement.Domain.Entities.Uom;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateUomRequest(
    string? UomName,
    string? Code,
    string? Type,
    string? Symbol,
    double? ConversionFactor,
    string? Description,
    MasterDataReferenceDto? BaseUom,
    bool? AllowDecimals,
    int? DecimalPlaces,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public UomEntity ToEntity()
    {
        return new UomEntity
        {
            UomName = UomName!.Trim(),
            Code = Code!.Trim(),
            Type = Type!.Trim(),
            Symbol = Symbol!.Trim(),
            ConversionFactor = ConversionFactor,
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            BaseUom = BaseUom?.ToReference(),
            AllowDecimals = AllowDecimals ?? false,
            DecimalPlaces = AllowDecimals == true ? DecimalPlaces ?? 0 : 0,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateUomRequest(
    string? UomName,
    string? Code,
    string? Type,
    string? Symbol,
    double? ConversionFactor,
    string? Description,
    MasterDataReferenceDto? BaseUom,
    bool? AllowDecimals,
    int? DecimalPlaces,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(UomEntity uom)
    {
        uom.UomName = UomName!.Trim();
        uom.Code = Code!.Trim();
        uom.Type = Type!.Trim();
        uom.Symbol = Symbol!.Trim();
        uom.ConversionFactor = ConversionFactor;
        uom.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        uom.BaseUom = BaseUom?.ToReference();
        uom.AllowDecimals = AllowDecimals ?? false;
        uom.DecimalPlaces = AllowDecimals == true ? DecimalPlaces ?? 0 : 0;

        uom.UpdatedBy = UpdatedBy;
        uom.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            uom.Status = Status;
        }
    }
}

public sealed record UpdateUomStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record UomResponse(
    string Id,
    string UomName,
    string Code,
    string Type,
    string Symbol,
    double? ConversionFactor,
    string? Description,
    MasterDataReferenceDto? BaseUom,
    bool AllowDecimals,
    int DecimalPlaces,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static UomResponse FromEntity(UomEntity uom)
    {
        return new UomResponse(
            uom.Id ?? string.Empty,
            uom.UomName ?? string.Empty,
            uom.Code ?? string.Empty,
            uom.Type ?? string.Empty,
            uom.Symbol ?? string.Empty,
            uom.ConversionFactor,
            uom.Description,
            uom.BaseUom is null
                ? null
                : MasterDataReferenceDto.FromReference(uom.BaseUom),
            uom.AllowDecimals,
            uom.DecimalPlaces,
            uom.CreatedBy,
            uom.CreatedAt,
            uom.UpdatedBy,
            uom.UpdatedAt,
            uom.ClientId,
            uom.TenantId,
            uom.Status,
            uom.IsDeleted
        );
    }
}
