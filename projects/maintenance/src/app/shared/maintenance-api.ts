import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { MaintenanceAudit, PredictiveMaintenance, PreventiveMaintenance, ServiceRequest, WorkOrder } from './maintenance.models';

/** Maintenance service routes, reached through the API gateway. */
export const MAINTENANCE_API = `${environment.maintenanceApiUrl}api/`;

/** Readable message for a failed call: first validation error, the server's `message`, or the status. */
export function apiErrorMessage(err: unknown): string {
  if (!(err instanceof HttpErrorResponse)) return 'Something went wrong.';
  if (err.status === 0) return 'Maintenance service is unreachable.';
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
export class ResourceStore<T extends MaintenanceAudit> {
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
        this.items.set(rows.filter((r) => !r.isDeleted));
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
    const actor = currentUserName(this.auth);
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
    return `${MAINTENANCE_API}${this.resource}${id ? `/${id}` : ''}`;
  }
}

/** Display name of the signed-in user (user name, else email). */
export function currentUserName(auth: AuthService): string {
  return auth.getUser()?.userName || auth.getUserEmail() || 'unknown';
}

/** One store per Maintenance resource, shared by every page so data loads once per session. */
@Injectable({ providedIn: 'root' })
export class MaintenanceApi {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  readonly preventive = this.store<PreventiveMaintenance>('preventive-maintenance');
  readonly predictive = this.store<PredictiveMaintenance>('predictive-maintenance');
  readonly serviceRequests = this.store<ServiceRequest>('issue-reports');
  readonly workOrders = this.store<WorkOrder>('work-orders');

  /** True when `name` is the signed-in user (matches user name or email, case-insensitive). */
  isMe(name: string | null | undefined): boolean {
    const n = (name ?? '').trim().toLowerCase();
    if (!n) return false;
    const user = this.auth.getUser();
    return [user?.userName, user?.email, this.auth.getUserEmail()].some((v) => !!v && v.toLowerCase() === n);
  }

  private store<T extends MaintenanceAudit>(resource: string): ResourceStore<T> {
    return new ResourceStore<T>(this.http, this.auth, resource);
  }
}

/**
 * Next running number for a code series, e.g. nextCode('WO', ['WO-2026-007']) -> 'WO-2026-008'.
 * Codes from other years are ignored so each year restarts at 001.
 */
export function nextCode(prefix: string, existing: (string | null | undefined)[]): string {
  const year = new Date().getFullYear();
  const head = `${prefix}-${year}-`;
  const max = existing.reduce((m, c) => {
    if (!c?.startsWith(head)) return m;
    const n = parseInt(c.slice(head.length), 10);
    return isNaN(n) ? m : Math.max(m, n);
  }, 0);
  return `${head}${String(max + 1).padStart(3, '0')}`;
}
