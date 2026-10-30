import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { apiError } from '../geofence-alerts/geofence-api.service';
import { Conn, Device, DeviceType, Health, MODEL_FW, NOW, SITES, TYPE_SPEC } from './device-data';

type ApiConnectivity = 'Online' | 'Offline' | 'Warning';
type ApiPower = 'Battery' | 'External power' | 'PoE';

/** An IoT device as the API stores it ({trackingApiUrl}api/iot-devices, addressed by deviceId). */
export interface IoTDeviceDto {
  id: string;
  deviceId: string;
  deviceName: string;
  deviceType: string;
  model: string;
  connectivity: ApiConnectivity;
  assetRegistrationId: string | null;
  assetName: string | null;
  gateway: string | null;
  powerSource: ApiPower;
  batteryLevel: number | null;
  lastSeenAt: string | null;
  health: Health | null;
  createdAt: string | null;
}

/** Request body for POST and PUT /api/iot-devices (same shape for both; deviceId cannot change on PUT). */
interface IoTDeviceRequest {
  deviceId: string;
  deviceName: string;
  deviceType: string;
  model: string;
  connectivity: ApiConnectivity;
  assetRegistrationId: string | null;
  assetName: string | null;
  gateway: string | null;
  powerSource: ApiPower;
  batteryLevel: number | null;
  lastSeenAt: string | null;
  health: Health;
  createdBy: string;
  clientId: string;
  tenantId: string;
}

/** The page's device type for the API's free-text type, e.g. "Vibration Sensor Node" → Vibration Sensor. */
function pageType(text: string): DeviceType {
  const t = text.toLowerCase();
  if (t.includes('gps') || t.includes('cellular')) return 'GPS / Cellular';
  if (t.includes('temp')) return 'Temperature Sensor';
  if (t.includes('vibration')) return 'Vibration Sensor';
  if (t.includes('rfid')) return 'RFID Reader';
  return 'BLE Tracker';
}

/** Small stable number from the device id, so its charts look the same on every visit. */
function seedOf(id: string): number {
  let h = 2166136261;
  for (const c of id) h = Math.imul(h ^ c.charCodeAt(0), 16777619);
  return h >>> 0;
}

/**
 * A page device for an API device. Fields the API does not store (serial, protocol, firmware, signal, settings)
 * get the defaults of its type; the site comes from its gateway.
 */
export function deviceFromDto(dto: IoTDeviceDto): Device {
  const type = pageType(dto.deviceType);
  const spec = TYPE_SPEC[type];
  const lastSeenMs = dto.lastSeenAt ? Date.parse(dto.lastSeenAt) : NaN;
  const neverSeen = isNaN(lastSeenMs);
  // a device that is offline and has never reported is one that was just registered
  const conn: Conn = neverSeen && dto.connectivity === 'Offline' ? 'Pending' : dto.connectivity;
  const gateway = dto.gateway ?? '';
  const createdMs = dto.createdAt ? Date.parse(dto.createdAt) : NaN;
  return {
    id: dto.deviceId,
    name: dto.deviceName,
    type,
    model: dto.model,
    serial: '',
    imei: '',
    protocol: spec.protocol,
    conn,
    health: dto.health ?? 'Good',
    asset: dto.assetRegistrationId ? { code: dto.assetRegistrationId, name: dto.assetName || dto.assetRegistrationId } : null,
    site: SITES.find((s) => s.gateways.includes(gateway))?.name ?? '—',
    gateway,
    power: dto.powerSource === 'External power' ? 'External' : dto.powerSource,
    battery: dto.powerSource === 'Battery' ? dto.batteryLevel : null,
    rssi: -70,
    lastSeenSec: neverSeen ? null : Math.max(0, Math.round((NOW.getTime() - lastSeenMs) / 1000)),
    firmware: MODEL_FW[dto.model] ?? '—',
    installedDaysAgo: isNaN(createdMs) ? 0 : Math.max(0, Math.floor((NOW.getTime() - createdMs) / 86400000)),
    settings: { intervalMin: spec.interval, lowBattery: 20, offlineAfterMin: Math.max(15, spec.interval * 3), alerts: true },
    log: [],
    seed: seedOf(dto.deviceId),
    api: { deviceType: dto.deviceType, lastSeenAt: dto.lastSeenAt },
  };
}

@Injectable({ providedIn: 'root' })
export class IoTDeviceApiService {
  private apiUrl = `${environment.trackingApiUrl}api/iot-devices`;

  constructor(private http: HttpClient, private auth: AuthService) {}

  /** GET /api/iot-devices */
  getAll(): Observable<IoTDeviceDto[]> {
    return this.http.get<IoTDeviceDto[]>(this.apiUrl).pipe(catchError((e) => apiError(e, 'Could not load IoT devices.')));
  }

  /** GET /api/iot-devices/{deviceId} */
  getById(deviceId: string): Observable<IoTDeviceDto> {
    return this.http.get<IoTDeviceDto>(`${this.apiUrl}/${encodeURIComponent(deviceId)}`).pipe(catchError((e) => apiError(e, 'Could not load the device.')));
  }

  /** POST /api/iot-devices */
  create(d: Device): Observable<IoTDeviceDto> {
    return this.http.post<IoTDeviceDto>(this.apiUrl, this.request(d)).pipe(catchError((e) => apiError(e, `Could not register ${d.id}.`)));
  }

  /** PUT /api/iot-devices/{deviceId} (full replace; the device id itself cannot change) */
  update(d: Device): Observable<IoTDeviceDto> {
    return this.http.put<IoTDeviceDto>(`${this.apiUrl}/${encodeURIComponent(d.id)}`, this.request(d)).pipe(catchError((e) => apiError(e, `Could not update ${d.id}.`)));
  }

  /** DELETE /api/iot-devices/{deviceId} */
  delete(deviceId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${encodeURIComponent(deviceId)}`).pipe(catchError((e) => apiError(e, `Could not delete ${deviceId}.`)));
  }

  private request(d: Device): IoTDeviceRequest {
    const power: ApiPower = d.power === 'External' ? 'External power' : d.power;
    return {
      deviceId: d.id,
      deviceName: d.name,
      // keep the API's own wording for devices that came from it
      deviceType: d.api?.deviceType && pageType(d.api.deviceType) === d.type ? d.api.deviceType : d.type,
      model: d.model,
      // a registered device that has not reported yet is Offline to the API
      connectivity: d.conn === 'Pending' ? 'Offline' : d.conn,
      assetRegistrationId: d.asset?.code ?? null,
      assetName: d.asset?.name ?? null,
      gateway: d.gateway || null,
      powerSource: power,
      batteryLevel: power === 'Battery' ? (d.battery ?? 100) : null,
      lastSeenAt: d.api?.lastSeenAt ?? null,
      health: d.health,
      createdBy: this.auth.getUserEmail() || 'unknown',
      clientId: environment.clientId,
      tenantId: environment.tenantId,
    };
  }
}
