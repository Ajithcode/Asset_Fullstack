import { Routes } from '@angular/router';
import { Administration } from './administration/administration';

// Mounted by the shell at /administration; paths match ADMINISTRATION_MENU in shared-ui, which the shell's
// sidebar is built from
export const routes: Routes = [
  { path: '', component: Administration },
  {
    path: 'organization',
    loadComponent: () => import('./organization/organization').then((m) => m.OrganizationPage)
  },
  {
    path: 'user-management',
    loadComponent: () => import('./user-management/user-management').then((m) => m.UserManagementPage)
  },
  {
    path: 'role-access-control',
    loadComponent: () => import('./role-access-control/role-access-control').then((m) => m.RoleAccessControlPage)
  },
  {
    path: 'module-access',
    loadComponent: () => import('./module-access/module-access').then((m) => m.ModuleAccessPage)
  },
  {
    path: 'workflow-builder',
    loadComponent: () => import('./workflow-builder/workflow-builder').then((m) => m.WorkflowBuilderPage)
  },
  {
    path: 'notification-templates',
    loadComponent: () => import('./notification-templates/notification-templates').then((m) => m.NotificationTemplatesPage)
  },
  {
    path: 'numbering-sequence',
    loadComponent: () => import('./numbering-sequence/numbering-sequence').then((m) => m.NumberingSequencePage)
  },
  {
    path: 'checklist-templates',
    loadComponent: () => import('./checklist-templates/checklist-templates').then((m) => m.ChecklistTemplatesPage)
  },
  {
    path: 'system-configuration',
    loadComponent: () => import('./system-configuration/system-configuration').then((m) => m.SystemConfigurationPage)
  },
  {
    path: 'integration-api',
    loadComponent: () => import('./integration-api/integration-api').then((m) => m.IntegrationApiPage)
  },
  {
    path: 'data-import-export',
    loadComponent: () => import('./data-import-export/data-import-export').then((m) => m.DataImportExportPage)
  },
  {
    path: 'system-audit-log',
    loadComponent: () => import('./system-audit-log/system-audit-log').then((m) => m.SystemAuditLogPage)
  }
];
