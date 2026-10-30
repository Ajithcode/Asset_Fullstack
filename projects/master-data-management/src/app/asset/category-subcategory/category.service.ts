import { Injectable } from '@angular/core';
import { Observable, map, throwError } from 'rxjs';
import { MdmAudit, inSequence } from '../../shared/mdm-api';
import { AssetModelIcon } from '../asset-model/asset-model.icons';
import { AssetCondition, DepreciationMethod, TrackingMethod } from '../asset-model/asset-model.model';
import { CustomField, CustomFieldType } from '../asset-type/asset-type.model';
import { AssetStore, activeStatus, localStamp } from '../asset-store';
import { blankCategory } from './category.data';
import { CategoryFilters, CategoryRecord, SubcategoryRecord } from './category.model';

/** Asset category as `/api/master-data/asset-categories` returns it. Subcategories are kept inside it. */
interface CategoryDto extends MdmAudit {
  categoryName: string;
  code: string;
  assetType: string | null;
  description: string | null;
  parentCategory: { id: string | null; name: string } | null;
  icon: string | null;
  color: string | null;
  image: string | null;
  subcategories: { code: string; name: string; icon: string | null; color: string | null; status: string | null }[] | null;
  depreciationMethod: string | null;
  usefulLifeYears: number | null;
  costCenter: string | null;
  tagPrefix: string | null;
  trackingMethod: string | null;
  defaultCondition: string | null;
  customFields: { name: string; type: string | null; required: boolean }[] | null;
  subcategoryRequired: boolean;
  showInMobile: boolean;
  includeInReports: boolean;
  approvalForNewAssets: boolean;
}

/**
 * Asset categories and their subcategories from the master data API. A category's parent is stored
 * by id on the server and shown by code here; subcategories are saved by saving their category.
 */
@Injectable({ providedIn: 'root' })
export class CategoryService extends AssetStore<CategoryRecord, CategoryDto> {
  protected readonly resource = 'asset-categories';

  constructor() {
    super();
  }

  /** Fills the list; parents are resolved from ids to codes once every category is known. */
  override load(query: Record<string, string | number | boolean | null | undefined> = {}): Observable<CategoryRecord[]> {
    return super.load(query).pipe(
      map(() => {
        this.records = this.records.map((r) => (r.parentCode && !this.find(r.parentCode) ? { ...r, parentCode: this.codeOfId(r.parentCode) } : r));
        return this.records;
      })
    );
  }

  /** A category's assets are the sum of its subcategories (counts owned by the Assets module). */
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

  // Subcategory codes are unique across all categories
  isSubCodeTaken(code: string, exceptCode: string | null): boolean {
    const wanted = code.trim().toLowerCase();
    return this.records.some((r) => r.subcategories.some((s) => s.code.toLowerCase() === wanted && s.code !== exceptCode));
  }

  allSubCodes(): Set<string> {
    return new Set(this.records.flatMap((r) => r.subcategories.map((s) => s.code)));
  }

  /** Stores a saved category locally; child categories follow a code change. */
  override save(record: CategoryRecord, originalCode: string | null): void {
    super.save(record, originalCode);
    if (originalCode && originalCode !== record.code) {
      this.records = this.records.map((r) => (r.parentCode === originalCode ? { ...r, parentCode: record.code } : r));
    }
  }

  /** Deletes categories, children before their parents (the server refuses a parent that still has children). */
  deleteCategories(codes: string[]): Observable<unknown> {
    const depth = (code: string) => this.ancestry(code).length;
    const ordered = [...codes].sort((a, b) => depth(b) - depth(a));
    return inSequence(ordered.map((code) => this.delete([code])));
  }

  /** Adds or replaces a subcategory by saving its category. */
  saveSubcategory(categoryCode: string, sub: SubcategoryRecord, originalCode: string | null): Observable<CategoryRecord> {
    const category = this.find(categoryCode);
    if (!category) return throwError(() => new Error(`Category ${categoryCode} not found.`));
    const index = originalCode ? category.subcategories.findIndex((s) => s.code === originalCode) : -1;
    const subcategories = index >= 0 ? category.subcategories.map((s, i) => (i === index ? sub : s)) : [...category.subcategories, sub];
    return this.persist({ ...category, subcategories }, categoryCode);
  }

  removeSubcategories(categoryCode: string, codes: string[]): Observable<CategoryRecord> {
    const category = this.find(categoryCode);
    if (!category) return throwError(() => new Error(`Category ${categoryCode} not found.`));
    return this.persist({ ...category, subcategories: category.subcategories.filter((s) => !codes.includes(s.code)) }, categoryCode);
  }

  private codeOfId(id: string | null | undefined): string | null {
    return (id && this.records.find((r) => this.idOf(r.code) === id)?.code) || null;
  }

  protected toRecord(dto: CategoryDto): CategoryRecord {
    const blank = blankCategory();
    const parentId = dto.parentCategory?.id ?? null;
    return {
      ...blank,
      code: dto.code,
      name: dto.categoryName,
      assetType: dto.assetType ?? '',
      icon: (dto.icon as AssetModelIcon) ?? blank.icon,
      color: dto.color ?? blank.color,
      image: dto.image,
      description: dto.description ?? '',
      // Until every category is loaded the parent may still be its id; load() resolves it
      parentCode: this.codeOfId(parentId) ?? parentId,
      status: activeStatus(dto.status),
      subcategories: (dto.subcategories ?? []).map((s) => ({
        code: s.code,
        name: s.name,
        icon: (s.icon as AssetModelIcon) ?? blank.icon,
        color: s.color ?? blank.color,
        status: activeStatus(s.status),
        assets: 0
      })),
      depreciationMethod: (dto.depreciationMethod as DepreciationMethod) ?? blank.depreciationMethod,
      usefulLifeYears: dto.usefulLifeYears ?? blank.usefulLifeYears,
      costCenter: dto.costCenter ?? '',
      tagPrefix: dto.tagPrefix ?? '',
      trackingMethod: (dto.trackingMethod as TrackingMethod) ?? blank.trackingMethod,
      defaultCondition: (dto.defaultCondition as AssetCondition) ?? blank.defaultCondition,
      customFields: (dto.customFields ?? []).map((f): CustomField => ({ name: f.name, type: (f.type as CustomFieldType) ?? 'Text', required: f.required })),
      subcategoryRequired: dto.subcategoryRequired,
      showInMobile: dto.showInMobile,
      includeInReports: dto.includeInReports,
      approvalForNewAssets: dto.approvalForNewAssets,
      createdOn: localStamp(dto.createdAt),
      createdBy: dto.createdBy ?? '',
      updatedOn: localStamp(dto.updatedAt ?? dto.createdAt)
    };
  }

  protected toRequest(r: CategoryRecord): object {
    const parent = this.find(r.parentCode);
    return {
      categoryName: r.name,
      code: r.code,
      assetType: r.assetType,
      description: r.description,
      parentCategory: parent ? { id: this.idOf(parent.code) ?? null, name: parent.name } : null,
      icon: r.icon,
      color: r.color,
      image: r.image,
      subcategories: r.subcategories.map(({ code, name, icon, color, status }) => ({ code, name, icon, color, status })),
      depreciationMethod: r.depreciationMethod,
      usefulLifeYears: r.usefulLifeYears,
      costCenter: r.costCenter,
      tagPrefix: r.tagPrefix,
      trackingMethod: r.trackingMethod,
      defaultCondition: r.defaultCondition,
      customFields: r.customFields,
      subcategoryRequired: r.subcategoryRequired,
      showInMobile: r.showInMobile,
      includeInReports: r.includeInReports,
      approvalForNewAssets: r.approvalForNewAssets,
      status: r.status
    };
  }
}
