import { Routes } from '@angular/router';
import { ModulePlaceholder } from 'shared-ui';
import { JobTemplates } from './job-templates/job-templates';
import { JobOrders } from './job-orders/job-orders';
import { Resources } from './resources/resources';
import { TaskMaster } from './task-master/task-master';
import { MaterialConsumption } from './material-consumption/material-consumption';
import { LocationManagement } from './location-management/location-management';
import { IssuesDelays } from './issues-delays/issues-delays';
import { SlaManagement } from './sla-management/sla-management';
import { AssetLinking } from './asset-linking/asset-linking';
import { PermitCompliance } from './permit-compliance/permit-compliance';
import { ProgressLog } from './progress-log/progress-log';
import { ProductOutput } from './product-output/product-output';

// Mounted by the shell at /wip-operations; paths match the WIP / Operations menu in the shell's sidebar
export const routes: Routes = [
  { path: '', redirectTo: 'job-templates', pathMatch: 'full' },
  // Opens one template; ?code=JT-00042 picks which
  { path: 'job-templates', component: JobTemplates },
  // Opens one job order; ?code=JO-2026-00438 picks which
  { path: 'job-orders', component: JobOrders },
  { path: 'process-routing', component: ModulePlaceholder, data: { title: 'Process & Routing' } },
  // Opens one task; ?code=TM-00056 picks which
  { path: 'task-master', component: TaskMaster },
  { path: 'resources', component: Resources },
  // Opens one job order's materials; ?job=JO-2026-00438 picks which
  { path: 'material-consumption', component: MaterialConsumption },
  { path: 'location-management', component: LocationManagement },
  { path: 'asset-linking', component: AssetLinking },
  { path: 'sla-management', component: SlaManagement },
  { path: 'progress-log', component: ProgressLog },
  { path: 'product-output', component: ProductOutput },
  { path: 'progress-output', redirectTo: 'progress-log', pathMatch: 'full' },
  { path: 'issues-delays', component: IssuesDelays },
  { path: 'permit-compliance', component: PermitCompliance }
];
