import { Injectable, inject } from '@angular/core';
import { MdmAudit } from '../../shared/mdm-api';
import { AssetStore, activeStatus, localStamp } from '../asset-store';
import { AssetTypeService } from '../asset-type/asset-type.service';
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

/** Asset model as `/api/master-data/asset-models` returns it. */
interface AssetModelDto extends MdmAudit {
  modelName: string;
  modelCode: string;
  manufacturer: Reference;
  assetType: Reference;
  category: Reference;
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
  private readonly typeStore = inject(AssetTypeService);
  private readonly makerStore = inject(ManufacturerService);
  private readonly categoryStore = inject(CategoryService);

  constructor() {
    super();
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
      subcategories: dto.subcategories ?? [],
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
      assetType: ref(r.assetType, this.typeStore.findByName(r.assetType)?.code, (c) => this.typeStore.idOf(c)),
      // Left out when unknown; the server then uses the asset type's category
      category: ref(r.category, this.categoryStore.findByName(r.category)?.code, (c) => this.categoryStore.idOf(c)),
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
      taggingFormat: r.taggingFormat,
      trackingMethod: r.trackingMethod,
      defaultCondition: r.defaultCondition,
      serialRequired: r.serialRequired,
      assignable: r.assignable,
      status: r.status
    };
  }
}
