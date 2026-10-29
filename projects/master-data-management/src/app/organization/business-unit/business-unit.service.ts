import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, ReplaySubject, map, switchMap, take, tap } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { apiErrorMessage } from '../../shared/api-error';
import { OrganizationService } from '../organization/organization.service';
import { blankBusinessUnit } from './business-unit.data';
import { BusinessUnitFilters, BusinessUnitRecord } from './business-unit.model';

interface ReferenceDto {
  id: string | null;
  name: string;
}

/** Raw shape returned by GET/POST/PUT {projectApiUrl}api/master-data/business-units */
interface BusinessUnitDto {
  id: string;
  businessUnitName: string;
  logoUrl: string | null;
  code: string;
  description: string | null;
  parentOrganization: ReferenceDto | null;
  parentBusinessUnit: ReferenceDto | null;
  departments: ReferenceDto[];
  sites: ReferenceDto[];
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  clientId: string | null;
  tenantId: string | null;
  status: string | null;
  isDeleted: boolean;
}

/** Fields the API stores; head, cost centre, currency, address, contact and settings are UI-only for now. */
interface BusinessUnitRequest {
  businessUnitName: string;
  logoUrl: string | null;
  code: string;
  description: string;
  parentOrganization: ReferenceDto | null;
  parentBusinessUnit: ReferenceDto | null;
  departments: ReferenceDto[];
  sites: ReferenceDto[];
  status: string;
}

/**
 * Business units from the Master Data API. Rows are cached after load() so this page and the
 * Department / Site pages can keep reading them synchronously; writes go to the API and update the
 * cache when they succeed.
 */
@Injectable({ providedIn: 'root' })
export class BusinessUnitService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly organizations = inject(OrganizationService);
  private readonly apiUrl = `${environment.projectApiUrl}api/master-data/business-units`;

  private records: BusinessUnitRecord[] = [];
  // Raw API rows by id: parent organization id and the linked departments and sites, which a PUT
  // clears if it leaves them out
  private dtos = new Map<string, BusinessUnitDto>();
  loading = false;
  loadError = '';
  private readonly settled = new ReplaySubject<void>(1);
  /** Emits once the first load has finished (or failed), so pages that link by code can map ids. */
  readonly settled$ = this.settled.asObservable();

  constructor() {
    this.load();
  }

  /** GET /api/master-data/business-units (skipped while a load is already running) */
  load(): void {
    if (this.loading) return;
    this.loading = true;
    this.loadError = '';
    // Wait for the organizations so each unit's organization id can be mapped to its code
    this.organizations.settled$.pipe(
      take(1),
      switchMap(() => this.http.get<BusinessUnitDto[]>(this.apiUrl, { params: { clientId: environment.clientId } }))
    ).subscribe({
      next: (list) => {
        this.dtos = new Map(list.map((d) => [d.id, d]));
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

  all(): BusinessUnitRecord[] {
    return this.records;
  }

  names(): string[] {
    return this.records.map((r) => r.name);
  }

  find(code: string | null): BusinessUnitRecord | undefined {
    return code ? this.records.find((r) => r.code === code) : undefined;
  }

  findByName(name: string): BusinessUnitRecord | undefined {
    return this.records.find((r) => r.name === name);
  }

  children(code: string): BusinessUnitRecord[] {
    return this.records.filter((r) => r.parentCode === code);
  }

  /** `code` and every unit under it, so none can become its own parent. */
  descendantCodes(code: string): Set<string> {
    const found = new Set([code]);
    for (let added = true; added; ) {
      added = false;
      for (const r of this.records) {
        if (r.parentCode && found.has(r.parentCode) && !found.has(r.code)) {
          found.add(r.code);
          added = true;
        }
      }
    }
    return found;
  }

  filter(f: BusinessUnitFilters): BusinessUnitRecord[] {
    const term = f.search.trim().toLowerCase();
    return this.records.filter(
      (r) =>
        (!f.status || r.status === f.status) &&
        (!f.organizationCode || r.organizationCode === f.organizationCode) &&
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

  /** POST for a new record, PUT /{id} for an existing one. */
  save(record: BusinessUnitRecord, originalCode: string | null): Observable<BusinessUnitRecord> {
    const user = this.auth.getUserEmail() || 'unknown';
    const body = this.toRequest(record);
    const request$ = record.id
      ? this.http.put<BusinessUnitDto>(`${this.apiUrl}/${record.id}`, { ...body, updatedBy: user })
      : this.http.post<BusinessUnitDto>(this.apiUrl, {
          ...body,
          createdBy: user,
          clientId: environment.clientId,
          tenantId: environment.tenantId
        });

    return request$.pipe(
      tap((dto) => this.dtos.set(dto.id, dto)),
      // Keep the fields the API doesn't store yet
      map((dto) => ({ ...record, ...this.toRecord(dto), icon: record.icon })),
      tap((saved) => {
        const rows = [...this.records];
        const index = rows.findIndex((r) => r.id === saved.id);
        if (index >= 0) {
          rows[index] = saved;
        } else {
          rows.push(saved);
        }
        // Sub-units point at their parent by code, so follow a code change
        if (originalCode && originalCode !== saved.code) {
          rows.forEach((r, i) => {
            if (r.parentCode === originalCode) rows[i] = { ...r, parentCode: saved.code };
          });
        }
        this.records = rows;
      })
    );
  }

  /** DELETE /api/master-data/business-units/{id}; the API makes its sub-units top-level. */
  remove(record: BusinessUnitRecord): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${record.id}`).pipe(
      tap(() => {
        this.dtos.delete(record.id);
        this.records = this.records
          .filter((r) => r.id !== record.id)
          .map((r) => (r.parentCode === record.code ? { ...r, parentCode: null } : r));
      })
    );
  }

  private toRecord(dto: BusinessUnitDto): BusinessUnitRecord {
    const orgId = dto.parentOrganization?.id;
    const organization = orgId ? this.organizations.all().find((o) => o.id === orgId) : undefined;
    const parentId = dto.parentBusinessUnit?.id;
    // DEMO MODE: the fields the API doesn't store yet keep what this session has
    const cached = this.records.find((r) => r.id === dto.id) ?? blankBusinessUnit();
    return {
      ...cached,
      id: dto.id,
      code: dto.code,
      name: dto.businessUnitName,
      organizationCode: organization?.code ?? '',
      parentCode: parentId ? (this.dtos.get(parentId)?.code ?? null) : null,
      logo: dto.logoUrl,
      description: dto.description ?? '',
      status: dto.status === 'Inactive' ? 'Inactive' : 'Active',
      createdOn: dto.createdAt?.slice(0, 16) ?? '',
      updatedOn: (dto.updatedAt ?? dto.createdAt)?.slice(0, 16) ?? ''
    };
  }

  private toRequest(r: BusinessUnitRecord): BusinessUnitRequest {
    const organization = this.organizations.find(r.organizationCode);
    const parent = this.find(r.parentCode);
    const existing = r.id ? this.dtos.get(r.id) : undefined;
    return {
      businessUnitName: r.name,
      // TODO: upload through the Media service and send its URL; this sends the picked image as a data URL
      logoUrl: r.logo,
      code: r.code,
      description: r.description,
      parentOrganization: organization ? { id: organization.id, name: organization.name } : null,
      parentBusinessUnit: parent ? { id: parent.id, name: parent.name } : null,
      departments: existing?.departments ?? [],
      sites: existing?.sites ?? [],
      status: r.status
    };
  }
}
