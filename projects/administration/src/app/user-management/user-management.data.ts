import { ADMIN_LOCATIONS } from '../shared/admin-common';
import { AdminUser, UserRole } from './user-management.model';

// Lookup lists for User Management; the users themselves come from the Administration API

/** The modules shown as access tiles in the user drawer. */
export const USER_MODULES = ['Dashboard', 'Assets', 'Maintenance', 'Inventory', 'Compliance', 'Reports'];

/** Roles offered in the user drawer / invite dialog; loaded from the roles API (empty until wired). */
export const USER_ROLES: UserRole[] = [];

export const ALL_LOCATIONS = 'All Locations';

/** Site -> time zone lookup; comes from the locations API (empty until wired). */
export const LOCATION_TIME_ZONES: Record<string, string> = {};

export const TIME_ZONES = ['(GMT+0) London, UK', '(GMT+3) Riyadh, KSA', '(GMT+4) Dubai, UAE', '(GMT+8) Singapore'];

/** Counts at the end of last month, for the "vs last month" trends on the stat cards (no history API yet). */
export const LAST_MONTH = { total: 0, active: 0, pending: 0, suspended: 0 };

export function blankUser(): AdminUser {
  return {
    id: '',
    name: '',
    email: '',
    employeeId: '',
    department: '',
    jobTitle: '',
    phone: '',
    location: ADMIN_LOCATIONS[0] ?? '',
    timeZone: LOCATION_TIME_ZONES[ADMIN_LOCATIONS[0] ?? ''] ?? '',
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
