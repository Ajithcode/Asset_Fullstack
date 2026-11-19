import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError } from 'rxjs';
import { apiError, tenantParams, trackingUrl } from '../shared/tracking-api';

/*
 * Tracking & Telemetry page data (Tracking service, GET /api/tracking-telemetry/...), built by the backend from the
 * live tag readings (NetworkTimeSeries), Tag Management, Asset Registration, IoT devices, the gateways plotted on the
 * floor plans and the IoT alerts. One row per ASSET (an asset can have several tags).
 */

export type RangeKey = '1h' | '24h' | '7d';
export type Connectivity = 'Online' | 'Idle' | 'Offline';

export interface RangeDto { key: RangeKey; label: string; from: string; to: string }

export interface TelemetryKpis {
  /** Unique assets with at least one linked tag. */
  trackedAssets: number;
  /** Gateways / IoT devices plus tags (assets and devices are not one-to-one). */
  devices: { registered: number; online: number; gateways: number; gatewaysOnline: number; tags: number; tagsOnline: number };
  alerts: { active: number; critical: number; warning: number; info: number };
  /** points = measured values (RSSI, battery…); messages = gateway reports. */
  telemetry: { messages: number; points: number; previousPoints: number; changePercent: number | null; period: string; definition: string };
}

export interface AssetTag { tagId: string; tagType: string | null; technology: string | null; lastSeenAt: string | null; battery: number | null; rssi: number | null; gatewayId: string | null; connectivity: Connectivity }

/** Estimated from the gateway hearing the tag strongest (BLE has no GPS). */
export interface AssetLocation {
  gatewayId: string;
  gatewayName: string | null;
  zoneId: string | null;
  zoneName: string | null;
  floorName: string | null;
  buildingName: string | null;
  siteId: string | null;
  siteName: string | null;
  floorPlanId: string | null;
  locationPath: string | null;
  /** Estimated or Measured. */
  method: string;
  basis: string;
  /** Zone level / Gateway range. */
  accuracy: string;
  at: string | null;
}

export interface TrackedAsset {
  assetId: string;
  assetTag: string | null;
  name: string;
  assetType: string | null;
  category: string | null;
  operationalStatus: string | null;
  condition: string | null;
  siteName: string | null;
  technology: string;
  tags: AssetTag[];
  connectivity: Connectivity;
  lastSeenAt: string | null;
  location: AssetLocation | null;
  lastLocationAt: string | null;
  lastTelemetryAt: string | null;
  battery: number | null;
  rssi: number | null;
  activeAlerts: number;
  /** Condition of the asset itself (Asset Registration). */
  assetHealth: string | null;
  /** Health of its tracking device(s): connectivity and battery. */
  deviceHealth: 'Healthy' | 'Attention' | 'Critical';
  siteId: string | null;
}

export interface Gateway { deviceId: string; name: string | null; type: string | null; model: string | null; connectivity: Connectivity; lastSeenAt: string | null; tagsHeard: number; zoneName: string | null; floorName: string | null; floorPlanId: string | null; plotted: boolean; siteId: string | null }

export interface TelemetryEvent {
  eventId: string;
  at: string;
  /** Telemetry, Location, Connectivity or Device. */
  category: string;
  event: string;
  assetId: string | null;
  assetName: string | null;
  sourceDevice: string | null;
  reading: string | null;
  severity: string;
  /** Open / Acknowledged / In Progress / Resolved for alerts; Recorded for location changes. */
  status: string;
  acknowledgedAt: string | null;
  acknowledgedBy: string | null;
}

export interface TelemetryOverview { generatedAt: string; range: RangeDto; kpis: TelemetryKpis; assets: TrackedAsset[]; gateways: Gateway[]; events: TelemetryEvent[] }

/** GET /api/tracking-telemetry/events: one page of the events table, newest first. */
export interface EventPage { items: TelemetryEvent[]; total: number; page: number; pageSize: number; totalPages: number; range: RangeDto }

export interface TrendPoint { at: string; avg: number; min: number; max: number; count: number }

export interface MetricSeries {
  key: string;
  label: string;
  unit: string;
  stepMinutes: number;
  points: TrendPoint[];
  latestValue: number | null;
  latestAt: string | null;
  /** Tag that reported the latest value. */
  source: string | null;
  /** Normal, Warning or Stale. */
  status: string;
  threshold: number | null;
  thresholdLabel: string | null;
}

export interface AssetTrend { assetId: string; range: RangeDto; metrics: MetricSeries[] }

@Injectable({ providedIn: 'root' })
export class TelemetryOverviewApi {
  private readonly http = inject(HttpClient);

  overview(range: RangeKey, siteId: string): Observable<TelemetryOverview> {
    return this.http
      .get<TelemetryOverview>(trackingUrl('tracking-telemetry/overview'), { params: tenantParams({ range, siteId }) })
      .pipe(catchError((err) => apiError(err, 'Could not load tracking & telemetry data.')));
  }

  /** One page of events; `assetIds` limits them to the assets matching the page filters. */
  events(range: RangeKey, siteId: string, page: number, pageSize: number, assetIds: string[] = [], category = ''): Observable<EventPage> {
    return this.http
      .get<EventPage>(trackingUrl('tracking-telemetry/events'), {
        params: tenantParams({ range, siteId, page, pageSize, assetIds: assetIds.join(','), category }),
      })
      .pipe(catchError((err) => apiError(err, 'Could not load events.')));
  }

  trend(assetId: string, range: RangeKey): Observable<AssetTrend> {
    return this.http
      .get<AssetTrend>(trackingUrl(`tracking-telemetry/assets/${encodeURIComponent(assetId)}/trend`), { params: tenantParams({ range }) })
      .pipe(catchError((err) => apiError(err, 'Could not load the telemetry trend.')));
  }
}
