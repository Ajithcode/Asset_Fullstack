import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { apiError } from '../geofence-alerts/geofence-api.service';
import { LocEvent } from './location-data';

/** Event types the API accepts; the page's own "Signal Lost" and "Stale Location" are not among them. */
export type ApiEventType = 'Entered Zone' | 'Departed Zone' | 'Geofence Alert' | 'Dwell Exceeded';
export const API_EVENT_TYPES: ApiEventType[] = ['Entered Zone', 'Departed Zone', 'Geofence Alert', 'Dwell Exceeded'];

/** A location event as the API stores it ({trackingApiUrl}api/location-events, addressed by eventId, e.g. LE000001). */
export interface LocationEventDto {
  id: string;
  eventId: string;
  eventTime: string;
  assetId: string;
  assetName: string;
  eventType: ApiEventType;
  zoneName: string;
  details: string | null;
}

/** Request body for POST and PUT /api/location-events (same shape; the API generates the event id). */
interface LocationEventRequest {
  eventTime: string;
  assetId: string;
  assetName: string;
  eventType: ApiEventType;
  zoneName: string;
  details: string | null;
  createdBy: string;
  clientId: string;
  tenantId: string;
}

export function eventFromDto(dto: LocationEventDto): LocEvent {
  return {
    eventId: dto.eventId,
    at: Date.parse(dto.eventTime),
    assetId: dto.assetId,
    assetName: dto.assetName,
    event: dto.eventType,
    zone: dto.zoneName,
    details: dto.details ?? '',
  };
}

@Injectable({ providedIn: 'root' })
export class LocationEventApiService {
  private apiUrl = `${environment.trackingApiUrl}api/location-events`;

  constructor(private http: HttpClient, private auth: AuthService) {}

  /** GET /api/location-events */
  getAll(): Observable<LocationEventDto[]> {
    return this.http.get<LocationEventDto[]>(this.apiUrl).pipe(catchError((e) => apiError(e, 'Could not load location events.')));
  }

  /** GET /api/location-events/{eventId} */
  getByEventId(eventId: string): Observable<LocationEventDto> {
    return this.http.get<LocationEventDto>(`${this.apiUrl}/${encodeURIComponent(eventId)}`).pipe(catchError((e) => apiError(e, 'Could not load the location event.')));
  }

  /** POST /api/location-events */
  create(e: LocEvent & { event: ApiEventType }): Observable<LocationEventDto> {
    return this.http.post<LocationEventDto>(this.apiUrl, this.request(e)).pipe(catchError((err) => apiError(err, 'Could not add the event.')));
  }

  /** PUT /api/location-events/{eventId} (full replace) */
  update(eventId: string, e: LocEvent & { event: ApiEventType }): Observable<LocationEventDto> {
    return this.http.put<LocationEventDto>(`${this.apiUrl}/${encodeURIComponent(eventId)}`, this.request(e)).pipe(catchError((err) => apiError(err, `Could not save ${eventId}.`)));
  }

  /** DELETE /api/location-events/{eventId} */
  delete(eventId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${encodeURIComponent(eventId)}`).pipe(catchError((err) => apiError(err, `Could not delete ${eventId}.`)));
  }

  private request(e: LocEvent & { event: ApiEventType }): LocationEventRequest {
    return {
      eventTime: new Date(e.at).toISOString(),
      assetId: e.assetId,
      assetName: e.assetName,
      eventType: e.event,
      zoneName: e.zone,
      details: e.details || null,
      createdBy: this.auth.getUserEmail() || 'unknown',
      clientId: environment.clientId,
      tenantId: environment.tenantId,
    };
  }
}
