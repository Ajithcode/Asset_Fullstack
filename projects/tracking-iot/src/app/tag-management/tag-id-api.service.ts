import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError } from 'rxjs';
import { ASSET_RECORDS, AuthService, environment } from 'shared-ui';
import { apiError } from '../geofence-alerts/geofence-api.service';
import { SITES, TECHS, TagId, TagStatus, TagType, Tech } from './tag-data';

/** A tag as the API stores it ({trackingApiUrl}api/tag-ids, addressed by tagId). */
export interface TagIdDto {
  id: string;
  tagId: string;
  tagType: string;
  technology: Tech;
  assetRegistrationId: string | null;
  assetName: string | null;
  siteId: string | null;
  siteName: string | null;
  locationName: string | null;
  status: TagStatus | null;
  lastSeenAt: string | null;
  createdBy: string | null;
  createdAt: string | null;
}

/** Request body for POST and PUT /api/tag-ids (same shape; tagId cannot change on PUT). */
interface TagIdRequest {
  tagId: string;
  tagType: string;
  technology: Tech;
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

/** The page's tag type for the API's free text ("Asset Tag", "Label", "Beacon", ...). */
export function typeOf(tagType: string): TagType {
  const t = tagType.toLowerCase();
  return t.includes('label') ? 'Label' : t.includes('beacon') ? 'Beacon' : 'Hard Tag';
}

/** Category of an asset name for icons: "Cooling Pump 1" → "Cooling Pump". */
function kindOf(name: string): string {
  const parts = name.trim().split(/\s+/);
  return parts.length > 1 ? parts.slice(0, -1).join(' ') : name.trim() || 'Asset';
}

/** Adds an API site (and location) the page does not list yet, so the site pickers offer it. */
export function ensureSite(name: string, code: string | null, location: string): void {
  if (!name) return;
  let site = SITES.find((s) => s.name.toLowerCase() === name.toLowerCase());
  if (!site) SITES.push((site = { name, code: code ?? '', locations: [] }));
  else if (!site.code && code) site.code = code;
  if (location && !site.locations.includes(location)) site.locations.push(location);
}

/**
 * A page tag for an API tag. The API keeps no encoded value (EPC / barcode / MAC), reader, assignment date or change
 * history: the value is empty ("Not recorded"), the assignment date is the registration date, and the history starts
 * with the registration.
 */
export function tagFromDto(dto: TagIdDto): TagId {
  const registeredOn = dto.createdAt ? Date.parse(dto.createdAt) : Date.now();
  const lastSeen = dto.lastSeenAt ? Date.parse(dto.lastSeenAt) : null;
  const tech = TECHS.includes(dto.technology) ? dto.technology : 'Passive RFID';
  const type = typeOf(dto.tagType);
  const assetTag = dto.assetRegistrationId;
  const real = !!assetTag && ASSET_RECORDS.some((a) => a.tag === assetTag);
  const status: TagStatus = dto.status ?? (assetTag ? 'Assigned' : 'Available');
  const by = dto.createdBy || 'System';
  return {
    id: dto.tagId,
    tech,
    type,
    code: '',
    asset: assetTag ? { tag: assetTag, kind: kindOf(dto.assetName ?? ''), name: dto.assetName ?? undefined, real } : null,
    site: dto.siteName ?? '',
    location: dto.locationName ?? '',
    status,
    assignedOn: status === 'Assigned' ? registeredOn : null,
    lastSeen,
    reader: '',
    registeredOn,
    history: [
      ...(lastSeen !== null ? [{ at: lastSeen, text: 'Last read by a reader', by: 'System' }] : []),
      { at: registeredOn, text: `Registered as ${tech} ${dto.tagType.toLowerCase()}`, by },
    ],
    api: { tagType: dto.tagType },
  };
}

@Injectable({ providedIn: 'root' })
export class TagIdApiService {
  private apiUrl = `${environment.trackingApiUrl}api/tag-ids`;

  constructor(private http: HttpClient, private auth: AuthService) {}

  /** GET /api/tag-ids */
  getAll(): Observable<TagIdDto[]> {
    return this.http.get<TagIdDto[]>(this.apiUrl).pipe(catchError((e) => apiError(e, 'Could not load tags.')));
  }

  /** GET /api/tag-ids/{tagId} */
  getByTagId(tagId: string): Observable<TagIdDto> {
    return this.http.get<TagIdDto>(`${this.apiUrl}/${encodeURIComponent(tagId)}`).pipe(catchError((e) => apiError(e, 'Could not load the tag.')));
  }

  /** POST /api/tag-ids */
  create(t: TagId): Observable<TagIdDto> {
    return this.http.post<TagIdDto>(this.apiUrl, this.request(t)).pipe(catchError((e) => apiError(e, `Could not register ${t.id}.`)));
  }

  /** PUT /api/tag-ids/{tagId} (full replace; the tag id itself cannot change) */
  update(t: TagId): Observable<TagIdDto> {
    return this.http.put<TagIdDto>(`${this.apiUrl}/${encodeURIComponent(t.id)}`, this.request(t)).pipe(catchError((e) => apiError(e, `Could not save ${t.id}.`)));
  }

  /** DELETE /api/tag-ids/{tagId} */
  delete(tagId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${encodeURIComponent(tagId)}`).pipe(catchError((e) => apiError(e, `Could not delete ${tagId}.`)));
  }

  private request(t: TagId): TagIdRequest {
    const a = t.asset;
    return {
      tagId: t.id,
      // keep the API's own type text ("Asset Tag") while the page type is unchanged
      tagType: t.api && typeOf(t.api.tagType) === t.type ? t.api.tagType : t.type,
      technology: t.tech,
      // the API only accepts a linked asset on an Assigned tag
      assetRegistrationId: t.status === 'Assigned' && a ? a.tag : null,
      assetName: t.status === 'Assigned' && a ? a.name ?? `${a.kind} ${a.tag}` : null,
      siteId: SITES.find((s) => s.name === t.site)?.code || null,
      siteName: t.site || null,
      locationName: t.location || null,
      status: t.status,
      lastSeenAt: t.lastSeen !== null ? new Date(t.lastSeen).toISOString() : null,
      createdBy: this.auth.getUserEmail() || 'unknown',
      clientId: environment.clientId,
      tenantId: environment.tenantId,
    };
  }
}
