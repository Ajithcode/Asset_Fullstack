import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, tap } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { apiErrorMessage } from '../../shared/api-error';
import { blankCriticality } from './criticality.data';
import { CriticalityFilters, CriticalityRecord, CriticalityStatus, MaintenancePriority, Scale } from './criticality.model';

/** Raw shape returned by GET/POST/PUT {projectApiUrl}api/master-data/criticalities */
interface CriticalityDto {
  id: string;
  criticalityName: string;
  code: string;
  riskLevel: string | null;
  maintenancePriority: string | null;
  color: string | null;
  defaultCriticality: boolean;
  description: string | null;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  clientId: string | null;
  tenantId: string | null;
  status: string | null;
  isDeleted: boolean;
}

/** Fields the API stores; the rest of CriticalityRecord is UI-only until the backend adds them. */
interface CriticalityRequest {
  criticalityName: string;
  code: string;
  riskLevel: string;
  maintenancePriority: string;
  color: string;
  defaultCriticality: boolean;
  description: string;
  status: CriticalityStatus;
}

function toRecord(dto: CriticalityDto): CriticalityRecord {
  const blank = blankCriticality();
  const riskLevel = (dto.riskLevel as Scale | null) ?? blank.riskLevel;
  return {
    ...blank,
    id: dto.id,
    code: dto.code,
    name: dto.criticalityName,
    description: dto.description ?? '',
    color: dto.color ?? blank.color,
    riskLevel,
    likelihood: riskLevel,
    impact: riskLevel,
    maintenancePriority: (dto.maintenancePriority as MaintenancePriority | null) ?? blank.maintenancePriority,
    isDefault: dto.defaultCriticality,
    status: dto.status === 'Inactive' ? 'Inactive' : 'Active',
    createdOn: dto.createdAt?.slice(0, 16) ?? '',
    createdBy: dto.createdBy ?? '',
    updatedOn: (dto.updatedAt ?? dto.createdAt)?.slice(0, 16) ?? ''
  };
}

function toRequest(r: CriticalityRecord): CriticalityRequest {
  return {
    criticalityName: r.name,
    code: r.code,
    riskLevel: r.riskLevel,
    maintenancePriority: r.maintenancePriority,
    color: r.color,
    defaultCriticality: r.isDefault,
    description: r.description,
    status: r.status
  };
}

/**
 * Criticality levels from the Master Data API. Rows are cached after load() so the page can keep
 * reading them synchronously; writes go to the API and update the cache when they succeed.
 */
@Injectable({ providedIn: 'root' })
export class CriticalityService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly apiUrl = `${environment.projectApiUrl}api/master-data/criticalities`;

  private records: CriticalityRecord[] = [];
  loading = false;
  loadError = '';

  /** GET /api/master-data/criticalities */
  load(): void {
    this.loading = true;
    this.loadError = '';
    this.http.get<CriticalityDto[]>(this.apiUrl, { params: { clientId: environment.clientId } }).subscribe({
      next: (list) => {
        this.records = list.map(toRecord);
        this.loading = false;
      },
      error: (err) => {
        this.loadError = apiErrorMessage(err);
        this.loading = false;
      }
    });
  }

  all(): CriticalityRecord[] {
    return this.records;
  }

  find(code: string | null): CriticalityRecord | undefined {
    return code ? this.records.find((r) => r.code === code) : undefined;
  }

  filter(f: CriticalityFilters): CriticalityRecord[] {
    const term = f.search.trim().toLowerCase();
    return this.records.filter(
      (r) => (!f.status || r.status === f.status) && (!term || [r.name, r.code, r.description].some((v) => v.toLowerCase().includes(term)))
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
   * POST for a new record, PUT /{id} for an existing one. The API clears the other defaults when
   * this one is the default, so the cache mirrors that.
   */
  save(record: CriticalityRecord): Observable<CriticalityRecord> {
    const user = this.auth.getUserEmail() || 'unknown';
    const request$ = record.id
      ? this.http.put<CriticalityDto>(`${this.apiUrl}/${record.id}`, { ...toRequest(record), updatedBy: user })
      : this.http.post<CriticalityDto>(this.apiUrl, {
          ...toRequest(record),
          createdBy: user,
          clientId: environment.clientId,
          tenantId: environment.tenantId
        });

    return request$.pipe(
      // Keep the UI-only fields the API doesn't store yet
      map((dto) => ({ ...record, ...toRecord(dto), ...uiOnly(record) })),
      tap((saved) => {
        const others = saved.isDefault ? this.records.map((r) => ({ ...r, isDefault: false })) : [...this.records];
        const index = others.findIndex((r) => r.id === saved.id);
        if (index >= 0) {
          others[index] = saved;
          this.records = others;
        } else {
          this.records = [...others, saved];
        }
      })
    );
  }

  /** DELETE /api/master-data/criticalities/{id} */
  remove(record: CriticalityRecord): Observable<void> {
    return this.http
      .delete<void>(`${this.apiUrl}/${record.id}`)
      .pipe(tap(() => (this.records = this.records.filter((r) => r.id !== record.id))));
  }
}

// DEMO MODE: fields with no API column yet; they survive in memory until the page is reloaded
function uiOnly(r: CriticalityRecord): Partial<CriticalityRecord> {
  const { icon, image, likelihood, impact, businessImpact, recommendedStrategy, inspectionFrequency, conditionMonitoring,
    sparePartsPriority, responseTimeHours, escalateOnFailure, rootCauseRequired, approvalForDisposal, showInReports, assets, documents } = r;
  return { icon, image, likelihood, impact, businessImpact, recommendedStrategy, inspectionFrequency, conditionMonitoring,
    sparePartsPriority, responseTimeHours, escalateOnFailure, rootCauseRequired, approvalForDisposal, showInReports, assets, documents };
}
