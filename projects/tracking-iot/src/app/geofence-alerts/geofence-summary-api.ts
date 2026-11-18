import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map } from 'rxjs';
import { apiError, tenantParams, trackingUrl } from '../shared/tracking-api';
import { AlertEvent } from './geofence-data';
import { statusFromApi } from './iot-alert-api.service';

/*
 * Geofence & Alerts page data (Tracking service, GET /api/geofence-alerts/...): KPIs, indoor (floor plan) and outdoor
 * monitored areas with live figures, and the paged alert history.
 */

/** Monitored assets are unique; open alerts = not resolved / closed; unacknowledged = New. */
export interface GeofenceKpis {
  activeGeofences: number;
  outdoorActive: number;
  indoorMonitored: number;
  monitoredAssets: number;
  taggedAssets: number;
  alertsToday: number;
  alertsYesterday: number;
  openAlerts: number;
  unacknowledged: number;
  escalated: number;
}

/** An outdoor geofence (id) or a floor plan zone ("zone:{id}"): the target of alert rules. */
export interface GeofenceTarget {
  id: string;
  kind: 'Indoor' | 'Outdoor';
  name: string;
  code: string | null;
  /** Outdoor: Allowed / Warning / Restricted; indoor: the zone's access rule. */
  type: string | null;
  category: string | null;
  siteId: string | null;
  siteName: string | null;
  buildingName: string | null;
  floorName: string | null;
  floorPlanId: string | null;
  locationPath: string | null;
  shape: string | null;
  areaM2: number | null;
  /** Outdoor: Active / Disabled; indoor: Monitored / Not monitored. */
  status: string;
  rules: number;
  activeRules: number;
  monitoredAssets: number;
  gateways: number | null;
  insideNow: number | null;
  activeAlerts: number;
  lastEventAt: string | null;
  createdBy: string | null;
  lastModifiedAt: string | null;
  latestOpenAlert: { alertId: string; eventType: string; assetName: string; raisedAt: string } | null;
}

export interface GeofenceSummary { generatedAt: string; kpis: GeofenceKpis; targets: GeofenceTarget[] }

interface EventDto {
  alertId: string; raisedAt: string; assetId: string | null; assetName: string; deviceId: string | null; siteName: string | null;
  targetId: string | null; targetName: string | null; eventType: string; severity: string; alertSeverity: string; status: string;
  message: string | null; ruleId: string | null; ruleName: string | null; durationSeconds: number | null; assignedTo: string | null;
  owner: string | null; escalatedAt: string | null; escalatedTo: string | null;
  history: { at: string; by: string | null; action: string; note: string | null }[];
}

interface EventPageDto { items: EventDto[]; total: number; page: number; pageSize: number; totalPages: number; eventTypes: string[] }

export interface GeofenceEventPage { items: AlertEvent[]; total: number; page: number; pageSize: number; totalPages: number; eventTypes: string[] }

export interface GeofenceEventQuery {
  page: number;
  pageSize: number;
  from?: string;
  to?: string;
  targetId?: string;
  eventType?: string;
  /** An API status, or 'Unresolved'. */
  status?: string;
  severity?: string;
  search?: string;
}

function toEvent(d: EventDto): AlertEvent {
  return {
    id: d.alertId,
    ts: new Date(d.raisedAt),
    assetCode: d.assetId || d.assetName,
    assetSub: d.assetId && d.assetId !== d.assetName ? d.assetName : '',
    zoneId: d.targetId ?? '',
    zoneName: d.targetName ?? '—',
    type: d.eventType,
    severity: d.severity,
    message: d.message ?? '',
    owner: d.owner || '—',
    status: statusFromApi(d.status),
    ruleId: d.ruleId ?? undefined,
    ruleName: d.ruleName ?? undefined,
    durationSec: d.durationSeconds,
    assignedTo: d.assignedTo ?? undefined,
    escalatedTo: d.escalatedAt ? d.escalatedTo ?? 'Escalation group' : undefined,
    deviceId: d.deviceId ?? undefined,
    site: d.siteName ?? undefined,
    history: (d.history ?? []).map((h) => ({ at: new Date(h.at), by: h.by ?? '—', action: h.action, note: h.note ?? '' })),
  };
}

@Injectable({ providedIn: 'root' })
export class GeofenceSummaryApi {
  private readonly http = inject(HttpClient);

  /** todayFrom: the viewer's local midnight, so "Alerts Today" follows their day. */
  summary(todayFrom: Date): Observable<GeofenceSummary> {
    return this.http.get<GeofenceSummary>(trackingUrl('geofence-alerts/summary'), { params: tenantParams({ todayFrom: todayFrom.toISOString() }) })
      .pipe(catchError((err) => apiError(err, 'Could not load the geofence summary.')));
  }

  events(q: GeofenceEventQuery): Observable<GeofenceEventPage> {
    return this.http.get<EventPageDto>(trackingUrl('geofence-alerts/events'), { params: tenantParams({ ...q }) }).pipe(
      map((p) => ({ ...p, items: p.items.map(toEvent) })),
      catchError((err) => apiError(err, 'Could not load alert events.')),
    );
  }
}
