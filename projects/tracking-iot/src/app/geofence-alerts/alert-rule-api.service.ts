import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map } from 'rxjs';
import { AuthService } from 'shared-ui';
import { apiError, tenantBody, tenantParams, trackingUrl } from '../shared/tracking-api';
import { actorName } from './geofence-api.service';
import { MAP_ZONE_PREFIX, RULE_STATUSES, Rule, RuleStatus, SEVERITIES, Severity, Trigger } from './geofence-data';

/** Fields added for the enterprise rule builder (same names on the DTO and the request). */
interface RuleExtras {
  description: string | null;
  assetIds: string[];
  authorizedGroups: string[];
  scheduleDays: string[];
  scheduleFrom: string | null;
  scheduleTo: string | null;
  debounceSeconds: number;
  cooldownMinutes: number;
  escalateAfterMinutes: number | null;
  escalateTo: string | null;
  autoResolveOnExit: boolean;
  effectiveFrom: string | null;
  effectiveTo: string | null;
}

/** GET /api/alert-rules row. */
export interface AlertRuleDto extends Partial<RuleExtras> {
  id: string;
  name: string;
  geofenceId: string;
  geofenceName: string;
  trigger: string;
  severity: string;
  assetGroup: string;
  condition: string;
  dwellMinutes: number | null;
  createAlert: boolean;
  notifyTeam: boolean;
  notifyTeamName: string | null;
  startWorkflow: boolean;
  workflowName: string | null;
  notificationChannels: string[];
  status: string | null;
  createdBy: string | null;
  createdAt: string | null;
  clientId: string | null;
  tenantId: string | null;
  mapZoneId?: string | null;
  notifyIntervalSeconds?: number;
  notifyRepeat?: number;
  notifySound?: boolean;
  updatedAt?: string | null;
}

/** POST / PUT /api/alert-rules body. */
export interface AlertRuleRequest extends RuleExtras {
  name: string;
  geofenceId: string;
  trigger: string;
  severity: string;
  assetGroup: string;
  condition: string;
  dwellMinutes: number | null;
  createAlert: boolean;
  notifyTeam: boolean;
  notifyTeamName: string | null;
  startWorkflow: boolean;
  workflowName: string | null;
  notificationChannels: string[];
  status: string;
  createdBy: string;
  clientId: string;
  tenantId: string;
  /** Floor plan zone (maps zone id) instead of an outdoor geofence. */
  mapZoneId: string | null;
  notifyIntervalSeconds: number;
  notifyRepeat: number;
  notifySound: boolean;
}

/** Page trigger ↔ API trigger text (AlertRuleTrigger in the Tracking service). */
const TRIGGER_TO_API: Record<Trigger, string> = {
  'enter-exit': 'When asset enters or exits zone',
  enter: 'When asset enters zone',
  exit: 'When asset exits zone',
  dwell: 'When asset remains in zone',
  unauthorized: 'When unauthorized asset enters zone',
  lost: 'When asset location is lost in zone',
};

/** yyyy-mm-dd of an ISO date ('' when none). */
const day = (iso: string | null | undefined): string => (iso ? iso.slice(0, 10) : '');

function triggerFromApi(t: string): Trigger {
  const hit = (Object.keys(TRIGGER_TO_API) as Trigger[]).find((k) => TRIGGER_TO_API[k].toLowerCase() === (t ?? '').toLowerCase());
  return hit ?? 'enter-exit';
}

export function toRule(d: AlertRuleDto): Rule {
  const channels = (d.notificationChannels ?? []).map((c) => c.toLowerCase());
  return {
    id: d.id,
    name: d.name,
    zoneId: d.geofenceId,
    trigger: triggerFromApi(d.trigger),
    dwellMin: d.dwellMinutes ?? 0,
    severity: ((SEVERITIES as string[]).includes(d.severity) ? d.severity : 'Medium') as Severity,
    group: d.assetGroup,
    condition: d.condition,
    createAlert: d.createAlert,
    notify: d.notifyTeam,
    team: d.notifyTeamName ?? '',
    workflow: d.startWorkflow,
    workflowName: d.workflowName ?? '',
    inApp: channels.includes('in-app'),
    email: channels.includes('email'),
    push: channels.includes('push'),
    enabled: (d.status ?? 'Active') === 'Active',
    status: ((RULE_STATUSES as string[]).includes(d.status ?? '') ? d.status : 'Active') as RuleStatus,
    description: d.description ?? '',
    assetIds: d.assetIds ?? [],
    authorizedGroups: d.authorizedGroups ?? [],
    scheduleDays: d.scheduleDays ?? [],
    scheduleFrom: d.scheduleFrom ?? '',
    scheduleTo: d.scheduleTo ?? '',
    debounceSec: d.debounceSeconds ?? 0,
    cooldownMin: d.cooldownMinutes ?? 0,
    escalateAfterMin: d.escalateAfterMinutes ?? 0,
    escalateTo: d.escalateTo ?? '',
    autoResolve: d.autoResolveOnExit ?? false,
    effectiveFrom: day(d.effectiveFrom),
    effectiveTo: day(d.effectiveTo),
    updatedAt: d.updatedAt ? new Date(d.updatedAt) : d.createdAt ? new Date(d.createdAt) : null,
    createdBy: d.createdBy ?? '',
    notifyEvery: d.notifyIntervalSeconds ?? 5,
    notifyRepeat: d.notifyRepeat ?? 1,
    notifySound: d.notifySound ?? true,
    zoneLabel: d.geofenceName,
  };
}

export function toAlertRuleRequest(r: Omit<Rule, 'id'>, createdBy: string): AlertRuleRequest {
  return {
    name: r.name,
    geofenceId: r.zoneId.startsWith(MAP_ZONE_PREFIX) ? '' : r.zoneId,
    mapZoneId: r.zoneId.startsWith(MAP_ZONE_PREFIX) ? r.zoneId.slice(MAP_ZONE_PREFIX.length) : null,
    notifyIntervalSeconds: r.notifyEvery || 5,
    notifyRepeat: Math.max(0, r.notifyRepeat ?? 1),
    notifySound: r.notifySound !== false,
    trigger: TRIGGER_TO_API[r.trigger],
    severity: r.severity,
    assetGroup: r.group,
    condition: r.condition,
    dwellMinutes: r.trigger === 'dwell' ? Math.max(1, Math.round(r.dwellMin || 1)) : null,
    createAlert: r.createAlert,
    notifyTeam: r.notify,
    notifyTeamName: r.notify ? r.team.trim() || null : null,
    startWorkflow: r.workflow,
    workflowName: r.workflow ? r.workflowName.trim() || null : null,
    notificationChannels: [r.inApp ? 'In-app' : '', r.email ? 'Email' : '', r.push ? 'Push' : ''].filter(Boolean),
    status: r.status === 'Draft' ? 'Draft' : r.enabled ? 'Active' : 'Disabled',
    description: r.description?.trim() || null,
    assetIds: r.assetIds ?? [],
    authorizedGroups: r.trigger === 'unauthorized' ? r.authorizedGroups ?? [] : [],
    scheduleDays: r.scheduleDays ?? [],
    scheduleFrom: r.scheduleFrom || null,
    scheduleTo: r.scheduleTo || null,
    debounceSeconds: Math.max(0, r.debounceSec || 0),
    cooldownMinutes: Math.max(0, r.cooldownMin || 0),
    escalateAfterMinutes: r.escalateAfterMin > 0 ? r.escalateAfterMin : null,
    escalateTo: r.escalateAfterMin > 0 ? r.escalateTo.trim() || null : null,
    autoResolveOnExit: !!r.autoResolve,
    effectiveFrom: r.effectiveFrom ? new Date(r.effectiveFrom + 'T00:00:00').toISOString() : null,
    effectiveTo: r.effectiveTo ? new Date(r.effectiveTo + 'T23:59:59').toISOString() : null,
    createdBy,
    ...tenantBody(),
  };
}

/** Alert Rule Builder: /api/alert-rules. */
@Injectable({ providedIn: 'root' })
export class AlertRuleApiService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly url = trackingUrl('alert-rules');

  list(geofenceId?: string): Observable<Rule[]> {
    return this.http.get<AlertRuleDto[]>(this.url, { params: tenantParams({ geofenceId }) }).pipe(
      map((rows) => rows.map(toRule)),
      catchError((err) => apiError(err, 'Could not load alert rules.')),
    );
  }

  create(r: Omit<Rule, 'id'>): Observable<Rule> {
    return this.http.post<AlertRuleDto>(this.url, toAlertRuleRequest(r, actorName(this.auth)), { params: tenantParams() }).pipe(
      map(toRule),
      catchError((err) => apiError(err, 'Could not save the alert rule.')),
    );
  }

  update(r: Rule): Observable<Rule> {
    return this.http.put<AlertRuleDto>(`${this.url}/${encodeURIComponent(r.id)}`, toAlertRuleRequest(r, actorName(this.auth)), { params: tenantParams() }).pipe(
      map(toRule),
      catchError((err) => apiError(err, 'Could not update the alert rule.')),
    );
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${encodeURIComponent(id)}`, { params: tenantParams() }).pipe(
      catchError((err) => apiError(err, 'Could not delete the alert rule.')),
    );
  }
}
