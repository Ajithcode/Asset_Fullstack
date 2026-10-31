namespace A360.Asset.Api.Contracts;

/// <summary>
/// Fixed choices for the Asset Registration dropdowns that have no master data list of their own.
/// Manufacturer, Condition, Criticality, Department, Business Unit, Supplier, Service Provider, Team, Shift,
/// UoM and Standards come from Master Management; Category, Sub Category and Asset Type from the asset categories.
/// </summary>
public static class AssetRegistrationOptions
{
    // Step 1: Basic Information.
    public static readonly OptionList Statuses =
        new("Active", "Inactive", "In Storage", "Under Maintenance", "In Repair", "Out of Service", "Disposed");

    // Step 2: Classification.
    public static readonly OptionList FunctionPurposes =
        new("Backup Power", "Primary Power", "Production", "Cooling", "Heating", "Ventilation", "Lighting",
            "Safety & Security", "Fire Protection", "Material Handling", "Transportation", "IT & Communication",
            "Office Use", "Other");

    public static readonly OptionList TaggingMethods = new("Barcode", "RFID", "BLE", "QR Code");

    public static readonly OptionList ComplianceCategories =
        new("Safety Critical Equipment", "Statutory Inspection Required", "Environmental", "Fire & Life Safety",
            "Electrical Safety", "Pressure Equipment", "Lifting Equipment", "Not Applicable");

    public static readonly OptionList LifecycleStages =
        new("Planned", "Procured", "Received", "Installed", "Commissioned", "In Use", "Under Maintenance",
            "Idle", "Retired", "Disposed");

    // Step 3: Location & Assignment.
    public static readonly OptionList OwnershipTypes =
        new("Company Owned", "Leased", "Rented", "Customer Owned", "Vendor Owned", "Financed");

    // Step 4: Purchase & Warranty.
    public static readonly OptionList QuantityUnits = new("Unit(s)", "Set(s)", "Piece(s)", "Lot(s)");

    public static readonly OptionList Currencies =
        new("AED - UAE Dirham", "OMR - Omani Rial", "SAR - Saudi Riyal", "QAR - Qatari Riyal", "KWD - Kuwaiti Dinar",
            "BHD - Bahraini Dinar", "USD - US Dollar", "EUR - Euro", "GBP - British Pound", "INR - Indian Rupee");

    public static readonly OptionList PaymentTerms =
        new("Advance", "Cash on Delivery", "15 Days", "30 Days", "45 Days", "60 Days", "90 Days", "Milestone Based");

    public static readonly OptionList ProcurementMethods =
        new("Direct Purchase", "Tender", "Request for Quotation", "Framework Agreement", "Lease", "Donation", "Transfer");

    public static readonly OptionList WarrantyTypes =
        new("Manufacturer Warranty", "Extended Warranty", "Vendor Warranty", "AMC", "No Warranty");

    public static readonly OptionList SupportCoverages = new("24x7", "Business Hours", "8x5", "On Call", "None");

    // Step 5: Additional Information.
    public static readonly OptionList PowerRatingUnits = new("W", "kW", "MW", "VA", "kVA", "MVA", "HP");

    public static readonly OptionList VoltageUnits = new("V", "kV");

    public static readonly OptionList FrequencyUnits = new("Hz", "kHz");

    public static readonly OptionList Phases = new("Single Phase", "3 Phase", "DC");

    public static readonly OptionList FuelTypes =
        new("Diesel", "Petrol", "Natural Gas", "LPG", "Electric", "Hybrid", "Solar", "Not Applicable");

    public static readonly OptionList CapacityUnits = new("Liters", "Gallons", "m³", "kg");

    public static readonly OptionList DimensionUnits = new("mm", "cm", "m", "in", "ft");

    public static readonly OptionList WeightUnits = new("g", "kg", "ton", "lb");

    public static readonly OptionList TemperatureUnits = new("°C", "°F");

    public static readonly OptionList IpRatings =
        new("IP20", "IP21", "IP22", "IP23", "IP44", "IP54", "IP55", "IP65", "IP66", "IP67", "IP68");

    public static readonly OptionList NoiseLevelUnits = new("dB", "dB(A)");

    public static readonly OptionList LifespanUnits = new("Month(s)", "Year(s)");

    public static readonly OptionList OperatingHoursUnits = new("Hours", "Minutes");

    public static readonly OptionList OperatingShifts = new("24x7", "Day Shift", "Night Shift", "Two Shifts", "General Shift");

    public static readonly OptionList Utilizations = new("Primary", "Standby", "Backup", "Shared", "Idle");

    public static readonly OptionList MaintenanceFrequencyUnits = new("Hours", "Days", "Weeks", "Months", "Years");

    public static readonly OptionList ComplianceStandards =
        new("ISO 8528", "ISO 9001", "ISO 14001", "ISO 45001", "IEC 60034", "NFPA 110", "UAE Civil Defence", "Manufacturer Standard");

    public static readonly OptionList ImpactLevels = new("Low", "Medium", "High", "Critical");

    public static readonly OptionList CustomFieldTypes = new("Text", "Number", "Date", "Dropdown", "Yes/No");
}

public sealed record AssetRegistrationOptionsResponse(
    AssetBasicInformationOptions BasicInformation,
    AssetClassificationOptions Classification,
    AssetLocationAssignmentOptions LocationAssignment,
    AssetPurchaseWarrantyOptions PurchaseWarranty,
    AssetAdditionalInformationOptions AdditionalInformation)
{
    public static readonly AssetRegistrationOptionsResponse Instance = new(
        new AssetBasicInformationOptions(AssetRegistrationOptions.Statuses.All),
        new AssetClassificationOptions(
            AssetRegistrationOptions.FunctionPurposes.All,
            AssetRegistrationOptions.TaggingMethods.All,
            AssetRegistrationOptions.ComplianceCategories.All,
            AssetRegistrationOptions.LifecycleStages.All),
        new AssetLocationAssignmentOptions(AssetRegistrationOptions.OwnershipTypes.All),
        new AssetPurchaseWarrantyOptions(
            AssetRegistrationOptions.QuantityUnits.All,
            AssetRegistrationOptions.Currencies.All,
            AssetRegistrationOptions.PaymentTerms.All,
            AssetRegistrationOptions.ProcurementMethods.All,
            AssetRegistrationOptions.WarrantyTypes.All,
            WarrantyPeriodUnits.All.ToList(),
            AssetRegistrationOptions.SupportCoverages.All),
        new AssetAdditionalInformationOptions(
            AssetRegistrationOptions.PowerRatingUnits.All,
            AssetRegistrationOptions.VoltageUnits.All,
            AssetRegistrationOptions.FrequencyUnits.All,
            AssetRegistrationOptions.Phases.All,
            AssetRegistrationOptions.FuelTypes.All,
            AssetRegistrationOptions.CapacityUnits.All,
            AssetRegistrationOptions.DimensionUnits.All,
            AssetRegistrationOptions.WeightUnits.All,
            AssetRegistrationOptions.TemperatureUnits.All,
            AssetRegistrationOptions.IpRatings.All,
            AssetRegistrationOptions.NoiseLevelUnits.All,
            AssetRegistrationOptions.LifespanUnits.All,
            AssetRegistrationOptions.OperatingHoursUnits.All,
            AssetRegistrationOptions.OperatingShifts.All,
            AssetRegistrationOptions.Utilizations.All,
            AssetRegistrationOptions.MaintenanceFrequencyUnits.All,
            AssetRegistrationOptions.ComplianceStandards.All,
            AssetRegistrationOptions.ImpactLevels.All,
            AssetRegistrationOptions.CustomFieldTypes.All));
}

public sealed record AssetBasicInformationOptions(IReadOnlyList<string> Statuses);

public sealed record AssetClassificationOptions(
    IReadOnlyList<string> FunctionPurposes,
    IReadOnlyList<string> TaggingMethods,
    IReadOnlyList<string> ComplianceCategories,
    IReadOnlyList<string> LifecycleStages);

public sealed record AssetLocationAssignmentOptions(IReadOnlyList<string> OwnershipTypes);

public sealed record AssetPurchaseWarrantyOptions(
    IReadOnlyList<string> QuantityUnits,
    IReadOnlyList<string> Currencies,
    IReadOnlyList<string> PaymentTerms,
    IReadOnlyList<string> ProcurementMethods,
    IReadOnlyList<string> WarrantyTypes,
    IReadOnlyList<string> WarrantyPeriodUnits,
    IReadOnlyList<string> SupportCoverages);

public sealed record AssetAdditionalInformationOptions(
    IReadOnlyList<string> PowerRatingUnits,
    IReadOnlyList<string> VoltageUnits,
    IReadOnlyList<string> FrequencyUnits,
    IReadOnlyList<string> Phases,
    IReadOnlyList<string> FuelTypes,
    IReadOnlyList<string> CapacityUnits,
    IReadOnlyList<string> DimensionUnits,
    IReadOnlyList<string> WeightUnits,
    IReadOnlyList<string> TemperatureUnits,
    IReadOnlyList<string> IpRatings,
    IReadOnlyList<string> NoiseLevelUnits,
    IReadOnlyList<string> LifespanUnits,
    IReadOnlyList<string> OperatingHoursUnits,
    IReadOnlyList<string> OperatingShifts,
    IReadOnlyList<string> Utilizations,
    IReadOnlyList<string> MaintenanceFrequencyUnits,
    IReadOnlyList<string> ComplianceStandards,
    IReadOnlyList<string> ImpactLevels,
    IReadOnlyList<string> CustomFieldTypes);
