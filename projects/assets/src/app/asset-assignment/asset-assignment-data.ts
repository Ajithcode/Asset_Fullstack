/**
 * Asset assignment sample data (DEMO MODE) until the Assignments API is wired up.
 * The first ten rows match the design; the rest are generated from a fixed seed so the list pages and filters have something to work with.
 */

export type AssignmentType = 'Employee' | 'Department' | 'Location' | 'Other';
export type AssignmentStatus = 'Assigned' | 'Overdue' | 'Returned';
export type AssetKind = 'laptop' | 'phone' | 'scanner' | 'projector' | 'forklift' | 'tool' | 'card' | 'monitor' | 'tablet' | 'camera';

export interface AssignmentEvent {
  /** "YYYY-MM-DDTHH:mm" */
  at: string;
  action: string;
  by: string;
  note: string;
}

export interface Assignment {
  id: number;
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

export const DEPARTMENTS = ['IT Department', 'Sales', 'Warehouse', 'Operations', 'Finance', 'Administration', 'Maintenance', 'Field Services'];

export const LOCATIONS = ['Head Office - Floor 2', 'Head Office - Floor 3', 'Meeting Room 1', 'Main Plant', 'Main Entrance', 'Warehouse A', 'Data Center'];

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

/** Assets in store with no assignment yet (DEMO MODE). */
export const UNASSIGNED_ASSETS: PoolAsset[] = [
  { assetName: 'Dell Latitude 7440', assetType: 'Laptop', kind: 'laptop', tag: 'IT-000457', serial: '8G2H1K8' },
  { assetName: 'Dell Latitude 7440', assetType: 'Laptop', kind: 'laptop', tag: 'IT-000458', serial: '9H3J2L7' },
  { assetName: 'iPhone 15', assetType: 'Mobile Phone', kind: 'phone', tag: 'MOB-001129', serial: '354882105' },
  { assetName: 'Zebra TC53', assetType: 'Barcode Scanner', kind: 'scanner', tag: 'IT-000790', serial: '5A8D9K4' },
  { assetName: 'LG 27" Monitor', assetType: 'Monitor', kind: 'monitor', tag: 'IT-000639', serial: 'M27UL1' },
  { assetName: 'iPad Air', assetType: 'Tablet', kind: 'tablet', tag: 'TAB-000322', serial: '9K1L2M4' },
  { assetName: 'Bosch GSR 18V', assetType: 'Power Tool', kind: 'tool', tag: 'TL-000446', serial: '18V-7789' },
  { assetName: 'Epson EB-L630U', assetType: 'Projector', kind: 'projector', tag: 'AV-000232', serial: 'X9L23M9' },
];

const BY = 'Rex Kumar';
const BY_ROLE = 'Administrator';

type Seed = Omit<Assignment, 'id' | 'assignedBy' | 'assignedByRole' | 'assignedOn' | 'history' | 'remarks'> & { remarks?: string };

const DESIGN_ROWS: Seed[] = [
  { assetName: 'Dell Latitude 7440', assetType: 'Laptop', kind: 'laptop', tag: 'IT-000456', serial: '7F3K2D9', type: 'Employee', assignee: 'Arjun Mehta', assigneeSub: 'IT Manager', department: 'IT Department', location: 'Head Office - Floor 2', from: '2025-01-01', to: '', status: 'Assigned', remarks: 'Assigned for IT operations' },
  { assetName: 'iPhone 15', assetType: 'Mobile Phone', kind: 'phone', tag: 'MOB-001128', serial: '354882104', type: 'Employee', assignee: 'Priya Nair', assigneeSub: 'Sales Executive', department: 'Sales', location: 'Head Office - Floor 3', from: '2025-03-15', to: '', status: 'Assigned' },
  { assetName: 'Zebra TC53', assetType: 'Barcode Scanner', kind: 'scanner', tag: 'IT-000789', serial: '5A8D9K3', type: 'Department', assignee: 'Warehouse Dept.', assigneeSub: 'Department', department: 'Warehouse', location: 'Warehouse A', from: '2025-02-01', to: '', status: 'Assigned' },
  { assetName: 'Epson EB-L630U', assetType: 'Projector', kind: 'projector', tag: 'AV-000231', serial: 'X9L23M8', type: 'Location', assignee: 'Meeting Room 1', assigneeSub: 'Head Office', department: 'Administration', location: 'Meeting Room 1', from: '2025-01-10', to: '', status: 'Assigned' },
  { assetName: 'Toyota 8FBET16', assetType: 'Forklift', kind: 'forklift', tag: 'EQ-000112', serial: '8FBE16-322', type: 'Employee', assignee: 'Ramesh Kumar', assigneeSub: 'Operations', department: 'Operations', location: 'Main Plant', from: '2024-08-05', to: '', status: 'Assigned' },
  { assetName: 'Bosch GSR 18V', assetType: 'Power Tool', kind: 'tool', tag: 'TL-000445', serial: '18V-7788', type: 'Employee', assignee: 'Salim Ahmed', assigneeSub: 'Technician', department: 'Maintenance', location: 'Main Plant', from: '2025-06-12', to: '2025-12-20', status: 'Assigned' },
  { assetName: 'HID Access Card', assetType: 'Access Card', kind: 'card', tag: 'AC-001992', serial: 'AC9921', type: 'Employee', assignee: 'Nisha Patel', assigneeSub: 'Admin', department: 'Administration', location: 'Head Office - Floor 2', from: '2025-01-01', to: '', status: 'Assigned' },
  { assetName: 'LG 27" Monitor', assetType: 'Monitor', kind: 'monitor', tag: 'IT-000638', serial: 'M27UK9', type: 'Department', assignee: 'Finance Dept.', assigneeSub: 'Department', department: 'Finance', location: 'Head Office - Floor 3', from: '2025-02-20', to: '', status: 'Assigned' },
  { assetName: 'iPad Air', assetType: 'Tablet', kind: 'tablet', tag: 'TAB-000321', serial: '9K1L2M3', type: 'Employee', assignee: 'Khalid Al Mansoori', assigneeSub: 'Field Engineer', department: 'Field Services', location: 'Main Plant', from: '2025-09-01', to: '', status: 'Assigned' },
  { assetName: 'Axis P3265-LVE', assetType: 'IP Camera', kind: 'camera', tag: 'CAM-000891', serial: 'AX3265L', type: 'Location', assignee: 'Main Entrance', assigneeSub: 'Main Plant', department: 'Operations', location: 'Main Entrance', from: '2025-04-01', to: '', status: 'Assigned' },
];

const CATALOG: { name: string; type: string; kind: AssetKind; prefix: string }[] = [
  { name: 'HP EliteBook 840', type: 'Laptop', kind: 'laptop', prefix: 'IT' },
  { name: 'Lenovo ThinkPad T14', type: 'Laptop', kind: 'laptop', prefix: 'IT' },
  { name: 'Samsung Galaxy S24', type: 'Mobile Phone', kind: 'phone', prefix: 'MOB' },
  { name: 'Honeywell CT45', type: 'Barcode Scanner', kind: 'scanner', prefix: 'IT' },
  { name: 'Dell P2423 Monitor', type: 'Monitor', kind: 'monitor', prefix: 'IT' },
  { name: 'Makita DHP485', type: 'Power Tool', kind: 'tool', prefix: 'TL' },
  { name: 'Samsung Galaxy Tab S9', type: 'Tablet', kind: 'tablet', prefix: 'TAB' },
  { name: 'Hikvision DS-2CD2143', type: 'IP Camera', kind: 'camera', prefix: 'CAM' },
  { name: 'BenQ MW560', type: 'Projector', kind: 'projector', prefix: 'AV' },
  { name: 'HID Access Card', type: 'Access Card', kind: 'card', prefix: 'AC' },
];

const pad = (n: number, w = 2) => String(n).padStart(w, '0');

function generated(count: number): Seed[] {
  let seed = 20250101;
  const rnd = () => {
    seed = (seed * 1664525 + 1013904223) >>> 0;
    return seed / 0x100000000;
  };
  const pick = <T>(list: T[]) => list[Math.floor(rnd() * list.length)];
  const rows: Seed[] = [];
  for (let i = 0; i < count; i++) {
    const item = pick(CATALOG);
    const r = rnd();
    const type: AssignmentType = r < 0.68 ? 'Employee' : r < 0.86 ? 'Department' : 'Location';
    const month = 1 + Math.floor(rnd() * 10);
    const from = `2025-${pad(month)}-${pad(1 + Math.floor(rnd() * 27))}`;
    const hasEnd = rnd() < 0.3;
    const to = hasEnd ? `2025-${pad(Math.min(12, month + 2 + Math.floor(rnd() * 5)))}-${pad(1 + Math.floor(rnd() * 27))}` : '';
    const s = rnd();
    const status: AssignmentStatus = s < 0.82 ? 'Assigned' : s < 0.92 ? 'Overdue' : 'Returned';
    const base = {
      assetName: item.name, assetType: item.type, kind: item.kind, tag: `${item.prefix}-${pad(1200 + i * 37, 6)}`,
      serial: Math.floor(rnd() * 0xfffffff).toString(36).toUpperCase().padStart(6, '0'), from, to, status,
    };
    if (type === 'Employee') {
      const e = pick(EMPLOYEES);
      rows.push({ ...base, type, assignee: e.name, assigneeSub: e.title, department: e.department, location: e.location });
    } else if (type === 'Department') {
      const d = pick(DEPARTMENTS);
      rows.push({ ...base, type, assignee: `${d.replace(' Department', '')} Dept.`, assigneeSub: 'Department', department: d, location: pick(LOCATIONS) });
    } else {
      const l = pick(LOCATIONS);
      rows.push({ ...base, type, assignee: l, assigneeSub: l.startsWith('Head Office') || l === 'Meeting Room 1' ? 'Head Office' : 'Main Plant', department: pick(DEPARTMENTS), location: l });
    }
  }
  return rows;
}

function build(seed: Seed, i: number): Assignment {
  const assignedOn = i === 0 ? '2025-01-01T10:30' : `${seed.from}T${pad(9 + (i % 8))}:${pad((i * 13) % 60)}`;
  const history: AssignmentEvent[] = [{ at: assignedOn, action: 'Assigned', by: BY, note: `Assigned to ${seed.assignee}` }];
  if (seed.to) history.push({ at: `${seed.from}T17:00`, action: 'Return date set', by: BY, note: `Due back ${seed.to}` });
  if (seed.status === 'Returned') history.push({ at: `${seed.to || seed.from}T16:30`, action: 'Returned', by: BY, note: 'Asset returned to store' });
  return {
    ...seed,
    id: i + 1,
    remarks: seed.remarks ?? '',
    assignedBy: BY,
    assignedByRole: BY_ROLE,
    assignedOn,
    history,
  };
}

/** DEMO MODE: more unassigned stock, so pickers and the bulk sample file have realistic volume. */
UNASSIGNED_ASSETS.push(
  ...Array.from({ length: 190 }, (_, i): PoolAsset => {
    const item = CATALOG[i % CATALOG.length];
    return { assetName: item.name, assetType: item.type, kind: item.kind, tag: `${item.prefix}-${pad(5000 + i * 7, 6)}`, serial: `ST${pad(73_000 + i * 131, 6)}` };
  }),
);

/** DEMO MODE: the in-memory assignment store, shared by the list and the Assign Asset wizard so new assignments survive moving between them. */
export const ASSIGNMENTS: Assignment[] = [...DESIGN_ROWS, ...generated(38)].map(build);

export const TARGET_LABELS: Record<AssignmentType, string> = { Employee: 'Employee', Department: 'Department', Location: 'Location/Site', Other: 'Other Entity' };

export const CURRENT_USER = 'Rex Kumar';
export const CURRENT_ROLE = 'Administrator';

export const isoDate = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
export const stamp = (d: Date) => `${isoDate(d)}T${pad(d.getHours())}:${pad(d.getMinutes())}`;

export function assigneeChoices(type: AssignmentType): string[] {
  if (type === 'Employee') return EMPLOYEES.map((e) => e.name);
  if (type === 'Other') return ENTITIES.map((e) => e.name);
  return type === 'Department' ? DEPARTMENTS : LOCATIONS;
}

/** Assignee name, subtitle, department and location for a new assignment. */
export function assigneeDetails(type: AssignmentType, name: string): Pick<Assignment, 'assignee' | 'assigneeSub' | 'department' | 'location'> {
  if (type === 'Employee') {
    const e = EMPLOYEES.find((x) => x.name === name);
    return { assignee: name, assigneeSub: e?.title ?? '', department: e?.department ?? '', location: e?.location ?? '' };
  }
  if (type === 'Department') return { assignee: `${name.replace(' Department', '')} Dept.`, assigneeSub: 'Department', department: name, location: '' };
  if (type === 'Other') {
    const e = ENTITIES.find((x) => x.name === name);
    return { assignee: name, assigneeSub: e?.kind ?? 'Other Entity', department: '', location: e?.location ?? '' };
  }
  return { assignee: name, assigneeSub: name.startsWith('Head Office') || name === 'Meeting Room 1' ? 'Head Office' : 'Main Plant', department: '', location: name };
}

const CATEGORIES: Record<AssetKind, string> = {
  laptop: 'IT Equipment', monitor: 'IT Equipment', phone: 'Mobile Device', tablet: 'Mobile Device', scanner: 'Barcode Scanner', card: 'Access Card',
  projector: 'AV Equipment', forklift: 'Material Handling', tool: 'Tools', camera: 'Security',
};
/** Product lines whose name doesn't start with the brand. */
const PRODUCT_BRANDS: Record<string, string> = { iPhone: 'Apple', iPad: 'Apple', Galaxy: 'Samsung' };
/** DEMO MODE: where unassigned stock is kept. */
export const STORE_LOCATION = 'Main Office - IT Store';

/** Brand, model and category shown when an asset is scanned or picked. */
export function assetFacts(a: PoolAsset): { brand: string; model: string; category: string } {
  const [first, ...rest] = a.assetName.split(' ');
  const brand = PRODUCT_BRANDS[first];
  return { brand: brand ?? first, model: brand ? a.assetName : rest.join(' ') || a.assetName, category: CATEGORIES[a.kind] };
}

export interface DepartmentManager {
  name: string;
  id: string;
  title: string;
  /** Where the department sits; one of LOCATIONS. */
  location: string;
}

/** DEMO MODE: department heads, shown on the Assign Asset review step. */
export const DEPARTMENT_MANAGERS: Record<string, DepartmentManager> = {
  'IT Department': { name: 'Kavita Sharma', id: 'EMP-00098', title: 'IT Director', location: 'Head Office - Floor 2' },
  Sales: { name: 'Arvind Rao', id: 'EMP-00112', title: 'Sales Director', location: 'Head Office - Floor 3' },
  Warehouse: { name: 'Vikram Singh', id: 'EMP-00145', title: 'Warehouse Manager', location: 'Warehouse A' },
  Operations: { name: 'Ramesh Kumar', id: 'EMP-00121', title: 'Operations Manager', location: 'Main Plant' },
  Finance: { name: 'Fatima Hassan', id: 'EMP-00133', title: 'Finance Manager', location: 'Head Office - Floor 3' },
  Administration: { name: 'Nisha Patel', id: 'EMP-00140', title: 'Admin Manager', location: 'Head Office - Floor 2' },
  Maintenance: { name: 'Salim Ahmed', id: 'EMP-00151', title: 'Maintenance Lead', location: 'Main Plant' },
  'Field Services': { name: 'Khalid Al Mansoori', id: 'EMP-00160', title: 'Field Services Manager', location: 'Main Plant' },
};

export interface LocationDetails {
  site: string;
  building: string;
  floor: string;
  room: string;
  costCenter: string;
}

/** DEMO MODE: site breakdown of each location in LOCATIONS. */
export const LOCATION_DETAILS: Record<string, LocationDetails> = {
  'Head Office - Floor 2': { site: 'Head Office', building: 'Block A', floor: 'Floor 2', room: 'IT-204', costCenter: 'IT-001 - IT Operations' },
  'Head Office - Floor 3': { site: 'Head Office', building: 'Block A', floor: 'Floor 3', room: 'ADM-310', costCenter: 'AD-002 - Administration' },
  'Meeting Room 1': { site: 'Head Office', building: 'Block B', floor: 'Ground Floor', room: 'MR-01', costCenter: 'AD-002 - Administration' },
  'Main Plant': { site: 'Main Plant', building: 'Production Hall', floor: 'Ground Floor', room: 'PH-01', costCenter: 'OP-010 - Operations' },
  'Main Entrance': { site: 'Main Plant', building: 'Gate House', floor: 'Ground Floor', room: 'GH-01', costCenter: 'SE-020 - Security' },
  'Warehouse A': { site: 'Warehouse', building: 'WH-01', floor: 'Ground Floor', room: 'WH-A', costCenter: 'WH-030 - Warehouse' },
  'Data Center': { site: 'Head Office', building: 'Block C', floor: 'Basement', room: 'DC-01', costCenter: 'IT-001 - IT Operations' },
};

/** DEMO MODE: choices for the site fields on the Assign Asset page. */
export const COST_CENTERS = ['IT-001 - IT Operations', 'AD-002 - Administration', 'OP-010 - Operations', 'SE-020 - Security', 'WH-030 - Warehouse', 'FN-040 - Finance', 'SL-050 - Sales'];
export const SITES = ['Head Office', 'Main Plant', 'Warehouse', 'Dubai Branch', 'Abu Dhabi Branch'];
export const BUILDINGS = ['Block A', 'Block B', 'Block C', 'Main Building', 'Production Hall', 'Gate House', 'WH-01'];
export const FLOORS = ['Basement', 'Ground Floor', 'Floor 1', 'Floor 2', 'Floor 3', 'Floor 4', 'Floor 5'];
export const ROOMS = ['IT-204', 'IT Store', 'ADM-310', 'MR-01', 'PH-01', 'GH-01', 'WH-A', 'DC-01'];

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

const building = (name: string): SiteNode => ({ name, kind: 'Building' });

/** DEMO MODE: the location hierarchy shown when assigning to a location / site. */
export const SITE_TREE: SiteNode[] = [
  {
    name: 'UAE', kind: 'Country', children: [
      {
        name: 'Head Office', kind: 'Site', code: 'HO-001', type: 'Office', address: 'Al Barsha, Dubai, UAE', building: 'Main Building', floors: 5,
        children: ['Block A', 'Block B', 'Parking', 'Warehouse', 'IT Store'].map(building),
      },
      { name: 'Dubai', kind: 'City', children: [{ name: 'Dubai Branch', kind: 'Site', code: 'DXB-002', type: 'Branch Office', address: 'Business Bay, Dubai, UAE', building: 'Bay Tower', floors: 3 }] },
      { name: 'Abu Dhabi', kind: 'City', children: [{ name: 'Abu Dhabi Branch', kind: 'Site', code: 'AUH-003', type: 'Branch Office', address: 'Al Reem Island, Abu Dhabi, UAE', building: 'Reem Plaza', floors: 4 }] },
    ],
  },
  { name: 'Oman', kind: 'Country', children: [{ name: 'Muscat Office', kind: 'Site', code: 'MCT-010', type: 'Office', address: 'Al Khuwair, Muscat, Oman', building: 'Khuwair House', floors: 2 }] },
  { name: 'Qatar', kind: 'Country', children: [{ name: 'Doha Office', kind: 'Site', code: 'DOH-020', type: 'Office', address: 'West Bay, Doha, Qatar', building: 'Bay Tower', floors: 3 }] },
  { name: 'Kuwait', kind: 'Country', children: [{ name: 'Kuwait City Office', kind: 'Site', code: 'KWI-030', type: 'Office', address: 'Sharq, Kuwait City, Kuwait', building: 'Sharq Plaza', floors: 2 }] },
];

/** DEMO MODE: the Assign Asset draft saved with "Save as Draft", restored when the page is opened again. */
export const ASSIGN_DRAFT: { value?: unknown } = {};
