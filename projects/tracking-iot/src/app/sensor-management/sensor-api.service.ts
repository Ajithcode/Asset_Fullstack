import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { apiError } from '../geofence-alerts/geofence-api.service';
import { NOW, PARAMETERS, Sensor, SensorStatus, SensorType, paramOf } from './sensor-data';

type ApiStatus = 'Active' | 'Offline' | 'Attention' | 'Inactive';

const DAY = 86_400_000;

/** A sensor as the API stores it ({trackingApiUrl}api/sensors, addressed by sensorId). */
export interface SensorDto {
  id: string;
  sensorId: string;
  name: string;
  sensorType: string;
  measuredParameter: string;
  unit: string | null;
  deviceId: string | null;
  deviceModel: string | null;
  assetRegistrationId: string | null;
  assetName: string | null;
  measurementPoint: string | null;
  liveReading: number | null;
  calibrationDueDate: string | null;
  status: ApiStatus | null;
  createdAt: string | null;
}

/** Request body for POST and PUT /api/sensors (same shape; sensorId cannot change on PUT). */
interface SensorRequest {
  sensorId: string;
  name: string;
  sensorType: string;
  measuredParameter: string;
  unit: string | null;
  deviceId: string | null;
  deviceModel: string | null;
  assetRegistrationId: string | null;
  assetName: string | null;
  measurementPoint: string | null;
  liveReading: number | null;
  calibrationDueDate: string | null;
  status: ApiStatus;
  createdBy: string;
  clientId: string;
  tenantId: string;
}

/** The page's sensor type for the API's free-text type ("Temperature", "Vibration Sensor", ...), else by parameter. */
export function typeOf(sensorType: string, parameter = ''): SensorType {
  const t = sensorType.toLowerCase();
  const byType = PARAMETERS.find((p) => t.includes(p.name.toLowerCase().split(' ').pop()!) || t === p.type.toLowerCase());
  if (byType) return byType.type;
  const q = parameter.toLowerCase();
  return PARAMETERS.find((p) => q.includes(p.name.toLowerCase().split(' ').pop()!))?.type ?? 'Temperature Probe';
}

/** Category of an asset name for the Asset filter: "Cooling Pump 1" → "Cooling Pump". */
export function assetKindOf(name: string): string {
  const parts = name.trim().split(/\s+/);
  return parts.length > 1 ? parts.slice(0, -1).join(' ') : 'Asset';
}

/** Local 09:00 on the date part of an ISO date, as the page keeps calibration dates. */
function localDate(iso: string): number {
  return new Date(iso.slice(0, 10) + 'T09:00').getTime();
}

function isoDate(ms: number): string {
  const d = new Date(ms);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function seedOf(id: string): number {
  let h = 0;
  for (const c of id) h = (h * 31 + c.charCodeAt(0)) >>> 0;
  return h % 100_000 || 1;
}

/**
 * A page sensor for an API sensor. The API keeps no serial, channel, site, sampling interval, alarm thresholds or
 * calibration certificate: these take the sensor type's defaults (thresholds = the type's range).
 */
export function sensorFromDto(dto: SensorDto): Sensor {
  const type = typeOf(dto.sensorType, dto.measuredParameter);
  const p = paramOf(type);
  const calDue = dto.calibrationDueDate ? localDate(dto.calibrationDueDate) : NOW + 365 * DAY;
  const assetName = dto.assetName ?? dto.assetRegistrationId ?? '';
  return {
    id: dto.sensorId,
    name: dto.name,
    type,
    param: { ...p, label: dto.measuredParameter, unit: dto.unit ?? p.unit },
    serial: '',
    deviceId: dto.deviceId ?? '',
    deviceModel: dto.deviceModel ?? '',
    channel: 1,
    assetId: dto.assetRegistrationId ?? '',
    assetName,
    assetKind: assetKindOf(assetName),
    point: dto.measurementPoint ?? '',
    site: '',
    intervalSec: 60,
    low: p.range[0],
    high: p.range[1],
    reading: dto.liveReading,
    status: dto.status === 'Inactive' ? 'Maintenance' : dto.status ?? 'Active',
    lastSeenMin: 0,
    installedOn: dto.createdAt ? Date.parse(dto.createdAt) : Date.now(),
    lastCal: calDue - 365 * DAY,
    calDue,
    certificate: '',
    seed: seedOf(dto.sensorId),
    events: [],
    api: { sensorType: dto.sensorType },
  };
}

function apiStatus(status: SensorStatus): ApiStatus {
  return status === 'Maintenance' ? 'Inactive' : status;
}

@Injectable({ providedIn: 'root' })
export class SensorApiService {
  private apiUrl = `${environment.trackingApiUrl}api/sensors`;

  constructor(private http: HttpClient, private auth: AuthService) {}

  /** GET /api/sensors */
  getAll(): Observable<SensorDto[]> {
    return this.http.get<SensorDto[]>(this.apiUrl).pipe(catchError((e) => apiError(e, 'Could not load sensors.')));
  }

  /** GET /api/sensors/{sensorId} */
  getBySensorId(sensorId: string): Observable<SensorDto> {
    return this.http.get<SensorDto>(`${this.apiUrl}/${encodeURIComponent(sensorId)}`).pipe(catchError((e) => apiError(e, 'Could not load the sensor.')));
  }

  /** POST /api/sensors */
  create(s: Sensor): Observable<SensorDto> {
    return this.http.post<SensorDto>(this.apiUrl, this.request(s)).pipe(catchError((e) => apiError(e, `Could not add ${s.id}.`)));
  }

  /** PUT /api/sensors/{sensorId} (full replace; the sensor id itself cannot change) */
  update(s: Sensor): Observable<SensorDto> {
    return this.http.put<SensorDto>(`${this.apiUrl}/${encodeURIComponent(s.id)}`, this.request(s)).pipe(catchError((e) => apiError(e, `Could not save ${s.id}.`)));
  }

  /** DELETE /api/sensors/{sensorId} */
  delete(sensorId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${encodeURIComponent(sensorId)}`).pipe(catchError((e) => apiError(e, `Could not delete ${sensorId}.`)));
  }

  private request(s: Sensor): SensorRequest {
    return {
      sensorId: s.id,
      name: s.name || s.type,
      // keep the API's own type text while the page type is unchanged, else the parameter name (e.g. "Temperature")
      sensorType: s.api && typeOf(s.api.sensorType, s.param.label) === s.type ? s.api.sensorType : paramOf(s.type).name,
      measuredParameter: s.param.label,
      unit: s.param.unit || null,
      deviceId: s.deviceId || null,
      deviceModel: s.deviceModel || null,
      assetRegistrationId: s.assetId || null,
      assetName: s.assetName || null,
      measurementPoint: s.point || null,
      liveReading: s.reading,
      // the API keeps the date only, as UTC midnight
      calibrationDueDate: `${isoDate(s.calDue)}T00:00:00Z`,
      status: apiStatus(s.status),
      createdBy: this.auth.getUserEmail() || 'unknown',
      clientId: environment.clientId,
      tenantId: environment.tenantId,
    };
  }
}
