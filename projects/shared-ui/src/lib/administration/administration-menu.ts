export interface AdminModule {
  label: string;
  slug: string;
  description: string;
}

export const ADMINISTRATION_BASE_PATH = '/administration';

// Sub modules of Administration, in sidebar order. Each opens /administration/<slug>.
export const ADMINISTRATION_MENU: AdminModule[] = [
  { label: 'Organization', slug: 'organization', description: 'Company profile, branding and regional settings.' },
  { label: 'User Management', slug: 'user-management', description: 'Users, sign-in and account settings.' },
  { label: 'Role & Access Control', slug: 'role-access-control', description: 'Roles and the permissions each role grants.' },
  { label: 'Module Access', slug: 'module-access', description: 'Turn modules on or off for roles and sites.' },
  { label: 'Workflow Builder', slug: 'workflow-builder', description: 'Design approval and process workflows.' },
  { label: 'Notification Templates', slug: 'notification-templates', description: 'Email, SMS and in-app message templates.' },
  { label: 'Numbering Sequence', slug: 'numbering-sequence', description: 'Prefixes and running numbers for records.' },
  { label: 'Checklist Templates', slug: 'checklist-templates', description: 'Reusable checklists for inspections and tasks.' },
  { label: 'System Configuration', slug: 'system-configuration', description: 'General system settings and defaults.' },
  { label: 'Integration & API', slug: 'integration-api', description: 'Connected systems, API keys and webhooks.' },
  { label: 'Data Import / Export', slug: 'data-import-export', description: 'Bulk import and export of system data.' },
  { label: 'System Audit Log', slug: 'system-audit-log', description: 'Who changed what, and when.' }
];
