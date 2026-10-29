import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../service/auth/auth.service';

/**
 * UI-friendly model derived from the API's AreaDto. Named "ProjectArea" (not "Area") to avoid
 * colliding with shared-ui's `Area` type, which models the "Country" level of the site hierarchy —
 * this backend "Area" resource corresponds to the "State" level (UI label "Area") nested under it.
 */
export interface AppProjectArea {
  /** Mongo id used for GET/PUT/DELETE by id. */
  id: string;
  projectId: string;
  countryId: string;
  areaName: string;
  description: string;
  outlineMap: string;
  latitude: string;
  longitude: string;
  status: boolean;
  mapPath: string;
}

/** Fields collected from the add/edit area form. */
export interface ProjectAreaFormValue {
  projectId: string;
  countryId: string;
  areaName: string;
  description: string;
  outlineMap: string;
  latitude: string;
  longitude: string;
  status: boolean;
  mapPath: string;
}

/** Request body for POST {projectApiUrl}api/areas */
interface CreateAreaRequest extends ProjectAreaFormValue {
  createdBy: string;
  clientId: string;
  tenantId: string;
}

/** Request body for PUT {projectApiUrl}api/areas/{id} — projectId/countryId are fixed at creation. */
interface UpdateAreaRequest extends Omit<ProjectAreaFormValue, 'projectId' | 'countryId'> {
  updatedBy: string;
}

/** Raw shape returned by the areas API. */
interface AreaDto {
  id: string;
  projectId: string;
  countryId: string;
  areaName: string;
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

function toAppProjectArea(dto: AreaDto): AppProjectArea {
  return {
    id: dto.id,
    projectId: dto.projectId,
    countryId: dto.countryId,
    areaName: dto.areaName,
    description: dto.description,
    outlineMap: dto.outlineMap,
    latitude: dto.latitude,
    longitude: dto.longitude,
    status: dto.status,
    mapPath: dto.mapPath,
  };
}

@Injectable({ providedIn: 'root' })
export class ProjectAreaService {
  private apiUrl = `${environment.projectApiUrl}api/areas`;

  constructor(private http: HttpClient, private auth: AuthService) {}

  /** GET /api/areas */
  getAll(): Observable<AppProjectArea[]> {
    return this.http.get<AreaDto[]>(this.apiUrl).pipe(map((list) => list.map(toAppProjectArea)));
  }

  /** GET /api/areas/{id} */
  getById(id: string): Observable<AppProjectArea> {
    return this.http.get<AreaDto>(`${this.apiUrl}/${id}`).pipe(map(toAppProjectArea));
  }

  /** POST /api/areas */
  create(fields: ProjectAreaFormValue): Observable<AppProjectArea> {
    const request: CreateAreaRequest = {
      ...fields,
      createdBy: this.auth.getUserEmail() || 'unknown',
      clientId: environment.clientId,
      tenantId: environment.tenantId,
    };
    return this.http.post<AreaDto>(this.apiUrl, request).pipe(map(toAppProjectArea));
  }

  /** PUT /api/areas/{id} (projectId/countryId are fixed at creation and cannot be changed here) */
  update(id: string, fields: ProjectAreaFormValue): Observable<AppProjectArea> {
    const { projectId, countryId, ...rest } = fields;
    const request: UpdateAreaRequest = {
      ...rest,
      updatedBy: this.auth.getUserEmail() || 'unknown',
    };
    return this.http.put<AreaDto>(`${this.apiUrl}/${id}`, request).pipe(map(toAppProjectArea));
  }

  /** DELETE /api/areas/{id} */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
