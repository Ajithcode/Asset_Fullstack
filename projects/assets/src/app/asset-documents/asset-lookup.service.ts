import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { EMPTY, Observable, expand, map, of, reduce } from 'rxjs';
import { AssetCondition, AssetHeader, AssetIconKey, AssetStatus, Criticality } from 'shared-ui';
import { ASSET_API, MEDIA_API, EVENTLOG_API } from '../services/asset-api';

/**
 * Helpers shared by the Asset Documents and Asset Images pages: registered assets (read-only),
 * file upload to the Media service, event-log history and file download.
 */

/** Page size used when reading every page of a list endpoint (the APIs allow up to 200). */
export const API_PAGE_SIZE = 200;

/** Paged list returned by the Asset API list endpoints. */
export interface PagedDto<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** One row of GET /api/asset-registrations (AssetListItemResponse). */
interface AssetListItemDto {
  id: string;
  assetRegistrationId: string;
  assetName: string | null;
  assetTag: string | null;
  imageUrl: string | null;
  category: string | null;
  assetType: string | null;
  siteName: string | null;
  buildingName: string | null;
  floorName: string | null;
  zoneName: string | null;
  roomName: string | null;
  locationPath: string | null;
  status: string | null;
  criticality: string | null;
  manufacturer: string | null;
}

/** A registered asset as used by the documents/images pages (picker, filter, header). */
export interface AssetOption {
  /** Asset tag, or the registration id when the asset has no tag; used in ?tag= links and as the UI key. */
  tag: string;
  /** AssetRegistrationId sent to the documents/images APIs. */
  id: string;
  name: string;
  location: string;
  header: AssetHeader;
}

/** Response of POST /api/media/upload. */
interface MediaUploadDto {
  url: string;
}

/** One entry of GET /api/eventlogs. */
export interface EventLogDto {
  id: string;
  entityType: string;
  entityId: string;
  entityName: string;
  action: string;
  performedBy: string | null;
  createdAt: string | null;
  message: string;
}

const STATUSES: AssetStatus[] = ['Active', 'Under Maintenance', 'In Repair', 'Disposed', 'In Storage', 'Checked Out'];
const CRITICALITIES: Criticality[] = ['Critical', 'High', 'Medium', 'Low'];

/** Picks the header icon from the asset's category and type. */
function iconFor(text: string): AssetIconKey {
  const t = text.toLowerCase();
  if (/generator|genset/.test(t)) return 'generator';
  if (/chiller|hvac|fan|ahu/.test(t)) return 'chiller';
  if (/ups|battery/.test(t)) return 'ups';
  if (/forklift|vehicle|fleet/.test(t)) return 'forklift';
  if (/fire|pump/.test(t)) return 'fire';
  if (/air ?con|\bac\b/.test(t)) return 'ac';
  if (/laptop|computer|\bit\b|server/.test(t)) return 'laptop';
  if (/cctv|camera|security/.test(t)) return 'cctv';
  if (/solar|pv/.test(t)) return 'solar';
  return 'transformer';
}

function matchOne<T extends string>(value: string | null, options: T[], fallback: T): T {
  const v = value?.trim().toLowerCase();
  return options.find((o) => o.toLowerCase() === v) ?? fallback;
}

function conditionFor(status: AssetStatus): AssetCondition {
  if (status === 'In Repair' || status === 'Disposed') return 'Poor';
  if (status === 'Under Maintenance') return 'Fair';
  return 'Good';
}

function toAssetOption(dto: AssetListItemDto): AssetOption {
  const tag = dto.assetTag?.trim() || dto.assetRegistrationId;
  const name = dto.assetName?.trim() || tag;
  const location =
    dto.locationPath?.trim() ||
    [dto.siteName, dto.buildingName, dto.floorName, dto.zoneName, dto.roomName].filter((p) => p?.trim()).join(' - ') ||
    '-';
  const status = matchOne(dto.status, STATUSES, 'Active');
  return {
    tag,
    id: dto.assetRegistrationId,
    name,
    location,
    header: {
      tag,
      name,
      category: dto.category?.trim() || '-',
      model: [dto.manufacturer, dto.assetType].filter((p) => p?.trim()).join(' ') || '-',
      location,
      status,
      condition: conditionFor(status),
      criticality: matchOne(dto.criticality, CRITICALITIES, 'Medium'),
      image: dto.imageUrl?.trim() || null,
      icon: iconFor(`${dto.category ?? ''} ${dto.assetType ?? ''} ${dto.assetName ?? ''}`),
    },
  };
}

/** Local date-time yyyy-MM-ddTHH:mm of an API timestamp ('' when missing). */
export function toLocalDateTime(iso: string | null | undefined): string {
  if (!iso) return '';
  const d = new Date(iso);
  if (isNaN(d.getTime())) return '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

@Injectable({ providedIn: 'root' })
export class AssetLookupService {
  private readonly http = inject(HttpClient);

  /** Reads every page of a paged list endpoint. */
  fetchAll<T>(url: string, params: Record<string, string> = {}): Observable<T[]> {
    const fetchPage = (page: number) =>
      this.http.get<PagedDto<T>>(url, {
        params: new HttpParams({ fromObject: params }).set('page', page).set('pageSize', API_PAGE_SIZE),
      });
    let page = 1;
    return fetchPage(page).pipe(
      expand((res) => (page * API_PAGE_SIZE < res.totalCount && res.items?.length ? fetchPage(++page) : EMPTY)),
      reduce((all, res) => all.concat(res.items ?? []), [] as T[]),
    );
  }

  /** Every registered asset (GET /api/asset-registrations, all pages). */
  listAssets(): Observable<AssetOption[]> {
    return this.fetchAll<AssetListItemDto>(`${ASSET_API}asset-registrations`).pipe(map((items) => items.map(toAssetOption)));
  }

  /**
   * The asset named by ?tag= (asset tag or registration id). With no tag, the first registered asset.
   * Emits null when no asset matches.
   */
  findAsset(tag: string | null): Observable<AssetOption | null> {
    const q = tag?.trim() ?? '';
    let params = new HttpParams().set('page', 1).set('pageSize', q ? 50 : 1);
    if (q) params = params.set('search', q);
    return this.http.get<PagedDto<AssetListItemDto>>(`${ASSET_API}asset-registrations`, { params }).pipe(
      map((res) => {
        const items = res.items ?? [];
        if (!q) return items[0] ? toAssetOption(items[0]) : null;
        const lower = q.toLowerCase();
        const hit =
          items.find((a) => a.assetTag?.trim().toLowerCase() === lower) ??
          items.find((a) => a.assetRegistrationId.toLowerCase() === lower);
        return hit ? toAssetOption(hit) : null;
      }),
    );
  }

  /** POST /api/media/upload (multipart: file + category); emits the stored file's URL. */
  uploadFile(file: File, category: 'asset-documents' | 'asset-images'): Observable<string> {
    const form = new FormData();
    form.append('file', file, file.name);
    form.append('category', category);
    return this.http.post<MediaUploadDto>(`${MEDIA_API}upload`, form).pipe(map((res) => res.url));
  }

  /** GET /api/eventlogs?entityType=&entityId= (newest first as returned by the API). */
  history(entityType: string, entityId: string): Observable<EventLogDto[]> {
    if (!entityId) return of([]);
    const params = new HttpParams().set('entityType', entityType).set('entityId', entityId).set('page', 1).set('pageSize', 200);
    return this.http
      .get<PagedDto<EventLogDto>>(EVENTLOG_API, { params })
      .pipe(map((res) => res.items ?? []));
  }

  /**
   * Saves a stored file under the given name. The file is fetched as a blob so the name is kept;
   * if that fails (e.g. blocked by CORS) it is opened in a new tab instead.
   */
  download(url: string, name: string): void {
    this.http.get(url, { responseType: 'blob' }).subscribe({
      next: (blob) => saveBlob(blob, name),
      error: () => window.open(url, '_blank', 'noopener'),
    });
  }
}

export function saveBlob(blob: Blob, name: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = name;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url));
}
