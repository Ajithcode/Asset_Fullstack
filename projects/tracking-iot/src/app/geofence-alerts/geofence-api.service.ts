import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { ZoneStatus, ZoneType } from './geofence-data';

/** A row of the Geofence List as the API stores it (GET/POST/PUT {trackingApiUrl}api/geofences). */
export interface GeofenceDto {
  id: string;
  name: string;
  geofenceType: ZoneType;
  siteId: string | null;
  siteName: string;
  monitoredAssetCount: number;
  ruleCount: number;
  status: ZoneStatus | null;
  lastEventAt: string | null;
  createdBy: string | null;
  createdAt: string | null;
  clientId: string | null;
  tenantId: string | null;
}

/** Fields the page sends on create and update. Name, geofenceType and siteName are required by the API. */
export interface GeofenceFields {
  name: string;
  geofenceType: ZoneType;
  siteId?: string;
  siteName: string;
  monitoredAssetCount: number;
  ruleCount: number;
  status: ZoneStatus;
  lastEventAt?: string | null;
}

/** Request body for POST and PUT /api/geofences (the API uses the same shape for both). */
interface GeofenceRequest extends GeofenceFields {
  createdBy: string;
  clientId: string;
  tenantId: string;
}

@Injectable({ providedIn: 'root' })
export class GeofenceApiService {
  private apiUrl = `${environment.trackingApiUrl}api/geofences`;

  constructor(private http: HttpClient, private auth: AuthService) {}

  /** GET /api/geofences */
  getAll(): Observable<GeofenceDto[]> {
    return this.http.get<GeofenceDto[]>(this.apiUrl).pipe(catchError((e) => this.fail(e, 'Could not load geofences.')));
  }

  /** GET /api/geofences/{id} */
  getById(id: string): Observable<GeofenceDto> {
    return this.http.get<GeofenceDto>(`${this.apiUrl}/${id}`).pipe(catchError((e) => this.fail(e, 'Could not load the geofence.')));
  }

  /** POST /api/geofences */
  create(fields: GeofenceFields): Observable<GeofenceDto> {
    return this.http.post<GeofenceDto>(this.apiUrl, this.request(fields)).pipe(catchError((e) => this.fail(e, 'Could not create the geofence.')));
  }

  /** PUT /api/geofences/{id} (full replace: send every field) */
  update(id: string, fields: GeofenceFields): Observable<GeofenceDto> {
    return this.http.put<GeofenceDto>(`${this.apiUrl}/${id}`, this.request(fields)).pipe(catchError((e) => this.fail(e, 'Could not update the geofence.')));
  }

  /** DELETE /api/geofences/{id}. The API refuses (409) while the geofence still has alert rules. */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`).pipe(catchError((e) => this.fail(e, 'Could not delete the geofence.')));
  }

  private request(fields: GeofenceFields): GeofenceRequest {
    return {
      ...fields,
      createdBy: this.auth.getUserEmail() || 'unknown',
      clientId: environment.clientId,
      tenantId: environment.tenantId,
    };
  }

  private fail(err: unknown, fallback: string): Observable<never> {
    return apiError(err, fallback);
  }
}

/** Turns { message }, ASP.NET validation { errors } or a network failure into an Error with a readable message. */
export function apiError(err: unknown, fallback: string): Observable<never> {
  let message = fallback;
  if (err instanceof HttpErrorResponse) {
    const body = err.error;
    const firstError = body?.errors && typeof body.errors === 'object' ? Object.values(body.errors).flat()[0] : undefined;
    if (err.status === 0) message = 'Cannot reach the server. Check that the backend is running.';
    else if (typeof firstError === 'string') message = firstError;
    else if (body?.message) message = body.message;
  }
  return throwError(() => new Error(message));
}
