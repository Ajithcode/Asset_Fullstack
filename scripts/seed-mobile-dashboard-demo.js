// Test data for the mobile Home & Dashboard screens (Dashboard service, /api/dashboards/mobile/*).
//
//   Seed (re-runnable; replaces earlier demo rows):  mongosh Asset360 scripts/seed-mobile-dashboard-demo.js
//   Remove the demo rows:                            mongosh Asset360 --eval 'var REMOVE = true' scripts/seed-mobile-dashboard-demo.js
//
// Every inserted record has created_by "mobile-demo-seed" (NetworkTimeSeries readings: meta.source), so it can be
// removed without touching real data. Records are tied to the registered assets already in AssetRegistration.
// Asset coordinates are only filled where an asset has none, and are kept on removal.

const SEED = "mobile-demo-seed";
const remove = typeof REMOVE !== "undefined" && REMOVE === true;

const seededCollections = [
  "work_orders", "inspections", "workflow_approval_tasks", "IoTAlert", "spare_parts",
  "preventive_maintenance_schedules", "AssetConditionAssessment", "TrackingTag",
];

for (const name of seededCollections) {
  db.getCollection(name).deleteMany({ created_by: SEED });
}
if (db.getCollectionNames().includes("NetworkTimeSeries")) {
  db.NetworkTimeSeries.deleteMany({ "meta.source": SEED });
}

if (remove) {
  print("Removed mobile demo data.");
  quit(0);
}

// ------------------------------------------------------------------ assets

const assets = db.AssetRegistration.find({ is_deleted: { $ne: true } }).toArray();
if (assets.length === 0) {
  print("No registered assets found; register some assets first.");
  quit(1);
}

// Rough site centres; each asset gets a small offset so pins do not overlap.
const siteCentres = {
  "dubai hq": [25.1972, 55.2744],
  "jebel ali warehouse": [25.0107, 55.1063],
};
const defaultCentre = [25.2285, 55.2867];

assets.forEach((asset, i) => {
  const location = (asset.location_assignment || {}).location || {};
  if (typeof location.latitude === "number" && typeof location.longitude === "number") {
    return;
  }
  const centre = siteCentres[(location.site_name || "").toLowerCase()] || defaultCentre;
  const latitude = +(centre[0] + ((i % 3) - 1) * 0.0012).toFixed(6);
  const longitude = +(centre[1] + ((i % 2) ? 0.0015 : -0.0011)).toFixed(6);
  db.AssetRegistration.updateOne(
    { _id: asset._id },
    { $set: { "location_assignment.location.latitude": latitude, "location_assignment.location.longitude": longitude } });
});

const info = (asset) => {
  const basic = asset.basic_information || {};
  const location = (asset.location_assignment || {}).location || {};
  return {
    id: asset.asset_registration_id,
    tag: basic.asset_tag || asset.asset_registration_id,
    name: basic.asset_name || asset.asset_registration_id,
    type: (asset.classification || {}).asset_type || "Equipment",
    site: location.site_name || null,
    siteId: location.site_id || null,
    building: location.building_name || null,
    floor: location.floor_name || null,
  };
};
const A = assets.map(info);
const asset = (i) => A[i % A.length];

// ------------------------------------------------------------------ helpers

const now = new Date();
const today = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()));
const day = (offset, hour = 9, minute = 0) =>
  new Date(today.getTime() + offset * 86400000 + (hour * 60 + minute) * 60000);
const hoursAgo = (h) => new Date(now.getTime() - h * 3600000);
const iso = (date) => date.toISOString().slice(0, 16); // inspection dates are strings
const base = (status, createdAt) => ({
  _id: new ObjectId(),
  client_id: null,
  tenant_id: null,
  created_by: SEED,
  created_at: createdAt || now,
  updated_by: null,
  updated_at: null,
  status,
  is_deleted: false,
});

const technicians = ["Max Milan", "Navin", "Ahmed Khan", "Priya Nair", "Ramesh Kumar"];
const parts = [["V-belt set", 180], ["Air filter", 95], ["Bearing SKF-6205", 240], ["Contactor 40A", 320], ["Battery 12V", 410]];

// ------------------------------------------------------------------ work orders

const workOrders = [];
let woNumber = 10400;
const workOrder = ({ a, type, category, title, priority, status, scheduled, due, start, end, technician, costScale = 1 }) => {
  const isDone = status === "Completed";
  const part = parts[woNumber % parts.length];
  const hours = 2 + (woNumber % 5);
  return Object.assign(base(status, new Date(scheduled.getTime() - 3 * 86400000)), {
    work_order_id: `WO-${++woNumber}`,
    asset_id: a.tag,
    asset_name: a.name,
    work_type: type,
    title,
    description: null,
    priority,
    scheduled_date: scheduled,
    assigned_technician: technician,
    category,
    due_date: due,
    planned_start_date: scheduled,
    planned_end_date: new Date(scheduled.getTime() + hours * 3600000),
    actual_start_date: start || null,
    actual_end_date: end || null,
    estimated_hours: hours,
    source: type === "Preventive" ? "PM Schedule" : "Manual",
    source_reference: null,
    site: a.site,
    building: a.building,
    area: a.floor,
    department: null,
    team: null,
    vendor: null,
    notes: null,
    progress: isDone ? 100 : status === "In Progress" ? 50 : 0,
    tasks: [],
    parts: [{ part_number: null, part_name: part[0], quantity: 1 + (woNumber % 3), unit_cost: part[1] * costScale }],
    labour: [{ technician, role: "Technician", hours, hourly_rate: 120 * costScale }],
    additional_cost: (woNumber % 4 === 0 ? 650 : 0) * costScale,
    documents: [],
    history: [{ at: new Date(scheduled.getTime() - 3 * 86400000), by: SEED, action: "Created", details: "Demo work order." }],
  });
};

// History: the last 10 months plus the same months last year (for "vs last year"), mostly planned and completed.
const kinds = [
  ["Preventive", "Routine", "Monthly preventive maintenance"],
  ["Preventive", "Routine", "Quarterly service"],
  ["Inspection", "Statutory", "Statutory check"],
  ["Corrective", "Breakdown", "Breakdown repair"],
  ["Predictive", "Routine", "Vibration analysis follow-up"],
  ["Preventive", "Routine", "Filter replacement"],
];
for (const yearsBack of [0, 1]) {
  for (let m = 1; m <= 10; m++) {
    for (let k = 0; k < 6; k++) {
      const scheduled = day(-30 * m - yearsBack * 365 + k * 4, 8 + k);
      const [type, category, label] = kinds[(m + k) % kinds.length];
      const a = asset(m + k);
      const late = (m + k) % 9 === 0; // finished after the due date -> counts as overdue
      const repairHours = type === "Corrective" ? 3 + (k % 3) : 2;
      const start = new Date(scheduled.getTime() + 30 * 60000);
      workOrders.push(workOrder({
        a, type, category,
        title: `${a.name} - ${label}`,
        priority: type === "Corrective" ? "High" : "Medium",
        status: "Completed",
        scheduled,
        due: new Date(scheduled.getTime() + 2 * 86400000),
        start,
        end: new Date(start.getTime() + (late ? 4 * 86400000 : repairHours * 3600000)),
        technician: technicians[(m + k) % technicians.length],
        costScale: yearsBack ? 0.9 : 1,
      }));
    }
  }
}

// Open work: overdue, today, and the coming two weeks (some assigned to the app's users for My Tasks).
const open = [
  [0, "Corrective", "Breakdown", "Generator trips on load", "Critical", "Open", -4, -1, "Max Milan"],
  [3, "Preventive", "Routine", "UPS battery check", "Medium", "In Progress", -2, -1, "Navin"],
  [0, "Preventive", "Routine", "Pump room inspection", "Medium", "Scheduled", 0, 0, "Max Milan"],
  [1, "Corrective", "Breakdown", "Chiller compressor noise", "High", "Open", 0, 1, "Navin"],
  [2, "Preventive", "Routine", "Forklift 250-hour service", "Medium", "Scheduled", 0, 2, "Ramesh Kumar"],
  [4, "Preventive", "Routine", "Fire pump weekly run test", "High", "Scheduled", 1, 1, "Max Milan"],
  [5, "Preventive", "Routine", "Elevator monthly service", "Medium", "Scheduled", 2, 3, "Priya Nair"],
  [1, "Predictive", "Routine", "Chiller bearing temperature trend", "Medium", "Scheduled", 4, 5, "Navin"],
  [3, "Preventive", "Routine", "UPS firmware update", "Low", "Scheduled", 7, 8, "Ahmed Khan"],
  [0, "Preventive", "Routine", "Generator load bank test", "High", "Scheduled", 10, 11, "Max Milan"],
];
open.forEach(([i, type, category, label, priority, status, startDay, dueDay, technician], n) => {
  const a = asset(i);
  const scheduled = day(startDay, 9 + (n % 6));
  workOrders.push(workOrder({
    a, type, category, title: `${a.name} - ${label}`, priority, status, scheduled,
    due: day(dueDay, 17),
    start: status === "In Progress" ? scheduled : null,
    technician,
  }));
});
db.work_orders.insertMany(workOrders);

// ------------------------------------------------------------------ inspections

let insNumber = 9000;
const inspection = (a, title, type, priority, status, start, assignedTo, { completed = null, failed = false } = {}) =>
  Object.assign(base(status, new Date(start.getTime() - 5 * 86400000)), {
    inspection_code: `INS-${now.getUTCFullYear()}-${++insNumber}`,
    title,
    inspection_type_name: type,
    type_short: type.slice(0, 3).toUpperCase(),
    priority,
    site_name: a.site || "",
    department: "Facilities",
    plan_code: "",
    plan_name: "",
    trigger: "Manual",
    description: `${title} of ${a.name} (${a.tag}).`,
    scope: "Asset-based",
    category: type,
    reference_standard: "",
    permit_required: false,
    evidence_required: false,
    asset_code: a.tag,
    asset_name: a.name,
    location: [a.building, a.floor].filter(Boolean).join(" - "),
    assigned_to: assignedTo,
    assigned_team: "",
    due_date: iso(new Date(start.getTime() + 8 * 3600000)),
    scheduled_start: iso(start),
    scheduled_end: iso(new Date(start.getTime() + 3600000)),
    started_at: completed ? iso(start) : null,
    completed_at: completed ? iso(completed) : null,
    findings_count: failed ? 1 : 0,
    checklist_code: "",
    checklist_name: "",
    responses: completed ? [{
      section_name: "General", task_code: "T-1", task_title: "Overall condition check", response_type: "Pass/Fail",
      unit: "", min_value: null, max_value: null, procedure: "", required: true, reading: "",
      result: failed ? "Fail" : "Pass", notes: failed ? "Alarm panel fault on zone 3." : "", evidence: [],
    }] : [],
    sign_off: [],
  });

db.inspections.insertMany([
  inspection(asset(4), "Fire extinguisher inspection", "Safety", "High", "Scheduled", day(0, 11), "Max Milan"),
  inspection(asset(0), "Generator safety inspection", "Safety", "Medium", "Scheduled", day(0, 15), "Navin"),
  inspection(asset(5), "Elevator statutory inspection", "Statutory", "High", "Scheduled", day(3, 10), "Priya Nair"),
  inspection(asset(1), "Chiller annual inspection", "Mechanical", "Medium", "Scheduled", day(6, 9), "Max Milan"),
  inspection(asset(2), "Forklift lifting gear inspection", "Statutory", "High", "Open", day(-2, 9), "Ramesh Kumar"),
  inspection(asset(4), "Fire alarm panel inspection", "Safety", "Critical", "Completed", day(-1, 9), "Navin", { completed: hoursAgo(3), failed: true }),
  inspection(asset(3), "UPS room inspection", "Electrical", "Low", "Completed", day(-5, 9), "Ahmed Khan", { completed: day(-5, 11) }),
]);

// ------------------------------------------------------------------ approvals (pending)

const approval = (workflow, request, requestedBy, assignedTo, dueDay, priority) =>
  Object.assign(base("Pending", day(-2)), {
    workflow_name: workflow,
    asset_request: request,
    requested_by: requestedBy,
    assigned_to: assignedTo,
    due_date: day(dueDay, 13),
    priority,
  });

db.workflow_approval_tasks.insertMany([
  approval("Spare Parts Purchase", "Bearing SKF-6205 x 4", "Navin", "Max Milan", 0, "High"),
  approval("Asset Move", `${asset(2).tag} to Warehouse 2`, "Ramesh Kumar", "Navin", 1, "Medium"),
  approval("Work Order Closure", "WO-10405 sign-off", "Priya Nair", "Max Milan", 2, "Low"),
]);

// ------------------------------------------------------------------ IoT alerts (last few days)

let alertNumber = 500;
const iotAlert = (a, eventType, severity, status, raisedAt, message) =>
  Object.assign(base(status, raisedAt), {
    alert_id: `ALT-${++alertNumber}`,
    raised_at: raisedAt,
    asset_registration_id: a.id,
    asset_name: a.name,
    device_id: `DEV-${a.tag}`,
    site_id: a.siteId,
    site_name: a.site,
    event_type: eventType,
    severity,
    message,
    alert_rule_id: null,
    acknowledged_by: status === "Acknowledged" ? "Max Milan" : null,
    acknowledged_at: status === "Acknowledged" ? hoursAgo(1) : null,
    resolved_by: status === "Resolved" ? "Navin" : null,
    resolved_at: status === "Resolved" ? hoursAgo(10) : null,
  });

db.IoTAlert.insertMany([
  iotAlert(asset(1), "Critical Asset Vibration High", "Critical", "Open", hoursAgo(0.05), "Vibration 11.2 mm/s above 9 mm/s limit."),
  iotAlert(asset(3), "High Temperature Detected", "Warning", "Acknowledged", hoursAgo(2), "Battery room 34 °C."),
  iotAlert(asset(4), "Low Battery", "Info", "Open", hoursAgo(3), "Sensor battery 15%."),
  iotAlert(asset(0), "Asset Offline", "Critical", "Open", hoursAgo(5), "No telemetry for 30 minutes."),
  iotAlert(asset(5), "Door Fault", "Warning", "Resolved", hoursAgo(26), "Landing door sensor fault."),
  iotAlert(asset(2), "Geofence Exit", "Info", "Resolved", hoursAgo(50), "Left Warehouse 1 zone."),
]);

// ------------------------------------------------------------------ spare parts (two at or below minimum)

const sparePart = (id, name, a, inStock, minimum, unitCost) =>
  Object.assign(base("Active"), {
    part_id: id,
    asset_id: a.tag,
    asset_name: a.name,
    part_name: name,
    category: "Mechanical",
    quantity_in_stock: inStock,
    minimum_stock_level: minimum,
    unit_cost: unitCost,
    supplier: "Demo Supplier",
    updated_at: hoursAgo(8),
  });

db.spare_parts.insertMany([
  sparePart("SKF-6205", "Bearing SKF-6205", asset(1), 2, 6, 240),
  sparePart("VB-SET-01", "V-belt set", asset(1), 0, 2, 180),
  sparePart("AF-200", "Air filter", asset(0), 25, 10, 95),
]);

// ------------------------------------------------------------------ PM schedules due in the next days

const pmSchedule = (id, a, name, dueDay, assignedTo) =>
  Object.assign(base("Active"), {
    pm_schedule_id: id,
    schedule_name: name,
    asset_id: a.tag,
    asset_name: a.name,
    asset_type: a.type,
    location: a.site,
    frequency: "Monthly",
    trigger_type: "Time-based",
    last_maintenance_date: day(dueDay - 30),
    next_due_date: day(dueDay, 8),
    auto_create_work_order: true,
    priority: "Medium",
    assigned_to: assignedTo,
    estimated_hours: 2,
    description: `${name} for ${a.name}.`,
    checklist: [],
  });

db.preventive_maintenance_schedules.insertMany([
  pmSchedule("PM-DEMO-01", asset(5), "Elevator monthly PM", 1, "Priya Nair"),
  pmSchedule("PM-DEMO-02", asset(0), "Generator monthly PM", 2, "Max Milan"),
]);

// ------------------------------------------------------------------ condition assessments (health)

const scores = [86, 38, 72, 64, 45, 91];
const conditions = (score) => score >= 70 ? "Good" : score >= 40 ? "Fair" : "Poor";
db.AssetConditionAssessment.insertMany(A.map((a, i) => {
  const score = scores[i % scores.length];
  return Object.assign(base("Active", day(-3 - i)), {
    asset_registration_id: a.id,
    asset_name: a.name,
    asset_tag: a.tag,
    assessment_date: day(-3 - i),
    assessed_by: "Max Milan",
    overall_condition: conditions(score),
    health_score: NumberDecimal(String(score)),
    physical_condition: conditions(score),
    performance: conditions(score),
    safety: "Good",
    remarks: score < 40 ? "High vibration on compressor" : score < 70 ? "Wear observed; monitor" : null,
  });
}));

// ------------------------------------------------------------------ tracking tags + live network readings

const tags = A.slice(0, 4).map((a, i) => ({ tagId: `TAG-DEMO-${String(i + 1).padStart(3, "0")}`, a, online: i !== 3 }));

db.TrackingTag.insertMany(tags.map(({ tagId, a }) => Object.assign(base("Active"), {
  tag_id: tagId,
  tag_type: "Asset Tag",
  technology: "BLE",
  asset_registration_id: a.id,
  asset_name: a.name,
  site_id: a.siteId,
  site_name: a.site,
  location_name: a.building,
  last_seen_at: hoursAgo(0.02),
})));

if (!db.getCollectionNames().includes("NetworkTimeSeries")) {
  db.createCollection("NetworkTimeSeries", { timeseries: { timeField: "timestamp", metaField: "meta", granularity: "minutes" } });
}
const readings = [];
for (const { tagId, online } of tags) {
  for (let minutes = 60; minutes >= 0; minutes -= 5) {
    // The offline tag stopped reporting "online" 20 minutes ago.
    const status = online || minutes > 20 ? "online" : "offline";
    readings.push({ timestamp: new Date(now.getTime() - minutes * 60000), meta: { tagid: tagId, source: SEED }, status });
  }
}
db.NetworkTimeSeries.insertMany(readings);

print(`Seeded mobile demo data for ${A.length} assets: ${workOrders.length} work orders, 7 inspections, 3 approvals, `
  + `6 IoT alerts, 3 spare parts, 2 PM schedules, ${A.length} assessments, ${tags.length} tags (${readings.length} readings).`);
