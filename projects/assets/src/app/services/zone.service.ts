import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../service/auth/auth.service';

/** UI-friendly model derived from the API's ZoneDto. Nests under a Floor. */
export interface AppZone {
  /** Mongo id used for GET/PUT/DELETE by id. */
  id: string;
  projectId: string;
  countryId: string;
  areaId: string;
  outerZoneId: string;
  buildingId: string;
  floorId: string;
  zoneName: string;
  description: string;
  topZone: string;
  priority: string;
  musterPoint: boolean;
  exitPoint: boolean;
  status: boolean;
  timeTakenAssemblePoint: number;
  /** Absolute URL of the uploaded zone map image, once one has been uploaded via `uploadMap`. */
  mapPath: string;
}

/** Fields collected from the add/edit zone form. */
export interface ZoneFormValue {
  projectId: string;
  countryId: string;
  areaId: string;
  outerZoneId: string;
  buildingId: string;
  floorId: string;
  zoneName: string;
  description: string;
  topZone: string;
  priority: string;
  musterPoint: boolean;
  exitPoint: boolean;
  status: boolean;
  timeTakenAssemblePoint: number;
  mapPath: string;
}

/** Request body for POST {projectApiUrl}api/zones */
interface CreateZoneRequest extends ZoneFormValue {
  createdBy: string;
  clientId: string;
  tenantId: string;
}

/** Request body for PUT {projectApiUrl}api/zones/{id} — parent ids are fixed at creation. */
interface UpdateZoneRequest
  extends Omit<ZoneFormValue, 'projectId' | 'countryId' | 'areaId' | 'outerZoneId' | 'buildingId' | 'floorId'> {
  updatedBy: string;
}

/** Raw shape returned by the zones API. */
interface ZoneDto {
  id: string;
  projectId: string;
  countryId: string;
  areaId: string;
  outerZoneId: string;
  buildingId: string;
  floorId: string;
  zoneName: string;
  description: string;
  topZone: string;
  priority: string;
  musterPoint: boolean;
  exitPoint: boolean;
  status: boolean;
  createdBy: string;
  createdAt: string;
  updatedBy: string | null;
  updatedAt: string | null;
  clientId: string;
  tenantId: string;
  isDeleted: boolean;
  timeTakenAssemblePoint: number;
  mapPath: string;
}

/** Raw shape returned by GET {projectApiUrl}api/zones/{id}/map */
interface ZoneMapDto {
  id: string;
  mapPath: string;
}

function toAppZone(dto: ZoneDto): AppZone {
  return {
    id: dto.id,
    projectId: dto.projectId,
    countryId: dto.countryId,
    areaId: dto.areaId,
    outerZoneId: dto.outerZoneId,
    buildingId: dto.buildingId,
    floorId: dto.floorId,
    zoneName: dto.zoneName,
    description: dto.description,
    topZone: dto.topZone,
    priority: dto.priority,
    musterPoint: dto.musterPoint,
    exitPoint: dto.exitPoint,
    status: dto.status,
    timeTakenAssemblePoint: dto.timeTakenAssemblePoint,
    mapPath: dto.mapPath,
  };
}

@Injectable({ providedIn: 'root' })
export class ZoneService {
  private apiUrl = `${environment.projectApiUrl}api/zones`;

  constructor(private http: HttpClient, private auth: AuthService) {}

  /** GET /api/zones */
  getAll(): Observable<AppZone[]> {
    return this.http.get<ZoneDto[]>(this.apiUrl).pipe(map((list) => list.map(toAppZone)));
  }

  /** GET /api/zones/{id} */
  getById(id: string): Observable<AppZone> {
    return this.http.get<ZoneDto>(`${this.apiUrl}/${id}`).pipe(map(toAppZone));
  }

  /** POST /api/zones */
  create(fields: ZoneFormValue): Observable<AppZone> {
    const request: CreateZoneRequest = {
      ...fields,
      createdBy: this.auth.getUserEmail() || 'unknown',
      clientId: environment.clientId,
      tenantId: environment.tenantId,
    };
    return this.http.post<ZoneDto>(this.apiUrl, request).pipe(map(toAppZone));
  }

  /** PUT /api/zones/{id} (parent ids are fixed at creation and cannot be changed here) */
  update(id: string, fields: ZoneFormValue): Observable<AppZone> {
    const { projectId, countryId, areaId, outerZoneId, buildingId, floorId, ...rest } = fields;
    const request: UpdateZoneRequest = {
      ...rest,
      updatedBy: this.auth.getUserEmail() || 'unknown',
    };
    return this.http.put<ZoneDto>(`${this.apiUrl}/${id}`, request).pipe(map(toAppZone));
  }

  /** DELETE /api/zones/{id} */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  /** POST /api/zones/{id}/map — multipart upload; returns the zone with its new mapPath. */
  uploadMap(id: string, file: File): Observable<AppZone> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<ZoneDto>(`${this.apiUrl}/${id}/map`, formData).pipe(map(toAppZone));
  }

  /** GET /api/zones/{id}/map */
  getMap(id: string): Observable<ZoneMapDto> {
    return this.http.get<ZoneMapDto>(`${this.apiUrl}/${id}/map`);
  }
}
