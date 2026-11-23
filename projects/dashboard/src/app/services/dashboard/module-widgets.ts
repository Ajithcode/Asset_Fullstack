import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../../../src/environments/environment';
import type { ReportKpi } from './dashboard';

/**
 * Data sources of the module tabs on the widgets page (/dashboard/widgets) other than the Asset service's
 * api/asset-dashboard endpoints (those stay in Dashboard): Tracking, Maintenance, Report Analytics, Dashboard and Inspection.
 */

// ===== Tracking service (5490) =====

/** One zone / geofence of GET {tracking}api/geofence-alerts/summary (fields the dashboard uses). */
export interface GeofenceTarget {
  id: string;
  kind: string;
  name: string;
  siteName: string | null;
  status: string;
  insideNow: number;
  activeAlerts: number;
}

/** Response of GET {tracking}api/geofence-alerts/summary */
export interface GeofenceAlertsSummary {
  generatedAt: string;
  targets: GeofenceTarget[];
}

/** One asset of GET {tracking}api/real-time-location/overview (fields the dashboard uses). */
export interface RealTimeAsset {
  assetId: string;
  assetTag: string | null;
  name: string;
  assetType: string | null;
  /** Live, Stale, Offline or Unknown. */
  freshness: string;
  /** Moving, Stationary or Unknown. */
  movement: string;
  location: { zoneName: string | null; floorName: string | null; buildingName: string | null; siteName: string | null; locationPath: string | null; at: string | null } | null;
  lastValidLocationAt: string | null;
  lastZoneChangeAt: string | null;
}

/** Response of GET {tracking}api/real-time-location/overview */
export interface RealTimeOverview {
  generatedAt: string;
  assets: RealTimeAsset[];
}

// ===== Maintenance service (5430) =====

/** One row of GET {maintenance}api/work-orders (fields the dashboard uses). */
export interface MaintenanceWorkOrder {
  id: string;
  workOrderId: string;
  assetId: string | null;
  assetName: string | null;
  workType: string | null;
  title: string | null;
  priority: string | null;
  assignedTechnician: string | null;
  dueDate: string | null;
  actualEndDate: string | null;
  createdAt: string | null;
  totalCost: number;
  /** Open, Scheduled, In Progress, On Hold, Completed or Cancelled; overdue is derived from dueDate. */
  status: string;
  isDeleted: boolean;
}

/** One row of GET {maintenance}api/preventive-maintenance (fields the dashboard uses). */
export interface PreventiveSchedule {
  id: string;
  pmScheduleId: string;
  scheduleName: string;
  assetId: string | null;
  assetName: string | null;
  nextDueDate: string | null;
  priority: string | null;
  status: string;
  isDeleted: boolean;
}

// ===== Report Analytics service (5480) =====

/** A Report Analytics section: `data` with the period it covers. */
export interface AnalyticsSection<T> {
  generatedAt: string;
  period: { preset: string; from: string; to: string };
  data: T;
}

/** Status breakdown (work-order-status sections). */
export interface CountBreakdown {
  total: number;
  items: { label: string; count: number; percentage: number }[];
}

/** data of GET {reports}api/maintenance-analytics/preventive-compliance */
export interface PreventiveCompliance {
  value: number | null;
  target: number;
  meetsTarget: boolean | null;
  completed: number;
  due: number;
}

/** One row of GET {reports}api/maintenance-analytics/technicians */
export interface TechnicianPerformance {
  technician: string;
  assigned: number;
  completed: number;
}

/** A month-by-month series (output-trend). */
export interface PerformanceSeries {
  categories: string[];
  series: { name: string; values: (number | null)[] }[];
}

/** data of GET {reports}api/production-analytics/summary (fields the dashboard uses). */
export interface ProductionSummary {
  productionOutput: ReportKpi;
  outputUnit: string;
}

/** One card of GET {reports}api/production-analytics/insights */
export interface ProductionInsight {
  key: string;
  /** success, info, warning or danger. */
  severity: string;
  title: string;
  message: string;
  count: number;
}

/** One row of GET {reports}api/production-analytics/orders */
export interface ProductionOrder {
  id: string;
  orderNo: string | null;
  product: string;
  workCenter: string;
  assetId: string | null;
  progress: number;
  dueDate: string | null;
  /** Not Started, In Progress, On Hold, Completed or Cancelled. */
  status: string;
  /** On Track, At Risk or Delayed. */
  schedule: string;
}

/** Response of GET {reports}api/production-analytics/orders */
export interface ProductionOrders {
  items: ProductionOrder[];
  totalCount: number;
}

// ===== Dashboard service (5470) =====

/** Response of GET {dashboard}api/dashboards/mobile/cost-summary (fields the dashboard uses). */
export interface MobileCostSummary {
  currency: string;
  period: { label: string };
  totalCost: number;
  previousPeriodCost: number | null;
  changePercent: number | null;
  monthlyTrend: { month: string; label: string; total: number }[];
}

/** Response of GET {dashboard}api/dashboards/mobile/my-tasks (fields the dashboard uses). */
export interface MobileMyTasks {
  userName: string | null;
  /** The names the signed-in user appears under as an assignee. */
  matchedAssigneeNames: string[];
}

// ===== Inspection service (5410) =====

/** One row of GET {inspection}api/inspections (fields the dashboard uses). */
export interface InspectionRecord {
  id: string;
  inspectionCode: string;
  title: string;
  assetCode: string | null;
  assetName: string | null;
  assignedTo: string | null;
  dueDate: string | null;
  startedAt: string | null;
  completedAt: string | null;
  findingsCount: number;
  responses: { result: string | null }[];
  signOff: { role: string; name: string | null; state: string; signedAt: string | null }[];
  /** Scheduled, In Progress, Completed, Overdue, Cancelled... */
  status: string;
  createdAt: string | null;
  updatedAt: string | null;
  isDeleted: boolean;
}

@Injectable({ providedIn: 'root' })
export class ModuleWidgetsApi {
  private readonly http = inject(HttpClient);

  /** GET {tracking}api/geofence-alerts/summary: every zone / geofence with assets inside and open alerts. */
  getGeofenceSummary(): Observable<GeofenceAlertsSummary> {
    return this.http.get<GeofenceAlertsSummary>(`${environment.trackingApiUrl}api/geofence-alerts/summary`);
  }

  /** GET {tracking}api/real-time-location/overview: tracked assets with movement, freshness and zone. */
  getRealTimeOverview(): Observable<RealTimeOverview> {
    return this.http.get<RealTimeOverview>(`${environment.trackingApiUrl}api/real-time-location/overview`);
  }

  /** GET {maintenance}api/work-orders: every work order. */
  getWorkOrders(): Observable<MaintenanceWorkOrder[]> {
    return this.http.get<MaintenanceWorkOrder[]>(`${environment.maintenanceApiUrl}api/work-orders`);
  }

  /** GET {maintenance}api/preventive-maintenance: every PM schedule. */
  getPreventiveSchedules(): Observable<PreventiveSchedule[]> {
    return this.http.get<PreventiveSchedule[]>(`${environment.maintenanceApiUrl}api/preventive-maintenance`);
  }

  /** GET {reports}api/maintenance-analytics/preventive-compliance (this year by default). */
  getPreventiveCompliance(): Observable<AnalyticsSection<PreventiveCompliance>> {
    return this.http.get<AnalyticsSection<PreventiveCompliance>>(`${environment.reportAnalyticsApiUrl}api/maintenance-analytics/preventive-compliance`);
  }

  /** GET {reports}api/maintenance-analytics/technicians (this year by default). */
  getTechnicianPerformance(): Observable<AnalyticsSection<TechnicianPerformance[]>> {
    return this.http.get<AnalyticsSection<TechnicianPerformance[]>>(`${environment.reportAnalyticsApiUrl}api/maintenance-analytics/technicians`);
  }

  /** GET {dashboard}api/dashboards/mobile/cost-summary: year-to-date cost vs. last year and the monthly trend. */
  getCostSummary(): Observable<MobileCostSummary> {
    return this.http.get<MobileCostSummary>(`${environment.dashboardApiUrl}api/dashboards/mobile/cost-summary`);
  }

  /** GET {reports}api/production-analytics/summary?datePreset= */
  getProductionSummary(datePreset: string): Observable<AnalyticsSection<ProductionSummary>> {
    const params = new HttpParams().set('datePreset', datePreset);
    return this.http.get<AnalyticsSection<ProductionSummary>>(`${environment.reportAnalyticsApiUrl}api/production-analytics/summary`, { params });
  }

  /** GET {reports}api/production-analytics/work-order-status (this year by default). */
  getProductionStatus(): Observable<AnalyticsSection<CountBreakdown>> {
    return this.http.get<AnalyticsSection<CountBreakdown>>(`${environment.reportAnalyticsApiUrl}api/production-analytics/work-order-status`);
  }

  /** GET {reports}api/production-analytics/output-trend: actual vs. planned output per month (this year by default). */
  getProductionOutputTrend(): Observable<AnalyticsSection<PerformanceSeries>> {
    return this.http.get<AnalyticsSection<PerformanceSeries>>(`${environment.reportAnalyticsApiUrl}api/production-analytics/output-trend`);
  }

  /** GET {reports}api/production-analytics/insights */
  getProductionInsights(): Observable<AnalyticsSection<ProductionInsight[]>> {
    return this.http.get<AnalyticsSection<ProductionInsight[]>>(`${environment.reportAnalyticsApiUrl}api/production-analytics/insights`);
  }

  /** GET {reports}api/production-analytics/orders?schedule=&pageSize=: active production orders (WIP jobs). */
  getProductionOrders(pageSize: number, schedule?: string): Observable<ProductionOrders> {
    let params = new HttpParams().set('pageSize', pageSize);
    if (schedule) params = params.set('schedule', schedule);
    return this.http.get<ProductionOrders>(`${environment.reportAnalyticsApiUrl}api/production-analytics/orders`, { params });
  }

  /** GET {inspection}api/inspections: every inspection. */
  getInspections(): Observable<InspectionRecord[]> {
    return this.http.get<InspectionRecord[]>(`${environment.inspectionApiUrl}api/inspections`);
  }

  /** GET {dashboard}api/dashboards/mobile/my-tasks?type=inspection: used for the names the signed-in user is assigned under. */
  getMyInspectionTasks(): Observable<MobileMyTasks> {
    const params = new HttpParams().set('type', 'inspection');
    return this.http.get<MobileMyTasks>(`${environment.dashboardApiUrl}api/dashboards/mobile/my-tasks`, { params });
  }
}

/** "5 min ago" / "3 h ago" / "2 d ago" from an ISO date; '' when missing. */
export function timeAgo(iso: string | null | undefined, now = Date.now()): string {
  if (!iso) return '';
  const t = Date.parse(iso);
  if (Number.isNaN(t)) return '';
  const minutes = Math.max(0, Math.round((now - t) / 60000));
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours} h ago`;
  return `${Math.round(hours / 24)} d ago`;
}

/** "Nov 23, 2026" from an ISO date (date part only, no time-zone shift); '—' when missing. */
export function shortDate(iso: string | null | undefined): string {
  if (!iso) return '—';
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso);
  const d = m ? new Date(+m[1], +m[2] - 1, +m[3]) : new Date(iso);
  return Number.isNaN(d.getTime()) ? '—' : d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
}
