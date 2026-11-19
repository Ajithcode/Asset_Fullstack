import { Routes } from '@angular/router';
import { AssetRegistration } from './asset-registration/asset-registration';
import { AssetList } from './asset-list/asset-list';
import { AssetCategories } from './asset-categories/asset-categories';
import { AssetTypesModels } from './asset-types-models/asset-types-models';
import { AssetHierarchy } from './asset-hierarchy/asset-hierarchy';
import { AssetComponents } from './asset-components/asset-components';
import { AssetCustomFields } from './asset-custom-fields/asset-custom-fields';
import { AssetRelationship } from './asset-relationship/asset-relationship';
import { AssetRelationshipList } from './asset-relationship-list/asset-relationship-list';
import { AssetDocuments } from './asset-documents/asset-documents';
import { AssetImages } from './asset-images/asset-images';
import { AssetStatusCondition } from './asset-status-condition/asset-status-condition';
import { AssetStatusConditionOverview } from './asset-status-condition-overview/asset-status-condition-overview';
import { UpdateStatusCondition } from './asset-status-condition-overview/update-status-condition/update-status-condition';
import { AssetCriticality } from './asset-criticality/asset-criticality';
import { CriticalityLanding } from './asset-criticality/landing/criticality-landing';
import { legacyCriticalityRedirect } from './asset-criticality/legacy-redirect';
import { AssetHealthScore } from './asset-health-score/asset-health-score';
import { AssetBulk } from './asset-bulk/asset-bulk';
import { BulkAssignment } from './asset-bulk/bulk-assignment/bulk-assignment';
import { AssetMerge } from './asset-merge/asset-merge';
import { LocationOverview } from './asset-location-history/location-overview';
import { AssetAssignment } from './asset-assignment/asset-assignment';
import { AssignAsset } from './asset-assignment/assign-asset/assign-asset';
import { AssetMaintenance } from './asset-maintenance/asset-maintenance';
import { ServiceRequest } from './service-request/service-request/service-request';
import { NewServiceRequest } from './service-request/new-service-request/new-service-request';
import { AssetAudit } from './asset-audit/asset-audit/asset-audit';
import { NewassetAudit } from './asset-audit/newasset-audit/newasset-audit';
import { AssetDisposal } from './asset-disposal/asset-disposal/asset-disposal';
import { NewDisposal } from './asset-disposal/new-disposal/new-disposal';
import { AssetLifecycle } from './asset-lifecycle/asset-lifecycle';
import { AssetMaintenanceTab } from './asset-maintenance-tab/asset-maintenance-tab';
import { AssetDetailTab } from './asset-detail-tab/asset-detail-tab';
import { installRelaxedValidation } from '../../../../src/environments/relax-validation';

// TEMP: relaxed form validation on the testing build only (no-op unless environment.relaxValidation)
installRelaxedValidation();

// Mounted by the shell at /assets; paths match the Assets menu in the shell's sidebar
export const routes: Routes = [
  // The Assets menu only expands its submenu; a direct visit to /assets lands on the Asset List
  { path: '', redirectTo: 'list', pathMatch: 'full' },
  // Asset detail tabs take the asset as ?tag=, e.g. /assets/relationships?tag=GEN-00045, and show only that asset
  { path: 'overview', component: AssetDetailTab, data: { view: 'overview' } },
  { path: 'component-details', component: AssetDetailTab, data: { view: 'components' } },
  { path: 'field-values', component: AssetDetailTab, data: { view: 'fields' } },
  { path: 'history', component: AssetDetailTab, data: { view: 'history' } },
  { path: 'relationships', component: AssetRelationship },
  // Menu page: relationships users create between a main asset and a sub asset
  { path: 'asset-relationships', component: AssetRelationshipList },
  // Warns before leaving with entered data that was neither saved as draft nor registered
  { path: 'registration', component: AssetRegistration, canDeactivate: [(page: AssetRegistration) => page.canLeave()] },
  { path: 'list', component: AssetList },
  { path: 'categories', component: AssetCategories },
  { path: 'types-models', component: AssetTypesModels },
  { path: 'hierarchy', component: AssetHierarchy },
  { path: 'components', component: AssetComponents },
  // Overview of every asset's location changes; ?tag= opens that asset's history
  { path: 'location-history', component: LocationOverview },
  { path: 'assignment', component: AssetAssignment },
  // ?type= preselects the assignee kind, ?assets= the asset tags
  { path: 'assignment/assign-asset', component: AssignAsset },
  { path: 'assignment/bulk-assignment', component: BulkAssignment },
  { path: 'maintenance', component: AssetMaintenance },
  // Asset detail tabs (?tag=): the asset's and its components' maintenance and service history, inside the detail page
  { path: 'maintenance-details', component: AssetMaintenanceTab, data: { view: 'maintenance' } },
  { path: 'service-history', component: AssetMaintenanceTab, data: { view: 'service' } },
  // ?tag= the asset, ?tab= details | updates | attachments | related; without an id the asset's latest request opens
  { path: 'service-requests', component: ServiceRequest },
  { path: 'service-requests/new', component: NewServiceRequest },
  { path: 'service-requests/:id', component: ServiceRequest },
  { path: 'audit', component: AssetAudit },
  { path: 'audit/new-audit', component: NewassetAudit },
  { path: 'disposal', component: AssetDisposal },
  { path: 'disposal/new-disposal', component: NewDisposal },
  { path: 'custom-fields', component: AssetCustomFields },
  { path: 'lifecycle', component: AssetLifecycle },
  { path: 'documents', component: AssetDocuments },
  // Menu page: documents of every asset, uploaded to one or more assets at a time
  { path: 'all-documents', component: AssetDocuments, data: { allAssets: true } },
  { path: 'images', component: AssetImages },
  // Menu page: images of every asset, uploaded to one or more assets at a time
  { path: 'all-images', component: AssetImages, data: { allAssets: true } },
  // Asset detail tab (?tag=): one asset's operational status, condition assessments, readings and alarms
  { path: 'status-condition', component: AssetStatusCondition },
  // Menu page: status & condition of every asset and the history of changes
  { path: 'asset-status-condition', component: AssetStatusConditionOverview },
  // ?tag= preselects the asset, ?change= reopens a draft or rejected change
  { path: 'asset-status-condition/update', component: UpdateStatusCondition },
  // Menu page: every asset's criticality; old ?tag= links redirect to the asset's page below
  { path: 'criticality', pathMatch: 'full', canMatch: [legacyCriticalityRedirect], component: CriticalityLanding },
  // Asset detail tab: one asset's criticality
  { path: 'criticality/asset/:tag', component: AssetCriticality },
  { path: 'health-score', component: AssetHealthScore },
  { path: 'bulk-import-export', component: AssetBulk },
  { path: 'merge-dedup', component: AssetMerge }
];
