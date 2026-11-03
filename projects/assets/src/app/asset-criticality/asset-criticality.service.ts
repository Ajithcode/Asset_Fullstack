import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { AuthService } from 'shared-ui';
import { ASSET_API, TENANT } from '../services/asset-api';
import { CriticalityCriterion, CriticalityLevel } from '../asset-details/asset-details';

/** Status of a risk mitigation action (AssetRiskMitigationStatuses). */
export type MitigationStatus = 'Open' | 'In Progress' | 'Completed' | 'Cancelled';

/** Likelihood / consequence labels; index 0 is position 1 on the matrix. */
export const RISK_SCALE = ['Very Low', 'Low', 'Medium', 'High', 'Very High'];

/** One criticality assessment of an asset (AssetCriticalityResponse), as the page uses it. */
export interface CriticalityAssessment {
  id: string;
  assetRegistrationId: string;
  tag: string;
  name: string;
  category: string;
  location: string;
  /** The asset's status when it was assessed. */
  assetStatus: string;
  isCurrent: boolean;
  level: CriticalityLevel;
  /** Out of 100. */
  score: number;
  /** yyyy-MM-dd */
  assessmentDate: string;
  validUntil: string;
  nextReview: string;
  assessedBy: string;
  approvedBy: string;
  /** Matrix position, 1 (very low) to 5 (very high). */
  likelihood: number;
  consequence: number;
  criteria: CriticalityCriterion[];
  remarks: string;
  method: string;
  reviewFrequency: string;
  department: string;
  assetGroup: string;
}

/** A risk mitigation action (AssetRiskMitigationActionResponse). */
export interface MitigationAction {
  id: string;
  action: string;
  owner: string;
  /** yyyy-MM-dd */
  dueDate: string;
  status: MitigationStatus;
  /** Criticality expected once the action is in place. */
  reducesTo: CriticalityLevel;
  isOverdue: boolean;
}

/** An asset linked to this one on the Relationships tab (read from GET /api/asset-relationships). */
export interface RelatedAssetLink {
  /** Null for a component that is not itself a registered asset. */
  assetRegistrationId: string | null;
  tag: string;
  name: string;
  location: string;
  relation: string;
}

export interface CriticalityOptions {
  likelihoodRatings: string[];
  consequenceRatings: string[];
  criticalityLevels: string[];
  criteria: { name: string; weight: number }[];
}

/** Re-assess / Assess Asset form. */
export interface CriticalityAssessmentFields {
  date: string;
  assessedBy: string;
  approvedBy: string;
  likelihood: number;
  consequence: number;
  criteria: CriticalityCriterion[];
  remarks: string;
  method?: string;
  reviewFrequency?: string;
  department?: string;
  assetGroup?: string;
  /** Leave empty for the API defaults (valid one year, review in six months). */
  validUntil?: string;
  nextReview?: string;
}

/** Edit Additional Information form. */
export interface CriticalityDetailsFields {
  validUntil: string;
  nextReview: string;
  assessedBy: string;
  approvedBy: string;
  method: string;
  reviewFrequency: string;
  department: string;
  assetGroup: string;
  remarks: string;
}

export interface MitigationFields {
  action: string;
  owner: string;
  dueDate: string;
  status: MitigationStatus;
  reducesTo: CriticalityLevel;
}

// ===== API shapes =====

interface CriterionDto {
  name: string;
  weight: number;
  score: number;
  weightedScore: number;
  justification: string;
}

interface CriticalityDto {
  id: string;
  assetRegistrationId: string;
  assetName: string | null;
  assetTag: string | null;
  category: string | null;
  location: string | null;
  assetStatus: string | null;
  isCurrent: boolean;
  assessmentDate: string;
  validUntil: string | null;
  nextReviewDate: string | null;
  assessedBy: string;
  approvedBy: string;
  likelihood: string | null;
  consequence: string | null;
  criteria: CriterionDto[];
  criticalityScore: number;
  criticalityLevel: string;
  remarks: string | null;
  assessmentMethod: string | null;
  reviewFrequency: string | null;
  department: string | null;
  assetGroup: string | null;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  clientId: string | null;
  tenantId: string | null;
}

interface PagedDto<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

interface MitigationDto {
  id: string;
  assetRegistrationId: string;
  assetName: string | null;
  assetTag: string | null;
  action: string;
  owner: string;
  dueDate: string;
  isOverdue: boolean;
  status: string | null;
  expectedCriticality: string | null;
  completedAt: string | null;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
}

interface MitigationListDto extends PagedDto<MitigationDto> {
  openCount: number;
}

interface RelationshipSubAssetDto {
  assetRegistrationId: string | null;
  componentId: string | null;
  name: string;
  tag: string | null;
  relationshipType: string;
  category: string | null;
  location: string | null;
}

interface RelationshipDto {
  id: string;
  mainAssetRegistrationId: string;
  mainAssetName: string | null;
  mainAssetTag: string | null;
  mainCategory: string | null;
  mainLocation: string | null;
  subAssets: RelationshipSubAssetDto[];
}

/** CreateAssetCriticalityRequest */
interface CreateCriticalityRequest {
  assetRegistrationId: string;
  assessmentDate: string;
  validUntil: string | null;
  nextReviewDate: string | null;
  assessedBy: string;
  approvedBy: string;
  likelihood: string | null;
  consequence: string | null;
  criteria: { name: string; weight: number; score: number; justification: string }[];
  remarks: string | null;
  assessmentMethod: string | null;
  reviewFrequency: string | null;
  department: string | null;
  assetGroup: string | null;
  createdBy: string | null;
  clientId: string;
  tenantId: string;
}

/** UpdateAssetCriticalityDetailsRequest (PUT /api/asset-criticality/{id}) */
interface UpdateCriticalityDetailsRequest {
  validUntil: string;
  nextReviewDate: string | null;
  assessedBy: string;
  approvedBy: string;
  assessmentMethod: string | null;
  reviewFrequency: string | null;
  department: string | null;
  assetGroup: string | null;
  remarks: string | null;
  updatedBy: string | null;
}

/** CreateAssetRiskMitigationActionRequest */
interface CreateMitigationRequest {
  assetRegistrationId: string;
  action: string;
  owner: string;
  dueDate: string;
  status: string;
  expectedCriticality: string;
  createdBy: string | null;
  clientId: string;
  tenantId: string;
}

const MAX_PAGE = 200;
const LEVELS: CriticalityLevel[] = ['Critical', 'High', 'Medium', 'Low'];
export const MITIGATION_STATUSES: MitigationStatus[] = ['Open', 'In Progress', 'Completed', 'Cancelled'];

function day(value: string | null | undefined): string {
  return (value ?? '').slice(0, 10);
}

function scalePosition(rating: string | null): number {
  const i = RISK_SCALE.findIndex((s) => s.toLowerCase() === rating?.toLowerCase());
  return i < 0 ? 3 : i + 1;
}

function toLevel(value: string | null | undefined): CriticalityLevel {
  return LEVELS.find((l) => l.toLowerCase() === value?.toLowerCase()) ?? 'Low';
}

function criterionKey(name: string): string {
  return name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
}

function toAssessment(dto: CriticalityDto): CriticalityAssessment {
  return {
    id: dto.id,
    assetRegistrationId: dto.assetRegistrationId,
    tag: dto.assetTag || dto.assetRegistrationId,
    name: dto.assetName ?? '',
    category: dto.category ?? '',
    location: dto.location ?? '',
    assetStatus: dto.assetStatus ?? '',
    isCurrent: dto.isCurrent,
    level: toLevel(dto.criticalityLevel),
    score: Number(dto.criticalityScore) || 0,
    assessmentDate: day(dto.assessmentDate),
    validUntil: day(dto.validUntil),
    nextReview: day(dto.nextReviewDate),
    assessedBy: dto.assessedBy ?? '',
    approvedBy: dto.approvedBy ?? '',
    likelihood: scalePosition(dto.likelihood),
    consequence: scalePosition(dto.consequence),
    criteria: (dto.criteria ?? []).map((c) => ({
      key: criterionKey(c.name),
      name: c.name,
      weight: Number(c.weight),
      score: Number(c.score),
      justification: c.justification ?? '',
    })),
    remarks: dto.remarks ?? '',
    method: dto.assessmentMethod ?? '',
    reviewFrequency: dto.reviewFrequency ?? '',
    department: dto.department ?? '',
    assetGroup: dto.assetGroup ?? '',
  };
}

function toMitigation(dto: MitigationDto): MitigationAction {
  const status = MITIGATION_STATUSES.find((s) => s.toLowerCase() === dto.status?.toLowerCase()) ?? 'Open';
  return {
    id: dto.id,
    action: dto.action,
    owner: dto.owner,
    dueDate: day(dto.dueDate),
    status,
    reducesTo: toLevel(dto.expectedCriticality),
    isOverdue: dto.isOverdue,
  };
}

function textOrNull(value: string | undefined | null): string | null {
  return value?.trim() || null;
}

@Injectable({ providedIn: 'root' })
export class AssetCriticalityService {
  private readonly apiUrl = `${ASSET_API}asset-criticality`;
  private readonly mitigationUrl = `${ASSET_API}asset-risk-mitigation-actions`;

  constructor(private http: HttpClient, private auth: AuthService) {}

  /** GET /api/asset-criticality — current assessment of every asset (first 200, filtered on the page). */
  getList(): Observable<CriticalityAssessment[]> {
    const params = new HttpParams().set('page', 1).set('pageSize', MAX_PAGE);
    return this.http.get<PagedDto<CriticalityDto>>(this.apiUrl, { params }).pipe(map((res) => (res.items ?? []).map(toAssessment)));
  }

  /** GET /api/asset-criticality/options — rating lists and the default criteria with weights. */
  getOptions(): Observable<CriticalityOptions> {
    return this.http.get<CriticalityOptions>(`${this.apiUrl}/options`);
  }

  /** GET /api/asset-criticality/history — every assessment of one asset, latest first (current one included). */
  getHistory(assetRegistrationId: string): Observable<CriticalityAssessment[]> {
    const params = new HttpParams().set('assetRegistrationId', assetRegistrationId);
    return this.http.get<CriticalityDto[]>(`${this.apiUrl}/history`, { params }).pipe(map((list) => (list ?? []).map(toAssessment)));
  }

  /** POST /api/asset-criticality — saves a new assessment that becomes the asset's current one. */
  create(assetRegistrationId: string, f: CriticalityAssessmentFields): Observable<CriticalityAssessment> {
    const body: CreateCriticalityRequest = {
      assetRegistrationId,
      assessmentDate: f.date,
      validUntil: f.validUntil || null,
      nextReviewDate: f.nextReview || null,
      assessedBy: f.assessedBy.trim(),
      approvedBy: f.approvedBy.trim(),
      likelihood: RISK_SCALE[Number(f.likelihood) - 1] ?? null,
      consequence: RISK_SCALE[Number(f.consequence) - 1] ?? null,
      criteria: f.criteria.map((c) => ({ name: c.name, weight: Number(c.weight), score: Number(c.score), justification: c.justification.trim() })),
      remarks: textOrNull(f.remarks),
      assessmentMethod: textOrNull(f.method),
      reviewFrequency: textOrNull(f.reviewFrequency),
      department: textOrNull(f.department),
      assetGroup: textOrNull(f.assetGroup),
      createdBy: this.auth.getUserEmail(),
      ...TENANT,
    };
    return this.http.post<CriticalityDto>(this.apiUrl, body).pipe(map(toAssessment));
  }

  /** PUT /api/asset-criticality/{id} — Edit Additional Information. */
  updateDetails(id: string, f: CriticalityDetailsFields): Observable<CriticalityAssessment> {
    const body: UpdateCriticalityDetailsRequest = {
      validUntil: f.validUntil,
      nextReviewDate: f.nextReview || null,
      assessedBy: f.assessedBy.trim(),
      approvedBy: f.approvedBy.trim(),
      assessmentMethod: textOrNull(f.method),
      reviewFrequency: textOrNull(f.reviewFrequency),
      department: textOrNull(f.department),
      assetGroup: textOrNull(f.assetGroup),
      remarks: textOrNull(f.remarks),
      updatedBy: this.auth.getUserEmail(),
    };
    return this.http.put<CriticalityDto>(`${this.apiUrl}/${encodeURIComponent(id)}`, body).pipe(map(toAssessment));
  }

  /** GET /api/asset-relationships?assetRegistrationId= (read only): the assets linked to this one, either way round. */
  getRelated(assetRegistrationId: string): Observable<RelatedAssetLink[]> {
    const params = new HttpParams().set('assetRegistrationId', assetRegistrationId).set('page', 1).set('pageSize', 100);
    return this.http.get<PagedDto<RelationshipDto>>(`${ASSET_API}asset-relationships`, { params }).pipe(
      map((res) => {
        const links: RelatedAssetLink[] = [];
        for (const r of res.items ?? []) {
          if (r.mainAssetRegistrationId === assetRegistrationId) {
            for (const sub of r.subAssets ?? []) {
              links.push({
                assetRegistrationId: sub.assetRegistrationId,
                tag: sub.tag || sub.assetRegistrationId || sub.componentId || '-',
                name: sub.name,
                location: sub.location ?? '',
                relation: `${sub.relationshipType} (Child)`,
              });
            }
          } else {
            const self = (r.subAssets ?? []).find((sub) => sub.assetRegistrationId === assetRegistrationId);
            links.push({
              assetRegistrationId: r.mainAssetRegistrationId,
              tag: r.mainAssetTag || r.mainAssetRegistrationId,
              name: r.mainAssetName ?? '',
              location: r.mainLocation ?? '',
              relation: `${self?.relationshipType ?? 'Related'} (Parent)`,
            });
          }
        }
        return links;
      }),
    );
  }

  /** GET /api/asset-risk-mitigation-actions?assetRegistrationId= */
  getMitigations(assetRegistrationId: string): Observable<MitigationAction[]> {
    const params = new HttpParams().set('assetRegistrationId', assetRegistrationId).set('page', 1).set('pageSize', MAX_PAGE);
    return this.http.get<MitigationListDto>(this.mitigationUrl, { params }).pipe(map((res) => (res.items ?? []).map(toMitigation)));
  }

  /** POST /api/asset-risk-mitigation-actions */
  createMitigation(assetRegistrationId: string, f: MitigationFields): Observable<MitigationAction> {
    const body: CreateMitigationRequest = {
      assetRegistrationId,
      action: f.action.trim(),
      owner: f.owner.trim(),
      dueDate: f.dueDate,
      status: f.status,
      expectedCriticality: f.reducesTo,
      createdBy: this.auth.getUserEmail(),
      ...TENANT,
    };
    return this.http.post<MitigationDto>(this.mitigationUrl, body).pipe(map(toMitigation));
  }

  /** PATCH /api/asset-risk-mitigation-actions/{id}/status */
  setMitigationStatus(id: string, status: MitigationStatus): Observable<MitigationAction> {
    return this.http.patch<MitigationDto>(`${this.mitigationUrl}/${encodeURIComponent(id)}/status`, { status }).pipe(map(toMitigation));
  }
}
