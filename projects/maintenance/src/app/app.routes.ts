import { Routes } from '@angular/router';
import { OverviewPage } from './overview/overview';
import { PredictivePage } from './predictive/predictive';
import { PreventivePage } from './preventive/preventive';
import { ServiceRequestsPage } from './service-requests/service-requests';
import { WorkOrdersPage } from './work-orders/work-orders';

// Mounted by the shell at /maintenance; paths match the Maintenance menu in the shell's sidebar.
// Deep links: ?code=<WO / SR / PM code or asset tag> opens that record; /work-orders?new=1 opens the create form.
export const routes: Routes = [
  { path: '', redirectTo: 'overview', pathMatch: 'full' },
  { path: 'overview', component: OverviewPage },
  { path: 'work-orders', component: WorkOrdersPage },
  { path: 'preventive', component: PreventivePage },
  { path: 'predictive', component: PredictivePage },
  { path: 'service-requests', component: ServiceRequestsPage }
];
