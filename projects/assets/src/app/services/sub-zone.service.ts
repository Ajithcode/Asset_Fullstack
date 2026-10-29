import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../service/auth/auth.service';

/**
 * UI-friendly model derived from the API's SubZoneDto. Nests under a Zone.
 *
 * The API's request body types `topZone` as boolean and `priority`/`timeTakenAssemblePoint` as
 * numbers, but its GET/POST/PUT responses always send them back stringified (e.g.
 * `"topZone": "False"`, `"priority": "1"`) while `assemblyPoint`/`exit`/`status` responses are a
 * mix of real booleans and stringified ones. `toAppSubZone` below normalizes all of that back into
 * proper boolean/number types so nothing downstream has to know about the quirk.
 */
export interface AppSubZone {
  /** Mongo id used for GET/PUT/DELETE by id. */
  id: string;
  projectId: string;
  countryId: string;
  areaId: string;
  outerZoneId: string;
  buildingId: string;
  floorId: string;
  zoneId: string;
  subZoneName: string;
  description: string;
  topZone: boolean;
  priority: number;
  assemblyPoint: boolean;
  exit: boolean;
  status: boolean;
  timeTakenAssemblePoint: number;
  /** Absolute URL of the uploaded sub-zone map image, once one has been uploaded via `uploadMap`. */
  mapPath: string;
}

/** Fields collected from the add/edit sub-zone form. */
export interface SubZoneFormValue {
  projectId: string;
  countryId: string;
  areaId: string;
  outerZoneId: string;
  buildingId: string;
  floorId: string;
  zoneId: string;
  subZoneName: string;
  description: string;
  topZone: boolean;
  priority: number;
  assemblyPoint: boolean;
  exit: boolean;
  status: boolean;
  timeTakenAssemblePoint: number;
  mapPath: string;
}

/** Request body for POST {projectApiUrl}api/sub-zones */
interface CreateSubZoneRequest extends SubZoneFormValue {
  createdBy: string;
  clientId: string;
  tenantId: string;
}

/** Request body for PUT {projectApiUrl}api/sub-zones/{id} — parent ids are fixed at creation. */
interface UpdateSubZoneRequest
  extends Omit<SubZoneFormValue, 'projectId' | 'countryId' | 'areaId' | 'outerZoneId' | 'buildingId' | 'floorId' | 'zoneId'> {
  updatedBy: string;
}

/** Raw shape returned by the sub-zones API — see the class doc comment for why these are loosely typed. */
interface SubZoneDto {
  id: string;
  projectId: string;
  countryId: string;
  areaId: string;
  outerZoneId: string;
  buildingId: string;
  floorId: string;
  zoneId: string;
  subZoneName: string;
  description: string;
  topZone: boolean | string;
  priority: number | string;
  assemblyPoint: boolean | string;
  exit: boolean | string;
  status: boolean | string;
  createdBy: string;
  createdAt: string;
  updatedBy: string | null;
  updatedAt: string | null;
  clientId: string;
  tenantId: string;
  isDeleted: boolean;
  timeTakenAssemblePoint: number | string;
  mapPath: string | null;
}

function toBoolean(value: boolean | string): boolean {
  if (typeof value === 'boolean') return value;
  const normalized = value.trim().toLowerCase();
  return normalized === 'true' || normalized === '1';
}

function toNumber(value: number | string): number {
  const parsed = typeof value === 'number' ? value : Number(value);
  return isNaN(parsed) ? 0 : parsed;
}

function toAppSubZone(dto: SubZoneDto): AppSubZone {
  return {
    id: dto.id,
    projectId: dto.projectId,
    countryId: dto.countryId,
    areaId: dto.areaId,
    outerZoneId: dto.outerZoneId,
    buildingId: dto.buildingId,
    floorId: dto.floorId,
    zoneId: dto.zoneId,
    subZoneName: dto.subZoneName,
    description: dto.description,
    topZone: toBoolean(dto.topZone),
    priority: toNumber(dto.priority),
    assemblyPoint: toBoolean(dto.assemblyPoint),
    exit: toBoolean(dto.exit),
    status: toBoolean(dto.status),
    timeTakenAssemblePoint: toNumber(dto.timeTakenAssemblePoint),
    mapPath: dto.mapPath ?? '',
  };
}

/** Raw shape returned by GET {projectApiUrl}api/sub-zones/{id}/map */
interface SubZoneMapDto {
  id: string;
  mapPath: string;
}

@Injectable({ providedIn: 'root' })
export class SubZoneService {
  private apiUrl = `${environment.projectApiUrl}api/sub-zones`;

  constructor(private http: HttpClient, private auth: AuthService) {}

  /** GET /api/sub-zones */
  getAll(): Observable<AppSubZone[]> {
    return this.http.get<SubZoneDto[]>(this.apiUrl).pipe(map((list) => list.map(toAppSubZone)));
  }

  /** GET /api/sub-zones/{id} */
  getById(id: string): Observable<AppSubZone> {
    return this.http.get<SubZoneDto>(`${this.apiUrl}/${id}`).pipe(map(toAppSubZone));
  }

  /** POST /api/sub-zones */
  create(fields: SubZoneFormValue): Observable<AppSubZone> {
    const request: CreateSubZoneRequest = {
      ...fields,
      createdBy: this.auth.getUserEmail() || 'unknown',
      clientId: environment.clientId,
      tenantId: environment.tenantId,
    };
    return this.http.post<SubZoneDto>(this.apiUrl, request).pipe(map(toAppSubZone));
  }

  /** PUT /api/sub-zones/{id} (parent ids are fixed at creation and cannot be changed here) */
  update(id: string, fields: SubZoneFormValue): Observable<AppSubZone> {
    const { projectId, countryId, areaId, outerZoneId, buildingId, floorId, zoneId, ...rest } = fields;
    const request: UpdateSubZoneRequest = {
      ...rest,
      updatedBy: this.auth.getUserEmail() || 'unknown',
    };
    return this.http.put<SubZoneDto>(`${this.apiUrl}/${id}`, request).pipe(map(toAppSubZone));
  }

  /** DELETE /api/sub-zones/{id} */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  /** POST /api/sub-zones/{id}/map — multipart upload; returns the sub-zone with its new mapPath. */
  uploadMap(id: string, file: File): Observable<AppSubZone> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<SubZoneDto>(`${this.apiUrl}/${id}/map`, formData).pipe(map(toAppSubZone));
  }

  /** GET /api/sub-zones/{id}/map */
  getMap(id: string): Observable<SubZoneMapDto> {
    return this.http.get<SubZoneMapDto>(`${this.apiUrl}/${id}/map`);
  }
}
