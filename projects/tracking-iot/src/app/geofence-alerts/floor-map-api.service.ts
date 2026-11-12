import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, forkJoin, map, of } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { tenantParams } from '../shared/tracking-api';

/*
 * Floor plan data for the Geofence & Alerts map, read from the services that own it (nothing new in the backend):
 * - Master Data Management: sites, buildings, floors and zones (the Building > Floor and Zone dropdowns);
 * - Maps & Floor Plans (Administration service): the floor's plan image, the zones drawn on it and the devices /
 *   assets plotted on it;
 * - Dashboard service: live assets per gateway (the "Total Assets" count on each zone).
 */

/** A floor in the Building > Floor dropdown. */
export interface FloorOption {
  id: string;
  /** "Building A1 > BuildingA2 > Floor2" */
  label: string;
  /** Floor picture set in Master Data (used when the floor has no floor plan). */
  image: string | null;
}

/** A Master Data zone on a floor (the Zone dropdown). Its drawn outline on the plan carries the same code. */
export interface ZoneOption {
  id: string;
  name: string;
  code: string;
  floorId: string;
}

export interface FloorLocations {
  floors: FloorOption[];
  zones: ZoneOption[];
}

/** The floor plan shown for a floor: the published one, else the newest. */
export interface FloorPlanRef {
  id: string;
  locationId: string;
  name: string;
  fileUrl: string;
  status: string;
}

/** A zone drawn on the plan; geometry in % of the plan (x right, y down). */
export interface PlanZone {
  id: string;
  code: string;
  name: string;
  type: string;
  color: string;
  shape: string;
  x: number;
  y: number;
  w: number;
  h: number;
  points: { x: number; y: number }[];
  /** Circle radius in % of the plan width. */
  radius: number | null;
  active: boolean;
}

/** A device or asset plotted on the plan, at x / y in % of the plan. */
export interface PlanPlot {
  kind: 'device' | 'asset';
  itemId: string;
  code: string;
  name: string;
  type: string;
  status: string;
  x: number;
  y: number;
  zoneId: string | null;
}

/** Live assets heard by one gateway (Dashboard service, NetworkTimeSeries). */
export interface GatewayFeed {
  gatewayId: string;
  assetCount: number;
  assets: { assetId: string; assetName: string | null }[];
}

export type LiveState = 'connecting' | 'live' | 'reconnecting';

interface MdmRow {
  id: string;
  name?: string | null;
  siteName?: string | null;
  code?: string | null;
  status?: string | null;
  isDeleted?: boolean;
}
interface MdmBuilding extends MdmRow { site?: { id: string | null } | null }
interface MdmFloor extends MdmRow { buildingId?: string | null; buildingName?: string | null; site?: { id: string | null } | string | null; imageUrl?: string | null; images?: string[] | null }
interface MdmZone extends MdmRow { floorId?: string | null }
interface PlanDto { floorPlanId: string; locationId: string; name: string; fileUrl: string | null; status: string; updatedAt?: string | null; createdAt?: string | null }

type Rows<T> = T[] | { items?: T[] } | null;
const rows = <T>(r: Rows<T>): T[] => (Array.isArray(r) ? r : (r?.items ?? []));
const live = <T extends MdmRow>(list: T[]): T[] => list.filter((r) => r.isDeleted !== true && !/^deleted$/i.test(r.status ?? ''));

@Injectable({ providedIn: 'root' })
export class FloorMapApiService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly mdm = `${environment.masterDataApiUrl}api/master-data`;
  private readonly maps = `${environment.administrationApiUrl}api/administration/maps`;

  /** Floors as "Site > Building > Floor" and the zones on them. */
  locations(): Observable<FloorLocations> {
    const get = <T extends MdmRow>(what: string) => this.http.get<Rows<T>>(`${this.mdm}/${what}`).pipe(map((r) => live(rows(r))));
    return forkJoin({
      sites: get<MdmRow>('sites'),
      buildings: get<MdmBuilding>('buildings'),
      floors: get<MdmFloor>('floors'),
      zones: get<MdmZone>('zones').pipe(catchError(() => of<MdmZone[]>([]))),
    }).pipe(
      map(({ sites, buildings, floors, zones }) => {
        const siteName = new Map(sites.map((s) => [s.id, s.siteName || s.name || s.code || '']));
        const building = new Map(buildings.map((b) => [b.id, b]));
        const floorOptions = floors.map((f): FloorOption => {
          const b = building.get(f.buildingId ?? '');
          const siteId = b?.site?.id ?? (typeof f.site === 'string' ? f.site : f.site?.id) ?? '';
          const path = [siteName.get(siteId) ?? '', b?.name || f.buildingName || '', f.name || f.code || f.id].filter(Boolean);
          return { id: f.id, label: path.join(' > '), image: f.imageUrl || f.images?.find((i) => !!i) || null };
        });
        floorOptions.sort((a, b) => a.label.localeCompare(b.label));
        const zoneOptions = zones
          .filter((z) => !!z.floorId)
          .map((z): ZoneOption => ({ id: z.id, name: z.name || z.code || z.id, code: z.code || z.name || '', floorId: z.floorId! }));
        zoneOptions.sort((a, b) => a.name.localeCompare(b.name));
        return { floors: floorOptions, zones: zoneOptions };
      })
    );
  }

  /** The floor plan per location: published first, else the newest. */
  floorPlans(): Observable<Map<string, FloorPlanRef>> {
    return this.http.get<Rows<PlanDto>>(`${this.maps}/floor-plans`, { params: tenantParams() }).pipe(
      map((r) => {
        const byLocation = new Map<string, FloorPlanRef>();
        const rank = (p: PlanDto) => (p.status === 'Published' ? 2 : p.status === 'Archived' ? 0 : 1);
        const sorted = rows(r).sort((a, b) => rank(b) - rank(a) || (b.updatedAt ?? b.createdAt ?? '').localeCompare(a.updatedAt ?? a.createdAt ?? ''));
        for (const p of sorted) {
          if (!p.fileUrl || byLocation.has(p.locationId)) continue;
          byLocation.set(p.locationId, { id: p.floorPlanId, locationId: p.locationId, name: p.name, fileUrl: p.fileUrl, status: p.status });
        }
        return byLocation;
      })
    );
  }

  zones(floorPlanId: string): Observable<PlanZone[]> {
    return this.http.get<Rows<PlanZone & { zoneId: string }>>(`${this.planUrl(floorPlanId)}/zones`, { params: tenantParams() }).pipe(
      map((r) => rows(r).map((z) => ({ ...z, id: z.zoneId, points: z.points ?? [] })))
    );
  }

  plots(floorPlanId: string): Observable<PlanPlot[]> {
    return this.http.get<Rows<PlanPlot>>(`${this.planUrl(floorPlanId)}/plots`, { params: tenantParams() }).pipe(
      map((r) => rows(r).filter((p) => p.x !== null && p.y !== null).map((p) => ({ ...p, kind: p.kind === 'device' ? 'device' : 'asset' })))
    );
  }

  /**
   * GET /api/dashboards/network-status/gateways/live (WebSocket, pushes on change): assets per gateway. Reconnects
   * with backoff; the sign-in token goes in ?access_token= since browsers cannot send headers on a WebSocket.
   */
  gatewaysLive(): Observable<{ state: LiveState; data?: GatewayFeed[] }> {
    const http = `${environment.dashboardApiUrl}api/dashboards/network-status/gateways/live`;
    return new Observable((subscriber) => {
      if (typeof WebSocket === 'undefined') {
        subscriber.complete();
        return undefined;
      }
      let socket: WebSocket | null = null;
      let retries = 0;
      let stopped = false;
      let timer: ReturnType<typeof setTimeout> | undefined;
      const connect = () => {
        subscriber.next({ state: retries ? 'reconnecting' : 'connecting' });
        socket = new WebSocket(this.socketUrl(http));
        socket.onopen = () => {
          retries = 0;
          subscriber.next({ state: 'live' });
        };
        socket.onmessage = (e) => {
          try {
            subscriber.next({ state: 'live', data: JSON.parse(e.data as string) as GatewayFeed[] });
          } catch {
            // not JSON: ignore
          }
        };
        socket.onerror = () => socket?.close();
        socket.onclose = () => {
          if (stopped) return;
          retries++;
          subscriber.next({ state: 'reconnecting' });
          timer = setTimeout(connect, Math.min(30_000, 1000 * 2 ** Math.min(retries - 1, 5)));
        };
      };
      connect();
      return () => {
        stopped = true;
        clearTimeout(timer);
        if (socket && socket.readyState <= WebSocket.OPEN) socket.close(1000);
      };
    });
  }

  private planUrl(id: string): string {
    return `${this.maps}/floor-plans/${encodeURIComponent(id)}`;
  }

  private socketUrl(httpUrl: string): string {
    const url = new URL(httpUrl, typeof window !== 'undefined' ? window.location.href : undefined);
    url.protocol = url.protocol === 'https:' ? 'wss:' : 'ws:';
    const token = this.auth.getToken();
    if (token) url.searchParams.set('access_token', token);
    return url.toString();
  }
}
