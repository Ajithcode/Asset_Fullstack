/**
 * Asset assignment types and helpers. Assignments, assets, employees, departments and sites come from the API
 * (see asset-assignment.service.ts); only lists the API has no source for are kept here.
 */

export type AssignmentType = 'Employee' | 'Department' | 'Location' | 'Other';
export type AssignmentStatus = 'Assigned' | 'Overdue' | 'Returned';
export type AssetKind = 'laptop' | 'phone' | 'scanner' | 'projector' | 'forklift' | 'tool' | 'card' | 'monitor' | 'tablet' | 'camera' | 'other';

export interface AssignmentEvent {
  /** "YYYY-MM-DDTHH:mm" */
  at: string;
  action: string;
  by: string;
  note: string;
}

/** One row of the assignment list: one asset of an API assignment. */
export interface Assignment {
  /** Row key: "{assignmentId}|{assetRegistrationId}". */
  id: string;
  /** The API assignment (one assignment can hold several assets). */
  assignmentId: string;
  assignmentNumber: string;
  assetRegistrationId: string;
  assetName: string;
  assetType: string;
  kind: AssetKind;
  tag: string;
  serial: string;
  type: AssignmentType;
  /** Employee name, department name or location name. */
  assignee: string;
  /** Job title, "Department" or the site the location belongs to. */
  assigneeSub: string;
  department: string;
  location: string;
  /** "YYYY-MM-DD" */
  from: string;
  /** "YYYY-MM-DD"; empty for an open-ended assignment. */
  to: string;
  status: AssignmentStatus;
  assignedBy: string;
  assignedByRole: string;
  /** "YYYY-MM-DDTHH:mm" */
  assignedOn: string;
  remarks: string;
  /** Extra facts captured by the Assign Asset wizard (vendor contact, work order, ...), shown in the details panel. */
  extra?: { label: string; value: string }[];
  /** Loaded from the event log when the History tab is opened. */
  history: AssignmentEvent[];
}

/** An asset that can be picked in the Assign Asset wizard. */
export interface PoolAsset {
  assetName: string;
  assetType: string;
  kind: AssetKind;
  tag: string;
  serial: string;
}

export type EntityKind = 'Vendor / Supplier' | 'Contractor' | 'Customer / Client' | 'Partner' | 'Project' | 'Cost Center' | 'Other';

/** External parties assets can be assigned to ("Assign to Other Entity"). */
export interface Entity {
  kind: EntityKind;
  name: string;
  contact: string;
  phone: string;
  email: string;
  location: string;
  /** People at the entity who can be made responsible for the assets. */
  people: string[];
}

export interface Employee {
  name: string;
  id: string;
  title: string;
  department: string;
  location: string;
}

/**
 * Sample employees. Not used by the assignment pages (they read master data employees);
 * kept because the Asset Disposal page still imports it.
 */
export const EMPLOYEES: Employee[] = [
  { name: 'Arjun Mehta', id: 'EMP-00125', title: 'IT Manager', department: 'IT Department', location: 'Head Office - Floor 2' },
  { name: 'Priya Nair', id: 'EMP-00131', title: 'Sales Executive', department: 'Sales', location: 'Head Office - Floor 3' },
  { name: 'Ramesh Kumar', id: 'EMP-00121', title: 'Operations', department: 'Operations', location: 'Main Plant' },
  { name: 'Salim Ahmed', id: 'EMP-00151', title: 'Technician', department: 'Maintenance', location: 'Main Plant' },
  { name: 'Nisha Patel', id: 'EMP-00140', title: 'Admin', department: 'Administration', location: 'Head Office - Floor 2' },
  { name: 'Khalid Al Mansoori', id: 'EMP-00160', title: 'Field Engineer', department: 'Field Services', location: 'Main Plant' },
  { name: 'Sara Ahmed', id: 'EMP-00137', title: 'Facilities Lead', department: 'Operations', location: 'Head Office - Floor 3' },
  { name: 'Mohan Kumar', id: 'EMP-00128', title: 'Network Engineer', department: 'IT Department', location: 'Data Center' },
  { name: 'Fatima Hassan', id: 'EMP-00133', title: 'Accountant', department: 'Finance', location: 'Head Office - Floor 3' },
  { name: 'Vikram Singh', id: 'EMP-00145', title: 'Storekeeper', department: 'Warehouse', location: 'Warehouse A' },
];

export const ENTITY_KINDS: EntityKind[] = ['Vendor / Supplier', 'Contractor', 'Customer / Client', 'Partner', 'Project', 'Cost Center', 'Other'];

/**
 * Directory of other entities for "Assign to Other Entity". The Asset API stores the entity's details on the
 * assignment and has no entity list, so these remain sample data.
 */
export const ENTITIES: Entity[] = [
  { kind: 'Vendor / Supplier', name: 'TechServe LLC', contact: 'Sameer Khan', phone: '+971 50 123 4567', email: 'sameer.khan@techserve.ae', location: 'TechServe Service Center, Dubai', people: ['Ahmed Raza', 'Sameer Khan', 'Joseph Mathew'] },
  { kind: 'Vendor / Supplier', name: 'Gulf IT Solutions', contact: 'Rania Saleh', phone: '+971 55 234 9876', email: 'rania@gulfit.ae', location: 'Gulf IT Workshop, Sharjah', people: ['Rania Saleh', 'Imran Ali'] },
  { kind: 'Vendor / Supplier', name: 'Al Futtaim Equipment', contact: 'Daniel Fernandes', phone: '+971 4 345 1122', email: 'daniel.f@afequip.ae', location: 'Al Futtaim Depot, Jebel Ali', people: ['Daniel Fernandes', 'Omar Hadi'] },
  { kind: 'Contractor', name: 'BuildRight Contracting', contact: 'Yusuf Kareem', phone: '+971 52 778 1200', email: 'yusuf@buildright.ae', location: 'Site Office, Main Plant', people: ['Yusuf Kareem', 'Pradeep Nair'] },
  { kind: 'Contractor', name: 'SafeGuard Security', contact: 'Hamza Tariq', phone: '+971 56 410 3300', email: 'hamza@safeguard.ae', location: 'Security Post, Main Entrance', people: ['Hamza Tariq', 'Leo Dsouza'] },
  { kind: 'Customer / Client', name: 'Emirates Logistics', contact: 'Aisha Rahman', phone: '+971 4 889 2000', email: 'aisha.r@emlog.ae', location: 'Emirates Logistics Hub, Dubai South', people: ['Aisha Rahman', 'Kevin Brown'] },
  { kind: 'Partner', name: 'Purple IQ Labs', contact: 'Meera Iyer', phone: '+971 50 900 1100', email: 'meera@purpleiq.ai', location: 'Innovation Lab, Dubai Silicon Oasis', people: ['Meera Iyer', 'Tom Wilson'] },
  { kind: 'Project', name: 'Warehouse Automation 2025', contact: 'Vikram Singh', phone: '+971 54 222 3344', email: 'vikram.singh@asset360.ae', location: 'Warehouse A', people: ['Vikram Singh', 'Ramesh Kumar'] },
  { kind: 'Cost Center', name: 'CC-4100 Field Operations', contact: 'Fatima Hassan', phone: '+971 50 333 4455', email: 'fatima.h@asset360.ae', location: 'Head Office - Floor 3', people: ['Fatima Hassan'] },
  { kind: 'Other', name: 'Dubai Expo Exhibition Stand', contact: 'Nisha Patel', phone: '+971 55 777 8899', email: 'nisha.patel@asset360.ae', location: 'Expo City, Dubai', people: ['Nisha Patel', 'Priya Nair'] },
];

/** Cost center choices; master data has no cost center list, so employees' cost centers are added to these. */
export const COST_CENTERS = ['IT-001 - IT Operations', 'AD-002 - Administration', 'OP-010 - Operations', 'SE-020 - Security', 'WH-030 - Warehouse', 'FN-040 - Finance', 'SL-050 - Sales'];

export const TARGET_LABELS: Record<AssignmentType, string> = { Employee: 'Employee', Department: 'Department', Location: 'Location/Site', Other: 'Other Entity' };

/** Sample user name; kept because the Asset Disposal page still imports it. The assignment pages use the signed-in user. */
export const CURRENT_USER = 'Rex Kumar';

const pad = (n: number, w = 2) => String(n).padStart(w, '0');

export const isoDate = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
export const stamp = (d: Date) => `${isoDate(d)}T${pad(d.getHours())}:${pad(d.getMinutes())}`;

const KIND_WORDS: [AssetKind, RegExp][] = [
  ['laptop', /laptop|notebook|macbook|thinkpad|latitude|elitebook/i],
  ['tablet', /tablet|ipad/i],
  ['phone', /phone|mobile|iphone|galaxy s/i],
  ['scanner', /scanner|barcode|rfid/i],
  ['projector', /projector/i],
  ['forklift', /forklift|pallet|material handling/i],
  ['card', /card|badge|access/i],
  ['monitor', /monitor|display|screen/i],
  ['camera', /camera|cctv/i],
  ['tool', /tool|drill|wrench|equipment/i],
];

/** Icon kind from the asset type / category / name text. */
export function kindOf(text: string): AssetKind {
  return KIND_WORDS.find(([, re]) => re.test(text))?.[0] ?? 'other';
}

export interface DepartmentManager {
  name: string;
  id: string;
  title: string;
  /** Where the manager sits. */
  location: string;
}

export interface LocationDetails {
  site: string;
  building: string;
  floor: string;
  room: string;
  costCenter: string;
}

/** A place in the Location / Site tree; sites carry the facts shown when one is picked. */
export interface SiteNode {
  name: string;
  kind: 'Country' | 'City' | 'Site' | 'Building';
  children?: SiteNode[];
  code?: string;
  type?: string;
  address?: string;
  building?: string;
  floors?: number;
}
