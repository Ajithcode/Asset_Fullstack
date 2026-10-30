import { Component, ElementRef, HostListener, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import * as XLSX from 'xlsx';
import {
  LucideAngularModule,
  AirVent,
  Armchair,
  Asterisk,
  ArrowLeft,
  ArrowRight,
  Building2,
  CalendarDays,
  Camera,
  ChartColumn,
  Check,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  CircleCheck,
  ClipboardList,
  Cpu,
  Cylinder,
  EllipsisVertical,
  Eye,
  Fan,
  FileText,
  FileImage,
  FireExtinguisher,
  Folder,
  Funnel,
  Info,
  ListChecks,
  MapPin,
  MessageSquare,
  Package,
  Paperclip,
  Pencil,
  PlugZap,
  Plus,
  RefreshCw,
  Save,
  Search,
  Server,
  Trash2,
  TriangleAlert,
  Truck,
  Upload,
  Wind,
  Wrench,
  X,
  Zap,
} from 'lucide-angular';
import { AUDITS, AUDIT_LOCATIONS, AUDIT_TYPES, AuditStatus, AuditType, isoDate, nextReference } from '../asset-audit/asset-audit';

/**
 * DEMO MODE: sample assets until the Asset Audit API returns assets by location.
 * Main Plant - Block A has 245 assets and starts with the generator shown in the design.
 */

type AssetSelection = 'all' | 'selected' | 'range' | 'filter';
type AssetStatus = 'In Service' | 'Under Maintenance' | 'Idle';
type AssetKind = 'generator' | 'electrical' | 'tank' | 'cooling' | 'exhaust' | 'hvac' | 'it' | 'vehicle' | 'furniture' | 'safety' | 'tool';

interface AuditSensor {
  /** "<asset tag>-S01" */
  id: string;
  name: string;
  type: 'Sensor' | 'Component';
}

interface AuditAsset {
  tag: string;
  serial: string;
  name: string;
  kind: AssetKind;
  category: string;
  location: string;
  department: string;
  status: AssetStatus;
  sensors: AuditSensor[];
}

const AUDIT_DEPARTMENTS = ['Finance Department', 'Operations', 'Maintenance', 'IT Department', 'Administration', 'Logistics'];

/** Asset categories with sample models: [model name, tag prefix, icon kind]. */
const CATEGORY_MODELS: Record<string, [string, string, AssetKind][]> = {
  'Power Generation': [['Generator 500 kVA', 'GEN', 'generator'], ['Generator 250 kVA', 'GEN', 'generator'], ['UPS 60 kVA', 'UPS', 'electrical'], ['Transformer 1 MVA', 'TRF', 'electrical']],
  Electrical: [['ATS Panel', 'ATS', 'electrical'], ['Control Panel', 'CP', 'electrical'], ['Battery Bank', 'BB', 'electrical'], ['Distribution Board', 'DB', 'electrical']],
  'Fuel System': [['Fuel Tank 10,000L', 'FT', 'tank'], ['Fuel Pump', 'FP', 'tank']],
  'Cooling System': [['Cooling Radiator', 'CR', 'cooling'], ['Cooling Tower', 'CT', 'cooling']],
  'Emission Control': [['Exhaust System', 'EXH', 'exhaust'], ['Silencer Unit', 'SIL', 'exhaust']],
  HVAC: [['Chiller Unit 200 TR', 'HVC', 'hvac'], ['Air Handling Unit', 'AHU', 'hvac']],
  'IT Equipment': [['Dell PowerEdge R750', 'ITE', 'it'], ['Cisco Switch 48P', 'ITE', 'it']],
  Vehicles: [['Forklift 3 Ton', 'VEH', 'vehicle'], ['Toyota Hilux', 'VEH', 'vehicle']],
  Furniture: [['Workstation Desk', 'FUR', 'furniture'], ['Filing Cabinet', 'FUR', 'furniture']],
  'Safety Equipment': [['Fire Extinguisher 9 kg', 'SAF', 'safety'], ['Gas Detector', 'SAF', 'safety']],
  Tools: [['Welding Machine', 'TLS', 'tool'], ['Hydraulic Jack', 'TLS', 'tool']],
};

/** Sensor / component names by icon kind. */
const KIND_SENSORS: Record<AssetKind, string[]> = {
  generator: ['Engine Temperature Sensor', 'Oil Pressure Sensor', 'Coolant Level Sensor', 'Fuel Level Sensor', 'Vibration Sensor', 'Voltage Sensor', 'Frequency Sensor', 'Battery Charger'],
  electrical: ['Voltage Sensor', 'Current Sensor', 'Breaker Status Sensor', 'Temperature Sensor', 'Controller Module', 'Surge Protector'],
  tank: ['Fuel Level Sensor', 'Leak Detector', 'Temperature Sensor'],
  cooling: ['Coolant Temperature Sensor', 'Fan Motor', 'Flow Sensor'],
  exhaust: ['Exhaust Temperature Sensor', 'Emission Analyzer'],
  hvac: ['Supply Air Sensor', 'Return Air Sensor', 'Compressor', 'Filter Pressure Sensor'],
  it: ['Power Supply Unit', 'Fan Module', 'Temperature Sensor'],
  vehicle: ['GPS Tracker', 'Fuel Level Sensor', 'Battery'],
  furniture: [],
  safety: ['Pressure Gauge', 'Inspection Tag'],
  tool: ['Power Cable', 'Safety Guard'],
};

const ASSET_CATEGORIES = Object.keys(CATEGORY_MODELS);
const ASSET_STATUSES: AssetStatus[] = ['In Service', 'Under Maintenance', 'Idle'];

/** Assets per site; "All Locations" is the union. */
const SITE_COUNTS: [string, number][] = [
  ['Main Plant - Block A', 245], ['Main Plant - Block B', 180], ['Head Office', 132], ['Data Centre', 96],
  ['Warehouse', 210], ['Field Operations', 74], ['Transport Yard', 58],
];

/** The first Main Plant - Block A assets, as in the design: [model name, tag, sensor count, status]. */
const DESIGN_ASSETS: [string, string, number, AssetStatus][] = [
  ['Generator 500 kVA', 'GEN-00045', 8, 'In Service'],
  ['ATS Panel', 'ATS-00012', 4, 'In Service'],
  ['Fuel Tank 10,000L', 'FT-00008', 2, 'In Service'],
  ['Cooling Radiator', 'CR-00015', 3, 'In Service'],
  ['Control Panel', 'CP-00018', 6, 'In Service'],
  ['Exhaust System', 'EXH-00021', 2, 'Under Maintenance'],
  ['Battery Bank', 'BB-00033', 1, 'In Service'],
];

function sensorsFor(tag: string, kind: AssetKind, count: number): AuditSensor[] {
  return KIND_SENSORS[kind].slice(0, count).map((name, i) => ({
    id: `${tag}-S${String(i + 1).padStart(2, '0')}`,
    name,
    type: /Sensor|Detector|Analyzer|Gauge|Tracker/.test(name) ? 'Sensor' : 'Component',
  }));
}

function buildAssets(): AuditAsset[] {
  // Fixed seed so the sample list is the same on every load
  let seed = 45;
  const next = (n: number) => ((seed = (seed * 1103515245 + 12345) % 2147483648), seed % n);
  const counters: Record<string, number> = {};
  const serial = () => `SN${String(100000 + next(900000))}`;
  const assets: AuditAsset[] = [];

  for (const [name, tag, sensors, status] of DESIGN_ASSETS) {
    const category = ASSET_CATEGORIES.find((c) => CATEGORY_MODELS[c].some(([m]) => m === name))!;
    const [, prefix, kind] = CATEGORY_MODELS[category].find(([m]) => m === name)!;
    counters[prefix] = Number(tag.split('-')[1]);
    assets.push({ tag, serial: serial(), name, kind, category, location: 'Main Plant - Block A', department: 'Finance Department', status, sensors: sensorsFor(tag, kind, sensors) });
  }

  for (const [location, count] of SITE_COUNTS) {
    const have = assets.filter((a) => a.location === location).length;
    for (let i = have; i < count; i++) {
      const category = ASSET_CATEGORIES[next(ASSET_CATEGORIES.length)];
      const models = CATEGORY_MODELS[category];
      const [name, prefix, kind] = models[next(models.length)];
      counters[prefix] = (counters[prefix] ?? 0) + 1;
      const tag = `${prefix}-${String(counters[prefix]).padStart(5, '0')}`;
      const max = KIND_SENSORS[kind].length;
      assets.push({
        tag,
        serial: serial(),
        name,
        kind,
        category,
        location,
        department: AUDIT_DEPARTMENTS[next(AUDIT_DEPARTMENTS.length)],
        status: ASSET_STATUSES[next(10) < 8 ? 0 : next(10) < 5 ? 1 : 2],
        sensors: sensorsFor(tag, kind, max ? 1 + next(max) : 0),
      });
    }
  }
  return assets;
}

const AUDIT_ASSETS: AuditAsset[] = buildAssets();

const AUDIT_CRITERIA = [
  'Physical existence (asset present at location)',
  'Condition check (visual inspection)',
  'Asset tag / serial number verification',
  'Accessories / components check',
  'Compliance and documentation check',
  'Update location / assignment if changed',
  'Photo capture (mandatory)',
];

const AUDIT_GUIDELINES = [
  'Ensure all selected assets are physically verified.',
  'Capture clear photos for any discrepancies.',
  'Update asset condition and location if changed.',
  'Report missing, damaged, or non-compliant assets.',
  'Obtain necessary approvals after the audit.',
];

type CriterionType = 'Checklist' | 'Condition Assessment' | 'Compliance' | 'Documentation' | 'Custom';
type CriterionAppliesTo = 'All Assets' | 'Selected Assets';

interface AuditCriterion {
  id: string;
  name: string;
  description: string;
  category: string;
  type: CriterionType;
  method: string;
  appliesTo: CriterionAppliesTo;
  mandatory: boolean;
}

interface CriteriaSettings {
  photos: boolean;
  comments: boolean;
  mandatoryDefault: boolean;
  passFail: boolean;
  scoring: boolean;
  attachments: boolean;
}

const CRITERION_TYPES: CriterionType[] = ['Checklist', 'Condition Assessment', 'Compliance', 'Documentation', 'Custom'];
const CHECK_METHODS = ['Visual Inspection', 'Scan Barcode / RFID', 'Compare with record', 'Functional Test', 'Rating (1-5)', 'Document Check', 'Compare with telemetry', 'Measurement'];
const CRITERION_CATEGORIES = ['Existence', 'Identification', 'Location', 'Condition', 'Components', 'Compliance', 'Documentation', 'Safety', 'Other'];

type CriterionRow = [string, string, string, CriterionType, string, CriterionAppliesTo, boolean];

const CRITERIA_ROWS: CriterionRow[] = [
  ['Physical Presence', 'Asset is physically present at the recorded location.', 'Existence', 'Checklist', 'Visual Inspection', 'All Assets', true],
  ['Asset Identification', 'Asset tag / serial number is readable and matches the record.', 'Identification', 'Checklist', 'Scan Barcode / RFID', 'All Assets', true],
  ['Location Verification', 'Actual location matches the location in the register.', 'Location', 'Checklist', 'Compare with record', 'All Assets', true],
  ['Physical Condition', 'Visual check for damage, corrosion or leaks.', 'Condition', 'Checklist', 'Visual Inspection', 'All Assets', true],
  ['Accessories & Components', 'Accessories, sensors and components are present and fitted.', 'Components', 'Checklist', 'Visual Inspection', 'Selected Assets', false],
  ['Maintenance Tag', 'Maintenance tag is present and the last service is within schedule.', 'Compliance', 'Checklist', 'Visual Inspection', 'Selected Assets', false],
  ['Overall Condition Rating', 'Rate the overall condition from 1 (poor) to 5 (excellent).', 'Condition', 'Condition Assessment', 'Rating (1-5)', 'All Assets', true],
  ['Wear & Damage', 'Record visible wear, cracks, dents or missing parts.', 'Condition', 'Condition Assessment', 'Visual Inspection', 'All Assets', false],
  ['Operational Status', 'Asset starts and runs as expected.', 'Condition', 'Condition Assessment', 'Functional Test', 'Selected Assets', true],
  ['Sensor Readings', 'Sensor readings are within the normal range.', 'Condition', 'Condition Assessment', 'Compare with telemetry', 'Selected Assets', false],
  ['Safety Certification', 'Safety certificate is valid and displayed.', 'Safety', 'Compliance', 'Document Check', 'Selected Assets', true],
  ['Statutory Inspection', 'Statutory inspection is done and not overdue.', 'Compliance', 'Compliance', 'Document Check', 'Selected Assets', true],
  ['Environmental Compliance', 'Emissions, leaks and disposal meet environmental rules.', 'Compliance', 'Compliance', 'Visual Inspection', 'Selected Assets', false],
  ['Warranty Documents', 'Warranty / AMC documents are on file and current.', 'Documentation', 'Documentation', 'Document Check', 'All Assets', false],
  ['User Manual Available', 'Operating manual is available at the site.', 'Documentation', 'Documentation', 'Document Check', 'Selected Assets', false],
];

const CRITERIA_LIBRARY: AuditCriterion[] = CRITERIA_ROWS.map(([name, description, category, type, method, appliesTo, mandatory], i) => ({
  id: `CR-${String(i + 1).padStart(3, '0')}`, name, description, category, type, method, appliesTo, mandatory,
}));

/** Ready-made criteria sets for "Load from Template". */
const CRITERIA_TEMPLATES: { name: string; ids: string[] }[] = [
  { name: 'Standard Physical Audit', ids: ['CR-001', 'CR-002', 'CR-003', 'CR-004', 'CR-005', 'CR-006'] },
  { name: 'Condition Survey', ids: ['CR-001', 'CR-004', 'CR-007', 'CR-008', 'CR-009', 'CR-010'] },
  { name: 'Compliance Audit', ids: ['CR-001', 'CR-002', 'CR-006', 'CR-011', 'CR-012', 'CR-013', 'CR-014'] },
  { name: 'Full Audit (all criteria)', ids: CRITERIA_LIBRARY.map((c) => c.id) },
];

/** Library criteria matching each quick criterion on the Basic Information step. */
const QUICK_CRITERIA_MAP: Record<string, string[]> = {
  'Physical existence (asset present at location)': ['CR-001'],
  'Condition check (visual inspection)': ['CR-004'],
  'Asset tag / serial number verification': ['CR-002'],
  'Accessories / components check': ['CR-005'],
  'Compliance and documentation check': ['CR-006', 'CR-011', 'CR-014'],
  'Update location / assignment if changed': ['CR-003'],
  'Photo capture (mandatory)': [],
};

// ===== Assign & schedule =====

type TeamRole = 'Lead Auditor' | 'Auditor' | 'Observer';
type WorkingDays = 'Weekdays (Mon - Fri)' | 'Mon - Sat' | 'All days';
type PlanSequence = 'location' | 'category' | 'custom';

interface AuditUser {
  name: string;
  email: string;
  department: string;
  title: string;
}

interface TeamMember {
  user: AuditUser;
  role: TeamRole;
}

interface PlanRow {
  id: number;
  /** "YYYY-MM-DD" */
  date: string;
  /** Location or category the day's assets belong to. */
  group: string;
  tags: string[];
  /** Team member name; empty when nobody is assigned. */
  assignee: string;
  status: 'Scheduled';
}

interface ScheduleState {
  team: TeamMember[];
  start: string;
  end: string;
  workingDays: WorkingDays;
  startTime: string;
  endTime: string;
  sequence: PlanSequence;
  plan: PlanRow[];
  /** Inputs the plan was generated from; see planKey(). */
  planKey: string;
  notifications: { onAssignment: boolean; reminder: boolean; onCompletion: boolean; onChange: boolean };
}

const TEAM_ROLES: TeamRole[] = ['Lead Auditor', 'Auditor', 'Observer'];
const WORKING_DAYS: WorkingDays[] = ['Weekdays (Mon - Fri)', 'Mon - Sat', 'All days'];

const USER_ROWS: [string, string, string][] = [
  ['Ramesh Kumar', 'Operations', 'Operations Supervisor'],
  ['Nisha Patel', 'Facilities', 'Facilities Engineer'],
  ['Ahmed Salim', 'Technical', 'Technical Lead'],
  ['Salim Ahmed', 'Finance Department', 'Asset Accountant'],
  ['Ahmed Khan', 'IT Department', 'IT Asset Manager'],
  ['Priya Nair', 'Maintenance', 'Maintenance Planner'],
  ['Divya Menon', 'Administration', 'Admin Officer'],
  ['Arjun Rao', 'Logistics', 'Warehouse Lead'],
  ['Kavya Iyer', 'Compliance', 'Compliance Officer'],
  ['Rex Kumar', 'Administration', 'Administrator'],
];

const AUDIT_USERS: AuditUser[] = USER_ROWS.map(([name, department, title]) => ({
  name, department, title, email: `${name.toLowerCase().replace(/\s+/g, '.')}@dcs.com`,
}));

/** Dates from start to end (inclusive) that fall on the chosen working days. */
function workDates(start: string, end: string, days: WorkingDays): string[] {
  const out: string[] = [];
  if (!start || !end || end < start) return out;
  const d = new Date(`${start}T00:00:00`);
  const last = new Date(`${end}T00:00:00`);
  while (d <= last) {
    const dow = d.getDay();
    if (days === 'All days' || (days === 'Mon - Sat' ? dow !== 0 : dow !== 0 && dow !== 6)) out.push(isoDate(d));
    d.setDate(d.getDate() + 1);
  }
  return out;
}

/** Team members who carry out audit days (observers don't). */
function planAuditors(state: ScheduleState): string[] {
  return state.team.filter((m) => m.role !== 'Observer').map((m) => m.user.name);
}

/** Everything the generated plan depends on; a different key means the plan is out of date. */
function planKey(assets: AuditAsset[], state: ScheduleState): string {
  return JSON.stringify([assets.map((a) => a.tag), state.start, state.end, state.workingDays, state.sequence]);
}

/**
 * Spreads the assets over the working days, grouped by location or category, and hands the days to the auditors in turn.
 * "Custom" keeps the group order of the current plan, so rows moved by hand stay in that order.
 */
function buildPlan(assets: AuditAsset[], state: ScheduleState): void {
  const days = workDates(state.start, state.end, state.workingDays);
  const groupOf = (a: AuditAsset) => (state.sequence === 'category' ? a.category : a.location);
  const groups = new Map<string, string[]>();
  for (const a of assets) groups.set(groupOf(a), [...(groups.get(groupOf(a)) ?? []), a.tag]);

  let order = [...groups.keys()];
  if (state.sequence === 'custom') {
    const previous = [...new Set(state.plan.map((r) => r.group))].filter((g) => groups.has(g));
    order = [...previous, ...order.filter((g) => !previous.includes(g))];
  } else {
    order.sort((x, y) => x.localeCompare(y));
  }

  const plan: PlanRow[] = [];
  if (days.length && assets.length) {
    const perDay = Math.ceil(assets.length / days.length);
    let day = 0;
    let used = 0;
    for (const group of order) {
      const tags = groups.get(group) ?? [];
      for (let i = 0; i < tags.length; ) {
        // The last day takes whatever is left
        const room = day === days.length - 1 ? tags.length - i : perDay - used;
        const take = Math.min(room, tags.length - i);
        plan.push({ id: plan.length + 1, date: days[day], group, tags: tags.slice(i, i + take), assignee: '', status: 'Scheduled' });
        i += take;
        used += take;
        if (used >= perDay && day < days.length - 1) {
          day++;
          used = 0;
        }
      }
    }
  }
  const auditors = planAuditors(state);
  plan.forEach((r, i) => (r.assignee = auditors.length ? auditors[i % auditors.length] : ''));
  state.plan = plan;
  state.planKey = planKey(assets, state);
}

/** Next Monday–Friday day after today. */
function nextWeekday(from = new Date()): Date {
  const d = new Date(from.getFullYear(), from.getMonth(), from.getDate() + 1);
  while (d.getDay() === 0 || d.getDay() === 6) d.setDate(d.getDate() + 1);
  return d;
}

type AssetTab = 'assets' | 'sensors' | 'hierarchy';

interface SensorRow { sensor: AuditSensor; asset: AuditAsset }

interface HierarchyGroup { location: string; categories: { category: string; assets: AuditAsset[] }[] }

type Section = 'basic' | 'scope' | 'assets' | 'criteria' | 'library' | 'assign';

type CriterionForm = Omit<AuditCriterion, 'id'> & { id: string | null };

/** File attached on the Review & Submit step (kept in the browser until the API stores audit attachments). */
interface AuditAttachment {
  name: string;
  type: 'Template' | 'Image' | 'Document' | 'Spreadsheet' | 'PDF' | 'Other';
  size: number;
  uploadedBy: string;
  /** "YYYY-MM-DD" */
  date: string;
}

interface AuditDraft {
  name: string;
  type: AuditType | '';
  reference: string;
  description: string;
  location: string;
  department: string;
  category: string;
  selection: AssetSelection;
  /** Tags ticked in the asset picker ("Selected assets"). */
  tags: string[];
  rangeFrom: string;
  rangeTo: string;
  filterStatus: string;
  filterText: string;
  criteria: string[];
  otherChecked: boolean;
  otherText: string;
  /** Notes & instructions for the audit team. */
  notes: string;
}

@Component({
  selector: 'app-newasset-audit',
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './newasset-audit.html',
  styleUrl: './newasset-audit.css',
})
export class NewassetAudit implements OnDestroy {
  readonly icons = {
    ArrowLeft, ArrowRight, Building2, CalendarDays, Check, ChevronDown, ChevronLeft, ChevronRight, CircleCheck, ClipboardList, Cpu, EllipsisVertical, Eye, Folder, Funnel,
    FileImage, FileText, Info, ListChecks, MapPin, Package, Pencil, Plus, RefreshCw, Save, Search, Trash2, TriangleAlert, Upload, X, Zap,
  };
  readonly kindIcons: Record<AssetKind, typeof Zap> = {
    generator: Zap, electrical: PlugZap, tank: Cylinder, cooling: Fan, exhaust: Wind, hvac: AirVent, it: Server, vehicle: Truck, furniture: Armchair, safety: FireExtinguisher, tool: Wrench,
  };
  readonly steps = ['Basic Information', 'Scope & Assets', 'Audit Criteria', 'Assign & Schedule', 'Review & Submit'];
  readonly types = AUDIT_TYPES;
  readonly locations = AUDIT_LOCATIONS;
  readonly departments = AUDIT_DEPARTMENTS;
  readonly categories = ASSET_CATEGORIES;
  readonly assetStatuses = ASSET_STATUSES;
  readonly roles = TEAM_ROLES;
  readonly workingDayOptions = WORKING_DAYS;
  readonly sequences: { key: PlanSequence; label: string; hint: string }[] = [
    { key: 'location', label: 'By Location (recommended)', hint: 'Assets grouped by location for efficient audit.' },
    { key: 'category', label: 'By Asset Category', hint: 'Assets grouped by category.' },
    { key: 'custom', label: 'Custom Order', hint: 'Manually define sequence.' },
  ];
  readonly calWeekdays = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
  readonly today = isoDate(new Date());
  readonly criteriaOptions = AUDIT_CRITERIA;
  readonly guidelines = AUDIT_GUIDELINES;
  readonly criterionTypes = CRITERION_TYPES;
  readonly checkMethods = CHECK_METHODS;
  readonly criterionCategories = CRITERION_CATEGORIES;
  readonly templates = CRITERIA_TEMPLATES;
  readonly appliesOptions: CriterionAppliesTo[] = ['All Assets', 'Selected Assets'];
  readonly settingRows: { key: keyof CriteriaSettings; label: string; hint: string; icon: typeof Camera }[] = [
    { key: 'photos', label: 'Capture photos for all criteria', hint: 'Require photo evidence for each check item', icon: Camera },
    { key: 'passFail', label: 'Show pass/fail status', hint: 'Enable pass/fail result for checklist items', icon: CircleCheck },
    { key: 'comments', label: 'Allow comments', hint: 'Enable auditor comments for each criteria', icon: MessageSquare },
    { key: 'scoring', label: 'Enable scoring', hint: 'Assign scores to criteria (0-100)', icon: ChartColumn },
    { key: 'mandatoryDefault', label: 'Set default as mandatory', hint: 'Make new criteria mandatory by default', icon: Asterisk },
    { key: 'attachments', label: 'Allow attachments', hint: 'Allow file attachments for criteria', icon: Paperclip },
  ];
  readonly selections: { key: AssetSelection; label: string }[] = [
    { key: 'all', label: 'All assets in selected location' },
    { key: 'selected', label: 'Selected assets' },
    { key: 'range', label: 'By asset tag range' },
    { key: 'filter', label: 'By filter / criteria' },
  ];
  readonly descriptionMax = 1000;
  readonly pageSizes = [10, 25, 50];
  readonly suggestedReference = nextReference(AUDITS);

  /** 0-based index into steps. Step 0 also shows the scope and criteria sections, as in the design. */
  step = 0;
  d: AuditDraft = {
    name: '', type: '', reference: '', description: '',
    location: '', department: '', category: '', selection: 'all', tags: [], rangeFrom: '', rangeTo: '', filterStatus: '', filterText: '',
    criteria: AUDIT_CRITERIA.slice(0, 3), otherChecked: false, otherText: '', notes: '',
  };
  attachments: AuditAttachment[] = [];
  /** Notes card is being edited. */
  editingNotes = false;
  error = '';
  /** Section with the current error, so it shows the message. */
  errorAt: Section | null = null;

  /** Assets ticked on the Scope & Assets step, with the sensors picked per asset tag. */
  readonly pick: { tags: string[]; sensors: Record<string, string[]> } = { tags: [], sensors: {} };
  /** Scope the picks were last filled from; a changed scope refills them from the step 1 selection. */
  private pickedFor = '';

  // Scope & Assets step: tabs, filters and paging
  assetTab: AssetTab = 'assets';
  assetSearch = '';
  assetLocation = '';
  assetCategory = '';
  assetStatus = '';
  assetDepartment = '';
  moreFilters = false;
  pageSize = 10;
  page = 0;
  sensorPage = 0;
  expanded = new Set<string>();
  collapsed = new Set<string>();
  rowMenu: { asset: AuditAsset; top: number; right: number } | null = null;
  detail: AuditAsset | null = null;

  /** Criteria library for this audit (the standard ones plus any added here), the ids included, and settings. */
  readonly criteria: { items: AuditCriterion[]; included: string[]; settings: CriteriaSettings } = {
    items: CRITERIA_LIBRARY.map((c) => ({ ...c })),
    included: [],
    settings: { photos: true, comments: true, mandatoryDefault: false, passFail: true, scoring: true, attachments: true },
  };
  /** Quick criteria the inclusion was last filled from. */
  private criteriaFor = '';

  // Audit Criteria step: tabs, filters, paging and dialogs
  criteriaTab: CriterionType = 'Checklist';
  criteriaSearch = '';
  criteriaCategory = '';
  criteriaApplies = '';
  criteriaMandatory = '';
  criteriaMethod = '';
  criteriaMoreFilters = false;
  criteriaPageSize = 10;
  criteriaPage = 0;
  menuOpen = '';
  criterionMenu: { item: AuditCriterion; top: number; right: number } | null = null;
  criterionForm: CriterionForm | null = null;
  criterionError = '';
  criterionToDelete: AuditCriterion | null = null;

  /** Team, dates and plan from the Assign & Schedule step; starts on the next weekday for a week. */
  readonly schedule: ScheduleState = (() => {
    const start = nextWeekday();
    const end = new Date(start.getFullYear(), start.getMonth(), start.getDate() + 7);
    return {
      team: [], start: isoDate(start), end: isoDate(end), workingDays: 'Weekdays (Mon - Fri)', startTime: '09:00', endTime: '17:00', sequence: 'location',
      plan: [], planKey: '', notifications: { onAssignment: true, reminder: true, onCompletion: true, onChange: false },
    } as ScheduleState;
  })();
  /** Other audits' open dates, for the schedule calendar. */
  readonly otherAuditDates = AUDITS.filter((a) => a.status !== 'Completed').map((a) => a.scheduled);

  // Assign & Schedule step: team table, plan table, menus and dialogs
  teamSearch = '';
  checkedMembers = new Set<string>();
  checkedPlanRows = new Set<number>();
  bulkAssignee = '';
  teamMenu: { kind: 'member'; member: TeamMember; top: number; right: number } | { kind: 'plan'; row: PlanRow; top: number; right: number } | null = null;
  addForm: { search: string; picked: string[]; role: TeamRole } | null = null;
  rowForm: { row: PlanRow; date: string; assignee: string } | null = null;
  teamError = '';
  /** First day of the month shown in the schedule calendar. */
  calMonth = new Date(new Date().getFullYear(), new Date().getMonth(), 1);
  private scopeCache: { key: string; assets: AuditAsset[] } = { key: '', assets: [] };

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  preview: 'preview' | 'pick' | null = null;
  pickerSearch = '';
  previewSearch = '';

  private readonly router = inject(Router);
  private readonly host = inject(ElementRef<HTMLElement>);

  // ===== Scope =====

  /** Assets matching location, department and category (before the selection mode). */
  get scopeAssets(): AuditAsset[] {
    const d = this.d;
    // Cached so the asset tables get the same list between change detection runs
    const key = `${d.location}|${d.department}|${d.category}`;
    if (this.scopeCache.key !== key) {
      // No location chosen yet counts as all locations, so the asset list isn't empty when opened from the step bar
      const assets = AUDIT_ASSETS.filter(
        (a) =>
          (!d.location || d.location === 'All Locations' || a.location === d.location) &&
          (!d.department || a.department === d.department) &&
          (!d.category || a.category === d.category),
      );
      this.scopeCache = { key, assets };
    }
    return this.scopeCache.assets;
  }

  /** Assets the step 1 selection covers. */
  get auditAssets(): AuditAsset[] {
    const d = this.d;
    const scope = this.scopeAssets;
    switch (d.selection) {
      case 'selected':
        return scope.filter((a) => d.tags.includes(a.tag));
      case 'range': {
        const from = d.rangeFrom.trim().toUpperCase();
        const to = d.rangeTo.trim().toUpperCase();
        if (!from || !to) return [];
        return scope.filter((a) => a.tag >= from && a.tag <= to);
      }
      case 'filter': {
        const q = d.filterText.trim().toLowerCase();
        return scope.filter((a) => (!d.filterStatus || a.status === d.filterStatus) && (!q || `${a.tag} ${a.name}`.toLowerCase().includes(q)));
      }
      default:
        return scope;
    }
  }

  /** Once the Scope & Assets step is reached, the audit covers the assets ticked there. */
  get finalAssets(): AuditAsset[] {
    if (!this.pickedFor) return this.auditAssets;
    return this.pickedAssets;
  }

  /** Asset shown in the side card: the first one in the audit, else the first in scope. */
  get featured(): AuditAsset {
    return this.finalAssets[0] ?? this.scopeAssets[0] ?? AUDIT_ASSETS[0];
  }

  /** Fills the Scope & Assets picks from the step 1 selection, when the scope changed since last time. */
  private fillPick(): void {
    const d = this.d;
    const key = JSON.stringify([d.location, d.department, d.category, d.selection, d.tags, d.rangeFrom, d.rangeTo, d.filterStatus, d.filterText]);
    if (key === this.pickedFor) return;
    this.pickedFor = key;
    this.pick.tags = this.auditAssets.map((a) => a.tag);
    this.pick.sensors = Object.fromEntries(this.auditAssets.map((a) => [a.tag, a.sensors.map((x) => x.id)]));
    this.page = this.sensorPage = 0;
  }

  onScopeChange(): void {
    // Picked assets outside the new scope drop out
    const inScope = new Set(this.scopeAssets.map((a) => a.tag));
    this.d.tags = this.d.tags.filter((t) => inScope.has(t));
  }

  openPicker(): void {
    if (!this.d.location) return this.fail('scope', 'Choose the location / site first.');
    this.pickerSearch = '';
    this.preview = 'pick';
  }

  get pickerRows(): AuditAsset[] {
    return this.matchSearch(this.scopeAssets, this.pickerSearch);
  }

  togglePick(tag: string): void {
    this.toggle(this.d.tags, tag);
  }

  get previewRows(): AuditAsset[] {
    return this.matchSearch(this.finalAssets, this.previewSearch);
  }

  openPreview(): void {
    this.previewSearch = '';
    this.preview = 'preview';
  }

  private matchSearch(assets: AuditAsset[], text: string): AuditAsset[] {
    const q = text.trim().toLowerCase();
    return assets.filter((a) => !q || `${a.tag} ${a.name} ${a.category}`.toLowerCase().includes(q)).slice(0, 200);
  }

  // ===== Step 2: assets & sensors =====

  get assetLocations(): string[] {
    return [...new Set(this.scopeAssets.map((a) => a.location))];
  }

  get assetCategories(): string[] {
    return [...new Set(this.scopeAssets.map((a) => a.category))].sort();
  }

  get filteredAssets(): AuditAsset[] {
    const q = this.assetSearch.trim().toLowerCase();
    return this.scopeAssets.filter(
      (a) =>
        (!this.assetLocation || a.location === this.assetLocation) &&
        (!this.assetCategory || a.category === this.assetCategory) &&
        (!this.assetStatus || a.status === this.assetStatus) &&
        (!this.assetDepartment || a.department === this.assetDepartment) &&
        (!q || `${a.name} ${a.tag} ${a.serial} ${a.sensors.map((s) => s.name).join(' ')}`.toLowerCase().includes(q)),
    );
  }

  get sensorRows(): SensorRow[] {
    return this.filteredAssets.flatMap((asset) => asset.sensors.map((sensor) => ({ sensor, asset })));
  }

  onAssetFilter(): void {
    this.page = this.sensorPage = 0;
  }

  resetAssetFilters(): void {
    this.assetSearch = this.assetLocation = this.assetCategory = this.assetStatus = this.assetDepartment = '';
    this.onAssetFilter();
  }

  get pageRows(): AuditAsset[] {
    return this.filteredAssets.slice(this.page * this.pageSize, (this.page + 1) * this.pageSize);
  }

  get sensorPageRows(): SensorRow[] {
    return this.sensorRows.slice(this.sensorPage * this.pageSize, (this.sensorPage + 1) * this.pageSize);
  }

  get total(): number {
    return this.assetTab === 'sensors' ? this.sensorRows.length : this.filteredAssets.length;
  }

  get current(): number {
    return this.assetTab === 'sensors' ? this.sensorPage : this.page;
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.total / this.pageSize));
  }

  /** Page buttons around the current page, at most five. */
  get pages(): number[] {
    const start = Math.max(0, Math.min(this.current - 2, this.pageCount - 5));
    return Array.from({ length: Math.min(5, this.pageCount) }, (_, i) => start + i);
  }

  setPage(p: number): void {
    if (this.assetTab === 'sensors') this.sensorPage = p;
    else this.page = p;
  }

  get showingLabel(): string {
    const n = this.total;
    if (!n) return 'No records';
    return `Showing ${this.current * this.pageSize + 1} to ${Math.min(n, (this.current + 1) * this.pageSize)} of ${n} records`;
  }

  rowNo(i: number): number {
    return this.current * this.pageSize + i + 1;
  }

  isPicked(a: AuditAsset): boolean {
    return this.pick.tags.includes(a.tag);
  }

  setPicked(a: AuditAsset, on: boolean): void {
    const i = this.pick.tags.indexOf(a.tag);
    if (on && i < 0) {
      this.pick.tags.push(a.tag);
      this.pick.sensors[a.tag] = a.sensors.map((s) => s.id);
    } else if (!on && i >= 0) {
      this.pick.tags.splice(i, 1);
      delete this.pick.sensors[a.tag];
    }
  }

  toggleAsset(a: AuditAsset): void {
    this.setPicked(a, !this.isPicked(a));
  }

  allPicked(list: AuditAsset[]): boolean {
    return list.length > 0 && list.every((a) => this.isPicked(a));
  }

  somePicked(list: AuditAsset[]): boolean {
    return list.some((a) => this.isPicked(a)) && !this.allPicked(list);
  }

  setAll(list: AuditAsset[], on: boolean): void {
    for (const a of list) this.setPicked(a, on);
  }

  pickedSensors(a: AuditAsset): string[] {
    return this.pick.sensors[a.tag] ?? [];
  }

  isSensorPicked(a: AuditAsset, s: AuditSensor): boolean {
    return this.pickedSensors(a).includes(s.id);
  }

  /** Ticking a sensor of an unpicked asset picks the asset with just that sensor. */
  toggleSensor(a: AuditAsset, s: AuditSensor): void {
    if (!this.isPicked(a)) {
      this.pick.tags.push(a.tag);
      this.pick.sensors[a.tag] = [s.id];
      return;
    }
    const list = this.pickedSensors(a);
    this.pick.sensors[a.tag] = list.includes(s.id) ? list.filter((id) => id !== s.id) : [...list, s.id];
  }

  setSensors(a: AuditAsset, on: boolean): void {
    this.closeMenus();
    if (on) this.setPicked(a, true);
    if (this.isPicked(a)) this.pick.sensors[a.tag] = on ? a.sensors.map((s) => s.id) : [];
  }

  toggleExpand(a: AuditAsset): void {
    if (this.expanded.has(a.tag)) this.expanded.delete(a.tag);
    else this.expanded.add(a.tag);
  }

  /** Picked assets in the order they were ticked. */
  get pickedAssets(): AuditAsset[] {
    const byTag = new Map(this.scopeAssets.map((a) => [a.tag, a]));
    return this.pick.tags.map((t) => byTag.get(t)).filter((a): a is AuditAsset => !!a);
  }

  removeAll(): void {
    this.pick.tags.splice(0);
    for (const tag of Object.keys(this.pick.sensors)) delete this.pick.sensors[tag];
  }

  get hierarchy(): HierarchyGroup[] {
    const groups = new Map<string, Map<string, AuditAsset[]>>();
    for (const a of this.filteredAssets) {
      const cats = groups.get(a.location) ?? new Map<string, AuditAsset[]>();
      cats.set(a.category, [...(cats.get(a.category) ?? []), a]);
      groups.set(a.location, cats);
    }
    return [...groups].map(([location, cats]) => ({
      location,
      categories: [...cats].sort(([x], [y]) => x.localeCompare(y)).map(([category, assets]) => ({ category, assets })),
    }));
  }

  groupAssets(g: HierarchyGroup): AuditAsset[] {
    return g.categories.flatMap((c) => c.assets);
  }

  pickedCount(list: AuditAsset[]): number {
    return list.filter((a) => this.isPicked(a)).length;
  }

  toggleNode(key: string): void {
    if (this.collapsed.has(key)) this.collapsed.delete(key);
    else this.collapsed.add(key);
  }

  /** Picks the assets listed in an Excel / CSV file, matched by asset tag or serial number. */
  importFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => {
      try {
        const book = XLSX.read(reader.result, { type: 'array' });
        const rows = XLSX.utils.sheet_to_json<unknown[]>(book.Sheets[book.SheetNames[0]], { header: 1, blankrows: false });
        const header = (rows[0] ?? []).map((c) => String(c ?? '').toLowerCase());
        let col = header.findIndex((h) => h.includes('tag'));
        if (col < 0) col = header.findIndex((h) => h.includes('serial'));
        const values = rows.slice(col < 0 ? 0 : 1).map((r) => String(r[Math.max(col, 0)] ?? '').trim().toUpperCase()).filter(Boolean);

        const byKey = new Map<string, AuditAsset>();
        for (const a of this.scopeAssets) byKey.set(a.tag.toUpperCase(), a).set(a.serial.toUpperCase(), a);
        let added = 0;
        let missing = 0;
        for (const v of new Set(values)) {
          const a = byKey.get(v);
          if (!a) missing++;
          else if (!this.isPicked(a)) {
            this.setPicked(a, true);
            added++;
          }
        }
        this.showToast(`${added} asset${added === 1 ? '' : 's'} added from ${file.name}` + (missing ? `; ${missing} not found in this audit's scope.` : '.'));
      } catch {
        this.showToast(`Could not read ${file.name}. Use an Excel or CSV file with an "Asset Tag" column.`);
      }
    };
    reader.readAsArrayBuffer(file);
  }

  openRowMenu(a: AuditAsset, event: MouseEvent): void {
    event.stopPropagation();
    if (this.rowMenu?.asset === a) return void (this.rowMenu = null);
    const r = (event.currentTarget as HTMLElement).getBoundingClientRect();
    // Opens below the button, or above it near the bottom of the window
    const top = r.bottom + 180 > window.innerHeight ? r.top - 176 : r.bottom + 6;
    this.rowMenu = { asset: a, top, right: window.innerWidth - r.right };
  }

  openDetail(a: AuditAsset): void {
    this.closeMenus();
    this.detail = a;
  }

  get progress(): number {
    const total = this.scopeAssets.length;
    return total ? (this.finalAssets.length / total) * 100 : 0;
  }

  // ===== Criteria =====

  toggle(list: string[], value: string): void {
    const i = list.indexOf(value);
    if (i >= 0) list.splice(i, 1);
    else list.push(value);
  }

  get criteriaList(): string[] {
    const other = this.d.otherChecked ? this.d.otherText.trim() : '';
    return other ? [...this.d.criteria, other] : this.d.criteria;
  }

  // ===== Step 3: criteria library =====

  get includedCriteria(): AuditCriterion[] {
    return this.criteria.items.filter((c) => this.criteria.included.includes(c.id));
  }

  get mandatoryCount(): number {
    return this.includedCriteria.filter((c) => c.mandatory).length;
  }

  get settingsSummary(): string {
    const st = this.criteria.settings;
    const on = [st.photos && 'photos required', st.comments && 'comments', st.passFail && 'pass/fail', st.scoring && 'scoring (0-100)', st.attachments && 'attachments'].filter(Boolean);
    return on.length ? on.join(', ') : 'none';
  }

  /** Fills the Audit Criteria step from the quick criteria on step 1, when those changed since last time. */
  private fillCriteria(): void {
    const d = this.d;
    const other = d.otherChecked ? d.otherText.trim() : '';
    const key = JSON.stringify([d.criteria, other]);
    if (key === this.criteriaFor) return;
    this.criteriaFor = key;
    const ids = new Set(d.criteria.flatMap((c) => QUICK_CRITERIA_MAP[c] ?? []));
    if (other) {
      let custom = this.criteria.items.find((c) => c.type === 'Custom' && c.name.toLowerCase() === other.toLowerCase());
      if (!custom) {
        custom = {
          id: this.nextCriterionId(), name: other, description: 'Added on the Basic Information step.', category: 'Other', type: 'Custom',
          method: 'Visual Inspection', appliesTo: 'All Assets', mandatory: this.criteria.settings.mandatoryDefault,
        };
        this.criteria.items.push(custom);
      }
      ids.add(custom.id);
    }
    this.criteria.included.splice(0, this.criteria.included.length, ...ids);
    this.criteria.settings.photos = d.criteria.includes('Photo capture (mandatory)');
    this.criteriaPage = 0;
  }

  criteriaCount(type: CriterionType): number {
    return this.criteria.items.filter((c) => c.type === type).length;
  }

  get filteredCriteria(): AuditCriterion[] {
    const q = this.criteriaSearch.trim().toLowerCase();
    return this.criteria.items.filter(
      (c) =>
        c.type === this.criteriaTab &&
        (!this.criteriaCategory || c.category === this.criteriaCategory) &&
        (!this.criteriaApplies || c.appliesTo === this.criteriaApplies) &&
        (!this.criteriaMandatory || c.mandatory === (this.criteriaMandatory === 'yes')) &&
        (!this.criteriaMethod || c.method === this.criteriaMethod) &&
        (!q || `${c.name} ${c.description}`.toLowerCase().includes(q)),
    );
  }

  setCriteriaTab(type: CriterionType): void {
    this.criteriaTab = type;
    this.criteriaPage = 0;
  }

  onCriteriaFilter(): void {
    this.criteriaPage = 0;
  }

  resetCriteriaFilters(): void {
    this.criteriaSearch = this.criteriaCategory = this.criteriaApplies = this.criteriaMandatory = this.criteriaMethod = '';
    this.onCriteriaFilter();
  }

  get criteriaRows(): AuditCriterion[] {
    return this.filteredCriteria.slice(this.criteriaPage * this.criteriaPageSize, (this.criteriaPage + 1) * this.criteriaPageSize);
  }

  get criteriaPageCount(): number {
    return Math.max(1, Math.ceil(this.filteredCriteria.length / this.criteriaPageSize));
  }

  get criteriaPages(): number[] {
    return Array.from({ length: this.criteriaPageCount }, (_, i) => i);
  }

  get criteriaShowing(): string {
    const n = this.filteredCriteria.length;
    if (!n) return 'No records';
    return `Showing ${this.criteriaPage * this.criteriaPageSize + 1} to ${Math.min(n, (this.criteriaPage + 1) * this.criteriaPageSize)} of ${n} records`;
  }

  isIncluded(c: AuditCriterion): boolean {
    return this.criteria.included.includes(c.id);
  }

  setIncluded(c: AuditCriterion, on: boolean): void {
    const list = this.criteria.included;
    const i = list.indexOf(c.id);
    if (on && i < 0) list.push(c.id);
    else if (!on && i >= 0) list.splice(i, 1);
  }

  allIncluded(list: AuditCriterion[]): boolean {
    return list.length > 0 && list.every((c) => this.isIncluded(c));
  }

  someIncluded(list: AuditCriterion[]): boolean {
    return list.some((c) => this.isIncluded(c)) && !this.allIncluded(list);
  }

  setAllIncluded(list: AuditCriterion[], on: boolean): void {
    for (const c of list) this.setIncluded(c, on);
  }

  toggleMandatory(c: AuditCriterion): void {
    this.closeMenus();
    c.mandatory = !c.mandatory;
  }

  loadTemplate(t: { name: string; ids: string[] }): void {
    this.menuOpen = '';
    // Custom criteria stay included; the standard ones are replaced by the template's
    const custom = this.criteria.included.filter((id) => this.criteria.items.find((c) => c.id === id)?.type === 'Custom');
    this.criteria.included.splice(0, this.criteria.included.length, ...t.ids, ...custom);
    this.showToast(`Loaded "${t.name}": ${t.ids.length} criteria included.`);
  }

  openAddCriterion(): void {
    this.closeMenus();
    this.criterionError = '';
    this.criterionForm = {
      id: null, name: '', description: '', category: 'Other', type: this.criteriaTab === 'Checklist' ? 'Custom' : this.criteriaTab, method: 'Visual Inspection',
      appliesTo: 'All Assets', mandatory: this.criteria.settings.mandatoryDefault,
    };
  }

  openEditCriterion(c: AuditCriterion): void {
    this.closeMenus();
    this.criterionError = '';
    this.criterionForm = { ...c };
  }

  saveCriterion(): void {
    const f = this.criterionForm;
    if (!f) return;
    const name = f.name.trim();
    if (!name) return void (this.criterionError = 'Enter the criteria name.');
    if (this.criteria.items.some((c) => c.id !== f.id && c.name.toLowerCase() === name.toLowerCase())) return void (this.criterionError = `"${name}" already exists.`);

    const fields = { ...f, name, description: f.description.trim() };
    const existing = this.criteria.items.find((c) => c.id === f.id);
    if (existing) {
      Object.assign(existing, fields, { id: existing.id });
      this.showToast(`"${name}" updated.`);
    } else {
      const item: AuditCriterion = { ...fields, id: this.nextCriterionId() };
      this.criteria.items.push(item);
      this.setIncluded(item, true);
      this.setCriteriaTab(item.type);
      this.showToast(`"${name}" added and included in the audit.`);
    }
    this.criterionForm = null;
  }

  duplicateCriterion(c: AuditCriterion): void {
    this.closeMenus();
    let name = `${c.name} (copy)`;
    for (let n = 2; this.criteria.items.some((x) => x.name === name); n++) name = `${c.name} (copy ${n})`;
    const item: AuditCriterion = { ...c, id: this.nextCriterionId(), name, type: 'Custom' };
    this.criteria.items.push(item);
    this.setIncluded(item, true);
    this.showToast(`"${name}" added to Custom.`);
  }

  openDeleteCriterion(c: AuditCriterion): void {
    this.closeMenus();
    this.criterionToDelete = c;
  }

  confirmDeleteCriterion(): void {
    const c = this.criterionToDelete;
    if (!c) return;
    this.setIncluded(c, false);
    this.criteria.items.splice(this.criteria.items.indexOf(c), 1);
    this.criterionToDelete = null;
    if (this.criteriaPage >= this.criteriaPageCount) this.criteriaPage = this.criteriaPageCount - 1;
    this.showToast(`"${c.name}" removed.`);
  }

  private nextCriterionId(): string {
    const max = this.criteria.items.reduce((n, c) => Math.max(n, Number(c.id.split('-')[1]) || 0), 0);
    return `CR-${String(max + 1).padStart(3, '0')}`;
  }

  toggleMenu(name: string, event: Event): void {
    event.stopPropagation();
    this.criterionMenu = this.rowMenu = null;
    this.menuOpen = this.menuOpen === name ? '' : name;
  }

  openCriterionMenu(item: AuditCriterion, event: MouseEvent): void {
    event.stopPropagation();
    this.menuOpen = '';
    if (this.criterionMenu?.item === item) return void (this.criterionMenu = null);
    const r = (event.currentTarget as HTMLElement).getBoundingClientRect();
    // Opens below the button, or above it near the bottom of the window
    const top = r.bottom + 220 > window.innerHeight ? r.top - 216 : r.bottom + 6;
    this.criterionMenu = { item, top, right: window.innerWidth - r.right };
  }

  // ===== Step 4: team =====

  get filteredTeam(): TeamMember[] {
    const q = this.teamSearch.trim().toLowerCase();
    return this.schedule.team.filter((m) => !q || `${m.user.name} ${m.role} ${m.user.department} ${m.user.email}`.toLowerCase().includes(q));
  }

  get auditorNames(): string[] {
    return planAuditors(this.schedule);
  }

  get leadAuditor(): string {
    return this.schedule.team.find((m) => m.role === 'Lead Auditor')?.user.name ?? '';
  }

  assignedCount(name: string): number {
    return this.schedule.plan.filter((r) => r.assignee === name).reduce((n, r) => n + r.tags.length, 0);
  }

  initials(name: string): string {
    return name.split(/\s+/).filter(Boolean).slice(0, 2).map((w) => w[0].toUpperCase()).join('');
  }

  /** Stable avatar colour per name. */
  avatarHue(name: string): number {
    let h = 0;
    for (const ch of name) h = (h * 31 + ch.charCodeAt(0)) % 360;
    return h;
  }

  /** Only one lead: making someone lead turns the previous lead into an auditor. */
  setRole(member: TeamMember, role: TeamRole): void {
    this.closeMenus();
    if (role === 'Lead Auditor') for (const m of this.schedule.team) if (m !== member && m.role === 'Lead Auditor') m.role = 'Auditor';
    member.role = role;
    if (role === 'Observer') this.handOver([member.user.name]);
    else this.fillUnassigned();
  }

  allMembersChecked(): boolean {
    return this.filteredTeam.length > 0 && this.filteredTeam.every((m) => this.checkedMembers.has(m.user.name));
  }

  toggleAllMembers(on: boolean): void {
    for (const m of this.filteredTeam) {
      if (on) this.checkedMembers.add(m.user.name);
      else this.checkedMembers.delete(m.user.name);
    }
  }

  toggleMember(name: string): void {
    if (this.checkedMembers.has(name)) this.checkedMembers.delete(name);
    else this.checkedMembers.add(name);
  }

  removeCheckedMembers(): void {
    this.removeMembers([...this.checkedMembers]);
  }

  removeMembers(names: string[]): void {
    this.closeMenus();
    this.schedule.team = this.schedule.team.filter((m) => !names.includes(m.user.name));
    for (const n of names) this.checkedMembers.delete(n);
    this.handOver(names);
    this.showToast(names.length === 1 ? `${names[0]} removed from the team.` : `${names.length} members removed from the team.`);
  }

  /** Gives the plan days of these people to the remaining auditors in turn. */
  private handOver(names: string[]): void {
    const auditors = this.auditorNames;
    let i = 0;
    for (const r of this.schedule.plan) {
      if (names.includes(r.assignee)) r.assignee = auditors.length ? auditors[i++ % auditors.length] : '';
    }
  }

  /** Plan days without anyone get the auditors in turn. */
  private fillUnassigned(): void {
    const auditors = this.auditorNames;
    let i = 0;
    for (const r of this.schedule.plan) if (!r.assignee && auditors.length) r.assignee = auditors[i++ % auditors.length];
  }

  openAddMember(): void {
    this.closeMenus();
    this.teamError = '';
    this.addForm = { search: '', picked: [], role: this.schedule.team.some((m) => m.role === 'Lead Auditor') ? 'Auditor' : 'Lead Auditor' };
  }

  get addChoices(): AuditUser[] {
    const f = this.addForm;
    if (!f) return [];
    const q = f.search.trim().toLowerCase();
    const inTeam = new Set(this.schedule.team.map((m) => m.user.name));
    return AUDIT_USERS.filter((u) => !inTeam.has(u.name) && (!q || `${u.name} ${u.department} ${u.title}`.toLowerCase().includes(q)));
  }

  togglePickUser(name: string): void {
    const picked = this.addForm?.picked;
    if (!picked) return;
    const i = picked.indexOf(name);
    if (i >= 0) picked.splice(i, 1);
    else picked.push(name);
  }

  saveAddMember(): void {
    const f = this.addForm;
    if (!f) return;
    if (!f.picked.length) return void (this.teamError = 'Tick the people to add.');
    f.picked.forEach((name, i) => {
      const user = AUDIT_USERS.find((u) => u.name === name);
      if (!user) return;
      const member: TeamMember = { user, role: 'Auditor' };
      this.schedule.team.push(member);
      // Only the first person picked can become the lead
      this.setRole(member, f.role === 'Lead Auditor' && i > 0 ? 'Auditor' : f.role);
    });
    this.showToast(f.picked.length === 1 ? `${f.picked[0]} added to the team.` : `${f.picked.length} members added to the team.`);
    this.addForm = null;
  }

  // ===== Step 4: schedule & plan =====

  get workDayCount(): number {
    return workDates(this.schedule.start, this.schedule.end, this.schedule.workingDays).length;
  }

  get planStale(): boolean {
    return this.schedule.planKey !== planKey(this.finalAssets, this.schedule);
  }

  get planDates(): string[] {
    return [...new Set(this.schedule.plan.map((r) => r.date))];
  }

  get notificationSummary(): string {
    const n = this.schedule.notifications;
    const on = [n.onAssignment && 'on assignment', n.reminder && '1 day before', n.onCompletion && 'on completion', n.onChange && 'on schedule change'].filter(Boolean);
    return on.length ? on.join(', ') : 'none';
  }

  /** Makes the first plan when the step is reached; later changes ask for a regenerate instead. */
  private ensurePlan(): void {
    if (!this.schedule.plan.length) buildPlan(this.finalAssets, this.schedule);
    this.showStartMonth();
  }

  regenerate(): void {
    const sc = this.schedule;
    if (!sc.start || !sc.end) return this.showToast('Choose the start and end dates first.');
    if (sc.end < sc.start) return this.showToast('The end date must be on or after the start date.');
    if (!this.workDayCount) return this.showToast('No working days between the start and end dates.');
    buildPlan(this.finalAssets, sc);
    this.checkedPlanRows.clear();
    this.showToast(`Plan generated: ${sc.plan.length} audit day${sc.plan.length === 1 ? '' : 's'} for ${this.finalAssets.length} assets.`);
  }

  allPlanChecked(): boolean {
    return this.schedule.plan.length > 0 && this.schedule.plan.every((r) => this.checkedPlanRows.has(r.id));
  }

  toggleAllPlan(on: boolean): void {
    for (const r of this.schedule.plan) {
      if (on) this.checkedPlanRows.add(r.id);
      else this.checkedPlanRows.delete(r.id);
    }
  }

  togglePlanRow(id: number): void {
    if (this.checkedPlanRows.has(id)) this.checkedPlanRows.delete(id);
    else this.checkedPlanRows.add(id);
  }

  assignChecked(): void {
    if (!this.bulkAssignee) return;
    for (const r of this.schedule.plan) if (this.checkedPlanRows.has(r.id)) r.assignee = this.bulkAssignee;
    this.showToast(`${this.checkedPlanRows.size} day${this.checkedPlanRows.size === 1 ? '' : 's'} assigned to ${this.bulkAssignee}.`);
    this.checkedPlanRows.clear();
    this.bulkAssignee = '';
  }

  /** Swaps a day's work with its neighbour; the dates stay in order. */
  movePlanRow(row: PlanRow, by: -1 | 1): void {
    this.closeMenus();
    const plan = this.schedule.plan;
    const other = plan[plan.indexOf(row) + by];
    if (!other) return;
    [row.group, other.group] = [other.group, row.group];
    [row.tags, other.tags] = [other.tags, row.tags];
    [row.assignee, other.assignee] = [other.assignee, row.assignee];
    // Moving rows by hand switches to the custom order, so regenerating keeps it
    if (this.schedule.sequence !== 'custom') {
      this.schedule.sequence = 'custom';
      this.schedule.planKey = planKey(this.finalAssets, this.schedule);
    }
  }

  openRowEdit(row: PlanRow): void {
    this.closeMenus();
    this.teamError = '';
    this.rowForm = { row, date: row.date, assignee: row.assignee };
  }

  saveRowEdit(): void {
    const f = this.rowForm;
    if (!f) return;
    if (!f.date) return void (this.teamError = 'Choose the date.');
    if (f.date < this.schedule.start || f.date > this.schedule.end) return void (this.teamError = 'The date must be within the audit start and end dates.');
    if (!f.assignee) return void (this.teamError = 'Choose who will audit this day.');
    f.row.date = f.date;
    f.row.assignee = f.assignee;
    this.schedule.plan.sort((x, y) => x.date.localeCompare(y.date) || x.id - y.id);
    this.rowForm = null;
  }

  get menuMember(): TeamMember | null {
    return this.teamMenu?.kind === 'member' ? this.teamMenu.member : null;
  }

  get menuPlanRow(): PlanRow | null {
    return this.teamMenu?.kind === 'plan' ? this.teamMenu.row : null;
  }

  openMemberMenu(member: TeamMember, event: MouseEvent): void {
    this.openTeamMenu(event, (top, right) => ({ kind: 'member', member, top, right }), this.menuMember === member);
  }

  openPlanMenu(row: PlanRow, event: MouseEvent): void {
    this.openTeamMenu(event, (top, right) => ({ kind: 'plan', row, top, right }), this.menuPlanRow === row);
  }

  private openTeamMenu(event: MouseEvent, make: (top: number, right: number) => NonNullable<NewassetAudit['teamMenu']>, isOpen: boolean): void {
    event.stopPropagation();
    this.closeMenus();
    if (isOpen) return;
    const r = (event.currentTarget as HTMLElement).getBoundingClientRect();
    // Opens below the button, or above it near the bottom of the window
    const top = r.bottom + 180 > window.innerHeight ? r.top - 176 : r.bottom + 6;
    this.teamMenu = make(top, window.innerWidth - r.right);
  }

  // ===== Schedule calendar =====

  /** Monday-first weeks of the shown month, starting on the Monday on or before the 1st. */
  get calWeeks(): { iso: string; day: number; inMonth: boolean }[][] {
    const first = this.calMonth;
    const cursor = new Date(first.getFullYear(), first.getMonth(), 1 - ((first.getDay() + 6) % 7));
    const weeks: { iso: string; day: number; inMonth: boolean }[][] = [];
    do {
      const week = [];
      for (let i = 0; i < 7; i++) {
        week.push({ iso: isoDate(cursor), day: cursor.getDate(), inMonth: cursor.getMonth() === first.getMonth() });
        cursor.setDate(cursor.getDate() + 1);
      }
      weeks.push(week);
    } while (cursor.getMonth() === first.getMonth());
    return weeks;
  }

  shiftMonth(by: number): void {
    this.calMonth = new Date(this.calMonth.getFullYear(), this.calMonth.getMonth() + by, 1);
  }

  /** Jumps the calendar to the start date's month. */
  showStartMonth(): void {
    if (!this.schedule.start) return;
    const d = new Date(`${this.schedule.start}T00:00:00`);
    this.calMonth = new Date(d.getFullYear(), d.getMonth(), 1);
  }

  inRange(iso: string): boolean {
    return !!this.schedule.start && !!this.schedule.end && iso >= this.schedule.start && iso <= this.schedule.end;
  }

  // ===== Step 5: attachments =====

  addAttachments(event: Event): void {
    const input = event.target as HTMLInputElement;
    const files = [...(input.files ?? [])];
    input.value = '';
    // DEMO MODE: the uploader is the lead auditor until the signed-in user is available here
    const uploadedBy = this.leadAuditor || 'You';
    for (const f of files) {
      this.attachments.push({ name: f.name, type: this.fileType(f.name), size: f.size, uploadedBy, date: isoDate(new Date()) });
    }
    if (files.length) this.showToast(`${files.length} file${files.length === 1 ? '' : 's'} attached.`);
  }

  removeAttachment(a: AuditAttachment): void {
    this.attachments.splice(this.attachments.indexOf(a), 1);
  }

  private fileType(name: string): AuditAttachment['type'] {
    const ext = name.split('.').pop()?.toLowerCase() ?? '';
    if (/template/i.test(name)) return 'Template';
    if (['png', 'jpg', 'jpeg', 'gif', 'webp', 'bmp'].includes(ext)) return 'Image';
    if (ext === 'pdf') return 'PDF';
    if (['xlsx', 'xls', 'csv'].includes(ext)) return 'Spreadsheet';
    if (['doc', 'docx', 'txt', 'rtf'].includes(ext)) return 'Document';
    return 'Other';
  }

  fileSize(bytes: number): string {
    if (bytes >= 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
    return `${Math.max(1, Math.round(bytes / 1024))} KB`;
  }

  // ===== Steps =====

  /** First problem in a step, or null when it's complete. */
  private stepError(step: number): { at: Section; message: string } | null {
    const d = this.d;
    const fail = (at: Section, message: string) => ({ at, message });
    if (step === 0) {
      if (!d.name.trim()) return fail('basic', 'Enter the audit name.');
      if (!d.type) return fail('basic', 'Choose the audit type.');
      const ref = d.reference.trim().toUpperCase();
      if (ref && AUDITS.some((a) => a.reference === ref)) return fail('basic', `Audit reference ${ref} is already used.`);
      if (!d.location) return fail('scope', 'Choose the location / site.');
      if (d.selection === 'range' && (!d.rangeFrom.trim() || !d.rangeTo.trim())) return fail('scope', 'Enter both the from and to asset tags.');
      if (d.selection === 'selected' && !d.tags.length) return fail('scope', 'Pick the assets to audit.');
      if (!this.auditAssets.length) return fail('scope', 'No assets match this scope. Change the location, filters or selection.');
      if (d.otherChecked && !d.otherText.trim()) return fail('criteria', 'Enter the details for "Other" criteria.');
    }
    if (step === 1 && !this.finalAssets.length) return fail('assets', 'Select at least one asset for the audit.');
    if (step === 2 && !this.includedCriteria.length) return fail('library', 'Include at least one criteria in the audit.');
    if (step === 3) {
      const sc = this.schedule;
      if (!planAuditors(sc).length) return fail('assign', 'Add at least one auditor to the team.');
      if (!this.leadAuditor) return fail('assign', 'Set one team member\'s role to Lead Auditor.');
      if (!sc.start || !sc.end) return fail('assign', 'Choose the start and end dates.');
      if (sc.end < sc.start) return fail('assign', 'The end date must be on or after the start date.');
      if (sc.startTime && sc.endTime && sc.endTime <= sc.startTime) return fail('assign', 'The daily end time must be after the start time.');
      if (!sc.plan.length) return fail('assign', 'Generate the audit plan (Regenerate Plan).');
      if (sc.planKey !== planKey(this.finalAssets, sc)) return fail('assign', 'The audit plan is out of date. Press Regenerate Plan.');
      if (sc.plan.some((r) => !r.assignee)) return fail('assign', 'Assign every plan day to an auditor.');
    }
    return null;
  }

  private fail(at: Section, message: string): void {
    this.error = message;
    this.errorAt = at;
    this.scrollTo(at);
  }

  /** Checks steps from..to-1; on a problem, moves there and shows it. */
  private checkSteps(from: number, to: number): boolean {
    for (let s = from; s < to; s++) {
      const problem = this.stepError(s);
      if (problem) {
        this.step = s;
        this.fail(problem.at, problem.message);
        return false;
      }
      if (s === 0) this.fillPick();
      if (s === 1) this.fillCriteria();
      if (s === 2) this.ensurePlan();
    }
    return true;
  }

  /** Moves to a step; with check = true the steps on the way must be complete. Next, Back and the step bar move freely; Submit checks every step. */
  goStep(target: number, check = true): void {
    if (check && !this.checkSteps(this.step, target)) return;
    if (target >= 1) this.fillPick();
    if (target >= 2) this.fillCriteria();
    if (target >= 3) this.ensurePlan();
    this.error = '';
    this.errorAt = null;
    this.step = target;
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  next(): void {
    if (this.step === this.steps.length - 1) return this.submit();
    this.goStep(this.step + 1, false);
  }

  back(): void {
    if (this.step === 0) return this.cancel();
    this.goStep(this.step - 1);
  }

  private scrollTo(at: Section): void {
    setTimeout(() => {
      const el = (this.host.nativeElement as HTMLElement).querySelector(`#na-${at}`);
      el?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
  }

  // ===== Save =====

  cancel(): void {
    this.router.navigate(['/assets/audit']);
  }

  saveDraft(): void {
    if (!this.d.name.trim()) {
      this.step = 0;
      return this.fail('basic', 'Enter the audit name to save a draft.');
    }
    const reference = this.save('Draft');
    this.router.navigate(['/assets/audit'], { state: { toast: `${reference} saved as draft.` } });
  }

  submit(): void {
    const n = this.finalAssets.length;
    const reference = this.save('Scheduled');
    this.router.navigate(['/assets/audit'], { state: { toast: `${reference} scheduled for ${n} asset${n === 1 ? '' : 's'}.` } });
  }

  /** DEMO MODE: adds the audit to the shared in-memory list. */
  private save(status: AuditStatus): string {
    const d = this.d;
    const reference = d.reference.trim().toUpperCase() || nextReference(AUDITS);
    AUDITS.unshift({
      id: AUDITS.reduce((n, a) => Math.max(n, a.id), 0) + 1,
      reference,
      name: d.name.trim(),
      type: d.type || 'Periodic',
      location: d.location,
      scheduled: this.schedule.start || isoDate(new Date()),
      completed: '',
      status,
      auditor: this.leadAuditor,
      findings: null,
      notes: [d.description.trim(), d.notes.trim()].filter(Boolean).join('\n\n'),
      assetCount: this.finalAssets.length,
      dueDate: this.schedule.end,
    });
    return reference;
  }

  // ===== Display helpers =====

  /** Line under each step title: progress once there is some, else what the step covers. */
  stepSub(i: number): string {
    const n = this.finalAssets.length;
    switch (i) {
      case 0: return this.d.name.trim() || 'Name, type and scope';
      case 1: return this.d.location ? `${n} asset${n === 1 ? '' : 's'} selected` : 'Select the assets';
      case 2: return this.criteriaFor ? `${this.includedCriteria.length} criteria selected` : 'What to verify';
      case 3: return this.leadAuditor || 'Team and dates';
      default: return 'Verify and submit';
    }
  }

  get sequenceLabel(): string {
    return { location: 'By Location', category: 'By Asset Category', custom: 'Custom Order' }[this.schedule.sequence];
  }

  get selectionLabel(): string {
    return this.selections.find((s) => s.key === this.d.selection)?.label ?? '';
  }

  showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 5000);
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
  }

  @HostListener('document:click')
  @HostListener('window:scroll')
  @HostListener('window:resize')
  @HostListener('document:wheel')
  closeMenus(): void {
    this.rowMenu = this.criterionMenu = this.teamMenu = null;
    this.menuOpen = '';
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeMenus();
    this.preview = this.detail = null;
    this.criterionForm = this.criterionToDelete = null;
    this.addForm = this.rowForm = null;
  }
}
