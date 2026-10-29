import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, ReplaySubject, map, tap } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { apiErrorMessage } from '../../shared/api-error';
import { AssetModelIcon } from '../../asset/asset-model/asset-model.icons';
import { blankSkill } from './skill.data';
import { SkillRecord, SkillType } from './skill.model';

/** Raw shape returned by GET/POST/PUT {projectApiUrl}api/master-data/skills */
interface SkillDto {
  id: string;
  skillName: string;
  code: string;
  category: string | null;
  type: string | null;
  description: string | null;
  icon: string | null;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  status: string | null;
}

/**
 * Skills from the Master Data API. Rows are cached after load() so the People pages can keep
 * reading them synchronously; writes go to the API and update the cache when they succeed.
 * Employees and technicians refer to skills by name; the API renames them too. Levels, related
 * roles and certification settings are UI-only for now.
 */
@Injectable({ providedIn: 'root' })
export class SkillService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly apiUrl = `${environment.projectApiUrl}api/master-data/skills`;

  private records: SkillRecord[] = [];
  loading = false;
  loadError = '';
  private readonly settled = new ReplaySubject<void>(1);
  /** Emits once the first load has finished (or failed), so pages that link by id can map them. */
  readonly settled$ = this.settled.asObservable();

  constructor() {
    this.load();
  }

  /** GET /api/master-data/skills (skipped while a load is already running) */
  load(): void {
    if (this.loading) return;
    this.loading = true;
    this.loadError = '';
    this.http.get<SkillDto[]>(this.apiUrl, { params: { clientId: environment.clientId } }).subscribe({
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

  all(): SkillRecord[] {
    return this.records;
  }

  find(code: string | null): SkillRecord | undefined {
    return code ? this.records.find((r) => r.code === code) : undefined;
  }

  /** Active skill names A-Z, plus `keep` so existing choices stay selectable. */
  activeNames(keep: string[] = []): string[] {
    return this.records
      .filter((r) => r.status === 'Active' || keep.includes(r.name))
      .map((r) => r.name)
      .sort((a, b) => a.localeCompare(b));
  }

  isCodeTaken(code: string, exceptCode: string | null): boolean {
    const wanted = code.trim().toLowerCase();
    return this.records.some((r) => r.code.toLowerCase() === wanted && r.code !== exceptCode);
  }

  isNameTaken(name: string, exceptCode: string | null): boolean {
    const wanted = name.trim().toLowerCase();
    return this.records.some((r) => r.name.toLowerCase() === wanted && r.code !== exceptCode);
  }

  /** POST for a new record, PUT /{id} for an existing one. */
  save(record: SkillRecord): Observable<SkillRecord> {
    const user = this.auth.getUserEmail() || 'unknown';
    const body = {
      skillName: record.name,
      code: record.code,
      category: record.category,
      type: record.type,
      description: record.description,
      icon: record.icon,
      status: record.status
    };
    const request$ = record.id
      ? this.http.put<SkillDto>(`${this.apiUrl}/${record.id}`, { ...body, updatedBy: user })
      : this.http.post<SkillDto>(this.apiUrl, { ...body, createdBy: user, clientId: environment.clientId, tenantId: environment.tenantId });

    return request$.pipe(
      // Keep the fields the API doesn't store yet
      map((dto) => ({ ...record, ...this.toRecord(dto) })),
      tap((saved) => {
        const index = this.records.findIndex((r) => r.id === saved.id);
        this.records = index >= 0 ? this.records.map((r, i) => (i === index ? saved : r)) : [saved, ...this.records];
      })
    );
  }

  /** DELETE /api/master-data/skills/{id} */
  remove(record: SkillRecord): Observable<void> {
    return this.http
      .delete<void>(`${this.apiUrl}/${record.id}`)
      .pipe(tap(() => (this.records = this.records.filter((r) => r.id !== record.id))));
  }

  private toRecord(dto: SkillDto): SkillRecord {
    // DEMO MODE: the fields the API doesn't store yet keep what this session has
    const cached = this.records.find((r) => r.id === dto.id) ?? blankSkill();
    return {
      ...cached,
      id: dto.id,
      code: dto.code,
      name: dto.skillName,
      category: dto.category ?? '',
      type: (dto.type ?? cached.type) as SkillType,
      description: dto.description ?? '',
      icon: (dto.icon ?? cached.icon) as AssetModelIcon,
      status: dto.status === 'Inactive' ? 'Inactive' : 'Active',
      createdOn: dto.createdAt?.slice(0, 16) ?? '',
      updatedOn: (dto.updatedAt ?? dto.createdAt)?.slice(0, 16) ?? ''
    };
  }
}
