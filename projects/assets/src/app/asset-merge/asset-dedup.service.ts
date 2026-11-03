import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from 'shared-ui';
import { DedupRules } from './dedup-match';

/** Asset Merge / Dedup API (Asset service, through the API gateway). */
const DEDUP_API = `${environment.assetApiUrl}asset/api/asset-dedup`;
const MERGES_API = `${environment.assetApiUrl}asset/api/asset-merges`;

export type MergeField = 'name' | 'type' | 'model' | 'serialNumber' | 'category' | 'location' | 'status' | 'purchaseDate' | 'supplier' | 'value';

/** GET/PUT /asset-dedup/rules */
export interface DedupRulesDto extends DedupRules {
  lastScanAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
}

/** One asset as compared and merged on the screen. */
export interface DedupRecordDto {
  tag: string;
  assetRegistrationId: string;
  name: string;
  type: string;
  manufacturer: string;
  model: string;
  serialNumber: string;
  category: string;
  location: string;
  status: string;
  /** yyyy-MM-dd, or '' */
  purchaseDate: string;
  supplier: string;
  value: number;
  /** yyyy-MM-ddTHH:mm, or '' */
  lastModified: string;
  modifiedBy: string;
  source: string;
  inRegister: boolean;
}

export interface DedupGroupDto {
  /** The primary asset's tag. */
  groupId: string;
  maxScore: number;
  confidence: 'High' | 'Medium' | 'Low' | null;
  primary: DedupRecordDto;
  members: { score: number; confidence: 'High' | 'Medium' | 'Low' | null; record: DedupRecordDto }[];
}

export interface DedupGroupListDto {
  items: DedupGroupDto[];
  total: number;
  page: number;
  pageSize: number;
  counts: { high: number; medium: number; low: number };
  duplicates: number;
  lastScanAt: string | null;
}

export interface DedupScanDto {
  scannedAt: string;
  groups: number;
  duplicates: number;
  high: number;
  medium: number;
  low: number;
}

export interface DedupDismissalDto {
  id: string;
  tags: string[];
  reason: string | null;
  dismissedAt: string | null;
  dismissedBy: string | null;
}

/** POST /asset-merges body. fieldSources: field → tag of the record whose value the primary keeps. */
export interface MergeAssetsRequest {
  primaryTag: string;
  mergedTags: string[];
  fieldSources: Partial<Record<MergeField, string>>;
  notes: string;
}

export interface AssetMergeDto {
  id: string;
  primaryTag: string;
  mergedTags: string[];
  fieldSources: Partial<Record<MergeField, string>>;
  /** Values the primary asset kept. */
  fields: Partial<Record<MergeField, string>>;
  notes: string | null;
  mergedAt: string | null;
  mergedBy: string | null;
}

export interface AssetMergeListDto {
  items: AssetMergeDto[];
  total: number;
  page: number;
  pageSize: number;
}

/** Groups are filtered and paged on the screen, so they are fetched in one page. */
const ALL_GROUPS = 1000;

@Injectable({ providedIn: 'root' })
export class AssetDedupService {
  private readonly http = inject(HttpClient);

  getRules(): Observable<DedupRulesDto> {
    return this.http.get<DedupRulesDto>(`${DEDUP_API}/rules`);
  }

  saveRules(rules: DedupRules): Observable<DedupRulesDto> {
    return this.http.put<DedupRulesDto>(`${DEDUP_API}/rules`, rules);
  }

  scan(): Observable<DedupScanDto> {
    return this.http.post<DedupScanDto>(`${DEDUP_API}/scan`, {});
  }

  getGroups(): Observable<DedupGroupListDto> {
    return this.http.get<DedupGroupListDto>(`${DEDUP_API}/groups`, { params: new HttpParams().set('page', 1).set('pageSize', ALL_GROUPS) });
  }

  getGroup(groupId: string): Observable<DedupGroupDto> {
    return this.http.get<DedupGroupDto>(`${DEDUP_API}/groups/${encodeURIComponent(groupId)}`);
  }

  getDismissals(): Observable<DedupDismissalDto[]> {
    return this.http.get<DedupDismissalDto[]>(`${DEDUP_API}/dismissals`);
  }

  /** Marks tags[0] (the primary asset) as not duplicating each of the other tags. */
  dismiss(tags: string[], reason = ''): Observable<DedupDismissalDto[]> {
    return this.http.post<DedupDismissalDto[]>(`${DEDUP_API}/dismissals`, { tags, reason });
  }

  undoDismissal(id: string): Observable<void> {
    return this.http.delete<void>(`${DEDUP_API}/dismissals/${encodeURIComponent(id)}`);
  }

  getMerges(page = 1, pageSize = 20, search = ''): Observable<AssetMergeListDto> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    return this.http.get<AssetMergeListDto>(MERGES_API, { params });
  }

  getMerge(id: string): Observable<AssetMergeDto> {
    return this.http.get<AssetMergeDto>(`${MERGES_API}/${encodeURIComponent(id)}`);
  }

  merge(request: MergeAssetsRequest): Observable<AssetMergeDto> {
    return this.http.post<AssetMergeDto>(MERGES_API, request);
  }
}

/** Readable message for a failed call: first validation error, the server's `message`, or the status. */
export function dedupErrorMessage(err: unknown): string {
  if (!(err instanceof HttpErrorResponse)) return 'Something went wrong.';
  if (err.status === 0) return 'The asset service is unreachable. Check that the backend is running.';
  if (err.status === 401) return 'Your session has expired. Please sign in again.';
  const body = err.error;
  const first = body?.errors && Object.values(body.errors as Record<string, string[]>)[0]?.[0];
  return first || body?.message || body?.title || `Request failed (${err.status}).`;
}
