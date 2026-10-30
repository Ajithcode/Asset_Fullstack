import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, catchError } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { apiError } from './geofence-api.service';
import { Rule, Severity, Trigger } from './geofence-data';

/** API wording for the page's triggers and conditions (the API accepts exactly these strings). */
const TRIGGER_TEXT: Record<Trigger, string> = {
  'enter': 'When asset enters zone',
  'exit': 'When asset exits zone',
  'enter-exit': 'When asset enters or exits zone',
  'dwell': 'When asset remains in zone',
};
const CONDITION_TEXT: Record<string, string> = {
  'During all hours': 'During all hours',
  'Working hours': 'During working hours',
  'After hours': 'Outside working hours',
};

type Channel = 'In-app' | 'Email' | 'Push';

/** An alert rule as the API stores it ({trackingApiUrl}api/alert-rules). */
export interface AlertRuleDto {
  id: string;
  name: string;
  geofenceId: string;
  geofenceName: string;
  trigger: string;
  severity: Severity;
  assetGroup: string;
  condition: string;
  dwellMinutes: number | null;
  createAlert: boolean;
  notifyTeam: boolean;
  notifyTeamName: string | null;
  startWorkflow: boolean;
  workflowName: string | null;
  notificationChannels: Channel[];
  status: 'Active' | 'Disabled' | null;
  createdBy: string | null;
  createdAt: string | null;
}

/** Request body for POST and PUT /api/alert-rules (same shape for both). */
interface AlertRuleRequest {
  name: string;
  geofenceId: string;
  trigger: string;
  severity: Severity;
  assetGroup: string;
  condition: string;
  dwellMinutes: number | null;
  createAlert: boolean;
  notifyTeam: boolean;
  notifyTeamName: string | null;
  startWorkflow: boolean;
  workflowName: string | null;
  notificationChannels: Channel[];
  status: 'Active' | 'Disabled';
  createdBy: string;
  clientId: string;
  tenantId: string;
}

/** The page's rule fields from an API rule; `id` (the AR-nnn label) and `zoneId` are set by the page. */
export function ruleFromDto(dto: AlertRuleDto): Omit<Rule, 'id' | 'zoneId'> {
  const trigger = (Object.keys(TRIGGER_TEXT) as Trigger[]).find((t) => TRIGGER_TEXT[t] === dto.trigger) ?? 'enter-exit';
  const condition = Object.keys(CONDITION_TEXT).find((c) => CONDITION_TEXT[c] === dto.condition) ?? 'During all hours';
  return {
    apiId: dto.id,
    name: dto.name,
    trigger,
    dwellMin: dto.dwellMinutes ?? 5,
    severity: dto.severity,
    group: dto.assetGroup,
    condition,
    createAlert: dto.createAlert,
    notify: dto.notifyTeam,
    team: dto.notifyTeamName ?? '',
    workflow: dto.startWorkflow,
    workflowName: dto.workflowName ?? '',
    inApp: dto.notificationChannels.includes('In-app'),
    email: dto.notificationChannels.includes('Email'),
    push: dto.notificationChannels.includes('Push'),
    enabled: dto.status !== 'Disabled',
  };
}

@Injectable({ providedIn: 'root' })
export class AlertRuleApiService {
  private apiUrl = `${environment.trackingApiUrl}api/alert-rules`;

  constructor(private http: HttpClient, private auth: AuthService) {}

  /** GET /api/alert-rules, or only one geofence's rules with ?geofenceId= */
  getAll(geofenceId?: string): Observable<AlertRuleDto[]> {
    const params = geofenceId ? new HttpParams().set('geofenceId', geofenceId) : undefined;
    return this.http.get<AlertRuleDto[]>(this.apiUrl, { params }).pipe(catchError((e) => apiError(e, 'Could not load alert rules.')));
  }

  /** GET /api/alert-rules/{id} */
  getById(id: string): Observable<AlertRuleDto> {
    return this.http.get<AlertRuleDto>(`${this.apiUrl}/${id}`).pipe(catchError((e) => apiError(e, 'Could not load the alert rule.')));
  }

  /** POST /api/alert-rules. The API also raises the geofence's rule count. */
  create(rule: Rule, geofenceId: string): Observable<AlertRuleDto> {
    return this.http.post<AlertRuleDto>(this.apiUrl, this.request(rule, geofenceId)).pipe(catchError((e) => apiError(e, 'Could not save the alert rule.')));
  }

  /** PUT /api/alert-rules/{id} (full replace) */
  update(id: string, rule: Rule, geofenceId: string): Observable<AlertRuleDto> {
    return this.http.put<AlertRuleDto>(`${this.apiUrl}/${id}`, this.request(rule, geofenceId)).pipe(catchError((e) => apiError(e, 'Could not update the alert rule.')));
  }

  /** DELETE /api/alert-rules/{id} */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`).pipe(catchError((e) => apiError(e, 'Could not delete the alert rule.')));
  }

  private request(r: Rule, geofenceId: string): AlertRuleRequest {
    const channels: Channel[] = [];
    if (r.inApp) channels.push('In-app');
    if (r.email) channels.push('Email');
    if (r.push) channels.push('Push');
    return {
      name: r.name,
      geofenceId,
      trigger: TRIGGER_TEXT[r.trigger],
      severity: r.severity,
      assetGroup: r.group,
      condition: CONDITION_TEXT[r.condition] ?? 'During all hours',
      // the API wants minutes only for the dwell trigger
      dwellMinutes: r.trigger === 'dwell' ? Math.max(1, Math.round(r.dwellMin || 1)) : null,
      createAlert: r.createAlert,
      notifyTeam: r.notify,
      notifyTeamName: r.notify ? r.team : null,
      startWorkflow: r.workflow,
      workflowName: r.workflow ? r.workflowName : null,
      notificationChannels: channels,
      status: r.enabled ? 'Active' : 'Disabled',
      createdBy: this.auth.getUserEmail() || 'unknown',
      clientId: environment.clientId,
      tenantId: environment.tenantId,
    };
  }
}
