export type SecurityStatus = 'Active' | 'Inactive';

export interface ModuleRecord {
  code: string;
  name: string;
  category: string;
  description: string;
  icon: string;
  status: SecurityStatus;
  createdOn: string;
  updatedOn: string;
}

export type PermissionType = 'Read' | 'Create' | 'Update' | 'Delete' | 'Approve' | 'Manage' | 'Execute';

export interface PermissionRecord {
  code: string;
  name: string;
  // Module code
  module: string;
  type: PermissionType;
  description: string;
  status: SecurityStatus;
  createdOn: string;
  updatedOn: string;
}

export type RoleType = 'System' | 'Business' | 'Operational' | 'Read Only';

export interface RoleRecord {
  code: string;
  name: string;
  type: RoleType;
  description: string;
  status: SecurityStatus;
  // Permission and module codes the role grants
  permissions: string[];
  modules: string[];
  createdOn: string;
  updatedOn: string;
}

export interface UserRecord {
  // USR-001
  code: string;
  name: string;
  username: string;
  email: string;
  department: string;
  // Role codes; the primary role is listed first in the table
  primaryRole: string;
  additionalRoles: string[];
  phone: string;
  photo: string | null;
  status: SecurityStatus;
  // Roles & access
  employeeId: string;
  sites: string[];
  accountExpiry: string;
  mfaRequired: boolean;
  mustChangePassword: boolean;
  sendInvite: boolean;
  // Preferences
  language: string;
  timeZone: string;
  dateFormat: string;
  emailNotifications: boolean;
  smsNotifications: boolean;
  lastLogin: string;
  createdOn: string;
  updatedOn: string;
}
