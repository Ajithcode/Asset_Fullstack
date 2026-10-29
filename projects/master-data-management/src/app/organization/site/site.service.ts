import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, switchMap, take, tap } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { apiErrorMessage } from '../../shared/api-error';
import { BfzService } from '../building-floor-zone/bfz.service';
import { BusinessUnitService } from '../business-unit/business-unit.service';
import { DepartmentService } from '../department/department.service';
import { OrganizationService } from '../organization/organization.service';
import { blankSite } from './site.data';
import { SiteFilters, SiteRecord, SiteType } from './site.model';

interface ReferenceDto {
  id: string | null;
  name: string;
}

/** Raw shape returned by GET/POST/PUT {projectApiUrl}api/master-data/sites */
interface SiteDto {
  id: string;
  siteName: string;
  code: string;
  description: string | null;
  organization: ReferenceDto | null;
  parentSite: ReferenceDto | null;
  latitude: number | null;
  longitude: number | null;
  businessUnit: ReferenceDto | null;
  department: ReferenceDto | null;
  siteType: string | null;
  city: string | null;
  country: string | null;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  clientId: string | null;
  tenantId: string | null;
  status: string | null;
  isDeleted: boolean;
}

/** Fields the API stores; the rest of the address, contacts, hours, settings and images are UI-only for now. */
interface SiteRequest {
  siteName: string;
  code: string;
  description: string;
  organization: ReferenceDto | null;
  parentSite: ReferenceDto | null;
  latitude: number | null;
  longitude: number | null;
  businessUnit: ReferenceDto | null;
  department: ReferenceDto | null;
  siteType: string;
  city: string;
  country: string;
  status: string;
}

/**
 * Sites from the Master Data API. Rows are cached after load() so this page and the Business Unit /
 * Department pages can keep reading them synchronously; writes go to the API and update the cache
 * when they succeed.
 */
@Injectable({ providedIn: 'root' })
export class SiteService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly bfz = inject(BfzService);
  private readonly organizations = inject(OrganizationService);
  private readonly businessUnits = inject(BusinessUnitService);
  private readonly departments = inject(DepartmentService);
  private readonly apiUrl = `${environment.projectApiUrl}api/master-data/sites`;

  private records: SiteRecord[] = [];
  loading = false;
  loadError = '';

  constructor() {
    this.load();
  }

  /** GET /api/master-data/sites (skipped while a load is already running) */
  load(): void {
    if (this.loading) return;
    this.loading = true;
    this.loadError = '';
    // Wait for the organizations so each site's organization id can be mapped to its code
    this.organizations.settled$.pipe(
      take(1),
      switchMap(() => this.http.get<SiteDto[]>(this.apiUrl, { params: { clientId: environment.clientId } }))
    ).subscribe({
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

  all(): SiteRecord[] {
    return this.records;
  }

  find(id: string | null): SiteRecord | undefined {
    return id ? this.records.find((r) => r.id === id) : undefined;
  }

  /** `id` and every site under it, so none can become its own parent. */
  descendantIds(id: string): Set<string> {
    const found = new Set([id]);
    for (let added = true; added; ) {
      added = false;
      for (const r of this.records) {
        if (r.parentId && found.has(r.parentId) && !found.has(r.id)) {
          found.add(r.id);
          added = true;
        }
      }
    }
    return found;
  }

  /** Sites refer to business units by name. */
  ofBusinessUnit(name: string): SiteRecord[] {
    return this.records.filter((r) => r.businessUnit === name);
  }

  // The API links by id and renames for us; these keep the cached rows in step until the next load
  renameBusinessUnit(oldName: string, newName: string): void {
    if (oldName === newName) return;
    this.records = this.records.map((r) => (r.businessUnit === oldName ? { ...r, businessUnit: newName } : r));
  }

  /** Sites refer to departments by name. */
  renameDepartment(oldName: string, newName: string): void {
    if (oldName === newName) return;
    this.records = this.records.map((r) => (r.department === oldName ? { ...r, department: newName } : r));
  }

  childSites(id: string): SiteRecord[] {
    return this.records.filter((r) => r.parentId === id);
  }

  filter(f: SiteFilters): SiteRecord[] {
    const term = f.search.trim().toLowerCase();
    return this.records.filter(
      (r) =>
        (!f.status || r.status === f.status) &&
        (!f.siteType || r.siteType === f.siteType) &&
        (!f.businessUnit || r.businessUnit === f.businessUnit) &&
        (!term ||
          [r.name, r.code, r.address.line1, r.address.line2, r.address.city, r.address.country].some((v) => v.toLowerCase().includes(term)))
    );
  }

  stats() {
    const active = this.records.filter((r) => r.status === 'Active').length;
    return {
      total: this.records.length,
      active,
      inactive: this.records.length - active,
      buildings: this.bfz.buildings().length,
      plants: this.records.filter((r) => r.siteType === 'Plant').length,
      warehouses: this.records.filter((r) => r.siteType === 'Warehouse').length
    };
  }

  buildingsOf(id: string) {
    return this.bfz.buildings(id);
  }

  organizationName(code: string): string {
    return this.organizations.find(code)?.name ?? code;
  }

  isCodeTaken(code: string, exceptId: string | null): boolean {
    const wanted = code.trim().toLowerCase();
    return this.records.some((r) => r.code.toLowerCase() === wanted && r.id !== exceptId);
  }

  /** POST for a new record (empty id), PUT /{id} for an existing one. */
  save(record: SiteRecord): Observable<SiteRecord> {
    const user = this.auth.getUserEmail() || 'unknown';
    const body = this.toRequest(record);
    const request$ = record.id
      ? this.http.put<SiteDto>(`${this.apiUrl}/${record.id}`, { ...body, updatedBy: user })
      : this.http.post<SiteDto>(this.apiUrl, {
          ...body,
          createdBy: user,
          clientId: environment.clientId,
          tenantId: environment.tenantId
        });

    return request$.pipe(
      // Keep the fields the API doesn't store yet
      map((dto) => {
        const saved = this.toRecord(dto);
        return { ...record, ...saved, address: { ...record.address, city: saved.address.city, country: saved.address.country } };
      }),
      tap((saved) => {
        const rows = [...this.records];
        const index = rows.findIndex((r) => r.id === saved.id);
        if (index >= 0) {
          rows[index] = saved;
        } else {
          rows.push(saved);
        }
        this.records = rows;
      })
    );
  }

  /** DELETE /api/master-data/sites/{id}; the API makes its sub-sites top-level. */
  remove(record: SiteRecord): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${record.id}`).pipe(
      tap(() => {
        this.records = this.records
          .filter((r) => r.id !== record.id)
          .map((r) => (r.parentId === record.id ? { ...r, parentId: null } : r));
      })
    );
  }

  private toRecord(dto: SiteDto): SiteRecord {
    const orgId = dto.organization?.id;
    const organization = orgId ? this.organizations.all().find((o) => o.id === orgId) : undefined;
    // DEMO MODE: the fields the API doesn't store yet keep what this session has
    const cached = this.records.find((r) => r.id === dto.id) ?? blankSite();
    return {
      ...cached,
      id: dto.id,
      code: dto.code,
      name: dto.siteName,
      siteType: (dto.siteType ?? '') as SiteType,
      organizationCode: organization?.code ?? '',
      // Business units and departments are shown and picked by name
      businessUnit: dto.businessUnit?.name ?? '',
      department: dto.department?.name ?? '',
      parentId: dto.parentSite?.id ?? null,
      description: dto.description ?? '',
      status: dto.status === 'Inactive' ? 'Inactive' : 'Active',
      address: { ...cached.address, city: dto.city ?? '', country: dto.country ?? '' },
      lat: dto.latitude,
      lng: dto.longitude,
      createdOn: dto.createdAt?.slice(0, 16) ?? '',
      updatedOn: (dto.updatedAt ?? dto.createdAt)?.slice(0, 16) ?? ''
    };
  }

  private toRequest(r: SiteRecord): SiteRequest {
    const organization = this.organizations.find(r.organizationCode);
    const unit = r.businessUnit ? this.businessUnits.findByName(r.businessUnit) : undefined;
    // Department names can repeat across business units, so prefer the one in this site's unit
    const sameName = r.department ? this.departments.all().filter((d) => d.name === r.department) : [];
    const department = sameName.find((d) => d.businessUnitCode === unit?.code) ?? sameName[0];
    const parent = this.find(r.parentId);
    return {
      siteName: r.name,
      code: r.code,
      description: r.description,
      organization: organization ? { id: organization.id, name: organization.name } : null,
      parentSite: parent ? { id: parent.id, name: parent.name } : null,
      latitude: r.lat,
      longitude: r.lng,
      businessUnit: unit ? { id: unit.id, name: unit.name } : null,
      department: department ? { id: department.id, name: department.name } : null,
      siteType: r.siteType,
      city: r.address.city,
      country: r.address.country,
      status: r.status
    };
  }
}
