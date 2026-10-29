import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, tap } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { apiErrorMessage } from '../../shared/api-error';
import { blankManufacturer } from './manufacturer.data';
import { ManufacturerFilters, ManufacturerRecord } from './manufacturer.model';

/** Raw shape returned by GET/POST/PUT {projectApiUrl}api/master-data/manufacturers */
interface ManufacturerDto {
  id: string;
  manufacturerName: string;
  code: string;
  country: string | null;
  website: string | null;
  description: string | null;
  contactPerson: string | null;
  email: string | null;
  phoneNumber: string | null;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  status: string | null;
}

/**
 * Manufacturers from the Master Data API. Rows are cached after load() so this page and the Asset
 * Model / Item / Supplier pages can keep reading them synchronously; writes go to the API and
 * update the cache when they succeed. Logo, address, support and default settings are UI-only.
 */
@Injectable({ providedIn: 'root' })
export class ManufacturerService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly apiUrl = `${environment.projectApiUrl}api/master-data/manufacturers`;

  private records: ManufacturerRecord[] = [];
  loading = false;
  loadError = '';

  constructor() {
    this.load();
  }

  /** GET /api/master-data/manufacturers (skipped while a load is already running) */
  load(): void {
    if (this.loading) return;
    this.loading = true;
    this.loadError = '';
    this.http.get<ManufacturerDto[]>(this.apiUrl, { params: { clientId: environment.clientId } }).subscribe({
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

  all(): ManufacturerRecord[] {
    return this.records;
  }

  find(code: string | null): ManufacturerRecord | undefined {
    return code ? this.records.find((r) => r.code === code) : undefined;
  }

  // Asset models refer to their manufacturer by name
  findByName(name: string): ManufacturerRecord | undefined {
    return this.records.find((r) => r.name === name);
  }

  /** Active manufacturers A-Z, plus `keep` so an existing choice stays selectable. */
  activeNames(keep = ''): string[] {
    return this.records
      .filter((r) => r.status === 'Active' || r.name === keep)
      .map((r) => r.name)
      .sort((a, b) => a.localeCompare(b));
  }

  // `makesType` answers whether a manufacturer has a model of the chosen asset type
  filter(f: ManufacturerFilters, makesType: (name: string) => boolean): ManufacturerRecord[] {
    const term = f.search.trim().toLowerCase();
    return this.records.filter(
      (r) =>
        (!f.status || r.status === f.status) &&
        (!f.assetType || makesType(r.name)) &&
        (!term || [r.name, r.code, r.country, r.primaryContact, r.email].some((v) => v.toLowerCase().includes(term)))
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
  save(record: ManufacturerRecord): Observable<ManufacturerRecord> {
    const user = this.auth.getUserEmail() || 'unknown';
    const body = {
      manufacturerName: record.name,
      code: record.code,
      country: record.country,
      website: record.website,
      description: record.description,
      contactPerson: record.primaryContact,
      email: record.email,
      phoneNumber: record.phone,
      status: record.status
    };
    const request$ = record.id
      ? this.http.put<ManufacturerDto>(`${this.apiUrl}/${record.id}`, { ...body, updatedBy: user })
      : this.http.post<ManufacturerDto>(this.apiUrl, { ...body, createdBy: user, clientId: environment.clientId, tenantId: environment.tenantId });

    return request$.pipe(
      // Keep the fields the API doesn't store yet
      map((dto) => ({ ...record, ...this.toRecord(dto) })),
      tap((saved) => {
        const index = this.records.findIndex((r) => r.id === saved.id);
        this.records = index >= 0 ? this.records.map((r, i) => (i === index ? saved : r)) : [saved, ...this.records];
      })
    );
  }

  /** DELETE /api/master-data/manufacturers/{id}; the API refuses one that still has asset models. */
  remove(record: ManufacturerRecord): Observable<void> {
    return this.http
      .delete<void>(`${this.apiUrl}/${record.id}`)
      .pipe(tap(() => (this.records = this.records.filter((r) => r.id !== record.id))));
  }

  private toRecord(dto: ManufacturerDto): ManufacturerRecord {
    // DEMO MODE: the fields the API doesn't store yet keep what this session has
    const cached = this.records.find((r) => r.id === dto.id) ?? blankManufacturer();
    return {
      ...cached,
      id: dto.id,
      code: dto.code,
      name: dto.manufacturerName,
      country: dto.country ?? '',
      website: dto.website ?? '',
      description: dto.description ?? '',
      primaryContact: dto.contactPerson ?? '',
      email: dto.email ?? '',
      phone: dto.phoneNumber ?? '',
      status: dto.status === 'Inactive' ? 'Inactive' : 'Active',
      createdOn: dto.createdAt?.slice(0, 16) ?? '',
      createdBy: dto.createdBy ?? '',
      updatedOn: (dto.updatedAt ?? dto.createdAt)?.slice(0, 16) ?? ''
    };
  }
}
