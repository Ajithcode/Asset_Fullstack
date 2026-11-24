import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, catchError, forkJoin, map, of, tap } from 'rxjs';
import { environment } from 'shared-ui';
import { AuditCategory, AuditEvent, AuditFilters, AuditOutcome, AuditSeverity, SavedAuditFilter } from './audit-log.model';
import { AUDIT_CATEGORIES, AUDIT_DAY } from './audit-log.data';

// Audit events come from the EventLog service through the gateway (read-only and append-only):
// GET api/audit-logs (paged, filtered, with per-tab counts), /filters, /export and /{id}.

export function emptyFilters(): AuditFilters {
  return { from: AUDIT_DAY, to: AUDIT_DAY, eventType: '', module: '', user: '', severity: '', ip: '', outcome: '' };
}

/** AuditLogItemResponse */
interface AuditLogItemDto {
  id: string;
  timestamp: string | null;
  event: string;
  action: string;
  category: string | null;
  categoryLabel: string;
  module: string | null;
  user: string | null;
  userId: string | null;
  entityType: string;
  entityId: string;
  entityName: string | null;
  record: string;
  ipAddress: string | null;
  severity: string | null;
  outcome: string | null;
  details: string | null;
}

interface AuditCategoryCountDto {
  key: string;
  label: string;
  count: number;
}

/** AuditLogListResponse */
interface AuditLogListDto {
  items: AuditLogItemDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  category: string;
  categories: AuditCategoryCountDto[];
}

/** AuditLogDetailResponse */
interface AuditLogDetailDto {
  id: string;
  message: string;
  details: string | null;
  actor: {
    user: string | null;
    userId: string | null;
    email: string | null;
    role: string | null;
    deviceBrowser: string | null;
    requestId: string | null;
  };
}

/** AuditLogFiltersResponse */
interface AuditLogFiltersDto {
  eventTypes: { value: string; label: string }[];
  modules: string[];
  users: string[];
  severities: string[];
  outcomes: string[];
}

export type AuditSortKey = 'timestamp' | 'event' | 'user' | 'ip' | 'severity';

export interface AuditQuery {
  filters: AuditFilters;
  category: '' | AuditCategory;
  sortKey: AuditSortKey;
  sortDir: 1 | -1;
  page: number;
  pageSize: number;
}

export interface AuditPage {
  items: AuditEvent[];
  total: number;
  /** All Events count under the filters (every tab together). */
  allCount: number;
  categoryCounts: Map<AuditCategory, number>;
}

export interface AuditStats {
  eventsToday: number;
  eventsYesterday: number;
  criticalToday: number;
  criticalYesterday: number;
  configurationToday: number;
  configurationYesterday: number;
  failedToday: number;
  failedYesterday: number;
}

const SORT_FIELDS: Record<AuditSortKey, string> = {
  timestamp: 'timestamp',
  event: 'event',
  user: 'user',
  ip: 'ipAddress',
  severity: 'severity'
};

/** Local yyyy-mm-ddThh:mm:ss for the UTC timestamp the API returns. */
function localStamp(iso: string | null): string {
  if (!iso) return '';
  const d = new Date(/[zZ]|[+-]\d{2}:\d{2}$/.test(iso) ? iso : `${iso}Z`);
  if (Number.isNaN(d.getTime())) return iso;
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
}

const CHANGED_PREFIX = 'Changed: ';

/**
 * Edits that record what changed (role fields, permission matrix / module access) store it as
 * "Changed: Status: Active → Inactive; Assets › edit: none → full; … and 3 more". Split into the drawer's
 * before / after boxes, one "field: value" line per change; other details stay as they are.
 */
export function withChanges<T extends Pick<AuditEvent, 'details' | 'before' | 'after'>>(event: T): T {
  const details = event.details ?? '';
  if (!details.startsWith(CHANGED_PREFIX)) return event;
  const before: string[] = [];
  const after: string[] = [];
  let count = 0;
  for (const entry of details.slice(CHANGED_PREFIX.length).split('; ')) {
    const arrow = entry.lastIndexOf(' → ');
    const colon = arrow < 0 ? -1 : entry.indexOf(': ');
    if (arrow < 0 || colon < 0 || colon > arrow) {
      // "… and N more"
      before.push(entry);
      after.push(entry);
      continue;
    }
    const field = entry.slice(0, colon);
    before.push(`${field}: ${entry.slice(colon + 2, arrow)}`);
    after.push(`${field}: ${entry.slice(arrow + 3)}`);
    count++;
  }
  return {
    ...event,
    before: before.join('\n'),
    after: after.join('\n'),
    details: `${count} change${count === 1 ? '' : 's'}, shown under Value Change (— = not set).`
  };
}

function toEvent(dto: AuditLogItemDto): AuditEvent {
  return withChanges({
    id: dto.id,
    timestamp: localStamp(dto.timestamp),
    event: dto.event,
    category: (dto.categoryLabel || dto.category || '') as AuditCategory,
    module: dto.module ?? '',
    action: dto.action,
    user: dto.user ?? '',
    userId: dto.userId ?? '',
    // Filled from GET /api/audit-logs/{id} when the event is opened
    role: '',
    entityType: dto.entityType,
    entity: dto.record,
    recordId: dto.entityId,
    ip: dto.ipAddress ?? '',
    device: '',
    location: '',
    countryCode: '',
    requestId: '',
    severity: (dto.severity ?? 'Low') as AuditSeverity,
    outcome: (dto.outcome ?? 'Success') as AuditOutcome,
    // Session / MFA / risk are not recorded by the EventLog service
    mfa: 'Not enrolled',
    sessionId: '',
    authMethod: '',
    riskScore: 0,
    before: '',
    after: '',
    details: dto.details ?? ''
  });
}

export function auditErrorMessage(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    if (typeof err.error?.message === 'string') return err.error.message;
    if (err.status === 0) return 'The audit log service could not be reached.';
    if (err.status === 401 || err.status === 403) return 'You are not allowed to read the audit log.';
  }
  return 'The audit log could not be loaded.';
}

@Injectable({ providedIn: 'root' })
export class AuditLogService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.eventLogApiUrl}api/audit-logs`;
  /** Saved filters have no backend yet: kept for this session only. */
  private readonly saved: SavedAuditFilter[] = [];
  /** Event Type dropdown label -> API action value. */
  private readonly eventTypeValues = new Map<string, string>();

  readonly categories: AuditCategory[] = [...AUDIT_CATEGORIES];
  eventTypes: string[] = [];
  modules: string[] = [];
  users: string[] = [];
  outcomes: AuditOutcome[] = ['Success', 'Failed'];

  /** GET /api/audit-logs/filters: dropdown values. A failure leaves the dropdowns empty. */
  loadFilterOptions(): Observable<void> {
    return this.http.get<AuditLogFiltersDto>(`${this.apiUrl}/filters`).pipe(
      tap((dto) => {
        this.eventTypeValues.clear();
        for (const t of dto.eventTypes ?? []) this.eventTypeValues.set(t.label, t.value);
        this.eventTypes = [...this.eventTypeValues.keys()].sort();
        this.modules = [...(dto.modules ?? [])].sort();
        this.users = [...(dto.users ?? [])].sort();
        if (dto.outcomes?.length) this.outcomes = dto.outcomes as AuditOutcome[];
      }),
      map(() => undefined),
      catchError(() => of(undefined))
    );
  }

  private params(filters: AuditFilters, category: '' | AuditCategory): HttpParams {
    let params = new HttpParams();
    const set = (key: string, value: string | null | undefined) => {
      if (value && value.trim()) params = params.set(key, value.trim());
    };
    set('category', category);
    set('from', filters.from);
    set('to', filters.to);
    set('eventType', filters.eventType ? (this.eventTypeValues.get(filters.eventType) ?? filters.eventType) : '');
    set('module', filters.module);
    set('user', filters.user);
    set('severity', filters.severity);
    set('ipAddress', filters.ip);
    set('outcome', filters.outcome);
    return params;
  }

  /** GET /api/audit-logs: one page of the current tab, plus every tab's count under the same filters. */
  query(q: AuditQuery): Observable<AuditPage> {
    const params = this.params(q.filters, q.category)
      .set('sortBy', SORT_FIELDS[q.sortKey])
      .set('sortDir', q.sortDir === 1 ? 'asc' : 'desc')
      .set('page', q.page)
      .set('pageSize', q.pageSize);
    return this.http.get<AuditLogListDto>(this.apiUrl, { params }).pipe(
      map((dto) => {
        const counts = new Map<AuditCategory, number>();
        let allCount = 0;
        for (const c of dto.categories ?? []) {
          if (c.key === 'all') allCount = c.count;
          else counts.set(c.label as AuditCategory, c.count);
        }
        return { items: (dto.items ?? []).map(toEvent), total: dto.totalCount ?? 0, allCount, categoryCounts: counts };
      })
    );
  }

  /** GET /api/audit-logs/{id}: actor details (role, email, device, request id) for the drawer. */
  detail(event: AuditEvent): Observable<AuditEvent> {
    return this.http.get<AuditLogDetailDto>(`${this.apiUrl}/${encodeURIComponent(event.id)}`).pipe(
      map((dto) =>
        withChanges({
          ...event,
          role: dto.actor?.role ?? event.role,
          device: dto.actor?.deviceBrowser ?? event.device,
          requestId: dto.actor?.requestId ?? event.requestId,
          // The list already split the change line; only take the server's text when it is new
          details: dto.details && !event.after ? dto.details : event.details
        })
      )
    );
  }

  /** Header stats: today against yesterday (events, critical, configuration changes, failed actions). */
  stats(): Observable<AuditStats> {
    const day = (offset: number) => {
      const d = new Date();
      d.setDate(d.getDate() + offset);
      return d.toISOString().slice(0, 10);
    };
    const count = (date: string, extra: Partial<AuditFilters>) =>
      this.query({
        filters: { ...emptyFilters(), from: date, to: date, ...extra },
        category: '',
        sortKey: 'timestamp',
        sortDir: -1,
        page: 1,
        pageSize: 1
      }).pipe(catchError(() => of<AuditPage>({ items: [], total: 0, allCount: 0, categoryCounts: new Map() })));
    const today = day(0);
    const yesterday = day(-1);
    return forkJoin({
      t: count(today, {}),
      y: count(yesterday, {}),
      tc: count(today, { severity: 'Critical' }),
      yc: count(yesterday, { severity: 'Critical' }),
      tf: count(today, { outcome: 'Failed' }),
      yf: count(yesterday, { outcome: 'Failed' })
    }).pipe(
      map(({ t, y, tc, yc, tf, yf }) => ({
        eventsToday: t.allCount,
        eventsYesterday: y.allCount,
        criticalToday: tc.allCount,
        criticalYesterday: yc.allCount,
        configurationToday: t.categoryCounts.get('Configuration') ?? 0,
        configurationYesterday: y.categoryCounts.get('Configuration') ?? 0,
        failedToday: tf.allCount,
        failedYesterday: yf.allCount
      }))
    );
  }

  savedFilters(): readonly SavedAuditFilter[] {
    return this.saved;
  }

  saveFilter(filter: SavedAuditFilter): void {
    const i = this.saved.findIndex((s) => s.name.toLowerCase() === filter.name.toLowerCase());
    if (i >= 0) this.saved[i] = filter;
    else this.saved.push(filter);
  }
}
