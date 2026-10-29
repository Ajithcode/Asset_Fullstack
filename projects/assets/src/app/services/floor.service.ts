import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../service/auth/auth.service';

/** UI-friendly model derived from the API's FloorDto. Nests under a Building. */
export interface AppFloor {
  /** Mongo id used for GET/PUT/DELETE by id. */
  id: string;
  projectId: string;
  countryId: string;
  areaId: string;
  outerZoneId: string;
  buildingId: string;
  floorName: string;
  description: string;
  status: boolean;
  /** Absolute URL of the uploaded floor map image, once one has been uploaded via `uploadMap`. */
  mapPath: string;
}

/** Fields collected from the add/edit floor form. */
export interface FloorFormValue {
  projectId: string;
  countryId: string;
  areaId: string;
  outerZoneId: string;
  buildingId: string;
  floorName: string;
  description: string;
  status: boolean;
  mapPath: string;
}

/** Request body for POST {projectApiUrl}api/floors */
interface CreateFloorRequest extends FloorFormValue {
  createdBy: string;
  clientId: string;
  tenantId: string;
}

/** Request body for PUT {projectApiUrl}api/floors/{id} — parent ids are fixed at creation. */
interface UpdateFloorRequest
  extends Omit<FloorFormValue, 'projectId' | 'countryId' | 'areaId' | 'outerZoneId' | 'buildingId'> {
  updatedBy: string;
}

/** Raw shape returned by the floors API. */
interface FloorDto {
  id: string;
  projectId: string;
  countryId: string;
  areaId: string;
  outerZoneId: string;
  buildingId: string;
  floorName: string;
  description: string;
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

/** Raw shape returned by GET {projectApiUrl}api/floors/{id}/map */
interface FloorMapDto {
  id: string;
  mapPath: string;
}

function toAppFloor(dto: FloorDto): AppFloor {
  return {
    id: dto.id,
    projectId: dto.projectId,
    countryId: dto.countryId,
    areaId: dto.areaId,
    outerZoneId: dto.outerZoneId,
    buildingId: dto.buildingId,
    floorName: dto.floorName,
    description: dto.description,
    status: dto.status,
    mapPath: dto.mapPath,
  };
}

@Injectable({ providedIn: 'root' })
export class FloorService {
  private apiUrl = `${environment.projectApiUrl}api/floors`;

  constructor(private http: HttpClient, private auth: AuthService) {}

  /** GET /api/floors */
  getAll(): Observable<AppFloor[]> {
    return this.http.get<FloorDto[]>(this.apiUrl).pipe(map((list) => list.map(toAppFloor)));
  }

  /** GET /api/floors/{id} */
  getById(id: string): Observable<AppFloor> {
    return this.http.get<FloorDto>(`${this.apiUrl}/${id}`).pipe(map(toAppFloor));
  }

  /** POST /api/floors */
  create(fields: FloorFormValue): Observable<AppFloor> {
    const request: CreateFloorRequest = {
      ...fields,
      createdBy: this.auth.getUserEmail() || 'unknown',
      clientId: environment.clientId,
      tenantId: environment.tenantId,
    };
    return this.http.post<FloorDto>(this.apiUrl, request).pipe(map(toAppFloor));
  }

  /** PUT /api/floors/{id} (parent ids are fixed at creation and cannot be changed here) */
  update(id: string, fields: FloorFormValue): Observable<AppFloor> {
    const { projectId, countryId, areaId, outerZoneId, buildingId, ...rest } = fields;
    const request: UpdateFloorRequest = {
      ...rest,
      updatedBy: this.auth.getUserEmail() || 'unknown',
    };
    return this.http.put<FloorDto>(`${this.apiUrl}/${id}`, request).pipe(map(toAppFloor));
  }

  /** DELETE /api/floors/{id} */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  /** POST /api/floors/{id}/map — multipart upload; returns the floor with its new mapPath. */
  uploadMap(id: string, file: File): Observable<AppFloor> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<FloorDto>(`${this.apiUrl}/${id}/map`, formData).pipe(map(toAppFloor));
  }

  /** GET /api/floors/{id}/map */
  getMap(id: string): Observable<FloorMapDto> {
    return this.http.get<FloorMapDto>(`${this.apiUrl}/${id}/map`);
  }
}
