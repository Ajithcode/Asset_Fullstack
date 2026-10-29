import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, ReplaySubject, map, tap } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { apiErrorMessage } from '../../shared/api-error';
import { blankOrganization } from './organization.data';
import { OrganizationFilters, OrganizationRecord, OrganizationType } from './organization.model';

interface ReferenceDto {
  id: string | null;
  name: string;
}

/** Raw shape returned by GET/POST/PUT {projectApiUrl}api/master-data/organizations */
interface OrganizationDto {
  id: string;
  organizationName: string;
  code: string;
  description: string | null;
  registrationNumber: string | null;
  website: string | null;
  logoUrl: string | null;
  type: string | null;
  parentOrganization: ReferenceDto | null;
  businessUnits: ReferenceDto[];
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

/** Fields the API stores; address, contact and settings are UI-only until the backend adds them. */
interface OrganizationRequest {
  organizationName: string;
  code: string;
  description: string;
  registrationNumber: string;
  website: string;
  logoUrl: string | null;
  type: string;
  parentOrganization: ReferenceDto | null;
  businessUnits: ReferenceDto[];
  sites: ReferenceDto[];
  status: string;
}

/**
 * Organizations from the Master Data API. Rows are cached after load() so this page and the
 * Business Unit / Site pages can keep reading them synchronously; writes go to the API and update
 * the cache when they succeed.
 */
@Injectable({ providedIn: 'root' })
export class OrganizationService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly apiUrl = `${environment.projectApiUrl}api/master-data/organizations`;

  private records: OrganizationRecord[] = [];
  // Linked business units and sites by organization id; the API clears them if a PUT leaves them out
  private links = new Map<string, Pick<OrganizationDto, 'businessUnits' | 'sites'>>();
  loading = false;
  loadError = '';
  private readonly settled = new ReplaySubject<void>(1);
  /** Emits once the first load has finished (or failed), so pages that link by code can map ids. */
  readonly settled$ = this.settled.asObservable();

  constructor() {
    this.load();
  }

  /** GET /api/master-data/organizations (skipped while a load is already running) */
  load(): void {
    if (this.loading) return;
    this.loading = true;
    this.loadError = '';
    this.http.get<OrganizationDto[]>(this.apiUrl, { params: { clientId: environment.clientId } }).subscribe({
      next: (list) => {
        this.links = new Map(list.map((d) => [d.id, { businessUnits: d.businessUnits, sites: d.sites }]));
        const codeById = new Map(list.map((d) => [d.id, d.code]));
        this.records = list.map((d) => this.toRecord(d, codeById));
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

  all(): OrganizationRecord[] {
    return this.records;
  }

  find(code: string | null): OrganizationRecord | undefined {
    return code ? this.records.find((r) => r.code === code) : undefined;
  }

  children(code: string): OrganizationRecord[] {
    return this.records.filter((r) => r.parentCode === code);
  }

  /** Top-level organization first, ending with `code` itself. */
  ancestry(code: string | null): OrganizationRecord[] {
    const chain: OrganizationRecord[] = [];
    for (let r = this.find(code); r && chain.length < 20; r = this.find(r.parentCode)) {
      chain.unshift(r);
    }
    return chain;
  }

  /** `code` and everything below it, so it can't be chosen as its own parent. */
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

  filter(f: OrganizationFilters): OrganizationRecord[] {
    const term = f.search.trim().toLowerCase();
    return this.records.filter(
      (r) =>
        (!f.status || r.status === f.status) &&
        (!f.type || r.type === f.type) &&
        (!term ||
          [r.name, r.code, r.type, this.find(r.parentCode)?.name ?? ''].some((v) => v.toLowerCase().includes(term)))
    );
  }

  stats() {
    const active = this.records.filter((r) => r.status === 'Active').length;
    return {
      total: this.records.length,
      active,
      inactive: this.records.length - active,
      topLevel: this.records.filter((r) => !r.parentCode).length,
      businessUnits: this.records.reduce((sum, r) => sum + r.businessUnits, 0),
      sites: this.records.reduce((sum, r) => sum + r.sites, 0)
    };
  }

  isCodeTaken(code: string, exceptCode: string | null): boolean {
    const wanted = code.trim().toLowerCase();
    return this.records.some((r) => r.code.toLowerCase() === wanted && r.code !== exceptCode);
  }

  /** POST for a new record, PUT /{id} for an existing one. */
  save(record: OrganizationRecord, originalCode: string | null): Observable<OrganizationRecord> {
    const user = this.auth.getUserEmail() || 'unknown';
    const body = this.toRequest(record);
    const request$ = record.id
      ? this.http.put<OrganizationDto>(`${this.apiUrl}/${record.id}`, { ...body, updatedBy: user })
      : this.http.post<OrganizationDto>(this.apiUrl, {
          ...body,
          createdBy: user,
          clientId: environment.clientId,
          tenantId: environment.tenantId
        });

    return request$.pipe(
      tap((dto) => this.links.set(dto.id, { businessUnits: dto.businessUnits, sites: dto.sites })),
      // Keep the address, contact and settings the API doesn't store yet
      map((dto) => ({ ...this.toRecord(dto), address: record.address, contact: record.contact, settings: record.settings })),
      tap((saved) => {
        const rows = [...this.records];
        const index = rows.findIndex((r) => r.id === saved.id);
        if (index >= 0) {
          rows[index] = saved;
        } else {
          rows.push(saved);
        }
        // Children point at the parent by code, so follow a code change
        if (originalCode && originalCode !== saved.code) {
          rows.forEach((r, i) => {
            if (r.parentCode === originalCode) rows[i] = { ...r, parentCode: saved.code };
          });
        }
        this.records = rows;
      })
    );
  }

  /** DELETE /api/master-data/organizations/{id} */
  remove(record: OrganizationRecord): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${record.id}`).pipe(
      tap(() => {
        this.records = this.records.filter((r) => r.id !== record.id);
        this.links.delete(record.id);
      })
    );
  }

  private toRecord(dto: OrganizationDto, codeById?: Map<string, string>): OrganizationRecord {
    const parentId = dto.parentOrganization?.id ?? null;
    const parentCode = parentId ? (codeById?.get(parentId) ?? this.records.find((r) => r.id === parentId)?.code ?? null) : null;
    // DEMO MODE: address, contact and settings aren't stored by the API yet, so keep what this session has
    const cached = this.records.find((r) => r.id === dto.id) ?? blankOrganization();
    return {
      ...cached,
      id: dto.id,
      code: dto.code,
      name: dto.organizationName,
      type: (dto.type ?? '') as OrganizationType,
      parentCode,
      logo: dto.logoUrl,
      registrationNumber: dto.registrationNumber ?? '',
      website: dto.website ?? '',
      description: dto.description ?? '',
      businessUnits: dto.businessUnits.length,
      sites: dto.sites.length,
      status: dto.status === 'Inactive' ? 'Inactive' : 'Active',
      createdOn: dto.createdAt?.slice(0, 16) ?? '',
      updatedOn: (dto.updatedAt ?? dto.createdAt)?.slice(0, 16) ?? ''
    };
  }

  private toRequest(r: OrganizationRecord): OrganizationRequest {
    const parent = this.find(r.parentCode);
    const links = (r.id && this.links.get(r.id)) || { businessUnits: [], sites: [] };
    return {
      organizationName: r.name,
      code: r.code,
      description: r.description,
      registrationNumber: r.registrationNumber,
      website: r.website,
      // TODO: upload through the Media service and send its URL; this sends the picked image as a data URL
      logoUrl: r.logo,
      type: r.type,
      parentOrganization: parent ? { id: parent.id, name: parent.name } : null,
      businessUnits: links.businessUnits,
      sites: links.sites,
      status: r.status
    };
  }
}
