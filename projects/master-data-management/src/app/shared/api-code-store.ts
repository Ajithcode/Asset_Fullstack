import { HttpClient } from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, ReplaySubject, concat, map, of, switchMap, take, tap, toArray } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { apiErrorMessage } from './api-error';
import { CodeStore } from './code-store';

/** Fields every Master Data API response has. */
export interface MasterDto {
  id: string;
  // Most masters call it `code`; the others override codeOf()
  code?: string;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  status: string | null;
}

/** Records kept by an ApiCodeStore: the API's id plus the page's own fields. */
export interface ApiRecord {
  /** Mongo id from the API; empty until the record is first saved. */
  id: string;
  code: string;
  name?: string;
  status: string;
  createdOn: string;
  updatedOn: string;
}

/**
 * A CodeStore backed by a Master Data API endpoint ({projectApiUrl}api/master-data/{endpoint}).
 * Rows are cached after load() so pages can keep reading them synchronously; save() and remove()
 * go to the API and update the cache when they succeed. Subclasses map between the page's record
 * and the API's shape, and call load() from their constructor.
 */
export abstract class ApiCodeStore<T extends ApiRecord, D extends MasterDto> extends CodeStore<T> {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  loading = false;
  loadError = '';
  private readonly settled = new ReplaySubject<void>(1);
  /** Emits once the first load has finished (or failed), so other masters can map ids. */
  readonly settled$ = this.settled.asObservable();

  protected constructor(private readonly endpoint: string) {
    super([]);
  }

  /** Builds the page's record from the API's; `cached` is the record this session already has. */
  protected abstract toRecord(dto: D, cached: T | undefined): T;
  /** The request body for POST / PUT, without status and the audit fields. */
  protected abstract toRequest(record: T): object;

  /** The record's code in the API's shape. */
  protected codeOf(dto: D): string {
    return dto.code ?? '';
  }

  /** Masters this one links to by id; loading waits for them so ids can be mapped to codes. */
  protected waitFor(): Observable<unknown>[] {
    return [];
  }

  private get url(): string {
    return `${environment.projectApiUrl}api/master-data/${this.endpoint}`;
  }

  /** GET the list (skipped while a load is already running). */
  load(): void {
    if (this.loading) return;
    this.loading = true;
    this.loadError = '';
    const ready$ = this.waitFor().length ? concat(...this.waitFor().map((s) => s.pipe(take(1)))).pipe(toArray()) : of([]);
    ready$.pipe(switchMap(() => this.http.get<D[]>(this.url, { params: { clientId: environment.clientId } }))).subscribe({
      next: (list) => {
        this.records = list.map((d) => this.fromDto(d));
        // Second pass so links between rows of this list (e.g. parent categories) resolve
        this.records = list.map((d) => this.fromDto(d));
        this.loading = false;
        this.settled.next();
      },
      error: (err) => {
        this.loadError = apiErrorMessage(err);
        this.loading = false;
        this.settled.next();
      }
    });
  }

  /** POST for a new record, PUT /{id} for an existing one. Fields the API doesn't store are kept. */
  override save(record: T, _originalCode: string | null = null): Observable<T> {
    const user = this.auth.getUserEmail() || 'unknown';
    const body = { ...this.toRequest(record), status: record.status };
    const request$ = record.id
      ? this.http.put<D>(`${this.url}/${record.id}`, { ...body, updatedBy: user })
      : this.http.post<D>(this.url, { ...body, createdBy: user, clientId: environment.clientId, tenantId: environment.tenantId });

    return request$.pipe(
      map((dto) => ({ ...record, ...this.fromDto(dto) })),
      tap((saved) => {
        const index = this.records.findIndex((r) => r.id === saved.id);
        this.records = index >= 0 ? this.records.map((r, i) => (i === index ? saved : r)) : [...this.records, saved];
      })
    );
  }

  /** DELETE /{id} for each code, one after another; stops at the first the API refuses. */
  override remove(codes: string[]): Observable<unknown> {
    const targets = codes.map((c) => this.find(c)).filter((r): r is T => !!r);
    if (!targets.length) return of([]);
    return concat(
      ...targets.map((r) => this.http.delete<void>(`${this.url}/${r.id}`).pipe(tap(() => (this.records = this.records.filter((x) => x.id !== r.id)))))
    ).pipe(toArray());
  }

  private fromDto(dto: D): T {
    const record = this.toRecord(dto, this.records.find((r) => r.id === dto.id));
    return {
      ...record,
      id: dto.id,
      code: this.codeOf(dto),
      status: dto.status === 'Inactive' ? 'Inactive' : 'Active',
      createdOn: dto.createdAt?.slice(0, 16) ?? '',
      updatedOn: (dto.updatedAt ?? dto.createdAt)?.slice(0, 16) ?? ''
    };
  }
}
