import { Routes } from '@angular/router';
import { Dashboard } from './dashboard/dashboard';
import { IotTrackingOverview } from './iot-tracking-overview/iot-tracking-overview';
import { AssetOverview } from './asset-overview/asset-overview';
import { Report } from './report/report';

// Mounted by the shell at /dashboard; paths match the Dashboard menu in the shell's sidebar
export const routes: Routes = [
  { path: '', component: Dashboard },
  { path: 'iot-tracking-overview', component: IotTrackingOverview },
  { path: 'asset-overview', component: AssetOverview },
  { path: 'reports', component: Report }
];
