import { ASSET360_MODULES } from '../shared/admin-common';

// Option lists, feature catalog and defaults for Module Access. Policies come from the Administration API
// (module-access.service.ts); module grants, features, exceptions and security settings have no backend fields yet and stay client-side.

export type PolicyType = 'Role' | 'Team' | 'User';
export type PolicyStatus = 'Active' | 'Inactive';
export type ModulePerm = 'view' | 'create' | 'edit' | 'approve' | 'export';
export type AccessLevel = 'Full Access' | 'Limited' | 'Read Only' | 'Restricted';
export type ExceptionAccess = 'Full Access' | 'Read Only' | 'No Access';
export type PolicyIcon = 'user' | 'users' | 'wrench' | 'box' | 'shield' | 'eye' | 'coins' | 'server' | 'hammer' | 'badge' | 'search' | 'building';
export type Tone = 'purple' | 'blue' | 'green' | 'pink' | 'orange';

export interface ModuleGrant {
  module: string;
  enabled: boolean;
  /** null = not applicable to the module */
  perms: Record<ModulePerm, boolean | null>;
  /** Permissions inherited from the parent policy (shown with a lock, not editable) */
  locked: ModulePerm[];
  scope: string;
}

export interface AccessException {
  id: string;
  user: string;
  module: string;
  access: ExceptionAccess;
  reason: string;
  /** ISO date, '' = no expiry */
  expires: string;
  addedBy: string;
  addedOn: string;
}

export interface PolicyRecord {
  id: string;
  name: string;
  code: string;
  description: string;
  type: PolicyType;
  status: PolicyStatus;
  icon: PolicyIcon;
  tone: Tone;
  users: number;
  roles: string[];
  grants: ModuleGrant[];
  /** Feature ids switched on for this policy */
  features: string[];
  exceptions: AccessException[];
  dataScope: string;
  landingModule: string;
  sessionTimeout: number;
  mfaRequired: boolean;
  updatedAt: string;
  updatedBy: string;
}

export interface FeatureDef {
  id: string;
  module: string;
  name: string;
  restricted: boolean;
}

export const MODULE_PERMS: { id: ModulePerm; label: string }[] = [
  { id: 'view', label: 'View' },
  { id: 'create', label: 'Create' },
  { id: 'edit', label: 'Edit' },
  { id: 'approve', label: 'Approve' },
  { id: 'export', label: 'Export' }
];

/** Actions that don't exist in a module */
const NOT_APPLICABLE: Record<string, ModulePerm[]> = {
  Dashboard: ['create', 'edit', 'approve'],
  'Tracking & IoT': ['approve'],
  'Master Data Management': ['approve']
};

export const MODULE_SCOPES = ['All Data', 'All Assets', 'Company Locations', 'Assigned Locations', 'Assigned Teams', 'Selected Locations', 'Read Only', 'Restricted'];
export const POLICY_SCOPES = ['All Data', 'Company Locations', 'Assigned Locations', 'Assigned Teams', 'Own Records'];
export const POLICY_TYPES: PolicyType[] = ['Role', 'Team', 'User'];
export const EXCEPTION_ACCESS: ExceptionAccess[] = ['Full Access', 'Read Only', 'No Access'];

export function scopeTone(scope: string): string {
  if (scope === 'All Data' || scope === 'All Assets') return 'green';
  if (scope === 'Read Only') return 'purple';
  if (scope === 'Restricted') return 'pink';
  return 'blue';
}

// ===== Feature catalog: 86 features, 42 restricted =====
const FEATURE_NAMES: Record<string, [string, boolean][]> = {
  Dashboard: [['Executive Overview', true], ['Operations Dashboard', false], ['KPI Widgets', false], ['Custom Dashboards', true], ['Live Alerts Feed', false]],
  Assets: [['Asset Register', false], ['Asset Details', false], ['Bulk Upload', true], ['Asset Transfer', false], ['Asset Disposal', true], ['Asset Revaluation', true], ['QR / Barcode Labels', false], ['Asset Hierarchy', false], ['Delete Assets', true]],
  'Tracking & IoT': [['Live Map', false], ['RFID Readers', true], ['BLE Gateways', true], ['Geofences', false], ['Device Health', false], ['Sensor Telemetry', false], ['Device Provisioning', true]],
  'Movement & Custody': [['Check-out', false], ['Check-in', false], ['Custody Transfer', false], ['Gate Pass', true], ['Movement Approval', true], ['Custody History', false], ['Lost & Found', true]],
  Maintenance: [['Work Orders', false], ['Preventive Schedules', false], ['Service Requests', false], ['Spare Parts Issue', false], ['Vendor Assignment', true], ['Cost Approval', true], ['Downtime Log', false], ['Close Work Orders', true], ['Maintenance Calendar', false]],
  Inspection: [['Inspection Rounds', false], ['Checklists', false], ['Findings', false], ['Photo Evidence', false], ['Inspection Sign-off', true], ['Template Designer', true]],
  Inventory: [['Stock Levels', false], ['Goods Receipt', false], ['Stock Issue', false], ['Stock Transfer', false], ['Stock Adjustment', true], ['Cycle Count', false], ['Stock Write-off', true], ['Reorder Rules', true]],
  'Audit & Compliance': [['Audit Plans', false], ['Audit Execution', false], ['Findings & CAPA', false], ['Compliance Register', true], ['Evidence Vault', true], ['Regulatory Reports', true], ['Audit Sign-off', true]],
  'Contracts & Warranty': [['Contracts', false], ['Warranties', false], ['AMC Schedules', false], ['Renewal Approval', true], ['Contract Values', true], ['Vendor Ratings', false]],
  Financial: [['Depreciation', true], ['Asset Valuation', true], ['Cost Centers', true], ['Budget Tracking', true], ['Invoices', true], ['GL Posting', true], ['Financial Reports', true]],
  'Reports & Analytics': [['Standard Reports', false], ['Report Builder', true], ['Scheduled Reports', false], ['Data Export', true], ['Analytics Workspace', false], ['Share Reports', false]],
  Administration: [['User Management', true], ['Roles & Permissions', true], ['System Settings', true], ['Integrations & API', true], ['Audit Logs', true], ['Workflow Builder', true]],
  'Master Data Management': [['Master Records', false], ['Bulk Import', true], ['Data Approval', false]]
};

let featureNo = 0;
export const FEATURES: FeatureDef[] = ASSET360_MODULES.flatMap((module) =>
  (FEATURE_NAMES[module] ?? []).map(([name, restricted]) => ({ id: `FT-${String(++featureNo).padStart(3, '0')}`, module, name, restricted }))
);

export function featuresOf(module: string): FeatureDef[] {
  return FEATURES.filter((f) => f.module === module);
}

// ===== Access levels =====
type Level = 'full' | 'limited' | 'read' | 'off';

function grant(module: string, level: Level, scope?: string, locked: ModulePerm[] = []): ModuleGrant {
  const na = NOT_APPLICABLE[module] ?? [];
  const perms = {} as Record<ModulePerm, boolean | null>;
  for (const p of MODULE_PERMS) {
    if (na.includes(p.id)) {
      perms[p.id] = null;
      continue;
    }
    perms[p.id] =
      level === 'full' ||
      (level === 'read' && (p.id === 'view' || p.id === 'export')) ||
      (level === 'limited' && p.id !== (na.includes('approve') ? 'export' : 'approve'));
  }
  const fallback = level === 'full' ? 'All Data' : level === 'read' ? 'Read Only' : level === 'limited' ? 'Assigned Locations' : 'Restricted';
  return { module, enabled: level !== 'off', perms, locked, scope: scope ?? fallback };
}

/** Full / Limited / Read Only / Restricted for one module grant. */
export function accessLevel(g: ModuleGrant): AccessLevel {
  if (!g.enabled) return 'Restricted';
  const applicable = MODULE_PERMS.filter((p) => g.perms[p.id] !== null);
  if (applicable.every((p) => g.perms[p.id])) return 'Full Access';
  if (g.perms.view && !g.perms.create && !g.perms.edit && !g.perms.approve) return 'Read Only';
  return 'Limited';
}

export interface AccessSummary {
  enabled: number;
  total: number;
  full: number;
  readOnly: number;
  restricted: number;
}

/** Access Summary box: restricted = everything that is neither full nor read-only (limited or switched off). */
export function accessSummary(p: PolicyRecord): AccessSummary {
  const levels = p.grants.map(accessLevel);
  const full = levels.filter((l) => l === 'Full Access').length;
  const readOnly = levels.filter((l) => l === 'Read Only').length;
  return { enabled: p.grants.filter((g) => g.enabled).length, total: p.grants.length, full, readOnly, restricted: p.grants.length - full - readOnly };
}

export function blankGrants(): ModuleGrant[] {
  return ASSET360_MODULES.map((m) => grant(m, m === 'Dashboard' ? 'read' : 'off', m === 'Dashboard' ? 'All Data' : undefined));
}

/** Default features for a grant: everything for full access, standard features otherwise */
function defaultFeatures(grants: ModuleGrant[]): string[] {
  return grants.flatMap((g) => {
    if (!g.enabled) return [];
    const full = accessLevel(g) === 'Full Access';
    return featuresOf(g.module).filter((f) => full || !f.restricted).map((f) => f.id);
  });
}

// ===== Policies =====
export function blankPolicy(): PolicyRecord {
  const grants = blankGrants();
  return {
    id: '',
    name: '',
    code: '',
    description: '',
    type: 'Role',
    status: 'Active',
    icon: 'users',
    tone: 'purple',
    users: 0,
    roles: [],
    grants,
    features: defaultFeatures(grants),
    exceptions: [],
    dataScope: 'Assigned Locations',
    landingModule: 'Dashboard',
    sessionTimeout: 60,
    mfaRequired: false,
    updatedAt: '',
    updatedBy: ''
  };
}

/** Standard features of a module (switched on when the module is enabled) */
export function standardFeatureIds(module: string): string[] {
  return featuresOf(module).filter((f) => !f.restricted).map((f) => f.id);
}
