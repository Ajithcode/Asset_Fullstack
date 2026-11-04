import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError } from 'rxjs';
import { apiError, tenantParams, trackingUrl } from '../shared/tracking-api';

// ===== DTOs (Tracking service responses; dates are ISO strings in UTC) =====

/** GET /api/iot-devices */
export interface IotDeviceDto {
  id: string;
  deviceId: string;
  deviceName: string;
  deviceType: string;
  model: string;
  serialNumber: string | null;
  connectivity: string;
  siteId: string | null;
  siteName: string | null;
  assetRegistrationId: string | null;
  assetCode: string | null;
  assetName: string | null;
  gateway: string | null;
  powerSource: string;
  batteryLevel: number | null;
  lastSeenAt: string | null;
  /** Good | Warning | Critical */
  health: string | null;
  firmwareVersion: string | null;
  createdAt: string | null;
}

/** GET /api/device-health */
export interface DeviceHealthDto {
  id: string;
  deviceId: string;
  deviceModel: string;
  assetRegistrationId: string | null;
  assetName: string | null;
  siteId: string | null;
  siteName: string | null;
  gateway: string | null;
  /** Online | Offline | Warning */
  connectivity: string;
  signalStrengthDbm: number | null;
  snrDb: number | null;
  signalQuality: string | null;
  powerSource: string;
  batteryLevel: number | null;
  lastSeenAt: string | null;
  /** Healthy | Attention | Critical | Maintenance */
  health: string | null;
}

/** GET /api/telemetry-readings (newest first) */
export interface TelemetryReadingDto {
  id: string;
  readingId: string;
  recordedAt: string;
  deviceId: string;
  sensorId: string | null;
  assetRegistrationId: string | null;
  parameter: string;
  value: number;
  unit: string | null;
  createdAt: string | null;
}

/** GET /api/location-events (newest first) */
export interface LocationEventDto {
  id: string;
  eventId: string;
  eventTime: string;
  assetId: string;
  assetName: string;
  /** Entered Zone | Departed Zone | Geofence Alert | Dwell Exceeded */
  eventType: string;
  zoneName: string;
  details: string | null;
  createdBy: string | null;
  createdAt: string | null;
}

/** GET /api/iot-alerts (most recently raised first) */
export interface IotAlertDto {
  id: string;
  alertId: string;
  raisedAt: string;
  assetRegistrationId: string | null;
  assetName: string;
  deviceId: string | null;
  siteId: string | null;
  siteName: string | null;
  eventType: string;
  /** Critical | Warning | Info */
  severity: string;
  /** Open | Acknowledged | In Progress | Resolved */
  status: string | null;
  message: string | null;
  alertRuleId: string | null;
  acknowledgedBy: string | null;
  acknowledgedAt: string | null;
  resolvedBy: string | null;
  resolvedAt: string | null;
  createdBy: string | null;
  createdAt: string | null;
}

export interface IotEventsTimelinePointDto {
  /** yyyy-MM-dd (UTC day), or "total" */
  date: string;
  telemetryData: number;
  locationUpdates: number;
  geofenceEvents: number;
  alerts: number;
}

/** GET /api/iot-events/timeline */
export interface IotEventsTimelineDto {
  days: number;
  from: string;
  to: string;
  totals: IotEventsTimelinePointDto;
  points: IotEventsTimelinePointDto[];
}

export interface TelemetryQuery {
  /** Last N whole UTC days including today (1–365, default 7 on the server). */
  days?: number;
  deviceId?: string;
  sensorId?: string;
  /** 1–5000, default 500 on the server. */
  limit?: number;
}

export interface AlertQuery {
  /** Last N whole UTC days; all time when omitted. */
  days?: number;
  status?: string;
  severity?: string;
  siteName?: string;
  assetRegistrationId?: string;
  search?: string;
  limit?: number;
}

/** Highest `limit` the telemetry-readings endpoint accepts. */
export const MAX_READINGS = 5000;
/** Longest `days` window the dashboard endpoints accept. */
export const MAX_DAYS = 365;

/** Read-only access to the Tracking service resources shown on Tracking & Telemetry (and reused by Analytics & Insights). */
@Injectable({ providedIn: 'root' })
export class TelemetryReadApi {
  private readonly http = inject(HttpClient);

  devices(): Observable<IotDeviceDto[]> {
    return this.get<IotDeviceDto[]>('iot-devices', {}, 'Could not load IoT devices.');
  }

  deviceHealth(): Observable<DeviceHealthDto[]> {
    return this.get<DeviceHealthDto[]>('device-health', {}, 'Could not load device health.');
  }

  readings(q: TelemetryQuery = {}): Observable<TelemetryReadingDto[]> {
    return this.get<TelemetryReadingDto[]>('telemetry-readings', { ...q }, 'Could not load telemetry readings.');
  }

  locationEvents(): Observable<LocationEventDto[]> {
    return this.get<LocationEventDto[]>('location-events', {}, 'Could not load location events.');
  }

  alerts(q: AlertQuery = {}): Observable<IotAlertDto[]> {
    return this.get<IotAlertDto[]>('iot-alerts', { ...q }, 'Could not load IoT alerts.');
  }

  timeline(days: number): Observable<IotEventsTimelineDto> {
    return this.get<IotEventsTimelineDto>('iot-events/timeline', { days }, 'Could not load the IoT events timeline.');
  }

  private get<T>(resource: string, query: Record<string, string | number | undefined>, fallback: string): Observable<T> {
    return this.http.get<T>(trackingUrl(resource), { params: tenantParams(query) }).pipe(catchError((err) => apiError(err, fallback)));
  }
}

// ===== shared mapping helpers =====

/** Sensor parameter family of a reading ("Temperature", "temp_c", "Vibration RMS", "Battery %", "RSSI" ...). */
export type ParamKind = 'temp' | 'vib' | 'battery' | 'rssi' | 'energy';

export function paramKind(parameter: string | null | undefined): ParamKind | null {
  const p = (parameter ?? '').toLowerCase();
  if (p.includes('temp')) return 'temp';
  if (p.includes('vib')) return 'vib';
  if (p.includes('batt')) return 'battery';
  if (p.includes('rssi') || p.includes('signal')) return 'rssi';
  if (p.includes('energy') || p.includes('kwh') || p.includes('power consumption')) return 'energy';
  return null;
}

/** Epoch ms of an API timestamp; NaN when missing. */
export function timeOf(iso: string | null | undefined): number {
  return iso ? Date.parse(iso) : NaN;
}
