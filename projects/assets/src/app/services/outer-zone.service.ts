import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../service/auth/auth.service';

/** UI-friendly model derived from the API's OuterZoneDto. Nests under a "Project Area" (State). */
export interface AppOuterZone {
  /** Mongo id used for GET/PUT/DELETE by id. */
  id: string;
  projectId: string;
  countryId: string;
  areaId: string;
  outerZoneName: string;
  description: string;
  outlineMap: string;
  latitude: string;
  longitude: string;
  status: boolean;
  mapPath: string;
}

/** Fields collected from the add/edit outer zone form. */
export interface OuterZoneFormValue {
  projectId: string;
  countryId: string;
  areaId: string;
  outerZoneName: string;
  description: string;
  outlineMap: string;
  latitude: string;
  longitude: string;
  status: boolean;
  mapPath: string;
}

/** Request body for POST {projectApiUrl}api/outer-zones */
interface CreateOuterZoneRequest extends OuterZoneFormValue {
  createdBy: string;
  clientId: string;
  tenantId: string;
}

/** Request body for PUT {projectApiUrl}api/outer-zones/{id} — projectId/countryId/areaId are fixed at creation. */
interface UpdateOuterZoneRequest extends Omit<OuterZoneFormValue, 'projectId' | 'countryId' | 'areaId'> {
  updatedBy: string;
}

/** Raw shape returned by the outer-zones API. */
interface OuterZoneDto {
  id: string;
  projectId: string;
  countryId: string;
  areaId: string;
  outerZoneName: string;
  description: string;
  outlineMap: string;
  latitude: string;
  longitude: string;
  status: boolean;
  createdBy: string;
  createdAt: string;
  updatedBy: string | null;
  updatedAt: string | null;
  clientId: string;
  tenantId: string;
  isDeleted: boolean;
  mapPath: string;
}

function toAppOuterZone(dto: OuterZoneDto): AppOuterZone {
  return {
    id: dto.id,
    projectId: dto.projectId,
    countryId: dto.countryId,
    areaId: dto.areaId,
    outerZoneName: dto.outerZoneName,
    description: dto.description,
    outlineMap: dto.outlineMap,
    latitude: dto.latitude,
    longitude: dto.longitude,
    status: dto.status,
    mapPath: dto.mapPath,
  };
}

@Injectable({ providedIn: 'root' })
export class OuterZoneService {
  private apiUrl = `${environment.projectApiUrl}api/outer-zones`;

  constructor(private http: HttpClient, private auth: AuthService) {}

  /** GET /api/outer-zones */
  getAll(): Observable<AppOuterZone[]> {
    return this.http.get<OuterZoneDto[]>(this.apiUrl).pipe(map((list) => list.map(toAppOuterZone)));
  }

  /** GET /api/outer-zones/{id} */
  getById(id: string): Observable<AppOuterZone> {
    return this.http.get<OuterZoneDto>(`${this.apiUrl}/${id}`).pipe(map(toAppOuterZone));
  }

  /** POST /api/outer-zones */
  create(fields: OuterZoneFormValue): Observable<AppOuterZone> {
    const request: CreateOuterZoneRequest = {
      ...fields,
      createdBy: this.auth.getUserEmail() || 'unknown',
      clientId: environment.clientId,
      tenantId: environment.tenantId,
    };
    return this.http.post<OuterZoneDto>(this.apiUrl, request).pipe(map(toAppOuterZone));
  }

  /** PUT /api/outer-zones/{id} (projectId/countryId/areaId are fixed at creation and cannot be changed here) */
  update(id: string, fields: OuterZoneFormValue): Observable<AppOuterZone> {
    const { projectId, countryId, areaId, ...rest } = fields;
    const request: UpdateOuterZoneRequest = {
      ...rest,
      updatedBy: this.auth.getUserEmail() || 'unknown',
    };
    return this.http.put<OuterZoneDto>(`${this.apiUrl}/${id}`, request).pipe(map(toAppOuterZone));
  }

  /** DELETE /api/outer-zones/{id} */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
