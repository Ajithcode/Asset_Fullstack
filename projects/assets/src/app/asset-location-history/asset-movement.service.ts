import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { EMPTY, Observable, catchError, expand, forkJoin, map, of, reduce } from 'rxjs';
import { environment } from 'shared-ui';
import { AssetListItemDto, AssetLookupService } from '../asset-maintenance/asset-lookup.service';
import { LocationEventDto } from './location-history.service';

/** GET {tracking}/api/tag-ids/asset-movement */
const URL = `${environment.trackingApiUrl}api/tag-ids/asset-movement`;
const PAGE_SIZE = 100;
const MAX_PAGES = 50;
/** The API accepts at most 31 days per call, so longer periods are split into 30-day windows. */
const WINDOW_DAYS = 30;
const DAY_MS = 86_400_000;

/** One asset of GET /api/tag-ids/asset-movement. */
export interface AssetMovementItemDto {
  assetRegistrationId: string;
  assetCode: string | null;
  assetName: string | null;
  tagIds: string[] | null;
  gatewayCount: number;
  zoneCount: number;
  movementCount: number;
  readingCount: number;
  firstSeenAt: string | null;
  lastSeenAt: string | null;
  /** Latest position (shape not fixed yet: a zone name, or an object with the zone / gateway). */
  currentLocation: unknown;
  gateways: unknown[] | null;
  /** Sightings / movements when history=true (null when none). */
  history: unknown[] | null;
}

export interface AssetMovementPageDto {
  from: string;
  to: string;
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
  items: AssetMovementItemDto[];
}

/** Tracking figures of one asset, keyed by the asset ID the page shows. */
export interface AssetTracking {
  registrationId: string;
  tagIds: string[];
  movementCount: number;
  gatewayCount: number;
  zoneCount: number;
  firstSeenAt: string | null;
  lastSeenAt: string | null;
}

export interface AssetMovementLoad {
  /** Every asset the API returned (registry details merged in when the registration is found). */
  assets: AssetListItemDto[];
  /** Their movements / sightings, oldest first not required. */
  events: LocationEventDto[];
  tracking: Map<string, AssetTracking>;
  from: string;
  to: string;
}

type Obj = Record<string, unknown>;

/** First non-empty text among the fields (case-insensitive), looking one level into nested objects. */
function text(value: unknown, ...keys: string[]): string {
  if (value === null || value === undefined) return '';
  if (typeof value !== 'object') return String(value);
  const entries = Object.entries(value as Obj);
  for (const key of keys) {
    const hit = entries.find(([k]) => k.toLowerCase() === key.toLowerCase());
    const v = hit?.[1];
    if (v !== null && v !== undefined && v !== '') {
      if (typeof v !== 'object') return String(v);
      const inner = text(v, ...keys);
      if (inner) return inner;
    }
  }
  return '';
}

const ZONE_KEYS = ['zoneName', 'zone', 'locationName', 'location', 'name', 'areaName', 'roomName', 'gatewayName', 'gatewayLocation', 'gateway'];
const TIME_KEYS = ['seenAt', 'eventTime', 'timestamp', 'movedAt', 'enteredAt', 'detectedAt', 'readAt', 'recordedAt', 'lastSeenAt', 'time', 'createdAt'];

/** Technology of a tag from its id prefix (BLE-…, RFID-…, QR-…, BC- barcode, GPS-…). */
export function tagSource(tagId: string): string {
  const p = tagId.split('-')[0].toUpperCase();
  return p === 'BLE' ? 'BLE' : p === 'RFID' ? 'RFID' : p === 'QR' || p === 'BC' ? 'QR Scan' : p === 'GPS' ? 'GPS' : '';
}

@Injectable({ providedIn: 'root' })
export class AssetMovementService {
  private readonly http = inject(HttpClient);
  private readonly lookup = inject(AssetLookupService);

  /** Every page of GET /api/tag-ids/asset-movement?history=true for the window. */
  private all(from: string, to: string): Observable<AssetMovementItemDto[]> {
    const get = (page: number) =>
      this.http.get<AssetMovementPageDto>(URL, {
        params: new HttpParams()
          .set('history', true)
          .set('from', from)
          .set('to', to)
          .set('page', page)
          .set('pageSize', PAGE_SIZE)
          .set('clientId', environment.clientId)
          .set('tenantId', environment.tenantId),
      });
    return get(1).pipe(
      expand((res) => (res && res.page < Math.min(res.totalPages, MAX_PAGES) ? get(res.page + 1) : EMPTY)),
      map((res) => (Array.isArray(res) ? (res as AssetMovementItemDto[]) : res?.items ?? [])),
      reduce((all, rows) => all.concat(rows), [] as AssetMovementItemDto[]),
    );
  }

  /** Every asset over the last `days` days: one call per 30-day window, merged per asset. */
  private period(days: number): Observable<AssetMovementItemDto[]> {
    const end = Date.now();
    const start = end - days * DAY_MS;
    const calls: Observable<AssetMovementItemDto[]>[] = [];
    for (let to = end; to > start; to -= WINDOW_DAYS * DAY_MS) {
      const from = Math.max(start, to - WINDOW_DAYS * DAY_MS);
      calls.push(this.all(new Date(from).toISOString(), new Date(to).toISOString()));
    }
    return forkJoin(calls).pipe(map((windows) => mergeWindows(windows.flat())));
  }

  /** The tracked assets with their movements over the last `days` days, plus their registry details (type, category, site, status). */
  load(days = WINDOW_DAYS): Observable<AssetMovementLoad> {
    const to = new Date().toISOString();
    const from = new Date(Date.now() - days * DAY_MS).toISOString();
    return forkJoin({
      items: this.period(days),
      // the page still shows the tracking data without the registry
      registry: this.lookup.list().pipe(catchError(() => of([] as AssetListItemDto[]))),
    }).pipe(
      map(({ items, registry }) => {
        const byReg = new Map(registry.map((r) => [r.assetRegistrationId.toUpperCase(), r]));
        const assets: AssetListItemDto[] = [];
        const events: LocationEventDto[] = [];
        const tracking = new Map<string, AssetTracking>();
        for (const it of items) {
          const reg = byReg.get((it.assetRegistrationId ?? '').toUpperCase());
          const tag = reg?.assetTag || it.assetCode || it.assetRegistrationId;
          const tagIds = it.tagIds ?? [];
          assets.push(reg ? { ...reg, assetTag: tag, assetName: reg.assetName || it.assetName } : blankAsset(it, tag));
          tracking.set(tag.toUpperCase(), {
            registrationId: it.assetRegistrationId,
            tagIds,
            movementCount: it.movementCount ?? 0,
            gatewayCount: it.gatewayCount ?? 0,
            zoneCount: it.zoneCount ?? 0,
            firstSeenAt: it.firstSeenAt,
            lastSeenAt: it.lastSeenAt,
          });
          events.push(...toEvents(it, tag, tagIds));
        }
        return { assets, events, tracking, from, to };
      }),
    );
  }
}

/** One row per asset from several windows: history joined, counts added, the latest current location kept. */
function mergeWindows(rows: AssetMovementItemDto[]): AssetMovementItemDto[] {
  const byId = new Map<string, AssetMovementItemDto>();
  for (const r of rows) {
    const m = byId.get(r.assetRegistrationId);
    if (!m) {
      byId.set(r.assetRegistrationId, { ...r, history: [...(r.history ?? [])] });
      continue;
    }
    m.history = [...(m.history ?? []), ...(r.history ?? [])];
    m.movementCount = (m.movementCount ?? 0) + (r.movementCount ?? 0);
    m.readingCount = (m.readingCount ?? 0) + (r.readingCount ?? 0);
    m.gatewayCount = Math.max(m.gatewayCount ?? 0, r.gatewayCount ?? 0);
    m.zoneCount = Math.max(m.zoneCount ?? 0, r.zoneCount ?? 0);
    m.tagIds = [...new Set([...(m.tagIds ?? []), ...(r.tagIds ?? [])])];
    if (r.firstSeenAt && (!m.firstSeenAt || r.firstSeenAt < m.firstSeenAt)) m.firstSeenAt = r.firstSeenAt;
    if (r.lastSeenAt && (!m.lastSeenAt || r.lastSeenAt > m.lastSeenAt)) {
      m.lastSeenAt = r.lastSeenAt;
      m.currentLocation = r.currentLocation ?? m.currentLocation;
    }
  }
  return [...byId.values()];
}

function blankAsset(it: AssetMovementItemDto, tag: string): AssetListItemDto {
  return {
    id: it.assetRegistrationId, assetRegistrationId: it.assetRegistrationId, assetName: it.assetName, assetTag: tag, imageUrl: null,
    category: null, assetType: null, siteName: null, buildingName: null, floorName: null, zoneName: null, roomName: null, locationPath: null,
    custodianUserId: null, custodianName: null, department: null, status: null, criticality: null, manufacturer: null, nextMaintenanceDate: null, createdAt: null,
  };
}

/** The asset's history entries as location events; without history, its current location at lastSeenAt. */
function toEvents(it: AssetMovementItemDto, tag: string, tagIds: string[]): LocationEventDto[] {
  const name = it.assetName || tag;
  const make = (entry: unknown, i: number, fallbackTime: string | null): LocationEventDto | null => {
    const time = text(entry, ...TIME_KEYS) || fallbackTime || '';
    const zone = text(entry, ...ZONE_KEYS);
    if (!time || isNaN(new Date(time).getTime()) || !zone) return null;
    const tagId = text(entry, 'tagId', 'tag') || tagIds[0] || '';
    const source = text(entry, 'source', 'technology', 'tagType') || tagSource(tagId);
    const gateway = text(entry, 'gatewayName', 'gatewayId', 'gateway');
    const type = text(entry, 'eventType', 'movementType', 'type');
    return {
      id: `${it.assetRegistrationId}-${i}`,
      eventId: text(entry, 'eventId', 'movementId', 'id') || `${it.assetRegistrationId}-${i + 1}`,
      eventTime: new Date(time).toISOString(),
      assetId: tag,
      assetName: name,
      eventType: /out|depart|exit|left/i.test(type) ? 'Departed Zone' : 'Entered Zone',
      zoneName: zone,
      details: [tagId && `Tag ${tagId}`, gateway && gateway !== zone && `via ${gateway}`, source && `(${source})`].filter(Boolean).join(' ') || null,
      createdBy: text(entry, 'createdBy', 'recordedBy', 'userName') || 'System',
      createdAt: null,
    };
  };
  const history = (it.history ?? []).map((h, i) => make(h, i, null)).filter((e): e is LocationEventDto => !!e);
  if (history.length) return history;
  const current = it.currentLocation ? make(it.currentLocation, 0, it.lastSeenAt) : null;
  return current ? [current] : [];
}
