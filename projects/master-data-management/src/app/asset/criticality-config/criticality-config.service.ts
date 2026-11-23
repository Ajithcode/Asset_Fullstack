import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from 'shared-ui';
import { MDM_API } from '../../shared/mdm-api';

/** A level of the MDM Criticality master with its score range (null on levels that are not score based). */
export interface LevelDto {
  id: string;
  criticalityName: string;
  code: string;
  color: string | null;
  status: string | null;
  minScore: number | null;
  maxScore: number | null;
  displayOrder: number | null;
}

export interface CriterionDto {
  id: string;
  code: string;
  name: string;
  description: string | null;
  isActive: boolean;
  sortOrder: number;
}

export interface TemplateCriterionDto {
  criterionId: string;
  code: string;
  name: string;
  weight: number;
}

export interface TemplateDto {
  id: string;
  code: string;
  name: string;
  industry: string | null;
  description: string | null;
  isDefault: boolean;
  version: number;
  isLatest: boolean;
  isActive: boolean;
  totalWeight: number;
  criteria: TemplateCriterionDto[];
  createdAt: string | null;
  createdBy: string | null;
}

export interface TemplateSave {
  name: string;
  industry: string;
  description: string;
  isDefault: boolean;
  criteria: { criterionId: string; weight: number }[];
}

export interface ApprovalRuleDto {
  id: string;
  scenario: string;
  label: string;
  approverPermission: string;
  approverRoles: string[];
  approvalType: string;
  requiresJustification: boolean;
  description: string | null;
}

export interface ReassessmentRuleDto {
  id: string;
  levelName: string;
  reviewIntervalMonths: number;
}

export interface RangeSave {
  id: string;
  minScore: number;
  maxScore: number;
  color: string | null;
  displayOrder: number;
}

/**
 * Asset Criticality configuration. Levels (score range, colour, order) are the MDM Criticality master; criteria,
 * templates, approval matrix and review intervals are served by the Asset service under /api/asset-criticality/config.
 */
@Injectable({ providedIn: 'root' })
export class CriticalityConfigService {
  private readonly http = inject(HttpClient);
  private readonly config = `${environment.assetApiUrl}api/asset-criticality/config/`;

  levels(): Observable<LevelDto[]> {
    return this.http.get<LevelDto[]>(`${MDM_API}criticalities`, { params: { clientId: environment.clientId } });
  }

  saveRanges(levels: RangeSave[], updatedBy: string): Observable<LevelDto[]> {
    return this.http.put<LevelDto[]>(`${MDM_API}criticalities/ranges`, { levels, updatedBy, clientId: environment.clientId });
  }

  criteria(): Observable<CriterionDto[]> {
    return this.http.get<CriterionDto[]>(this.config + 'criteria');
  }

  createCriterion(body: Partial<CriterionDto>): Observable<CriterionDto> {
    return this.http.post<CriterionDto>(this.config + 'criteria', body);
  }

  updateCriterion(id: string, body: Partial<CriterionDto>): Observable<CriterionDto> {
    return this.http.put<CriterionDto>(`${this.config}criteria/${id}`, body);
  }

  deleteCriterion(id: string): Observable<unknown> {
    return this.http.delete(`${this.config}criteria/${id}`);
  }

  templates(): Observable<TemplateDto[]> {
    return this.http.get<TemplateDto[]>(this.config + 'templates');
  }

  templateVersions(id: string): Observable<TemplateDto[]> {
    return this.http.get<TemplateDto[]>(`${this.config}templates/${id}/versions`);
  }

  createTemplate(body: TemplateSave): Observable<TemplateDto> {
    return this.http.post<TemplateDto>(this.config + 'templates', body);
  }

  /** Saves a new version of the template. */
  updateTemplate(id: string, body: TemplateSave): Observable<TemplateDto> {
    return this.http.put<TemplateDto>(`${this.config}templates/${id}`, body);
  }

  retireTemplate(id: string): Observable<TemplateDto> {
    return this.http.delete<TemplateDto>(`${this.config}templates/${id}`);
  }

  approvalRules(): Observable<ApprovalRuleDto[]> {
    return this.http.get<ApprovalRuleDto[]>(this.config + 'approval-rules');
  }

  saveApprovalRule(rule: ApprovalRuleDto): Observable<ApprovalRuleDto> {
    const { approverPermission, approverRoles, approvalType, requiresJustification, description } = rule;
    return this.http.put<ApprovalRuleDto>(`${this.config}approval-rules/${encodeURIComponent(rule.scenario)}`, {
      approverPermission,
      approverRoles,
      approvalType,
      requiresJustification,
      description
    });
  }

  reassessmentRules(): Observable<ReassessmentRuleDto[]> {
    return this.http.get<ReassessmentRuleDto[]>(this.config + 'reassessment-rules');
  }

  saveReassessmentRule(levelName: string, reviewIntervalMonths: number): Observable<ReassessmentRuleDto> {
    return this.http.put<ReassessmentRuleDto>(`${this.config}reassessment-rules/${encodeURIComponent(levelName)}`, { reviewIntervalMonths });
  }
}
