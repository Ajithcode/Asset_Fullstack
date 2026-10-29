import { ADMIN_DEPARTMENTS, ADMIN_LOCATIONS, CURRENT_USER } from '../shared/admin-common';
import { AdminUser, UserRole, UserStatus } from './user-management.model';

// DEMO MODE: 248 users generated deterministically (231 active, 9 pending invitations, 8 suspended)

/** The modules shown as access tiles in the user drawer. */
export const USER_MODULES = ['Dashboard', 'Assets', 'Maintenance', 'Inventory', 'Compliance', 'Reports'];

export const USER_ROLES: UserRole[] = [
  { name: 'System Administrator', short: 'System Admin', tone: 'pink', modules: USER_MODULES },
  { name: 'Operations Manager', short: 'Operations Manager', tone: 'purple', modules: ['Dashboard', 'Assets', 'Maintenance', 'Inventory', 'Reports'] },
  { name: 'Asset Manager', short: 'Asset Manager', tone: 'purple', modules: ['Dashboard', 'Assets', 'Maintenance', 'Reports'] },
  { name: 'Maintenance Lead', short: 'Maintenance Lead', tone: 'blue', modules: ['Dashboard', 'Assets', 'Maintenance', 'Reports'] },
  { name: 'Technician', short: 'Technician', tone: 'green', modules: ['Dashboard', 'Maintenance'] },
  { name: 'Finance Analyst', short: 'Finance Analyst', tone: 'blue', modules: ['Dashboard', 'Assets', 'Reports'] },
  { name: 'Compliance Officer', short: 'Compliance Officer', tone: 'orange', modules: ['Dashboard', 'Assets', 'Compliance', 'Reports'] },
  { name: 'Inventory Controller', short: 'Inventory Controller', tone: 'green', modules: ['Dashboard', 'Inventory', 'Reports'] },
  { name: 'IT Support', short: 'IT Support', tone: 'blue', modules: ['Dashboard', 'Assets'] },
  { name: 'HR Officer', short: 'HR Officer', tone: 'grey', modules: ['Dashboard', 'Reports'] },
  { name: 'Procurement Officer', short: 'Procurement Officer', tone: 'orange', modules: ['Dashboard', 'Inventory', 'Reports'] },
  { name: 'Report Viewer', short: 'Report Viewer', tone: 'grey', modules: ['Dashboard', 'Reports'], secondaryOnly: true },
  { name: 'Compliance User', short: 'Compliance User', tone: 'orange', modules: ['Dashboard', 'Compliance'], secondaryOnly: true }
];

export const ALL_LOCATIONS = 'All Locations';

export const LOCATION_TIME_ZONES: Record<string, string> = {
  'Dubai HQ': '(GMT+4) Dubai, UAE',
  'Abu Dhabi Office': '(GMT+4) Dubai, UAE',
  'Sharjah Facility': '(GMT+4) Dubai, UAE',
  'Riyadh Office': '(GMT+3) Riyadh, KSA',
  'London Office': '(GMT+0) London, UK',
  'Singapore Office': '(GMT+8) Singapore'
};

export const TIME_ZONES = [...new Set(Object.values(LOCATION_TIME_ZONES))];

/** Counts at the end of last month, for the "vs last month" trends on the stat cards. */
export const LAST_MONTH = { total: 221, active: 214, pending: 12, suspended: 9 };

export function blankUser(): AdminUser {
  return {
    id: '',
    name: '',
    email: '',
    employeeId: '',
    department: '',
    jobTitle: '',
    phone: '',
    location: ADMIN_LOCATIONS[0],
    timeZone: LOCATION_TIME_ZONES[ADMIN_LOCATIONS[0]],
    photo: null,
    primaryRole: '',
    secondaryRoles: [],
    accessScope: ALL_LOCATIONS,
    mfa: true,
    status: 'Pending',
    suspendReason: '',
    lastLogin: null,
    created: '',
    invitedAt: null,
    modules: []
  };
}

export function roleModules(roles: string[]): string[] {
  const set = new Set(roles.flatMap((r) => USER_ROLES.find((x) => x.name === r)?.modules ?? []));
  return USER_MODULES.filter((m) => set.has(m));
}

/** Local date-time `days` before today at hh:mm, as an ISO string. */
function at(days: number, hh: number, mm: number): string {
  const d = new Date();
  d.setDate(d.getDate() - days);
  d.setHours(hh, mm, 0, 0);
  return d.toISOString();
}

function fixed(y: number, mo: number, d: number, hh: number, mm: number): string {
  return new Date(y, mo - 1, d, hh, mm).toISOString();
}

type Seed = Omit<AdminUser, 'id' | 'photo' | 'suspendReason' | 'invitedAt' | 'modules' | 'secondaryRoles' | 'accessScope' | 'mfa' | 'timeZone'> &
  Partial<Pick<AdminUser, 'secondaryRoles' | 'accessScope' | 'mfa' | 'suspendReason' | 'invitedAt'>>;

// The accounts shown in the design; the signed-in administrator is one of them
const NAMED: Seed[] = [
  { name: 'Aisha Rahman', email: 'aisha.rahman@purpleiq.com', employeeId: 'EMP-1024', department: 'Operations', jobTitle: 'Operations Manager', phone: '+971 50 214 3381', location: 'Dubai HQ', primaryRole: 'Operations Manager', status: 'Active', lastLogin: at(0, 9, 42), created: fixed(2024, 3, 4, 10, 5) },
  { name: 'Omar Hassan', email: 'omar.hassan@purpleiq.com', employeeId: 'EMP-1088', department: 'Maintenance', jobTitle: 'Maintenance Lead', phone: '+971 52 778 1904', location: 'Abu Dhabi Office', primaryRole: 'Maintenance Lead', status: 'Active', lastLogin: at(1, 14, 18), created: fixed(2024, 6, 17, 9, 40) },
  { name: 'Fatima Noor', email: 'fatima.noor@purpleiq.com', employeeId: 'EMP-1102', department: 'Finance', jobTitle: 'Finance Analyst', phone: '+971 55 610 2287', location: 'Dubai HQ', primaryRole: 'Finance Analyst', status: 'Pending', lastLogin: at(4, 10, 21), created: fixed(2026, 9, 30, 11, 15), invitedAt: at(4, 10, 21) },
  { name: 'John Mathew', email: 'john.mathew@purpleiq.com', employeeId: 'EMP-0987', department: 'IT', jobTitle: 'IT Administrator', phone: '+971 50 993 4410', location: 'Dubai HQ', primaryRole: 'System Administrator', status: 'Suspended', lastLogin: at(10, 16, 5), created: fixed(2023, 11, 8, 14, 30), suspendReason: 'Under access review after a role change.' },
  { name: CURRENT_USER.name, email: CURRENT_USER.email, employeeId: 'EMP-0001', department: 'Administration', jobTitle: 'System Administrator', phone: '+971 50 123 4567', location: 'Dubai HQ', primaryRole: 'System Administrator', secondaryRoles: ['Report Viewer', 'Compliance User'], status: 'Active', lastLogin: at(0, 9, 42), created: fixed(2024, 1, 12, 11, 20) }
];

const FIRST = ['Ahmed', 'Priya', 'Daniel', 'Layla', 'Rahul', 'Mariam', 'James', 'Noura', 'Michael', 'Sara', 'Khalid', 'Lisa', 'Yousef', 'Hana', 'Arjun', 'Reem', 'David', 'Amira', 'Vikram', 'Huda', 'Thomas', 'Zainab', 'Ravi', 'Nadia', 'Samuel', 'Leena', 'Faisal', 'Grace', 'Imran', 'Maya'];
const LAST = ['Khan', 'Sharma', 'Kim', 'Al Mansoori', 'Mehta', 'Al Hashimi', 'Tan', 'Wong', 'Chen', 'Nair', 'Al Marri', 'Wilson', 'Haddad', 'Pillai', 'Saleh', 'Fernandes', 'Al Suwaidi', 'Iyer', 'Qureshi', 'Mathew', 'Abdullah', 'George', 'Rao'];

const DEPT_ROLES: Record<string, string[]> = {
  Administration: ['Asset Manager'],
  Operations: ['Operations Manager', 'Asset Manager', 'Asset Manager'],
  Maintenance: ['Maintenance Lead', 'Technician', 'Technician', 'Technician'],
  Finance: ['Finance Analyst'],
  IT: ['IT Support'],
  Compliance: ['Compliance Officer'],
  Inventory: ['Inventory Controller'],
  HR: ['HR Officer'],
  Procurement: ['Procurement Officer']
};

const JOB_TITLES: Record<string, string> = {
  'Asset Manager': 'Asset Coordinator',
  'Operations Manager': 'Operations Manager',
  'Maintenance Lead': 'Maintenance Supervisor',
  Technician: 'Maintenance Technician',
  'Finance Analyst': 'Finance Analyst',
  'IT Support': 'IT Support Engineer',
  'Compliance Officer': 'Compliance Officer',
  'Inventory Controller': 'Storekeeper',
  'HR Officer': 'HR Officer',
  'Procurement Officer': 'Procurement Officer'
};

const GENERATED = 243;
// Generated rows that are pending invitations (8) or suspended (7); with the named users: 9 pending, 8 suspended
const PENDING_AT = new Set([17, 46, 79, 112, 141, 170, 203, 236]);
const SUSPENDED_AT = new Set([28, 63, 98, 131, 158, 191, 224]);

function slug(name: string): string {
  return name.toLowerCase().replace(/[^a-z\s]/g, '').trim().replace(/\s+/g, '.');
}

export function seedUsers(): AdminUser[] {
  const seeds: Seed[] = [...NAMED];
  const emails = new Set(seeds.map((s) => s.email));
  for (let i = 0; i < GENERATED; i++) {
    const name = `${FIRST[(i * 7) % FIRST.length]} ${LAST[(i * 11 + Math.floor(i / FIRST.length)) % LAST.length]}`;
    let email = `${slug(name)}@purpleiq.com`;
    for (let n = 2; emails.has(email); n++) email = `${slug(name)}${n}@purpleiq.com`;
    emails.add(email);
    const department = ADMIN_DEPARTMENTS[(i * 5 + 1) % ADMIN_DEPARTMENTS.length];
    const roles = DEPT_ROLES[department];
    const primaryRole = roles[i % roles.length];
    const status: UserStatus = PENDING_AT.has(i) ? 'Pending' : SUSPENDED_AT.has(i) ? 'Suspended' : 'Active';
    const location = ADMIN_LOCATIONS[(i * 3) % 10 < 5 ? 0 : (i * 7) % ADMIN_LOCATIONS.length];
    const created = at(30 + ((i * 97) % 900), 8 + (i % 9), (i * 13) % 60);
    seeds.push({
      name,
      email,
      employeeId: `EMP-${1103 + i}`,
      department,
      jobTitle: JOB_TITLES[primaryRole],
      phone: `+971 5${i % 6} ${String(100 + ((i * 389) % 900))} ${String(1000 + ((i * 7919) % 9000))}`,
      location,
      primaryRole,
      secondaryRoles: i % 6 === 0 ? ['Report Viewer'] : [],
      accessScope: i % 4 === 0 ? ALL_LOCATIONS : location,
      mfa: i % 5 !== 2,
      status,
      suspendReason: status === 'Suspended' ? 'Suspended pending HR review.' : '',
      // Pending users have never signed in; suspended ones last did a while ago
      lastLogin: status === 'Pending' ? null : at(status === 'Suspended' ? 20 + (i % 40) : (i * 37) % 21, 7 + ((i * 5) % 11), (i * 23) % 60),
      created: status === 'Pending' ? at((i % 9) + 1, 9, 30) : created,
      invitedAt: status === 'Pending' ? at((i % 9) + 1, 9, 30) : null
    });
  }
  return seeds.map((s, i) => ({
    secondaryRoles: [],
    accessScope: ALL_LOCATIONS,
    mfa: true,
    suspendReason: '',
    invitedAt: null,
    ...s,
    id: s.email === CURRENT_USER.email ? CURRENT_USER.id : `USR-${String(i + 1001).padStart(6, '0')}`,
    timeZone: LOCATION_TIME_ZONES[s.location],
    photo: null,
    modules: roleModules([s.primaryRole, ...(s.secondaryRoles ?? [])])
  }));
}
