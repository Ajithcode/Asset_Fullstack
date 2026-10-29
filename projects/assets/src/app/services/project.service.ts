import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../service/auth/auth.service';

/** UI-friendly model derived from the API's ProjectDto. */
export interface AppProject {
  /** Mongo id used for GET/PUT/DELETE by id. */
  id: string;
  projectName: string;
  description: string;
  status: boolean;
  /** ISO date-time string */
  weekStart: string;
  /** ISO date-time string */
  weekEnd: string;
}

/** Fields collected from the add/edit project form. */
export interface ProjectFormValue {
  projectName: string;
  description: string;
  status: boolean;
  /** ISO date-time string (a bare yyyy-MM-dd is accepted by the API) */
  weekStart: string;
  /** ISO date-time string (a bare yyyy-MM-dd is accepted by the API) */
  weekEnd: string;
}

/** Request body for POST {projectApiUrl}api/projects */
interface CreateProjectRequest extends ProjectFormValue {
  createdBy: string;
  clientId: string;
  tenantId: string;
}

/** Request body for PUT {projectApiUrl}api/projects/{id} */
interface UpdateProjectRequest extends ProjectFormValue {
  clientId: string;
  updatedBy: string;
}

/** Raw shape returned by the projects API. */
interface ProjectDto {
  id: string;
  projectName: string;
  description: string;
  status: boolean;
  createdBy: string;
  createdAt: string;
  updatedBy: string | null;
  updatedAt: string | null;
  clientId: string;
  tenantId: string;
  isDeleted: boolean;
  weekStart: string;
  weekEnd: string;
}

function toAppProject(dto: ProjectDto): AppProject {
  return {
    id: dto.id,
    projectName: dto.projectName,
    description: dto.description,
    status: dto.status,
    weekStart: dto.weekStart,
    weekEnd: dto.weekEnd
  };
}

@Injectable({ providedIn: 'root' })
export class ProjectService {
  private apiUrl = `${environment.projectApiUrl}api/projects`;

  constructor(private http: HttpClient, private auth: AuthService) {}

  /** GET /api/projects */
  getAll(): Observable<AppProject[]> {
    return this.http.get<ProjectDto[]>(this.apiUrl).pipe(map(list => list.map(toAppProject)));
  }

  /** GET /api/projects/{id} */
  getById(id: string): Observable<AppProject> {
    return this.http.get<ProjectDto>(`${this.apiUrl}/${id}`).pipe(map(toAppProject));
  }

  /** POST /api/projects */
  create(fields: ProjectFormValue): Observable<AppProject> {
    const request: CreateProjectRequest = {
      ...fields,
      createdBy: this.auth.getUserEmail() || 'unknown',
      clientId: environment.clientId,
      tenantId: environment.tenantId
    };
    return this.http.post<ProjectDto>(this.apiUrl, request).pipe(map(toAppProject));
  }

  /** PUT /api/projects/{id} */
  update(id: string, fields: ProjectFormValue): Observable<AppProject> {
    const request: UpdateProjectRequest = {
      ...fields,
      clientId: environment.clientId,
      updatedBy: this.auth.getUserEmail() || 'unknown'
    };
    return this.http.put<ProjectDto>(`${this.apiUrl}/${id}`, request).pipe(map(toAppProject));
  }

  /** DELETE /api/projects/{id} */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
