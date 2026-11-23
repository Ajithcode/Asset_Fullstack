import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, Subject, catchError, forkJoin, map, of, switchMap, tap } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { apiError, getAllPages, tenantBody, tenantParams, trackingUrl } from '../shared/tracking-api';
import { LinkedAsset, TagEvent, TagId, TagStatus, TagType, Tech } from './tag-data';

/**
 * A tag as /api/tag-ids returns it (addressed by tagId). List rows carry registeredOn, value and validation;
 * single-tag responses may still carry id / createdAt / createdBy instead, so those fields are all optional.
 */
export interface TagIdDto {
  id?: string;
  tagId: string;
  tagType: string;
  technology: string;
  /** Name of the encoded value for the technology (EPC, MAC Address, ...). */
  valueLabel?: string | null;
  /** The encoded value (EPC / barcode / QR payload / MAC). */
  value?: string | null;
  registeredOn?: string | null;
  /** Server-side validation: "Valid" / "Invalid", with the reason. */
  validation?: string | null;
  validationMessage?: string | null;
  assetRegistrationId: string | null;
  assetCode?: string | null;
  assetName: string | null;
  siteId: string | null;
  siteName: string | null;
  locationName: string | null;
  status: string | null;
  assignedAt?: string | null;
  lastSeenAt: string | null;
  createdBy?: string | null;
  createdAt?: string | null;
  clientId?: string | null;
  tenantId?: string | null;
  /** Single-tag responses (GET/assign/unassign/...) nest the asset and location instead of the flat list fields. */
  linkedAsset?: { assetRegistrationId: string; assetCode?: string | null; assetName?: string | null; assignedAt?: string | null; assignedBy?: string | null } | null;
  location?: { siteId?: string | null; siteName?: string | null; locationName?: string | null; lastSeenAt?: string | null } | null;
  registeredBy?: string | null;
}

/** Brings a single-tag response (nested linkedAsset / location) to the flat list shape. */
function flatten(dto: TagIdDto): TagIdDto {
  const a = dto.linkedAsset;
  const l = dto.location;
  if (a === undefined && l === undefined) return dto;
  return {
    ...dto,
    assetRegistrationId: a?.assetRegistrationId ?? null,
    assetCode: a?.assetCode ?? null,
    assetName: a?.assetName ?? null,
    assignedAt: a?.assignedAt ?? null,
    siteId: l?.siteId ?? null,
    siteName: l?.siteName ?? null,
    locationName: l?.locationName ?? null,
    lastSeenAt: l?.lastSeenAt ?? null,
    createdBy: dto.createdBy ?? dto.registeredBy ?? null,
  };
}

/** GET /api/tag-ids (paged). */
export interface TagIdPageDto {
  items: TagIdDto[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

/** Page size used to read the whole list (the page filters and pages it itself). */
const LIST_PAGE_SIZE = 100;

/** Body of POST /api/tag-ids and PUT /api/tag-ids/{tagId} (same shape; tagId must match the URL on PUT). */
export interface TagIdRequest {
  tagId: string;
  /** The number part of the tag id (BLE-000123 → "000123"; a device id kept as entered). Required. */
  tagNumber: string;
  /** The encoded value (EPC / MAC / barcode / QR payload / UID). Required. */
  value: string | null;
  tagType: string;
  technology: string;
  assetRegistrationId: string | null;
  assetName: string | null;
  siteId: string | null;
  siteName: string | null;
  locationName: string | null;
  status: TagStatus;
  lastSeenAt: string | null;
  createdBy: string;
  clientId: string;
  tenantId: string;
}

/** GET /api/tag-ids/options (technologies, their id prefix and tag types) */
export interface TagTechnologyOption {
  technology: string;
  idPrefix: string;
  /** Name of the encoded value (EPC, MAC Address, UID, Encoded Value). */
  valueLabel: string;
  /** Tag types the technology comes in, as the API names them ("Barcode Label", "Hard Tag", ...). */
  tagTypes: string[];
}

/** One row of GET {assetApiUrl}api/asset-registrations (Asset service). */
interface AssetListItemDto {
  id: string;
  assetRegistrationId: string | null;
  assetName: string | null;
  assetTag: string | null;
}

/** A master-data site, for the site pickers. */
export interface SiteOption {
  id: string;
  name: string;
}

/** A master-data location for the Location picker; `siteName` is null when it is not tied to a site. */
export interface LocationOption {
  name: string;
  siteName: string | null;
}

/** The page's tag type for the API's text ("Barcode Label", "QR Label", "Label", "Hard Tag", "Beacon"). */
export function pageType(tagType: string): TagType {
  const t = (tagType ?? '').toLowerCase();
  return t.includes('beacon') ? 'Beacon' : t.includes('hard') ? 'Hard Tag' : 'Label';
}

/** The API's tag type text for a page type: labels are named after their technology ("Barcode Label", "QR Label"). */
export function apiType(tech: Tech, type: TagType): string {
  if (type !== 'Label') return type;
  return tech === 'Barcode' ? 'Barcode Label' : tech === 'QR Code' ? 'QR Label' : 'Label';
}

function epoch(iso: string | null | undefined): number | null {
  if (!iso) return null;
  const ms = Date.parse(iso);
  return Number.isNaN(ms) ? null : ms;
}

/**
 * A page tag for an API tag. The API keeps no encoded value (EPC / barcode / MAC), reader, assignment date,
 * telemetry or change history: the value is empty (shown as "—"), and the history holds what the record itself
 * says (registration and last read).
 */
export function tagFromDto(raw: TagIdDto): TagId {
  const dto = flatten(raw);
  const registeredOn = epoch(dto.registeredOn) ?? epoch(dto.createdAt) ?? Date.now();
  const lastSeen = epoch(dto.lastSeenAt);
  const assignedOn = epoch(dto.assignedAt);
  const tech = dto.technology as Tech;
  const status: TagStatus = (['Assigned', 'Available', 'Inactive'] as TagStatus[]).find((s) => s === dto.status) ?? (dto.assetRegistrationId ? 'Assigned' : 'Available');
  const by = dto.createdBy || '—';
  return {
    id: dto.tagId,
    tech,
    type: pageType(dto.tagType),
    code: dto.value ?? '',
    asset: dto.assetRegistrationId ? { tag: dto.assetCode || dto.assetRegistrationId, kind: dto.assetName?.trim() || 'Asset', real: false } : null,
    site: dto.siteName ?? '',
    location: dto.locationName ?? '',
    status,
    assignedOn,
    lastSeen,
    reader: '',
    registeredOn,
    history: [
      ...(lastSeen !== null ? [{ at: lastSeen, text: 'Last read by a reader', by: 'System' }] : []),
      ...(assignedOn !== null && dto.assetName ? [{ at: assignedOn, text: `Assigned to ${dto.assetName}`, by }] : []),
      { at: registeredOn, text: `Registered as ${tech} ${dto.tagType.toLowerCase()}`, by },
    ],
    siteId: dto.siteId,
    apiTagType: dto.tagType,
    apiValidation: dto.validation ?? null,
    apiValidationMessage: dto.validationMessage ?? null,
    valueLabel: dto.valueLabel ?? null,
  };
}

/** A row of GET /api/tag-ids/audit-trail and of GET /api/tag-ids/{tagId}/history. */
export interface TagEventDto {
  id: string;
  when: string;
  tagId: string;
  technology: string;
  /** Registered, Assigned, Reassigned, Unassigned, Updated, Read, Deactivated, Reactivated or Deleted. */
  event: string;
  details: string;
  assetRegistrationId: string | null;
  assetCode: string | null;
  assetName: string | null;
  statusAfter: string | null;
  performedBy: string;
}

/** GET /api/tag-ids/audit-trail/options */
export interface TagAuditOptionsDto {
  eventTypes: string[];
  technologies: string[];
  performedBy: string[];
  periods: string[];
}

/** A history entry of the page for an API event (the details text, e.g. "Assigned to Table (TB123)"). */
export function tagEventFromDto(e: TagEventDto): TagEvent {
  return { at: epoch(e.when) ?? 0, text: e.details?.trim() || e.event, by: e.performedBy || '—' };
}

/** One KPI of GET /api/tag-ids/summary: this month's count, last month's and the change in %. */
export interface TagSummaryCountDto {
  count: number;
  lastMonthCount: number;
  changePercent: number;
}

/** GET /api/tag-ids/summary */
export interface TagSummaryDto {
  total: TagSummaryCountDto;
  assigned: TagSummaryCountDto;
  available: TagSummaryCountDto;
  inactive: TagSummaryCountDto;
  failingValidation: number;
  byTechnology: Record<string, number>;
}

export type TagSummaryKey = 'Total' | 'Assigned' | 'Available' | 'Inactive';

/** The summary as the page uses it: KPI counts with their change vs last month. */
export interface TagSummary {
  counts: Record<TagSummaryKey, number>;
  /** Change vs last month in % (null when the API gave none). */
  change: Record<TagSummaryKey, number | null>;
  failingValidation: number;
  byTechnology: Record<string, number>;
}

export function summaryFromDto(dto: TagSummaryDto | null): TagSummary | null {
  if (!dto?.total) return null;
  const n = (v: unknown) => (typeof v === 'number' && Number.isFinite(v) ? v : null);
  const parts: [TagSummaryKey, TagSummaryCountDto | undefined][] = [
    ['Total', dto.total],
    ['Assigned', dto.assigned],
    ['Available', dto.available],
    ['Inactive', dto.inactive],
  ];
  const counts = {} as Record<TagSummaryKey, number>;
  const change = {} as Record<TagSummaryKey, number | null>;
  for (const [k, v] of parts) {
    counts[k] = n(v?.count) ?? 0;
    change[k] = n(v?.changePercent);
  }
  return { counts, change, failingValidation: n(dto.failingValidation) ?? 0, byTechnology: dto.byTechnology ?? {} };
}

/** The tag id as the user typed it; the API stores it as entered (no prefix is added). */
export function tagNumberOf(t: { id: string }): string {
  return t.id;
}

/** Tag Management → /api/tag-ids (Tracking service). */
@Injectable({ providedIn: 'root' })
export class TagIdApiService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly url = trackingUrl('tag-ids');
  /** Emits after a tag is created, updated or deleted (the summary counts are then stale). */
  readonly changed$ = new Subject<void>();

  /** Name recorded on changes: the signed-in user, or the stored e-mail. */
  currentUser(): string {
    return this.auth.getUser()?.userName || this.auth.getUserEmail() || 'Unknown user';
  }

  /** GET /api/tag-ids: every page (the list is paged; a plain array is accepted too). */
  getAll(): Observable<TagId[]> {
    const page = (n: number) =>
      this.http.get<TagIdPageDto | TagIdDto[]>(this.url, { params: tenantParams({ page: n, pageSize: LIST_PAGE_SIZE }) });
    const rowsOf = (res: TagIdPageDto | TagIdDto[]) => (Array.isArray(res) ? res : res?.items ?? []);
    return page(1).pipe(
      switchMap((first) => {
        const pages = Array.isArray(first) ? 1 : Math.max(1, first?.totalPages ?? 1);
        if (pages <= 1) return of(rowsOf(first));
        const rest = Array.from({ length: pages - 1 }, (_, i) => page(i + 2));
        return forkJoin(rest).pipe(map((more) => [first, ...more].flatMap(rowsOf)));
      }),
      map((rows) => rows.map(tagFromDto)),
      catchError((e) => apiError(e, 'Could not load tags.')),
    );
  }

  /** GET /api/tag-ids/summary: KPI counts; null when it fails or returns no counts (the page then counts the loaded list). */
  summary(): Observable<TagSummary | null> {
    return this.http.get<TagSummaryDto>(`${this.url}/summary`, { params: tenantParams() }).pipe(
      map(summaryFromDto),
      catchError(() => of(null)),
    );
  }

  /** GET /api/tag-ids/{tagId} */
  get(tagId: string): Observable<TagId> {
    return this.http.get<TagIdDto>(`${this.url}/${encodeURIComponent(tagId)}`, { params: tenantParams() }).pipe(
      map(tagFromDto),
      catchError((e) => apiError(e, `Could not load ${tagId}.`)),
    );
  }

  /** GET /api/tag-ids/audit-trail?period=all: every recorded tag event (all pages), newest first. */
  auditTrail(): Observable<TagEventDto[]> {
    return getAllPages<TagEventDto>(this.http, `${this.url}/audit-trail`, { period: 'all' }, 200).pipe(
      catchError((e) => apiError(e, 'Could not load the audit trail.')),
    );
  }

  /** GET /api/tag-ids/audit-trail/options: event types and people for the Audit Trail filters; null when it fails. */
  auditOptions(): Observable<TagAuditOptionsDto | null> {
    return this.http.get<TagAuditOptionsDto>(`${this.url}/audit-trail/options`, { params: tenantParams() }).pipe(catchError(() => of(null)));
  }

  /** GET /api/tag-ids/{tagId}/history: the tag's events, newest first. */
  history(tagId: string): Observable<TagEvent[]> {
    return this.http.get<TagEventDto[]>(`${this.url}/${encodeURIComponent(tagId)}/history`, { params: tenantParams() }).pipe(
      map((rows) => (rows ?? []).map(tagEventFromDto).sort((a, b) => b.at - a.at)),
      catchError((e) => apiError(e, `Could not load the history of ${tagId}.`)),
    );
  }

  /** GET /api/tag-ids/options (technologies, their id prefix and tag types) */
  technologies(): Observable<TagTechnologyOption[]> {
    // GET /api/tag-ids/options: the technologies as a list, or under `technologies` with the other dropdown options
    return this.http.get<unknown>(`${this.url}/options`, { params: tenantParams() }).pipe(
      map((res) => {
        const list = Array.isArray(res) ? res : (res as { technologies?: unknown } | null)?.technologies;
        return (Array.isArray(list) ? list : [])
          .map((o): TagTechnologyOption | null => {
            if (typeof o === 'string') return { technology: o, idPrefix: '', valueLabel: '', tagTypes: [] };
            const r = o as Partial<TagTechnologyOption> & { name?: string; value?: string; prefix?: string };
            const technology = r.technology ?? r.name ?? r.value;
            return technology ? { technology, idPrefix: r.idPrefix ?? r.prefix ?? '', valueLabel: r.valueLabel ?? '', tagTypes: Array.isArray(r.tagTypes) ? r.tagTypes : [] } : null;
          })
          .filter((o): o is TagTechnologyOption => !!o);
      }),
      catchError((e) => apiError(e, 'Could not load technologies.')),
    );
  }


  /** Master-data sites for the site pickers; empty when the service cannot be reached. */
  sites(): Observable<SiteOption[]> {
    type SiteRow = { id?: string; siteName?: string; name?: string; status?: string; isDeleted?: boolean };
    // Master Data service (GET /api/master-data/sites); a list or a page of { items }
    return this.http.get<SiteRow[] | { items?: SiteRow[] }>(`${environment.masterDataApiUrl}api/master-data/sites`, { params: tenantParams() }).pipe(
      map((res) => (Array.isArray(res) ? res : res?.items ?? [])),
      map((rows) =>
        rows
          .filter((r) => r.id && r.isDeleted !== true && !/^(inactive|disabled|deleted)$/i.test(r.status ?? ''))
          .map((r) => ({ id: String(r.id), name: String(r.siteName ?? r.name ?? r.id) })),
      ),
      catchError(() => of([])),
    );
  }

  /**
   * Locations for the Location picker: GET /api/master-data/locations (MDM > Organization > Location). A location
   * names its building by code or name; the building's site (GET /api/master-data/buildings) tells which site it is
   * in. A location without a (known) building has no site and is offered for every site.
   */
  locations(): Observable<LocationOption[]> {
    type Row = { id?: string; locationName?: string; building?: string | null; status?: string; isDeleted?: boolean };
    type BuildingRow = { code?: string; name?: string; site?: { name?: string | null } | null };
    const list = <T>(resource: string) =>
      this.http.get<T[] | { items?: T[] }>(`${environment.masterDataApiUrl}api/master-data/${resource}`, { params: tenantParams() }).pipe(
        map((res) => (Array.isArray(res) ? res : res?.items ?? [])),
        catchError(() => of([] as T[])),
      );
    const same = (a?: string | null, b?: string | null) => !!a && !!b && a.trim().toLowerCase() === b.trim().toLowerCase();
    return forkJoin({ rows: list<Row>('locations'), buildings: list<BuildingRow>('buildings') }).pipe(
      map(({ rows, buildings }) =>
        rows
          .filter((r) => r.id && r.locationName && r.isDeleted !== true && !/^(inactive|disabled|deleted)$/i.test(r.status ?? ''))
          .map((r) => {
            const b = r.building ? buildings.find((x) => same(x.code, r.building) || same(x.name, r.building)) : undefined;
            return { name: String(r.locationName), siteName: b?.site?.name ?? null };
          }),
      ),
    );
  }

  /** Registered assets for the "Link to asset" pickers (Asset service, first 1000); empty when it cannot be reached. */
  assets(): Observable<LinkedAsset[]> {
    const params = tenantParams({ page: 1, pageSize: 1000 });
    return this.http.get<AssetListItemDto[] | { items?: AssetListItemDto[] }>(`${environment.assetApiUrl}api/asset-registrations`, { params }).pipe(
      map((res) => (Array.isArray(res) ? res : res?.items ?? [])
        .map((r) => ({ tag: (r.assetTag || r.assetRegistrationId || '').trim(), kind: r.assetName?.trim() || 'Asset', real: true }))
        .filter((a) => !!a.tag)
        .sort((a, b) => a.kind.localeCompare(b.kind) || a.tag.localeCompare(b.tag))),
      catchError(() => of([])),
    );
  }

  /** POST /api/tag-ids */
  create(t: TagId): Observable<TagId> {
    return this.http.post<TagIdDto>(this.url, this.request(t), { params: tenantParams() }).pipe(
      map(tagFromDto),
      tap(() => this.changed$.next()),
      catchError((e) => apiError(e, `Could not register ${t.id}.`)),
    );
  }

  /** PUT /api/tag-ids/{tagId}: full replace of the tag's fields (the tag id itself cannot change). */
  update(t: TagId): Observable<TagId> {
    return this.http.put<TagIdDto>(`${this.url}/${encodeURIComponent(t.id)}`, this.request(t), { params: tenantParams() }).pipe(
      map(tagFromDto),
      tap(() => this.changed$.next()),
      catchError((e) => apiError(e, `Could not save ${t.id}.`)),
    );
  }

  /** DELETE /api/tag-ids/{tagId}; with unassign, an assigned tag is unassigned and deleted in one call. */
  delete(tagId: string, unassign = false): Observable<void> {
    return this.http.delete<void>(`${this.url}/${encodeURIComponent(tagId)}`, { params: tenantParams({ unassign: unassign || null }) }).pipe(
      tap(() => this.changed$.next()),
      catchError((e) => apiError(e, `Could not delete ${tagId}.`)),
    );
  }

  /** POST /api/tag-ids/{tagId}/assign: links (or relinks) the tag to an asset; the server sets it Assigned. */
  assign(tagId: string, assetCode: string, site: { siteId: string | null; siteName: string | null; locationName: string | null }, note?: string): Observable<TagId> {
    return this.action(tagId, 'assign', { assetCode, ...site, note: note?.trim() || null }, `Could not assign ${tagId}.`);
  }

  /** POST /api/tag-ids/{tagId}/unassign: unlinks the tag; the server sets it Available. */
  unassign(tagId: string, reason?: string): Observable<TagId> {
    return this.action(tagId, 'unassign', { reason: reason?.trim() || null }, `Could not unassign ${tagId}.`);
  }

  /** POST /api/tag-ids/{tagId}/deactivate (manual status Inactive; an assigned tag is unassigned first). */
  deactivate(tagId: string, reason: string): Observable<TagId> {
    return this.action(tagId, 'deactivate', { reason: reason.trim() || 'Deactivated' }, `Could not deactivate ${tagId}.`);
  }

  /** POST /api/tag-ids/{tagId}/reactivate (back to Available). */
  reactivate(tagId: string): Observable<TagId> {
    return this.action(tagId, 'reactivate', {}, `Could not reactivate ${tagId}.`);
  }

  private action(tagId: string, path: string, body: object, failure: string): Observable<TagId> {
    return this.http.post<TagIdDto>(`${this.url}/${encodeURIComponent(tagId)}/${path}`, body, { params: tenantParams() }).pipe(
      map(tagFromDto),
      tap(() => this.changed$.next()),
      catchError((e) => apiError(e, failure)),
    );
  }

  private request(t: TagId): TagIdRequest & { assetCode: string | null } {
    // The API links the asset given as assetCode (asset tag or registration id) and sets the status itself.
    const assetCode = t.status === 'Assigned' && t.asset ? t.asset.tag : null;
    return { ...this.requestBody(t), tagNumber: tagNumberOf(t), value: t.code.trim() || null, assetCode };
  }

  private requestBody(t: TagId): Omit<TagIdRequest, 'tagNumber' | 'value'> {
    const assigned = t.status === 'Assigned' && !!t.asset;
    const kind = t.asset?.kind;
    return {
      tagId: t.id,
      // keep the API's own type text while the page type is unchanged
      tagType: t.apiTagType && pageType(t.apiTagType) === t.type ? t.apiTagType : apiType(t.tech, t.type),
      technology: t.tech,
      // the API only accepts a linked asset on an Assigned tag
      assetRegistrationId: assigned ? t.asset!.tag : null,
      assetName: assigned && kind && kind !== 'Asset' ? kind : null,
      siteId: t.site ? t.siteId ?? null : null,
      siteName: t.site || null,
      locationName: t.location || null,
      status: t.status === 'Assigned' && !t.asset ? 'Available' : t.status,
      lastSeenAt: t.lastSeen !== null ? new Date(t.lastSeen).toISOString() : null,
      createdBy: this.currentUser(),
      ...tenantBody(),
    };
  }
}
