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

export const ENTITY_KINDS: EntityKind[] = ['Vendor / Supplier', 'Contractor', 'Customer / Client', 'Partner', 'Project', 'Cost Center', 'Other'];

/**
 * Directory of other entities for "Assign to Other Entity". The Asset API stores the entity's details on the
 * assignment and has no entity list yet, so this stays empty until one is served.
 */
export const ENTITIES: Entity[] = [];

/** Extra cost center choices; master data has no cost center list, so the pages list the employees' cost centers. */
export const COST_CENTERS: string[] = [];

export const TARGET_LABELS: Record<AssignmentType, string> = { Employee: 'Employee', Department: 'Department', Location: 'Location/Site', Other: 'Other Entity' };

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
