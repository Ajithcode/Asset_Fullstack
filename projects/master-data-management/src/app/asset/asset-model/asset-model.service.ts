import { Injectable, inject, signal } from '@angular/core';
import { Observable, map, tap } from 'rxjs';
import { AssetServiceApi, MdmAudit } from '../../shared/mdm-api';
import { AssetStore, activeStatus, localStamp } from '../asset-store';
import { CategoryService } from '../category-subcategory/category.service';
import { ManufacturerService } from '../manufacturer/manufacturer.service';
import { blankAssetModel } from './asset-model.data';
import {
  AssetCondition,
  AssetModelFilters,
  AssetModelRecord,
  Criticality,
  DepreciationMethod,
  MaintenanceStrategy,
  PmFrequency,
  ProcurementType,
  TrackingMethod
} from './asset-model.model';

type Reference = { id: string | null; name: string } | null;

/** An asset type from the Asset service's `/api/asset-category-types` (the list Asset Registration uses). */
export interface CategoryTypeOption {
  id: string;
  name: string;
  /** The category or sub-category the type belongs to. */
  categoryId: string;
  status: string;
}

/** Asset model as `/api/master-data/asset-models` returns it. */
interface AssetModelDto extends MdmAudit {
  modelName: string;
  modelCode: string;
  manufacturer: Reference;
  assetType: Reference;
  category: Reference;
  subCategory: Reference;
  description: string | null;
  image: string | null;
  specs: { label: string; value: string | null }[] | null;
  lifecycleYears: number | null;
  warrantyMonths: number | null;
  pmFrequency: string | null;
  maintenanceStrategy: string | null;
  criticality: string | null;
  calibrationRequired: boolean;
  costCenter: string | null;
  currency: string | null;
  purchasePrice: number | null;
  includeInDepreciation: boolean;
  depreciationMethod: string | null;
  salvagePercent: number | null;
  procurementType: string | null;
  subcategories: string[] | null;
  taggingFormat: string | null;
  trackingMethod: string | null;
  defaultCondition: string | null;
  serialRequired: boolean;
  assignable: boolean;
}

/**
 * Asset models from the master data API. Manufacturer, asset type and category are shown by name
 * here and sent by id; the server renames them on models when the master is renamed.
 */
@Injectable({ providedIn: 'root' })
export class AssetModelService extends AssetStore<AssetModelRecord, AssetModelDto> {
  protected readonly resource = 'asset-models';
  private readonly makerStore = inject(ManufacturerService);
  private readonly categoryStore = inject(CategoryService);
  private readonly assetApi = inject(AssetServiceApi);
  /** Asset types from the Asset service, shared with Asset Registration. */
  readonly categoryTypes = signal<CategoryTypeOption[]>([]);

  constructor() {
    super();
  }

  loadCategoryTypes(): Observable<CategoryTypeOption[]> {
    return this.assetApi.list<unknown>('asset-category-types').pipe(
      map((res) => (Array.isArray(res) ? res : ((res as { items?: unknown[] })?.items ?? [])) as Record<string, string>[]),
      map((rows) => rows.map((r) => ({ id: r['id'], name: r['typeName'], categoryId: r['categoryId'], status: r['status'] ?? 'Active' }))),
      tap((types) => this.categoryTypes.set(types))
    );
  }

  /** Top-level category id for a name (sub-categories have a parent and are not model categories). */
  private categoryId(name: string): string | undefined {
    const category = this.categoryStore.all().find((c) => c.name === name && !c.parentCode);
    return category ? this.categoryStore.idOf(category.code) : undefined;
  }

  /** Id of the named sub category under the category. */
  private subCategoryId(name: string, categoryName: string): string | undefined {
    const category = this.categoryStore.all().find((c) => c.name === categoryName && !c.parentCode);
    const sub = category ? this.categoryStore.children(category.code).find((c) => c.name === name) : undefined;
    return sub ? this.categoryStore.idOf(sub.code) : undefined;
  }

  /** Id of the named type inside the category or one of its sub-categories. */
  private typeId(typeName: string, categoryName: string): string | undefined {
    const category = this.categoryStore.all().find((c) => c.name === categoryName && !c.parentCode);
    const ids = new Set<string | undefined>(
      category ? [this.categoryStore.idOf(category.code), ...this.categoryStore.children(category.code).map((c) => this.categoryStore.idOf(c.code))] : []
    );
    const types = this.categoryTypes().filter((t) => t.name === typeName);
    return (types.find((t) => ids.has(t.categoryId)) ?? types[0])?.id;
  }

  ofType(typeName: string): AssetModelRecord[] {
    return this.all().filter((r) => r.assetType === typeName);
  }

  /** Registered assets across every model of a type. */
  assetsOfType(typeName: string): number {
    return this.ofType(typeName).reduce((sum, r) => sum + r.assetCount, 0);
  }

  ofManufacturer(name: string): AssetModelRecord[] {
    return this.all().filter((r) => r.manufacturer === name);
  }

  /** Keeps the local list in step after a manufacturer rename (the server does the same). */
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
    return [...new Set(this.all().map((r) => r.manufacturer))].sort((a, b) => a.localeCompare(b));
  }

  usedTypes(): string[] {
    return [...new Set(this.all().map((r) => r.assetType))].sort((a, b) => a.localeCompare(b));
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

  // Model names only need to be unique per manufacturer
  isModelNameTaken(name: string, manufacturer: string, exceptCode: string | null): boolean {
    const wanted = name.trim().toLowerCase();
    return this.records.some((r) => r.manufacturer === manufacturer && r.name.toLowerCase() === wanted && r.code !== exceptCode);
  }

  protected toRecord(dto: AssetModelDto): AssetModelRecord {
    const blank = blankAssetModel();
    return {
      ...blank,
      code: dto.modelCode,
      name: dto.modelName,
      manufacturer: dto.manufacturer?.name ?? '',
      assetType: dto.assetType?.name ?? '',
      category: dto.category?.name ?? '',
      description: dto.description ?? '',
      image: dto.image,
      status: activeStatus(dto.status),
      specs: (dto.specs ?? []).map((s) => ({ label: s.label, value: s.value ?? '' })),
      lifecycleYears: dto.lifecycleYears ?? blank.lifecycleYears,
      warrantyMonths: dto.warrantyMonths ?? blank.warrantyMonths,
      pmFrequency: (dto.pmFrequency as PmFrequency) ?? blank.pmFrequency,
      maintenanceStrategy: (dto.maintenanceStrategy as MaintenanceStrategy) ?? blank.maintenanceStrategy,
      criticality: (dto.criticality as Criticality) ?? blank.criticality,
      calibrationRequired: dto.calibrationRequired,
      costCenter: dto.costCenter ?? '',
      currency: dto.currency ?? blank.currency,
      purchasePrice: dto.purchasePrice,
      includeInDepreciation: dto.includeInDepreciation,
      depreciationMethod: (dto.depreciationMethod as DepreciationMethod) ?? blank.depreciationMethod,
      salvagePercent: dto.salvagePercent ?? blank.salvagePercent,
      procurementType: (dto.procurementType as ProcurementType) ?? blank.procurementType,
      subcategories: dto.subCategory?.name ? [dto.subCategory.name] : (dto.subcategories ?? []),
      taggingFormat: dto.taggingFormat ?? '',
      trackingMethod: (dto.trackingMethod as TrackingMethod) ?? blank.trackingMethod,
      defaultCondition: (dto.defaultCondition as AssetCondition) ?? blank.defaultCondition,
      serialRequired: dto.serialRequired,
      assignable: dto.assignable,
      createdOn: localStamp(dto.createdAt),
      updatedOn: localStamp(dto.updatedAt ?? dto.createdAt)
    };
  }

  protected toRequest(r: AssetModelRecord): object {
    const ref = (name: string, code: string | undefined, id: (code: string) => string | undefined) => {
      const resolved = code ? id(code) : undefined;
      return resolved ? { id: resolved, name } : null;
    };
    return {
      modelName: r.name,
      modelCode: r.code,
      manufacturer: ref(r.manufacturer, this.makerStore.findByName(r.manufacturer)?.code, (c) => this.makerStore.idOf(c)),
      // Category and type come from the Asset service's masters, the same lists Asset Registration uses
      assetType: ref(r.assetType, r.assetType, () => this.typeId(r.assetType, r.category)),
      // Left out when unknown; the server then uses the asset type's category
      category: ref(r.category, r.category, () => this.categoryId(r.category)),
      subCategory: ref(r.subcategories[0] ?? '', r.subcategories[0], () => this.subCategoryId(r.subcategories[0], r.category)),
      description: r.description,
      image: r.image,
      specs: r.specs,
      lifecycleYears: r.lifecycleYears,
      warrantyMonths: r.warrantyMonths,
      pmFrequency: r.pmFrequency,
      maintenanceStrategy: r.maintenanceStrategy,
      criticality: r.criticality,
      calibrationRequired: r.calibrationRequired,
      costCenter: r.costCenter,
      currency: r.currency,
      purchasePrice: r.purchasePrice,
      includeInDepreciation: r.includeInDepreciation,
      depreciationMethod: r.depreciationMethod,
      salvagePercent: r.salvagePercent,
      procurementType: r.procurementType,
      subcategories: r.subcategories,
      taggingFormat: r.taggingFormat?.trim() || null,
      trackingMethod: r.trackingMethod,
      defaultCondition: r.defaultCondition,
      serialRequired: r.serialRequired,
      assignable: r.assignable,
      status: r.status
    };
  }
}
