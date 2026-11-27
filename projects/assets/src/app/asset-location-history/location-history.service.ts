import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, forkJoin, map, of, switchMap } from 'rxjs';
import { AssetRecord, AuthService, environment } from 'shared-ui';
import { TENANT } from '../services/asset-api';
import { AssetListItemDto, AssetLookupService } from '../asset-maintenance/asset-lookup.service';

/** Tracking service routes (/api/location-events). */
export const TRACKING_API = `${environment.trackingApiUrl}api/`;

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

/** GET /api/tag-ids/asset-movement visit (MovementVisit). */
export interface MovementVisitDto {
  fromPlace: string | null;
  gatewayIds: string[];
  zoneName: string | null;
  from: string;
  to: string;
  minutes: number;
  readings: number;
}

/** GET /api/tag-ids/asset-movement item (AssetMovement), the fields used here. */
export interface AssetMovementDto {
  assetRegistrationId: string;
  assetCode: string | null;
  assetName: string | null;
  tagIds: string[];
  history: MovementVisitDto[] | null;
}

interface AssetMovementPageDto {
  totalPages: number;
  items: AssetMovementDto[];
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

/** "1h 5m" style duration from minutes. */
export function fmtMinutes(minutes: number): string {
  const m = Math.max(0, Math.round(minutes));
  const d = Math.floor(m / 1440);
  const h = Math.floor((m % 1440) / 60);
  return d ? `${d}d ${h}h ${m % 60}m` : h ? `${h}h ${m % 60}m` : `${m}m`;
}

/** Zone shown for a visit: the zone's current name, else the gateway it was heard by (unplotted gateway). */
export function visitPlace(v: MovementVisitDto): string {
  return v.zoneName || (v.gatewayIds?.length ? `Gateway ${v.gatewayIds[0]}` : 'Unknown location');
}

/**
 * Resolved zone visits (device_trigger "in" readings, written by the location resolver) as location events:
 * "Entered Zone" at the visit start, "Departed Zone" at its end (not for the latest visit, which is the current location),
 * both with the time spent in the zone.
 */
export function visitsToEvents(item: AssetMovementDto, assetKey: string, assetName: string): LocationEventDto[] {
  const visits = item.history ?? [];
  const out: LocationEventDto[] = [];
  visits.forEach((v, i) => {
    const zone = visitPlace(v);
    const last = i === visits.length - 1;
    const base = { assetId: assetKey, assetName, zoneName: zone, createdBy: 'System', createdAt: null };
    out.push({
      ...base, id: `${item.assetRegistrationId}-${i}-in`, eventId: `${item.assetRegistrationId}-${i}-in`, eventTime: v.from,
      eventType: 'Entered Zone', details: last ? `Current location, ${fmtMinutes(v.minutes)} so far (BLE)` : `Time spent in zone ${fmtMinutes(v.minutes)} (BLE)`,
    });
    if (!last) {
      out.push({
        ...base, id: `${item.assetRegistrationId}-${i}-out`, eventId: `${item.assetRegistrationId}-${i}-out`, eventTime: v.to,
        eventType: 'Departed Zone', details: `Left after ${fmtMinutes(v.minutes)} (BLE)`,
      });
    }
  });
  return out;
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
    const manual$ = this.http.get<LocationEventDto[]>(`${TRACKING_API}location-events`).pipe(catchError(() => of([] as LocationEventDto[])));
    // resolved zone visits of the asset's tags, last 31 days
    const resolved$ = reg$.pipe(
      switchMap((reg) =>
        reg
          ? this.fetchMovement(31, reg.item.assetRegistrationId).pipe(
              map((items) => items.flatMap((i) => visitsToEvents(i, asset.tag, asset.name))),
            )
          : of([] as LocationEventDto[]),
      ),
    );
    const events$ = forkJoin({ manual: manual$, resolved: resolved$ }).pipe(map((r) => [...r.manual, ...r.resolved]));
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

  /** Zone visits (history=true) of all assets, or of one, for the last `days` days (the API allows 31), all pages. */
  private fetchMovement(days: number, assetId?: string): Observable<AssetMovementDto[]> {
    const to = new Date();
    const from = new Date(to.getTime() - Math.min(days, 31) * 86_400_000 + 60_000);
    const page = (n: number): Observable<AssetMovementPageDto> => {
      let params = new HttpParams().set('history', 'true').set('page', n).set('pageSize', 200).set('from', from.toISOString()).set('to', to.toISOString());
      if (assetId) params = params.set('assetId', assetId);
      return this.http.get<AssetMovementPageDto>(`${TRACKING_API}tag-ids/asset-movement`, { params });
    };
    return page(1).pipe(
      switchMap((first) =>
        first.totalPages > 1
          ? forkJoin(Array.from({ length: first.totalPages - 1 }, (_, i) => page(i + 2))).pipe(map((rest) => [first, ...rest].flatMap((p) => p.items)))
          : of(first.items),
      ),
      catchError(() => of([] as AssetMovementDto[])),
    );
  }

  /**
   * Overview: the resolved zone visits of the last `days` days (Tracking service, from the worker-resolved zone readings)
   * plus manually recorded location events, and the registered assets (Asset API) they refer to.
   */
  overview(days = 1): Observable<{ assets: AssetListItemDto[]; events: LocationEventDto[] }> {
    return forkJoin({
      assets: this.lookup.list(),
      manual: this.http.get<LocationEventDto[]>(`${TRACKING_API}location-events`).pipe(catchError(() => of([] as LocationEventDto[]))),
      // the previous period is loaded too (for the +/- % deltas), capped at the API's 31 days
      movement: this.fetchMovement(Math.max(days * 2, 2)),
    }).pipe(
      map(({ assets, manual, movement }) => {
        const byReg = new Map(assets.map((a) => [a.assetRegistrationId, a]));
        const resolved = movement.flatMap((m) => {
          const a = byReg.get(m.assetRegistrationId);
          return visitsToEvents(m, a?.assetTag || m.tagIds[0] || m.assetCode || m.assetRegistrationId, a?.assetName || m.assetName || m.assetCode || '');
        });
        return { assets, events: [...manual, ...resolved] };
      }),
    );
  }

  /** Records a movement as a location event. */
  record(asset: Pick<AssetRecord, 'tag' | 'name'>, m: { at: string; location: string; movement: MovementType; remarks: string; source: string }): Observable<LocationEventDto> {
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
