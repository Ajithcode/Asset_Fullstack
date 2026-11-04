import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { AuthService } from 'shared-ui';
import { ASSET_API, TENANT } from '../services/asset-api';
import { Condition } from '../asset-details/asset-details';

/** A condition assessment as the pages use it (Assessment from asset-details, with the API's string id). */
export interface ConditionAssessment {
  id: string;
  assetRegistrationId: string;
  /** yyyy-MM-dd */
  date: string;
  assessedBy: string;
  overall: Condition;
  healthScore: number;
  physical: Condition;
  performance: Condition;
  safety: Condition;
  remarks: string;
}

/** Fields of the New / Edit Condition Assessment form. */
export type ConditionAssessmentFields = Omit<ConditionAssessment, 'id' | 'assetRegistrationId'>;

/** AssetConditionAssessmentResponse */
interface ConditionAssessmentDto {
  id: string;
  assetRegistrationId: string;
  assetName: string | null;
  assetTag: string | null;
  assessmentDate: string;
  assessedBy: string;
  overallCondition: string;
  healthScore: number;
  physicalCondition: string | null;
  performance: string | null;
  safety: string | null;
  remarks: string | null;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  clientId: string | null;
  tenantId: string | null;
}

interface ConditionAssessmentListDto {
  items: ConditionAssessmentDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** Body of PUT /api/asset-condition-assessments/{id} (UpdateAssetConditionAssessmentRequest). */
interface UpdateConditionAssessmentRequest {
  assessmentDate: string;
  assessedBy: string;
  overallCondition: string;
  healthScore: number;
  physicalCondition: string | null;
  performance: string | null;
  safety: string | null;
  remarks: string | null;
}

/** Body of POST /api/asset-condition-assessments (CreateAssetConditionAssessmentRequest). */
interface CreateConditionAssessmentRequest extends UpdateConditionAssessmentRequest {
  assetRegistrationId: string;
  createdBy: string | null;
  clientId: string;
  tenantId: string;
}

/** Largest page the API serves. */
const MAX_PAGE = 200;

function toCondition(value: string | null | undefined, fallback: Condition): Condition {
  return (['Excellent', 'Good', 'Fair', 'Poor', 'Critical'] as Condition[]).find((c) => c.toLowerCase() === value?.toLowerCase()) ?? fallback;
}

function toAssessment(dto: ConditionAssessmentDto): ConditionAssessment {
  const overall = toCondition(dto.overallCondition, 'Good');
  return {
    id: dto.id,
    assetRegistrationId: dto.assetRegistrationId,
    date: (dto.assessmentDate ?? '').slice(0, 10),
    assessedBy: dto.assessedBy ?? '',
    overall,
    healthScore: Number(dto.healthScore) || 0,
    physical: toCondition(dto.physicalCondition, overall),
    performance: toCondition(dto.performance, overall),
    safety: toCondition(dto.safety, overall),
    remarks: dto.remarks ?? '',
  };
}

function toRequest(f: ConditionAssessmentFields): UpdateConditionAssessmentRequest {
  return {
    assessmentDate: f.date,
    assessedBy: f.assessedBy.trim(),
    overallCondition: f.overall,
    healthScore: Math.round(Number(f.healthScore)),
    physicalCondition: f.physical || null,
    performance: f.performance || null,
    safety: f.safety || null,
    remarks: f.remarks.trim() || null,
  };
}

@Injectable({ providedIn: 'root' })
export class AssetConditionService {
  private readonly apiUrl = `${ASSET_API}asset-condition-assessments`;

  constructor(private http: HttpClient, private auth: AuthService) {}

  /** GET /api/asset-condition-assessments?assetRegistrationId= — the asset's assessments, latest first. */
  getForAsset(assetRegistrationId: string): Observable<ConditionAssessment[]> {
    const params = new HttpParams().set('assetRegistrationId', assetRegistrationId).set('page', 1).set('pageSize', MAX_PAGE);
    return this.http.get<ConditionAssessmentListDto>(this.apiUrl, { params }).pipe(map((res) => (res.items ?? []).map(toAssessment)));
  }

  /** GET /api/asset-condition-assessments — latest assessments of every asset (newest first), up to one page of 200. */
  getRecent(): Observable<ConditionAssessment[]> {
    const params = new HttpParams().set('page', 1).set('pageSize', MAX_PAGE);
    return this.http.get<ConditionAssessmentListDto>(this.apiUrl, { params }).pipe(map((res) => (res.items ?? []).map(toAssessment)));
  }

  /** GET /api/asset-condition-assessments/ratings */
  getRatings(): Observable<string[]> {
    return this.http.get<string[]>(`${this.apiUrl}/ratings`);
  }

  /** POST /api/asset-condition-assessments */
  create(assetRegistrationId: string, fields: ConditionAssessmentFields): Observable<ConditionAssessment> {
    const body: CreateConditionAssessmentRequest = {
      ...toRequest(fields),
      assetRegistrationId,
      createdBy: this.auth.getUserEmail(),
      ...TENANT,
    };
    return this.http.post<ConditionAssessmentDto>(this.apiUrl, body).pipe(map(toAssessment));
  }

  /** PUT /api/asset-condition-assessments/{id} */
  update(id: string, fields: ConditionAssessmentFields): Observable<ConditionAssessment> {
    return this.http.put<ConditionAssessmentDto>(`${this.apiUrl}/${encodeURIComponent(id)}`, toRequest(fields)).pipe(map(toAssessment));
  }
}
