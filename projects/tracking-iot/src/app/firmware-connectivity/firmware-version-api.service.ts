import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { apiError } from '../geofence-alerts/geofence-api.service';
import { DeviceModel, ModelKind } from './firmware-data';

type ApiStatus = 'Up to date' | 'Update available';

/** A row of Firmware Versions by Device Model as the API stores it ({trackingApiUrl}api/firmware-versions/{id}). */
export interface FirmwareVersionDto {
  id: string;
  deviceModel: string;
  currentVersion: string;
  latestVersion: string;
  deviceCount: number;
  status: ApiStatus | null;
  releaseDate: string | null;
}

/** Request body for POST and PUT /api/firmware-versions (same shape). */
interface FirmwareVersionRequest {
  deviceModel: string;
  currentVersion: string;
  latestVersion: string;
  deviceCount: number;
  status: ApiStatus;
  releaseDate: string | null;
  createdBy: string;
  clientId: string;
  tenantId: string;
}

/** The icon kind for a model name the API sends (the API has no vendor or kind). */
export function kindOf(name: string): ModelKind {
  const n = name.toLowerCase();
  if (n.includes('beacon')) return 'beacon';
  if (n.includes('tag')) return 'tag';
  if (n.includes('vib') || n.startsWith('vx')) return 'vibration';
  if (/track|fm[a-z]?\d|tmt|gps/.test(n)) return 'tracker';
  return 'sensor';
}

/**
 * A page row for an API row. The API keeps no vendor, release notes or online count: the vendor is the model
 * name's first word and every device counts as online (the connectivity figures come from the gateways).
 */
export function modelFromDto(dto: FirmwareVersionDto): DeviceModel {
  return {
    id: `fw-${dto.id}`,
    apiId: dto.id,
    name: dto.deviceModel,
    vendor: dto.deviceModel.split(/\s+/)[0] ?? '',
    kind: kindOf(dto.deviceModel),
    current: dto.currentVersion,
    latest: dto.latestVersion,
    devices: dto.deviceCount,
    online: dto.deviceCount,
    releaseDate: dto.releaseDate?.slice(0, 10) ?? '',
    notes: [],
  };
}

@Injectable({ providedIn: 'root' })
export class FirmwareVersionApiService {
  private apiUrl = `${environment.trackingApiUrl}api/firmware-versions`;

  constructor(private http: HttpClient, private auth: AuthService) {}

  /** GET /api/firmware-versions */
  getAll(): Observable<FirmwareVersionDto[]> {
    return this.http.get<FirmwareVersionDto[]>(this.apiUrl).pipe(catchError((e) => apiError(e, 'Could not load firmware versions.')));
  }

  /** GET /api/firmware-versions/{id} */
  getById(id: string): Observable<FirmwareVersionDto> {
    return this.http.get<FirmwareVersionDto>(`${this.apiUrl}/${encodeURIComponent(id)}`).pipe(catchError((e) => apiError(e, 'Could not load the firmware version.')));
  }

  /** POST /api/firmware-versions */
  create(m: DeviceModel): Observable<FirmwareVersionDto> {
    return this.http.post<FirmwareVersionDto>(this.apiUrl, this.request(m)).pipe(catchError((e) => apiError(e, `Could not add ${m.name}.`)));
  }

  /** PUT /api/firmware-versions/{id} (full replace) */
  update(id: string, m: DeviceModel): Observable<FirmwareVersionDto> {
    return this.http.put<FirmwareVersionDto>(`${this.apiUrl}/${encodeURIComponent(id)}`, this.request(m)).pipe(catchError((e) => apiError(e, `Could not save ${m.name}.`)));
  }

  /** DELETE /api/firmware-versions/{id} */
  delete(id: string, name: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${encodeURIComponent(id)}`).pipe(catchError((e) => apiError(e, `Could not delete ${name}.`)));
  }

  private request(m: DeviceModel): FirmwareVersionRequest {
    return {
      deviceModel: m.name,
      currentVersion: m.current,
      latestVersion: m.latest,
      deviceCount: m.devices,
      status: m.current.toLowerCase() === m.latest.toLowerCase() ? 'Up to date' : 'Update available',
      // the API keeps the date only, as UTC midnight
      releaseDate: m.releaseDate ? `${m.releaseDate}T00:00:00Z` : null,
      createdBy: this.auth.getUserEmail() || 'unknown',
      clientId: environment.clientId,
      tenantId: environment.tenantId,
    };
  }
}
