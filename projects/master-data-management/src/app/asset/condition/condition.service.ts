import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, tap } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { apiErrorMessage } from '../../shared/api-error';
import { blankCondition } from './condition.data';
import { ConditionFilters, ConditionRecord, ConditionType } from './condition.model';

/** Raw shape returned by GET/POST/PUT {projectApiUrl}api/master-data/conditions */
interface ConditionDto {
  id: string;
  conditionName: string;
  code: string;
  type: string | null;
  color: string | null;
  defaultCondition: boolean;
  description: string | null;
  healthScore: number | null;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  status: string | null;
}

/**
 * Condition values from the Master Data API. Rows are cached after load() so the page can keep
 * reading them synchronously; writes go to the API and update the cache when they succeed.
 * Transition rules, characteristics and settings are UI-only until the backend adds them.
 */
@Injectable({ providedIn: 'root' })
export class ConditionService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly apiUrl = `${environment.projectApiUrl}api/master-data/conditions`;

  private records: ConditionRecord[] = [];
  loading = false;
  loadError = '';

  constructor() {
    this.load();
  }

  /** GET /api/master-data/conditions (skipped while a load is already running) */
  load(): void {
    if (this.loading) return;
    this.loading = true;
    this.loadError = '';
    this.http.get<ConditionDto[]>(this.apiUrl, { params: { clientId: environment.clientId } }).subscribe({
      next: (list) => {
        this.records = list.map((d) => this.toRecord(d));
        this.loading = false;
      },
      error: (err) => {
        this.loadError = apiErrorMessage(err);
        this.loading = false;
      }
    });
  }

  all(): ConditionRecord[] {
    return this.records;
  }

  find(code: string | null): ConditionRecord | undefined {
    return code ? this.records.find((r) => r.code === code) : undefined;
  }

  nameOf(code: string): string {
    return this.find(code)?.name ?? code;
  }

  filter(f: ConditionFilters): ConditionRecord[] {
    const term = f.search.trim().toLowerCase();
    return this.records.filter(
      (r) =>
        (!f.status || r.status === f.status) &&
        (!f.type || r.type === f.type) &&
        (!term || [r.name, r.code, r.description].some((v) => v.toLowerCase().includes(term)))
    );
  }

  isCodeTaken(code: string, exceptCode: string | null): boolean {
    const wanted = code.trim().toLowerCase();
    return this.records.some((r) => r.code.toLowerCase() === wanted && r.code !== exceptCode);
  }

  isNameTaken(name: string, exceptCode: string | null): boolean {
    const wanted = name.trim().toLowerCase();
    return this.records.some((r) => r.name.toLowerCase() === wanted && r.code !== exceptCode);
  }

  /**
   * POST for a new record, PUT /{id} for an existing one. Only one condition can be the default (the
   * API clears the others), a changed code is carried into other conditions' transition rules, and
   * "can move to / from" stay mirrored on both sides.
   */
  save(record: ConditionRecord, originalCode: string | null): Observable<ConditionRecord> {
    const user = this.auth.getUserEmail() || 'unknown';
    const body = {
      conditionName: record.name,
      code: record.code,
      type: record.type,
      color: record.color,
      defaultCondition: record.isDefault,
      description: record.description,
      healthScore: record.healthScore,
      status: record.status
    };
    const request$ = record.id
      ? this.http.put<ConditionDto>(`${this.apiUrl}/${record.id}`, { ...body, updatedBy: user })
      : this.http.post<ConditionDto>(this.apiUrl, { ...body, createdBy: user, clientId: environment.clientId, tenantId: environment.tenantId });

    return request$.pipe(
      // Keep the fields the API doesn't store yet
      map((dto) => ({ ...record, ...this.toRecord(dto), icon: record.icon })),
      tap((saved) => this.store(saved, originalCode))
    );
  }

  /** DELETE /api/master-data/conditions/{id} */
  remove(record: ConditionRecord): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${record.id}`).pipe(
      tap(() => {
        this.records = this.records
          .filter((r) => r.id !== record.id)
          .map((r) => ({ ...r, canMoveFrom: r.canMoveFrom.filter((c) => c !== record.code), canMoveTo: r.canMoveTo.filter((c) => c !== record.code) }));
      })
    );
  }

  private store(record: ConditionRecord, originalCode: string | null): void {
    const oldCode = originalCode ?? record.code;
    let list = this.records.map((r) => {
      const rename = (codes: string[]) => codes.map((c) => (c === oldCode ? record.code : c));
      return { ...r, isDefault: record.isDefault ? false : r.isDefault, canMoveFrom: rename(r.canMoveFrom), canMoveTo: rename(r.canMoveTo) };
    });
    const index = list.findIndex((r) => r.id === record.id);
    if (index >= 0) list[index] = record;
    else list = [...list, record];

    // Mirror this record's rules onto the other conditions
    this.records = list.map((r) => {
      if (r.code === record.code) return r;
      const from = r.canMoveFrom.filter((c) => c !== record.code);
      const to = r.canMoveTo.filter((c) => c !== record.code);
      if (record.canMoveTo.includes(r.code)) from.push(record.code);
      if (record.canMoveFrom.includes(r.code)) to.push(record.code);
      return { ...r, canMoveFrom: from, canMoveTo: to };
    });
  }

  private toRecord(dto: ConditionDto): ConditionRecord {
    // DEMO MODE: the fields the API doesn't store yet keep what this session has
    const cached = this.records.find((r) => r.id === dto.id) ?? blankCondition();
    return {
      ...cached,
      id: dto.id,
      code: dto.code,
      name: dto.conditionName,
      description: dto.description ?? '',
      type: (dto.type ?? cached.type) as ConditionType,
      color: dto.color ?? cached.color,
      healthScore: dto.healthScore ?? cached.healthScore,
      isDefault: dto.defaultCondition,
      status: dto.status === 'Inactive' ? 'Inactive' : 'Active',
      createdOn: dto.createdAt?.slice(0, 16) ?? '',
      createdBy: dto.createdBy ?? '',
      updatedOn: (dto.updatedAt ?? dto.createdAt)?.slice(0, 16) ?? ''
    };
  }
}
