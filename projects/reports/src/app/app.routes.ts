import { Routes } from '@angular/router';
import { Reports } from './reports/reports';
import { CreateReport } from './create-report/create-report';
import { ReportView } from './report-view/report-view';
// import { ReportsAnalytics } from './analytics/reports-analytics/reports-analytics';

// Mounted by the shell at /reports-analytics (the old /report links redirect there)
export const routes: Routes = [
  // The Reports & Analytics overview page is not shown: the section opens on Standard Reports
  // { path: '', component: ReportsAnalytics },
  { path: '', redirectTo: 'standard-reports', pathMatch: 'full' },
  { path: 'generated-reports', component: Reports },
  { path: 'create', component: CreateReport },
  { path: 'view/:id', component: ReportView },
  {
    path: 'standard-reports',
    loadComponent: () => import('./analytics/standard-reports/standard-reports').then((m) => m.StandardReportsPage)
  },
  {
    path: 'custom-reports',
    loadComponent: () => import('./analytics/custom-reports/custom-reports').then((m) => m.CustomReportsPage)
  },
  {
    path: 'interactive-dashboards',
    loadComponent: () => import('./analytics/interactive-dashboards/interactive-dashboards').then((m) => m.InteractiveDashboardsPage)
  },
  {
    path: 'asset-performance',
    loadComponent: () => import('./analytics/asset-performance/asset-performance').then((m) => m.AssetPerformancePage)
  },
  {
    path: 'maintenance-analytics',
    loadComponent: () => import('./analytics/maintenance-analytics/maintenance-analytics').then((m) => m.MaintenanceAnalyticsPage)
  },
  {
    path: 'inspection-analytics',
    loadComponent: () => import('./analytics/inspection-analytics/inspection-analytics').then((m) => m.InspectionAnalyticsPage)
  },
  {
    path: 'wip-production-analytics',
    loadComponent: () => import('./analytics/wip-production-analytics/wip-production-analytics').then((m) => m.WipProductionAnalyticsPage)
  },
  {
    path: 'inventory-analytics',
    loadComponent: () => import('./analytics/inventory-analytics/inventory-analytics').then((m) => m.InventoryAnalyticsPage)
  },
  {
    path: 'financial-analytics',
    loadComponent: () => import('./analytics/financial-analytics/financial-analytics').then((m) => m.FinancialAnalyticsPage)
  },
  {
    path: 'compliance-analytics',
    loadComponent: () => import('./analytics/compliance-analytics/compliance-analytics').then((m) => m.ComplianceAnalyticsPage)
  },
  {
    path: 'iot-utilization-analytics',
    loadComponent: () => import('./analytics/iot-utilization-analytics/iot-utilization-analytics').then((m) => m.IotUtilizationAnalyticsPage)
  },
  {
    path: 'predictive-analytics',
    loadComponent: () => import('./analytics/predictive-analytics/predictive-analytics').then((m) => m.PredictiveAnalyticsPage)
  },
  {
    path: 'kpi-dashboard',
    loadComponent: () => import('./analytics/kpi-dashboard/kpi-dashboard').then((m) => m.KpiDashboardPage)
  },
  {
    path: 'data-export-api',
    loadComponent: () => import('./analytics/data-export-api/data-export-api').then((m) => m.DataExportApiPage)
  }
];
