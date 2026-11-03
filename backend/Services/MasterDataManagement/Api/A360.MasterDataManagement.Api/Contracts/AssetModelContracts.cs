using A360.MasterDataManagement.Domain.Entities;
using AssetModelEntity = A360.MasterDataManagement.Domain.Entities.AssetModel;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateAssetModelRequest(
    string? ModelName,
    string? ModelCode,
    MasterDataReferenceDto? Manufacturer,
    MasterDataReferenceDto? AssetType,
    MasterDataReferenceDto? Category,
    string? Description,
    string? Lifecycle,
    string? Image,
    List<AssetModelSpecDto>? Specs,
    int? LifecycleYears,
    int? WarrantyMonths,
    string? PmFrequency,
    string? MaintenanceStrategy,
    string? Criticality,
    bool? CalibrationRequired,
    string? CostCenter,
    string? Currency,
    decimal? PurchasePrice,
    bool? IncludeInDepreciation,
    string? DepreciationMethod,
    decimal? SalvagePercent,
    string? ProcurementType,
    List<string>? Subcategories,
    string? TaggingFormat,
    string? TrackingMethod,
    string? DefaultCondition,
    bool? SerialRequired,
    bool? Assignable,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public AssetModelEntity ToEntity()
    {
        return new AssetModelEntity
        {
            ModelName = ModelName!.Trim(),
            ModelCode = ModelCode!.Trim(),
            Manufacturer = Manufacturer?.ToReference(),
            AssetType = AssetType?.ToReference(),
            Category = Category?.ToReference(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            Lifecycle = string.IsNullOrWhiteSpace(Lifecycle) ? null : Lifecycle.Trim(),
            Image = string.IsNullOrWhiteSpace(Image) ? null : Image,
            Specs = AssetModelSpecDto.ToEntities(Specs),
            LifecycleYears = LifecycleYears,
            WarrantyMonths = WarrantyMonths,
            PmFrequency = string.IsNullOrWhiteSpace(PmFrequency) ? null : PmFrequency.Trim(),
            MaintenanceStrategy = string.IsNullOrWhiteSpace(MaintenanceStrategy) ? null : MaintenanceStrategy.Trim(),
            Criticality = string.IsNullOrWhiteSpace(Criticality) ? null : Criticality.Trim(),
            CalibrationRequired = CalibrationRequired ?? false,
            CostCenter = string.IsNullOrWhiteSpace(CostCenter) ? null : CostCenter.Trim(),
            Currency = string.IsNullOrWhiteSpace(Currency) ? null : Currency.Trim(),
            PurchasePrice = PurchasePrice,
            IncludeInDepreciation = IncludeInDepreciation ?? false,
            DepreciationMethod = string.IsNullOrWhiteSpace(DepreciationMethod) ? null : DepreciationMethod.Trim(),
            SalvagePercent = SalvagePercent,
            ProcurementType = string.IsNullOrWhiteSpace(ProcurementType) ? null : ProcurementType.Trim(),
            Subcategories = Subcategories?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList() ?? [],
            TaggingFormat = string.IsNullOrWhiteSpace(TaggingFormat) ? null : TaggingFormat.Trim(),
            TrackingMethod = string.IsNullOrWhiteSpace(TrackingMethod) ? null : TrackingMethod.Trim(),
            DefaultCondition = string.IsNullOrWhiteSpace(DefaultCondition) ? null : DefaultCondition.Trim(),
            SerialRequired = SerialRequired ?? false,
            Assignable = Assignable ?? false,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateAssetModelRequest(
    string? ModelName,
    string? ModelCode,
    MasterDataReferenceDto? Manufacturer,
    MasterDataReferenceDto? AssetType,
    MasterDataReferenceDto? Category,
    string? Description,
    string? Lifecycle,
    string? Image,
    List<AssetModelSpecDto>? Specs,
    int? LifecycleYears,
    int? WarrantyMonths,
    string? PmFrequency,
    string? MaintenanceStrategy,
    string? Criticality,
    bool? CalibrationRequired,
    string? CostCenter,
    string? Currency,
    decimal? PurchasePrice,
    bool? IncludeInDepreciation,
    string? DepreciationMethod,
    decimal? SalvagePercent,
    string? ProcurementType,
    List<string>? Subcategories,
    string? TaggingFormat,
    string? TrackingMethod,
    string? DefaultCondition,
    bool? SerialRequired,
    bool? Assignable,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(AssetModelEntity model)
    {
        model.ModelName = ModelName!.Trim();
        model.ModelCode = ModelCode!.Trim();
        model.Manufacturer = Manufacturer?.ToReference();
        model.AssetType = AssetType?.ToReference();
        model.Category = Category?.ToReference();
        model.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        model.Lifecycle = string.IsNullOrWhiteSpace(Lifecycle) ? null : Lifecycle.Trim();
        model.Image = string.IsNullOrWhiteSpace(Image) ? null : Image;
        model.Specs = AssetModelSpecDto.ToEntities(Specs);
        model.LifecycleYears = LifecycleYears;
        model.WarrantyMonths = WarrantyMonths;
        model.PmFrequency = string.IsNullOrWhiteSpace(PmFrequency) ? null : PmFrequency.Trim();
        model.MaintenanceStrategy = string.IsNullOrWhiteSpace(MaintenanceStrategy) ? null : MaintenanceStrategy.Trim();
        model.Criticality = string.IsNullOrWhiteSpace(Criticality) ? null : Criticality.Trim();
        model.CalibrationRequired = CalibrationRequired ?? false;
        model.CostCenter = string.IsNullOrWhiteSpace(CostCenter) ? null : CostCenter.Trim();
        model.Currency = string.IsNullOrWhiteSpace(Currency) ? null : Currency.Trim();
        model.PurchasePrice = PurchasePrice;
        model.IncludeInDepreciation = IncludeInDepreciation ?? false;
        model.DepreciationMethod = string.IsNullOrWhiteSpace(DepreciationMethod) ? null : DepreciationMethod.Trim();
        model.SalvagePercent = SalvagePercent;
        model.ProcurementType = string.IsNullOrWhiteSpace(ProcurementType) ? null : ProcurementType.Trim();
        model.Subcategories = Subcategories?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList() ?? [];
        model.TaggingFormat = string.IsNullOrWhiteSpace(TaggingFormat) ? null : TaggingFormat.Trim();
        model.TrackingMethod = string.IsNullOrWhiteSpace(TrackingMethod) ? null : TrackingMethod.Trim();
        model.DefaultCondition = string.IsNullOrWhiteSpace(DefaultCondition) ? null : DefaultCondition.Trim();
        model.SerialRequired = SerialRequired ?? false;
        model.Assignable = Assignable ?? false;

        model.UpdatedBy = UpdatedBy;
        model.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            model.Status = Status;
        }
    }
}

public sealed record UpdateAssetModelStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record AssetModelSpecDto(
    string? Label,
    string? Value)
{
    // Rows left completely empty are dropped.
    public static List<AssetModelSpec> ToEntities(List<AssetModelSpecDto>? specs)
    {
        return specs?
            .Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Label))
            .Select(x => new AssetModelSpec
            {
                Label = x.Label!.Trim(),
                Value = string.IsNullOrWhiteSpace(x.Value) ? null : x.Value.Trim()
            })
            .ToList() ?? [];
    }

    public static AssetModelSpecDto FromEntity(AssetModelSpec spec)
    {
        return new AssetModelSpecDto(spec.Label, spec.Value);
    }
}

public sealed record AssetModelResponse(
    string Id,
    string ModelName,
    string ModelCode,
    MasterDataReferenceDto? Manufacturer,
    MasterDataReferenceDto? AssetType,
    MasterDataReferenceDto? Category,
    string? Description,
    string? Lifecycle,
    string? Image,
    IReadOnlyList<AssetModelSpecDto> Specs,
    int? LifecycleYears,
    int? WarrantyMonths,
    string? PmFrequency,
    string? MaintenanceStrategy,
    string? Criticality,
    bool CalibrationRequired,
    string? CostCenter,
    string? Currency,
    decimal? PurchasePrice,
    bool IncludeInDepreciation,
    string? DepreciationMethod,
    decimal? SalvagePercent,
    string? ProcurementType,
    IReadOnlyList<string> Subcategories,
    string? TaggingFormat,
    string? TrackingMethod,
    string? DefaultCondition,
    bool SerialRequired,
    bool Assignable,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static AssetModelResponse FromEntity(AssetModelEntity model)
    {
        return new AssetModelResponse(
            model.Id ?? string.Empty,
            model.ModelName ?? string.Empty,
            model.ModelCode ?? string.Empty,
            model.Manufacturer is null
                ? null
                : MasterDataReferenceDto.FromReference(model.Manufacturer),
            model.AssetType is null
                ? null
                : MasterDataReferenceDto.FromReference(model.AssetType),
            model.Category is null
                ? null
                : MasterDataReferenceDto.FromReference(model.Category),
            model.Description,
            model.Lifecycle,
            model.Image,
            (model.Specs ?? []).Select(AssetModelSpecDto.FromEntity).ToList(),
            model.LifecycleYears,
            model.WarrantyMonths,
            model.PmFrequency,
            model.MaintenanceStrategy,
            model.Criticality,
            model.CalibrationRequired,
            model.CostCenter,
            model.Currency,
            model.PurchasePrice,
            model.IncludeInDepreciation,
            model.DepreciationMethod,
            model.SalvagePercent,
            model.ProcurementType,
            model.Subcategories ?? [],
            model.TaggingFormat,
            model.TrackingMethod,
            model.DefaultCondition,
            model.SerialRequired,
            model.Assignable,
            model.CreatedBy,
            model.CreatedAt,
            model.UpdatedBy,
            model.UpdatedAt,
            model.ClientId,
            model.TenantId,
            model.Status,
            model.IsDeleted
        );
    }
}
