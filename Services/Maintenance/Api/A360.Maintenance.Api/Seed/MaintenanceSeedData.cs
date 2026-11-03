using A360.Domain.Entities;
using A360.Maintenance.Domain.Entities;

namespace A360.Maintenance.Api.Seed;

/// <summary>
/// Sample Maintenance data taken from the module mockups (work orders, service requests, PM schedules and
/// predictive models for the Main Plant assets), plus two years of history so the trend charts have data.
/// Dates are relative to the day the seed runs: the mockups' "today" (9 Oct) maps to the seed date, so
/// overdue / upcoming items stay meaningful. Output is deterministic for a given seed date.
/// </summary>
public static class MaintenanceSeedData
{
    private const string ClientId = "CLT-1001";
    private const string TenantId = "TENANT-DEFAULT";
    private const string SeedUser = "Rex Kumar";

    /// <summary>Months of history before the current month, so "vs previous period" has a full prior year.</summary>
    private const int HistoryMonths = 23;

    public sealed record SeedAsset(
        string Tag, string Name, string Type, string Site, string Building, string Area, string Department,
        string Manufacturer, string Model, string Serial);

    /// <summary>Assets used across the mockups. The frontend keeps the same list in shared/asset-catalog.ts.</summary>
    public static readonly SeedAsset[] Assets =
    [
        new("GEN-00045", "Generator 500 kVA", "Generator", "Main Plant", "Block A", "Generator Room - G1", "Facilities", "Cummins", "C500D5", "SN-2023-00045"),
        new("AHU-00021", "AHU-01", "AHU", "Head Office", "Building A", "Plant Room - L3", "Facilities", "Trane", "CSAA-012", "SN-2022-00021"),
        new("FL-00078", "Forklift Toyota", "Forklift", "Warehouse", "Main Warehouse", "Loading Bay 2", "Logistics", "Toyota", "8FBE20", "SN-2021-00078"),
        new("UPS-00112", "UPS 20 kVA", "UPS", "Head Office", "Building A", "IT Room", "IT", "APC", "SRT20KXLI", "SN-2023-00112"),
        new("FP-00033", "Fire Pump", "Fire Pump", "Main Plant", "Utility Block", "Utility Area", "Safety", "Grundfos", "NKF 80-250", "SN-2020-00033"),
        new("CAM-00089", "CCTV Camera", "IP Camera", "Head Office", "Main Building", "Main Entrance", "Security", "Hikvision", "DS-2CD2143", "SN-2024-00089"),
        new("SW-00176", "Network Switch", "Network Switch", "Head Office", "Data Centre", "Rack B-04", "IT", "Cisco", "C9300-48P", "SN-2023-00176"),
        new("CH-00012", "Chiller Plant", "Chiller", "Main Plant", "Block B", "Chiller Yard", "Facilities", "Carrier", "30XA-502", "SN-2019-00012"),
        new("ACP-00056", "Access Control Panel", "Access Control", "Head Office", "Main Building", "Security Room", "Security", "HID", "VertX V1000", "SN-2022-00056"),
        new("AC-00100", "Air Compressor", "Air Compressor", "Main Plant", "Maintenance Block", "Compressor Room", "Maintenance", "Atlas Copco", "GA 37 VSD", "SN-2021-00100"),
        new("HVAC-00201", "HVAC Unit 1", "HVAC Unit", "Head Office", "Building B", "Roof", "Facilities", "Daikin", "RXYQ20", "SN-2022-00201"),
        new("HVAC-00202", "HVAC Unit 2", "HVAC Unit", "Head Office", "Building B", "Roof", "Facilities", "Daikin", "RXYQ20", "SN-2022-00202"),
        new("HVAC-00203", "HVAC Unit 3", "HVAC Unit", "Main Plant", "Block A", "Roof", "Facilities", "Daikin", "RXYQ16", "SN-2023-00203"),
        new("ELV-00011", "Elevator 1", "Elevator", "Head Office", "Main Building", "Lift Core A", "Facilities", "Otis", "Gen2 Life", "SN-2020-00011"),
        new("GEN-00052", "Gas Generator 750 kVA", "Generator", "Main Plant", "Substation", "Outdoor", "Engineering", "Caterpillar", "G3512", "SN-2021-00052"),
        new("UPS-00118", "UPS 60 kVA", "UPS", "Head Office", "Data Centre", "UPS Room", "IT", "Eaton", "93PM-60", "SN-2022-00118"),
        new("CH-00014", "Chiller Unit 2", "Chiller", "Main Plant", "Block B", "Chiller Yard", "Facilities", "Carrier", "30XA-402", "SN-2020-00014"),
        new("AC-00104", "Air Compressor 2", "Air Compressor", "Main Plant", "Maintenance Block", "Compressor Room", "Maintenance", "Atlas Copco", "GA 22", "SN-2022-00104")
    ];

    private static readonly string[] Technicians = ["Ahmed Khan", "Priya Nair", "Ramesh Kumar", "Salim Ahmed", "Nisha Patel", "Khalid Mansoori", "Arjun Mehta"];

    private static SeedAsset Asset(string tag) => Assets.First(asset => asset.Tag == tag);

    private static T Audit<T>(T entity, string status, DateTime createdAt, string? createdBy = null)
        where T : BaseEntity
    {
        entity.ClientId = ClientId;
        entity.TenantId = TenantId;
        entity.CreatedBy = createdBy ?? SeedUser;
        entity.CreatedAt = createdAt;
        entity.Status = status;
        entity.IsDeleted = false;
        return entity;
    }

    // ------------------------------------------------------------------ work orders

    public static IReadOnlyList<WorkOrder> WorkOrders(DateTime today)
    {
        var random = new Random(4211);
        var rows = new List<WorkOrder>();
        var types = new[] { "Preventive", "Preventive", "Preventive", "Corrective", "Corrective", "Predictive", "Inspection" };
        var priorities = new[] { "High", "Medium", "Medium", "Low" };
        var historyAssets = Assets.Take(10).ToArray();

        // Eleven months of history: mostly completed, a few cancelled.
        for (var monthsBack = HistoryMonths; monthsBack >= 1; monthsBack--)
        {
            var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-monthsBack);
            var count = 6 + random.Next(7);
            for (var i = 0; i < count; i++)
            {
                var asset = historyAssets[random.Next(historyAssets.Length)];
                var type = types[random.Next(types.Length)];
                var created = monthStart.AddDays(random.Next(26)).AddHours(8);
                var due = created.AddDays(2 + random.Next(8));
                var roll = random.Next(100);
                var status = roll < 86 ? "Completed" : roll < 93 ? "Cancelled" : "Completed";
                var technician = Technicians[random.Next(Technicians.Length)];
                rows.Add(BuildWorkOrder(asset, type, $"{asset.Name} - {TitleFor(type)}", priorities[random.Next(priorities.Length)],
                    status, created, due, technician, finishedLate: random.Next(100) < 12));
            }
        }

        // Current month backlog.
        var currentStatuses = new[] { "Completed", "Completed", "In Progress", "Scheduled", "Open" };
        for (var i = 0; i < 8; i++)
        {
            var asset = historyAssets[random.Next(historyAssets.Length)];
            var type = types[random.Next(types.Length)];
            var created = today.AddDays(-1 - random.Next(Math.Max(1, today.Day))).AddHours(8);
            var due = created.AddDays(3 + random.Next(10));
            rows.Add(BuildWorkOrder(asset, type, $"{asset.Name} - {TitleFor(type)}", priorities[random.Next(priorities.Length)],
                currentStatuses[random.Next(currentStatuses.Length)], created, due, Technicians[random.Next(Technicians.Length)], false));
        }

        // The ten work orders shown in the mockups (offsets from the mockups' 9 Oct).
        DateTime D(int offset, int hour = 0) => today.AddDays(offset).AddHours(hour);
        rows.Add(MockupLeadWorkOrder(today));
        rows.Add(BuildWorkOrder(Asset("AHU-00021"), "Corrective", "AHU-01 - Supply fan vibration", "Medium", "Open", D(-4), D(3), "Priya Nair", false));
        rows.Add(BuildWorkOrder(Asset("FL-00078"), "Service", "Forklift Toyota - 250 hour service", "High", "Open", D(-9), D(-4), "Ramesh Kumar", false));
        rows.Add(BuildWorkOrder(Asset("UPS-00112"), "Preventive", "UPS 20 kVA - Quarterly battery check", "Medium", "In Progress", D(-3), D(5), "Salim Ahmed", false));
        rows.Add(BuildWorkOrder(Asset("FP-00033"), "Inspection", "Fire Pump - Weekly run test", "Low", "Scheduled", D(-1), D(11), "Nisha Patel", false));
        rows.Add(BuildWorkOrder(Asset("CAM-00089"), "Corrective", "CCTV Camera - No video feed", "High", "Open", D(-6), D(7), "Khalid Mansoori", false));
        rows.Add(BuildWorkOrder(Asset("SW-00176"), "Preventive", "Network Switch - Firmware & port audit", "Medium", "In Progress", D(-2), D(9), "Arjun Mehta", false));
        rows.Add(BuildWorkOrder(Asset("CH-00012"), "Predictive", "Chiller Plant - Compressor bearing check", "High", "In Progress", D(-8), D(-1), "Priya Nair", false));
        rows.Add(BuildWorkOrder(Asset("ACP-00056"), "Corrective", "Access Control Panel - Reader offline", "Low", "Completed", D(-7), D(-4), "Ahmed Khan", false));
        rows.Add(BuildWorkOrder(Asset("AC-00100"), "Preventive", "Air Compressor - Filter & oil change", "Medium", "Scheduled", D(0), D(13), "Ramesh Kumar", false));

        // Link preventive work orders to the PM schedule of the same asset.
        var scheduleByAsset = PmScheduleCodes(today);
        foreach (var row in rows.Where(row => row.WorkType == "Preventive" && row.Source != "PM Schedule"))
        {
            if (scheduleByAsset.TryGetValue(row.AssetId, out var code))
            {
                row.Source = "PM Schedule";
                row.SourceReference = code;
            }
        }

        // Codes in creation order, numbered per year: WO-2026-001, ...
        var counters = new Dictionary<int, int>();
        foreach (var row in rows.OrderBy(row => row.CreatedAt))
        {
            var year = row.CreatedAt!.Value.Year;
            counters[year] = counters.GetValueOrDefault(year) + 1;
            row.WorkOrderId = $"WO-{year}-{counters[year]:000}";
        }

        return rows;
    }

    private static string TitleFor(string type) => type switch
    {
        "Preventive" => "Scheduled preventive maintenance",
        "Corrective" => "Breakdown repair",
        "Predictive" => "Condition-based intervention",
        "Inspection" => "Statutory inspection",
        _ => "Service"
    };

    private static WorkOrder BuildWorkOrder(SeedAsset asset, string type, string title, string priority, string status,
        DateTime created, DateTime due, string technician, bool finishedLate)
    {
        var hours = type == "Inspection" ? 2 : type == "Corrective" ? 4 : 6;
        var start = due.Date.AddHours(9);
        var order = Audit(new WorkOrder
        {
            AssetId = asset.Tag,
            AssetName = asset.Name,
            WorkType = type,
            Title = title,
            Description = $"{TitleFor(type)} on {asset.Name} ({asset.Tag}).",
            Priority = priority,
            Category = type == "Corrective" ? "Breakdown" : type == "Inspection" ? "Statutory" : "Routine",
            ScheduledDate = due.Date,
            DueDate = due.Date,
            PlannedStartDate = start,
            PlannedEndDate = start.AddHours(hours),
            EstimatedHours = hours,
            AssignedTechnician = status == "Open" ? null : technician,
            Source = type == "Predictive" ? "Predictive Alert" : type == "Corrective" ? "Service Request" : "Manual",
            Site = asset.Site,
            Building = asset.Building,
            Area = asset.Area,
            Department = asset.Department,
            Team = asset.Department == "IT" ? "IT Infrastructure Team" : asset.Department == "Security" ? "Security Systems Team" : "Electrical Team"
        }, status, created);

        // Work already started or finished cannot have actual dates in the future.
        var now = DateTime.UtcNow;
        var actualStart = start.AddMinutes(15) <= now ? start.AddMinutes(15) : created.AddHours(2);
        if (status is "In Progress" or "Completed")
        {
            order.ActualStartDate = actualStart;
        }

        if (status == "Completed")
        {
            var actualEnd = actualStart.AddHours(hours).AddDays(finishedLate ? 3 : 0);
            order.ActualEndDate = actualEnd <= now ? actualEnd : actualStart.AddHours(hours);
            order.Progress = 100;
            order.Labour = [new WorkOrderLabour { Technician = technician, Role = "Technician", Hours = hours, HourlyRate = 85 }];
        }
        else if (status == "In Progress")
        {
            order.Progress = 40;
        }

        order.History =
        [
            new WorkOrderHistoryEntry { At = created, By = SeedUser, Action = "Created", Details = $"Work order created with status {(status == "Completed" ? "Open" : status)}." }
        ];
        if (status == "Completed")
        {
            order.History.Add(new WorkOrderHistoryEntry { At = order.ActualEndDate!.Value, By = technician, Action = "Status Changed", Details = "Status changed from In Progress to Completed." });
        }

        return order;
    }

    /// <summary>WO for the Generator monthly PM, with the full detail shown in the work order panel.</summary>
    private static WorkOrder MockupLeadWorkOrder(DateTime today)
    {
        var asset = Asset("GEN-00045");
        var start = today.AddDays(-1).AddHours(9);
        var order = BuildWorkOrder(asset, "Preventive", "Generator 500 kVA - Monthly Preventive Maintenance", "High", "In Progress",
            today.AddDays(-8).AddHours(10), today.AddDays(1), "Ahmed Khan", false);
        order.Description = "Perform monthly preventive maintenance and inspection as per manufacturer guidelines.";
        order.PlannedStartDate = start;
        order.PlannedEndDate = start.AddHours(8);
        order.ActualStartDate = start.AddMinutes(15);
        order.EstimatedHours = 6;
        order.Department = "Finance Department";
        order.Source = "PM Schedule";
        order.SourceReference = "PM-" + today.Year + "-001";
        order.Notes = "Monthly preventive maintenance as per Cummins service manual. Check oil, filters, coolant, battery, belts, and perform load test.";
        order.Tasks =
        [
            new WorkOrderTask { Title = "Check engine oil level and quality", AssignedTo = "Ahmed Khan", IsDone = true, CompletedAt = start.AddHours(1) },
            new WorkOrderTask { Title = "Replace fuel and air filters", AssignedTo = "Ahmed Khan", IsDone = true, CompletedAt = start.AddHours(2) },
            new WorkOrderTask { Title = "Inspect coolant level and hoses", AssignedTo = "Salim Ahmed", IsDone = true, CompletedAt = start.AddHours(3) },
            new WorkOrderTask { Title = "Test battery and charging system", AssignedTo = "Salim Ahmed" },
            new WorkOrderTask { Title = "Run 30-minute load test", AssignedTo = "Ahmed Khan" }
        ];
        order.Progress = 60;
        order.Parts =
        [
            new WorkOrderPart { PartNumber = "CUM-FF5052", PartName = "Fuel Filter", Quantity = 2, UnitCost = 45 },
            new WorkOrderPart { PartNumber = "CUM-AF25454", PartName = "Air Filter Element", Quantity = 1, UnitCost = 120 }
        ];
        order.Labour =
        [
            new WorkOrderLabour { Technician = "Ahmed Khan", Role = "Lead Technician", Hours = 4, HourlyRate = 95 },
            new WorkOrderLabour { Technician = "Salim Ahmed", Role = "Electrician", Hours = 3, HourlyRate = 85 },
            new WorkOrderLabour { Technician = "Ramesh Kumar", Role = "Helper", Hours = 2, HourlyRate = 45 }
        ];
        order.AdditionalCost = 150;
        order.Documents =
        [
            new WorkOrderDocument { Name = "Cummins C500D5 Service Manual.pdf", FileType = "PDF", UploadedBy = SeedUser, UploadedAt = today.AddDays(-8) },
            new WorkOrderDocument { Name = "Monthly PM Checklist.xlsx", FileType = "XLSX", UploadedBy = SeedUser, UploadedAt = today.AddDays(-8) },
            new WorkOrderDocument { Name = "Load Test Report Template.docx", FileType = "DOCX", UploadedBy = "Ahmed Khan", UploadedAt = today.AddDays(-2) }
        ];
        order.History =
        [
            new WorkOrderHistoryEntry { At = today.AddDays(-8).AddHours(10), By = SeedUser, Action = "Created", Details = $"Work order created from PM Schedule PM-{today.Year}-001." },
            new WorkOrderHistoryEntry { At = today.AddDays(-7).AddHours(11), By = SeedUser, Action = "Updated", Details = "Assigned to Ahmed Khan." },
            new WorkOrderHistoryEntry { At = start.AddMinutes(15), By = "Ahmed Khan", Action = "Status Changed", Details = "Status changed from Scheduled to In Progress." }
        ];
        return order;
    }

    // ------------------------------------------------------------------ service requests

    public static IReadOnlyList<IssueReport> ServiceRequests(DateTime today)
    {
        var random = new Random(4212);
        var rows = new List<IssueReport>();
        var types = new (string Type, string Category)[]
        {
            ("Repair Request", "Equipment Issue"), ("Repair Request", "Equipment Issue"), ("Service Request", "Equipment Issue"),
            ("Service Request", "Facility Issue"), ("Spare Part Request", "Spare Part Request"), ("Inspection Request", "Inspection Request"),
            ("Service Request", "Other")
        };
        var priorities = new[] { "High", "Medium", "Medium", "Low" };
        var historyAssets = Assets.Take(10).ToArray();

        for (var monthsBack = HistoryMonths; monthsBack >= 1; monthsBack--)
        {
            var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-monthsBack);
            var count = 3 + random.Next(6);
            for (var i = 0; i < count; i++)
            {
                var asset = historyAssets[random.Next(historyAssets.Length)];
                var (type, category) = types[random.Next(types.Length)];
                var requested = monthStart.AddDays(random.Next(26)).AddHours(9);
                rows.Add(BuildRequest(asset, type, category, priorities[random.Next(priorities.Length)],
                    random.Next(100) < 90 ? "Resolved" : "Closed", requested, requested.AddDays(4 + random.Next(5)),
                    Technicians[random.Next(Technicians.Length)]));
            }
        }

        DateTime D(int offset) => today.AddDays(offset).AddHours(9);
        rows.Add(BuildRequest(Asset("GEN-00045"), "Repair Request", "Equipment Issue", "High", "In Progress", D(-8), D(-4), "Ahmed Khan", "Generator trips on load above 70%."));
        rows.Add(BuildRequest(Asset("AHU-00021"), "Spare Part Request", "Spare Part Request", "Medium", "Open", D(-7), D(-1), "Priya Nair", "Need replacement V-belt set for supply fan."));
        rows.Add(BuildRequest(Asset("FL-00078"), "Inspection Request", "Inspection Request", "High", "Open", D(-11), D(-7), "Ramesh Kumar", "Annual lifting equipment inspection due."));
        rows.Add(BuildRequest(Asset("UPS-00112"), "Service Request", "Equipment Issue", "Medium", "Resolved", D(-14), D(-10), "Salim Ahmed", "UPS showing battery fault alarm."));
        rows.Add(BuildRequest(Asset("FP-00033"), "Repair Request", "Equipment Issue", "Low", "In Progress", D(-5), D(1), "Nisha Patel", "Minor seal leak at pump gland."));
        rows.Add(BuildRequest(Asset("CAM-00089"), "Service Request", "Facility Issue", "High", "Open", D(-13), D(-9), "Khalid Mansoori", "Camera at main entrance not recording at night."));
        rows.Add(BuildRequest(Asset("SW-00176"), "Spare Part Request", "Spare Part Request", "Medium", "Open", D(-6), D(0), "Arjun Mehta", "Spare SFP modules required for uplink ports."));
        rows.Add(BuildRequest(Asset("CH-00012"), "Inspection Request", "Inspection Request", "Low", "Resolved", D(-19), D(-15), "Priya Nair", "Refrigerant leak check after shutdown."));
        rows.Add(BuildRequest(Asset("ACP-00056"), "Service Request", "Facility Issue", "Medium", "In Progress", D(-4), D(2), "Ahmed Khan", "Card reader at gate 2 intermittently offline."));
        rows.Add(BuildRequest(Asset("AC-00100"), "Repair Request", "Equipment Issue", "High", "Open", D(-3), D(3), "Ramesh Kumar", "Compressor overheating during second shift."));

        var counters = new Dictionary<int, int>();
        foreach (var row in rows.OrderBy(row => row.RequestedDate))
        {
            var year = row.RequestedDate!.Value.Year;
            counters[year] = counters.GetValueOrDefault(year) + 1;
            row.IssueId = $"SR-{year}-{counters[year]:000}";
        }

        return rows;
    }

    private static IssueReport BuildRequest(SeedAsset asset, string type, string category, string priority, string status,
        DateTime requested, DateTime target, string requestedBy, string? description = null)
    {
        var request = Audit(new IssueReport
        {
            AssetId = asset.Tag,
            AssetName = asset.Name,
            IssueType = type,
            Category = category,
            Severity = priority,
            ReportedBy = requestedBy,
            Description = description ?? $"{type} raised for {asset.Name}.",
            Location = asset.Site,
            RequestedDate = requested,
            TargetDate = target.Date,
            AssignedTo = status == "Open" ? null : Technicians[(asset.Tag.Length + requestedBy.Length) % Technicians.Length]
        }, status, requested, requestedBy);

        if (status is "Resolved" or "Closed")
        {
            request.ResolvedDate = target.AddDays(-1);
            request.ResolutionNotes = "Issue fixed and verified with requester.";
        }

        return request;
    }

    // ------------------------------------------------------------------ preventive maintenance

    private static readonly (string Name, string Tag, string Type, string Frequency, int NextOffset, int LastOffset, string Status)[] PmRows =
    [
        ("Generator PM", "GEN-00045", "Time Based", "Monthly", 1, -29, "Active"),
        ("AHU Filter Cleaning", "AHU-00021", "Time Based", "Quarterly", 6, -86, "Active"),
        ("Forklift Service", "FL-00078", "Usage Based", "Every 250 Hours", -4, -42, "Active"),
        ("UPS Inspection", "UPS-00112", "Time Based", "Monthly", 3, -27, "Active"),
        ("Fire Pump Test", "FP-00033", "Calendar Based", "Quarterly", 11, -81, "Active"),
        ("CCTV Camera Check", "CAM-00089", "Condition Based", "Monthly", -9, -40, "Active"),
        ("Network Switch PM", "SW-00176", "Time Based", "Quarterly", 9, -83, "Active"),
        ("Chiller Plant Service", "CH-00012", "Time Based", "Bi-Annual", 2, -181, "Active"),
        ("Access Control Panel", "ACP-00056", "Time Based", "Monthly", -2, -32, "Active"),
        ("Air Compressor", "AC-00100", "Usage Based", "Every 500 Hours", 11, -50, "Active")
    ];

    private static Dictionary<string, string> PmScheduleCodes(DateTime today) =>
        PmRows.Select((row, index) => (row.Tag, Code: $"PM-{today.Year}-{index + 1:000}"))
            .ToDictionary(pair => pair.Tag, pair => pair.Code);

    public static IReadOnlyList<PreventiveMaintenance> PmSchedules(DateTime today) =>
        PmRows.Select((row, index) =>
        {
            var asset = Asset(row.Tag);
            return Audit(new PreventiveMaintenance
            {
                PmScheduleId = $"PM-{today.Year}-{index + 1:000}",
                ScheduleName = row.Name,
                AssetId = asset.Tag,
                AssetName = asset.Name,
                AssetType = asset.Type,
                Location = asset.Site,
                Frequency = row.Frequency,
                TriggerType = row.Type,
                NextDueDate = today.AddDays(row.NextOffset),
                LastMaintenanceDate = today.AddDays(row.LastOffset),
                AutoCreateWorkOrder = row.Type != "Condition Based",
                Priority = index % 3 == 0 ? "High" : "Medium",
                AssignedTo = Technicians[index % Technicians.Length],
                EstimatedHours = 2 + index % 5,
                Description = $"{row.Frequency} {row.Name.ToLowerInvariant()} as per manufacturer guidelines.",
                Checklist = ["Isolate and lock out equipment", "Visual inspection for wear, leaks and damage", "Clean and lubricate as required", "Functional test and record readings"]
            }, row.Status, today.AddMonths(-HistoryMonths));
        }).ToList();

    // ------------------------------------------------------------------ predictive maintenance

    public static IReadOnlyList<PredictiveMaintenance> Predictions(DateTime today)
    {
        DateTime D(int offset) => today.AddDays(offset);
        var rows = new List<PredictiveMaintenance>
        {
            Prediction("CH-00012", "Vibration", 7.1, 9.4, 32, "High", "Compressor Failure", D(6), 92, "Schedule maintenance", "Vibration Anomaly v2", "Alert"),
            Prediction("GEN-00045", "Vibration", 6.5, 7.8, 45, "High", "Bearing Wear", D(9), 87, "Inspect bearings", "Vibration Anomaly v2", "Alert"),
            Prediction("AHU-00021", "Current", 18, 17.2, 58, "At Risk", "Fan Motor Issue", D(16), 78, "Check motor alignment", "Motor Current Signature v1", "Monitoring"),
            Prediction("UPS-00112", "Voltage", 11.5, 11.7, 61, "At Risk", "Battery Degradation", D(32), 75, "Plan battery replacement", "Battery Health v3", "Monitoring"),
            Prediction("FL-00078", "Pressure", 180, 168, 35, "High", "Hydraulic Pump Failure", D(3), 88, "Schedule service", "Hydraulic Pressure v1", "Alert"),
            Prediction("FP-00033", "Pressure", 6.5, 6.9, 67, "At Risk", "Seal Wear", D(27), 72, "Prepare spare parts", "Hydraulic Pressure v1", "Monitoring"),
            Prediction("CAM-00089", "Temperature", 70, 41, 78, "Healthy", null, null, null, null, "Thermal Degradation v1", "Monitoring"),
            Prediction("SW-00176", "Temperature", 65, 38, 81, "Healthy", null, null, null, null, "Thermal Degradation v1", "Monitoring"),
            Prediction("ACP-00056", "Voltage", 22, 22.8, 69, "At Risk", "Power Supply Issue", D(44), 70, "Check power module", "Battery Health v3", "Monitoring"),
            Prediction("AC-00100", "Vibration", 7.1, 8.2, 40, "High", "Vibration Anomaly", D(19), 85, "Inspect vibration isolators", "Vibration Anomaly v2", "Alert"),
            Prediction("HVAC-00201", "Temperature", 55, 58, 48, "High", "Condenser Fouling", D(12), 83, "Clean condenser coils", "Thermal Degradation v1", "Alert"),
            Prediction("HVAC-00202", "Current", 22, 20.5, 62, "At Risk", "Compressor Overload", D(25), 74, "Check refrigerant charge", "Motor Current Signature v1", "Monitoring"),
            Prediction("HVAC-00203", "Temperature", 55, 44, 74, "Healthy", null, null, null, null, "Thermal Degradation v1", "Monitoring"),
            Prediction("ELV-00011", "Vibration", 4.5, 4.9, 57, "At Risk", "Guide Roller Wear", D(21), 76, "Inspect guide rollers", "Vibration Anomaly v2", "Monitoring"),
            Prediction("GEN-00052", "Temperature", 95, 88, 71, "Healthy", null, null, null, null, "Thermal Degradation v1", "Monitoring"),
            Prediction("UPS-00118", "Voltage", 11.5, 12.1, 84, "Healthy", null, null, null, null, "Battery Health v3", "Monitoring"),
            Prediction("CH-00014", "Vibration", 7.1, 6.2, 66, "At Risk", "Shaft Misalignment", D(38), 71, "Laser-align compressor shaft", "Vibration Anomaly v2", "Monitoring"),
            Prediction("AC-00104", "Oil Quality", 250, 120, 88, "Healthy", null, null, null, null, "Oil Analysis v1", "Monitoring")
        };

        // Past predictions with outcomes, for the trend chart and model accuracy.
        var random = new Random(4213);
        var historyAssets = Assets.Take(10).ToArray();
        var models = new[] { "Vibration Anomaly v2", "Battery Health v3", "Thermal Degradation v1", "Hydraulic Pressure v1" };
        var issues = new[] { "Bearing Wear", "Battery Degradation", "Overheating", "Seal Wear", "Belt Wear" };
        for (var monthsBack = HistoryMonths; monthsBack >= 1; monthsBack--)
        {
            var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-monthsBack);
            var count = 1 + random.Next(4);
            for (var i = 0; i < count; i++)
            {
                var asset = historyAssets[random.Next(historyAssets.Length)];
                var predicted = monthStart.AddDays(random.Next(26));
                var happened = random.Next(100) < 70;
                var row = Prediction(asset.Tag, "Vibration", 7.1, 6.0, 70 + random.Next(20), "Healthy", issues[random.Next(issues.Length)],
                    predicted, 70 + random.Next(25), "Resolved by planned maintenance", models[random.Next(models.Length)], "Resolved");
                row.ActualFailureDate = happened ? predicted.AddDays(random.Next(-5, 6)) : null;
                row.CreatedAt = predicted.AddDays(-20);
                rows.Add(row);
            }
        }

        return rows;
    }

    private static PredictiveMaintenance Prediction(string tag, string sensor, double threshold, double reading, double health,
        string risk, string? issue, DateTime? predictedDate, double? confidence, string? action, string model, string status)
    {
        var asset = Asset(tag);
        var now = DateTime.UtcNow;
        var prediction = Audit(new PredictiveMaintenance
        {
            AssetId = asset.Tag,
            AssetName = asset.Name,
            AssetType = asset.Type,
            Location = asset.Site,
            SensorType = sensor,
            ThresholdValue = threshold,
            AlertCondition = sensor is "Voltage" or "Pressure" ? "Below Threshold" : "Above Threshold",
            DataSourceDeviceId = $"{sensor[..3].ToUpperInvariant()}-SNS-{asset.Tag[^3..]}",
            LastReading = reading,
            LastReadingAt = now.AddMinutes(-15),
            ModelName = model,
            RiskLevel = risk,
            HealthScore = health,
            PredictedIssue = issue,
            PredictedFailureDate = predictedDate,
            Confidence = confidence,
            RecommendedAction = action,
            PredictionModelOutput = issue is null
                ? "No degradation pattern detected."
                : $"{issue} likely by {predictedDate:dd MMM yyyy} (confidence {confidence}%)."
        }, status, now.AddDays(-30));

        if (risk != "Healthy")
        {
            prediction.Anomalies =
            [
                new PredictiveAnomaly { DetectedAt = now.AddDays(-2).AddHours(-3), Metric = sensor, Value = reading, Expected = $"{(prediction.AlertCondition == "Below Threshold" ? ">" : "<")} {threshold}", Severity = risk == "High" ? "High" : "Medium", Note = $"{sensor} outside normal band." },
                new PredictiveAnomaly { DetectedAt = now.AddDays(-6), Metric = sensor, Value = Math.Round(reading * 0.95, 1), Expected = $"{(prediction.AlertCondition == "Below Threshold" ? ">" : "<")} {threshold}", Severity = "Medium", Note = "Upward drift over 7 days." }
            ];
        }

        return prediction;
    }
}
