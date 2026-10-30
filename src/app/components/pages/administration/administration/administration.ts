import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, RouterOutlet, NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs/operators';
import { Breadcrumb } from 'shared-ui';

interface BreadcrumbItem {
  label: string;
  path: string | null;
}

interface BreadcrumbOption {
  label: string;
  path: string;
}

@Component({
  standalone: true,
  selector: 'app-administration',
  imports: [CommonModule, RouterModule, RouterOutlet, Breadcrumb],
  templateUrl: './administration.html',
  styleUrls: ['./administration.css']
})
export class Administration {
  showLanding = true;
  breadcrumbItems: BreadcrumbItem[] = [];
  dropdownOptions = new Map<string, BreadcrumbOption[]>();

  adminOptions: BreadcrumbOption[] = [
    { label: 'Configuration', path: '/administration/configuration' },
    { label: 'License', path: '/administration/license' },
    { label: 'User Management', path: '/administration/user-management' }
  ];

  userMgmtOptions: BreadcrumbOption[] = [
    { label: 'User', path: '/administration/user-management/user' },
    { label: 'Role', path: '/administration/user-management/role' }
  ];

  configOptions: BreadcrumbOption[] = [
    { label: 'Projects', path: '/administration/configuration/projects' },
    { label: 'Devices', path: '/administration/configuration/devices' },
    { label: 'Assets', path: '/administration/configuration/assets' },
    { label: 'Maintenance', path: '/administration/configuration/maintenance' },
    { label: 'Workflows', path: '/administration/configuration/workflows' },
    { label: 'WIP', path: '/administration/configuration/wip' },
    { label: 'Master Management', path: '/administration/configuration/master-management' },

  ];

  assetsOptions: BreadcrumbOption[] = [
    { label: 'Asset registration', path: '/administration/configuration/assets/asset-registry' },
    { label: 'Location History', path: '/administration/configuration/assets/location-history' },
    { label: 'Assignment / Ownership', path: '/administration/configuration/assets/assignment-ownership' },
    { label: 'Asset Lifecycle', path: '/administration/configuration/assets/asset-lifecycle' },
    { label: 'Tracking & Telemetry', path: '/administration/configuration/assets/tracking-telemetry' },
    { label: 'Maintenance & Service', path: '/administration/configuration/assets/maintenance-service' },
    { label: 'Utilization & Performance', path: '/administration/configuration/assets/utilization-performance' },
    { label: 'Financial', path: '/administration/configuration/assets/financial' },
    { label: 'Document & Attachment', path: '/administration/configuration/assets/document-attachment' },
    { label: 'Warranty & Contract', path: '/administration/configuration/assets/warranty-contract' },
    { label: 'Alert & Incident', path: '/administration/configuration/assets/alert-incident' },
    { label: 'Audit & Verification', path: '/administration/configuration/assets/audit-verification' },
    { label: 'Activity / Audit Trail', path: '/administration/configuration/assets/activity-audit-trail' },
    { label: 'Custom / Domain-specific Asset Type Fields', path: '/administration/configuration/assets/custom-domain-fields' },
    { label: 'Integration', path: '/administration/configuration/assets/integration' },
    { label: 'Compliance & Certification', path: '/administration/configuration/assets/compliance-certification' },
    { label: 'Asset Movement', path: '/administration/configuration/assets/asset-movement' },
    { label: 'Asset Disposal', path: '/administration/configuration/assets/asset-disposal' },
    { label: 'Tagged Assets', path: '/administration/configuration/assets/tagged-assets' },
    { label: 'Asset Check-Out', path: '/administration/configuration/assets/asset-checkout' },
    { label: 'Asset Check-In', path: '/administration/configuration/assets/asset-checkin' }
  ];

  maintenanceOptions: BreadcrumbOption[] = [
    { label: 'Work Order', path: '/administration/configuration/maintenance/work-order' },
    { label: 'Maintenance Task', path: '/administration/configuration/maintenance/maintenance-task' },
    { label: 'Preventive Maintenance', path: '/administration/configuration/maintenance/preventive-maintenance' },
    { label: 'Predictive Maintenance', path: '/administration/configuration/maintenance/predictive-maintenance' },
    { label: 'Breakdown / Issue Reporting', path: '/administration/configuration/maintenance/breakdown-issue-reporting' },
    { label: 'Spare Parts', path: '/administration/configuration/maintenance/spare-parts' },
    { label: 'Technician', path: '/administration/configuration/maintenance/technician' },
    { label: 'Vendor / AMC', path: '/administration/configuration/maintenance/vendor-amc' },
    { label: 'Cost Tracking', path: '/administration/configuration/maintenance/cost-tracking' },
    { label: 'Downtime Tracking', path: '/administration/configuration/maintenance/downtime-tracking' },
    { label: 'Performance', path: '/administration/configuration/maintenance/performance' },
    { label: 'Compliance & Inspection', path: '/administration/configuration/maintenance/compliance-inspection' }
  ];

  wipOptions: BreadcrumbOption[] = [
    { label: 'Job Master', path: '/administration/configuration/wip/job-master' },
    { label: 'Status Master', path: '/administration/configuration/wip/status-master' },
    { label: 'Resource Master', path: '/administration/configuration/wip/resource-master' },
    { label: 'Task Master', path: '/administration/configuration/wip/task-master' },
    { label: 'Checklist Master', path: '/administration/configuration/wip/checklist-master' },
    { label: 'Checklist Items', path: '/administration/configuration/wip/checklist-items' },
    { label: 'Location Master', path: '/administration/configuration/wip/location-master' },
    { label: 'Asset Linking', path: '/administration/configuration/wip/asset-linking' },
    { label: 'SLA Master', path: '/administration/configuration/wip/sla-master' },
    { label: 'Issue / Delay', path: '/administration/configuration/wip/issue-delay' },
    { label: 'Material Consumption', path: '/administration/configuration/wip/material-consumption' },
    { label: 'Permit / Compliance', path: '/administration/configuration/wip/permit-compliance' },
    { label: 'Progress Log', path: '/administration/configuration/wip/progress-log' },
    { label: 'Alerts', path: '/administration/configuration/wip/alerts' },
    { label: 'KPI Config', path: '/administration/configuration/wip/kpi-config' },
    { label: 'Role & Access', path: '/administration/configuration/wip/role-access' }
  ];

  workflowsOptions: BreadcrumbOption[] = [
    { label: 'Workflow List', path: '/administration/configuration/workflows/list' },
    { label: 'Workflow Builder', path: '/administration/configuration/workflows/builder' },
    { label: 'Workflow Instances', path: '/administration/configuration/workflows/instances' },
    { label: 'My Tasks / Approvals', path: '/administration/configuration/workflows/tasks' },
    { label: 'Insights', path: '/administration/configuration/workflows/insights' }
  ];

  masterManagementOptions: BreadcrumbOption[] = [
    { label: 'Organization', path: '/administration/configuration/master-management/organization' },
    { label: 'Business Unit', path: '/administration/configuration/master-management/business-unit' },
    { label: 'Department', path: '/administration/configuration/master-management/department' },
    { label: 'Site', path: '/administration/configuration/master-management/site' },
    { label: 'Manufacturer', path: '/administration/configuration/master-management/manufacturer' },
    { label: 'Supplier', path: '/administration/configuration/master-management/supplier' },
    { label: 'Master Maintenance', path: '/administration/configuration/master-management/master-maintenance' },
    { label: 'Category / Sub-category', path: '/administration/configuration/master-management/category-subcategory' },
    { label: 'Asset Type', path: '/administration/configuration/master-management/asset-type' },
    { label: 'Assigned Custodian / Department', path: '/administration/configuration/master-management/assigned-custodian-department' },
    { label: 'Current Location', path: '/administration/configuration/master-management/current-location' },
    { label: 'Status Changes', path: '/administration/configuration/master-management/status-changes' },
    { label: 'Tag IDs', path: '/administration/configuration/master-management/tag-ids' },
    { label: 'Depreciation Method', path: '/administration/configuration/master-management/depreciation-method' },
    { label: 'Cost Center', path: '/administration/configuration/master-management/cost-center' },
    { label: 'Alert Type', path: '/administration/configuration/master-management/alert-type' },
    { label: 'Resolution Status', path: '/administration/configuration/master-management/resolution-status' },
    { label: 'Auditor Details', path: '/administration/configuration/master-management/auditor-details' },
    { label: 'Physical Verification Result', path: '/administration/configuration/master-management/physical-verification-result' },
    { label: 'Asset Type Fields', path: '/administration/configuration/master-management/asset-type-fields' },
    { label: 'API Sync Status Master', path: '/administration/configuration/master-management/api-sync-status-master' },
    { label: 'Certification Type Master', path: '/administration/configuration/master-management/certification-type-master' },
    { label: 'Work Type', path: '/administration/configuration/master-management/work-type' },
    { label: 'Priority', path: '/administration/configuration/master-management/priority' },
    { label: 'Status', path: '/administration/configuration/master-management/status' },
    { label: 'Resource Type', path: '/administration/configuration/master-management/resource-type' },
    { label: 'Skill Master', path: '/administration/configuration/master-management/skill-master' },
    { label: 'Shift Master', path: '/administration/configuration/master-management/shift-master' },
    { label: 'Checklist Type Master', path: '/administration/configuration/master-management/checklist-type-master' },
    { label: 'Response Type Master', path: '/administration/configuration/master-management/response-type-master' },
    { label: 'Condition Master', path: '/administration/configuration/master-management/condition-master' },
    { label: 'Issue Type Master', path: '/administration/configuration/master-management/issue-type-master' },
    { label: 'Severity Master', path: '/administration/configuration/master-management/severity-master' },
    { label: 'Unit Master', path: '/administration/configuration/master-management/unit-master' },
    { label: 'Permit Type Master', path: '/administration/configuration/master-management/permit-type-master' },
    { label: 'Update Source Master', path: '/administration/configuration/master-management/update-source-master' },
    { label: 'Chart Type Master', path: '/administration/configuration/master-management/chart-type-master' },
    { label: 'Permission Master', path: '/administration/configuration/master-management/permission-master' },
    { label: 'Module Access Master', path: '/administration/configuration/master-management/module-access-master' }
  ];

  inspectionOptions: BreadcrumbOption[] = [
    { label: 'Inspection Type', path: '/administration/configuration/inspection/inspection-type' },
    { label: 'Task Category', path: '/administration/configuration/inspection/task-category' },
    { label: 'Inspection Task', path: '/administration/configuration/inspection/inspection-task' },
    { label: 'Checklist', path: '/administration/configuration/inspection/checklist-template/list' },
    { label: 'Failure Reason', path: '/administration/configuration/inspection/failure-reason' },
    { label: 'Defect', path: '/administration/configuration/inspection/defect' },
    { label: 'Severity', path: '/administration/configuration/inspection/severity' },
    { label: 'Priority', path: '/administration/configuration/inspection/priority' },
    { label: 'Signature and Stamp', path: '/administration/configuration/inspection/signature-stamp' },
    { label: 'Notification Template', path: '/administration/configuration/inspection/notification-template' },
    { label: 'Report Template', path: '/administration/configuration/inspection/report-template' },
    { label: 'Numbering Sequence', path: '/administration/configuration/inspection/numbering-sequence' },
    { label: 'Holiday and Working Calendar', path: '/administration/configuration/inspection/holiday-calendar' }
  ];

  // Maps URL slug -> breadcrumb label, used when building the trail
  private configSlugLabelMap: Record<string, string> = {
    'projects': 'Projects',
    'devices': 'Devices',
    'assets': 'Assets',
    'maintenance': 'Maintenance',
    'workflows': 'Workflows',
    'wip': 'WIP',
    'master-management': 'Master Management',
    'inspection': 'Inspection',
    'people': 'People',
    'attendance': 'Attendance',
    'access-control': 'Access Control',
    'ot-management': 'OT Management',
    'visitor-management': 'Visitor Management',
    'patrol': 'Patrol'
  };

  // Maps URL slug -> breadcrumb label for pages nested under Assets
  private assetsSlugLabelMap: Record<string, string> = {
    'asset-registry': 'Asset registration',
    'location-history': 'Location History',
    'assignment-ownership': 'Assignment / Ownership',
    'asset-lifecycle': 'Asset Lifecycle',
    'tracking-telemetry': 'Tracking & Telemetry',
    'maintenance-service': 'Maintenance & Service',
    'utilization-performance': 'Utilization & Performance',
    'financial': 'Financial',
    'document-attachment': 'Document & Attachment',
    'warranty-contract': 'Warranty & Contract',
    'alert-incident': 'Alert & Incident',
    'audit-verification': 'Audit & Verification',
    'activity-audit-trail': 'Activity / Audit Trail',
    'custom-domain-fields': 'Custom / Domain-specific Asset Type Fields',
    'integration': 'Integration',
    'compliance-certification': 'Compliance & Certification',
    'asset-movement': 'Asset Movement',
    'asset-disposal': 'Asset Disposal',
    'tagged-assets': 'Tagged Assets',
    'asset-checkout': 'Asset Check-Out',
    'asset-checkin': 'Asset Check-In'
  };

  // Maps URL slug -> breadcrumb label for pages nested under Maintenance
  private maintenanceSlugLabelMap: Record<string, string> = {
    'work-order': 'Work Order',
    'maintenance-task': 'Maintenance Task',
    'preventive-maintenance': 'Preventive Maintenance',
    'predictive-maintenance': 'Predictive Maintenance',
    'breakdown-issue-reporting': 'Breakdown / Issue Reporting',
    'spare-parts': 'Spare Parts',
    'technician': 'Technician',
    'vendor-amc': 'Vendor / AMC',
    'cost-tracking': 'Cost Tracking',
    'downtime-tracking': 'Downtime Tracking',
    'performance': 'Performance',
    'compliance-inspection': 'Compliance & Inspection'
  };

  // Maps URL slug -> breadcrumb label for pages nested under Workflows
  private workflowsSlugLabelMap: Record<string, string> = {
    'list': 'Workflow List',
    'builder': 'Workflow Builder',
    'instances': 'Workflow Instances',
    'tasks': 'My Tasks / Approvals',
    'insights': 'Insights'
  };

  // Maps URL slug -> breadcrumb label for pages nested under WIP
  private wipSlugLabelMap: Record<string, string> = {
    'job-master': 'Job Master',
    'status-master': 'Status Master',
    'resource-master': 'Resource Master',
    'task-master': 'Task Master',
    'checklist-master': 'Checklist Master',
    'checklist-items': 'Checklist Items',
    'location-master': 'Location Master',
    'asset-linking': 'Asset Linking',
    'sla-master': 'SLA Master',
    'issue-delay': 'Issue / Delay',
    'material-consumption': 'Material Consumption',
    'permit-compliance': 'Permit / Compliance',
    'progress-log': 'Progress Log',
    'alerts': 'Alerts',
    'kpi-config': 'KPI Config',
    'role-access': 'Role & Access'
  };

  // Maps URL slug -> breadcrumb label for pages nested under Master Management
  private masterManagementSlugLabelMap: Record<string, string> = {
    'organization': 'Organization',
    'business-unit': 'Business Unit',
    'department': 'Department',
    'site': 'Site',
    'manufacturer': 'Manufacturer',
    'supplier': 'Supplier',
    'master-maintenance': 'Master Maintenance',
    'category-subcategory': 'Category / Sub-category',
    'asset-type': 'Asset Type',
    'assigned-custodian-department': 'Assigned Custodian / Department',
    'current-location': 'Current Location',
    'status-changes': 'Status Changes',
    'tag-ids': 'Tag IDs',
    'depreciation-method': 'Depreciation Method',
    'cost-center': 'Cost Center',
    'alert-type': 'Alert Type',
    'resolution-status': 'Resolution Status',
    'auditor-details': 'Auditor Details',
    'physical-verification-result': 'Physical Verification Result',
    'asset-type-fields': 'Asset Type Fields',
    'api-sync-status-master': 'API Sync Status Master',
    'certification-type-master': 'Certification Type Master',
    'work-type': 'Work Type',
    'priority': 'Priority',
    'status': 'Status',
    'resource-type': 'Resource Type',
    'skill-master': 'Skill Master',
    'shift-master': 'Shift Master',
    'checklist-type-master': 'Checklist Type Master',
    'response-type-master': 'Response Type Master',
    'condition-master': 'Condition Master',
    'issue-type-master': 'Issue Type Master',
    'severity-master': 'Severity Master',
    'unit-master': 'Unit Master',
    'permit-type-master': 'Permit Type Master',
    'update-source-master': 'Update Source Master',
    'chart-type-master': 'Chart Type Master',
    'permission-master': 'Permission Master',
    'module-access-master': 'Module Access Master'
  };

  // Maps URL slug -> breadcrumb label for pages nested under Inspection
  private inspectionSlugLabelMap: Record<string, string> = {
    'inspection-type': 'Inspection Type',
    'task-category': 'Task Category',
    'inspection-task': 'Inspection Task',
    'checklist-template': 'Checklist ',
    'failure-reason': 'Failure Reason',
    'defect': 'Defect',
    'severity': 'Severity',
    'priority': 'Priority',
    'signature-stamp': 'Signature and Stamp',
    'notification-template': 'Notification Template',
    'report-template': 'Report Template',
    'numbering-sequence': 'Numbering Sequence',
    'holiday-calendar': 'Holiday and Working Calendar'
  };

  constructor(private router: Router) {
    this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe(() => {
        this.showLanding = this.router.url === '/administration' || this.router.url === '/administration/';
        this.updateBreadcrumb();
      });
    this.showLanding = this.router.url === '/administration' || this.router.url === '/administration/';
    this.updateBreadcrumb();
  }

  private updateBreadcrumb(): void {
    const url = this.router.url;
    const segments = url.split('/').filter(s => s);

    this.breadcrumbItems = [
      { label: 'Administration', path: '/administration' }
    ];

    this.dropdownOptions = new Map<string, BreadcrumbOption[]>();
    this.dropdownOptions.set('Administration', this.adminOptions);
    this.dropdownOptions.set('User Management', this.userMgmtOptions);
    this.dropdownOptions.set('Configuration', this.configOptions);
    this.dropdownOptions.set('Assets', this.assetsOptions);
    this.dropdownOptions.set('Maintenance', this.maintenanceOptions);
    this.dropdownOptions.set('Workflows', this.workflowsOptions);
    this.dropdownOptions.set('WIP', this.wipOptions);
    this.dropdownOptions.set('Master Management', this.masterManagementOptions);
    this.dropdownOptions.set('Inspection', this.inspectionOptions);

    const userMgmtIdx = segments.indexOf('user-management');
    const configIdx = segments.indexOf('configuration');

    // Configuration submenu handling
    if (configIdx !== -1) {
      this.breadcrumbItems.push({ label: 'Configuration', path: '/administration/configuration' });

      const childSlug = segments[configIdx + 1];
      if (childSlug) {
        const childLabel = this.configSlugLabelMap[childSlug];
        if (childLabel) {
          const grandchildSlugMap: Record<string, Record<string, string>> = {
            'assets': this.assetsSlugLabelMap,
            'maintenance': this.maintenanceSlugLabelMap,
            'workflows': this.workflowsSlugLabelMap,
            'wip': this.wipSlugLabelMap,
            'master-management': this.masterManagementSlugLabelMap,
            'inspection': this.inspectionSlugLabelMap
          };
          const grandchildSlug = grandchildSlugMap[childSlug] ? segments[configIdx + 2] : undefined;
          const grandchildLabel = grandchildSlug ? grandchildSlugMap[childSlug][grandchildSlug] : undefined;

          this.breadcrumbItems.push({
            label: childLabel,
            path: grandchildLabel ? `/administration/configuration/${childSlug}` : null
          });

          if (grandchildLabel) {
            this.breadcrumbItems.push({ label: grandchildLabel, path: null });
          }

          // Checklist Template Master has its own List/Builder sub-routes (like Workflows), one level deeper
          // than the generic child/grandchild dispatch above handles.
          if (childSlug === 'inspection' && grandchildSlug === 'checklist-template') {
            const stepSlug = segments[configIdx + 3];
            if (stepSlug === 'builder') {
              const lastIndex = this.breadcrumbItems.length - 1;
              this.breadcrumbItems[lastIndex] = {
                ...this.breadcrumbItems[lastIndex],
                path: '/administration/configuration/inspection/checklist-template/list'
              };
              const hasId = !!segments[configIdx + 4];
              this.breadcrumbItems.push({ label: hasId ? 'Edit Template' : 'Create Template', path: null });
            }
          }
        }
      }
    }

    if (userMgmtIdx !== -1) {
      this.breadcrumbItems.push({ label: 'User Management', path: '/administration/user-management' });

      if (segments[userMgmtIdx + 1]) {
        const childPage = segments[userMgmtIdx + 1];
        if (childPage === 'user') {
          this.breadcrumbItems.push({ label: 'User', path: null });
        } else if (childPage === 'role') {
          if (segments[userMgmtIdx + 2]) {
            this.breadcrumbItems.push({ label: 'Role', path: '/administration/user-management/role' });
            if (segments[userMgmtIdx + 2] === 'create') {
              this.breadcrumbItems.push({ label: 'Create', path: null });
            }
          } else {
            this.breadcrumbItems.push({ label: 'Role', path: null });
          }
        }
      }
    }
  }
}