import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import {
  CalibrationInstrument,
  Checklist,
  Inspection,
  InspectionAudit,
  InspectionFinding,
  InspectionPlan,
  InspectionTask,
  InspectionType,
  NotificationTemplate,
  NumberingSequence,
  ReportTemplate,
  SignatureAndStamp,
  TaskCategory,
  WorkingCalendar
} from './inspection.models';

/** Inspection service routes, reached through the API gateway. */
export const INSPECTION_API = `${environment.inspectionApiUrl}api/`;

/** Readable message for a failed call: first validation error, the server's `message`, or the status. */
export function apiErrorMessage(err: unknown): string {
  if (!(err instanceof HttpErrorResponse)) return 'Something went wrong.';
  if (err.status === 0) return 'Inspection service is unreachable.';
  const body = err.error;
  if (body?.errors && typeof body.errors === 'object') {
    const first = Object.values(body.errors as Record<string, string[]>).flat()[0];
    if (first) return first;
  }
  if (typeof body?.message === 'string') return body.message;
  if (typeof body?.title === 'string') return body.title;
  if (err.status === 404) return 'Record not found.';
  return `Request failed (${err.status}).`;
}

/**
 * In-memory copy of one API resource. Pages read `items()` and call `load()` on init; `save()` creates
 * when the record has no id, otherwise updates, and keeps `items()` in step with the server.
 */
export class ResourceStore<T extends InspectionAudit> {
  readonly items = signal<T[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly loaded = signal(false);
  readonly count = computed(() => this.items().length);

  constructor(
    private readonly http: HttpClient,
    private readonly auth: AuthService,
    readonly resource: string
  ) {}

  /** Fetches the list once; pass `force` to refetch. */
  load(force = false): void {
    if ((this.loaded() || this.loading()) && !force) return;
    this.loading.set(true);
    this.error.set(null);
    this.http.get<T[]>(this.url()).subscribe({
      next: (rows) => {
        this.items.set(rows);
        this.loaded.set(true);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(apiErrorMessage(err));
        this.loading.set(false);
      }
    });
  }

  byId(id: string | null | undefined): T | undefined {
    return id ? this.items().find((r) => r.id === id) : undefined;
  }

  save(record: Partial<T>): Observable<T> {
    const actor = this.auth.getUserEmail() || 'unknown';
    if (record.id) {
      return this.http
        .put<T>(this.url(record.id), { ...record, updatedBy: actor })
        .pipe(tap((saved) => this.items.update((rows) => rows.map((r) => (r.id === saved.id ? saved : r)))));
    }
    const body = { ...record, createdBy: actor, clientId: environment.clientId, tenantId: environment.tenantId };
    return this.http.post<T>(this.url(), body).pipe(tap((saved) => this.items.update((rows) => [saved, ...rows])));
  }

  remove(id: string): Observable<unknown> {
    return this.http.delete(this.url(id)).pipe(tap(() => this.items.update((rows) => rows.filter((r) => r.id !== id))));
  }

  private url(id?: string): string {
    return `${INSPECTION_API}${this.resource}${id ? `/${id}` : ''}`;
  }
}

/** One store per Inspection resource, shared by every page so data loads once per session. */
@Injectable({ providedIn: 'root' })
export class InspectionApi {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  readonly types = this.store<InspectionType>('inspection-types');
  readonly taskCategories = this.store<TaskCategory>('task-categories');
  readonly tasks = this.store<InspectionTask>('inspection-tasks');
  readonly checklists = this.store<Checklist>('checklists');
  readonly plans = this.store<InspectionPlan>('inspection-plans');
  readonly inspections = this.store<Inspection>('inspections');
  readonly findings = this.store<InspectionFinding>('inspection-findings');
  readonly instruments = this.store<CalibrationInstrument>('calibration-instruments');
  readonly signatures = this.store<SignatureAndStamp>('signatures-and-stamps');
  readonly notificationTemplates = this.store<NotificationTemplate>('notification-templates');
  readonly reportTemplates = this.store<ReportTemplate>('report-templates');
  readonly sequences = this.store<NumberingSequence>('numbering-sequences');
  readonly calendars = this.store<WorkingCalendar>('holiday-and-working-calendars');

  private store<T extends InspectionAudit>(resource: string): ResourceStore<T> {
    return new ResourceStore<T>(this.http, this.auth, resource);
  }
}
