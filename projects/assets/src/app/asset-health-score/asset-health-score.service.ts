import { Injectable } from '@angular/core';
import { Observable, catchError, forkJoin, map, of } from 'rxjs';
import { Condition, HealthComponent, HealthData, HealthInsight, HealthRecord, HealthStatus, MaintenanceTask } from '../asset-details/asset-details';
import { AssetConditionService, ConditionAssessment, ConditionAssessmentFields } from '../asset-status-condition/asset-condition.service';
import { AssetCriticalityService, CriticalityAssessment } from '../asset-criticality/asset-criticality.service';

/**
 * The Asset API has no health score resource: the health of an asset is worked out here from its
 * condition assessments (GET /api/asset-condition-assessments) and its current criticality (GET /api/asset-criticality/history).
 * Recording a health check saves a condition assessment.
 */

/** Health score drawn for each condition rating (same scale as the Status & Condition trend chart). */
const RATING_SCORE: Record<Condition, number> = { Excellent: 100, Good: 85, Fair: 70, Poor: 55, Critical: 40 };

/** Remarks of an assessment recorded with "Run Health Check"; shown as a Manual trigger. */
export const HEALTH_CHECK_REMARKS = 'Manual health check';

/** Months from one assessment to the next (as scheduled on the Status & Condition page). */
const ASSESSMENT_INTERVAL_MONTHS = 6;

export function healthStatus(score: number): HealthStatus {
  return score >= 85 ? 'Healthy' : score >= 70 ? 'Good' : score >= 55 ? 'Fair' : score >= 40 ? 'Poor' : 'Critical';
}

/** Overall condition rating closest to a health score. */
export function conditionForScore(score: number): Condition {
  return score >= 92.5 ? 'Excellent' : score >= 77.5 ? 'Good' : score >= 62.5 ? 'Fair' : score >= 47.5 ? 'Poor' : 'Critical';
}

function gradeFor(overall: Condition): string {
  return overall === 'Excellent' || overall === 'Good' ? 'Good' : overall === 'Fair' ? 'Fair' : 'Poor';
}

function addMonths(iso: string, months: number): string {
  const [y, m, d] = iso.split('-').map(Number);
  const date = new Date(y, m - 1 + months, d);
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
}

function newestFirst(list: ConditionAssessment[]): ConditionAssessment[] {
  return [...list].sort((a, b) => b.date.localeCompare(a.date));
}

/** Parts of the health score, from the ratings of the latest condition assessment. */
function breakdownFor(a: ConditionAssessment): HealthComponent[] {
  return [
    { key: 'performance', name: 'Performance', weight: 30, score: RATING_SCORE[a.performance] },
    { key: 'reliability', name: 'Reliability', weight: 20, score: RATING_SCORE[a.overall] },
    { key: 'condition', name: 'Condition', weight: 30, score: RATING_SCORE[a.physical] },
    { key: 'safety', name: 'Risk & Safety', weight: 20, score: RATING_SCORE[a.safety] },
  ];
}

function insightsFor(health: Omit<HealthData, 'insights'>, crit: CriticalityAssessment | null, today: string): HealthInsight[] {
  const insights: HealthInsight[] = [];
  const [latest, previous] = health.history;
  const weakest = [...health.breakdown].sort((a, b) => a.score - b.score)[0];

  if (health.nextAssessment && health.nextAssessment < today) {
    insights.push({ type: 'warning', text: `The next condition assessment was due on ${health.nextAssessment}.` });
  }
  if (crit && (crit.level === 'Critical' || crit.level === 'High') && health.score < 70) {
    insights.push({
      type: 'warning',
      text: `${crit.level} criticality asset (${crit.score} / 100) in ${health.status.toLowerCase()} health: prioritise corrective maintenance.`,
    });
  } else if (crit) {
    insights.push({ type: 'info', text: `Criticality is ${crit.level} (${crit.score} / 100), assessed on ${crit.assessmentDate}.` });
  }
  if (weakest && weakest.score < 85) {
    insights.push({ type: 'warning', text: `${weakest.name} is the weakest area (${weakest.score}%). Consider an inspection.` });
  } else if (weakest) {
    insights.push({ type: 'success', text: `All health areas are at ${weakest.score}% or better.` });
  }
  if (latest && previous) {
    const change = latest.score - previous.score;
    insights.push(
      change === 0
        ? { type: 'info', text: 'Health score is unchanged since the previous assessment.' }
        : { type: change > 0 ? 'success' : 'warning', text: `Health score ${change > 0 ? 'improved' : 'dropped'} by ${Math.abs(change)} points compared to the previous assessment.` },
    );
  }
  return insights;
}

/** Details edited on the page that have no field in the Asset API (kept in memory). */
export interface HealthOverrides {
  method?: string;
  nextAssessment?: string;
}

/** Health of one asset, or null when it has no condition assessment yet. */
export function buildHealth(
  assessments: ConditionAssessment[],
  crit: CriticalityAssessment | null,
  maintenancePlan: MaintenanceTask[],
  overrides: HealthOverrides,
  today: string,
): HealthData | null {
  const list = newestFirst(assessments);
  const latest = list[0];
  if (!latest) return null;
  const score = Math.round(latest.healthScore);
  const history: HealthRecord[] = list.map((a) => ({
    date: a.date,
    score: Math.round(a.healthScore),
    status: healthStatus(a.healthScore),
    trigger: a.remarks === HEALTH_CHECK_REMARKS ? 'Manual' : 'Scheduled',
    comments: a.remarks || `Condition assessed as ${a.overall}`,
    assessedBy: a.assessedBy,
  }));
  const base: Omit<HealthData, 'insights'> = {
    score,
    status: healthStatus(score),
    grade: gradeFor(latest.overall),
    assessmentDate: latest.date,
    nextAssessment: overrides.nextAssessment ?? addMonths(latest.date, ASSESSMENT_INTERVAL_MONTHS),
    assessedBy: latest.assessedBy,
    method: overrides.method ?? 'Condition Assessment',
    remarks: latest.remarks,
    breakdown: breakdownFor(latest),
    history,
    maintenancePlan,
  };
  return { ...base, insights: insightsFor(base, crit, today) };
}

export interface AssetHealthSource {
  assessments: ConditionAssessment[];
  /** Current criticality assessment, or null when the asset has none (or it could not be read). */
  criticality: CriticalityAssessment | null;
}

@Injectable({ providedIn: 'root' })
export class AssetHealthScoreService {
  constructor(private condition: AssetConditionService, private criticality: AssetCriticalityService) {}

  /** Condition assessments and current criticality of one asset. */
  load(assetRegistrationId: string): Observable<AssetHealthSource> {
    return forkJoin({
      assessments: this.condition.getForAsset(assetRegistrationId),
      criticality: this.criticality.getHistory(assetRegistrationId).pipe(
        map((list) => list.find((c) => c.isCurrent) ?? list[0] ?? null),
        catchError(() => of(null)),
      ),
    });
  }

  /** Health Index: share of assets whose latest assessment is Healthy or Good (from the latest 200 assessments). */
  healthIndex(): Observable<number | null> {
    return this.condition.getRecent().pipe(
      map((list) => {
        const latest = new Map<string, ConditionAssessment>();
        for (const a of newestFirst(list)) if (!latest.has(a.assetRegistrationId)) latest.set(a.assetRegistrationId, a);
        const all = [...latest.values()];
        if (!all.length) return null;
        const healthy = all.filter((a) => ['Healthy', 'Good'].includes(healthStatus(a.healthScore))).length;
        return Math.round((healthy / all.length) * 100);
      }),
    );
  }

  /** Run Health Check: records a condition assessment with the score worked out from the breakdown. */
  recordHealthCheck(assetRegistrationId: string, latest: ConditionAssessment, score: number, assessedBy: string, today: string): Observable<ConditionAssessment> {
    const fields: ConditionAssessmentFields = {
      date: today,
      assessedBy,
      overall: conditionForScore(score),
      healthScore: score,
      physical: latest.physical,
      performance: latest.performance,
      safety: latest.safety,
      remarks: HEALTH_CHECK_REMARKS,
    };
    return this.condition.create(assetRegistrationId, fields);
  }

  /** Edit Health Details: Assessed By and Remarks are saved on the latest condition assessment. */
  updateLatest(latest: ConditionAssessment, assessedBy: string, remarks: string): Observable<ConditionAssessment> {
    const { id, assetRegistrationId, ...fields } = latest;
    return this.condition.update(id, { ...fields, assessedBy, remarks });
  }
}
