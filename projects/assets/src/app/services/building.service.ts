import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../service/auth/auth.service';

/** UI-friendly model derived from the API's BuildingDto. Nests under an Outer Zone. */
export interface AppBuilding {
  /** Mongo id used for GET/PUT/DELETE by id. */
  id: string;
  projectId: string;
  countryId: string;
  areaId: string;
  outerZoneId: string;
  buildingName: string;
  description: string;
  latitude: string;
  longitude: string;
  status: boolean;
}

/** Fields collected from the add/edit building form. */
export interface BuildingFormValue {
  projectId: string;
  countryId: string;
  areaId: string;
  outerZoneId: string;
  buildingName: string;
  description: string;
  latitude: string;
  longitude: string;
  status: boolean;
}

/** Request body for POST {projectApiUrl}api/buildings */
interface CreateBuildingRequest extends BuildingFormValue {
  createdBy: string;
  clientId: string;
  tenantId: string;
}

/** Request body for PUT {projectApiUrl}api/buildings/{id} — projectId/countryId/areaId/outerZoneId are fixed at creation. */
interface UpdateBuildingRequest extends Omit<BuildingFormValue, 'projectId' | 'countryId' | 'areaId' | 'outerZoneId'> {
  updatedBy: string;
}

/** Raw shape returned by the buildings API. */
interface BuildingDto {
  id: string;
  projectId: string;
  countryId: string;
  areaId: string;
  outerZoneId: string;
  buildingName: string;
  description: string;
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
}

function toAppBuilding(dto: BuildingDto): AppBuilding {
  return {
    id: dto.id,
    projectId: dto.projectId,
    countryId: dto.countryId,
    areaId: dto.areaId,
    outerZoneId: dto.outerZoneId,
    buildingName: dto.buildingName,
    description: dto.description,
    latitude: dto.latitude,
    longitude: dto.longitude,
    status: dto.status,
  };
}

@Injectable({ providedIn: 'root' })
export class BuildingService {
  private apiUrl = `${environment.projectApiUrl}api/buildings`;

  constructor(private http: HttpClient, private auth: AuthService) {}

  /** GET /api/buildings */
  getAll(): Observable<AppBuilding[]> {
    return this.http.get<BuildingDto[]>(this.apiUrl).pipe(map((list) => list.map(toAppBuilding)));
  }

  /** GET /api/buildings/{id} */
  getById(id: string): Observable<AppBuilding> {
    return this.http.get<BuildingDto>(`${this.apiUrl}/${id}`).pipe(map(toAppBuilding));
  }

  /** POST /api/buildings */
  create(fields: BuildingFormValue): Observable<AppBuilding> {
    const request: CreateBuildingRequest = {
      ...fields,
      createdBy: this.auth.getUserEmail() || 'unknown',
      clientId: environment.clientId,
      tenantId: environment.tenantId,
    };
    return this.http.post<BuildingDto>(this.apiUrl, request).pipe(map(toAppBuilding));
  }

  /** PUT /api/buildings/{id} (parent ids are fixed at creation and cannot be changed here) */
  update(id: string, fields: BuildingFormValue): Observable<AppBuilding> {
    const { projectId, countryId, areaId, outerZoneId, ...rest } = fields;
    const request: UpdateBuildingRequest = {
      ...rest,
      updatedBy: this.auth.getUserEmail() || 'unknown',
    };
    return this.http.put<BuildingDto>(`${this.apiUrl}/${id}`, request).pipe(map(toAppBuilding));
  }

  /** DELETE /api/buildings/{id} */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
