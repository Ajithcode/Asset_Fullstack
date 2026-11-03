using A360.Asset.Api.Validation;
using A360.Asset.Domain.Entities;

namespace A360.Asset.Api.Contracts;

public sealed record CreateAssetRegistrationDraftRequest(
    AssetBasicInformationDto? BasicInformation,
    string? CreatedBy,
    string? ClientId,
    string? TenantId);

public sealed record AssetBasicInformationDto(
    string? AssetName,
    string? AssetTag,
    string? Barcode,
    string? RfidTagId,
    string? Model,
    string? Manufacturer,
    string? SerialNumber,
    string? Status,
    string? Condition,
    string? Criticality,
    string? Description,
    List<string>? Images)
{
    public AssetBasicInformation ToSection()
    {
        return new AssetBasicInformation
        {
            AssetName = ContractText.TrimOrNull(AssetName),
            AssetTag = ContractText.TrimOrNull(AssetTag),
            Barcode = ContractText.TrimOrNull(Barcode),
            RfidTagId = ContractText.TrimOrNull(RfidTagId),
            Model = ContractText.TrimOrNull(Model),
            Manufacturer = ContractText.TrimOrNull(Manufacturer),
            SerialNumber = ContractText.TrimOrNull(SerialNumber),
            Status = ContractText.TrimOrNull(Status),
            Condition = ContractText.TrimOrNull(Condition),
            Criticality = ContractText.TrimOrNull(Criticality),
            Description = ContractText.TrimOrNull(Description),
            Images = ContractText.CleanList(Images, StringComparer.Ordinal)
        };
    }

    public static AssetBasicInformationDto? FromSection(AssetBasicInformation? section)
    {
        return section is null
            ? null
            : new AssetBasicInformationDto(
                section.AssetName,
                section.AssetTag,
                section.Barcode,
                section.RfidTagId,
                section.Model,
                section.Manufacturer,
                section.SerialNumber,
                section.Status,
                section.Condition,
                section.Criticality,
                section.Description,
                section.Images);
    }
}

public sealed record AssetClassificationDto(
    string? Category,
    string? SubCategory,
    string? AssetType,
    string? AssetSubType,
    string? FunctionPurpose,
    string? Criticality,
    string? Department,
    string? CostCenter,
    string? BusinessUnit,
    string? TaggingMethod,
    string? ComplianceCategory,
    string? LifecycleStage,
    List<string>? Keywords)
{
    public AssetClassification ToSection()
    {
        return new AssetClassification
        {
            Category = ContractText.TrimOrNull(Category),
            SubCategory = ContractText.TrimOrNull(SubCategory),
            AssetType = ContractText.TrimOrNull(AssetType),
            AssetSubType = ContractText.TrimOrNull(AssetSubType),
            FunctionPurpose = ContractText.TrimOrNull(FunctionPurpose),
            Criticality = ContractText.TrimOrNull(Criticality),
            Department = ContractText.TrimOrNull(Department),
            CostCenter = ContractText.TrimOrNull(CostCenter),
            BusinessUnit = ContractText.TrimOrNull(BusinessUnit),
            TaggingMethod = AssetRegistrationDraftValidationExtensions.NormalizeTaggingMethod(ContractText.TrimOrNull(TaggingMethod)),
            ComplianceCategory = ContractText.TrimOrNull(ComplianceCategory),
            LifecycleStage = ContractText.TrimOrNull(LifecycleStage),
            Keywords = ContractText.CleanList(Keywords, StringComparer.OrdinalIgnoreCase)
        };
    }

    public static AssetClassificationDto? FromSection(AssetClassification? section)
    {
        return section is null
            ? null
            : new AssetClassificationDto(
                section.Category,
                section.SubCategory,
                section.AssetType,
                section.AssetSubType,
                section.FunctionPurpose,
                section.Criticality,
                section.Department,
                section.CostCenter,
                section.BusinessUnit,
                section.TaggingMethod,
                section.ComplianceCategory,
                section.LifecycleStage,
                section.Keywords);
    }
}

public sealed record AssetLocationAssignmentDto(
    AssetLocationDto? Location,
    AssetAssignmentDto? Assignment)
{
    public AssetLocationAssignment ToSection()
    {
        return new AssetLocationAssignment
        {
            Location = (Location ?? AssetLocationDto.Empty).ToSection(),
            Assignment = (Assignment ?? AssetAssignmentDto.Empty).ToSection()
        };
    }

    public static AssetLocationAssignmentDto? FromSection(AssetLocationAssignment? section)
    {
        return section is null
            ? null
            : new AssetLocationAssignmentDto(
                AssetLocationDto.FromSection(section.Location),
                AssetAssignmentDto.FromSection(section.Assignment));
    }
}

public sealed record AssetLocationDto(
    string? ProjectId,
    string? CountryId,
    string? CountryName,
    string? SiteId,
    string? SiteName,
    string? BuildingId,
    string? BuildingName,
    string? FloorId,
    string? FloorName,
    string? ZoneId,
    string? ZoneName,
    string? RoomId,
    string? RoomName,
    string? LocationPath)
{
    public static readonly AssetLocationDto Empty =
        new(null, null, null, null, null, null, null, null, null, null, null, null, null, null);

    public AssetLocation ToSection()
    {
        var section = new AssetLocation
        {
            ProjectId = ContractText.TrimOrNull(ProjectId),
            CountryId = ContractText.TrimOrNull(CountryId),
            CountryName = ContractText.TrimOrNull(CountryName),
            SiteId = ContractText.TrimOrNull(SiteId),
            SiteName = ContractText.TrimOrNull(SiteName),
            BuildingId = ContractText.TrimOrNull(BuildingId),
            BuildingName = ContractText.TrimOrNull(BuildingName),
            FloorId = ContractText.TrimOrNull(FloorId),
            FloorName = ContractText.TrimOrNull(FloorName),
            ZoneId = ContractText.TrimOrNull(ZoneId),
            ZoneName = ContractText.TrimOrNull(ZoneName),
            RoomId = ContractText.TrimOrNull(RoomId),
            RoomName = ContractText.TrimOrNull(RoomName)
        };

        // The path is derived from the selected levels; any value sent by the client is ignored.
        var pathParts = new[] { section.CountryName, section.SiteName, section.BuildingName, section.FloorName, section.ZoneName, section.RoomName }
            .Where(part => part is not null)
            .ToList();
        section.LocationPath = pathParts.Count == 0 ? null : string.Join(" > ", pathParts);

        return section;
    }

    public static AssetLocationDto FromSection(AssetLocation section)
    {
        return new AssetLocationDto(
            section.ProjectId,
            section.CountryId,
            section.CountryName,
            section.SiteId,
            section.SiteName,
            section.BuildingId,
            section.BuildingName,
            section.FloorId,
            section.FloorName,
            section.ZoneId,
            section.ZoneName,
            section.RoomId,
            section.RoomName,
            section.LocationPath);
    }
}

public sealed record AssetAssignmentDto(
    string? CustodianDepartment,
    string? AssignedToUserId,
    string? AssignedToName,
    string? OwnershipType,
    string? CostCenter,
    string? ResponsibleTeam,
    DateTime? AssignmentStartDate,
    DateTime? AssignmentEndDate,
    bool? IsCurrent)
{
    public static readonly AssetAssignmentDto Empty =
        new(null, null, null, null, null, null, null, null, null);

    public AssetAssignment ToSection()
    {
        return new AssetAssignment
        {
            CustodianDepartment = ContractText.TrimOrNull(CustodianDepartment),
            AssignedToUserId = ContractText.TrimOrNull(AssignedToUserId),
            AssignedToName = ContractText.TrimOrNull(AssignedToName),
            OwnershipType = ContractText.TrimOrNull(OwnershipType),
            CostCenter = ContractText.TrimOrNull(CostCenter),
            ResponsibleTeam = ContractText.TrimOrNull(ResponsibleTeam),
            AssignmentStartDate = ContractText.DateOnly(AssignmentStartDate),
            AssignmentEndDate = ContractText.DateOnly(AssignmentEndDate),
            IsCurrent = IsCurrent ?? false
        };
    }

    public static AssetAssignmentDto FromSection(AssetAssignment section)
    {
        return new AssetAssignmentDto(
            section.CustodianDepartment,
            section.AssignedToUserId,
            section.AssignedToName,
            section.OwnershipType,
            section.CostCenter,
            section.ResponsibleTeam,
            section.AssignmentStartDate,
            section.AssignmentEndDate,
            section.IsCurrent);
    }
}

public sealed record AssetDocumentDto(
    string? FileName,
    string? Url,
    string? ContentType,
    long? FileSize,
    DateTime? UploadedAt)
{
    /// <summary>Keeps one entry per URL; validation has already ensured each has a FileName and Url.</summary>
    public static List<AssetDocument> ToSections(IEnumerable<AssetDocumentDto>? documents)
    {
        return (documents ?? [])
            .Where(document => !string.IsNullOrWhiteSpace(document?.Url))
            .GroupBy(document => document.Url!.Trim(), StringComparer.Ordinal)
            .Select(group => group.First())
            .Select(document => new AssetDocument
            {
                FileName = document.FileName!.Trim(),
                Url = document.Url!.Trim(),
                ContentType = ContractText.TrimOrNull(document.ContentType),
                FileSize = document.FileSize,
                UploadedAt = document.UploadedAt ?? DateTime.UtcNow
            })
            .ToList();
    }

    public static List<AssetDocumentDto> FromSections(IEnumerable<AssetDocument> documents)
    {
        return documents
            .Select(document => new AssetDocumentDto(document.FileName, document.Url, document.ContentType, document.FileSize, document.UploadedAt))
            .ToList();
    }
}

public sealed record AssetPurchaseWarrantyDto(
    AssetPurchaseDto? Purchase,
    List<AssetDocumentDto>? Documents,
    AssetWarrantyDto? Warranty)
{
    public AssetPurchaseWarranty ToSection()
    {
        return new AssetPurchaseWarranty
        {
            Purchase = (Purchase ?? AssetPurchaseDto.Empty).ToSection(),
            Documents = AssetDocumentDto.ToSections(Documents),
            Warranty = (Warranty ?? AssetWarrantyDto.Empty).ToSection()
        };
    }

    public static AssetPurchaseWarrantyDto? FromSection(AssetPurchaseWarranty? section)
    {
        return section is null
            ? null
            : new AssetPurchaseWarrantyDto(
                AssetPurchaseDto.FromSection(section.Purchase),
                AssetDocumentDto.FromSections(section.Documents),
                AssetWarrantyDto.FromSection(section.Warranty));
    }
}

public sealed record AssetPurchaseDto(
    string? PurchaseOrderNumber,
    string? InvoiceNumber,
    string? SupplierVendor,
    string? Manufacturer,
    DateTime? PurchaseDate,
    DateTime? DeliveryDate,
    decimal? PurchaseCost,
    decimal? Quantity,
    string? QuantityUnit,
    string? Currency,
    decimal? TaxPercent,
    decimal? TotalCost,
    string? PaymentTerms,
    string? ProcurementMethod,
    string? ProjectReference,
    string? Remarks)
{
    public static readonly AssetPurchaseDto Empty =
        new(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);

    public AssetPurchase ToSection()
    {
        return new AssetPurchase
        {
            PurchaseOrderNumber = ContractText.TrimOrNull(PurchaseOrderNumber),
            InvoiceNumber = ContractText.TrimOrNull(InvoiceNumber),
            SupplierVendor = ContractText.TrimOrNull(SupplierVendor),
            Manufacturer = ContractText.TrimOrNull(Manufacturer),
            PurchaseDate = ContractText.DateOnly(PurchaseDate),
            DeliveryDate = ContractText.DateOnly(DeliveryDate),
            PurchaseCost = PurchaseCost,
            Quantity = Quantity,
            QuantityUnit = ContractText.TrimOrNull(QuantityUnit),
            Currency = ContractText.TrimOrNull(Currency),
            TaxPercent = TaxPercent,
            // Any total sent by the client is ignored.
            TotalCost = PurchaseCost is { } cost
                ? decimal.Round(cost * (1 + (TaxPercent ?? 0) / 100), 2, MidpointRounding.AwayFromZero)
                : null,
            PaymentTerms = ContractText.TrimOrNull(PaymentTerms),
            ProcurementMethod = ContractText.TrimOrNull(ProcurementMethod),
            ProjectReference = ContractText.TrimOrNull(ProjectReference),
            Remarks = ContractText.TrimOrNull(Remarks)
        };
    }

    public static AssetPurchaseDto FromSection(AssetPurchase section)
    {
        return new AssetPurchaseDto(
            section.PurchaseOrderNumber,
            section.InvoiceNumber,
            section.SupplierVendor,
            section.Manufacturer,
            section.PurchaseDate,
            section.DeliveryDate,
            section.PurchaseCost,
            section.Quantity,
            section.QuantityUnit,
            section.Currency,
            section.TaxPercent,
            section.TotalCost,
            section.PaymentTerms,
            section.ProcurementMethod,
            section.ProjectReference,
            section.Remarks);
    }
}

public sealed record AssetWarrantyDto(
    string? WarrantyType,
    DateTime? WarrantyStartDate,
    DateTime? WarrantyEndDate,
    int? WarrantyPeriod,
    string? WarrantyPeriodUnit,
    bool? AmcIncluded,
    string? SupportCoverage,
    string? ServiceProvider,
    string? WarrantyReference)
{
    public static readonly AssetWarrantyDto Empty =
        new(null, null, null, null, null, null, null, null, null);

    public AssetWarranty ToSection()
    {
        var warrantyStartDate = ContractText.DateOnly(WarrantyStartDate);
        var warrantyPeriodUnit = WarrantyPeriodUnits.Normalize(ContractText.TrimOrNull(WarrantyPeriodUnit));

        return new AssetWarranty
        {
            WarrantyType = ContractText.TrimOrNull(WarrantyType),
            WarrantyStartDate = warrantyStartDate,
            WarrantyEndDate = WarrantyPeriodUnits.EndDate(warrantyStartDate, WarrantyPeriod, warrantyPeriodUnit)
                ?? ContractText.DateOnly(WarrantyEndDate),
            WarrantyPeriod = WarrantyPeriod,
            WarrantyPeriodUnit = warrantyPeriodUnit,
            AmcIncluded = AmcIncluded ?? false,
            SupportCoverage = ContractText.TrimOrNull(SupportCoverage),
            ServiceProvider = ContractText.TrimOrNull(ServiceProvider),
            WarrantyReference = ContractText.TrimOrNull(WarrantyReference)
        };
    }

    public static AssetWarrantyDto FromSection(AssetWarranty section)
    {
        return new AssetWarrantyDto(
            section.WarrantyType,
            section.WarrantyStartDate,
            section.WarrantyEndDate,
            section.WarrantyPeriod,
            section.WarrantyPeriodUnit,
            section.AmcIncluded,
            section.SupportCoverage,
            section.ServiceProvider,
            section.WarrantyReference);
    }
}

/// <summary>Warranty period units shown on the Purchase & Warranty step.</summary>
public static class WarrantyPeriodUnits
{
    public const string Days = "Day(s)";
    public const string Months = "Month(s)";
    public const string Years = "Year(s)";

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        [Days] = Days, ["Day"] = Days, ["Days"] = Days,
        [Months] = Months, ["Month"] = Months, ["Months"] = Months,
        [Years] = Years, ["Year"] = Years, ["Years"] = Years
    };

    public static IEnumerable<string> All => [Days, Months, Years];

    public static bool IsValid(string? unit) => unit is not null && Aliases.ContainsKey(unit.Trim());

    /// <summary>Returns the unit spelled as listed (e.g. "years" → "Year(s)"), or the value unchanged if it is not listed.</summary>
    public static string? Normalize(string? unit) => unit is not null && Aliases.TryGetValue(unit, out var listed) ? listed : unit;

    /// <summary>The last day covered: a 5 year warranty starting 28 Jun 2026 ends 27 Jun 2031.</summary>
    public static DateTime? EndDate(DateTime? startDate, int? period, string? unit)
    {
        if (startDate is not { } start || period is not > 0)
        {
            return null;
        }

        DateTime? afterPeriod = Normalize(unit) switch
        {
            Days => start.AddDays(period.Value),
            Months => start.AddMonths(period.Value),
            Years => start.AddYears(period.Value),
            _ => null
        };

        return afterPeriod?.AddDays(-1);
    }
}

public sealed record AssetSpecificationDto(string? Name, string? Value, string? Unit);

public sealed record AssetCustomFieldDto(string? FieldName, string? FieldType, string? Value);

public sealed record AssetAdditionalInformationDto(
    AssetTechnicalDetailsDto? Technical,
    AssetOperationalDetailsDto? Operational,
    List<AssetCustomFieldDto>? CustomFields,
    List<AssetDocumentDto>? Attachments)
{
    public AssetAdditionalInformation ToSection()
    {
        return new AssetAdditionalInformation
        {
            Technical = (Technical ?? AssetTechnicalDetailsDto.Empty).ToSection(),
            Operational = (Operational ?? AssetOperationalDetailsDto.Empty).ToSection(),
            // Rows left without a field name are blank form rows and are dropped.
            CustomFields = (CustomFields ?? [])
                .Where(field => !string.IsNullOrWhiteSpace(field?.FieldName))
                .Select(field => new AssetCustomField
                {
                    FieldName = field.FieldName!.Trim(),
                    FieldType = ContractText.TrimOrNull(field.FieldType),
                    Value = ContractText.TrimOrNull(field.Value)
                })
                .ToList(),
            Attachments = AssetDocumentDto.ToSections(Attachments)
        };
    }

    public static AssetAdditionalInformationDto? FromSection(AssetAdditionalInformation? section)
    {
        return section is null
            ? null
            : new AssetAdditionalInformationDto(
                AssetTechnicalDetailsDto.FromSection(section.Technical),
                AssetOperationalDetailsDto.FromSection(section.Operational),
                section.CustomFields
                    .Select(field => new AssetCustomFieldDto(field.FieldName, field.FieldType, field.Value))
                    .ToList(),
                AssetDocumentDto.FromSections(section.Attachments));
    }
}

public sealed record AssetTechnicalDetailsDto(
    List<AssetSpecificationDto>? Specifications,
    decimal? DimensionLength,
    decimal? DimensionWidth,
    decimal? DimensionHeight,
    string? DimensionUnit,
    decimal? Weight,
    string? WeightUnit,
    int? ExpectedLifespan,
    string? ExpectedLifespanUnit,
    string? TechnicalRemarks)
{
    public static readonly AssetTechnicalDetailsDto Empty =
        new(null, null, null, null, null, null, null, null, null, null);

    public AssetTechnicalDetails ToSection()
    {
        return new AssetTechnicalDetails
        {
            // Rows left without a name are blank form rows and are dropped.
            Specifications = (Specifications ?? [])
                .Where(specification => !string.IsNullOrWhiteSpace(specification?.Name))
                .Select(specification => new AssetSpecification
                {
                    Name = specification.Name!.Trim(),
                    Value = ContractText.TrimOrNull(specification.Value),
                    Unit = ContractText.TrimOrNull(specification.Unit)
                })
                .ToList(),
            DimensionLength = DimensionLength,
            DimensionWidth = DimensionWidth,
            DimensionHeight = DimensionHeight,
            DimensionUnit = ContractText.TrimOrNull(DimensionUnit),
            Weight = Weight,
            WeightUnit = ContractText.TrimOrNull(WeightUnit),
            ExpectedLifespan = ExpectedLifespan,
            ExpectedLifespanUnit = ContractText.TrimOrNull(ExpectedLifespanUnit),
            TechnicalRemarks = ContractText.TrimOrNull(TechnicalRemarks)
        };
    }

    public static AssetTechnicalDetailsDto FromSection(AssetTechnicalDetails section)
    {
        return new AssetTechnicalDetailsDto(
            section.Specifications
                .Select(specification => new AssetSpecificationDto(specification.Name, specification.Value, specification.Unit))
                .ToList(),
            section.DimensionLength,
            section.DimensionWidth,
            section.DimensionHeight,
            section.DimensionUnit,
            section.Weight,
            section.WeightUnit,
            section.ExpectedLifespan,
            section.ExpectedLifespanUnit,
            section.TechnicalRemarks);
    }
}

public sealed record AssetOperationalDetailsDto(
    decimal? OperatingHoursPerDay,
    string? OperatingHoursUnit,
    string? OperatingShift,
    string? Utilization,
    int? MaintenanceFrequency,
    string? MaintenanceFrequencyUnit,
    DateTime? LastMaintenanceDate,
    DateTime? NextMaintenanceDate,
    List<string>? ComplianceStandards)
{
    public static readonly AssetOperationalDetailsDto Empty =
        new(null, null, null, null, null, null, null, null, null);

    public AssetOperationalDetails ToSection()
    {
        return new AssetOperationalDetails
        {
            OperatingHoursPerDay = OperatingHoursPerDay,
            OperatingHoursUnit = ContractText.TrimOrNull(OperatingHoursUnit),
            OperatingShift = ContractText.TrimOrNull(OperatingShift),
            Utilization = ContractText.TrimOrNull(Utilization),
            MaintenanceFrequency = MaintenanceFrequency,
            MaintenanceFrequencyUnit = ContractText.TrimOrNull(MaintenanceFrequencyUnit),
            LastMaintenanceDate = ContractText.DateOnly(LastMaintenanceDate),
            NextMaintenanceDate = ContractText.DateOnly(NextMaintenanceDate),
            ComplianceStandards = ContractText.CleanList(ComplianceStandards, StringComparer.OrdinalIgnoreCase)
        };
    }

    public static AssetOperationalDetailsDto FromSection(AssetOperationalDetails section)
    {
        return new AssetOperationalDetailsDto(
            section.OperatingHoursPerDay,
            section.OperatingHoursUnit,
            section.OperatingShift,
            section.Utilization,
            section.MaintenanceFrequency,
            section.MaintenanceFrequencyUnit,
            section.LastMaintenanceDate,
            section.NextMaintenanceDate,
            section.ComplianceStandards);
    }
}

public sealed record AssetRegistrationResponse(
    string Id,
    string AssetRegistrationId,
    AssetBasicInformationDto? BasicInformation,
    AssetClassificationDto? Classification,
    AssetLocationAssignmentDto? LocationAssignment,
    AssetPurchaseWarrantyDto? PurchaseWarranty,
    AssetAdditionalInformationDto? AdditionalInformation,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId)
{
    public static AssetRegistrationResponse FromEntity(AssetRegistration asset)
    {
        return new AssetRegistrationResponse(
            asset.Id,
            asset.AssetRegistrationId,
            AssetBasicInformationDto.FromSection(asset.BasicInformation),
            AssetClassificationDto.FromSection(asset.Classification),
            AssetLocationAssignmentDto.FromSection(asset.LocationAssignment),
            AssetPurchaseWarrantyDto.FromSection(asset.PurchaseWarranty),
            AssetAdditionalInformationDto.FromSection(asset.AdditionalInformation),
            asset.CreatedBy,
            asset.CreatedAt,
            asset.UpdatedBy,
            asset.UpdatedAt,
            asset.ClientId,
            asset.TenantId);
    }
}

public sealed record AssetRegistrationDraftResponse(
    string Id,
    string AssetRegistrationId,
    int CurrentStep,
    AssetBasicInformationDto? BasicInformation,
    AssetClassificationDto? Classification,
    AssetLocationAssignmentDto? LocationAssignment,
    AssetPurchaseWarrantyDto? PurchaseWarranty,
    AssetAdditionalInformationDto? AdditionalInformation,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId)
{
    public static AssetRegistrationDraftResponse FromEntity(AssetRegistrationDraft draft)
    {
        return new AssetRegistrationDraftResponse(
            draft.Id,
            draft.AssetRegistrationId,
            draft.CurrentStep,
            AssetBasicInformationDto.FromSection(draft.BasicInformation),
            AssetClassificationDto.FromSection(draft.Classification),
            AssetLocationAssignmentDto.FromSection(draft.LocationAssignment),
            AssetPurchaseWarrantyDto.FromSection(draft.PurchaseWarranty),
            AssetAdditionalInformationDto.FromSection(draft.AdditionalInformation),
            draft.CreatedBy,
            draft.CreatedAt,
            draft.UpdatedBy,
            draft.UpdatedAt,
            draft.ClientId,
            draft.TenantId);
    }
}

internal static class ContractText
{
    public static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static string? FirstNonEmpty(string? requested, string? fallback)
    {
        return string.IsNullOrWhiteSpace(requested) ? fallback : requested.Trim();
    }

    /// <summary>
    /// Keeps only the calendar date, as UTC midnight. A date sent without a time ("2026-09-01")
    /// would otherwise be read as server local time and shift a day when stored as UTC.
    /// </summary>
    public static DateTime? DateOnly(DateTime? value)
    {
        return value is null ? null : DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Utc);
    }

    public static List<string> CleanList(IEnumerable<string>? values, StringComparer comparer)
    {
        return (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(comparer)
            .ToList();
    }
}
