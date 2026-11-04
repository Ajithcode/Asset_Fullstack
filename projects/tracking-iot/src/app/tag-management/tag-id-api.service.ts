import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, map, of } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { apiError, tenantBody, tenantParams, trackingUrl } from '../shared/tracking-api';
import { TagId, TagStatus, TagType, Tech } from './tag-data';

/** A tag as GET/POST/PUT /api/tag-ids returns it (addressed by tagId). */
export interface TagIdDto {
  id: string;
  tagId: string;
  tagType: string;
  technology: string;
  assetRegistrationId: string | null;
  assetName: string | null;
  siteId: string | null;
  siteName: string | null;
  locationName: string | null;
  status: string | null;
  lastSeenAt: string | null;
  createdBy: string | null;
  createdAt: string | null;
  clientId: string | null;
  tenantId: string | null;
}

/** Body of POST /api/tag-ids and PUT /api/tag-ids/{tagId} (same shape; tagId must match the URL on PUT). */
export interface TagIdRequest {
  tagId: string;
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

/** GET /api/tag-ids/technologies */
export interface TagTechnologyOption {
  technology: string;
  idPrefix: string;
  tagTypes: string[];
}

/** A master-data site, for the site pickers. */
export interface SiteOption {
  id: string;
  name: string;
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
export function tagFromDto(dto: TagIdDto): TagId {
  const registeredOn = epoch(dto.createdAt) ?? Date.now();
  const lastSeen = epoch(dto.lastSeenAt);
  const tech = dto.technology as Tech;
  const status: TagStatus = (['Assigned', 'Available', 'Inactive'] as TagStatus[]).find((s) => s === dto.status) ?? (dto.assetRegistrationId ? 'Assigned' : 'Available');
  const by = dto.createdBy || '—';
  return {
    id: dto.tagId,
    tech,
    type: pageType(dto.tagType),
    code: '',
    asset: dto.assetRegistrationId ? { tag: dto.assetRegistrationId, kind: dto.assetName?.trim() || 'Asset', real: false } : null,
    site: dto.siteName ?? '',
    location: dto.locationName ?? '',
    status,
    assignedOn: null,
    lastSeen,
    reader: '',
    registeredOn,
    history: [
      ...(lastSeen !== null ? [{ at: lastSeen, text: 'Last read by a reader', by: 'System' }] : []),
      { at: registeredOn, text: `Registered as ${tech} ${dto.tagType.toLowerCase()}`, by },
    ],
    siteId: dto.siteId,
    apiTagType: dto.tagType,
  };
}

/** Tag Management → /api/tag-ids (Tracking service). */
@Injectable({ providedIn: 'root' })
export class TagIdApiService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly url = trackingUrl('tag-ids');

  /** Name recorded on changes: the signed-in user, or the stored e-mail. */
  currentUser(): string {
    return this.auth.getUser()?.userName || this.auth.getUserEmail() || 'Unknown user';
  }

  /** GET /api/tag-ids */
  getAll(): Observable<TagId[]> {
    return this.http.get<TagIdDto[]>(this.url, { params: tenantParams() }).pipe(
      map((rows) => (rows ?? []).map(tagFromDto)),
      catchError((e) => apiError(e, 'Could not load tags.')),
    );
  }

  /** GET /api/tag-ids/{tagId} */
  get(tagId: string): Observable<TagId> {
    return this.http.get<TagIdDto>(`${this.url}/${encodeURIComponent(tagId)}`, { params: tenantParams() }).pipe(
      map(tagFromDto),
      catchError((e) => apiError(e, `Could not load ${tagId}.`)),
    );
  }

  /** GET /api/tag-ids/technologies */
  technologies(): Observable<TagTechnologyOption[]> {
    return this.http.get<TagTechnologyOption[]>(`${this.url}/technologies`, { params: tenantParams() }).pipe(
      catchError((e) => apiError(e, 'Could not load technologies.')),
    );
  }

  /** Master-data sites for the site pickers; empty when the service cannot be reached. */
  sites(): Observable<SiteOption[]> {
    return this.http.get<Record<string, unknown>[]>(`${environment.assetMasterDataApiUrl}api/master-data/sites`).pipe(
      map((rows) => (Array.isArray(rows) ? rows : [])
        .filter((r) => r['id'] && r['isDeleted'] !== true)
        .map((r) => ({ id: String(r['id']), name: String(r['siteName'] ?? r['name'] ?? r['id']) }))),
      catchError(() => of([])),
    );
  }

  /** POST /api/tag-ids */
  create(t: TagId): Observable<TagId> {
    return this.http.post<TagIdDto>(this.url, this.request(t), { params: tenantParams() }).pipe(
      map(tagFromDto),
      catchError((e) => apiError(e, `Could not register ${t.id}.`)),
    );
  }

  /** PUT /api/tag-ids/{tagId}: full replace of the tag's fields (the tag id itself cannot change). */
  update(t: TagId): Observable<TagId> {
    return this.http.put<TagIdDto>(`${this.url}/${encodeURIComponent(t.id)}`, this.request(t), { params: tenantParams() }).pipe(
      map(tagFromDto),
      catchError((e) => apiError(e, `Could not save ${t.id}.`)),
    );
  }

  /** DELETE /api/tag-ids/{tagId} */
  delete(tagId: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${encodeURIComponent(tagId)}`, { params: tenantParams() }).pipe(
      catchError((e) => apiError(e, `Could not delete ${tagId}.`)),
    );
  }

  private request(t: TagId): TagIdRequest {
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
