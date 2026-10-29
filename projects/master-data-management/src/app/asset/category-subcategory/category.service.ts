import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, tap } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { AssetModelIcon } from '../asset-model/asset-model.icons';
import { apiErrorMessage } from '../../shared/api-error';
import { blankCategory } from './category.data';
import { CategoryFilters, CategoryRecord, SubcategoryRecord } from './category.model';

interface ReferenceDto {
  id: string | null;
  name: string;
}

interface SubcategoryDto {
  code: string;
  name: string;
  icon: string | null;
  color: string | null;
  status: string | null;
}

/** Raw shape returned by GET/POST/PUT {projectApiUrl}api/master-data/asset-categories */
interface CategoryDto {
  id: string;
  categoryName: string;
  code: string;
  assetType: string | null;
  description: string | null;
  parentCategory: ReferenceDto | null;
  subcategories: SubcategoryDto[];
  assets: ReferenceDto[];
  assetCount: number;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  status: string | null;
}

/**
 * Asset categories (with their subcategories) from the Master Data API. Rows are cached after
 * load() so the asset pages can keep reading them synchronously; writes go to the API and update
 * the cache when they succeed. Icons, defaults, custom fields and settings are UI-only for now.
 */
@Injectable({ providedIn: 'root' })
export class CategoryService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly apiUrl = `${environment.projectApiUrl}api/master-data/asset-categories`;

  private records: CategoryRecord[] = [];
  // Linked assets by category id, which a PUT clears if it leaves them out
  private assetLinks = new Map<string, ReferenceDto[]>();
  loading = false;
  loadError = '';

  constructor() {
    this.load();
  }

  /** GET /api/master-data/asset-categories (skipped while a load is already running) */
  load(): void {
    if (this.loading) return;
    this.loading = true;
    this.loadError = '';
    this.http.get<CategoryDto[]>(this.apiUrl, { params: { clientId: environment.clientId } }).subscribe({
      next: (list) => {
        this.assetLinks = new Map(list.map((d) => [d.id, d.assets]));
        const codeById = new Map(list.map((d) => [d.id, d.code]));
        this.records = list.map((d) => this.toRecord(d, codeById));
        this.loading = false;
      },
      error: (err) => {
        this.loadError = apiErrorMessage(err);
        this.loading = false;
      }
    });
  }

  all(): CategoryRecord[] {
    return this.records;
  }

  find(code: string | null): CategoryRecord | undefined {
    return code ? this.records.find((r) => r.code === code) : undefined;
  }

  // Asset types and models refer to their category by name
  findByName(name: string): CategoryRecord | undefined {
    return name ? this.records.find((r) => r.name === name) : undefined;
  }

  /** A category's assets are the sum of its subcategories. */
  assetsOf(category: CategoryRecord): number {
    return category.subcategories.reduce((sum, s) => sum + s.assets, 0);
  }

  children(code: string): CategoryRecord[] {
    return this.records.filter((r) => r.parentCode === code);
  }

  /** Parents from the top down, not including `code` itself. */
  ancestry(code: string | null): CategoryRecord[] {
    const chain: CategoryRecord[] = [];
    for (let c = this.find(code); c && chain.length < 20; c = this.find(c.parentCode)) chain.unshift(c);
    return chain;
  }

  /** `code` and every category under it, so none can become its own parent. */
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

  assetTypesInUse(): string[] {
    return [...new Set(this.records.map((r) => r.assetType))];
  }

  filter(f: CategoryFilters): CategoryRecord[] {
    const term = f.search.trim().toLowerCase();
    return this.records.filter(
      (r) =>
        (!f.status || r.status === f.status) &&
        (!f.assetType || r.assetType === f.assetType) &&
        (!term || [r.name, r.code, r.description, ...r.subcategories.map((s) => s.name)].some((v) => v.toLowerCase().includes(term)))
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

  // Subcategory codes are unique across all categories
  isSubCodeTaken(code: string, exceptCode: string | null): boolean {
    const wanted = code.trim().toLowerCase();
    return this.records.some((r) => r.subcategories.some((s) => s.code.toLowerCase() === wanted && s.code !== exceptCode));
  }

  allSubCodes(): Set<string> {
    return new Set(this.records.flatMap((r) => r.subcategories.map((s) => s.code)));
  }

  /** POST for a new record, PUT /{id} for an existing one. Subcategories are saved with it. */
  save(record: CategoryRecord, originalCode: string | null): Observable<CategoryRecord> {
    const user = this.auth.getUserEmail() || 'unknown';
    const parent = this.find(record.parentCode);
    const body = {
      categoryName: record.name,
      code: record.code,
      assetType: record.assetType,
      description: record.description,
      parentCategory: parent ? { id: parent.id, name: parent.name } : null,
      subcategories: record.subcategories.map(({ code, name, icon, color, status }) => ({ code, name, icon, color, status })),
      assets: (record.id && this.assetLinks.get(record.id)) || [],
      status: record.status
    };
    const request$ = record.id
      ? this.http.put<CategoryDto>(`${this.apiUrl}/${record.id}`, { ...body, updatedBy: user })
      : this.http.post<CategoryDto>(this.apiUrl, { ...body, createdBy: user, clientId: environment.clientId, tenantId: environment.tenantId });

    return request$.pipe(
      tap((dto) => this.assetLinks.set(dto.id, dto.assets)),
      // Keep the fields the API doesn't store yet
      map((dto) => {
        const saved = this.toRecord(dto);
        return { ...record, ...saved, image: record.image, subcategories: saved.subcategories.map((sub) => ({ ...sub, assets: record.subcategories.find((x) => x.code === sub.code)?.assets ?? 0 })) };
      }),
      tap((saved) => {
        const index = this.records.findIndex((r) => r.id === saved.id);
        const list = index >= 0 ? this.records.map((r, i) => (i === index ? saved : r)) : [saved, ...this.records];
        // Child categories follow a code change
        this.records = originalCode && originalCode !== saved.code ? list.map((r) => (r.parentCode === originalCode ? { ...r, parentCode: saved.code } : r)) : list;
      })
    );
  }

  /** DELETE /api/master-data/asset-categories/{id}; the API refuses one with sub-categories, types or models. */
  remove(record: CategoryRecord): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${record.id}`).pipe(
      tap(() => {
        this.records = this.records.filter((r) => r.id !== record.id);
        this.assetLinks.delete(record.id);
      })
    );
  }

  /** Adds or updates one subcategory by saving its category. */
  saveSubcategory(categoryCode: string, sub: SubcategoryRecord, originalCode: string | null): Observable<CategoryRecord> {
    const category = this.find(categoryCode)!;
    const index = originalCode ? category.subcategories.findIndex((s) => s.code === originalCode) : -1;
    const subcategories = index >= 0 ? category.subcategories.map((s, i) => (i === index ? sub : s)) : [...category.subcategories, sub];
    return this.save({ ...category, subcategories }, category.code);
  }

  removeSubcategories(categoryCode: string, codes: string[]): Observable<CategoryRecord> {
    const category = this.find(categoryCode)!;
    return this.save({ ...category, subcategories: category.subcategories.filter((s) => !codes.includes(s.code)) }, category.code);
  }

  private toRecord(dto: CategoryDto, codeById?: Map<string, string>): CategoryRecord {
    const parentId = dto.parentCategory?.id;
    // DEMO MODE: the fields the API doesn't store yet keep what this session has
    const cached = this.records.find((r) => r.id === dto.id) ?? blankCategory();
    return {
      ...cached,
      id: dto.id,
      code: dto.code,
      name: dto.categoryName,
      assetType: dto.assetType ?? '',
      description: dto.description ?? '',
      parentCode: parentId ? (codeById?.get(parentId) ?? this.records.find((r) => r.id === parentId)?.code ?? null) : null,
      subcategories: dto.subcategories.map((sub) => ({
        code: sub.code,
        name: sub.name,
        icon: (sub.icon ?? cached.icon) as AssetModelIcon,
        color: sub.color ?? cached.color,
        status: sub.status === 'Inactive' ? 'Inactive' : 'Active',
        // DEMO MODE: registered assets per subcategory are owned by the Assets module
        assets: cached.subcategories.find((x) => x.code === sub.code)?.assets ?? 0
      })),
      status: dto.status === 'Inactive' ? 'Inactive' : 'Active',
      createdOn: dto.createdAt?.slice(0, 16) ?? '',
      createdBy: dto.createdBy ?? '',
      updatedOn: (dto.updatedAt ?? dto.createdAt)?.slice(0, 16) ?? ''
    };
  }
}
