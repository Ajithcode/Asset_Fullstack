import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, tap } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { AssetTypeService } from '../asset-type/asset-type.service';
import { CategoryService } from '../category-subcategory/category.service';
import { apiErrorMessage } from '../../shared/api-error';
import { ManufacturerService } from '../manufacturer/manufacturer.service';
import { blankAssetModel } from './asset-model.data';
import { AssetModelFilters, AssetModelRecord } from './asset-model.model';

interface ReferenceDto {
  id: string | null;
  name: string;
}

/** Raw shape returned by GET/POST/PUT {projectApiUrl}api/master-data/asset-models */
interface AssetModelDto {
  id: string;
  modelName: string;
  modelCode: string;
  manufacturer: ReferenceDto | null;
  assetType: ReferenceDto | null;
  category: ReferenceDto | null;
  description: string | null;
  // Free text; this page stores the lifecycle in years
  lifecycle: string | null;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  status: string | null;
}

/**
 * Asset models from the Master Data API. Rows are cached after load() so the asset pages can keep
 * reading them synchronously; writes go to the API and update the cache when they succeed.
 * Manufacturer, type and category are shown by name and linked by id. Specs, maintenance,
 * financial defaults and settings are UI-only for now.
 */
@Injectable({ providedIn: 'root' })
export class AssetModelService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly makers = inject(ManufacturerService);
  private readonly types = inject(AssetTypeService);
  private readonly categories = inject(CategoryService);
  private readonly apiUrl = `${environment.projectApiUrl}api/master-data/asset-models`;

  private records: AssetModelRecord[] = [];
  loading = false;
  loadError = '';

  constructor() {
    this.load();
  }

  /** GET /api/master-data/asset-models (skipped while a load is already running) */
  load(): void {
    if (this.loading) return;
    this.loading = true;
    this.loadError = '';
    this.http.get<AssetModelDto[]>(this.apiUrl, { params: { clientId: environment.clientId } }).subscribe({
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

  all(): AssetModelRecord[] {
    return this.records;
  }

  find(code: string | null): AssetModelRecord | undefined {
    return code ? this.records.find((r) => r.code === code) : undefined;
  }

  ofType(typeName: string): AssetModelRecord[] {
    return this.records.filter((r) => r.assetType === typeName);
  }

  /** Registered assets across every model of a type. */
  assetsOfType(typeName: string): number {
    return this.ofType(typeName).reduce((sum, r) => sum + r.assetCount, 0);
  }

  ofManufacturer(name: string): AssetModelRecord[] {
    return this.records.filter((r) => r.manufacturer === name);
  }

  renameManufacturer(from: string, to: string): void {
    if (from === to) return;
    this.records = this.records.map((r) => (r.manufacturer === from ? { ...r, manufacturer: to } : r));
  }

  renameType(from: string, to: string): void {
    if (from === to) return;
    this.records = this.records.map((r) => (r.assetType === from ? { ...r, assetType: to } : r));
  }

  /** Manufacturers that have at least one model, A-Z. */
  manufacturers(): string[] {
    return [...new Set(this.records.map((r) => r.manufacturer))].sort((a, b) => a.localeCompare(b));
  }

  usedTypes(): string[] {
    return [...new Set(this.records.map((r) => r.assetType))].sort((a, b) => a.localeCompare(b));
  }

  filter(f: AssetModelFilters): AssetModelRecord[] {
    const term = f.search.trim().toLowerCase();
    return this.records.filter(
      (r) =>
        (!f.status || r.status === f.status) &&
        (!f.manufacturer || r.manufacturer === f.manufacturer) &&
        (!f.assetType || r.assetType === f.assetType) &&
        (!term || [r.name, r.code, r.manufacturer, r.assetType, r.category].some((v) => v.toLowerCase().includes(term)))
    );
  }

  isCodeTaken(code: string, exceptCode: string | null): boolean {
    const wanted = code.trim().toLowerCase();
    return this.records.some((r) => r.code.toLowerCase() === wanted && r.code !== exceptCode);
  }

  // Model names only need to be unique per manufacturer
  isNameTaken(name: string, manufacturer: string, exceptCode: string | null): boolean {
    const wanted = name.trim().toLowerCase();
    return this.records.some((r) => r.manufacturer === manufacturer && r.name.toLowerCase() === wanted && r.code !== exceptCode);
  }

  /** POST for a new record, PUT /{id} for an existing one. */
  save(record: AssetModelRecord): Observable<AssetModelRecord> {
    const user = this.auth.getUserEmail() || 'unknown';
    const ref = (r: { id: string; name: string } | undefined) => (r ? { id: r.id, name: r.name } : null);
    const body = {
      modelName: record.name,
      modelCode: record.code,
      manufacturer: ref(this.makers.findByName(record.manufacturer)),
      assetType: ref(this.types.findByName(record.assetType)),
      category: ref(this.categories.findByName(record.category)),
      description: record.description,
      lifecycle: String(record.lifecycleYears),
      status: record.status
    };
    const request$ = record.id
      ? this.http.put<AssetModelDto>(`${this.apiUrl}/${record.id}`, { ...body, updatedBy: user })
      : this.http.post<AssetModelDto>(this.apiUrl, { ...body, createdBy: user, clientId: environment.clientId, tenantId: environment.tenantId });

    return request$.pipe(
      // Keep the fields the API doesn't store yet
      map((dto) => ({ ...record, ...this.toRecord(dto) })),
      tap((saved) => {
        const index = this.records.findIndex((r) => r.id === saved.id);
        this.records = index >= 0 ? this.records.map((r, i) => (i === index ? saved : r)) : [saved, ...this.records];
      })
    );
  }

  /** DELETE /api/master-data/asset-models/{id} */
  remove(record: AssetModelRecord): Observable<void> {
    return this.http
      .delete<void>(`${this.apiUrl}/${record.id}`)
      .pipe(tap(() => (this.records = this.records.filter((r) => r.id !== record.id))));
  }

  private toRecord(dto: AssetModelDto): AssetModelRecord {
    // DEMO MODE: the fields the API doesn't store yet keep what this session has
    const cached = this.records.find((r) => r.id === dto.id) ?? blankAssetModel();
    const years = Number.parseInt(dto.lifecycle ?? '', 10);
    return {
      ...cached,
      id: dto.id,
      code: dto.modelCode,
      name: dto.modelName,
      manufacturer: dto.manufacturer?.name ?? '',
      assetType: dto.assetType?.name ?? '',
      category: dto.category?.name ?? '',
      description: dto.description ?? '',
      lifecycleYears: Number.isNaN(years) ? cached.lifecycleYears : years,
      status: dto.status === 'Inactive' ? 'Inactive' : 'Active',
      createdOn: dto.createdAt?.slice(0, 16) ?? '',
      updatedOn: (dto.updatedAt ?? dto.createdAt)?.slice(0, 16) ?? ''
    };
  }
}
