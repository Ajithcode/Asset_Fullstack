import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { apiError } from '../geofence-alerts/geofence-api.service';
import { CONNECTIVITY, Connectivity, DeviceModel, ModelStatus, blankModel, nextModelId } from './model-data';

type ApiStatus = 'Draft' | 'Pending Review' | 'Active' | 'Inactive';
type ApiPower = 'Battery' | 'External power' | 'PoE';

/** A device model as the API stores it ({trackingApiUrl}api/device-models, addressed by modelNumber). */
export interface DeviceModelDto {
  id: string;
  modelNumber: string;
  manufacturer: string;
  deviceType: string;
  connectivity: Connectivity;
  supportedMeasurements: string[];
  powerSource: ApiPower;
  protocol: string | null;
  status: ApiStatus | null;
}

/** Request body for POST and PUT /api/device-models (same shape; modelNumber cannot change on PUT). */
interface DeviceModelRequest {
  modelNumber: string;
  manufacturer: string;
  deviceType: string;
  connectivity: Connectivity;
  supportedMeasurements: string[];
  powerSource: ApiPower;
  protocol: string;
  status: ApiStatus;
  createdBy: string;
  clientId: string;
  tenantId: string;
}

/** The API's power source for the page's detailed power option. */
function apiPower(power: string): ApiPower {
  const p = power.toLowerCase();
  return p.startsWith('battery') ? 'Battery' : p.startsWith('poe') ? 'PoE' : 'External power';
}

/** The API's status for the page's: Draft is shown as "Pending Review" on the page; Retired is Inactive. */
function apiStatus(status: ModelStatus): ApiStatus {
  return status === 'Retired' ? 'Inactive' : status;
}

function pageStatus(status: ApiStatus | null): ModelStatus {
  return status === 'Active' ? 'Active' : status === 'Inactive' ? 'Retired' : 'Draft';
}

/**
 * A page model for an API model, under the page type `typeId`. Fields the API does not store (bands, IP rating,
 * battery life, operating temperature, firmware range, attributes) take the defaults of that type.
 */
export function modelFromDto(dto: DeviceModelDto, typeId: string): DeviceModel {
  const b = blankModel(typeId);
  const pagePowerMatches = apiPower(b.power) === dto.powerSource;
  return {
    ...b,
    id: nextModelId(),
    name: dto.modelNumber,
    manufacturer: dto.manufacturer,
    typeId,
    connectivity: CONNECTIVITY.includes(dto.connectivity) ? [dto.connectivity] : b.connectivity,
    protocol: dto.protocol ?? '',
    measurements: [...dto.supportedMeasurements],
    // keep the type's detailed power option when it is the same kind of power, else the closest one
    power: pagePowerMatches ? b.power : dto.powerSource === 'Battery' ? 'Battery (replaceable)' : dto.powerSource === 'PoE' ? 'PoE (802.3af) / 12 V DC' : 'External 10–30 V DC',
    status: pageStatus(dto.status),
    deployed: 0,
    form: undefined,
    api: { status: dto.status ?? 'Draft', powerSource: dto.powerSource },
  };
}

@Injectable({ providedIn: 'root' })
export class DeviceModelApiService {
  private apiUrl = `${environment.trackingApiUrl}api/device-models`;

  constructor(private http: HttpClient, private auth: AuthService) {}

  /** GET /api/device-models */
  getAll(): Observable<DeviceModelDto[]> {
    return this.http.get<DeviceModelDto[]>(this.apiUrl).pipe(catchError((e) => apiError(e, 'Could not load device models.')));
  }

  /** GET /api/device-models/{modelNumber} */
  getByModelNumber(modelNumber: string): Observable<DeviceModelDto> {
    return this.http.get<DeviceModelDto>(`${this.apiUrl}/${encodeURIComponent(modelNumber)}`).pipe(catchError((e) => apiError(e, 'Could not load the device model.')));
  }

  /** POST /api/device-models. `typeName` is the device type's name. */
  create(m: DeviceModel, typeName: string): Observable<DeviceModelDto> {
    return this.http.post<DeviceModelDto>(this.apiUrl, this.request(m, typeName)).pipe(catchError((e) => apiError(e, `Could not add ${m.name}.`)));
  }

  /** PUT /api/device-models/{modelNumber} (full replace; the model number itself cannot change) */
  update(m: DeviceModel, typeName: string): Observable<DeviceModelDto> {
    return this.http.put<DeviceModelDto>(`${this.apiUrl}/${encodeURIComponent(m.name)}`, this.request(m, typeName)).pipe(catchError((e) => apiError(e, `Could not save ${m.name}.`)));
  }

  /** DELETE /api/device-models/{modelNumber} */
  delete(modelNumber: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${encodeURIComponent(modelNumber)}`).pipe(catchError((e) => apiError(e, `Could not delete ${modelNumber}.`)));
  }

  private request(m: DeviceModel, typeName: string): DeviceModelRequest {
    const status = apiStatus(m.status);
    const power = apiPower(m.power);
    return {
      modelNumber: m.name,
      manufacturer: m.manufacturer,
      deviceType: typeName,
      // the API stores one connectivity: the model's first
      connectivity: m.connectivity[0],
      supportedMeasurements: m.measurements,
      powerSource: power,
      protocol: m.protocol,
      // "Pending Review" and "Draft" are both Draft on the page: keep the API's own one while unchanged
      status: m.api && pageStatus(m.api.status as ApiStatus) === m.status ? (m.api.status as ApiStatus) : status,
      createdBy: this.auth.getUserEmail() || 'unknown',
      clientId: environment.clientId,
      tenantId: environment.tenantId,
    };
  }
}
