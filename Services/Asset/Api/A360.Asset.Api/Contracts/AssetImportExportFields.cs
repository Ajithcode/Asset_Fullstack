using A360.Asset.Domain.Entities;

namespace A360.Asset.Api.Contracts;

/// <summary>Import types on the Import Assets panel.</summary>
public static class AssetImportTypes
{
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Upsert = "Upsert";

    private static readonly OptionList List = new(Create, Update, Upsert);

    public static IReadOnlyList<string> All => List.All;

    public static string? Normalize(string? value) => List.Normalize(value);
}

/// <summary>Data types shown in the Field Mapping table; values are checked and converted by type.</summary>
public static class AssetImportDataTypes
{
    public const string Text = "Text";
    public const string Number = "Number";
    public const string Integer = "Integer";
    public const string Date = "Date";
    public const string Lookup = "Lookup";
}

/// <summary>One Asset360 field that a file column can be mapped to, or that can be exported.</summary>
/// <param name="Key">Stable key used by the API, e.g. "purchaseDate".</param>
/// <param name="Label">Column title in templates and exports; also matched when auto mapping.</param>
/// <param name="Group">Section of the registration wizard the field belongs to.</param>
/// <param name="RequiredForCreate">Must be mapped (and filled) to create a new asset.</param>
/// <param name="Importable">False for values that are calculated or set by the system; those are only exported.</param>
/// <param name="Standard">Included in "Standard Fields (Recommended)" exports and the basic template.</param>
/// <param name="ErrorKey">The key the registration validation reports this field's errors under, e.g. "Purchase.PurchaseDate".</param>
/// <param name="Options">For lookups with a fixed list, the listed values.</param>
public sealed record AssetImportField(
    string Key,
    string Label,
    string Group,
    string DataType,
    bool RequiredForCreate,
    bool Importable,
    bool Standard,
    string? Hint,
    string? ErrorKey,
    IReadOnlyCollection<string> Aliases,
    OptionList? Options,
    Func<AssetRegistration, object?> Get,
    Action<AssetRegistration, object?>? Set)
{
    /// <summary>Mandatory in the Field Mapping table for the given import type.</summary>
    public bool IsMandatory(string importType) => importType switch
    {
        AssetImportTypes.Update => Key == AssetImportFields.AssetId,
        _ => RequiredForCreate
    };
}

/// <summary>
/// The Asset360 fields of the Bulk Import / Export screen, in template and export column order.
/// Labels match the older single-step import (<see cref="AssetImportColumns"/>), so its files map automatically.
/// </summary>
public static class AssetImportExportFields
{
    public const string SheetName = "Assets";
    public const int MaxRows = 1000;
    public const int MaxColumns = 150;
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private const string BasicInformation = "Basic Information";
    private const string Classification = "Classification";
    private const string LocationAssignment = "Location & Assignment";
    private const string PurchaseWarranty = "Purchase & Warranty";
    private const string Maintenance = "Maintenance & Technical";
    private const string SystemGroup = "System";

    private const string DateHint = "Date, e.g. 2025-01-15";

    public static readonly IReadOnlyList<AssetImportField> All =
    [
        Field(AssetImportFields.AssetId, "Asset ID", SystemGroup, AssetImportDataTypes.Text, required: false, standard: true,
            hint: "Asset360 ID, e.g. AR000123. Used to find the asset when updating; leave blank for new assets",
            aliases: ["assetregistrationid", "registrationid", "assetno", "assetnumber", "id"],
            get: asset => asset.AssetRegistrationId, set: null, importable: true),

        // Basic Information
        Text("assetName", "Asset Name", BasicInformation, required: true, standard: true, errorKey: "AssetName",
            aliases: ["name", "asset"], get: a => a.BasicInformation.AssetName, set: (a, v) => a.BasicInformation.AssetName = v),
        Text(AssetImportFields.AssetTag, "Asset Tag", BasicInformation, required: true, standard: true, errorKey: "AssetTag",
            hint: "Unique across all assets", aliases: ["tag", "tagno", "tagnumber", "assettagno", "assettagnumber"],
            get: a => a.BasicInformation.AssetTag, set: (a, v) => a.BasicInformation.AssetTag = v),
        Text("barcode", "Barcode", BasicInformation, standard: false, get: a => a.BasicInformation.Barcode, set: (a, v) => a.BasicInformation.Barcode = v),
        Text("rfidTagId", "RFID Tag ID", BasicInformation, standard: false, aliases: ["rfid", "rfidtag"],
            get: a => a.BasicInformation.RfidTagId, set: (a, v) => a.BasicInformation.RfidTagId = v),
        Text("model", "Model", BasicInformation, standard: true, aliases: ["modelno", "modelnumber"],
            get: a => a.BasicInformation.Model, set: (a, v) => a.BasicInformation.Model = v),
        Text("manufacturer", "Manufacturer", BasicInformation, required: true, standard: true, errorKey: "Purchase.Manufacturer",
            hint: "Also used as the purchase manufacturer", aliases: ["make", "brand", "oem"],
            get: a => a.BasicInformation.Manufacturer ?? a.PurchaseWarranty.Purchase.Manufacturer,
            set: (a, v) => { a.BasicInformation.Manufacturer = v; a.PurchaseWarranty.Purchase.Manufacturer = v; }),
        Text("serialNumber", "Serial Number", BasicInformation, standard: true, aliases: ["serialno", "serial", "sno", "slno"],
            get: a => a.BasicInformation.SerialNumber, set: (a, v) => a.BasicInformation.SerialNumber = v),
        Lookup("status", "Status", BasicInformation, AssetRegistrationOptions.Statuses, required: true, standard: true, errorKey: "Status",
            aliases: ["assetstatus"], get: a => a.BasicInformation.Status, set: (a, v) => a.BasicInformation.Status = v),
        Lookup("condition", "Condition", BasicInformation, null, standard: true, hint: "e.g. Excellent, Good, Fair, Poor",
            aliases: ["assetcondition"], get: a => a.BasicInformation.Condition, set: (a, v) => a.BasicInformation.Condition = v),
        Text("description", "Description", BasicInformation, standard: false, aliases: ["remarks", "details"],
            get: a => a.BasicInformation.Description, set: (a, v) => a.BasicInformation.Description = v),

        // Classification
        Lookup("category", "Category", Classification, null, required: true, standard: true, errorKey: "Category",
            aliases: ["assetcategory"], get: a => a.Classification.Category, set: (a, v) => a.Classification.Category = v),
        Lookup("subCategory", "Sub Category", Classification, null, required: true, standard: true, errorKey: "SubCategory",
            aliases: ["subcat", "assetsubcategory"], get: a => a.Classification.SubCategory, set: (a, v) => a.Classification.SubCategory = v),
        Lookup("assetType", "Asset Type", Classification, null, required: true, standard: true, errorKey: "AssetType",
            aliases: ["type"], get: a => a.Classification.AssetType, set: (a, v) => a.Classification.AssetType = v),
        Lookup("assetSubType", "Asset Sub Type", Classification, null, standard: false, aliases: ["subtype"],
            get: a => a.Classification.AssetSubType, set: (a, v) => a.Classification.AssetSubType = v),
        Lookup("functionPurpose", "Function / Purpose", Classification, AssetRegistrationOptions.FunctionPurposes, standard: false,
            aliases: ["function", "purpose"], get: a => a.Classification.FunctionPurpose, set: (a, v) => a.Classification.FunctionPurpose = v),
        Lookup("criticality", "Criticality", Classification, null, required: true, standard: true, errorKey: "Criticality",
            hint: "e.g. Critical, High, Medium, Low", aliases: ["criticalitylevel"],
            get: a => a.Classification.Criticality ?? a.BasicInformation.Criticality,
            set: (a, v) => { a.Classification.Criticality = v; a.BasicInformation.Criticality = v; }),
        Lookup("department", "Department", Classification, null, required: true, standard: true, errorKey: "Department",
            aliases: ["dept"], get: a => a.Classification.Department, set: (a, v) => a.Classification.Department = v),
        Text("costCenter", "Cost Center", Classification, standard: false, aliases: ["costcentre", "cc"],
            get: a => a.Classification.CostCenter, set: (a, v) => a.Classification.CostCenter = v),
        Text("businessUnit", "Business Unit", Classification, standard: false, aliases: ["bu"],
            get: a => a.Classification.BusinessUnit, set: (a, v) => a.Classification.BusinessUnit = v),
        Lookup("taggingMethod", "Tagging Method", Classification, AssetRegistrationOptions.TaggingMethods, required: true, standard: false,
            errorKey: "TaggingMethod", get: a => a.Classification.TaggingMethod, set: (a, v) => a.Classification.TaggingMethod = v),
        Lookup("complianceCategory", "Compliance Category", Classification, AssetRegistrationOptions.ComplianceCategories, standard: false,
            aliases: ["compliance"], get: a => a.Classification.ComplianceCategory, set: (a, v) => a.Classification.ComplianceCategory = v),
        Lookup("lifecycleStage", "Lifecycle Stage", Classification, AssetRegistrationOptions.LifecycleStages, required: true, standard: false,
            errorKey: "LifecycleStage", aliases: ["lifecycle"], get: a => a.Classification.LifecycleStage, set: (a, v) => a.Classification.LifecycleStage = v),

        // Location & Assignment. A sheet holds level names, not hierarchy ids; a changed name clears that level's id.
        Text("country", "Country", LocationAssignment, required: true, standard: false,
            get: a => a.LocationAssignment.Location.CountryName,
            set: (a, v) => SetLevel(a.LocationAssignment.Location, l => l.CountryName, (l, n) => { l.CountryName = n; l.CountryId = null; }, v)),
        Text("site", "Site", LocationAssignment, required: true, standard: true, aliases: ["location", "sitename"],
            get: a => a.LocationAssignment.Location.SiteName,
            set: (a, v) => SetLevel(a.LocationAssignment.Location, l => l.SiteName, (l, n) => { l.SiteName = n; l.SiteId = null; }, v)),
        Text("building", "Building", LocationAssignment, required: true, standard: true,
            get: a => a.LocationAssignment.Location.BuildingName,
            set: (a, v) => SetLevel(a.LocationAssignment.Location, l => l.BuildingName, (l, n) => { l.BuildingName = n; l.BuildingId = null; }, v)),
        Text("floor", "Floor", LocationAssignment, required: true, standard: true,
            get: a => a.LocationAssignment.Location.FloorName,
            set: (a, v) => SetLevel(a.LocationAssignment.Location, l => l.FloorName, (l, n) => { l.FloorName = n; l.FloorId = null; }, v)),
        Text("zone", "Zone", LocationAssignment, standard: false, aliases: ["area"],
            get: a => a.LocationAssignment.Location.ZoneName,
            set: (a, v) => SetLevel(a.LocationAssignment.Location, l => l.ZoneName, (l, n) => { l.ZoneName = n; l.ZoneId = null; }, v)),
        Text("room", "Room", LocationAssignment, standard: false,
            get: a => a.LocationAssignment.Location.RoomName,
            set: (a, v) => SetLevel(a.LocationAssignment.Location, l => l.RoomName, (l, n) => { l.RoomName = n; l.RoomId = null; }, v)),
        Text("custodianDepartment", "Custodian Department", LocationAssignment, required: true, standard: false,
            errorKey: "Assignment.CustodianDepartment", aliases: ["custodiandept"],
            get: a => a.LocationAssignment.Assignment.CustodianDepartment, set: (a, v) => a.LocationAssignment.Assignment.CustodianDepartment = v),
        Text("assignedTo", "Assigned To", LocationAssignment, standard: true, hint: "Person's name", aliases: ["custodian", "assignee", "owner"],
            get: a => a.LocationAssignment.Assignment.AssignedToName,
            set: (a, v) =>
            {
                var assignment = a.LocationAssignment.Assignment;
                if (!SameText(assignment.AssignedToName, v))
                {
                    assignment.AssignedToName = v;
                    assignment.AssignedToUserId = null;
                }
            }),
        Lookup("ownershipType", "Ownership Type", LocationAssignment, AssetRegistrationOptions.OwnershipTypes, standard: false,
            aliases: ["ownership"], get: a => a.LocationAssignment.Assignment.OwnershipType, set: (a, v) => a.LocationAssignment.Assignment.OwnershipType = v),
        Text("responsibleTeam", "Responsible Team", LocationAssignment, standard: false, aliases: ["team"],
            get: a => a.LocationAssignment.Assignment.ResponsibleTeam, set: (a, v) => a.LocationAssignment.Assignment.ResponsibleTeam = v),

        // Purchase & Warranty
        Text("purchaseOrderNumber", "PO Number", PurchaseWarranty, required: true, standard: false, errorKey: "Purchase.PurchaseOrderNumber",
            aliases: ["po", "pono", "purchaseorder", "purchaseorderno", "purchaseordernumber"],
            get: a => a.PurchaseWarranty.Purchase.PurchaseOrderNumber, set: (a, v) => a.PurchaseWarranty.Purchase.PurchaseOrderNumber = v),
        Text("invoiceNumber", "Invoice Number", PurchaseWarranty, standard: false, aliases: ["invoice", "invoiceno"],
            get: a => a.PurchaseWarranty.Purchase.InvoiceNumber, set: (a, v) => a.PurchaseWarranty.Purchase.InvoiceNumber = v),
        Text("supplierVendor", "Supplier / Vendor", PurchaseWarranty, required: true, standard: false, errorKey: "Purchase.SupplierVendor",
            aliases: ["supplier", "vendor", "suppliername", "vendorname"],
            get: a => a.PurchaseWarranty.Purchase.SupplierVendor, set: (a, v) => a.PurchaseWarranty.Purchase.SupplierVendor = v),
        Date("purchaseDate", "Purchase Date", PurchaseWarranty, required: true, standard: true, errorKey: "Purchase.PurchaseDate",
            aliases: ["dateofpurchase", "acquisitiondate", "purchasedon"],
            get: a => a.PurchaseWarranty.Purchase.PurchaseDate, set: (a, v) => a.PurchaseWarranty.Purchase.PurchaseDate = v),
        Date("deliveryDate", "Delivery Date", PurchaseWarranty, standard: false, errorKey: "Purchase.DeliveryDate",
            get: a => a.PurchaseWarranty.Purchase.DeliveryDate, set: (a, v) => a.PurchaseWarranty.Purchase.DeliveryDate = v),
        Number("purchaseCost", "Purchase Cost", PurchaseWarranty, required: true, standard: true, errorKey: "Purchase.PurchaseCost",
            hint: "Number, without tax", aliases: ["cost", "price", "amount", "purchaseprice", "purchasevalue"],
            get: a => a.PurchaseWarranty.Purchase.PurchaseCost, set: (a, v) => a.PurchaseWarranty.Purchase.PurchaseCost = v),
        Number("quantity", "Quantity", PurchaseWarranty, required: true, standard: false, errorKey: "Purchase.Quantity",
            hint: "Number greater than 0", aliases: ["qty"],
            get: a => a.PurchaseWarranty.Purchase.Quantity, set: (a, v) => a.PurchaseWarranty.Purchase.Quantity = v),
        Lookup("currency", "Currency", PurchaseWarranty, null, standard: false, hint: "e.g. INR, USD, AED",
            get: a => a.PurchaseWarranty.Purchase.Currency, set: (a, v) => a.PurchaseWarranty.Purchase.Currency = v),
        Number("taxPercent", "Tax %", PurchaseWarranty, standard: false, errorKey: "Purchase.TaxPercent", hint: "0 to 100",
            aliases: ["tax", "taxpercentage", "gst", "vat"],
            get: a => a.PurchaseWarranty.Purchase.TaxPercent, set: (a, v) => a.PurchaseWarranty.Purchase.TaxPercent = v),
        ExportOnly("totalCost", "Total Cost", PurchaseWarranty, AssetImportDataTypes.Number, standard: false,
            get: a => a.PurchaseWarranty.Purchase.TotalCost),
        Lookup("warrantyType", "Warranty Type", PurchaseWarranty, null, standard: false, hint: "e.g. Manufacturer, Extended",
            get: a => a.PurchaseWarranty.Warranty.WarrantyType, set: (a, v) => a.PurchaseWarranty.Warranty.WarrantyType = v),
        Date("warrantyStartDate", "Warranty Start Date", PurchaseWarranty, standard: false, errorKey: "Warranty.WarrantyStartDate",
            get: a => a.PurchaseWarranty.Warranty.WarrantyStartDate, set: (a, v) => a.PurchaseWarranty.Warranty.WarrantyStartDate = v),
        Integer("warrantyPeriod", "Warranty Period", PurchaseWarranty, standard: false, errorKey: "Warranty.WarrantyPeriod",
            get: a => a.PurchaseWarranty.Warranty.WarrantyPeriod, set: (a, v) => a.PurchaseWarranty.Warranty.WarrantyPeriod = v),
        Lookup("warrantyPeriodUnit", "Warranty Period Unit", PurchaseWarranty, null, standard: false, errorKey: "Warranty.WarrantyPeriodUnit",
            hint: "Day(s), Month(s) or Year(s)", get: a => a.PurchaseWarranty.Warranty.WarrantyPeriodUnit,
            set: (a, v) => a.PurchaseWarranty.Warranty.WarrantyPeriodUnit = v),
        ExportOnly("warrantyEndDate", "Warranty End Date", PurchaseWarranty, AssetImportDataTypes.Date, standard: true,
            get: a => a.PurchaseWarranty.Warranty.WarrantyEndDate),
        Text("serviceProvider", "Service Provider", PurchaseWarranty, standard: false,
            get: a => a.PurchaseWarranty.Warranty.ServiceProvider, set: (a, v) => a.PurchaseWarranty.Warranty.ServiceProvider = v),

        // Maintenance & Technical
        Date("lastMaintenanceDate", "Last Maintenance Date", Maintenance, standard: false,
            get: a => a.AdditionalInformation.Operational.LastMaintenanceDate, set: (a, v) => a.AdditionalInformation.Operational.LastMaintenanceDate = v),
        Date("nextMaintenanceDate", "Next Maintenance Date", Maintenance, standard: true, aliases: ["nextmaintenance", "nextservicedate"],
            get: a => a.AdditionalInformation.Operational.NextMaintenanceDate, set: (a, v) => a.AdditionalInformation.Operational.NextMaintenanceDate = v),
        Integer("expectedLifespan", "Expected Lifespan", Maintenance, standard: false, aliases: ["usefullife", "lifespan"],
            get: a => a.AdditionalInformation.Technical.ExpectedLifespan, set: (a, v) => a.AdditionalInformation.Technical.ExpectedLifespan = v),
        Lookup("expectedLifespanUnit", "Expected Lifespan Unit", Maintenance, null, standard: false, hint: "e.g. Year(s)",
            get: a => a.AdditionalInformation.Technical.ExpectedLifespanUnit, set: (a, v) => a.AdditionalInformation.Technical.ExpectedLifespanUnit = v),

        // System
        ExportOnly("locationPath", "Location Path", SystemGroup, AssetImportDataTypes.Text, standard: true,
            get: a => a.LocationAssignment.Location.LocationPath),
        ExportOnly("createdBy", "Created By", SystemGroup, AssetImportDataTypes.Text, standard: false, get: a => a.CreatedBy),
        ExportOnly("createdAt", "Created On", SystemGroup, AssetImportDataTypes.Date, standard: false, get: a => a.CreatedAt),
        ExportOnly("updatedAt", "Last Updated On", SystemGroup, AssetImportDataTypes.Date, standard: false, get: a => a.UpdatedAt)
    ];

    private static readonly Dictionary<string, AssetImportField> ByKey = All.ToDictionary(field => field.Key, StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<AssetImportField> Importable => All.Where(field => field.Importable);

    public static AssetImportField? Find(string? key) =>
        key is not null && ByKey.TryGetValue(key.Trim(), out var field) ? field : null;

    /// <summary>The field a registration validation key belongs to, e.g. "Purchase.PurchaseDate" → purchaseDate.</summary>
    public static AssetImportField? FindByErrorKey(string errorKey) =>
        All.FirstOrDefault(field => string.Equals(field.ErrorKey, errorKey, StringComparison.OrdinalIgnoreCase));

    /// <summary>Lower case letters and digits only, so "Asset Tag *", "asset_tag" and "AssetTag" all compare equal.</summary>
    public static string NormalizeHeader(string? header) =>
        new((header ?? string.Empty).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static AssetImportField Field(
        string key, string label, string group, string dataType, bool required, bool standard, string? hint,
        IReadOnlyCollection<string> aliases, Func<AssetRegistration, object?> get, Action<AssetRegistration, object?>? set,
        bool importable, string? errorKey = null, OptionList? options = null) =>
        new(key, label, group, dataType, required, importable, standard, hint, errorKey,
            aliases.Select(NormalizeHeader).ToList(), options, get, set);

    private static AssetImportField Text(
        string key, string label, string group, Func<AssetRegistration, string?> get, Action<AssetRegistration, string?> set,
        bool required = false, bool standard = false, string? errorKey = null, string? hint = null, IReadOnlyCollection<string>? aliases = null) =>
        Field(key, label, group, AssetImportDataTypes.Text, required, standard, hint, aliases ?? [], get, (a, v) => set(a, (string?)v),
            importable: true, errorKey);

    private static AssetImportField Lookup(
        string key, string label, string group, OptionList? options, Func<AssetRegistration, string?> get, Action<AssetRegistration, string?> set,
        bool required = false, bool standard = false, string? errorKey = null, string? hint = null, IReadOnlyCollection<string>? aliases = null) =>
        Field(key, label, group, AssetImportDataTypes.Lookup, required, standard, hint ?? (options is null ? null : string.Join(", ", options.All)),
            aliases ?? [], get, (a, v) => set(a, (string?)v), importable: true, errorKey, options);

    private static AssetImportField Date(
        string key, string label, string group, Func<AssetRegistration, DateTime?> get, Action<AssetRegistration, DateTime?> set,
        bool required = false, bool standard = false, string? errorKey = null, IReadOnlyCollection<string>? aliases = null) =>
        Field(key, label, group, AssetImportDataTypes.Date, required, standard, DateHint, aliases ?? [], a => get(a), (a, v) => set(a, (DateTime?)v),
            importable: true, errorKey);

    private static AssetImportField Number(
        string key, string label, string group, Func<AssetRegistration, decimal?> get, Action<AssetRegistration, decimal?> set,
        bool required = false, bool standard = false, string? errorKey = null, string? hint = null, IReadOnlyCollection<string>? aliases = null) =>
        Field(key, label, group, AssetImportDataTypes.Number, required, standard, hint ?? "Number", aliases ?? [], a => get(a), (a, v) => set(a, (decimal?)v),
            importable: true, errorKey);

    private static AssetImportField Integer(
        string key, string label, string group, Func<AssetRegistration, int?> get, Action<AssetRegistration, int?> set,
        bool standard = false, string? errorKey = null, IReadOnlyCollection<string>? aliases = null) =>
        Field(key, label, group, AssetImportDataTypes.Integer, required: false, standard, "Whole number", aliases ?? [], a => get(a), (a, v) => set(a, (int?)v),
            importable: true, errorKey);

    private static AssetImportField ExportOnly(string key, string label, string group, string dataType, bool standard, Func<AssetRegistration, object?> get) =>
        Field(key, label, group, dataType, required: false, standard, hint: "Calculated by Asset360; ignored on import", aliases: [], get, set: null, importable: false);

    private static void SetLevel(AssetLocation location, Func<AssetLocation, string?> current, Action<AssetLocation, string?> set, object? value)
    {
        var name = (string?)value;
        if (!SameText(current(location), name))
        {
            set(location, name);
        }
    }

    private static bool SameText(string? left, object? right) =>
        string.Equals(left?.Trim(), (right as string)?.Trim(), StringComparison.OrdinalIgnoreCase);
}

/// <summary>Keys of the fields the import logic itself relies on.</summary>
public static class AssetImportFields
{
    public const string AssetId = "assetId";
    public const string AssetTag = "assetTag";
}
