using AssetTypeEntity = A360.MasterDataManagement.Domain.Entities.AssetType;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateAssetTypeRequest(
    string? AssetTypeName,
    string? Code,
    MasterDataReferenceDto? Category,
    string? AssetClass,
    string? Description,
    string? DepreciationMethod,
    int? DefaultLifetime,
    string? Icon,
    string? Image,
    List<string>? Subcategories,
    string? TaggingFormat,
    string? MaintenanceType,
    string? MaintenancePlan,
    int? WarrantyMonths,
    string? ServiceProvider,
    int? UsefulLifeMonths,
    decimal? ResidualPercent,
    string? CostCenter,
    string? Currency,
    decimal? CapitalizationThreshold,
    string? ProcurementType,
    string? DefaultCondition,
    string? TrackingMethod,
    bool? SerialRequired,
    bool? Assignable,
    List<AssetCustomFieldDto>? CustomFields,
    bool? InspectionRequired,
    bool? AllowTransfers,
    bool? IotTracking,
    bool? DisposalApproval,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public AssetTypeEntity ToEntity()
    {
        return new AssetTypeEntity
        {
            AssetTypeName = AssetTypeName!.Trim(),
            Code = Code!.Trim(),
            Category = Category?.ToReference(),
            AssetClass = string.IsNullOrWhiteSpace(AssetClass) ? null : AssetClass.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            DepreciationMethod = string.IsNullOrWhiteSpace(DepreciationMethod) ? null : DepreciationMethod.Trim(),
            DefaultLifetime = DefaultLifetime,
            Icon = string.IsNullOrWhiteSpace(Icon) ? null : Icon.Trim(),
            Image = string.IsNullOrWhiteSpace(Image) ? null : Image,
            Subcategories = Subcategories?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList() ?? [],
            TaggingFormat = string.IsNullOrWhiteSpace(TaggingFormat) ? null : TaggingFormat.Trim(),
            MaintenanceType = string.IsNullOrWhiteSpace(MaintenanceType) ? null : MaintenanceType.Trim(),
            MaintenancePlan = string.IsNullOrWhiteSpace(MaintenancePlan) ? null : MaintenancePlan.Trim(),
            WarrantyMonths = WarrantyMonths,
            ServiceProvider = string.IsNullOrWhiteSpace(ServiceProvider) ? null : ServiceProvider.Trim(),
            UsefulLifeMonths = UsefulLifeMonths,
            ResidualPercent = ResidualPercent,
            CostCenter = string.IsNullOrWhiteSpace(CostCenter) ? null : CostCenter.Trim(),
            Currency = string.IsNullOrWhiteSpace(Currency) ? null : Currency.Trim(),
            CapitalizationThreshold = CapitalizationThreshold,
            ProcurementType = string.IsNullOrWhiteSpace(ProcurementType) ? null : ProcurementType.Trim(),
            DefaultCondition = string.IsNullOrWhiteSpace(DefaultCondition) ? null : DefaultCondition.Trim(),
            TrackingMethod = string.IsNullOrWhiteSpace(TrackingMethod) ? null : TrackingMethod.Trim(),
            SerialRequired = SerialRequired ?? false,
            Assignable = Assignable ?? false,
            CustomFields = CustomFields?.Select(x => x.ToEntity()).ToList() ?? [],
            InspectionRequired = InspectionRequired ?? false,
            AllowTransfers = AllowTransfers ?? false,
            IotTracking = IotTracking ?? false,
            DisposalApproval = DisposalApproval ?? false,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateAssetTypeRequest(
    string? AssetTypeName,
    string? Code,
    MasterDataReferenceDto? Category,
    string? AssetClass,
    string? Description,
    string? DepreciationMethod,
    int? DefaultLifetime,
    string? Icon,
    string? Image,
    List<string>? Subcategories,
    string? TaggingFormat,
    string? MaintenanceType,
    string? MaintenancePlan,
    int? WarrantyMonths,
    string? ServiceProvider,
    int? UsefulLifeMonths,
    decimal? ResidualPercent,
    string? CostCenter,
    string? Currency,
    decimal? CapitalizationThreshold,
    string? ProcurementType,
    string? DefaultCondition,
    string? TrackingMethod,
    bool? SerialRequired,
    bool? Assignable,
    List<AssetCustomFieldDto>? CustomFields,
    bool? InspectionRequired,
    bool? AllowTransfers,
    bool? IotTracking,
    bool? DisposalApproval,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(AssetTypeEntity assetType)
    {
        assetType.AssetTypeName = AssetTypeName!.Trim();
        assetType.Code = Code!.Trim();
        assetType.Category = Category?.ToReference();
        assetType.AssetClass = string.IsNullOrWhiteSpace(AssetClass) ? null : AssetClass.Trim();
        assetType.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        assetType.DepreciationMethod = string.IsNullOrWhiteSpace(DepreciationMethod) ? null : DepreciationMethod.Trim();
        assetType.DefaultLifetime = DefaultLifetime;
        assetType.Icon = string.IsNullOrWhiteSpace(Icon) ? null : Icon.Trim();
        assetType.Image = string.IsNullOrWhiteSpace(Image) ? null : Image;
        assetType.Subcategories = Subcategories?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList() ?? [];
        assetType.TaggingFormat = string.IsNullOrWhiteSpace(TaggingFormat) ? null : TaggingFormat.Trim();
        assetType.MaintenanceType = string.IsNullOrWhiteSpace(MaintenanceType) ? null : MaintenanceType.Trim();
        assetType.MaintenancePlan = string.IsNullOrWhiteSpace(MaintenancePlan) ? null : MaintenancePlan.Trim();
        assetType.WarrantyMonths = WarrantyMonths;
        assetType.ServiceProvider = string.IsNullOrWhiteSpace(ServiceProvider) ? null : ServiceProvider.Trim();
        assetType.UsefulLifeMonths = UsefulLifeMonths;
        assetType.ResidualPercent = ResidualPercent;
        assetType.CostCenter = string.IsNullOrWhiteSpace(CostCenter) ? null : CostCenter.Trim();
        assetType.Currency = string.IsNullOrWhiteSpace(Currency) ? null : Currency.Trim();
        assetType.CapitalizationThreshold = CapitalizationThreshold;
        assetType.ProcurementType = string.IsNullOrWhiteSpace(ProcurementType) ? null : ProcurementType.Trim();
        assetType.DefaultCondition = string.IsNullOrWhiteSpace(DefaultCondition) ? null : DefaultCondition.Trim();
        assetType.TrackingMethod = string.IsNullOrWhiteSpace(TrackingMethod) ? null : TrackingMethod.Trim();
        assetType.SerialRequired = SerialRequired ?? false;
        assetType.Assignable = Assignable ?? false;
        assetType.CustomFields = CustomFields?.Select(x => x.ToEntity()).ToList() ?? [];
        assetType.InspectionRequired = InspectionRequired ?? false;
        assetType.AllowTransfers = AllowTransfers ?? false;
        assetType.IotTracking = IotTracking ?? false;
        assetType.DisposalApproval = DisposalApproval ?? false;

        assetType.UpdatedBy = UpdatedBy;
        assetType.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            assetType.Status = Status;
        }
    }
}

public sealed record UpdateAssetTypeStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record AssetTypeResponse(
    string Id,
    string AssetTypeName,
    string Code,
    MasterDataReferenceDto? Category,
    string? AssetClass,
    string? Description,
    string? DepreciationMethod,
    int? DefaultLifetime,
    string? Icon,
    string? Image,
    IReadOnlyList<string> Subcategories,
    string? TaggingFormat,
    string? MaintenanceType,
    string? MaintenancePlan,
    int? WarrantyMonths,
    string? ServiceProvider,
    int? UsefulLifeMonths,
    decimal? ResidualPercent,
    string? CostCenter,
    string? Currency,
    decimal? CapitalizationThreshold,
    string? ProcurementType,
    string? DefaultCondition,
    string? TrackingMethod,
    bool SerialRequired,
    bool Assignable,
    IReadOnlyList<AssetCustomFieldDto> CustomFields,
    bool InspectionRequired,
    bool AllowTransfers,
    bool IotTracking,
    bool DisposalApproval,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static AssetTypeResponse FromEntity(AssetTypeEntity assetType)
    {
        return new AssetTypeResponse(
            assetType.Id ?? string.Empty,
            assetType.AssetTypeName ?? string.Empty,
            assetType.Code ?? string.Empty,
            assetType.Category is null
                ? null
                : MasterDataReferenceDto.FromReference(assetType.Category),
            assetType.AssetClass,
            assetType.Description,
            assetType.DepreciationMethod,
            assetType.DefaultLifetime,
            assetType.Icon,
            assetType.Image,
            assetType.Subcategories ?? [],
            assetType.TaggingFormat,
            assetType.MaintenanceType,
            assetType.MaintenancePlan,
            assetType.WarrantyMonths,
            assetType.ServiceProvider,
            assetType.UsefulLifeMonths,
            assetType.ResidualPercent,
            assetType.CostCenter,
            assetType.Currency,
            assetType.CapitalizationThreshold,
            assetType.ProcurementType,
            assetType.DefaultCondition,
            assetType.TrackingMethod,
            assetType.SerialRequired,
            assetType.Assignable,
            (assetType.CustomFields ?? []).Select(AssetCustomFieldDto.FromEntity).ToList(),
            assetType.InspectionRequired,
            assetType.AllowTransfers,
            assetType.IotTracking,
            assetType.DisposalApproval,
            assetType.CreatedBy,
            assetType.CreatedAt,
            assetType.UpdatedBy,
            assetType.UpdatedAt,
            assetType.ClientId,
            assetType.TenantId,
            assetType.Status,
            assetType.IsDeleted
        );
    }
}
