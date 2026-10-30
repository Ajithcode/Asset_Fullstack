import { Routes } from '@angular/router';
import { loadRemoteModule } from '@angular-architects/native-federation';
import { Login } from './components/pages/login/login/login';
import { Signup } from './components/pages/signup/signup/signup';
import { SectionUnavailable } from './section-unavailable/section-unavailable';
import { ModulePlaceholder } from './module-placeholder/module-placeholder';
import { AnalyticsInsights } from './components/pages/tracking-iot/analytics-insights/analytics-insights';
import { TagIds } from './components/pages/tracking-iot/tag-ids/tag-ids';
import { FirmwareConnectivity } from './components/pages/tracking-iot/firmware-connectivity/firmware-connectivity';
import { AssetRegistration } from './components/pages/assets/asset-registration/asset-registration';
import { AssetList } from './components/pages/assets/asset-list/asset-list';
import { AssetCategories } from './components/pages/assets/asset-categories/asset-categories';
import { AssetTypesModels } from './components/pages/assets/asset-types-models/asset-types-models';
import { AssetHierarchy } from './components/pages/assets/asset-hierarchy/asset-hierarchy';
import { AssetComponents } from './components/pages/assets/asset-components/asset-components';
import { AssetCustomFields } from './components/pages/assets/asset-custom-fields/asset-custom-fields';
import { AssetRelationship } from './components/pages/assets/asset-relationship/asset-relationship';
import { AssetRelationshipList } from './components/pages/assets/asset-relationship-list/asset-relationship-list';
import { AssetDocuments } from './components/pages/assets/asset-documents/asset-documents';
import { AssetImages } from './components/pages/assets/asset-images/asset-images';
import { AssetStatusCondition } from './components/pages/assets/asset-status-condition/asset-status-condition';
import { AssetCriticality } from './components/pages/assets/asset-criticality/asset-criticality';
import { AssetHealthScore } from './components/pages/assets/asset-health-score/asset-health-score';
import { AssetBulk } from './components/pages/assets/asset-bulk/asset-bulk';
import { BulkAssignment } from './components/pages/assets/asset-bulk/bulk-assignment/bulk-assignment';
import { AssetMerge } from './components/pages/assets/asset-merge/asset-merge';
import { LocationHistory } from './components/pages/assets/asset-location-history/location-history';
import { AssetAssignment } from './components/pages/assets/asset-assignment/asset-assignment';
import { AssignAsset } from './components/pages/assets/asset-assignment/assign-asset/assign-asset';
import { AssetMaintenance } from './components/pages/assets/asset-maintenance/asset-maintenance';
import { JobTemplates } from './components/pages/wip-operations/job-templates/job-templates';
import { JobOrders } from './components/pages/wip-operations/job-orders/job-orders';
import { Resources } from './components/pages/wip-operations/resources/resources';
import { TaskMaster } from './components/pages/wip-operations/task-master/task-master';
import { MaterialConsumption } from './components/pages/wip-operations/material-consumption/material-consumption';
import { LocationManagement } from './components/pages/wip-operations/location-management/location-management';
import { IssuesDelays } from './components/pages/wip-operations/issues-delays/issues-delays';
import { SlaManagement } from './components/pages/wip-operations/sla-management/sla-management';
import { AssetLinking } from './components/pages/wip-operations/asset-linking/asset-linking';
import { PermitCompliance } from './components/pages/wip-operations/permit-compliance/permit-compliance';
import { ProgressLog } from './components/pages/wip-operations/progress-log/progress-log';
import { ProductOutput } from './components/pages/wip-operations/product-output/product-output';
import { Events } from './components/pages/events/events/events';
import { Reports } from './components/pages/reports/reports/reports';
import { CreateReport } from './components/pages/reports/create-report/create-report';
import { ReportView } from './components/pages/reports/report-view/report-view';
import { Layout } from './components/shared/layout/layout';
// DEMO MODE: Auth guard import kept for reference
// import { authGuard } from './components/guard/auth-guard';
import { Administration } from './components/pages/administration/administration/administration';
import { Configuration } from './components/pages/administration/configuration/configuration';
import { License } from './components/pages/administration/license/license';
import { UserManagement } from './components/pages/administration/user-management/user-management';
import { User } from './components/pages/administration/user-management/user/user';
import { Role } from './components/pages/administration/user-management/role/role';
import { CreateRole } from './components/pages/administration/user-management/create-role/create-role';

import { Projects } from './components/pages/administration/configuration/projects/projects';
import { Devices } from './components/pages/administration/configuration/devices/devices';
import { Assets } from './components/pages/administration/configuration/assets/assets';
import { AssetLocationHistory } from './components/pages/administration/configuration/assets/location-history/location-history';
import { AssetAssignmentOwnership } from './components/pages/administration/configuration/assets/assignment-ownership/assignment-ownership';
import { AssetLifecycle } from './components/pages/administration/configuration/assets/asset-lifecycle/asset-lifecycle';
import { AssetTrackingTelemetry } from './components/pages/administration/configuration/assets/tracking-telemetry/tracking-telemetry';
import { AssetMaintenanceService } from './components/pages/administration/configuration/assets/maintenance-service/maintenance-service';
import { AssetUtilizationPerformance } from './components/pages/administration/configuration/assets/utilization-performance/utilization-performance';
import { AssetFinancial } from './components/pages/administration/configuration/assets/financial/financial';
import { AssetDocumentAttachment } from './components/pages/administration/configuration/assets/document-attachment/document-attachment';
import { AssetWarrantyContract } from './components/pages/administration/configuration/assets/warranty-contract/warranty-contract';
import { AssetAlertIncident } from './components/pages/administration/configuration/assets/alert-incident/alert-incident';
import { AssetAuditVerification } from './components/pages/administration/configuration/assets/audit-verification/audit-verification';
import { AssetActivityAuditTrail } from './components/pages/administration/configuration/assets/activity-audit-trail/activity-audit-trail';
import { AssetCustomDomainFields } from './components/pages/administration/configuration/assets/custom-domain-fields/custom-domain-fields';
import { AssetIntegration } from './components/pages/administration/configuration/assets/integration/integration';
import { AssetComplianceCertification } from './components/pages/administration/configuration/assets/compliance-certification/compliance-certification';
import { AssetAudit } from './components/pages/administration/configuration/assets/asset-audit/asset-audit/asset-audit';
import { NewassetAudit } from './components/pages/administration/configuration/assets/asset-audit/newasset-audit/newasset-audit';
import { AssetMovement } from './components/pages/administration/configuration/assets/asset-movement/asset-movement';
import { AssetDisposal } from './components/pages/administration/configuration/assets/asset-disposal/asset-disposal/asset-disposal';
import { NewDisposal } from './components/pages/administration/configuration/assets/asset-disposal/new-disposal/new-disposal';
import { AssetOverview } from './components/pages/dashboard/asset-overview/asset-overview';
import { IotTrackingOverview } from './components/pages/dashboard/iot-tracking-overview/iot-tracking-overview';
import { Report } from './components/pages/dashboard/report/report';
import { ServiceRequest } from './components/pages/administration/configuration/assets/service-request/service-request/service-request';
import { NewServiceRequest } from './components/pages/administration/configuration/assets/service-request/new-service-request/new-service-request';
import { AssetTaggedAssets } from './components/pages/administration/configuration/assets/tagged-assets/tagged-assets';
import { AssetCheckout } from './components/pages/administration/configuration/assets/asset-checkout/asset-checkout';
import { AssetCheckin } from './components/pages/administration/configuration/assets/asset-checkin/asset-checkin';
import { MaintenanceWorkOrder } from './components/pages/administration/configuration/maintenance/work-order/work-order';
import { MaintenanceTask } from './components/pages/administration/configuration/maintenance/maintenance-task/maintenance-task';
import { MaintenancePreventive } from './components/pages/administration/configuration/maintenance/preventive-maintenance/preventive-maintenance';
import { MaintenancePredictive } from './components/pages/administration/configuration/maintenance/predictive-maintenance/predictive-maintenance';
import { MaintenanceBreakdownIssueReporting } from './components/pages/administration/configuration/maintenance/breakdown-issue-reporting/breakdown-issue-reporting';
import { MaintenanceSpareParts } from './components/pages/administration/configuration/maintenance/spare-parts/spare-parts';
import { MaintenanceTechnician } from './components/pages/administration/configuration/maintenance/technician/technician';
import { MaintenanceVendorAmc } from './components/pages/administration/configuration/maintenance/vendor-amc/vendor-amc';
import { MaintenanceCostTracking } from './components/pages/administration/configuration/maintenance/cost-tracking/cost-tracking';
import { MaintenanceDowntimeTracking } from './components/pages/administration/configuration/maintenance/downtime-tracking/downtime-tracking';
import { MaintenancePerformance } from './components/pages/administration/configuration/maintenance/performance/performance';
import { MaintenanceComplianceInspection } from './components/pages/administration/configuration/maintenance/compliance-inspection/compliance-inspection';
import { MasterManagementMaster } from './components/pages/administration/configuration/master-management/master-maintenance/master-maintenance';
import { MasterManagementCategorySubcategory } from './components/pages/administration/configuration/master-management/category-subcategory/category-subcategory';
import { MasterManagementAssetType } from './components/pages/administration/configuration/master-management/asset-type/asset-type';
import { MasterManagementAssignedCustodianDepartment } from './components/pages/administration/configuration/master-management/assigned-custodian-department/assigned-custodian-department';
import { MasterManagementCurrentLocation } from './components/pages/administration/configuration/master-management/current-location/current-location';
import { MasterManagementStatusChanges } from './components/pages/administration/configuration/master-management/status-changes/status-changes';
import { MasterManagementTagIds } from './components/pages/administration/configuration/master-management/tag-ids/tag-ids';
import { MasterManagementDepreciationMethod } from './components/pages/administration/configuration/master-management/depreciation-method/depreciation-method';
import { MasterManagementCostCenter } from './components/pages/administration/configuration/master-management/cost-center/cost-center';
import { MasterManagementAlertType } from './components/pages/administration/configuration/master-management/alert-type/alert-type';
import { MasterManagementResolutionStatus } from './components/pages/administration/configuration/master-management/resolution-status/resolution-status';
import { MasterManagementAuditorDetails } from './components/pages/administration/configuration/master-management/auditor-details/auditor-details';
import { MasterManagementPhysicalVerificationResult } from './components/pages/administration/configuration/master-management/physical-verification-result/physical-verification-result';
import { MasterManagementAssetTypeFields } from './components/pages/administration/configuration/master-management/asset-type-fields/asset-type-fields';
import { MasterManagementApiSyncStatusMaster } from './components/pages/administration/configuration/master-management/api-sync-status-master/api-sync-status-master';
import { MasterManagementCertificationTypeMaster } from './components/pages/administration/configuration/master-management/certification-type-master/certification-type-master';
import { MasterManagementWorkType } from './components/pages/administration/configuration/master-management/work-type/work-type';
import { MasterManagementPriority } from './components/pages/administration/configuration/master-management/priority/priority';
import { MasterManagementStatus } from './components/pages/administration/configuration/master-management/status/status';
import { MasterManagementResourceType } from './components/pages/administration/configuration/master-management/resource-type/resource-type';
import { MasterManagementSkillMaster } from './components/pages/administration/configuration/master-management/skill-master/skill-master';
import { MasterManagementShiftMaster } from './components/pages/administration/configuration/master-management/shift-master/shift-master';
import { MasterManagementChecklistTypeMaster } from './components/pages/administration/configuration/master-management/checklist-type-master/checklist-type-master';
import { MasterManagementResponseTypeMaster } from './components/pages/administration/configuration/master-management/response-type-master/response-type-master';
import { MasterManagementConditionMaster } from './components/pages/administration/configuration/master-management/condition-master/condition-master';
import { MasterManagementIssueTypeMaster } from './components/pages/administration/configuration/master-management/issue-type-master/issue-type-master';
import { MasterManagementSeverityMaster } from './components/pages/administration/configuration/master-management/severity-master/severity-master';
import { MasterManagementUnitMaster } from './components/pages/administration/configuration/master-management/unit-master/unit-master';
import { MasterManagementPermitTypeMaster } from './components/pages/administration/configuration/master-management/permit-type-master/permit-type-master';
import { MasterManagementUpdateSourceMaster } from './components/pages/administration/configuration/master-management/update-source-master/update-source-master';
import { MasterManagementChartTypeMaster } from './components/pages/administration/configuration/master-management/chart-type-master/chart-type-master';
import { MasterManagementPermissionMaster } from './components/pages/administration/configuration/master-management/permission-master/permission-master';
import { MasterManagementModuleAccessMaster } from './components/pages/administration/configuration/master-management/module-access-master/module-access-master';
import { MasterManagementOrganization } from './components/pages/administration/configuration/master-management/organization/organization';
import { MasterManagementBusinessUnit } from './components/pages/administration/configuration/master-management/business-unit/business-unit';
import { MasterManagementDepartment } from './components/pages/administration/configuration/master-management/department/department';
import { MasterManagementSite } from './components/pages/administration/configuration/master-management/site/site';
import { MasterManagementManufacturer } from './components/pages/administration/configuration/master-management/manufacturer/manufacturer';
import { MasterManagementSupplier } from './components/pages/administration/configuration/master-management/supplier/supplier';
import { InspectionTypeMaster } from './components/pages/administration/configuration/inspection/inspection-type/inspection-type';
import { InspectionTaskCategory } from './components/pages/administration/configuration/inspection/task-category/task-category';
import { InspectionTask } from './components/pages/administration/configuration/inspection/inspection-task/inspection-task';
import { InspectionChecklistTemplateList } from './components/pages/administration/configuration/inspection/checklist-template/checklist-template-list/checklist-template-list';
import { InspectionChecklistTemplateBuilder } from './components/pages/administration/configuration/inspection/checklist-template/checklist-template-builder/checklist-template-builder';
import { InspectionFailureReason } from './components/pages/administration/configuration/inspection/failure-reason/failure-reason';
import { InspectionDefect } from './components/pages/administration/configuration/inspection/defect/defect';
import { InspectionSeverity } from './components/pages/administration/configuration/inspection/severity/severity';
import { InspectionPriority } from './components/pages/administration/configuration/inspection/priority/priority';
import { InspectionSignatureStamp } from './components/pages/administration/configuration/inspection/signature-stamp/signature-stamp';
import { InspectionNotificationTemplate } from './components/pages/administration/configuration/inspection/notification-template/notification-template';
import { InspectionReportTemplate } from './components/pages/administration/configuration/inspection/report-template/report-template';
import { InspectionNumberingSequence } from './components/pages/administration/configuration/inspection/numbering-sequence/numbering-sequence';
import { InspectionHolidayCalendar } from './components/pages/administration/configuration/inspection/holiday-calendar/holiday-calendar';
import { WorkflowList } from './components/pages/administration/configuration/workflows/workflow-list/workflow-list';
import { WorkflowBuilder } from './components/pages/administration/configuration/workflows/workflow-builder/workflow-builder';
import { WorkflowInstances } from './components/pages/administration/configuration/workflows/workflow-instances/workflow-instances';
import { MyTasks } from './components/pages/administration/configuration/workflows/my-tasks/my-tasks';
import { WorkflowInsights } from './components/pages/administration/configuration/workflows/workflow-insights/workflow-insights';
import { ProcessAutomation } from './components/pages/process-automation/process-automation/process-automation';
import { ProcessAutomationAdd } from './components/pages/process-automation/process-automation-add/process-automation-add';
import { WipJobMaster } from './components/pages/administration/configuration/wip/job-master/job-master';
import { WipStatusMaster } from './components/pages/administration/configuration/wip/status-master/status-master';
import { WipResourceMaster } from './components/pages/administration/configuration/wip/resource-master/resource-master';
import { WipTaskMaster } from './components/pages/administration/configuration/wip/task-master/task-master';
import { WipChecklistMaster } from './components/pages/administration/configuration/wip/checklist-master/checklist-master';
import { WipChecklistItems } from './components/pages/administration/configuration/wip/checklist-items/checklist-items';
import { WipLocationMaster } from './components/pages/administration/configuration/wip/location-master/location-master';
import { WipAssetLinking } from './components/pages/administration/configuration/wip/asset-linking/asset-linking';
import { WipSlaMaster } from './components/pages/administration/configuration/wip/sla-master/sla-master';
import { WipIssueDelay } from './components/pages/administration/configuration/wip/issue-delay/issue-delay';
import { WipMaterialConsumption } from './components/pages/administration/configuration/wip/material-consumption/material-consumption';
import { WipPermitCompliance } from './components/pages/administration/configuration/wip/permit-compliance/permit-compliance';
import { WipProgressLog } from './components/pages/administration/configuration/wip/progress-log/progress-log';
import { WipAlerts } from './components/pages/administration/configuration/wip/alerts/alerts';
import { WipKpiConfig } from './components/pages/administration/configuration/wip/kpi-config/kpi-config';
import { WipRoleAccess } from './components/pages/administration/configuration/wip/role-access/role-access';

function remoteRoutes(remoteName: string) {
  return () =>
    loadRemoteModule({ remoteName, exposedModule: './routes' })
      .then((m) => m.routes)
      .catch((err) => {
        console.error(`Failed to load remote "${remoteName}":`, err);
        return [{ path: '', component: SectionUnavailable }];
      });
}

export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: 'login', component: Login },
  { path: 'signup', component: Signup },
  {
    path: '',
    component: Layout,
    // DEMO MODE: Auth guard is disabled
    // canActivate: [authGuard],
    children: [
      // Dashboard sub pages: placeholders until their screens are built (listed before the remote so they match first)
      { path: 'dashboard/iot-tracking-overview', component: IotTrackingOverview },
      { path: 'dashboard/asset-overview', component: AssetOverview },
      { path: 'dashboard/reports', component: Report },
      { path: 'dashboard', loadChildren: remoteRoutes('dashboard') },
      // New UI modules: placeholder pages until their screens are built
      // The Assets menu only expands its submenu; a direct visit to /assets lands on the Asset List
      { path: 'assets', redirectTo: 'assets/list', pathMatch: 'full' },
      // Asset detail tabs take the asset as ?tag=, e.g. /assets/relationships?tag=GEN-00045
      { path: 'assets/relationships', component: AssetRelationship },
      // Menu page: relationships users create between a main asset and a sub asset
      { path: 'assets/asset-relationships', component: AssetRelationshipList },
      { path: 'assets/registration', component: AssetRegistration },
      { path: 'assets/list', component: AssetList },
      { path: 'assets/categories', component: AssetCategories },
      { path: 'assets/types-models', component: AssetTypesModels },
      { path: 'assets/hierarchy', component: AssetHierarchy },
      { path: 'assets/components', component: AssetComponents },
      { path: 'assets/location-history', component: LocationHistory },
      { path: 'assets/assignment', component: AssetAssignment },
      // ?type= preselects the assignee kind, ?assets= the asset tags
      { path: 'assets/assignment/assign-asset', component: AssignAsset },
      { path: 'assets/assignment/bulk-assignment', component: BulkAssignment },
      { path: 'assets/maintenance', component: AssetMaintenance },
      // ?tag= the asset, ?tab= details | updates | attachments | related; without an id the asset's latest request opens
      { path: 'assets/service-requests', component: ServiceRequest },
      { path: 'assets/service-requests/new', component: NewServiceRequest },
      { path: 'assets/service-requests/:id', component: ServiceRequest },
      { path: 'assets/audit', component: AssetAudit },
      { path: 'assets/audit/new-audit', component: NewassetAudit },
      { path: 'assets/disposal', component: AssetDisposal },
      { path: 'assets/disposal/new-disposal', component: NewDisposal },
      { path: 'assets/custom-fields', component: AssetCustomFields },
      { path: 'assets/lifecycle', component: AssetLifecycle },
      { path: 'assets/documents', component: AssetDocuments },
      // Menu page: documents of every asset, uploaded to one or more assets at a time
      { path: 'assets/all-documents', component: AssetDocuments, data: { allAssets: true } },
      { path: 'assets/images', component: AssetImages },
      // Menu page: images of every asset, uploaded to one or more assets at a time
      { path: 'assets/all-images', component: AssetImages, data: { allAssets: true } },
      { path: 'assets/status-condition', component: AssetStatusCondition },
      { path: 'assets/criticality', component: AssetCriticality },
      { path: 'assets/health-score', component: AssetHealthScore },
      { path: 'assets/bulk-import-export', component: AssetBulk },
      { path: 'assets/merge-dedup', component: AssetMerge },
      { path: 'tracking-iot', component: ModulePlaceholder, data: { title: 'Tracking & IoT' } },
      { path: 'tracking-iot/analytics-insights', component: AnalyticsInsights },
      { path: 'tracking-iot/tag-ids', component: TagIds },
      { path: 'tracking-iot/firmware-connectivity', component: FirmwareConnectivity },
      { path: 'tracking-iot/real-time-location', loadChildren: remoteRoutes('locating') },
      { path: 'movement-custody', component: ModulePlaceholder, data: { title: 'Movement & Custody' } },
      { path: 'maintenance', component: ModulePlaceholder, data: { title: 'Maintenance' } },
      { path: 'inspection', component: ModulePlaceholder, data: { title: 'Inspection' } },
      { path: 'inventory', component: ModulePlaceholder, data: { title: 'Inventory' } },
      { path: 'wip-operations', redirectTo: 'wip-operations/job-templates', pathMatch: 'full' },
      // Opens one template; ?code=JT-00042 picks which
      { path: 'wip-operations/job-templates', component: JobTemplates },
      // Opens one job order; ?code=JO-2026-00438 picks which
      { path: 'wip-operations/job-orders', component: JobOrders },
      { path: 'wip-operations/process-routing', component: ModulePlaceholder, data: { title: 'Process & Routing' } },
      // Opens one task; ?code=TM-00056 picks which
      { path: 'wip-operations/task-master', component: TaskMaster },
      { path: 'wip-operations/resources', component: Resources },
      // Opens one job order's materials; ?job=JO-2026-00438 picks which
      { path: 'wip-operations/material-consumption', component: MaterialConsumption },
      { path: 'wip-operations/location-management', component: LocationManagement },
      { path: 'wip-operations/asset-linking', component: AssetLinking },
      { path: 'wip-operations/sla-management', component: SlaManagement },
      { path: 'wip-operations/progress-log', component: ProgressLog },
      { path: 'wip-operations/product-output', component: ProductOutput },
      { path: 'wip-operations/progress-output', redirectTo: 'wip-operations/progress-log', pathMatch: 'full' },
      { path: 'wip-operations/issues-delays', component: IssuesDelays },
      { path: 'wip-operations/permit-compliance', component: PermitCompliance },
      { path: 'audit-compliance', component: ModulePlaceholder, data: { title: 'Audit & Compliance' } },
      { path: 'contracts-warranty', component: ModulePlaceholder, data: { title: 'Contracts & Warranty' } },
      { path: 'financial', component: ModulePlaceholder, data: { title: 'Financial' } },
      { path: 'reports-analytics', component: ModulePlaceholder, data: { title: 'Reports & Analytics' } },
      { path: 'locating', redirectTo: 'tracking-iot/real-time-location', pathMatch: 'full' },
      { path: 'events', component: Events },
      { path: 'report', component: Reports },
      { path: 'report/create', component: CreateReport },
      { path: 'report/view/:id', component: ReportView },
      { path: 'process-automation', component: ProcessAutomation },
      { path: 'process-automation/add', component: ProcessAutomationAdd },
      {
        path: 'administration',
        component: Administration,
        children: [
          { path: '', redirectTo: 'configuration', pathMatch: 'full' },
          {
            path: 'configuration',
            component: Configuration,
            children: [
              { path: '', redirectTo: 'projects', pathMatch: 'full' },
              { path: 'projects', component: Projects },
              { path: 'devices', component: Devices },
              { path: 'assets', redirectTo: 'assets/asset-registry', pathMatch: 'full' },
              { path: 'assets/asset-registry', component: Assets },
              { path: 'assets/location-history', component: AssetLocationHistory },
              { path: 'assets/assignment-ownership', component: AssetAssignmentOwnership },
              // The lifecycle page now lives under the Assets menu.
              { path: 'assets/asset-lifecycle', redirectTo: '/assets/lifecycle', pathMatch: 'full' },
              { path: 'assets/tracking-telemetry', component: AssetTrackingTelemetry },
              { path: 'assets/maintenance-service', component: AssetMaintenanceService },
              { path: 'assets/utilization-performance', component: AssetUtilizationPerformance },
              { path: 'assets/financial', component: AssetFinancial },
              { path: 'assets/document-attachment', component: AssetDocumentAttachment },
              { path: 'assets/warranty-contract', component: AssetWarrantyContract },
              { path: 'assets/alert-incident', component: AssetAlertIncident },
              { path: 'assets/audit-verification', component: AssetAuditVerification },
              { path: 'assets/activity-audit-trail', component: AssetActivityAuditTrail },
              { path: 'assets/custom-domain-fields', component: AssetCustomDomainFields },
              { path: 'assets/integration', component: AssetIntegration },
              { path: 'assets/compliance-certification', component: AssetComplianceCertification },
              { path: 'assets/asset-movement', component: AssetMovement },
              { path: 'assets/asset-disposal', component: AssetDisposal },
              { path: 'assets/tagged-assets', component: AssetTaggedAssets },
              { path: 'assets/asset-checkout', component: AssetCheckout },
              { path: 'assets/asset-checkin', component: AssetCheckin },
              { path: 'maintenance', redirectTo: 'maintenance/work-order', pathMatch: 'full' },
              { path: 'maintenance/work-order', component: MaintenanceWorkOrder },
              { path: 'maintenance/maintenance-task', component: MaintenanceTask },
              { path: 'maintenance/preventive-maintenance', component: MaintenancePreventive },
              { path: 'maintenance/predictive-maintenance', component: MaintenancePredictive },
              { path: 'maintenance/breakdown-issue-reporting', component: MaintenanceBreakdownIssueReporting },
              { path: 'maintenance/spare-parts', component: MaintenanceSpareParts },
              { path: 'maintenance/technician', component: MaintenanceTechnician },
              { path: 'maintenance/vendor-amc', component: MaintenanceVendorAmc },
              { path: 'maintenance/cost-tracking', component: MaintenanceCostTracking },
              { path: 'maintenance/downtime-tracking', component: MaintenanceDowntimeTracking },
              { path: 'maintenance/performance', component: MaintenancePerformance },
              { path: 'maintenance/compliance-inspection', component: MaintenanceComplianceInspection },
              { path: 'workflows', redirectTo: 'workflows/list', pathMatch: 'full' },
              { path: 'workflows/list', component: WorkflowList },
              { path: 'workflows/builder', component: WorkflowBuilder },
              { path: 'workflows/builder/:id', component: WorkflowBuilder },
              { path: 'workflows/instances', component: WorkflowInstances },
              { path: 'workflows/tasks', component: MyTasks },
              { path: 'workflows/insights', component: WorkflowInsights },
              { path: 'wip', redirectTo: 'wip/job-master', pathMatch: 'full' },
              { path: 'wip/job-master', component: WipJobMaster },
              { path: 'wip/status-master', component: WipStatusMaster },
              { path: 'wip/resource-master', component: WipResourceMaster },
              { path: 'wip/task-master', component: WipTaskMaster },
              { path: 'wip/checklist-master', component: WipChecklistMaster },
              { path: 'wip/checklist-items', component: WipChecklistItems },
              { path: 'wip/location-master', component: WipLocationMaster },
              { path: 'wip/asset-linking', component: WipAssetLinking },
              { path: 'wip/sla-master', component: WipSlaMaster },
              { path: 'wip/issue-delay', component: WipIssueDelay },
              { path: 'wip/material-consumption', component: WipMaterialConsumption },
              { path: 'wip/permit-compliance', component: WipPermitCompliance },
              { path: 'wip/progress-log', component: WipProgressLog },
              { path: 'wip/alerts', component: WipAlerts },
              { path: 'wip/kpi-config', component: WipKpiConfig },
              { path: 'wip/role-access', component: WipRoleAccess },
              { path: 'master-management', redirectTo: 'master-management/master-maintenance', pathMatch: 'full' },
              { path: 'master-management/master-maintenance', component: MasterManagementMaster },
              { path: 'master-management/category-subcategory', component: MasterManagementCategorySubcategory },
              { path: 'master-management/asset-type', component: MasterManagementAssetType },
              { path: 'master-management/assigned-custodian-department', component: MasterManagementAssignedCustodianDepartment },
              { path: 'master-management/current-location', component: MasterManagementCurrentLocation },
              { path: 'master-management/status-changes', component: MasterManagementStatusChanges },
              { path: 'master-management/tag-ids', component: MasterManagementTagIds },
              { path: 'master-management/depreciation-method', component: MasterManagementDepreciationMethod },
              { path: 'master-management/cost-center', component: MasterManagementCostCenter },
              { path: 'master-management/alert-type', component: MasterManagementAlertType },
              { path: 'master-management/resolution-status', component: MasterManagementResolutionStatus },
              { path: 'master-management/auditor-details', component: MasterManagementAuditorDetails },
              { path: 'master-management/physical-verification-result', component: MasterManagementPhysicalVerificationResult },
              { path: 'master-management/asset-type-fields', component: MasterManagementAssetTypeFields },
              { path: 'master-management/api-sync-status-master', component: MasterManagementApiSyncStatusMaster },
              { path: 'master-management/certification-type-master', component: MasterManagementCertificationTypeMaster },
              { path: 'master-management/work-type', component: MasterManagementWorkType },
              { path: 'master-management/priority', component: MasterManagementPriority },
              { path: 'master-management/status', component: MasterManagementStatus },
              { path: 'master-management/resource-type', component: MasterManagementResourceType },
              { path: 'master-management/skill-master', component: MasterManagementSkillMaster },
              { path: 'master-management/shift-master', component: MasterManagementShiftMaster },
              { path: 'master-management/checklist-type-master', component: MasterManagementChecklistTypeMaster },
              { path: 'master-management/response-type-master', component: MasterManagementResponseTypeMaster },
              { path: 'master-management/condition-master', component: MasterManagementConditionMaster },
              { path: 'master-management/issue-type-master', component: MasterManagementIssueTypeMaster },
              { path: 'master-management/severity-master', component: MasterManagementSeverityMaster },
              { path: 'master-management/unit-master', component: MasterManagementUnitMaster },
              { path: 'master-management/permit-type-master', component: MasterManagementPermitTypeMaster },
              { path: 'master-management/update-source-master', component: MasterManagementUpdateSourceMaster },
              { path: 'master-management/chart-type-master', component: MasterManagementChartTypeMaster },
              { path: 'master-management/permission-master', component: MasterManagementPermissionMaster },
              { path: 'master-management/module-access-master', component: MasterManagementModuleAccessMaster },
              { path: 'master-management/organization', component: MasterManagementOrganization },
              { path: 'master-management/business-unit', component: MasterManagementBusinessUnit },
              { path: 'master-management/department', component: MasterManagementDepartment },
              { path: 'master-management/site', component: MasterManagementSite },
              { path: 'master-management/manufacturer', component: MasterManagementManufacturer },
              { path: 'master-management/supplier', component: MasterManagementSupplier },
              { path: 'inspection', redirectTo: 'inspection/inspection-type', pathMatch: 'full' },
              { path: 'inspection/inspection-type', component: InspectionTypeMaster },
              { path: 'inspection/task-category', component: InspectionTaskCategory },
              { path: 'inspection/inspection-task', component: InspectionTask },
              { path: 'inspection/checklist-template', redirectTo: 'inspection/checklist-template/list', pathMatch: 'full' },
              { path: 'inspection/checklist-template/list', component: InspectionChecklistTemplateList },
              { path: 'inspection/checklist-template/builder', component: InspectionChecklistTemplateBuilder },
              { path: 'inspection/checklist-template/builder/:id', component: InspectionChecklistTemplateBuilder },
              { path: 'inspection/failure-reason', component: InspectionFailureReason },
              { path: 'inspection/defect', component: InspectionDefect },
              { path: 'inspection/severity', component: InspectionSeverity },
              { path: 'inspection/priority', component: InspectionPriority },
              { path: 'inspection/signature-stamp', component: InspectionSignatureStamp },
              { path: 'inspection/notification-template', component: InspectionNotificationTemplate },
              { path: 'inspection/report-template', component: InspectionReportTemplate },
              { path: 'inspection/numbering-sequence', component: InspectionNumberingSequence },
              { path: 'inspection/holiday-calendar', component: InspectionHolidayCalendar }
            ]
          },
          { path: 'license', component: License },
          {
            path: 'user-management',
            component: UserManagement,
            children: [
              { path: '', redirectTo: 'user', pathMatch: 'full' },
              { path: 'user', component: User },
              { path: 'role', component: Role },
              { path: 'role/create', component: CreateRole },
              { path: 'role/edit/:id', component: CreateRole }
            ]
          }
        ]
      }
    ]
  }
];

