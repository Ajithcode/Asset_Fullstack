import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { EMPTY, Observable, catchError, expand, forkJoin, map, of, reduce } from 'rxjs';
import { apiError, tenantParams, trackingUrl } from '../shared/tracking-api';
import {
  DeviceHealthDto, IotAlertDto, IotDeviceDto, IotEventsTimelineDto, LocationEventDto, MAX_DAYS, MAX_READINGS, TelemetryReadApi, TelemetryReadingDto,
} from '../tracking-telemetry/telemetry-api';

/** GET /api/asset-utilization: one asset's running / idle time on one day. */
export interface AssetUtilizationDto {
  id: string;
  utilizationId: string;
  /** The day (00:00 UTC). */
  date: string;
  assetRegistrationId: string;
  assetName: string | null;
  assetType: string;
  deviceId: string | null;
  siteName: string | null;
  runningMinutes: number;
  idleMinutes: number;
  runningPercent: number;
  idlePercent: number;
  createdBy: string | null;
  createdAt: string | null;
}

export interface AssetTypeUtilizationDto {
  assetType: string;
  assetCount: number;
  runningHours: number;
  idleHours: number;
  runningPercent: number;
  idlePercent: number;
}

/** GET /api/asset-utilization/by-asset-type */
export interface UtilizationByAssetTypeDto {
  days: number;
  from: string;
  to: string;
  overallRunningPercent: number;
  overallIdlePercent: number;
  items: AssetTypeUtilizationDto[];
}

/** Everything Analytics & Insights aggregates, for a window of the last `days` days. */
export interface AnalyticsSource {
  days: number;
  devices: IotDeviceDto[];
  health: DeviceHealthDto[];
  utilization: AssetUtilizationDto[];
  byAssetType: UtilizationByAssetTypeDto;
  alerts: IotAlertDto[];
  readings: TelemetryReadingDto[];
  timeline: IotEventsTimelineDto;
  locationEvents: LocationEventDto[];
  /** Asset type per device id, for devices that come from Real-Time Location tags (no utilization rows). */
  deviceAssetTypes?: Record<string, string>;
  /** True when the events KPI is counted from Real-Time Location events (the legacy telemetry timeline is empty). */
  eventsFromRtl?: boolean;
}

/** GET /api/real-time-location/tags (NetworkTimeSeries: newest reading of every tag, with its zone and asset). */
interface RtlTagDto {
  tagId: string; gatewayId: string | null; gatewayName: string | null; timestamp: string | null; status: string; connection: string;
  rssi: number | null; battery: number | null; siteId: string | null; siteName: string | null;
  asset: { assetRegistrationId: string; assetTag: string | null; assetName: string | null; assetType: string | null } | null;
}

/** GET /api/real-time-location/events */
interface RtlEventDto { eventId: string; at: string; assetId: string; assetName: string; eventType: string; zone: string | null; details: string | null }
interface RtlEventPageDto { items: RtlEventDto[]; total: number }

const RTL_PAGE = 100; // the API's maximum page size

@Injectable({ providedIn: 'root' })
export class AnalyticsApi {
  private readonly http = inject(HttpClient);
  private readonly reads = inject(TelemetryReadApi);

  /** Fills what the legacy collections (device health, location events, timeline) lack from Real-Time Location / NetworkTimeSeries. */
  private withRealTime(src: AnalyticsSource, tags: RtlTagDto[], moves: LocationEventDto[]): AnalyticsSource {
    const out: AnalyticsSource = { ...src, deviceAssetTypes: {} };
    const known = new Set(src.devices.map((x) => x.deviceId));
    const healthed = new Set(src.health.map((h) => h.deviceId));
    for (const t of tags) {
      const stale = !t.timestamp || Date.now() - new Date(t.timestamp).getTime() > 10 * 60_000;
      const offline = t.status !== 'online' || stale;
      if (!known.has(t.tagId)) {
        out.devices = [...out.devices, {
          id: t.tagId, deviceId: t.tagId, deviceName: `Tag ${t.tagId}`, deviceType: 'Tag', model: '', serialNumber: null, connectivity: 'BLE',
          siteId: t.siteId, siteName: t.siteName, assetRegistrationId: t.asset?.assetRegistrationId ?? null, assetCode: t.asset?.assetTag ?? null,
          assetName: t.asset?.assetName ?? null, gateway: t.gatewayName ?? t.gatewayId, powerSource: 'Battery', batteryLevel: t.battery,
          lastSeenAt: t.timestamp, health: offline ? 'Critical' : t.battery !== null && t.battery < 20 ? 'Warning' : 'Good', firmwareVersion: null, createdAt: null,
        }];
        known.add(t.tagId);
      }
      if (t.asset?.assetType) out.deviceAssetTypes![t.tagId] = t.asset.assetType;
      if (!healthed.has(t.tagId)) {
        out.health = [...out.health, {
          id: t.tagId, deviceId: t.tagId, deviceModel: '', assetRegistrationId: t.asset?.assetRegistrationId ?? null, assetName: t.asset?.assetName ?? null,
          siteId: t.siteId, siteName: t.siteName, gateway: t.gatewayName ?? t.gatewayId, connectivity: offline ? 'Offline' : t.rssi !== null && t.rssi < -85 ? 'Warning' : 'Online',
          signalStrengthDbm: t.rssi, snrDb: null, signalQuality: null, powerSource: 'Battery', batteryLevel: t.battery, lastSeenAt: t.timestamp,
          health: offline ? 'Critical' : t.battery !== null && t.battery < 20 ? 'Attention' : 'Healthy',
        }];
        healthed.add(t.tagId);
      }
    }
    if (!out.locationEvents.length) out.locationEvents = moves;
    // the legacy timeline has no telemetry / location / geofence counts: count the real events instead
    if (!out.timeline.points.some((p) => p.telemetryData + p.locationUpdates + p.geofenceEvents > 0)) out.eventsFromRtl = true;
    return out;
  }

  utilization(days: number, assetType?: string): Observable<AssetUtilizationDto[]> {
    return this.http
      .get<AssetUtilizationDto[]>(trackingUrl('asset-utilization'), { params: tenantParams({ days, assetType }) })
      .pipe(catchError((err) => apiError(err, 'Could not load asset utilization.')));
  }

  utilizationByAssetType(days: number): Observable<UtilizationByAssetTypeDto> {
    return this.http
      .get<UtilizationByAssetTypeDto>(trackingUrl('asset-utilization/by-asset-type'), { params: tenantParams({ days }) })
      .pipe(catchError((err) => apiError(err, 'Could not load utilization by asset type.')));
  }

  /** Tags heard in the last 24 h (Real-Time Location), mapped to the device / health models of this page. */
  private tags(): Observable<RtlTagDto[]> {
    return this.http
      .get<{ tags: RtlTagDto[] }>(trackingUrl('real-time-location/tags'), { params: tenantParams({ minutes: 7 * 24 * 60 }) })
      .pipe(map((r) => r?.tags ?? []), catchError(() => of([] as RtlTagDto[])));
  }

  /** Zone entries / exits (Real-Time Location events, 7 days at most) in the shape of location events. */
  private rtlMoves(): Observable<LocationEventDto[]> {
    const page = (n: number) => this.http.get<RtlEventPageDto>(trackingUrl('real-time-location/events'), { params: tenantParams({ range: '7d', page: n, pageSize: RTL_PAGE }) });
    return page(1).pipe(
      expand((r, i) => (i < 29 && (i + 1) * RTL_PAGE < r.total ? page(i + 2) : EMPTY)),
      map((r) => r.items),
      reduce((all, rows) => all.concat(rows), [] as RtlEventDto[]),
      map((rows) =>
        rows
          .filter((e) => e.eventType === 'Zone Entry' || e.eventType === 'Zone Exit' || e.eventType === 'Floor Change')
          .map((e): LocationEventDto => ({
            id: e.eventId, eventId: e.eventId, eventTime: e.at, assetId: e.assetId, assetName: e.assetName,
            eventType: e.eventType === 'Zone Exit' ? 'Departed Zone' : 'Entered Zone', zoneName: e.zone ?? '', details: e.details, createdBy: null, createdAt: null,
          })),
      ),
      catchError(() => of([] as LocationEventDto[])),
    );
  }

  /**
   * Loads the analytics data for the last `days` days (the API windows are whole UTC days ending today).
   * Alerts and the events timeline cover twice that, for the change vs the previous period.
   */
  load(days: number): Observable<AnalyticsSource> {
    const d = Math.min(MAX_DAYS, Math.max(1, days));
    const d2 = Math.min(MAX_DAYS, d * 2);
    return forkJoin({
      days: of(d),
      devices: this.reads.devices(),
      health: this.reads.deviceHealth(),
      utilization: this.utilization(d),
      byAssetType: this.utilizationByAssetType(d),
      alerts: this.reads.alerts({ days: d2 }),
      readings: this.reads.readings({ days: d, limit: MAX_READINGS }),
      timeline: this.reads.timeline(d2),
      locationEvents: this.reads.locationEvents(),
      tags: this.tags(),
      moves: this.rtlMoves(),
    }).pipe(map(({ tags, moves, ...src }) => this.withRealTime(src, tags, moves)));
  }
}
