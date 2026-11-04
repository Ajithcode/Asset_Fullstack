import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, forkJoin, map, of, switchMap } from 'rxjs';
import { AssetRecord, AuthService, environment } from 'shared-ui';
import { TENANT } from '../services/asset-api';
import { AssetListItemDto, AssetLookupService } from '../asset-maintenance/asset-lookup.service';

/**
 * Tracking service routes (/api/location-events), reached through the API gateway. The environment has no separate
 * tracking URL; the gateway is the same host as the Asset API.
 */
export const TRACKING_API = `${environment.assetApiUrl}api/`;

/** Event types of the Tracking service (LocationEventType). */
export type LocationEventType = 'Entered Zone' | 'Departed Zone' | 'Geofence Alert' | 'Dwell Exceeded';

/** GET /api/location-events row (LocationEventResponse). */
export interface LocationEventDto {
  id: string;
  eventId: string;
  eventTime: string;
  assetId: string;
  assetName: string;
  eventType: string;
  zoneName: string;
  details: string | null;
  createdBy: string | null;
  createdAt: string | null;
}

export type MovementType = 'In' | 'Out' | 'Move';

/** One movement as the Location History page shows it. */
export interface ApiMovement {
  id: number;
  eventId: string;
  /** Local time, "YYYY-MM-DDTHH:mm". */
  at: string;
  location: string;
  movement: MovementType;
  source: string;
  remarks: string;
  by: string;
}

export interface LocationLoad {
  registration: AssetListItemDto | null;
  serialNumber: string;
  assignedTo: string;
  custodian: string;
  movements: ApiMovement[];
}

const pad = (n: number) => String(n).padStart(2, '0');

/** API date-time → local "YYYY-MM-DDTHH:mm". */
export function localStamp(value: string): string {
  const d = new Date(value);
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function movementOf(eventType: string): MovementType {
  return eventType === 'Entered Zone' ? 'In' : eventType === 'Departed Zone' ? 'Out' : 'Move';
}

function eventTypeOf(m: MovementType): LocationEventType {
  return m === 'Out' ? 'Departed Zone' : 'Entered Zone';
}

/** Movements of one asset from the Tracking service's location events (oldest = id 1). */
export function toMovements(events: LocationEventDto[]): ApiMovement[] {
  return [...events]
    .sort((a, b) => a.eventTime.localeCompare(b.eventTime))
    .map((e, i) => {
      const manual = !!e.createdBy && !/^system$/i.test(e.createdBy);
      return {
        id: i + 1,
        eventId: e.eventId,
        at: localStamp(e.eventTime),
        location: e.zoneName,
        movement: movementOf(e.eventType),
        source: manual ? 'Manual Update' : 'GPS Tag',
        remarks: e.details || e.eventType,
        by: e.createdBy || 'System',
      };
    });
}

@Injectable({ providedIn: 'root' })
export class LocationHistoryService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly lookup = inject(AssetLookupService);

  /** The asset's location events (Tracking service) and its serial number / custodian (Asset API registration). */
  load(asset: AssetRecord): Observable<LocationLoad> {
    const reg$ = this.lookup.byTag(asset.tag).pipe(
      switchMap((item) =>
        item
          ? this.lookup.registration(item.assetRegistrationId).pipe(
              map((full) => ({ item, full })),
              catchError(() => of({ item, full: null })),
            )
          : of(null),
      ),
      // the history still shows without the registration
      catchError(() => of(null)),
    );
    const events$ = this.http.get<LocationEventDto[]>(`${TRACKING_API}location-events`);
    return forkJoin({ reg: reg$, events: events$ }).pipe(
      map(({ reg, events }) => {
        const tag = asset.tag.toUpperCase();
        const assignment = reg?.full?.locationAssignment?.assignment;
        return {
          registration: reg?.item ?? null,
          serialNumber: reg?.full?.basicInformation?.serialNumber || asset.serialNumber || '—',
          assignedTo: assignment?.custodianDepartment || reg?.item.department || (asset.department ? `${asset.department} Dept.` : '—'),
          custodian: assignment?.assignedToName || reg?.item.custodianName || asset.custodian || '—',
          movements: toMovements(events.filter((e) => (e.assetId ?? '').toUpperCase() === tag)),
        };
      }),
    );
  }

  /** Records a movement as a location event. */
  record(asset: AssetRecord, m: { at: string; location: string; movement: MovementType; remarks: string; source: string }): Observable<LocationEventDto> {
    return this.http.post<LocationEventDto>(`${TRACKING_API}location-events`, {
      eventTime: new Date(m.at).toISOString(),
      assetId: asset.tag,
      assetName: asset.name,
      eventType: eventTypeOf(m.movement),
      zoneName: m.location,
      details: m.remarks ? `${m.remarks} (${m.source})` : m.source,
      createdBy: this.auth.getUser()?.userName || this.auth.getUserEmail() || null,
      ...TENANT,
    });
  }
}
