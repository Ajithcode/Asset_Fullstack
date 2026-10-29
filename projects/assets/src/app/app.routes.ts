import { Routes } from '@angular/router';
import { ModulePlaceholder } from 'shared-ui';
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
import { AssetCriticality } from './asset-criticality/asset-criticality';
import { AssetHealthScore } from './asset-health-score/asset-health-score';
import { AssetBulk } from './asset-bulk/asset-bulk';
import { AssetMerge } from './asset-merge/asset-merge';
import { LocationHistory } from './asset-location-history/location-history';
import { AssetMaintenance } from './asset-maintenance/asset-maintenance';
import { AssetLifecycle } from './asset-lifecycle/asset-lifecycle';

// Mounted by the shell at /assets; paths match the Assets menu in the shell's sidebar
export const routes: Routes = [
  // The Assets menu only expands its submenu; a direct visit to /assets lands on the Asset List
  { path: '', redirectTo: 'list', pathMatch: 'full' },
  // Asset detail tabs take the asset as ?tag=, e.g. /assets/relationships?tag=GEN-00045
  { path: 'relationships', component: AssetRelationship },
  // Menu page: relationships users create between a main asset and a sub asset
  { path: 'asset-relationships', component: AssetRelationshipList },
  { path: 'registration', component: AssetRegistration },
  { path: 'list', component: AssetList },
  { path: 'categories', component: AssetCategories },
  { path: 'types-models', component: AssetTypesModels },
  { path: 'hierarchy', component: AssetHierarchy },
  { path: 'components', component: AssetComponents },
  { path: 'location-history', component: LocationHistory },
  { path: 'assignment', component: ModulePlaceholder, data: { title: 'Assignment' } },
  { path: 'maintenance', component: AssetMaintenance },
  { path: 'service-requests', component: ModulePlaceholder, data: { title: 'Service Requests' } },
  { path: 'audit', component: ModulePlaceholder, data: { title: 'Asset Audit' } },
  { path: 'disposal', component: ModulePlaceholder, data: { title: 'Disposal' } },
  { path: 'custom-fields', component: AssetCustomFields },
  { path: 'lifecycle', component: AssetLifecycle },
  { path: 'documents', component: AssetDocuments },
  // Menu page: documents of every asset, uploaded to one or more assets at a time
  { path: 'all-documents', component: AssetDocuments, data: { allAssets: true } },
  { path: 'images', component: AssetImages },
  // Menu page: images of every asset, uploaded to one or more assets at a time
  { path: 'all-images', component: AssetImages, data: { allAssets: true } },
  { path: 'status-condition', component: AssetStatusCondition },
  { path: 'criticality', component: AssetCriticality },
  { path: 'health-score', component: AssetHealthScore },
  { path: 'bulk-import-export', component: AssetBulk },
  { path: 'merge-dedup', component: AssetMerge }
];
