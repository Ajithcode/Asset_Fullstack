import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { apiError } from '../geofence-alerts/geofence-api.service';
import { Device, DeviceHealthRecord, MODEL_INFO, NOW, SITES, Status, protocolFor, typeFor } from './health-data';

/** Editable fields of a health record (POST and PUT body without the audit fields). */
export type DeviceHealthFields = Omit<DeviceHealthRecord, 'id' | 'createdAt'>;

interface DeviceHealthRequest extends DeviceHealthFields {
  createdBy: string;
  clientId: string;
  tenantId: string;
}

/** Small stable number from the device id, so its charts look the same on every visit. */
function seedOf(id: string): number {
  let h = 2166136261;
  for (const c of id) h = Math.imul(h ^ c.charCodeAt(0), 16777619);
  return h >>> 0;
}

/**
 * The page's device for an API health record. The page's "Warning" is the API's "Attention"; a device the API
 * reports as offline (and not critical or in maintenance) shows as Offline. Firmware and reporting interval
 * come from the model when the page knows it.
 */
export function deviceFromRecord(r: DeviceHealthRecord): Device {
  const type = typeFor(r.deviceModel, r.deviceId);
  const info = MODEL_INFO[r.deviceModel];
  const lastSeenMs = r.lastSeenAt ? Date.parse(r.lastSeenAt) : NaN;
  const health = r.health ?? 'Healthy';
  const status: Status =
    health === 'Maintenance' || health === 'Critical' ? health
      : r.connectivity === 'Offline' ? 'Offline'
        : health === 'Attention' ? 'Warning' : 'Healthy';
  const gateway = r.gateway ?? '';
  return {
    id: r.deviceId,
    model: r.deviceModel,
    type,
    asset: r.assetRegistrationId ? { code: r.assetRegistrationId, name: r.assetName || r.assetRegistrationId, kind: 'box' } : null,
    site: r.siteName || SITES.find((s) => s.gateways.includes(gateway))?.name || '—',
    gateway,
    conn: r.connectivity,
    rssi: r.signalStrengthDbm ?? -70,
    battery: r.powerSource === 'Battery' ? r.batteryLevel : null,
    power: r.powerSource === 'External power' ? 'External' : r.powerSource,
    lastSeenMin: isNaN(lastSeenMs) ? 0 : Math.max(0, Math.round((NOW.getTime() - lastSeenMs) / 60000)),
    status,
    // the API does not say when the status began; 0 keeps every record inside the page's time window
    sinceMin: 0,
    blip: null,
    firmware: info?.fw ?? '—',
    latestFirmware: info?.latest ?? '—',
    protocol: protocolFor(type),
    intervalMin: info?.interval ?? 10,
    seed: seedOf(r.deviceId),
    api: r,
  };
}

@Injectable({ providedIn: 'root' })
export class DeviceHealthApiService {
  private apiUrl = `${environment.trackingApiUrl}api/device-health`;

  constructor(private http: HttpClient, private auth: AuthService) {}

  /** GET /api/device-health */
  getAll(): Observable<DeviceHealthRecord[]> {
    return this.http.get<DeviceHealthRecord[]>(this.apiUrl).pipe(catchError((e) => apiError(e, 'Could not load device health.')));
  }

  /** GET /api/device-health/{id} */
  getById(id: string): Observable<DeviceHealthRecord> {
    return this.http.get<DeviceHealthRecord>(`${this.apiUrl}/${id}`).pipe(catchError((e) => apiError(e, 'Could not load the health record.')));
  }

  /** POST /api/device-health (one record per device; the API refuses a second one) */
  create(fields: DeviceHealthFields): Observable<DeviceHealthRecord> {
    return this.http.post<DeviceHealthRecord>(this.apiUrl, this.request(fields)).pipe(catchError((e) => apiError(e, `Could not add health for ${fields.deviceId}.`)));
  }

  /** PUT /api/device-health/{id} (full replace) */
  update(id: string, fields: DeviceHealthFields): Observable<DeviceHealthRecord> {
    return this.http.put<DeviceHealthRecord>(`${this.apiUrl}/${id}`, this.request(fields)).pipe(catchError((e) => apiError(e, `Could not update ${fields.deviceId}.`)));
  }

  /** DELETE /api/device-health/{id} */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`).pipe(catchError((e) => apiError(e, 'Could not delete the health record.')));
  }

  private request(f: DeviceHealthFields): DeviceHealthRequest {
    return {
      ...f,
      // the API needs a level only for battery-powered devices
      batteryLevel: f.powerSource === 'Battery' ? f.batteryLevel : null,
      createdBy: this.auth.getUserEmail() || 'unknown',
      clientId: environment.clientId,
      tenantId: environment.tenantId,
    };
  }
}
