using A360.Domain.Entities;
using A360.Inspection.Domain.Entities;
using InspectionEntity = A360.Inspection.Domain.Entities.Inspection;

namespace A360.Inspection.Api.Seed;

/// <summary>
/// Sample data for the Inspection module, taken from the design mockups (Dubai Manufacturing Plant,
/// October 2026). Codes and cross-references are kept consistent across collections.
/// </summary>
public static class InspectionSeedData
{
    public const int SeedYear = 2026;

    private const string ClientId = "CLT-1001";
    private const string TenantId = "TENANT-DEFAULT";
    private const string SeedUser = "system";
    private const string Site = "Dubai Manufacturing Plant";

    private static readonly DateTime SeededAt = new(2026, 10, 1, 6, 0, 0, DateTimeKind.Utc);

    private static T Audit<T>(T entity, string status, DateTime? updatedAt = null, string? updatedBy = null)
        where T : BaseEntity
    {
        entity.ClientId = ClientId;
        entity.TenantId = TenantId;
        entity.CreatedBy = SeedUser;
        entity.CreatedAt = SeededAt;
        entity.UpdatedAt = updatedAt;
        entity.UpdatedBy = updatedBy;
        entity.Status = status;
        entity.IsDeleted = false;
        return entity;
    }

    private static DateTime Utc(int month, int day, int hour = 0, int minute = 0) => new(SeedYear, month, day, hour, minute, 0, DateTimeKind.Utc);

    // ---------------------------------------------------------------- inspection types

    public static IReadOnlyList<InspectionType> InspectionTypes()
    {
        InspectionType Type(string code, string name, string category, string description, string checklistCode, string checklistName,
            string priority, string frequency, string assets, string role, bool photo, bool signature, bool corrective, bool permit, bool active = true)
        {
            return Audit(new InspectionType
            {
                InspectionTypeCode = code,
                InspectionTypeName = name,
                Category = category,
                Description = description,
                DefaultChecklistCode = checklistCode,
                DefaultChecklistName = checklistName,
                DefaultPriority = priority,
                DefaultFrequency = frequency,
                ApplicableAssets = assets,
                ResponsibleRole = role,
                PhotoEvidenceOnFailure = photo,
                SignatureRequired = signature,
                CorrectiveActionOnFailure = corrective,
                PermitRequired = permit,
                IsActive = active
            }, active ? "Active" : "Inactive");
        }

        return
        [
            Type("IT-001", "Safety Inspection", "Safety", "Routine safety inspection of production equipment covering protective devices, guarding and safe operation.",
                "CHK-001", "Equipment Safety v3.0", "High", "Monthly", "All equipment", "Safety Inspector", true, true, true, false),
            Type("IT-002", "Condition Inspection", "Condition", "Condition assessment of rotating and utility equipment to detect wear, vibration and leaks early.",
                "CHK-002", "Condition Assessment v2.1", "Medium", "Weekly", "Rotating equipment", "Reliability Engineer", true, false, true, false),
            Type("IT-003", "Statutory Inspection", "Regulatory", "Legally required inspection of pressure, lifting and fire-safety equipment by a certified inspector.",
                "", "Statutory Compliance v4", "Critical", "Quarterly", "Pressure & lifting equipment", "Certified Inspector", true, true, true, true),
            Type("IT-004", "Pre-use Inspection", "Operational", "Operator checks performed before each shift on mobile equipment.",
                "CHK-003", "Pre-use Forklift Checks v2.0", "High", "Daily", "Mobile equipment", "Equipment Operator", true, false, true, false),
            Type("IT-005", "Quality Inspection", "Quality", "Verification of production line settings and product quality controls.",
                "", "Quality Assurance v1", "Medium", "Weekly", "Production lines", "Quality Inspector", true, true, false, false),
            Type("IT-006", "Environmental Inspection", "Environmental", "Inspection of emissions, waste storage and spill controls across site facilities.",
                "", "Environmental Checks v1", "Low", "Quarterly", "Site facilities", "EHS Officer", false, false, true, false, active: false),
            Type("IT-007", "Electrical Inspection", "Electrical", "Inspection of electrical panels, isolation and earthing arrangements.",
                "CHK-005", "Electrical Safety v1.0", "High", "Quarterly", "Electrical panels", "Electrical Engineer", true, true, true, true),
            Type("IT-008", "Asset Verification", "Audit", "Physical verification of asset tags, location and custody for audit.",
                "CHK-004", "Physical Asset Verification v1.4", "Low", "Annually", "All assets", "Asset Controller", false, true, false, false)
        ];
    }

    // ---------------------------------------------------------------- task categories

    public static IReadOnlyList<TaskCategory> TaskCategories()
    {
        TaskCategory Category(string code, string name, string description, int order) =>
            Audit(new TaskCategory { CategoryCode = code, CategoryName = name, Description = description, DisplayOrder = order }, "Active");

        return
        [
            Category("TC-001", "Safety", "Protective devices, guarding, emergency stops and safety signage.", 1),
            Category("TC-002", "Mechanical", "Bearings, belts, lubrication, leaks and mechanical wear.", 2),
            Category("TC-003", "Condition", "Condition monitoring readings such as vibration and noise.", 3),
            Category("TC-004", "Electrical", "Electrical connections, panels, earthing and isolation.", 4),
            Category("TC-005", "Instrumentation", "Gauges, sensors and instrument readings.", 5),
            Category("TC-006", "Compliance", "Statutory, documentation and regulatory checks.", 6)
        ];
    }

    // ---------------------------------------------------------------- inspection tasks

    public static IReadOnlyList<InspectionTask> InspectionTasks()
    {
        InspectionTask Task(string code, string title, string category, string description, string responseType, string unit, int minutes,
            double? min, double? max, string procedure, string severity, bool reading = false, bool photo = true, bool finding = true,
            bool critical = false, bool mandatory = true)
        {
            return Audit(new InspectionTask
            {
                TaskCode = code,
                TaskTitle = title,
                TaskCategory = category,
                TaskDescription = description,
                ResponseType = responseType,
                Unit = unit,
                EstimatedDurationMinutes = minutes,
                MinValue = min,
                MaxValue = max,
                Procedure = procedure,
                ReadingRequired = reading,
                PhotoRequiredOnFailure = photo,
                CreateFindingOutsideLimits = finding,
                DefaultSeverity = severity,
                IsCriticalTask = critical,
                IsMandatory = mandatory,
                IsActive = true
            }, "Active");
        }

        return
        [
            Task("ITK-001", "Check emergency stop operation", "Safety", "Confirm every emergency stop halts the machine and needs a reset to restart.", "Pass / Fail", "", 2,
                null, null, "Press each emergency stop button with the machine running and confirm all motion stops and cannot restart until reset.", "Critical", critical: true),
            Task("ITK-002", "Inspect guards and interlocks", "Safety", "Check fixed guards are secure and interlocked guards stop the machine when opened.", "Pass / Fail", "", 3,
                null, null, "Open each interlocked guard and confirm the machine stops; check fixed guards are complete and fastened.", "High", critical: true),
            Task("ITK-003", "Measure bearing temperature", "Mechanical", "Record bearing temperature at the marked measurement point.", "Numeric", "°C", 3,
                0, 80, "Use a calibrated temperature probe at the marked bearing measurement point while the machine is operating normally.", "High", reading: true),
            Task("ITK-004", "Check for oil leakage", "Mechanical", "Inspect seals, fittings and the drip tray for oil leakage.", "Yes / No", "", 2,
                null, null, "Inspect around seals, hoses and fittings and the drip tray. Answer 'No' when no leakage is observed.", "Medium"),
            Task("ITK-005", "Record vibration level", "Condition", "Record overall vibration velocity at the drive-end bearing.", "Numeric", "mm/s", 4,
                0, 4.5, "Take an overall vibration velocity reading at the drive-end bearing housing with the portable analyser.", "High", reading: true),
            Task("ITK-006", "Inspect electrical connections", "Electrical", "Check terminals and cables for looseness, heat damage or wear.", "Pass / Fail", "", 5,
                null, null, "With the panel isolated, check terminals for tightness and look for discolouration or damaged insulation.", "High"),
            Task("ITK-007", "Verify pressure gauge reading", "Instrumentation", "Record the hydraulic system pressure shown on the gauge.", "Numeric", "bar", 2,
                0, 10, "Read the hydraulic pressure gauge at normal operating load and record the value.", "Medium", reading: true),
            Task("ITK-008", "Verify warning labels", "Safety", "Check safety and warning labels are present and legible.", "Pass / Fail", "", 2,
                null, null, "Walk around the machine and confirm all warning and instruction labels are present, legible and undamaged.", "Low", photo: false),
            Task("ITK-009", "Check belt tension and condition", "Mechanical", "Check drive belts for correct tension, cracking and glazing.", "Pass / Fail", "", 4,
                null, null, "With the drive isolated, check belt deflection against the specification and inspect for cracks or glazing.", "Medium"),
            Task("ITK-010", "Test earth continuity", "Electrical", "Measure protective earth continuity resistance.", "Numeric", "Ω", 5,
                0, 1, "Measure resistance between the machine frame and the main earth terminal with a calibrated low-resistance ohmmeter.", "High", reading: true),
            Task("ITK-011", "Check panel cooling fans", "Electrical", "Confirm panel cooling fans run and filters are clean.", "Pass / Fail", "", 2,
                null, null, "Confirm cooling fans are running and filters are clean and unobstructed.", "Low", photo: false, mandatory: false),
            Task("ITK-012", "Verify control panel indicators", "Electrical", "Confirm control panel lamps and HMI alarms work.", "Pass / Fail", "", 2,
                null, null, "Run the lamp test and confirm all indicators and HMI alarm messages display correctly.", "Low", photo: false)
        ];
    }

    // ---------------------------------------------------------------- checklists

    private sealed record ItemSpec(string Title, string ResponseType, double Weight, string Criteria, string TaskCode = "", bool Mandatory = true);

    public static IReadOnlyList<Checklist> Checklists(IReadOnlyDictionary<string, string> typeIds, IReadOnlyDictionary<string, string> taskIds)
    {
        ChecklistSection Section(string name, int sectionOrder, params ItemSpec[] items)
        {
            return new ChecklistSection
            {
                SectionName = name,
                Order = sectionOrder,
                Tasks = items.Select(item => new ChecklistTaskItem
                {
                    TaskId = item.TaskCode.Length > 0 && taskIds.TryGetValue(item.TaskCode, out var id) ? id : string.Empty,
                    TaskCode = item.TaskCode,
                    TaskTitle = item.Title,
                    ResponseType = item.ResponseType,
                    Weight = item.Weight,
                    IsMandatory = item.Mandatory,
                    AcceptanceCriteria = item.Criteria
                }).ToList()
            };
        }

        Checklist Make(string code, string name, string typeName, string assetCategory, string assetType, string[] modules, string owner,
            int version, string versionLabel, string effectiveDate, string status, DateTime updatedAt, params ChecklistSection[] sections)
        {
            // Task numbers run across the whole checklist (1..n), in section order.
            var number = 0;
            foreach (var task in sections.SelectMany(section => section.Tasks))
            {
                task.Order = ++number;
            }

            var published = status == "Published";
            return Audit(new Checklist
            {
                ChecklistCode = code,
                TemplateName = name,
                InspectionTypeId = typeIds.TryGetValue(typeName, out var typeId) ? typeId : string.Empty,
                InspectionTypeName = typeName,
                AssetCategory = assetCategory,
                AssetType = assetType,
                Modules = modules.ToList(),
                Scoring = "Weighted",
                Owner = owner,
                Version = version,
                VersionLabel = versionLabel,
                EffectiveDate = DateTime.SpecifyKind(DateTime.Parse(effectiveDate), DateTimeKind.Utc),
                Sections = sections.ToList(),
                AllMandatoryRequired = true,
                PhotoOnFailedItems = true,
                InspectorSignatureRequired = published,
                CriticalFailureOverridesScore = true,
                CreateFindingOnFailure = true,
                RouteCorrectiveAction = published,
                IsActive = true
            }, status, updatedAt, "Sarah Ahmed");
        }

        const string PassFail = "Pass / Fail";
        const string YesNo = "Yes / No";
        const string Numeric = "Numeric";

        var checklists = new List<Checklist>();

        checklists.Add(Make("CHK-001", "Equipment Safety", "Safety Inspection", "Production Equipment", "CNC Machine", ["Inspection", "Maintenance"], "Safety Team",
            3, "3.0", "2026-10-18", "Published", Utc(10, 18, 9, 30),
            Section("Safety & Protection", 1,
                new ItemSpec("Check emergency stop operation", PassFail, 15, "-", "ITK-001"),
                new ItemSpec("Inspect guards and interlocks", PassFail, 15, "-", "ITK-002"),
                new ItemSpec("Verify warning labels", PassFail, 10, "-", "ITK-008")),
            Section("Mechanical Condition", 2,
                new ItemSpec("Check belt tension and condition", PassFail, 6, "-", "ITK-009"),
                new ItemSpec("Verify pressure gauge reading", "Numeric (bar)", 6, "0 – 10 bar", "ITK-007"),
                new ItemSpec("Measure bearing temperature", "Numeric (°C)", 12, "0 – 80 °C", "ITK-003"),
                new ItemSpec("Check for oil leakage", YesNo, 8, "Expected: No", "ITK-004"),
                new ItemSpec("Record vibration level", "Numeric (mm/s)", 8, "0 – 4.5 mm/s", "ITK-005")),
            Section("Electrical Checks", 3,
                new ItemSpec("Inspect electrical connections", PassFail, 8, "-", "ITK-006"),
                new ItemSpec("Test earth continuity", "Numeric (Ω)", 6, "≤ 1 Ω", "ITK-010"),
                new ItemSpec("Check panel cooling fans", PassFail, 3, "-", "ITK-011", Mandatory: false),
                new ItemSpec("Verify control panel indicators", PassFail, 3, "-", "ITK-012"))));

        checklists.Add(Make("CHK-002", "Condition Assessment", "Condition Inspection", "Rotating Equipment", "Compressors & Pumps", ["Inspection"], "Reliability Team",
            2, "2.1", "2026-08-03", "Published", Utc(8, 3, 11, 0),
            Section("Vibration & Noise", 1,
                new ItemSpec("Record vibration level", Numeric, 8, "0 – 4.5 mm/s", "ITK-005"),
                new ItemSpec("Check for abnormal noise", PassFail, 5, "-"),
                new ItemSpec("Inspect coupling alignment", PassFail, 5, "-"),
                new ItemSpec("Check mounting bolts", PassFail, 4, "-"),
                new ItemSpec("Inspect anti-vibration mounts", PassFail, 4, "-"),
                new ItemSpec("Record motor current", Numeric, 6, "Within nameplate FLA")),
            Section("Lubrication & Wear", 2,
                new ItemSpec("Check for oil leakage", YesNo, 6, "Expected: No", "ITK-004"),
                new ItemSpec("Check lubricant level", PassFail, 5, "-"),
                new ItemSpec("Inspect oil condition", PassFail, 5, "-"),
                new ItemSpec("Check belt tension and condition", PassFail, 5, "-", "ITK-009"),
                new ItemSpec("Inspect shaft seals", PassFail, 5, "-"),
                new ItemSpec("Check filter differential pressure", Numeric, 5, "< 0.7 bar")),
            Section("Thermal & Electrical", 3,
                new ItemSpec("Measure bearing temperature", Numeric, 8, "0 – 80 °C", "ITK-003"),
                new ItemSpec("Measure motor casing temperature", Numeric, 6, "0 – 90 °C"),
                new ItemSpec("Verify pressure gauge reading", Numeric, 5, "0 – 10 bar", "ITK-007"),
                new ItemSpec("Inspect electrical connections", PassFail, 6, "-", "ITK-006"),
                new ItemSpec("Check panel cooling fans", PassFail, 4, "-", "ITK-011", Mandatory: false),
                new ItemSpec("Check cable glands and conduits", PassFail, 4, "-"))));

        checklists.Add(Make("CHK-003", "Pre-use Forklift Checks", "Pre-use Inspection", "Mobile Equipment", "Forklift", ["Inspection", "WIP"], "Operations Team",
            2, "2.0", "2026-06-15", "Published", Utc(6, 15, 8, 0),
            Section("Visual Walk-around", 1,
                new ItemSpec("Inspect tyres and wheels", PassFail, 12, "-"),
                new ItemSpec("Check forks and mast for damage", PassFail, 12, "-"),
                new ItemSpec("Check for hydraulic or oil leaks", YesNo, 10, "Expected: No"),
                new ItemSpec("Verify warning labels", PassFail, 8, "-", "ITK-008")),
            Section("Operational Tests", 2,
                new ItemSpec("Test service and parking brakes", PassFail, 14, "-"),
                new ItemSpec("Test horn and reversing alarm", PassFail, 10, "-"),
                new ItemSpec("Check lights and beacon", PassFail, 10, "-"),
                new ItemSpec("Test steering and lift controls", PassFail, 14, "-"),
                new ItemSpec("Check seat belt and operator restraint", PassFail, 10, "-"))));

        checklists.Add(Make("CHK-004", "Physical Asset Verification", "Asset Verification", "All Assets", "Fixed Assets", ["Audit"], "Finance & Audit",
            1, "1.4", "2026-03-01", "Published", Utc(3, 1, 10, 0),
            Section("Identification", 1,
                new ItemSpec("Asset tag present and legible", YesNo, 8, "Expected: Yes"),
                new ItemSpec("Tag matches register record", YesNo, 8, "Expected: Yes"),
                new ItemSpec("Serial number verified", YesNo, 7, "Expected: Yes"),
                new ItemSpec("Manufacturer and model match", YesNo, 6, "Expected: Yes"),
                new ItemSpec("Photo captured", YesNo, 5, "Expected: Yes")),
            Section("Location & Custody", 2,
                new ItemSpec("Asset at registered location", YesNo, 8, "Expected: Yes"),
                new ItemSpec("Custodian confirmed", YesNo, 7, "Expected: Yes"),
                new ItemSpec("Cost centre correct", YesNo, 6, "Expected: Yes"),
                new ItemSpec("Movement records up to date", YesNo, 6, "Expected: Yes"),
                new ItemSpec("Sub-assets accounted for", YesNo, 6, "Expected: Yes")),
            Section("Condition", 3,
                new ItemSpec("Overall condition rating", "Text", 7, "Good / Fair / Poor"),
                new ItemSpec("Asset in use", YesNo, 6, "-"),
                new ItemSpec("Visible damage", YesNo, 6, "Expected: No"),
                new ItemSpec("Candidate for disposal", YesNo, 5, "-", Mandatory: false),
                new ItemSpec("Remarks", "Text", 9, "-", Mandatory: false))));

        checklists.Add(Make("CHK-005", "Electrical Safety", "Electrical Inspection", "Electrical Equipment", "Distribution Panel", ["Inspection"], "Electrical Team",
            1, "1.0", "2026-11-01", "Draft", Utc(10, 27, 14, 0),
            Section("Isolation & Earthing", 1,
                new ItemSpec("Verify lock-out / tag-out applied", PassFail, 12, "-"),
                new ItemSpec("Prove dead before work", PassFail, 12, "-"),
                new ItemSpec("Test earth continuity", Numeric, 10, "≤ 1 Ω", "ITK-010"),
                new ItemSpec("Measure insulation resistance", Numeric, 10, "≥ 1 MΩ"),
                new ItemSpec("Test RCD trip time", Numeric, 8, "≤ 300 ms")),
            Section("Panels & Wiring", 2,
                new ItemSpec("Inspect electrical connections", PassFail, 10, "-", "ITK-006"),
                new ItemSpec("Thermographic scan of terminals", PassFail, 10, "ΔT < 10 °C"),
                new ItemSpec("Check panel cooling fans", PassFail, 6, "-", "ITK-011", Mandatory: false),
                new ItemSpec("Verify control panel indicators", PassFail, 6, "-", "ITK-012"),
                new ItemSpec("Check panel labelling and schedules", PassFail, 6, "-"))));

        return checklists;
    }

    // ---------------------------------------------------------------- inspection plans

    public static IReadOnlyList<InspectionPlan> InspectionPlans()
    {
        InspectionPlan Plan(string code, string name, string typeName, string category, string frequency, int assets, string nextDue,
            string inspector, string checklistCode, string checklistName, int leadTime, bool autoGenerate, bool active = true)
        {
            return Audit(new InspectionPlan
            {
                PlanCode = code,
                PlanName = name,
                InspectionTypeName = typeName,
                Category = category,
                Frequency = frequency,
                AssetCount = assets,
                NextDueDate = nextDue,
                InspectorName = inspector,
                ChecklistCode = checklistCode,
                ChecklistName = checklistName,
                LeadTimeDays = leadTime,
                AutoGenerate = autoGenerate,
                IsActive = active
            }, active ? "Active" : "Inactive");
        }

        return
        [
            Plan("PLN-001", "Monthly Equipment Safety", "Safety Inspection", "Safety", "Monthly", 12, "2026-10-22", "Ali Khan", "CHK-001", "Equipment Safety v3.0", 3, true),
            Plan("PLN-002", "Weekly Compressor Check", "Condition Inspection", "Condition", "Weekly", 4, "2026-10-23", "Priya Nair", "CHK-002", "Condition Assessment v2.1", 2, true),
            Plan("PLN-003", "Quarterly Statutory Inspection", "Statutory Inspection", "Statutory", "Quarterly", 6, "2026-10-28", "Omar Hassan", "", "Statutory Compliance v4", 14, true),
            Plan("PLN-004", "Forklift Pre-use Checks", "Pre-use Inspection", "Pre-use", "Daily", 8, "2026-10-21", "Operations Team", "CHK-003", "Pre-use Forklift Checks v2.0", 0, true),
            Plan("PLN-005", "Monthly HVAC Condition Check", "Condition Inspection", "Condition", "Monthly", 5, "2026-11-08", "Fatima Al Mansoori", "CHK-002", "Condition Assessment v2.1", 3, true),
            Plan("PLN-006", "Annual Electrical Safety Audit", "Electrical Inspection", "Electrical", "Annually", 10, "2026-12-15", "Rajesh Kumar", "CHK-005", "Electrical Safety v1.0", 14, false, active: false)
        ];
    }

    // ---------------------------------------------------------------- inspections

    private sealed record Person(string Name, string Team);

    private static readonly Person AliKhan = new("Ali Khan", "Maintenance");
    private static readonly Person PriyaNair = new("Priya Nair", "Reliability");
    private static readonly Person OmarHassan = new("Omar Hassan", "EHS");
    private static readonly Person ChenWei = new("Chen Wei", "Operations");
    private static readonly Person FatimaAlMansoori = new("Fatima Al Mansoori", "Facilities");
    private static readonly Person RajeshKumar = new("Rajesh Kumar", "Inspection");

    public static IReadOnlyList<InspectionEntity> Inspections()
    {
        InspectionEntity Make(int number, string title, string typeName, string typeShort, string priority, string assetCode, string assetName,
            string location, Person inspector, string dueDate, string start, string end, string status, int findings,
            string planCode = "", string planName = "", string checklistCode = "", string checklistName = "", string department = "Maintenance & Engineering",
            string description = "", string category = "Internal", string referenceStandard = "", bool permit = false, bool evidence = true,
            string? startedAt = null, string? completedAt = null)
        {
            var inspection = new InspectionEntity
            {
                InspectionCode = $"INS-{SeedYear}-{number:D4}",
                Title = title,
                InspectionTypeName = typeName,
                TypeShort = typeShort,
                Priority = priority,
                SiteName = Site,
                Department = department,
                PlanCode = planCode,
                PlanName = planName,
                Trigger = planCode.Length > 0 ? "Scheduled" : "Manual",
                Description = description.Length > 0 ? description : $"{title} of {assetName} ({assetCode}) at {location}.",
                Scope = "Asset-based",
                Category = category,
                ReferenceStandard = referenceStandard,
                PermitRequired = permit,
                EvidenceRequired = evidence,
                AssetCode = assetCode,
                AssetName = assetName,
                Location = location,
                AssignedTo = inspector.Name,
                AssignedTeam = inspector.Team,
                DueDate = dueDate,
                ScheduledStart = start,
                ScheduledEnd = end,
                StartedAt = startedAt,
                CompletedAt = completedAt,
                FindingsCount = findings,
                ChecklistCode = checklistCode,
                ChecklistName = checklistName,
                SignOff = status == "Completed"
                    ?
                    [
                        new SignOffStep { Role = "Inspector", Name = inspector.Name, State = "Signed", SignedAt = completedAt },
                        new SignOffStep { Role = "Supervisor", Name = "Omar Hassan", State = "Signed", SignedAt = completedAt },
                        new SignOffStep { Role = "Approver", Name = "Sarah Ahmed", State = "Signed", SignedAt = completedAt }
                    ]
                    :
                    [
                        new SignOffStep { Role = "Inspector", Name = inspector.Name, State = "Pending" },
                        new SignOffStep { Role = "Supervisor", Name = "Omar Hassan", State = "Pending" },
                        new SignOffStep { Role = "Approver", Name = "Sarah Ahmed", State = "Pending" }
                    ]
            };

            return Audit(inspection, status);
        }

        const string Safety = "Safety Inspection";
        const string Condition = "Condition Inspection";
        const string Statutory = "Statutory Inspection";
        const string PreUse = "Pre-use Inspection";

        var cnc = Make(148, "CNC Machine Safety Inspection", Safety, "Safety", "High", "CNC-00123", "CNC Machine", "Production Hall A", AliKhan,
            "2026-10-20", "2026-10-20T09:00:00", "2026-10-20T10:00:00", "In Progress", 2,
            "PLN-001", "Monthly Equipment Safety", "CHK-001", "Equipment Safety v3.0",
            description: "Routine safety inspection of production equipment to verify operating condition, protective devices and compliance requirements.",
            referenceStandard: "Company Equipment Safety Procedure", startedAt: "2026-10-20T09:00:00");
        cnc.Responses = CncResponses();
        cnc.SignOff =
        [
            new SignOffStep { Role = "Inspector", Name = "Ali Khan", State = "Signed", SignedAt = "2026-10-20T10:05:00" },
            new SignOffStep { Role = "Supervisor", Name = "Omar Hassan", State = "Awaiting review" },
            new SignOffStep { Role = "Approver", Name = "Sarah Ahmed", State = "Pending" }
        ];

        return
        [
            Make(144, "Overhead Crane Statutory Inspection", Statutory, "Statutory", "Critical", "OHC-002", "Overhead Crane", "Production Hall B", RajeshKumar,
                "2026-11-12", "2026-11-12T08:00:00", "2026-11-12T12:00:00", "Scheduled", 0, "PLN-003", "Quarterly Statutory Inspection", "", "Statutory Compliance v4",
                category: "Statutory", referenceStandard: "UAE Lifting Equipment Regulations", permit: true),
            Make(145, "Weekly Compressor Check", Condition, "Condition", "Medium", "AC-004", "Air Compressor", "Utility Area", PriyaNair,
                "2026-10-14", "2026-10-14T10:00:00", "2026-10-14T11:00:00", "Completed", 1, "PLN-002", "Weekly Compressor Check", "CHK-002", "Condition Assessment v2.1",
                department: "Reliability", startedAt: "2026-10-14T10:02:00", completedAt: "2026-10-14T10:58:00"),
            Make(146, "Conveyor Safety Inspection", Safety, "Safety", "High", "CV-009", "Conveyor", "Packing Line", ChenWei,
                "2026-10-16", "2026-10-16T13:00:00", "2026-10-16T14:00:00", "Completed", 1, "", "", "CHK-001", "Equipment Safety v3.0",
                department: "Operations", startedAt: "2026-10-16T13:05:00", completedAt: "2026-10-16T13:52:00"),
            Make(147, "Chiller Monthly Condition Check", Condition, "Condition", "Medium", "CH-001", "Chiller", "HVAC Plant Room", FatimaAlMansoori,
                "2026-10-08", "2026-10-08T09:00:00", "2026-10-08T10:30:00", "Completed", 0, "PLN-005", "Monthly HVAC Condition Check", "CHK-002", "Condition Assessment v2.1",
                department: "Facilities", startedAt: "2026-10-08T09:04:00", completedAt: "2026-10-08T10:21:00"),
            cnc,
            Make(149, "Air Compressor Condition Inspection", Condition, "Condition", "Medium", "AC-004", "Air Compressor", "Utility Area", PriyaNair,
                "2026-10-21", "2026-10-21T10:00:00", "2026-10-21T11:00:00", "Scheduled", 0, "PLN-002", "Weekly Compressor Check", "CHK-002", "Condition Assessment v2.1",
                department: "Reliability"),
            Make(150, "Fire Pump Statutory Inspection", Statutory, "Statutory", "Critical", "FP-012", "Fire Pump", "Fire Water System", OmarHassan,
                "2026-10-21", "2026-10-21T08:00:00", "2026-10-21T10:00:00", "Overdue", 3, "PLN-003", "Quarterly Statutory Inspection", "", "Statutory Compliance v4",
                department: "EHS", category: "Statutory", referenceStandard: "NFPA 25", permit: true),
            Make(151, "Forklift Pre-use Inspection", PreUse, "Pre-use", "High", "FL-001", "Forklift", "Warehouse", ChenWei,
                "2026-10-22", "2026-10-22T07:00:00", "2026-10-22T07:30:00", "Completed", 1, "PLN-004", "Forklift Pre-use Checks", "CHK-003", "Pre-use Forklift Checks v2.0",
                department: "Operations", startedAt: "2026-10-22T07:02:00", completedAt: "2026-10-22T07:24:00"),
            Make(152, "Chiller Condition Inspection", Condition, "Condition", "Medium", "CH-002", "Chiller", "HVAC Plant Room", FatimaAlMansoori,
                "2026-10-23", "2026-10-23T09:00:00", "2026-10-23T10:30:00", "Scheduled", 0, "PLN-005", "Monthly HVAC Condition Check", "CHK-002", "Condition Assessment v2.1",
                department: "Facilities"),
            Make(153, "Pressure Vessel Statutory Inspection", Statutory, "Statutory", "Critical", "PV-006", "Pressure Vessel", "Process Area B", RajeshKumar,
                "2026-10-24", "2026-10-24T08:00:00", "2026-10-24T11:00:00", "Scheduled", 0, "PLN-003", "Quarterly Statutory Inspection", "", "Statutory Compliance v4",
                department: "Inspection", category: "Statutory", referenceStandard: "ASME Section VIII", permit: true),
            Make(154, "Hydraulic Press Safety Inspection", Safety, "Safety", "High", "PRS-004", "Hydraulic Press", "Press Shop", OmarHassan,
                "2026-10-19", "2026-10-19T14:00:00", "2026-10-19T15:00:00", "Completed", 1, "PLN-001", "Monthly Equipment Safety", "CHK-001", "Equipment Safety v3.0",
                department: "EHS", startedAt: "2026-10-19T14:03:00", completedAt: "2026-10-19T15:10:00"),
            Make(155, "Electrical Panel Condition Inspection", Condition, "Condition", "High", "EP-003", "Electrical Panel", "Utility Building", FatimaAlMansoori,
                "2026-10-20", "2026-10-20T11:00:00", "2026-10-20T12:00:00", "Completed", 1, "", "", "CHK-005", "Electrical Safety v1.0",
                department: "Facilities", permit: true, startedAt: "2026-10-20T11:02:00", completedAt: "2026-10-20T11:50:00"),
            Make(156, "Forklift Pre-use Inspection", PreUse, "Pre-use", "High", "FL-002", "Forklift", "Warehouse", ChenWei,
                "2026-10-20", "2026-10-20T14:00:00", "2026-10-20T15:00:00", "Completed", 0, "PLN-004", "Forklift Pre-use Checks", "CHK-003", "Pre-use Forklift Checks v2.0",
                department: "Operations", startedAt: "2026-10-20T14:01:00", completedAt: "2026-10-20T14:22:00"),
            Make(157, "Monthly Equipment Safety Inspection", Safety, "Safety", "High", "CNC-00124", "CNC Machine", "Production Hall A", AliKhan,
                "2026-11-22", "2026-11-22T09:00:00", "2026-11-22T10:00:00", "Scheduled", 0, "PLN-001", "Monthly Equipment Safety", "CHK-001", "Equipment Safety v3.0",
                description: "Routine safety inspection of production equipment to verify operating condition, protective devices and compliance requirements.",
                referenceStandard: "Company Equipment Safety Procedure")
        ];
    }

    /// <summary>INS-2026-0148: 12 tasks, 8 answered (6 pass, 1 fail, 1 N/A), task 6 is the failed bearing reading.</summary>
    private static List<InspectionChecklistResponse> CncResponses()
    {
        InspectionChecklistResponse R(string section, string code, string title, string type, string result, string reading = "", string notes = "",
            string unit = "", double? min = null, double? max = null, string procedure = "", bool required = true, params string[] evidence)
        {
            return new InspectionChecklistResponse
            {
                SectionName = section,
                TaskCode = code,
                TaskTitle = title,
                ResponseType = type,
                Unit = unit,
                MinValue = min,
                MaxValue = max,
                Procedure = procedure,
                Required = required,
                Reading = reading,
                Result = result,
                Notes = notes,
                Evidence = evidence.ToList()
            };
        }

        const string SafetySection = "Safety & Protection";
        const string MechanicalSection = "Mechanical Condition";
        const string ElectricalSection = "Electrical Checks";

        return
        [
            R(SafetySection, "ITK-001", "Check emergency stop operation", "Pass / Fail", "Pass", notes: "All three E-stops halt spindle and axes; reset required to restart.",
                procedure: "Press each emergency stop button with the machine running and confirm all motion stops and cannot restart until reset."),
            R(SafetySection, "ITK-002", "Inspect guards and interlocks", "Pass / Fail", "Pass", notes: "Door interlock stops spindle when opened.",
                procedure: "Open each interlocked guard and confirm the machine stops; check fixed guards are complete and fastened."),
            R(SafetySection, "ITK-008", "Verify warning labels", "Pass / Fail", "Pass",
                procedure: "Walk around the machine and confirm all warning and instruction labels are present, legible and undamaged."),
            R(MechanicalSection, "ITK-009", "Check belt tension and condition", "Pass / Fail", "Pass", notes: "Deflection within specification.",
                procedure: "With the drive isolated, check belt deflection against the specification and inspect for cracks or glazing."),
            R(MechanicalSection, "ITK-007", "Verify pressure gauge reading", "Numeric", "", unit: "bar", min: 0, max: 10,
                procedure: "Read the hydraulic pressure gauge at normal operating load and record the value."),
            R(MechanicalSection, "ITK-003", "Measure bearing temperature", "Numeric", "Fail", "86.5",
                "Bearing temperature exceeds the configured limit. Abnormal heat observed near the drive-end bearing.",
                "°C", 0, 80, "Use a calibrated probe at the marked bearing measurement point.", true,
                "bearing-drive-end-1.jpg"),
            R(MechanicalSection, "ITK-004", "Check for oil leakage", "Yes / No", "Pass", "No",
                procedure: "Inspect around seals, hoses and fittings and the drip tray. Answer 'No' when no leakage is observed."),
            R(MechanicalSection, "ITK-005", "Record vibration level", "Numeric", "", unit: "mm/s", min: 0, max: 4.5,
                procedure: "Take an overall vibration velocity reading at the drive-end bearing housing with the portable analyser."),
            R(ElectricalSection, "ITK-006", "Inspect electrical connections", "Pass / Fail", "Pass", notes: "No discolouration or loose terminals found.",
                procedure: "With the panel isolated, check terminals for tightness and look for discolouration or damaged insulation."),
            R(ElectricalSection, "ITK-010", "Test earth continuity", "Numeric", "N/A", notes: "Ohmmeter MM-012 out for calibration; to be tested on next visit.",
                unit: "Ω", min: 0, max: 1,
                procedure: "Measure resistance between the machine frame and the main earth terminal with a calibrated low-resistance ohmmeter."),
            R(ElectricalSection, "ITK-011", "Check panel cooling fans", "Pass / Fail", "", required: false,
                procedure: "Confirm cooling fans are running and filters are clean and unobstructed."),
            R(ElectricalSection, "ITK-012", "Verify control panel indicators", "Pass / Fail", "",
                procedure: "Run the lamp test and confirm all indicators and HMI alarm messages display correctly.")
        ];
    }

    // ---------------------------------------------------------------- findings

    public static IReadOnlyList<InspectionFinding> Findings()
    {
        InspectionFinding Finding(int number, string title, string inspectionCode, string assetCode, string assetName, string sourceTask, string severity,
            Person owner, string dueDate, string recorded, string acceptance, string observation, string correctiveAction, string workOrder,
            string workOrderStatus, string workflowStep, string status, string[] evidence, params FindingActivity[] activity)
        {
            return Audit(new InspectionFinding
            {
                FindingCode = $"FND-{SeedYear}-{number:D4}",
                Title = title,
                InspectionCode = inspectionCode,
                AssetCode = assetCode,
                AssetName = assetName,
                SourceTask = sourceTask,
                Severity = severity,
                Owner = owner.Name,
                DueDate = dueDate,
                RecordedValue = recorded,
                Acceptance = acceptance,
                Observation = observation,
                Evidence = evidence.ToList(),
                CorrectiveAction = correctiveAction,
                AssignedTo = owner.Name,
                WorkOrder = workOrder,
                WorkOrderStatus = workOrderStatus,
                WorkflowStep = workflowStep,
                Activity = activity.ToList()
            }, status);
        }

        FindingActivity A(string at, string by, string text) => new() { At = at, By = by, Text = text };

        return
        [
            Finding(82, "Worn warning label", "INS-2026-0151", "FL-001", "Forklift", "Verify warning labels", "Low", RajeshKumar, "2026-10-24",
                "Label faded", "All labels legible", "Load-capacity warning label on the mast is faded and partly unreadable.",
                "Replace the load-capacity warning label.", "", "", "Open", "Open", [],
                A("2026-10-22T07:20:00", "Chen Wei", "Finding created from task 'Verify warning labels'.")),
            Finding(83, "Loose electrical terminal", "INS-2026-0155", "EP-003", "Electrical Panel", "Inspect electrical connections", "High", FatimaAlMansoori, "2026-10-20",
                "Loose terminal L2", "All terminals tight", "Incoming supply terminal L2 found loose with slight discolouration.",
                "Re-torque terminals and repeat thermographic scan.", "WO-00844", "Completed", "Verify", "Pending Verification", ["panel-ep003-terminal.jpg"],
                A("2026-10-20T11:30:00", "Fatima Al Mansoori", "Finding created from task 'Inspect electrical connections'."),
                A("2026-10-20T12:10:00", "Sarah Ahmed", "Work order WO-00844 raised."),
                A("2026-10-20T16:45:00", "Fatima Al Mansoori", "Terminals re-torqued; awaiting verification scan.")),
            Finding(84, "Missing safety guard", "INS-2026-0146", "CV-009", "Conveyor", "Inspect guards and interlocks", "High", ChenWei, "2026-10-19",
                "Guard missing", "All guards fitted and secured", "Tail-pulley guard missing on the packing line conveyor.",
                "Refit and secure the tail-pulley guard.", "WO-00841", "Open", "Action", "Overdue", ["conveyor-cv009-guard.jpg"],
                A("2026-10-16T13:40:00", "Chen Wei", "Finding created from task 'Inspect guards and interlocks'."),
                A("2026-10-16T14:05:00", "Sarah Ahmed", "Work order WO-00841 raised and assigned to Chen Wei.")),
            Finding(85, "Oil leakage at seal", "INS-2026-0145", "AC-004", "Air Compressor", "Check for oil leakage", "Medium", PriyaNair, "2026-10-22",
                "Leak observed", "No leakage", "Oil weeping from the compressor shaft seal into the drip tray.",
                "Replace the compressor shaft seal.", "WO-00843", "In Progress", "Action", "In Progress", ["compressor-ac004-seal.jpg"],
                A("2026-10-14T10:40:00", "Priya Nair", "Finding created from task 'Check for oil leakage'."),
                A("2026-10-14T11:15:00", "Sarah Ahmed", "Work order WO-00843 raised and assigned to Priya Nair.")),
            Finding(86, "Damaged emergency stop", "INS-2026-0154", "PRS-004", "Hydraulic Press", "Check emergency stop operation", "Critical", OmarHassan, "2026-10-20",
                "Fail", "Stops all motion; reset required", "Emergency stop button cracked and latching intermittently.",
                "Replace the emergency stop push button and prove the safety circuit.", "WO-00845", "In Progress", "Action", "In Progress", ["press-prs004-estop.jpg"],
                A("2026-10-19T14:30:00", "Omar Hassan", "Finding created from task 'Check emergency stop operation'. Press locked out."),
                A("2026-10-19T15:20:00", "Sarah Ahmed", "Work order WO-00845 raised as critical.")),
            Finding(87, "Elevated bearing temperature", "INS-2026-0148", "CNC-00123", "CNC Machine", "Measure bearing temperature", "High", AliKhan, "2026-10-21",
                "86.5 °C", "0 – 80 °C", "Bearing temperature exceeded the maximum by 6.5 °C during inspection.",
                "Inspect lubrication and bearing condition.", "WO-00847", "In Progress", "Open", "Open", ["bearing-drive-end-1.jpg", "bearing-drive-end-2.jpg"],
                A("2026-10-20T09:24:00", "Ali Khan", "Bearing temperature measured at 86.5 °C, above maximum limit of 80 °C."),
                A("2026-10-20T09:40:00", "Sarah Ahmed", "Work order WO-00847 raised and assigned to Ali Khan."))
        ];
    }

    // ---------------------------------------------------------------- calibration instruments

    public static IReadOnlyList<CalibrationInstrument> CalibrationInstruments()
    {
        CalibrationInstrument Instrument(string code, string name, string serial, string location, string range, string unit, string tolerance,
            string provider, string providerType, string assignedTo, string? last, string? next, string certificate, string lastResult, string status,
            string referenceStandard, string referenceCertificate, CalibrationPoint[] results, params CalibrationHistoryEntry[] history)
        {
            return Audit(new CalibrationInstrument
            {
                InstrumentCode = code,
                InstrumentName = name,
                SerialNumber = serial,
                Location = location,
                Range = range,
                Unit = unit,
                Tolerance = tolerance,
                IntervalMonths = 12,
                Provider = provider,
                ProviderType = providerType,
                AssignedTo = assignedTo,
                LastCalibration = last,
                NextDue = next,
                Certificate = certificate,
                LastResult = lastResult,
                Results = results.ToList(),
                ReferenceStandard = referenceStandard,
                ReferenceCertificate = referenceCertificate,
                ValidAtCalibration = true,
                History = history.ToList()
            }, status);
        }

        CalibrationPoint P(double reference, double asFound, double asLeft, string result = "Pass") =>
            new() { Reference = reference, AsFound = asFound, AsLeft = asLeft, Result = result };
        CalibrationHistoryEntry H(string date, string result, string certificate, string by) =>
            new() { Date = date, Result = result, Certificate = certificate, PerformedBy = by };

        const string Lab = "Precision Calibration Lab";
        const string InHouse = "In-house Metrology";

        return
        [
            Instrument("PG-001", "Digital Pressure Gauge", "DPG-240018", "Utility Room", "0 – 10 bar", "bar", "±0.05 bar", Lab, "In-house", "Ali Khan",
                "2025-10-22", "2026-10-22", "CERT-0148", "Pass", "Due Soon", "REF-P-002", "RC-2025-098",
                [P(2.00, 2.03, 2.01), P(5.00, 5.04, 5.01), P(8.00, 8.03, 8.01)],
                H("2025-10-22", "Pass", "CERT-0148", Lab), H("2024-10-18", "Pass", "CERT-0091", Lab)),
            Instrument("TP-006", "Temperature Probe", "TPR-230611", "Production Hall A", "-20 – 150 °C", "°C", "±0.5 °C", InHouse, "In-house", "Priya Nair",
                "2025-10-15", "2026-10-15", "CERT-0127", "Pass", "Overdue", "REF-T-004", "RC-2025-071",
                [P(0, 0.2, 0.1), P(50, 50.3, 50.1), P(100, 100.4, 100.2)],
                H("2025-10-15", "Pass", "CERT-0127", "Priya Nair"), H("2024-10-11", "Pass", "CERT-0083", "Priya Nair")),
            Instrument("TW-003", "Torque Wrench", "TQW-220145", "Maintenance Workshop", "20 – 200 N·m", "N·m", "±4%", Lab, "Service Provider", "External provider",
                "2026-08-12", "2027-08-12", "CERT-0135", "Pass", "Valid", "REF-TQ-001", "RC-2026-044",
                [P(40, 40.9, 40.4), P(100, 101.8, 100.6), P(180, 182.9, 180.7)],
                H("2026-08-12", "Pass", "CERT-0135", Lab), H("2025-08-08", "Pass", "CERT-0102", Lab)),
            Instrument("MM-012", "Digital Multimeter", "DMM-250077", "Electrical Workshop", "0 – 600 V", "V", "±0.5%", Lab, "Service Provider", "Rajesh Kumar",
                "2026-09-08", "2027-09-08", "CERT-0142", "Pass", "Valid", "REF-E-003", "RC-2026-052",
                [P(10, 10.02, 10.01), P(230, 230.6, 230.2), P(500, 501.1, 500.4)],
                H("2026-09-08", "Pass", "CERT-0142", Lab), H("2025-09-04", "Pass", "CERT-0109", Lab)),
            Instrument("FM-004", "Flow Meter", "FLM-210933", "Fire Water System", "0 – 100 m³/h", "m³/h", "±1%", Lab, "Service Provider", "Omar Hassan",
                "2026-10-19", null, "", "Fail", "Out of Tolerance", "REF-F-001", "RC-2026-061",
                [P(25, 25.4, 25.4), P(50, 51.3, 51.3, "Fail"), P(75, 77.1, 77.1, "Fail")],
                H("2026-10-19", "Fail", "", Lab), H("2025-10-14", "Pass", "CERT-0124", Lab)),
            Instrument("WS-002", "Weighing Scale", "WSC-240402", "Warehouse", "0 – 150 kg", "kg", "±0.05 kg", InHouse, "In-house", "Chen Wei",
                "2026-07-02", "2027-07-02", "CERT-0116", "Pass", "Valid", "REF-M-002", "RC-2026-029",
                [P(20, 20.02, 20.01), P(75, 75.03, 75.01), P(140, 140.04, 140.02)],
                H("2026-07-02", "Pass", "CERT-0116", "Chen Wei"), H("2025-06-30", "Pass", "CERT-0078", "Chen Wei"))
        ];
    }

    // ---------------------------------------------------------------- signatures & stamps

    public static IReadOnlyList<SignatureAndStamp> SignaturesAndStamps()
    {
        SignatureAndStamp Signature(string code, string user, string role, string department) => Audit(new SignatureAndStamp
        {
            SignatureCode = code,
            Kind = "Signature",
            User = user,
            SignatureName = user,
            Role = role,
            Department = department,
            AuthorizedRole = role,
            SignatureImage = string.Empty,
            StampImage = string.Empty,
            AssignedStampCode = "STP-001",
            EffectiveDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ExpiryDate = new DateTime(2027, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            SignaturePosition = "Footer – left",
            StampPosition = "Footer – right",
            IncludeSignerNameDate = true,
            RequirePasswordConfirmationBeforeSigning = true,
            AllowUploadedSignature = true,
            IsActive = true
        }, "Active");

        return
        [
            Signature("SIG-001", "Ali Khan", "Inspector", "Maintenance & Engineering"),
            Signature("SIG-002", "Omar Hassan", "Supervisor", "EHS"),
            Signature("SIG-003", "Sarah Ahmed", "Approver", "Asset Management"),
            Audit(new SignatureAndStamp
            {
                SignatureCode = "STP-001",
                Kind = "Stamp",
                User = string.Empty,
                SignatureName = "Quality Department Stamp",
                Role = string.Empty,
                Department = "Quality",
                AuthorizedRole = "Approver",
                SignatureImage = string.Empty,
                StampImage = string.Empty,
                EffectiveDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ExpiryDate = new DateTime(2027, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                SignaturePosition = "Footer – left",
                StampPosition = "Footer – right",
                IncludeSignerNameDate = true,
                IsDefaultStamp = true,
                IsActive = true
            }, "Active")
        ];
    }

    // ---------------------------------------------------------------- notification templates

    public static IReadOnlyList<NotificationTemplate> NotificationTemplates()
    {
        string[] allChannels = ["Email", "In-app", "Push"];

        NotificationTemplate Template(string code, string name, string evt, string subject, string body, string[] recipients, string[] cc,
            string button, string timing, bool active = true)
        {
            return Audit(new NotificationTemplate
            {
                TemplateCode = code,
                TemplateName = name,
                Event = evt,
                Channels = allChannels.ToList(),
                Channel = string.Join(", ", allChannels),
                Subject = subject,
                MessageBody = body,
                Recipients = recipients.ToList(),
                CcRecipients = cc.ToList(),
                ButtonLabel = button,
                Timing = timing,
                RespectUserLocale = true,
                PreventDuplicates = true,
                IsActive = active
            }, active ? "Active" : "Draft", active ? null : Utc(10, 20, 10, 0), active ? null : "Sarah Ahmed");
        }

        return
        [
            Template("NT-001", "Inspection Assigned", "Inspection assigned", "Inspection {{inspection_id}} assigned to you",
                "Hello {{inspector_name}},\n\nYou have been assigned {{inspection_title}} for {{asset_name}} at {{location}}.\nDue date: {{due_date}}.\n\nPlease review the checklist and complete the inspection before the due date.",
                ["Assigned inspector"], ["Supervisor"], "Open Inspection", "Send immediately on assignment"),
            Template("NT-002", "Upcoming Inspection Reminder", "Inspection due soon", "Reminder: inspection {{inspection_id}} is due on {{due_date}}",
                "Hello {{inspector_name}},\n\n{{inspection_title}} for {{asset_name}} is due on {{due_date}}.\nPlease plan your visit and confirm any permits in advance.",
                ["Assigned inspector"], [], "Open Inspection", "24 hours before due date"),
            Template("NT-003", "Inspection Overdue", "Inspection overdue", "Inspection {{inspection_id}} is overdue",
                "Hello {{inspector_name}},\n\n{{inspection_title}} for {{asset_name}} was due on {{due_date}} and has not been completed.\nPlease complete it as soon as possible or contact your supervisor.",
                ["Assigned inspector"], ["Supervisor"], "Open Inspection", "Daily at 08:00 until completed"),
            Template("NT-004", "Critical Finding Raised", "Critical finding raised", "Critical finding on {{asset_name}} ({{inspection_id}})",
                "A critical finding was recorded during {{inspection_title}} on {{asset_name}} at {{location}}.\nImmediate review and corrective action are required.",
                ["Supervisor", "EHS Manager"], ["Asset Manager"], "View Finding", "Send immediately"),
            Template("NT-005", "Corrective Action Due", "Corrective action due", "Corrective action for {{asset_name}} is due on {{due_date}}",
                "Hello,\n\nThe corrective action raised from {{inspection_id}} on {{asset_name}} is due on {{due_date}}.\nPlease update the work order once the action is complete.",
                ["Action owner"], ["Supervisor"], "Open Work Order", "48 hours before due date"),
            Template("NT-006", "Inspection Completed", "Inspection completed", "Inspection {{inspection_id}} completed",
                "{{inspection_title}} for {{asset_name}} was completed by {{inspector_name}}.\nThe report is ready for review and approval.",
                ["Supervisor", "Approver"], [], "View Report", "Send immediately on completion", active: false)
        ];
    }

    // ---------------------------------------------------------------- report templates

    public static IReadOnlyList<ReportTemplate> ReportTemplates()
    {
        List<ReportSection> Sections() =>
        [
            new() { Name = "Header & Branding", Enabled = true, Order = 1 },
            new() { Name = "Inspection Summary", Enabled = true, Order = 2 },
            new() { Name = "Asset Details", Enabled = true, Order = 3 },
            new() { Name = "Checklist Results", Enabled = true, Order = 4 },
            new() { Name = "Findings & Corrective Actions", Enabled = true, Order = 5 },
            new() { Name = "Evidence Gallery", Enabled = true, Order = 6 },
            new() { Name = "Signatures & Stamp", Enabled = true, Order = 7 },
            new() { Name = "Footer & Page Numbers", Enabled = true, Order = 8 }
        ];

        ReportTemplate Template(string code, string title, string typeName, string version, string status)
        {
            return Audit(new ReportTemplate
            {
                ReportTemplateCode = code,
                ReportTitle = title,
                InspectionTypeName = typeName,
                Format = "PDF",
                PageSize = "A4",
                Orientation = "Portrait",
                Language = "English",
                AccentColor = "#6D28D9",
                VersionLabel = version,
                ReportNumberFormat = "INSR-{YYYY}-{SEQ}",
                Sections = Sections(),
                IncludePassedItems = true,
                IncludePhotos = true,
                ShowAcceptanceCriteria = true,
                ShowFindingStatus = true,
                IncludeSignatures = true,
                IncludeStamps = true,
                IncludeInternalNotes = false,
                IncludeOrganizationLogo = true,
                IncludeAssetDetails = true,
                IncludeTaskResponses = true,
                IncludeCorrectiveActions = true,
                ShowPageNumber = true,
                IsActive = status == "Published"
            }, status);
        }

        return
        [
            Template("RT-001", "Equipment Safety Report", "Safety Inspection", "3.0", "Published"),
            Template("RT-002", "Condition Assessment Report", "Condition Inspection", "2.1", "Published"),
            Template("RT-003", "Statutory Inspection Report", "Statutory Inspection", "1.0", "Draft")
        ];
    }

    // ---------------------------------------------------------------- numbering sequences

    public static IReadOnlyList<NumberingSequence> NumberingSequences()
    {
        NumberingSequence Sequence(string code, string name, string numberType, string prefix, string yearFormat, int digits, int running,
            string reset, string format, string lastAllocated, int assigned, params SequenceAllocation[] allocations)
        {
            return Audit(new NumberingSequence
            {
                SequenceCode = code,
                SequenceName = name,
                NumberType = numberType,
                Prefix = prefix,
                Separator = "-",
                YearFormat = yearFormat,
                SequenceDigits = digits,
                StartingNumber = 1,
                RunningNumber = running,
                ResetFrequency = reset,
                Scope = "Tenant-wide",
                Timezone = "Asia/Dubai",
                Format = format,
                GenerateOnFinalCreation = true,
                DoNotReuse = true,
                AllowManualOverride = false,
                LastAllocated = lastAllocated,
                AssignedThisYear = assigned,
                Allocations = allocations.ToList(),
                SampleNumberPreview = yearFormat.Length > 0
                    ? $"{prefix}-{SeedYear}-{running.ToString().PadLeft(digits, '0')}"
                    : $"{prefix}-{running.ToString().PadLeft(digits, '0')}",
                IsActive = true
            }, "Active");
        }

        SequenceAllocation A(string reference, string record, string at, string state = "Created") =>
            new() { Reference = reference, Record = record, AllocatedAt = at, State = state };

        return
        [
            Sequence("NS-001", "Inspection Records", "Inspection", "INS", "YYYY", 4, 158, "Annually", "INS-{YYYY}-{SEQ}", "INS-2026-0157", 157,
                A("INS-2026-0157", "Forklift Pre-use Inspection", "2026-10-20T10:12:00"),
                A("INS-2026-0156", "Electrical Panel Inspection", "2026-10-20T09:48:00"),
                A("INS-2026-0155", "Compressor Condition Check", "2026-10-20T09:30:00", "Cancelled")),
            Sequence("NS-002", "Inspection Plans", "Plan", "PLN", "", 3, 7, "Never", "PLN-{SEQ}", "PLN-006", 6,
                A("PLN-006", "Annual Electrical Safety Audit", "2026-09-14T11:05:00"),
                A("PLN-005", "Monthly HVAC Condition Check", "2026-08-02T09:20:00")),
            Sequence("NS-003", "Inspection Findings", "Finding", "FND", "YYYY", 4, 88, "Annually", "FND-{YYYY}-{SEQ}", "FND-2026-0087", 87,
                A("FND-2026-0087", "Elevated bearing temperature", "2026-10-20T09:24:00"),
                A("FND-2026-0086", "Damaged emergency stop", "2026-10-19T14:30:00"),
                A("FND-2026-0085", "Oil leakage at seal", "2026-10-14T10:40:00")),
            Sequence("NS-004", "Inspection Reports", "Report", "INSR", "YYYY", 4, 149, "Annually", "INSR-{YYYY}-{SEQ}", "INSR-2026-0148", 148,
                A("INSR-2026-0148", "CNC Machine Safety Inspection", "2026-10-20T10:06:00")),
            Sequence("NS-005", "Calibration Records", "Calibration", "CAL", "YYYY", 4, 43, "Annually", "CAL-{YYYY}-{SEQ}", "CAL-2026-0042", 42,
                A("CAL-2026-0042", "Flow Meter FM-004", "2026-10-19T13:15:00"))
        ];
    }

    // ---------------------------------------------------------------- working calendars

    public static IReadOnlyList<HolidayAndWorkingCalendar> WorkingCalendars()
    {
        WorkingDay Day(string day, bool working) =>
            new() { Day = day, Working = working, Start = working ? "08:00" : string.Empty, End = working ? "17:00" : string.Empty };

        return
        [
            Audit(new HolidayAndWorkingCalendar
            {
                CalendarCode = "CAL-001",
                CalendarName = "Dubai Plant – Standard",
                SiteName = Site,
                Timezone = "Asia/Dubai",
                EffectiveFrom = "2026-01-01",
                WorkingWeek =
                [
                    Day("Monday", true), Day("Tuesday", true), Day("Wednesday", true), Day("Thursday", true), Day("Friday", true),
                    Day("Saturday", false), Day("Sunday", false)
                ],
                BreakPeriod = "12:00 – 13:00",
                Exceptions =
                [
                    new CalendarException { Date = "2026-10-23", Description = "Site Maintenance Shutdown", Duration = "Full day", Type = "Site exception" },
                    new CalendarException { Date = "2026-10-28", Description = "Extended Inspection Shift", Duration = "08:00 – 19:00", Type = "Working override" }
                ],
                NonWorkingDayRule = "Move to next working day",
                DueDateCalculation = "Working days",
                SlaCalendar = "Use this calendar",
                SkipHolidays = true,
                RespectInspectorShift = true,
                AllowManualOverride = false,
                AssignedPlans = 12,
                AssignedInspectors = 8,
                AssignedSites = 1,
                Country = "United Arab Emirates",
                WorkingDays = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"],
                Weekend = ["Saturday", "Sunday"],
                WorkingHours = "08:00 – 17:00",
                IsActive = true
            }, "Active")
        ];
    }
}
