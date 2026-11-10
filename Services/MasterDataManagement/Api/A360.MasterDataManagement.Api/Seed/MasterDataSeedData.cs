using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;
using A360.Repository.Repositories;
using ServiceProvider = A360.MasterDataManagement.Domain.Entities.ServiceProvider;

namespace A360.MasterDataManagement.Api.Seed;

/// <summary>
/// Sample master data: an organization → business unit → department → site → building → floor → zone
/// hierarchy, a matching location tree, employees, and an asset catalog (categories, sub-categories and
/// asset types) and criticality levels. Names line up with the Maintenance seed (sites, buildings, areas, departments, asset types,
/// technicians) so records from both services read as one dataset. Ids are generated up front so every
/// cross-reference (and its copied name) is filled in on insert.
/// </summary>
public static class MasterDataSeedData
{
    public const string ClientId = "CLT-1001";
    public const string TenantId = "TENANT-DEFAULT";
    private const string SeedUser = "Rex Kumar";
    private const string Active = "Active";

    private static T Audit<T>(T entity, DateTime createdAt)
        where T : BaseEntity
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            entity.Id = MongoObjectId.Create();
        }

        entity.ClientId = ClientId;
        entity.TenantId = TenantId;
        entity.CreatedBy = SeedUser;
        entity.CreatedAt = createdAt;
        entity.Status = Active;
        entity.IsDeleted = false;
        return entity;
    }

    private static MasterDataReference Ref(string id, string name) => new() { Id = id, Name = name };

    // ------------------------------------------------------------------ organization / location hierarchy

    public sealed class OrganizationSet
    {
        public List<Organization> Organizations { get; } = [];
        public List<BusinessUnit> BusinessUnits { get; } = [];
        public List<Department> Departments { get; } = [];
        public List<Site> Sites { get; } = [];
        public List<Building> Buildings { get; } = [];
        public List<Floor> Floors { get; } = [];
        public List<Zone> Zones { get; } = [];
        public List<Location> Locations { get; } = [];
        public List<Employee> Employees { get; } = [];
    }

    private sealed record DepartmentSeed(string Code, string Name, string BusinessUnitCode, string HeadEmployeeId);

    private sealed record SiteSeed(string Code, string Name, string SiteType, string City, string Country, string BusinessUnitCode, string DepartmentCode);

    private sealed record BuildingSeed(string Code, string Name, string SiteCode, string Description, int Capacity, double Area, string[] Floors);

    private sealed record ZoneSeed(string Code, string Name, string BuildingCode, string? FloorName, string Description, int Capacity, double Area);

    private sealed record EmployeeSeed(string EmployeeId, string Name, string DepartmentCode, string Designation, string Phone);

    private static readonly (string Code, string Name)[] BusinessUnitSeeds =
    [
        ("BU-OPS", "Operations"),
        ("BU-CORP", "Corporate Services")
    ];

    private static readonly DepartmentSeed[] DepartmentSeeds =
    [
        new("DEPT-FAC", "Facilities", "BU-OPS", "EMP-0002"),
        new("DEPT-MNT", "Maintenance", "BU-OPS", "EMP-0004"),
        new("DEPT-ENG", "Engineering", "BU-OPS", "EMP-0006"),
        new("DEPT-LOG", "Logistics", "BU-OPS", "EMP-0009"),
        new("DEPT-SAF", "Safety", "BU-OPS", "EMP-0011"),
        new("DEPT-IT", "IT", "BU-CORP", "EMP-0012"),
        new("DEPT-SEC", "Security", "BU-CORP", "EMP-0014"),
        new("DEPT-ADM", "Administration", "BU-CORP", "EMP-0001")
    ];

    private static readonly SiteSeed[] SiteSeeds =
    [
        new("SITE-MP", "Main Plant", "Industrial", "Dubai", "United Arab Emirates", "BU-OPS", "DEPT-FAC"),
        new("SITE-HO", "Head Office", "Office", "Dubai", "United Arab Emirates", "BU-CORP", "DEPT-ADM"),
        new("SITE-WH", "Warehouse", "Warehouse", "Jebel Ali", "United Arab Emirates", "BU-OPS", "DEPT-LOG")
    ];

    private static readonly BuildingSeed[] BuildingSeeds =
    [
        new("BLD-MP-A", "Block A", "SITE-MP", "Production and generator block", 250, 5200, ["Ground Floor", "First Floor", "Roof"]),
        new("BLD-MP-B", "Block B", "SITE-MP", "Process block with chiller yard", 200, 4600, ["Ground Floor", "First Floor"]),
        new("BLD-MP-UT", "Utility Block", "SITE-MP", "Pumps and utility services", 30, 900, ["Ground Floor"]),
        new("BLD-MP-MB", "Maintenance Block", "SITE-MP", "Workshops and compressor room", 60, 1400, ["Ground Floor"]),
        new("BLD-MP-SS", "Substation", "SITE-MP", "HV/LV substation and outdoor plant", 10, 600, ["Ground Floor"]),
        new("BLD-HO-MAIN", "Main Building", "SITE-HO", "Reception, security and executive offices", 400, 8000, ["Ground Floor", "First Floor", "Second Floor"]),
        new("BLD-HO-A", "Building A", "SITE-HO", "Office wing A", 300, 6000, ["Ground Floor", "First Floor", "Level 3"]),
        new("BLD-HO-B", "Building B", "SITE-HO", "Office wing B", 300, 6000, ["Ground Floor", "First Floor", "Roof"]),
        new("BLD-HO-DC", "Data Centre", "SITE-HO", "Tier III data centre", 25, 1200, ["Ground Floor"]),
        new("BLD-WH-MAIN", "Main Warehouse", "SITE-WH", "Racked storage and dispatch", 80, 12000, ["Ground Floor", "Mezzanine"])
    ];

    private static readonly ZoneSeed[] ZoneSeeds =
    [
        new("ZN-MP-GEN1", "Generator Room - G1", "BLD-MP-A", "Ground Floor", "Standby generator room", 6, 180),
        new("ZN-MP-A-ROOF", "Roof", "BLD-MP-A", "Roof", "Rooftop HVAC plant", 4, 900),
        new("ZN-MP-CHY", "Chiller Yard", "BLD-MP-B", "Ground Floor", "Chiller plant yard", 6, 650),
        new("ZN-MP-UTL", "Utility Area", "BLD-MP-UT", "Ground Floor", "Fire and water pumps", 6, 320),
        new("ZN-MP-CMP", "Compressor Room", "BLD-MP-MB", "Ground Floor", "Compressed air plant", 4, 140),
        new("ZN-MP-OUT", "Outdoor", "BLD-MP-SS", null, "Outdoor generator and transformer pad", 6, 450),
        new("ZN-HO-ENT", "Main Entrance", "BLD-HO-MAIN", "Ground Floor", "Lobby and entrance gates", 50, 300),
        new("ZN-HO-SEC", "Security Room", "BLD-HO-MAIN", "Ground Floor", "Security control room", 6, 60),
        new("ZN-HO-LIFT", "Lift Core A", "BLD-HO-MAIN", null, "Passenger lift shaft and machine room", 2, 40),
        new("ZN-HO-ITR", "IT Room", "BLD-HO-A", "First Floor", "Floor IT and comms room", 4, 45),
        new("ZN-HO-PLT", "Plant Room - L3", "BLD-HO-A", "Level 3", "Air handling plant room", 4, 220),
        new("ZN-HO-B-ROOF", "Roof", "BLD-HO-B", "Roof", "Rooftop HVAC units", 4, 800),
        new("ZN-HO-RKB4", "Rack B-04", "BLD-HO-DC", "Ground Floor", "Network rack row B", 2, 12),
        new("ZN-HO-UPS", "UPS Room", "BLD-HO-DC", "Ground Floor", "UPS and battery room", 3, 70),
        new("ZN-WH-LB2", "Loading Bay 2", "BLD-WH-MAIN", "Ground Floor", "Dispatch loading bay", 15, 600)
    ];

    private static readonly EmployeeSeed[] EmployeeSeeds =
    [
        new("EMP-0001", "Rex Kumar", "DEPT-ADM", "Asset Manager", "+971 50 100 0001"),
        new("EMP-0002", "Fatima Al Hashimi", "DEPT-FAC", "Facilities Manager", "+971 50 100 0002"),
        new("EMP-0003", "Ahmed Khan", "DEPT-FAC", "HVAC Technician", "+971 50 100 0003"),
        new("EMP-0004", "Priya Nair", "DEPT-MNT", "Maintenance Supervisor", "+971 50 100 0004"),
        new("EMP-0005", "Ramesh Kumar", "DEPT-MNT", "Mechanical Technician", "+971 50 100 0005"),
        new("EMP-0006", "Salim Ahmed", "DEPT-ENG", "Electrical Engineer", "+971 50 100 0006"),
        new("EMP-0007", "Nisha Patel", "DEPT-MNT", "Reliability Engineer", "+971 50 100 0007"),
        new("EMP-0008", "Khalid Mansoori", "DEPT-ENG", "Electrical Technician", "+971 50 100 0008"),
        new("EMP-0009", "Arjun Mehta", "DEPT-LOG", "Logistics Supervisor", "+971 50 100 0009"),
        new("EMP-0010", "Joseph Mathew", "DEPT-LOG", "Forklift Operator", "+971 50 100 0010"),
        new("EMP-0011", "Omar Farooq", "DEPT-SAF", "HSE Officer", "+971 50 100 0011"),
        new("EMP-0012", "Sneha Iyer", "DEPT-IT", "IT Manager", "+971 50 100 0012"),
        new("EMP-0013", "Daniel Thomas", "DEPT-IT", "Network Engineer", "+971 50 100 0013"),
        new("EMP-0014", "Yusuf Rahman", "DEPT-SEC", "Security Supervisor", "+971 50 100 0014"),
        new("EMP-0015", "Meera Pillai", "DEPT-ADM", "Admin Executive", "+971 50 100 0015")
    ];

    public static OrganizationSet Organization(DateTime now)
    {
        var set = new OrganizationSet();

        var organization = Audit(new Organization
        {
            OrganizationName = "Asset360 Industries",
            Type = "Corporate"
        }, now);
        set.Organizations.Add(organization);
        var organizationRef = Ref(organization.Id, organization.OrganizationName);

        var businessUnits = BusinessUnitSeeds.ToDictionary(
            seed => seed.Code,
            seed => Audit(new BusinessUnit
            {
                BusinessUnitName = seed.Name,
                Code = seed.Code,
                ParentOrganization = organizationRef
            }, now));
        set.BusinessUnits.AddRange(businessUnits.Values);

        // Employee ids are needed for department heads, and department ids for employees.
        var employeeIds = EmployeeSeeds.ToDictionary(seed => seed.EmployeeId, _ => MongoObjectId.Create());
        var employeeNames = EmployeeSeeds.ToDictionary(seed => seed.EmployeeId, seed => seed.Name);

        var departments = DepartmentSeeds.ToDictionary(
            seed => seed.Code,
            seed => Audit(new Department
            {
                DepartmentName = seed.Name,
                Code = seed.Code,
                BusinessUnit = Ref(businessUnits[seed.BusinessUnitCode].Id, businessUnits[seed.BusinessUnitCode].BusinessUnitName),
                DepartmentHead = Ref(employeeIds[seed.HeadEmployeeId], employeeNames[seed.HeadEmployeeId])
            }, now));
        set.Departments.AddRange(departments.Values);

        foreach (var seed in DepartmentSeeds)
        {
            var department = departments[seed.Code];
            businessUnits[seed.BusinessUnitCode].Departments.Add(Ref(department.Id, department.DepartmentName));
        }

        set.Employees.AddRange(EmployeeSeeds.Select(seed => Audit(new Employee
        {
            Id = employeeIds[seed.EmployeeId],
            EmployeeName = seed.Name,
            EmployeeId = seed.EmployeeId,
            Department = Ref(departments[seed.DepartmentCode].Id, departments[seed.DepartmentCode].DepartmentName),
            Designation = seed.Designation,
            Email = $"{seed.Name.ToLowerInvariant().Replace(' ', '.')}@asset360.example",
            PhoneNumber = seed.Phone
        }, now)));

        var sites = SiteSeeds.ToDictionary(
            seed => seed.Code,
            seed => Audit(new Site
            {
                SiteName = seed.Name,
                Code = seed.Code,
                BusinessUnit = Ref(businessUnits[seed.BusinessUnitCode].Id, businessUnits[seed.BusinessUnitCode].BusinessUnitName),
                Department = Ref(departments[seed.DepartmentCode].Id, departments[seed.DepartmentCode].DepartmentName),
                SiteType = seed.SiteType,
                City = seed.City,
                Country = seed.Country
            }, now));
        set.Sites.AddRange(sites.Values);

        foreach (var seed in SiteSeeds)
        {
            var siteRef = Ref(sites[seed.Code].Id, sites[seed.Code].SiteName);
            organization.Sites.Add(siteRef);
            businessUnits[seed.BusinessUnitCode].Sites.Add(Ref(siteRef.Id!, siteRef.Name));
        }

        organization.BusinessUnits.AddRange(businessUnits.Values.Select(unit => Ref(unit.Id, unit.BusinessUnitName)));

        // Location tree: Site → Building → Floor → Area, built alongside the space records.
        var siteLocations = sites.ToDictionary(
            pair => pair.Key,
            pair => Audit(new Location
            {
                LocationName = pair.Value.SiteName,
                Code = $"LOC-{pair.Key[5..]}",
                Type = "Site"
            }, now));
        set.Locations.AddRange(siteLocations.Values);

        var buildings = new Dictionary<string, Building>();
        var floors = new Dictionary<(string Building, string Floor), Floor>();
        var buildingLocations = new Dictionary<string, Location>();
        var floorLocations = new Dictionary<(string Building, string Floor), Location>();

        foreach (var seed in BuildingSeeds)
        {
            var site = sites[seed.SiteCode];
            var building = Audit(new Building
            {
                Name = seed.Name,
                Code = seed.Code,
                Site = Ref(site.Id, site.SiteName),
                ParentLevel = "Site",
                Description = seed.Description,
                Capacity = seed.Capacity,
                Area = seed.Area
            }, now);
            buildings[seed.Code] = building;
            set.Buildings.Add(building);

            var parentLocation = siteLocations[seed.SiteCode];
            var buildingLocation = Audit(new Location
            {
                LocationName = seed.Name,
                Code = $"LOC-{seed.Code[4..]}",
                Type = "Building",
                Building = seed.Name,
                ParentLocationId = parentLocation.Id,
                ParentLocationName = parentLocation.LocationName
            }, now);
            buildingLocations[seed.Code] = buildingLocation;
            set.Locations.Add(buildingLocation);

            for (var index = 0; index < seed.Floors.Length; index++)
            {
                var floorName = seed.Floors[index];
                var floorCode = $"{seed.Code}-F{index}";
                var floor = Audit(new Floor
                {
                    Name = floorName,
                    Code = floorCode,
                    Site = Ref(site.Id, site.SiteName),
                    BuildingId = building.Id,
                    BuildingName = building.Name,
                    ParentLevel = "Building",
                    Description = $"{floorName}, {building.Name}",
                    Capacity = Math.Max(1, seed.Capacity / seed.Floors.Length),
                    Area = Math.Round(seed.Area / seed.Floors.Length, 1)
                }, now);
                floors[(seed.Code, floorName)] = floor;
                set.Floors.Add(floor);

                var floorLocation = Audit(new Location
                {
                    LocationName = $"{building.Name} - {floorName}",
                    Code = $"LOC-{floorCode[4..]}",
                    Type = "Floor",
                    Building = building.Name,
                    Floor = floorName,
                    ParentLocationId = buildingLocation.Id,
                    ParentLocationName = buildingLocation.LocationName
                }, now);
                floorLocations[(seed.Code, floorName)] = floorLocation;
                set.Locations.Add(floorLocation);
            }
        }

        foreach (var seed in ZoneSeeds)
        {
            var building = buildings[seed.BuildingCode];
            var floor = seed.FloorName is null ? null : floors[(seed.BuildingCode, seed.FloorName)];

            set.Zones.Add(Audit(new Zone
            {
                Name = seed.Name,
                Code = seed.Code,
                Site = building.Site is null ? null : Ref(building.Site.Id!, building.Site.Name),
                BuildingId = building.Id,
                BuildingName = building.Name,
                FloorId = floor?.Id,
                FloorName = floor?.Name,
                ParentLevel = floor is null ? "Building" : "Floor",
                Description = seed.Description,
                Capacity = seed.Capacity,
                Area = seed.Area
            }, now));

            var parentLocation = seed.FloorName is null
                ? buildingLocations[seed.BuildingCode]
                : floorLocations[(seed.BuildingCode, seed.FloorName)];

            set.Locations.Add(Audit(new Location
            {
                LocationName = seed.Name,
                Code = $"LOC-{seed.Code[3..]}",
                Type = "Area",
                Building = building.Name,
                Floor = seed.FloorName,
                Zone = seed.Name,
                ParentLocationId = parentLocation.Id,
                ParentLocationName = parentLocation.LocationName
            }, now));
        }

        return set;
    }

    // ------------------------------------------------------------------ criticality

    public static IReadOnlyList<Criticality> Criticalities(DateTime now) =>
    [
        Audit(new Criticality
        {
            CriticalityName = "Critical",
            Code = "CRIT-A",
            Color = "#C62828",
            RiskLevel = "Very High",
            MaintenancePriority = "P1 - Immediate",
            Description = "Failure stops operations or creates a safety risk (e.g. fire pumps, generators, chillers)."
        }, now),
        Audit(new Criticality
        {
            CriticalityName = "High",
            Code = "CRIT-B",
            Color = "#EF6C00",
            RiskLevel = "High",
            MaintenancePriority = "P2 - Within 24 hours",
            Description = "Failure severely degrades a key service; limited redundancy (e.g. UPS, AHUs, core switches)."
        }, now),
        Audit(new Criticality
        {
            CriticalityName = "Medium",
            Code = "CRIT-C",
            Color = "#F9A825",
            RiskLevel = "Medium",
            MaintenancePriority = "P3 - Within 3 days",
            DefaultCriticality = true,
            Description = "Failure has a moderate impact and a workaround exists (e.g. compressors, forklifts)."
        }, now),
        Audit(new Criticality
        {
            CriticalityName = "Low",
            Code = "CRIT-D",
            Color = "#2E7D32",
            RiskLevel = "Low",
            MaintenancePriority = "P4 - Planned",
            Description = "Failure has minor impact; can wait for planned maintenance (e.g. cameras, office equipment)."
        }, now),
        Audit(new Criticality
        {
            CriticalityName = "Non-Critical",
            Code = "CRIT-E",
            Color = "#757575",
            RiskLevel = "Negligible",
            MaintenancePriority = "P5 - Run to failure",
            Description = "Run-to-failure or easily replaced items (e.g. laptops, peripherals)."
        }, now)
    ];

    // ------------------------------------------------------------------ conditions

    public static IReadOnlyList<Condition> Conditions(DateTime now) =>
    [
        Audit(new Condition { ConditionName = "New", Code = "COND-NEW", Type = "Operational", Color = "#1565C0", HealthScore = 100, Description = "Unused, as delivered." }, now),
        Audit(new Condition { ConditionName = "Excellent", Code = "COND-EXC", Type = "Operational", Color = "#2E7D32", HealthScore = 90, Description = "Like new; no visible wear or faults." }, now),
        Audit(new Condition { ConditionName = "Good", Code = "COND-GOOD", Type = "Operational", Color = "#66BB6A", HealthScore = 75, DefaultCondition = true, Description = "Normal wear; fully functional." }, now),
        Audit(new Condition { ConditionName = "Fair", Code = "COND-FAIR", Type = "Operational", Color = "#F9A825", HealthScore = 55, Description = "Noticeable wear; works but needs attention soon." }, now),
        Audit(new Condition { ConditionName = "Poor", Code = "COND-POOR", Type = "Degraded", Color = "#EF6C00", HealthScore = 30, Description = "Frequent faults; repair or replacement should be planned." }, now),
        Audit(new Condition { ConditionName = "Damaged", Code = "COND-DMG", Type = "Non-Operational", Color = "#C62828", HealthScore = 10, Description = "Not working; needs repair before use." }, now),
        Audit(new Condition { ConditionName = "Beyond Repair", Code = "COND-BER", Type = "Non-Operational", Color = "#757575", HealthScore = 0, Description = "Uneconomical to repair; candidate for disposal." }, now)
    ];

    // ------------------------------------------------------------------ units of measure

    public static IReadOnlyList<Uom> Uoms(DateTime now) =>
    [
        Audit(new Uom { UomName = "Each", Code = "EA", Type = "Discrete", Symbol = "ea", ConversionFactor = 1, Description = "Single item (base unit for counts)." }, now),
        Audit(new Uom { UomName = "Piece", Code = "PCS", Type = "Discrete", Symbol = "pcs", ConversionFactor = 1, Description = "Individual piece." }, now),
        Audit(new Uom { UomName = "Pair", Code = "PR", Type = "Discrete", Symbol = "pr", ConversionFactor = 2, Description = "Two items." }, now),
        Audit(new Uom { UomName = "Set", Code = "SET", Type = "Discrete", Symbol = "set", ConversionFactor = 1, Description = "A matched set sold as one unit." }, now),
        Audit(new Uom { UomName = "Box", Code = "BOX", Type = "Discrete", Symbol = "box", ConversionFactor = 1, Description = "Box / carton." }, now),
        Audit(new Uom { UomName = "Gram", Code = "G", Type = "Weight", Symbol = "g", ConversionFactor = 1, Description = "Base unit for weight." }, now),
        Audit(new Uom { UomName = "Kilogram", Code = "KG", Type = "Weight", Symbol = "kg", ConversionFactor = 1000, Description = "1000 grams." }, now),
        Audit(new Uom { UomName = "Millilitre", Code = "ML", Type = "Volume", Symbol = "ml", ConversionFactor = 1, Description = "Base unit for volume." }, now),
        Audit(new Uom { UomName = "Litre", Code = "L", Type = "Volume", Symbol = "L", ConversionFactor = 1000, Description = "1000 millilitres." }, now),
        Audit(new Uom { UomName = "Metre", Code = "M", Type = "Length", Symbol = "m", ConversionFactor = 1, Description = "Base unit for length." }, now),
        Audit(new Uom { UomName = "Square Metre", Code = "M2", Type = "Area", Symbol = "m²", ConversionFactor = 1, Description = "Base unit for area." }, now),
        Audit(new Uom { UomName = "Hour", Code = "HR", Type = "Time", Symbol = "h", ConversionFactor = 1, Description = "Base unit for labour / running time." }, now)
    ];

    // ------------------------------------------------------------------ shifts

    private static readonly string[] WeekDays = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"];
    private static readonly string[] AllDays = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];

    private static Shift NewShift(string name, string code, string shiftTime, string start, string end, string[] days, string color, string description) => new()
    {
        ShiftName = name,
        Code = code,
        ShiftTime = shiftTime,
        StartTime = start,
        EndTime = end,
        Duration = Contracts.ShiftTimes.DurationMinutes(start, end),
        WorkingDays = [.. days],
        Color = color,
        Description = description
    };

    public static IReadOnlyList<Shift> Shifts(DateTime now) =>
    [
        Audit(NewShift("General Shift", "SH-GEN", "Day", "08:00", "17:00", WeekDays, "#1565C0", "Office hours, Monday to Friday."), now),
        Audit(NewShift("Morning Shift", "SH-A", "Morning", "06:00", "14:00", AllDays, "#2E7D32", "Plant shift A."), now),
        Audit(NewShift("Evening Shift", "SH-B", "Evening", "14:00", "22:00", AllDays, "#EF6C00", "Plant shift B."), now),
        Audit(NewShift("Night Shift", "SH-C", "Night", "22:00", "06:00", AllDays, "#4527A0", "Plant shift C; runs past midnight."), now)
    ];

    // ------------------------------------------------------------------ vendors and asset models

    /// <summary>An asset type that already exists in the database, found by its code.</summary>
    public sealed record AssetTypeLookup(string Id, string Name, MasterDataReference? Category);

    public sealed class VendorSet
    {
        public List<Manufacturer> Manufacturers { get; } = [];
        public List<AssetModel> AssetModels { get; } = [];
        public List<Supplier> Suppliers { get; } = [];
        public List<ServiceProvider> ServiceProviders { get; } = [];
        public List<AmcVendor> AmcVendors { get; } = [];
    }

    private sealed record ManufacturerSeed(string Code, string Name, string Country, string Website, string Contact, string Description);

    private sealed record ModelSeed(string Code, string Name, string ManufacturerCode, string AssetTypeCode, string Description);

    private static readonly ManufacturerSeed[] ManufacturerSeeds =
    [
        new("MFR-CUM", "Cummins", "United States", "https://www.cummins.com", "John Miller", "Diesel and gas generator sets"),
        new("MFR-CAT", "Caterpillar", "United States", "https://www.cat.com", "Laura Chen", "Gas generator sets and heavy equipment"),
        new("MFR-APC", "APC by Schneider Electric", "France", "https://www.apc.com", "Pierre Martin", "UPS and power protection"),
        new("MFR-EAT", "Eaton", "Ireland", "https://www.eaton.com", "Sean Walsh", "UPS and power management"),
        new("MFR-CAR", "Carrier", "United States", "https://www.carrier.com", "Mark Davis", "Chillers and HVAC equipment"),
        new("MFR-TRN", "Trane", "United States", "https://www.trane.com", "Emily Brooks", "Air handling units and HVAC"),
        new("MFR-DAI", "Daikin", "Japan", "https://www.daikin.com", "Kenji Sato", "VRF and packaged HVAC units"),
        new("MFR-ATC", "Atlas Copco", "Sweden", "https://www.atlascopco.com", "Erik Larsson", "Air compressors"),
        new("MFR-GRF", "Grundfos", "Denmark", "https://www.grundfos.com", "Mette Jensen", "Pumps and fire pump sets"),
        new("MFR-HIK", "Hikvision", "China", "https://www.hikvision.com", "Li Wei", "IP cameras and recorders"),
        new("MFR-HID", "HID Global", "United States", "https://www.hidglobal.com", "Sarah Lopez", "Access control hardware"),
        new("MFR-CSC", "Cisco", "United States", "https://www.cisco.com", "Raj Malhotra", "Network switches and routers"),
        new("MFR-DEL", "Dell Technologies", "United States", "https://www.dell.com", "Amanda Ross", "Servers and laptops"),
        new("MFR-TOY", "Toyota Material Handling", "Japan", "https://toyota-forklifts.com", "Hiro Tanaka", "Forklifts"),
        new("MFR-OTS", "Otis", "United States", "https://www.otis.com", "Robert King", "Elevators and escalators")
    ];

    // Models from the Maintenance seed's asset list, plus a few common extras.
    private static readonly ModelSeed[] ModelSeeds =
    [
        new("C500D5", "Cummins C500 D5", "MFR-CUM", "AT-GEN", "500 kVA diesel generator set"),
        new("G3512", "Caterpillar G3512", "MFR-CAT", "AT-GEN", "750 kVA gas generator set"),
        new("SRT20KXLI", "APC Smart-UPS SRT 20kVA", "MFR-APC", "AT-UPS", "20 kVA online UPS"),
        new("93PM-60", "Eaton 93PM 60kW", "MFR-EAT", "AT-UPS", "60 kVA modular UPS"),
        new("30XA-502", "Carrier AquaForce 30XA-502", "MFR-CAR", "AT-CHL", "500 TR air-cooled screw chiller"),
        new("30XA-402", "Carrier AquaForce 30XA-402", "MFR-CAR", "AT-CHL", "400 TR air-cooled screw chiller"),
        new("CSAA-012", "Trane CSAA 012", "MFR-TRN", "AT-AHU", "Climate changer air handler"),
        new("RXYQ20", "Daikin VRV IV RXYQ20", "MFR-DAI", "AT-HVAC", "20 HP VRF outdoor unit"),
        new("RXYQ16", "Daikin VRV IV RXYQ16", "MFR-DAI", "AT-HVAC", "16 HP VRF outdoor unit"),
        new("GA37VSD", "Atlas Copco GA 37 VSD", "MFR-ATC", "AT-ACMP", "37 kW variable speed screw compressor"),
        new("GA22", "Atlas Copco GA 22", "MFR-ATC", "AT-ACMP", "22 kW screw compressor"),
        new("NKF80-250", "Grundfos NKF 80-250", "MFR-GRF", "AT-FPMP", "End-suction fire pump"),
        new("CR32", "Grundfos CR 32", "MFR-GRF", "AT-PMP", "Vertical multistage pump"),
        new("DS-2CD2143", "Hikvision DS-2CD2143G2", "MFR-HIK", "AT-CAM", "4 MP dome network camera"),
        new("V1000", "HID VertX V1000", "MFR-HID", "AT-ACP", "Networked access controller"),
        new("C9300-48P", "Cisco Catalyst 9300-48P", "MFR-CSC", "AT-NSW", "48-port PoE+ switch"),
        new("R750", "Dell PowerEdge R750", "MFR-DEL", "AT-SRV", "2U rack server"),
        new("LAT5440", "Dell Latitude 5440", "MFR-DEL", "AT-LAP", "14-inch business laptop"),
        new("8FBE20", "Toyota 8FBE20", "MFR-TOY", "AT-FLT", "2 t electric counterbalance forklift"),
        new("GEN2-LIFE", "Otis Gen2 Life", "MFR-OTS", "AT-ELV", "Machine-room-less passenger lift")
    ];

    public static VendorSet Vendors(DateTime now, IReadOnlyDictionary<string, AssetTypeLookup> assetTypes)
    {
        var set = new VendorSet();

        var manufacturers = ManufacturerSeeds.ToDictionary(
            seed => seed.Code,
            seed => Audit(new Manufacturer
            {
                ManufacturerName = seed.Name,
                Code = seed.Code,
                Country = seed.Country,
                Website = seed.Website,
                Description = seed.Description,
                ContactPerson = seed.Contact,
                Email = $"sales@{new Uri(seed.Website).Host.Replace("www.", string.Empty)}",
                PhoneNumber = "+971 4 000 0000"
            }, now));
        set.Manufacturers.AddRange(manufacturers.Values);

        // Models need their asset type; any whose type is missing from the database are left out.
        foreach (var seed in ModelSeeds)
        {
            if (!assetTypes.TryGetValue(seed.AssetTypeCode, out var assetType))
            {
                continue;
            }

            var manufacturer = manufacturers[seed.ManufacturerCode];
            set.AssetModels.Add(Audit(new AssetModel
            {
                ModelName = seed.Name,
                ModelCode = seed.Code,
                Manufacturer = Ref(manufacturer.Id, manufacturer.ManufacturerName),
                AssetType = Ref(assetType.Id, assetType.Name),
                Category = assetType.Category is null ? null : Ref(assetType.Category.Id!, assetType.Category.Name),
                Description = seed.Description,
                Lifecycle = "Active"
            }, now));
        }

        set.Suppliers.AddRange(
        [
            Audit(new Supplier { SupplierName = "Gulf Power Solutions LLC", Code = "SUP-GPS", Category = "Electrical & Power", PrimaryContact = "Vikram Rao", Email = "sales@gulfpower.example", Phone = "+971 4 321 1001", Country = "United Arab Emirates", Website = "https://gulfpower.example" }, now),
            Audit(new Supplier { SupplierName = "CoolTech HVAC Trading", Code = "SUP-CTH", Category = "HVAC", PrimaryContact = "Hassan Ali", Email = "orders@cooltech.example", Phone = "+971 4 321 1002", Country = "United Arab Emirates", Website = "https://cooltech.example" }, now),
            Audit(new Supplier { SupplierName = "Emirates IT Distribution", Code = "SUP-EITD", Category = "IT Hardware", PrimaryContact = "Anita George", Email = "sales@eitd.example", Phone = "+971 4 321 1003", Country = "United Arab Emirates", Website = "https://eitd.example" }, now),
            Audit(new Supplier { SupplierName = "SafeGuard Security Systems", Code = "SUP-SGS", Category = "Safety & Security", PrimaryContact = "Bilal Qureshi", Email = "info@safeguard.example", Phone = "+971 4 321 1004", Country = "United Arab Emirates", Website = "https://safeguard.example" }, now),
            Audit(new Supplier { SupplierName = "Al Noor Industrial Supplies", Code = "SUP-ANIS", Category = "Mechanical", PrimaryContact = "Faisal Noor", Email = "sales@alnoor.example", Phone = "+971 4 321 1005", Country = "United Arab Emirates", Website = "https://alnoor.example" }, now),
            Audit(new Supplier { SupplierName = "Prime Material Handling", Code = "SUP-PMH", Category = "Vehicles & Material Handling", PrimaryContact = "George Varghese", Email = "sales@primemh.example", Phone = "+971 4 321 1006", Country = "United Arab Emirates", Website = "https://primemh.example" }, now)
        ]);

        set.ServiceProviders.AddRange(
        [
            Audit(new ServiceProvider { ServiceProviderName = "PowerCare Services", Code = "SP-PWC", ServiceType = "Generator & UPS Maintenance", PrimaryContact = "Suresh Menon", Email = "service@powercare.example", Phone = "+971 4 555 2001", Country = "United Arab Emirates", Website = "https://powercare.example", Description = "Authorised service for generators and UPS." }, now),
            Audit(new ServiceProvider { ServiceProviderName = "ClimaServ HVAC", Code = "SP-CLM", ServiceType = "HVAC Maintenance", PrimaryContact = "Tariq Aziz", Email = "service@climaserv.example", Phone = "+971 4 555 2002", Country = "United Arab Emirates", Website = "https://climaserv.example", Description = "Chiller, AHU and VRF servicing." }, now),
            Audit(new ServiceProvider { ServiceProviderName = "FireShield Technical Services", Code = "SP-FSH", ServiceType = "Fire & Life Safety", PrimaryContact = "Kevin D'Souza", Email = "service@fireshield.example", Phone = "+971 4 555 2003", Country = "United Arab Emirates", Website = "https://fireshield.example", Description = "Fire pump, alarm and suppression maintenance." }, now),
            Audit(new ServiceProvider { ServiceProviderName = "NetCore IT Services", Code = "SP-NCI", ServiceType = "IT Support", PrimaryContact = "Rahul Sharma", Email = "support@netcore.example", Phone = "+971 4 555 2004", Country = "United Arab Emirates", Website = "https://netcore.example", Description = "Network, server and end-user device support." }, now),
            Audit(new ServiceProvider { ServiceProviderName = "LiftTech Elevators", Code = "SP-LFT", ServiceType = "Vertical Transport", PrimaryContact = "Imran Sheikh", Email = "service@lifttech.example", Phone = "+971 4 555 2005", Country = "United Arab Emirates", Website = "https://lifttech.example", Description = "Elevator inspection and maintenance." }, now)
        ]);

        var contractStart = new DateOnly(now.Year, 1, 1);
        (string Name, string Code, string AssetTypeCode, string Contact, string Email, string ContractType, int Years)[] amcSeeds =
        [
            ("PowerCare Services - Generators", "AMC-PWC-GEN", "AT-GEN", "Suresh Menon", "amc@powercare.example", "Comprehensive", 3),
            ("PowerCare Services - UPS", "AMC-PWC-UPS", "AT-UPS", "Suresh Menon", "amc@powercare.example", "Comprehensive", 2),
            ("ClimaServ HVAC - Chillers", "AMC-CLM-CHL", "AT-CHL", "Tariq Aziz", "amc@climaserv.example", "Non-Comprehensive", 2),
            ("FireShield - Fire Pumps", "AMC-FSH-FP", "AT-FPMP", "Kevin D'Souza", "amc@fireshield.example", "Comprehensive", 1),
            ("LiftTech - Elevators", "AMC-LFT-ELV", "AT-ELV", "Imran Sheikh", "amc@lifttech.example", "Comprehensive", 5)
        ];

        foreach (var seed in amcSeeds)
        {
            assetTypes.TryGetValue(seed.AssetTypeCode, out var assetType);
            set.AmcVendors.Add(Audit(new AmcVendor
            {
                AmcVendorName = seed.Name,
                Code = seed.Code,
                StartDate = contractStart,
                EndDate = contractStart.AddYears(seed.Years).AddDays(-1),
                PrimaryContact = seed.Contact,
                Email = seed.Email,
                ContractType = seed.ContractType,
                AssetType = assetType is null ? null : Ref(assetType.Id, assetType.Name)
            }, now));
        }

        return set;
    }

    // ------------------------------------------------------------------ teams and technicians

    public sealed class WorkforceSet
    {
        public List<Team> Teams { get; } = [];
        public List<Technician> Technicians { get; } = [];
    }

    /// <summary>
    /// Teams and their technicians. Technician names match the Maintenance seed. Departments are looked up by
    /// code; a team whose department is missing is still created, just without the link.
    /// </summary>
    public static WorkforceSet Workforce(DateTime now, IReadOnlyDictionary<string, MasterDataReference> departments)
    {
        (string Code, string Name, string DepartmentCode, string Description)[] teamSeeds =
        [
            ("TM-HVAC", "HVAC Team", "DEPT-FAC", "Chillers, AHUs and VRF units"),
            ("TM-ELEC", "Electrical Team", "DEPT-ENG", "Generators, UPS and switchgear"),
            ("TM-MECH", "Mechanical Team", "DEPT-MNT", "Compressors, pumps and rotating equipment"),
            ("TM-IT", "IT Support Team", "DEPT-IT", "Network, servers and end-user devices"),
            ("TM-FLS", "Fire & Life Safety Team", "DEPT-SAF", "Fire pumps, alarms and safety systems")
        ];

        (string TechnicianId, string Name, string Skill, string TeamCode, bool Leader)[] technicianSeeds =
        [
            ("TECH-0001", "Ahmed Khan", "HVAC", "TM-HVAC", true),
            ("TECH-0002", "Vinod Pillai", "Refrigeration", "TM-HVAC", false),
            ("TECH-0003", "Salim Ahmed", "Electrical", "TM-ELEC", true),
            ("TECH-0004", "Khalid Mansoori", "Electrical", "TM-ELEC", false),
            ("TECH-0005", "Ramesh Kumar", "Mechanical", "TM-MECH", true),
            ("TECH-0006", "Nisha Patel", "Vibration Analysis", "TM-MECH", false),
            ("TECH-0007", "Daniel Thomas", "Networking", "TM-IT", true),
            ("TECH-0008", "Arjun Mehta", "Fire Systems", "TM-FLS", true),
            ("TECH-0009", "Priya Nair", "Mechanical", "TM-MECH", false)
        ];

        var set = new WorkforceSet();
        var technicianIds = technicianSeeds.ToDictionary(seed => seed.TechnicianId, _ => MongoObjectId.Create());

        var teams = teamSeeds.ToDictionary(
            seed => seed.Code,
            seed =>
            {
                var leader = technicianSeeds.FirstOrDefault(tech => tech.TeamCode == seed.Code && tech.Leader);
                departments.TryGetValue(seed.DepartmentCode, out var department);
                return Audit(new Team
                {
                    TeamName = seed.Name,
                    Code = seed.Code,
                    Department = department is null ? null : Ref(department.Id!, department.Name),
                    TeamLeader = leader.Name is null ? null : Ref(technicianIds[leader.TechnicianId], leader.Name),
                    Description = seed.Description
                }, now);
            });
        set.Teams.AddRange(teams.Values);

        set.Technicians.AddRange(technicianSeeds.Select(seed => Audit(new Technician
        {
            Id = technicianIds[seed.TechnicianId],
            TechnicianName = seed.Name,
            TechnicianId = seed.TechnicianId,
            PrimarySkill = seed.Skill,
            AssignedTeam = Ref(teams[seed.TeamCode].Id, teams[seed.TeamCode].TeamName),
            Email = $"{seed.Name.ToLowerInvariant().Replace(' ', '.')}@asset360.example",
            PhoneNumber = $"+971 55 200 {seed.TechnicianId[^4..]}"
        }, now)));

        return set;
    }

    // ------------------------------------------------------------------ asset catalog

    public sealed class AssetCatalogSet
    {
        public List<AssetCategory> Categories { get; } = [];
        public List<AssetSubCategory> SubCategories { get; } = [];
        public List<AssetType> AssetTypes { get; } = [];
    }

    private sealed record CategorySeed(string Code, string Name, string? ParentCode, string AssetType, string Description);

    private sealed record SubCategorySeed(string Code, string Name, string CategoryCode, string Description);

    private sealed record AssetTypeSeed(string Code, string Name, string CategoryCode, string AssetClass, string DepreciationMethod, int Lifetime, string Description);

    private static readonly CategorySeed[] CategorySeeds =
    [
        new("CAT-MECH", "Mechanical", null, "Fixed Asset", "Rotating and mechanical plant equipment"),
        new("CAT-ELEC", "Electrical", null, "Fixed Asset", "Electrical distribution and power equipment"),
        new("CAT-PWR", "Power Systems", "CAT-ELEC", "Fixed Asset", "Standby and backup power generation"),
        new("CAT-HVAC", "HVAC", null, "Fixed Asset", "Heating, ventilation and air conditioning"),
        new("CAT-IT", "IT & Network", null, "Fixed Asset", "IT hardware and network infrastructure"),
        new("CAT-SAFE", "Safety & Security", null, "Fixed Asset", "Life safety and physical security systems"),
        new("CAT-FIRE", "Fire Protection", "CAT-SAFE", "Fixed Asset", "Fire detection, suppression and pumps"),
        new("CAT-VEH", "Vehicles & Material Handling", null, "Movable Asset", "Forklifts, vehicles and handling equipment"),
        new("CAT-BLDG", "Building Systems", null, "Fixed Asset", "Vertical transport and building services")
    ];

    private static readonly SubCategorySeed[] SubCategorySeeds =
    [
        new("SUB-GEN", "Generators", "CAT-PWR", "Diesel and gas generator sets"),
        new("SUB-UPS", "UPS Systems", "CAT-PWR", "Uninterruptible power supplies"),
        new("SUB-SWG", "Switchgear & Panels", "CAT-ELEC", "LV/MV switchgear and distribution boards"),
        new("SUB-CHL", "Chillers", "CAT-HVAC", "Air and water cooled chillers"),
        new("SUB-AHU", "Air Handling Units", "CAT-HVAC", "AHUs and FAHUs"),
        new("SUB-PKG", "Packaged & VRF Units", "CAT-HVAC", "Rooftop packaged and VRF units"),
        new("SUB-CMP", "Air Compressors", "CAT-MECH", "Screw and reciprocating compressors"),
        new("SUB-PMP", "Pumps", "CAT-MECH", "Transfer and booster pumps"),
        new("SUB-NET", "Network Equipment", "CAT-IT", "Switches, routers and firewalls"),
        new("SUB-SRV", "Servers & Storage", "CAT-IT", "Rack servers and storage arrays"),
        new("SUB-EUD", "End User Devices", "CAT-IT", "Laptops, desktops and monitors"),
        new("SUB-CCTV", "CCTV", "CAT-SAFE", "IP cameras and recorders"),
        new("SUB-ACS", "Access Control", "CAT-SAFE", "Door controllers and readers"),
        new("SUB-FPMP", "Fire Pumps", "CAT-FIRE", "Fire pump sets and jockey pumps"),
        new("SUB-FDA", "Fire Detection & Alarm", "CAT-FIRE", "Panels, detectors and sounders"),
        new("SUB-FLT", "Forklifts", "CAT-VEH", "Electric and diesel forklifts"),
        new("SUB-ELV", "Elevators", "CAT-BLDG", "Passenger and goods lifts")
    ];

    private static readonly AssetTypeSeed[] AssetTypeSeeds =
    [
        new("AT-GEN", "Generator", "CAT-PWR", "Fixed Asset", "Straight Line", 20, "Diesel / gas generator set"),
        new("AT-UPS", "UPS", "CAT-PWR", "Fixed Asset", "Straight Line", 10, "Online double-conversion UPS"),
        new("AT-CHL", "Chiller", "CAT-HVAC", "Fixed Asset", "Straight Line", 20, "Air cooled screw chiller"),
        new("AT-AHU", "AHU", "CAT-HVAC", "Fixed Asset", "Straight Line", 15, "Air handling unit"),
        new("AT-HVAC", "HVAC Unit", "CAT-HVAC", "Fixed Asset", "Straight Line", 15, "Rooftop / VRF outdoor unit"),
        new("AT-ACMP", "Air Compressor", "CAT-MECH", "Fixed Asset", "Straight Line", 12, "Rotary screw air compressor"),
        new("AT-PMP", "Pump", "CAT-MECH", "Fixed Asset", "Straight Line", 15, "Centrifugal pump"),
        new("AT-FPMP", "Fire Pump", "CAT-FIRE", "Fixed Asset", "Straight Line", 20, "Fire pump set"),
        new("AT-CAM", "IP Camera", "CAT-SAFE", "Fixed Asset", "Straight Line", 5, "Network surveillance camera"),
        new("AT-ACP", "Access Control", "CAT-SAFE", "Fixed Asset", "Straight Line", 8, "Access control panel"),
        new("AT-NSW", "Network Switch", "CAT-IT", "Fixed Asset", "Straight Line", 5, "Managed PoE switch"),
        new("AT-SRV", "Server", "CAT-IT", "Fixed Asset", "Declining Balance", 5, "Rack mount server"),
        new("AT-LAP", "Laptop", "CAT-IT", "Movable Asset", "Declining Balance", 3, "Employee laptop"),
        new("AT-FLT", "Forklift", "CAT-VEH", "Movable Asset", "Declining Balance", 10, "Electric counterbalance forklift"),
        new("AT-ELV", "Elevator", "CAT-BLDG", "Fixed Asset", "Straight Line", 25, "Passenger elevator")
    ];

    public static AssetCatalogSet AssetCatalog(DateTime now)
    {
        var set = new AssetCatalogSet();

        // Parents are listed before children, so a single pass can resolve them.
        var categories = new Dictionary<string, AssetCategory>();
        foreach (var seed in CategorySeeds)
        {
            var parent = seed.ParentCode is null ? null : categories[seed.ParentCode];
            var category = Audit(new AssetCategory
            {
                CategoryName = seed.Name,
                Code = seed.Code,
                AssetType = seed.AssetType,
                Description = seed.Description,
                ParentCategory = parent is null ? null : Ref(parent.Id, parent.CategoryName)
            }, now);
            categories[seed.Code] = category;
            set.Categories.Add(category);
        }

        set.SubCategories.AddRange(SubCategorySeeds.Select(seed =>
        {
            var category = categories[seed.CategoryCode];
            return Audit(new AssetSubCategory
            {
                SubCategoryName = seed.Name,
                Code = seed.Code,
                AssetType = category.AssetType,
                Description = seed.Description,
                CategoryId = category.Id,
                CategoryName = category.CategoryName
            }, now);
        }));

        set.AssetTypes.AddRange(AssetTypeSeeds.Select(seed =>
        {
            var category = categories[seed.CategoryCode];
            return Audit(new AssetType
            {
                AssetTypeName = seed.Name,
                Code = seed.Code,
                Category = Ref(category.Id, category.CategoryName),
                AssetClass = seed.AssetClass,
                DepreciationMethod = seed.DepreciationMethod,
                DefaultLifetime = seed.Lifetime,
                Description = seed.Description
            }, now);
        }));

        return set;
    }
}
