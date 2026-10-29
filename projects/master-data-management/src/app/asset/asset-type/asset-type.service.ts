import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, tap } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { AssetModelIcon } from '../asset-model/asset-model.icons';
import { DepreciationMethod } from '../asset-model/asset-model.model';
import { CategoryService } from '../category-subcategory/category.service';
import { apiErrorMessage } from '../../shared/api-error';
import { blankAssetType } from './asset-type.data';
import { AssetTypeFilters, AssetTypeRecord } from './asset-type.model';

interface ReferenceDto {
  id: string | null;
  name: string;
}

/** Raw shape returned by GET/POST/PUT {projectApiUrl}api/master-data/asset-types */
interface AssetTypeDto {
  id: string;
  assetTypeName: string;
  code: string;
  category: ReferenceDto | null;
  assetClass: string | null;
  description: string | null;
  depreciationMethod: string | null;
  defaultLifetime: number | null;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  status: string | null;
}

/**
 * Asset types from the Master Data API. Rows are cached after load() so this page and the Asset
 * Model page can keep reading them synchronously; writes go to the API and update the cache when
 * they succeed. Tagging, maintenance, most financial defaults and settings are UI-only for now.
 */
@Injectable({ providedIn: 'root' })
export class AssetTypeService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly categoryService = inject(CategoryService);
  private readonly apiUrl = `${environment.projectApiUrl}api/master-data/asset-types`;

  private records: AssetTypeRecord[] = [];
  loading = false;
  loadError = '';

  constructor() {
    this.load();
  }

  /** GET /api/master-data/asset-types (skipped while a load is already running) */
  load(): void {
    if (this.loading) return;
    this.loading = true;
    this.loadError = '';
    this.http.get<AssetTypeDto[]>(this.apiUrl, { params: { clientId: environment.clientId } }).subscribe({
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

  all(): AssetTypeRecord[] {
    return this.records;
  }

  active(): AssetTypeRecord[] {
    return this.records.filter((r) => r.status === 'Active');
  }

  find(code: string | null): AssetTypeRecord | undefined {
    return code ? this.records.find((r) => r.code === code) : undefined;
  }

  // Asset models refer to their type by name
  findByName(name: string): AssetTypeRecord | undefined {
    return this.records.find((r) => r.name === name);
  }

  iconFor(name: string): AssetModelIcon {
    return this.findByName(name)?.icon ?? 'conveyor';
  }

  /** Asset categories from the Category master, plus any already on a record. */
  categories(): string[] {
    return [...new Set([...this.categoryService.all().map((c) => c.name), ...this.records.map((r) => r.category).filter(Boolean)])];
  }

  classesInUse(): string[] {
    return [...new Set(this.records.map((r) => r.assetClass).filter(Boolean))];
  }

  filter(f: AssetTypeFilters): AssetTypeRecord[] {
    const term = f.search.trim().toLowerCase();
    return this.records.filter(
      (r) =>
        (!f.status || r.status === f.status) &&
        (!f.category || r.category === f.category) &&
        (!term || [r.name, r.code, r.category, r.assetClass, r.description].some((v) => v.toLowerCase().includes(term)))
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

  /** POST for a new record, PUT /{id} for an existing one. The category is linked by id. */
  save(record: AssetTypeRecord): Observable<AssetTypeRecord> {
    const user = this.auth.getUserEmail() || 'unknown';
    const category = this.categoryService.findByName(record.category);
    const body = {
      assetTypeName: record.name,
      code: record.code,
      category: category ? { id: category.id, name: category.name } : null,
      assetClass: record.assetClass,
      description: record.description,
      depreciationMethod: record.depreciationMethod,
      defaultLifetime: record.lifecycleYears,
      status: record.status
    };
    const request$ = record.id
      ? this.http.put<AssetTypeDto>(`${this.apiUrl}/${record.id}`, { ...body, updatedBy: user })
      : this.http.post<AssetTypeDto>(this.apiUrl, { ...body, createdBy: user, clientId: environment.clientId, tenantId: environment.tenantId });

    return request$.pipe(
      // Keep the fields the API doesn't store yet
      map((dto) => ({ ...record, ...this.toRecord(dto) })),
      tap((saved) => {
        const index = this.records.findIndex((r) => r.id === saved.id);
        this.records = index >= 0 ? this.records.map((r, i) => (i === index ? saved : r)) : [saved, ...this.records];
      })
    );
  }

  /** DELETE /api/master-data/asset-types/{id}; the API refuses one still used by models or AMC vendors. */
  remove(record: AssetTypeRecord): Observable<void> {
    return this.http
      .delete<void>(`${this.apiUrl}/${record.id}`)
      .pipe(tap(() => (this.records = this.records.filter((r) => r.id !== record.id))));
  }

  /** Keeps the cached rows in step when a category is renamed (the API renames them too). */
  renameCategory(oldName: string, newName: string): void {
    if (oldName === newName) return;
    this.records = this.records.map((r) => (r.category === oldName ? { ...r, category: newName } : r));
  }

  private toRecord(dto: AssetTypeDto): AssetTypeRecord {
    // DEMO MODE: the fields the API doesn't store yet keep what this session has
    const cached = this.records.find((r) => r.id === dto.id) ?? blankAssetType();
    return {
      ...cached,
      id: dto.id,
      code: dto.code,
      name: dto.assetTypeName,
      category: dto.category?.name ?? '',
      assetClass: dto.assetClass ?? '',
      description: dto.description ?? '',
      depreciationMethod: (dto.depreciationMethod ?? cached.depreciationMethod) as DepreciationMethod,
      lifecycleYears: dto.defaultLifetime ?? cached.lifecycleYears,
      status: dto.status === 'Inactive' ? 'Inactive' : 'Active',
      createdOn: dto.createdAt?.slice(0, 16) ?? '',
      createdBy: dto.createdBy ?? '',
      updatedOn: (dto.updatedAt ?? dto.createdAt)?.slice(0, 16) ?? ''
    };
  }
}
