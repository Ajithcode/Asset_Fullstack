import { Routes } from '@angular/router';
import { Reports } from './reports/reports';
import { CreateReport } from './create-report/create-report';
import { ReportView } from './report-view/report-view';

// Mounted by the shell at /reports-analytics (the old /report links redirect there)
export const routes: Routes = [
  { path: '', component: Reports },
  { path: 'create', component: CreateReport },
  { path: 'view/:id', component: ReportView }
];
