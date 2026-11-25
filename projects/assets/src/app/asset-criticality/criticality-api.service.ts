import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ASSET_API } from '../services/asset-api';

// ===== Phase 3: assessment form, workflow =====

export interface AssessmentLevel { id: string | null; name: string; code: string | null; color: string | null; minScore: number | null; maxScore: number | null; }
export interface AssessmentTemplateCriterion { criterionId: string; code: string; name: string; weight: number; description: string | null; }
export interface AssessmentTemplate { id: string; code: string; name: string; version: number; isDefault: boolean; criteria: AssessmentTemplateCriterion[]; }
export interface AssessmentAssetInfo { assetRegistrationId: string; name: string | null; tag: string | null; category: string | null; assetType: string | null; site: string | null; location: string | null; department: string | null; owner: string | null; }
export interface AssessmentApprovalInfo { scenario: string; label: string | null; approverRoles: string[]; approvalType: string | null; description: string | null; }

export interface AssessmentDraftContext {
  asset: AssessmentAssetInfo;
  operationalStatus: string | null;
  condition: string | null;
  current: { id: string; level: string; score: number; assessmentDate: string; nextReviewDate: string | null } | null;
  openAssessmentId: string | null;
  openAssessmentStatus: string | null;
  assessmentType: 'Initial' | 'Reassessment';
  assessmentDate: string;
  assessedBy: string;
  templates: AssessmentTemplate[];
  defaultTemplateId: string | null;
  levels: AssessmentLevel[];
  reviewIntervals: { levelName: string; months: number }[];
}

export interface AssessmentBreakdownRow { criterionId: string | null; code: string; name: string; weight: number; rating: number | null; weighted: number; comment: string | null; }

export interface AssessmentScorePreview {
  score: number;
  calculatedLevel: AssessmentLevel | null;
  finalLevel: AssessmentLevel | null;
  isOverride: boolean;
  previousLevel: string | null;
  isLowered: boolean;
  justificationRequired: boolean;
  suggestedNextReviewDate: string | null;
  reviewIntervalMonths: number | null;
  approval: AssessmentApprovalInfo | null;
  breakdown: AssessmentBreakdownRow[];
}

export interface AssessmentRisk { failureConsequences: string | null; alternativeEquipmentAvailable: boolean | null; downtimeImpact: string | null; financialLoss: string | null; safetyEnvironmentalConsequences: string | null; }
export interface AssessmentAttachment { fileName: string; fileUrl: string; fileType: string | null; fileSize: number; uploadedBy?: string | null; uploadedAt?: string | null; }
export interface AssessmentTrailEntry { action: string; by: string | null; at: string; comments: string | null; }
export interface AssessmentActions { canEdit: boolean; canSubmit: boolean; canCancel: boolean; canApprove: boolean; canReject: boolean; blocked: string | null; }

export interface AssessmentDetail {
  id: string;
  assessmentNumber: string | null;
  asset: AssessmentAssetInfo;
  assessmentType: string;
  status: CriticalityApprovalStatus;
  isCurrent: boolean;
  assessmentDate: string;
  assessedBy: string;
  templateId: string | null;
  templateName: string | null;
  templateVersion: number | null;
  criteria: AssessmentBreakdownRow[];
  score: number;
  calculatedLevel: string | null;
  finalLevel: string;
  finalLevelId: string | null;
  finalLevelColor: string | null;
  isOverride: boolean;
  justification: string | null;
  previousLevel: string | null;
  previousApprovedAssessmentId: string | null;
  risk: AssessmentRisk;
  attachments: AssessmentAttachment[];
  nextReviewDate: string | null;
  remarks: string | null;
  approval: AssessmentApprovalInfo | null;
  rejectedReason: string | null;
  submittedBy: string | null;
  submittedAt: string | null;
  approvedBy: string | null;
  approvedAt: string | null;
  createdBy: string | null;
  createdAt: string | null;
  trail: AssessmentTrailEntry[];
  concurrencyVersion: number;
  isLegacy: boolean;
  actions: AssessmentActions;
  isComplete: boolean;
}

export interface AssessmentVersion {
  id: string; assessmentNumber: string | null; assessmentType: string; status: CriticalityApprovalStatus; isCurrent: boolean;
  assessmentDate: string; score: number; level: string; assessedBy: string; approvedBy: string | null; approvedAt: string | null;
  templateVersion: number | null; templateName: string | null;
}

/** Body of POST/PUT /assessments and POST /score-preview. Scores and weights are never sent: the server computes them. */
export interface SaveAssessmentBody {
  assetRegistrationId: string;
  templateId: string;
  assessmentDate: string | null;
  ratings: { criterionCode: string; rating: number | null; comment?: string | null }[];
  overrideLevel: string | null;
  justification: string | null;
  risk: AssessmentRisk;
  attachments: AssessmentAttachment[];
  nextReviewDate: string | null;
  remarks: string | null;
  concurrencyVersion?: number | null;
}

export interface AssetSearchHit { assetRegistrationId: string; assetName: string | null; assetTag: string | null; category: string | null; locationPath: string | null; }


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
  openLevel?: string | null;
  openScore?: number | null;
  openAssessedBy?: string | null;
  openSubmittedAt?: string | null;
  openType?: string | null;
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
  /** Score range from the MDM Criticality master. */
  minScore?: number | null;
  maxScore?: number | null;
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

  // ----- Phase 3 -----
  draftContext(assetId: string): Observable<AssessmentDraftContext> {
    return this.http.get<AssessmentDraftContext>(`${this.url}/assets/${encodeURIComponent(assetId)}/draft-context`);
  }

  scorePreview(body: Pick<SaveAssessmentBody, 'assetRegistrationId' | 'templateId' | 'ratings' | 'overrideLevel' | 'assessmentDate'>): Observable<AssessmentScorePreview> {
    return this.http.post<AssessmentScorePreview>(`${this.url}/score-preview`, body);
  }

  createAssessment(body: SaveAssessmentBody): Observable<AssessmentDetail> {
    return this.http.post<AssessmentDetail>(`${this.url}/assessments`, body);
  }

  updateAssessment(id: string, body: SaveAssessmentBody): Observable<AssessmentDetail> {
    return this.http.put<AssessmentDetail>(`${this.url}/assessments/${id}`, body);
  }

  assessment(id: string): Observable<AssessmentDetail> {
    return this.http.get<AssessmentDetail>(`${this.url}/assessments/${id}`);
  }

  submit(id: string, concurrencyVersion: number | null): Observable<AssessmentDetail> {
    return this.http.post<AssessmentDetail>(`${this.url}/assessments/${id}/submit`, { concurrencyVersion });
  }

  approve(id: string, comments: string | null): Observable<AssessmentDetail> {
    return this.http.post<AssessmentDetail>(`${this.url}/assessments/${id}/approve`, { comments });
  }

  reject(id: string, comments: string): Observable<AssessmentDetail> {
    return this.http.post<AssessmentDetail>(`${this.url}/assessments/${id}/reject`, { comments });
  }

  cancel(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/assessments/${id}`);
  }

  versions(assetId: string): Observable<AssessmentVersion[]> {
    return this.http.get<AssessmentVersion[]>(`${this.url}/assets/${encodeURIComponent(assetId)}/versions`);
  }

  uploadAttachment(file: File): Observable<AssessmentAttachment> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<AssessmentAttachment>(`${this.url}/attachments`, form);
  }

  searchAssets(query: string): Observable<{ items: AssetSearchHit[] }> {
    return this.http.get<{ items: AssetSearchHit[] }>(`${ASSET_API}asset-registrations`, { params: new HttpParams().set('search', query).set('page', 1).set('pageSize', 12) });
  }

  assessments(filters: CriticalityHistoryFilters, page: number, pageSize: number): Observable<Paged<CriticalityAssessmentRow>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    for (const [key, value] of Object.entries(filters)) {
      if (value) params = params.set(key, value);
    }
    return this.http.get<Paged<CriticalityAssessmentRow>>(`${this.url}/assessments`, { params });
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
