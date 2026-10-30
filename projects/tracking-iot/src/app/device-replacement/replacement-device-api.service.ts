import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { apiError } from '../geofence-alerts/geofence-api.service';
import { Availability, Candidate, Compat, DeviceKind } from './replacement-data';

/** A spare device as the API stores it ({trackingApiUrl}api/replacement-devices, addressed by deviceId). */
export interface ReplacementDeviceDto {
  id: string;
  deviceId: string;
  model: string;
  availability: Availability | null;
  compatibility: Compat;
  protocol: string | null;
  batteryLevel: number | null;
  locationName: string;
}

/** Request body for POST and PUT /api/replacement-devices (same shape; deviceId cannot change on PUT). */
interface ReplacementDeviceRequest {
  deviceId: string;
  model: string;
  availability: Availability;
  compatibility: Compat;
  protocol: string | null;
  batteryLevel: number | null;
  locationName: string;
  createdBy: string;
  clientId: string;
  tenantId: string;
}

/** The device family for a model / protocol the API sends, or null when it cannot be told. */
export function kindOf(model: string, protocol: string): DeviceKind | null {
  const t = `${model} ${protocol}`.toLowerCase();
  if (/beacon|minew|\bble\b|bluetooth/.test(t)) return 'beacon';
  if (/vib|\bvx-/.test(t)) return 'vibration';
  if (/temp|thermo|humid/.test(t)) return 'temperature';
  if (/gps|gnss|teltonika|\bfm[a-z]?\d|lte|gsm/.test(t)) return 'gps';
  return null;
}

/**
 * A page candidate for an API spare. The API keeps no scan tag, sensor channels, gateway signal or firmware
 * profiles: these are empty / "Unknown", and the compatibility checks report them as not recorded.
 */
export function candidateFromDto(dto: ReplacementDeviceDto): Candidate {
  const protocol = dto.protocol ?? '';
  return {
    id: dto.deviceId,
    availability: dto.availability ?? 'Available',
    kind: kindOf(dto.model, protocol),
    model: dto.model,
    protocol,
    battery: dto.batteryLevel,
    location: dto.locationName,
    tag: '',
    channels: [],
    signal: 'Unknown',
    firmwareProfiles: [],
    recordedCompat: dto.compatibility,
  };
}

@Injectable({ providedIn: 'root' })
export class ReplacementDeviceApiService {
  private apiUrl = `${environment.trackingApiUrl}api/replacement-devices`;

  constructor(private http: HttpClient, private auth: AuthService) {}

  /** GET /api/replacement-devices */
  getAll(): Observable<ReplacementDeviceDto[]> {
    return this.http.get<ReplacementDeviceDto[]>(this.apiUrl).pipe(catchError((e) => apiError(e, 'Could not load replacement devices.')));
  }

  /** GET /api/replacement-devices/{deviceId} */
  getByDeviceId(deviceId: string): Observable<ReplacementDeviceDto> {
    return this.http.get<ReplacementDeviceDto>(`${this.apiUrl}/${encodeURIComponent(deviceId)}`).pipe(catchError((e) => apiError(e, 'Could not load the replacement device.')));
  }

  /** POST /api/replacement-devices */
  create(c: Candidate, compatibility: Compat): Observable<ReplacementDeviceDto> {
    return this.http.post<ReplacementDeviceDto>(this.apiUrl, this.request(c, compatibility)).pipe(catchError((e) => apiError(e, `Could not add ${c.id}.`)));
  }

  /** PUT /api/replacement-devices/{deviceId} (full replace; the device id itself cannot change) */
  update(c: Candidate, compatibility: Compat): Observable<ReplacementDeviceDto> {
    return this.http.put<ReplacementDeviceDto>(`${this.apiUrl}/${encodeURIComponent(c.id)}`, this.request(c, compatibility)).pipe(catchError((e) => apiError(e, `Could not save ${c.id}.`)));
  }

  /** DELETE /api/replacement-devices/{deviceId} */
  delete(deviceId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${encodeURIComponent(deviceId)}`).pipe(catchError((e) => apiError(e, `Could not delete ${deviceId}.`)));
  }

  private request(c: Candidate, compatibility: Compat): ReplacementDeviceRequest {
    return {
      deviceId: c.id,
      model: c.model,
      availability: c.availability,
      compatibility,
      protocol: c.protocol || null,
      batteryLevel: c.battery,
      locationName: c.location,
      createdBy: this.auth.getUserEmail() || 'unknown',
      clientId: environment.clientId,
      tenantId: environment.tenantId,
    };
  }
}
