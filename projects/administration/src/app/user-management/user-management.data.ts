import { ADMIN_LOCATIONS } from '../shared/admin-common';
import { AdminUser, UserRole } from './user-management.model';

// Lookup lists for User Management; the users themselves come from the Administration API

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

// DEMO MODE: the API has no history of counts, so last month's figures are fixed
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
