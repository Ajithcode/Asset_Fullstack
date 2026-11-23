import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { ASSET_API, MEDIA_API } from '../services/asset-api';

/** Statuses an asset can be given on the Asset Status & Condition pages. */
export const OPERATIONAL_STATUSES = ['Active', 'Under Maintenance', 'Out of Service'] as const;
export const PHYSICAL_CONDITIONS = ['Excellent', 'Good', 'Fair', 'Poor', 'Critical'] as const;

export type ChangeState = 'Draft' | 'Pending Approval' | 'Approved' | 'Rejected';
export type ApprovalStatus = 'Not Started' | 'Pending' | 'Approved' | 'Rejected';

/** GET /api/asset-status-condition/options */
export interface StatusConditionOptions {
  statuses: string[];
  conditions: string[];
  states: string[];
  reasons: string[];
  assessmentMethods: string[];
  assessmentSources: string[];
  operationalImpacts: string[];
  downtimeUnits: string[];
  approvalLevels: { level: number; role: string }[];
  /** Narrows the Reason dropdown to the chosen new status. */
  reasonsByStatus: Record<string, string[]>;
  /** Narrows the Operational Impact dropdown to the chosen new status. */
  impactsByStatus: Record<string, string[]>;
  /** Narrows the Assessment Method dropdown to the chosen source. */
  methodsBySource: Record<string, string[]>;
}

/** GET /api/asset-status-condition/assets/{id}/lookups: dropdown data tied to one asset. */
export interface StatusConditionLookups {
  workOrders: { workOrderId: string; title: string | null; workType: string | null; priority: string | null; status: string | null; scheduledDate: string | null }[];
  inspections: { code: string; name: string; kind: string }[];
  failureReasons: { code: string; name: string; category: string; severity: string }[];
}

/** GET /api/asset-status-condition/summary */
export interface StatusConditionSummary {
  totalAssets: number;
  operationalAssets: number;
  underMaintenance: number;
  outOfService: number;
  poorOrCritical: number;
  assessmentsDue: number;
  pendingApprovals: number;
  assessmentDueWithinDays: number;
}

/** A registered asset with its current status and condition. */
export interface StatusConditionAsset {
  assetRegistrationId: string;
  assetTag: string | null;
  assetName: string | null;
  category: string | null;
  assetType: string | null;
  siteName: string | null;
  locationPath: string | null;
  criticality: string | null;
  imageUrl: string | null;
  manufacturer: string | null;
  model: string | null;
  status: string | null;
  condition: string | null;
  healthScore: number | null;
  lastAssessmentDate: string | null;
  lastAssessmentSource: string | null;
  nextAssessmentDue: string | null;
  pendingChangeId: string | null;
  pendingChangeNumber: string | null;
}

export interface Evidence {
  name: string;
  url: string | null;
  kind: 'File' | 'Link';
  sizeBytes: number | null;
  addedBy?: string | null;
  addedAt?: string | null;
}

export interface Approval {
  level: number;
  role: string;
  approver: string | null;
  status: ApprovalStatus;
  actedBy: string | null;
  actedAt: string | null;
  comments: string | null;
}

export interface StatusConditionChange {
  id: string;
  changeNumber: string;
  assetRegistrationId: string;
  assetTag: string | null;
  assetName: string | null;
  siteName: string | null;
  previousStatus: string | null;
  newStatus: string;
  statusEffectiveFrom: string | null;
  previousCondition: string | null;
  newCondition: string;
  conditionAssessmentDate: string | null;
  reasonCode: string | null;
  assessmentMethod: string | null;
  assessmentSource: string | null;
  assessedBy: string | null;
  nextAssessmentDue: string | null;
  remarks: string | null;
  operationalImpact: string | null;
  relatedWorkOrder: string | null;
  relatedInspection: string | null;
  downtimeEstimate: number | null;
  downtimeUnit: string | null;
  evidence: Evidence[];
  state: ChangeState;
  submittedBy: string | null;
  submittedAt: string | null;
  approvals: Approval[];
  appliedAt: string | null;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
}

/** Body of POST / PUT /changes. */
export interface SaveChangeRequest {
  assetRegistrationId: string;
  newStatus: string | null;
  statusEffectiveFrom: string | null;
  newCondition: string | null;
  conditionAssessmentDate: string | null;
  reasonCode: string | null;
  assessmentMethod: string | null;
  assessmentSource: string | null;
  assessedBy: string | null;
  nextAssessmentDue: string | null;
  remarks: string | null;
  operationalImpact: string | null;
  relatedWorkOrder: string | null;
  relatedInspection: string | null;
  downtimeEstimate: number | null;
  downtimeUnit: string | null;
  evidence: Evidence[];
  approvers: (string | null)[];
  submit: boolean;
}

export interface Paged<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ListFilters {
  search?: string;
  status?: string;
  condition?: string;
  state?: string;
  assetRegistrationId?: string;
  page?: number;
  pageSize?: number;
}

function toParams(filters: ListFilters): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(filters)) {
    if (value !== undefined && value !== null && `${value}`.trim() !== '') params = params.set(key, `${value}`.trim());
  }
  return params;
}

/** Asset Status & Condition API (/api/asset-status-condition) through the gateway. */
@Injectable({ providedIn: 'root' })
export class StatusConditionService {
  private readonly api = `${ASSET_API}asset-status-condition`;

  constructor(private http: HttpClient) {}

  options(): Observable<StatusConditionOptions> {
    return this.http.get<StatusConditionOptions>(`${this.api}/options`);
  }

  summary(): Observable<StatusConditionSummary> {
    return this.http.get<StatusConditionSummary>(`${this.api}/summary`);
  }

  assets(filters: ListFilters): Observable<Paged<StatusConditionAsset>> {
    return this.http.get<Paged<StatusConditionAsset>>(`${this.api}/assets`, { params: toParams(filters) });
  }

  /** By asset registration id or asset tag. */
  asset(idOrTag: string): Observable<StatusConditionAsset> {
    return this.http.get<StatusConditionAsset>(`${this.api}/assets/${encodeURIComponent(idOrTag)}`);
  }

  /** The asset's work orders and inspections, and the failure reasons master data. */
  lookups(idOrTag: string): Observable<StatusConditionLookups> {
    return this.http.get<StatusConditionLookups>(`${this.api}/assets/${encodeURIComponent(idOrTag)}/lookups`);
  }

  changes(filters: ListFilters): Observable<Paged<StatusConditionChange>> {
    return this.http.get<Paged<StatusConditionChange>>(`${this.api}/changes`, { params: toParams(filters) });
  }

  change(id: string): Observable<StatusConditionChange> {
    return this.http.get<StatusConditionChange>(`${this.api}/changes/${encodeURIComponent(id)}`);
  }

  /** Creates the change, or updates it when an id is given (drafts and rejected changes only). */
  save(id: string | null, body: SaveChangeRequest): Observable<StatusConditionChange> {
    return id
      ? this.http.put<StatusConditionChange>(`${this.api}/changes/${encodeURIComponent(id)}`, body)
      : this.http.post<StatusConditionChange>(`${this.api}/changes`, body);
  }

  deleteDraft(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/changes/${encodeURIComponent(id)}`);
  }

  approve(id: string, comments: string): Observable<StatusConditionChange> {
    return this.http.post<StatusConditionChange>(`${this.api}/changes/${encodeURIComponent(id)}/approve`, { comments });
  }

  reject(id: string, comments: string): Observable<StatusConditionChange> {
    return this.http.post<StatusConditionChange>(`${this.api}/changes/${encodeURIComponent(id)}/reject`, { comments });
  }

  /** POST to the Media service's /api/media/upload; resolves to the stored file's URL. */
  upload(file: File): Observable<string> {
    const form = new FormData();
    form.append('file', file, file.name);
    form.append('category', 'asset-documents');
    return this.http.post<{ url: string }>(`${MEDIA_API}upload`, form).pipe(map((res) => res.url));
  }
}

/** Pill colour class (from the Status & Condition stylesheet) of a status. */
export function statusTone(status: string | null | undefined): string {
  if (status === 'Active') return 'pill--good';
  if (status === 'Under Maintenance') return 'pill--warn';
  if (status === 'Out of Service' || status === 'Disposed') return 'pill--critical';
  return 'pill--serious';
}

/** Pill colour class of a condition. */
export function conditionTone(condition: string | null | undefined): string {
  if (condition === 'Excellent' || condition === 'Good') return 'pill--good';
  if (condition === 'Fair') return 'pill--warn';
  if (condition === 'Poor') return 'pill--serious';
  if (condition === 'Critical') return 'pill--critical';
  return '';
}

export function stateTone(state: string | null | undefined): string {
  if (state === 'Approved') return 'pill--good';
  if (state === 'Pending Approval') return 'pill--warn';
  if (state === 'Rejected') return 'pill--critical';
  return '';
}

/** "15 Nov 2026" in the viewer's time zone; "-" when empty. */
export function formatDate(iso: string | null | undefined): string {
  if (!iso) return '-';
  const d = new Date(iso);
  if (isNaN(d.getTime())) return '-';
  return d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
}

/** "15 Nov 2026, 10:30". */
export function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return '-';
  const d = new Date(iso);
  if (isNaN(d.getTime())) return '-';
  return `${formatDate(iso)}, ${d.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })}`;
}
