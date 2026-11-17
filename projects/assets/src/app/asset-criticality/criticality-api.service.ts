import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ASSET_API } from '../services/asset-api';

/** Approval status of an asset row: the open assessment's status, else Approved or Unassessed. */
export type CriticalityApprovalStatus = 'Draft' | 'Submitted' | 'PendingApproval' | 'Approved' | 'Rejected' | 'Cancelled' | 'Unassessed';

/** Landing filters; empty strings are left out of the request. Same filters for list, KPIs, chart and export. */
export interface CriticalityFilters {
  search: string;
  organization: string;
  site: string;
  department: string;
  category: string;
  assetType: string;
  level: string;
  approvalStatus: string;
  assessor: string;
  reviewDueFrom: string;
  reviewDueTo: string;
  overdue: boolean;
  operationalStatus: string;
}

export const EMPTY_FILTERS: CriticalityFilters = {
  search: '', organization: '', site: '', department: '', category: '', assetType: '', level: '',
  approvalStatus: '', assessor: '', reviewDueFrom: '', reviewDueTo: '', overdue: false, operationalStatus: '',
};

/** GET /api/asset-criticality/assets row. */
export interface CriticalityAssetRow {
  assetRegistrationId: string;
  assetTag: string | null;
  assetName: string | null;
  category: string | null;
  assetType: string | null;
  organization: string | null;
  siteName: string | null;
  locationPath: string | null;
  department: string | null;
  owner: string | null;
  operationalStatus: string | null;
  condition: string | null;
  currentAssessmentId: string | null;
  level: string | null;
  score: number | null;
  assessmentDate: string | null;
  assessedBy: string | null;
  nextReviewDate: string | null;
  reviewOverdue: boolean;
  openAssessmentId: string | null;
  approvalStatus: CriticalityApprovalStatus;
}

export interface Paged<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface CriticalityLevel {
  name: string;
  code: string | null;
  color: string | null;
  /** 4 = Critical … 1 = Low, 0 = other levels. */
  rank: number;
}

export interface CriticalityKpis {
  total: number;
  critical: number;
  high: number;
  medium: number;
  low: number;
  unassessed: number;
  pendingApprovals: number;
  reassessmentOverdue: number;
  levels: (CriticalityLevel & { count: number })[];
}

export interface CriticalityDistributionCount {
  level: string;
  count: number;
}

export interface CriticalityDistribution {
  byLevel: CriticalityDistributionCount[];
  byCategory: { category: string; total: number; levels: CriticalityDistributionCount[] }[];
}

export interface CriticalityOption {
  value: string;
  label: string;
}

export interface CriticalityFilterOptions {
  levels: CriticalityLevel[];
  approvalStatuses: CriticalityOption[];
  organizations: string[];
  sites: CriticalityOption[];
  departments: string[];
  categories: string[];
  assetTypes: string[];
  operationalStatuses: string[];
  assessors: string[];
  /** Export / table columns: value = column key. */
  columns: CriticalityOption[];
}

/** GET /api/asset-criticality/assessments row (Assessment History tab). */
export interface CriticalityAssessmentRow {
  id: string;
  assetRegistrationId: string;
  assetTag: string | null;
  assetName: string | null;
  assessmentType: string | null;
  assessmentDate: string | null;
  assessedBy: string | null;
  approvedBy: string | null;
  level: string | null;
  score: number | null;
  approvalStatus: CriticalityApprovalStatus;
  isCurrent: boolean;
  createdAt: string | null;
}

export interface CriticalityHistoryFilters {
  search: string;
  assetRegistrationId: string;
  approvalStatus: string;
  assessor: string;
  from: string;
  to: string;
}

/** Asset Criticality landing page API (Asset service, /api/asset-criticality). */
@Injectable({ providedIn: 'root' })
export class CriticalityApiService {
  private readonly http = inject(HttpClient);
  private readonly url = `${ASSET_API}asset-criticality`;

  assets(filters: CriticalityFilters, page: number, pageSize: number, sortBy: string, sortDir: 'asc' | 'desc'): Observable<Paged<CriticalityAssetRow>> {
    const params = toParams(filters).set('page', page).set('pageSize', pageSize).set('sortBy', sortBy).set('sortDir', sortDir);
    return this.http.get<Paged<CriticalityAssetRow>>(`${this.url}/assets`, { params });
  }

  kpis(filters: CriticalityFilters): Observable<CriticalityKpis> {
    return this.http.get<CriticalityKpis>(`${this.url}/kpis`, { params: toParams(filters) });
  }

  distribution(filters: CriticalityFilters): Observable<CriticalityDistribution> {
    return this.http.get<CriticalityDistribution>(`${this.url}/distribution`, { params: toParams(filters) });
  }

  filterOptions(): Observable<CriticalityFilterOptions> {
    return this.http.get<CriticalityFilterOptions>(`${this.url}/filter-options`);
  }

  assessments(filters: CriticalityHistoryFilters, page: number, pageSize: number): Observable<Paged<CriticalityAssessmentRow>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    for (const [key, value] of Object.entries(filters)) {
      if (value) params = params.set(key, value);
    }
    return this.http.get<Paged<CriticalityAssessmentRow>>(`${this.url}/assessments`, { params });
  }

  /** Excel file of the filtered list with the given columns, in that order. */
  exportXlsx(filters: CriticalityFilters, columns: string[], sortBy: string, sortDir: 'asc' | 'desc'): Observable<Blob> {
    const params = toParams(filters).set('format', 'xlsx').set('columns', columns.join(',')).set('sortBy', sortBy).set('sortDir', sortDir);
    return this.http.get(`${this.url}/export`, { params, responseType: 'blob' });
  }
}

function toParams(filters: CriticalityFilters): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(filters)) {
    if (value === true) params = params.set(key, 'true');
    else if (typeof value === 'string' && value.trim()) params = params.set(key, value.trim());
  }
  return params;
}
