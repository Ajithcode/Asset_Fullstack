import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, ReplaySubject, map, tap } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { apiErrorMessage } from '../../shared/api-error';
import { WEEK_DAYS, blankShift } from './shift.data';
import { ShiftRecord, ShiftType } from './shift.model';

/** Raw shape returned by GET/POST/PUT {projectApiUrl}api/master-data/shifts */
interface ShiftDto {
  id: string;
  shiftName: string;
  code: string;
  shiftType: string | null;
  // 24-hour "HH:mm"; empty for shifts without fixed hours
  startTime: string | null;
  endTime: string | null;
  duration: number;
  // Full day names, Monday first
  workingDays: string[];
  color: string | null;
  description: string | null;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  status: string | null;
}

/**
 * Shifts from the Master Data API. Rows are cached after load() so the People pages can keep
 * reading them synchronously; writes go to the API and update the cache when they succeed.
 * Schedule details, breaks and settings are UI-only for now.
 */
@Injectable({ providedIn: 'root' })
export class ShiftService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly apiUrl = `${environment.projectApiUrl}api/master-data/shifts`;

  private records: ShiftRecord[] = [];
  loading = false;
  loadError = '';
  private readonly settled = new ReplaySubject<void>(1);
  /** Emits once the first load has finished (or failed), so pages that link by id can map them. */
  readonly settled$ = this.settled.asObservable();

  constructor() {
    this.load();
  }

  /** GET /api/master-data/shifts (skipped while a load is already running) */
  load(): void {
    if (this.loading) return;
    this.loading = true;
    this.loadError = '';
    this.http.get<ShiftDto[]>(this.apiUrl, { params: { clientId: environment.clientId } }).subscribe({
      next: (list) => {
        this.records = list.map((d) => this.toRecord(d));
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

  all(): ShiftRecord[] {
    return this.records;
  }

  find(code: string | null): ShiftRecord | undefined {
    return code ? this.records.find((r) => r.code === code) : undefined;
  }

  isCodeTaken(code: string, exceptCode: string | null): boolean {
    const wanted = code.trim().toLowerCase();
    return this.records.some((r) => r.code.toLowerCase() === wanted && r.code !== exceptCode);
  }

  isNameTaken(name: string, exceptCode: string | null): boolean {
    const wanted = name.trim().toLowerCase();
    return this.records.some((r) => r.name.toLowerCase() === wanted && r.code !== exceptCode);
  }

  /** POST for a new record, PUT /{id} for an existing one. The API accepts "Mon" as well as "Monday". */
  save(record: ShiftRecord): Observable<ShiftRecord> {
    const user = this.auth.getUserEmail() || 'unknown';
    const body = {
      shiftName: record.name,
      code: record.code,
      shiftType: record.shiftType,
      startTime: record.startTime || null,
      endTime: record.endTime || null,
      workingDays: record.workingDays,
      color: record.color,
      description: record.description,
      status: record.status
    };
    const request$ = record.id
      ? this.http.put<ShiftDto>(`${this.apiUrl}/${record.id}`, { ...body, updatedBy: user })
      : this.http.post<ShiftDto>(this.apiUrl, { ...body, createdBy: user, clientId: environment.clientId, tenantId: environment.tenantId });

    return request$.pipe(
      // Keep the fields the API doesn't store yet
      map((dto) => ({ ...record, ...this.toRecord(dto) })),
      tap((saved) => {
        const index = this.records.findIndex((r) => r.id === saved.id);
        this.records = index >= 0 ? this.records.map((r, i) => (i === index ? saved : r)) : [...this.records, saved];
      })
    );
  }

  /** DELETE /api/master-data/shifts/{id}; the API refuses a shift teams still work. */
  remove(record: ShiftRecord): Observable<void> {
    return this.http
      .delete<void>(`${this.apiUrl}/${record.id}`)
      .pipe(tap(() => (this.records = this.records.filter((r) => r.id !== record.id))));
  }

  private toRecord(dto: ShiftDto): ShiftRecord {
    // DEMO MODE: the fields the API doesn't store yet keep what this session has
    const cached = this.records.find((r) => r.id === dto.id) ?? blankShift();
    return {
      ...cached,
      id: dto.id,
      code: dto.code,
      name: dto.shiftName,
      shiftType: (dto.shiftType ?? cached.shiftType) as ShiftType,
      startTime: dto.startTime ?? '',
      endTime: dto.endTime ?? '',
      // "Monday" -> "Mon"
      workingDays: dto.workingDays.map((d) => d.slice(0, 3)).filter((d) => WEEK_DAYS.includes(d)),
      color: dto.color?.toLowerCase() ?? cached.color,
      description: dto.description ?? '',
      status: dto.status === 'Inactive' ? 'Inactive' : 'Active',
      createdOn: dto.createdAt?.slice(0, 16) ?? '',
      updatedOn: (dto.updatedAt ?? dto.createdAt)?.slice(0, 16) ?? ''
    };
  }
}
