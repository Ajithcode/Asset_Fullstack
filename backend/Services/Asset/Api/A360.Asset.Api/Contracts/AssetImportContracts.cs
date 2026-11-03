namespace A360.Asset.Api.Contracts;

/// <summary>One column of the bulk import sheet.</summary>
/// <param name="Header">Column title in row 1; matched ignoring case and a trailing "*".</param>
/// <param name="ErrorKey">The validation key this column's errors come back under, e.g. "Purchase.PurchaseDate".</param>
public sealed record AssetImportColumn(string Header, bool Required, string? Hint = null, string? ErrorKey = null);

/// <summary>Columns of the "Assets" sheet, in template order. Columns can be in any order in an uploaded file.</summary>
public static class AssetImportColumns
{
    public const string SheetName = "Assets";
    public const int MaxRows = 1000;
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    // Basic Information
    public const string AssetName = "Asset Name";
    public const string AssetTag = "Asset Tag";
    public const string Barcode = "Barcode";
    public const string RfidTagId = "RFID Tag ID";
    public const string Model = "Model";
    public const string Manufacturer = "Manufacturer";
    public const string SerialNumber = "Serial Number";
    public const string Status = "Status";
    public const string Condition = "Condition";
    public const string Description = "Description";

    // Classification
    public const string Category = "Category";
    public const string SubCategory = "Sub Category";
    public const string AssetType = "Asset Type";
    public const string AssetSubType = "Asset Sub Type";
    public const string FunctionPurpose = "Function / Purpose";
    public const string Criticality = "Criticality";
    public const string Department = "Department";
    public const string CostCenter = "Cost Center";
    public const string BusinessUnit = "Business Unit";
    public const string TaggingMethod = "Tagging Method";
    public const string ComplianceCategory = "Compliance Category";
    public const string LifecycleStage = "Lifecycle Stage";

    // Location & Assignment
    public const string Country = "Country";
    public const string Site = "Site";
    public const string Building = "Building";
    public const string Floor = "Floor";
    public const string Zone = "Zone";
    public const string Room = "Room";
    public const string CustodianDepartment = "Custodian Department";
    public const string AssignedTo = "Assigned To";
    public const string OwnershipType = "Ownership Type";
    public const string ResponsibleTeam = "Responsible Team";

    // Purchase & Warranty
    public const string PurchaseOrderNumber = "PO Number";
    public const string InvoiceNumber = "Invoice Number";
    public const string SupplierVendor = "Supplier / Vendor";
    public const string PurchaseDate = "Purchase Date";
    public const string DeliveryDate = "Delivery Date";
    public const string PurchaseCost = "Purchase Cost";
    public const string Quantity = "Quantity";
    public const string Currency = "Currency";
    public const string TaxPercent = "Tax %";
    public const string WarrantyType = "Warranty Type";
    public const string WarrantyStartDate = "Warranty Start Date";
    public const string WarrantyPeriod = "Warranty Period";
    public const string WarrantyPeriodUnit = "Warranty Period Unit";
    public const string ServiceProvider = "Service Provider";

    private const string DateHint = "Date, e.g. 2025-01-15";

    public static readonly IReadOnlyList<AssetImportColumn> All =
    [
        new(AssetName, true, ErrorKey: "AssetName"),
        new(AssetTag, true, "Unique across all assets", "AssetTag"),
        new(Barcode, false),
        new(RfidTagId, false),
        new(Model, false),
        new(Manufacturer, true, "Also used as the purchase manufacturer", "Purchase.Manufacturer"),
        new(SerialNumber, false),
        new(Status, true, "e.g. Active, Inactive, Under Maintenance", "Status"),
        new(Condition, false, "e.g. Excellent, Good, Fair, Poor"),
        new(Description, false),

        new(Category, true, ErrorKey: "Category"),
        new(SubCategory, true, ErrorKey: "SubCategory"),
        new(AssetType, true, ErrorKey: "AssetType"),
        new(AssetSubType, false),
        new(FunctionPurpose, false),
        new(Criticality, true, "e.g. Critical, High, Medium, Low", "Criticality"),
        new(Department, true, ErrorKey: "Department"),
        new(CostCenter, false),
        new(BusinessUnit, false),
        new(TaggingMethod, true, "Barcode, RFID, BLE or QR Code", "TaggingMethod"),
        new(ComplianceCategory, false),
        new(LifecycleStage, true, "e.g. In Service, Commissioning", "LifecycleStage"),

        new(Country, true, ErrorKey: "Location.Country"),
        new(Site, true, ErrorKey: "Location.Site"),
        new(Building, true, ErrorKey: "Location.Building"),
        new(Floor, true, ErrorKey: "Location.Floor"),
        new(Zone, false),
        new(Room, false),
        new(CustodianDepartment, true, ErrorKey: "Assignment.CustodianDepartment"),
        new(AssignedTo, false, "Person's name"),
        new(OwnershipType, false, "e.g. Owned, Leased"),
        new(ResponsibleTeam, false),

        new(PurchaseOrderNumber, true, ErrorKey: "Purchase.PurchaseOrderNumber"),
        new(InvoiceNumber, false),
        new(SupplierVendor, true, ErrorKey: "Purchase.SupplierVendor"),
        new(PurchaseDate, true, DateHint, "Purchase.PurchaseDate"),
        new(DeliveryDate, false, DateHint, "Purchase.DeliveryDate"),
        new(PurchaseCost, true, "Number, without tax", "Purchase.PurchaseCost"),
        new(Quantity, true, "Number greater than 0", "Purchase.Quantity"),
        new(Currency, false, "e.g. INR, USD, AED"),
        new(TaxPercent, false, "0 to 100", "Purchase.TaxPercent"),
        new(WarrantyType, false, "e.g. Manufacturer, Extended"),
        new(WarrantyStartDate, false, DateHint, "Warranty.WarrantyStartDate"),
        new(WarrantyPeriod, false, "Whole number", "Warranty.WarrantyPeriod"),
        new(WarrantyPeriodUnit, false, "Day(s), Month(s) or Year(s)", "Warranty.WarrantyPeriodUnit"),
        new(ServiceProvider, false)
    ];

    /// <summary>The column a validation error belongs to, e.g. "Purchase.PurchaseDate" → "Purchase Date".</summary>
    public static string HeaderForErrorKey(string errorKey) =>
        All.FirstOrDefault(column => string.Equals(column.ErrorKey, errorKey, StringComparison.OrdinalIgnoreCase))?.Header ?? errorKey;
}

/// <summary>Example assets written into the downloaded template, one array per row in AssetImportColumns.All order.</summary>
public static class AssetImportSampleData
{
    public static readonly IReadOnlyList<object?[]> Rows =
    [
        ["Diesel Generator 250 kVA", "GEN-20045", "8901234500451", null, "C250 D5", "Cummins", "CMS-250-77812", "Active", "Good", "Standby power for Building A",
         "Electrical", "Power Generation", "Generator", "Diesel Generator", "Backup power", "Critical", "Facilities", "CC-1001", "Operations", "Barcode", "Electrical Safety", "In Service",
         "India", "Head Office", "Building A", "Ground Floor", "Utility Zone", "Generator Room", "Facilities", "Rex Kumar", "Owned", "Maintenance Team",
         "PO-2025-0101", "INV-88101", "PowerGen Solutions", new DateTime(2023, 1, 10), new DateTime(2023, 1, 24), 1850000m, 1m, "INR", 18m, "Manufacturer", new DateTime(2023, 1, 26), 3, "Year(s)", "Cummins Service Center"],
        ["HVAC Chiller", "HVAC-20112", "8901234501125", null, "30XA 252", "Carrier", "CAR-30XA-55120", "Active", "Good", "Main chiller for office cooling",
         "HVAC", "Cooling", "Chiller", "Air-Cooled Chiller", "Comfort cooling", "High", "Facilities", "CC-1002", "Operations", "QR Code", null, "In Service",
         "India", "Head Office", "Building B", "Roof", null, null, "Facilities", "Anita Rao", "Owned", "HVAC Team",
         "PO-2025-0102", "INV-88102", "CoolAir Systems", new DateTime(2022, 6, 5), new DateTime(2022, 6, 20), 4200000m, 1m, "INR", 18m, "Manufacturer", new DateTime(2022, 7, 1), 2, "Year(s)", "Carrier Service"],
        ["UPS 100 kVA", "UPS-20078", "8901234500789", "RFID-UPS-0078", "Galaxy VS 100", "Schneider Electric", "SE-GVS-100-3391", "Active", "Excellent", "Data center UPS",
         "Electrical", "Power Protection", "UPS", "Online UPS", "Uninterrupted power", "Critical", "IT", "CC-2001", "Technology", "RFID", "Electrical Safety", "In Service",
         "India", "Data Center", "Building A", "First Floor", "DC Zone", "DC Room", "IT", "Vikram Shah", "Owned", "IT Infrastructure",
         "PO-2025-0103", "INV-88103", "Schneider Partner Pvt Ltd", new DateTime(2024, 2, 12), new DateTime(2024, 2, 28), 950000m, 1m, "INR", 18m, "Extended", new DateTime(2024, 3, 1), 5, "Year(s)", "Schneider Electric"],
        ["Forklift - Electric", "FLT-20021", "8901234500215", null, "8FBE20", "Toyota", "TYT-8FBE-20981", "Active", "Good", "Warehouse material handling",
         "Material Handling", "Lifting Equipment", "Forklift", "Electric Forklift", "Pallet movement", "Medium", "Warehouse", "CC-3001", "Logistics", "Barcode", null, "In Service",
         "India", "Warehouse", "Main Warehouse", "Ground Floor", "Loading Bay", null, "Warehouse", "Suresh Iyer", "Leased", "Warehouse Team",
         "PO-2025-0104", "INV-88104", "Toyota Material Handling", new DateTime(2023, 9, 18), new DateTime(2023, 9, 30), 1450000m, 1m, "INR", 18m, "Manufacturer", new DateTime(2023, 10, 1), 24, "Month(s)", "Toyota Service"],
        ["Fire Pump", "FP-20056", "8901234500567", null, "NFPA-20 750", "Kirloskar", "KRL-FP-750-1044", "Active", "Good", "Main fire fighting pump",
         "Safety & Security", "Fire Protection", "Pump", "Fire Pump", "Fire fighting", "Critical", "Safety", "CC-4001", "Operations", "QR Code", "Fire Safety", "In Service",
         "India", "Head Office", "Building A", "Basement", "Pump Room", null, "Safety", "Meena Pillai", "Owned", "Safety Team",
         "PO-2025-0105", "INV-88105", "FireSafe Engineering", new DateTime(2022, 11, 2), new DateTime(2022, 11, 15), 780000m, 1m, "INR", 18m, "Manufacturer", new DateTime(2022, 11, 20), 2, "Year(s)", "Kirloskar Service"],
        ["Split AC - 5 Ton", "AC-20311", "8901234503112", null, "FXAQ50", "Daikin", "DKN-FX-50-77120", "Active", "Fair", "Conference room AC",
         "HVAC", "Cooling", "Air Conditioner", "Split AC", "Comfort cooling", "Low", "Facilities", "CC-1002", "Operations", "Barcode", null, "In Service",
         "India", "Head Office", "Building B", "Second Floor", null, "Conference Room 2", "Facilities", null, "Owned", "HVAC Team",
         "PO-2025-0106", "INV-88106", "CoolAir Systems", new DateTime(2024, 4, 8), new DateTime(2024, 4, 15), 165000m, 1m, "INR", 18m, "Manufacturer", new DateTime(2024, 4, 20), 1, "Year(s)", "Daikin Service"],
        ["Laptop - Dell Latitude", "IT-20189", "8901234501897", null, "Latitude 5440", "Dell", "DL-5440-9QX21", "Active", "Good", "Assigned to finance team",
         "IT Equipment", "Computers", "Laptop", "Business Laptop", "End user computing", "Medium", "IT", "CC-2002", "Technology", "Barcode", null, "In Service",
         "India", "Head Office", "Building A", "Third Floor", "IT Department", null, "IT", "Priya Nair", "Owned", "IT Support",
         "PO-2025-0107", "INV-88107", "Dell Technologies", new DateTime(2025, 6, 6), new DateTime(2025, 6, 10), 92000m, 1m, "INR", 18m, "Manufacturer", new DateTime(2025, 6, 10), 3, "Year(s)", "Dell ProSupport"],
        ["CCTV Camera - Dome", "CCTV-20234", "8901234502344", null, "DS-2CD2143", "Hikvision", "HIK-2143-55091", "Active", "Good", "Lobby surveillance camera",
         "Security", "Surveillance", "Camera", "Dome Camera", "Surveillance", "Medium", "Security", "CC-4002", "Operations", "QR Code", null, "In Service",
         "India", "Head Office", "Building A", "Ground Floor", "Lobby", null, "Security", null, "Owned", "Security Team",
         "PO-2025-0108", "INV-88108", "SecureVision Pvt Ltd", new DateTime(2024, 1, 20), new DateTime(2024, 1, 25), 18500m, 1m, "INR", 18m, "Manufacturer", new DateTime(2024, 1, 25), 2, "Year(s)", "SecureVision"],
        ["Transformer 1 MVA", "TRF-20067", "8901234500673", null, "OLTC 1000", "ABB", "ABB-TRF-1000-2210", "Active", "Fair", "Main incoming transformer",
         "Electrical", "Power Distribution", "Transformer", "Oil-Cooled Transformer", "Power distribution", "Critical", "Facilities", "CC-1001", "Operations", "RFID", "Electrical Safety", "In Service",
         "India", "Main Substation", "Substation Block", "Ground Floor", "Outdoor Yard", null, "Facilities", "Rex Kumar", "Owned", "Electrical Team",
         "PO-2025-0109", "INV-88109", "ABB India", new DateTime(2021, 3, 15), new DateTime(2021, 4, 2), 3600000m, 1m, "INR", 18m, "Manufacturer", new DateTime(2021, 4, 5), 5, "Year(s)", "ABB Service"],
        ["Water Treatment Plant", "WTP-20012", "8901234500123", null, "RO-5000", "Ion Exchange", "IEX-RO-5000-118", "Active", "Good", "RO plant for process water",
         "Utilities", "Water Treatment", "RO Plant", null, "Process water", "High", "Facilities", "CC-1003", "Operations", "Barcode", "Environmental", "In Service",
         "India", "Plant", "Utility Block", "Ground Floor", null, null, "Facilities", null, "Owned", "Utilities Team",
         "PO-2025-0110", "INV-88110", "Ion Exchange India", new DateTime(2023, 7, 1), new DateTime(2023, 7, 20), 2250000m, 1m, "INR", 18m, "Manufacturer", new DateTime(2023, 8, 1), 2, "Year(s)", "Ion Exchange Service"],
        ["Air Compressor 50 HP", "CMP-20033", "8901234500338", null, "GA37", "Atlas Copco", "AC-GA37-60341", "Under Maintenance", "Poor", "Compressed air for production line",
         "Mechanical", "Compressed Air", "Compressor", "Screw Compressor", "Compressed air supply", "High", "Production", "CC-5001", "Manufacturing", "QR Code", null, "In Service",
         "India", "Plant", "Production Block", "Ground Floor", "Compressor Bay", null, "Production", "Arun Das", "Owned", "Maintenance Team",
         "PO-2025-0111", "INV-88111", "Atlas Copco India", new DateTime(2020, 10, 12), new DateTime(2020, 10, 30), 1250000m, 1m, "INR", 18m, "Manufacturer", new DateTime(2020, 11, 1), 3, "Year(s)", "Atlas Copco Service"],
        ["Passenger Elevator", "ELV-20004", "8901234500048", null, "Gen2 MRL", "Otis", "OTS-GEN2-88213", "Active", "Good", "Main passenger lift",
         "Vertical Transport", "Elevators", "Elevator", "Passenger Elevator", "People movement", "High", "Facilities", "CC-1004", "Operations", "Barcode", "Lift Safety", "In Service",
         "India", "Head Office", "Building B", "Ground Floor", "Lift Lobby", null, "Facilities", null, "Owned", "Facilities Team",
         "PO-2025-0112", "INV-88112", "Otis Elevator Company", new DateTime(2022, 2, 14), new DateTime(2022, 3, 30), 3200000m, 1m, "INR", 18m, "Extended", new DateTime(2022, 4, 1), 5, "Year(s)", "Otis Service"]
    ];
}

/// <summary>Outcome of one data row of the uploaded sheet.</summary>
/// <param name="RowNumber">The Excel row number, so users can find it in their file.</param>
/// <param name="Result">"Imported", "Valid" (checked only, not saved) or "Failed".</param>
/// <param name="AssetRegistrationId">Set when the row was imported.</param>
public sealed record AssetImportRowResult(
    int RowNumber,
    string? AssetName,
    string? AssetTag,
    string Result,
    string? AssetRegistrationId,
    IReadOnlyCollection<AssetImportRowError> Errors);

/// <param name="Column">The sheet column the error is about.</param>
public sealed record AssetImportRowError(string Column, string Message);

/// <param name="ValidateOnly">True when the file was only checked and nothing was saved.</param>
public sealed record AssetImportResponse(
    string FileName,
    bool ValidateOnly,
    int TotalRows,
    int ImportedCount,
    int ValidCount,
    int FailedCount,
    IReadOnlyCollection<AssetImportRowResult> Rows);

public static class AssetImportResults
{
    public const string Imported = "Imported";
    public const string Valid = "Valid";
    public const string Failed = "Failed";
}
