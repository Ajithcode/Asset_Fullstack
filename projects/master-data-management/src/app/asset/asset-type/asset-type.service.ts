import { Injectable, inject } from '@angular/core';
import { MdmAudit } from '../../shared/mdm-api';
import { AssetModelIcon } from '../asset-model/asset-model.icons';
import { AssetCondition, DepreciationMethod, ProcurementType, TrackingMethod } from '../asset-model/asset-model.model';
import { AssetStore, activeStatus, localStamp } from '../asset-store';
import { CategoryService } from '../category-subcategory/category.service';
import { blankAssetType } from './asset-type.data';
import { AssetTypeFilters, AssetTypeRecord, CustomField, CustomFieldType, MaintenanceType } from './asset-type.model';

/** Asset type as `/api/master-data/asset-types` returns it. */
interface AssetTypeDto extends MdmAudit {
  assetTypeName: string;
  code: string;
  category: { id: string | null; name: string } | null;
  assetClass: string | null;
  description: string | null;
  depreciationMethod: string | null;
  defaultLifetime: number | null;
  icon: string | null;
  image: string | null;
  subcategories: string[] | null;
  taggingFormat: string | null;
  maintenanceType: string | null;
  maintenancePlan: string | null;
  warrantyMonths: number | null;
  serviceProvider: string | null;
  usefulLifeMonths: number | null;
  residualPercent: number | null;
  costCenter: string | null;
  currency: string | null;
  capitalizationThreshold: number | null;
  procurementType: string | null;
  defaultCondition: string | null;
  trackingMethod: string | null;
  serialRequired: boolean;
  assignable: boolean;
  customFields: { name: string; type: string | null; required: boolean }[] | null;
  inspectionRequired: boolean;
  allowTransfers: boolean;
  iotTracking: boolean;
  disposalApproval: boolean;
}

/**
 * Asset types from the master data API. The category is an asset category, shown by name here
 * and sent by id. Asset models use this service for their type lists and icons.
 */
@Injectable({ providedIn: 'root' })
export class AssetTypeService extends AssetStore<AssetTypeRecord, AssetTypeDto> {
  protected readonly resource = 'asset-types';
  private readonly categoryStore = inject(CategoryService);

  constructor() {
    super();
  }

  active(): AssetTypeRecord[] {
    return this.all().filter((r) => r.status === 'Active');
  }

  iconFor(name: string): AssetModelIcon {
    return this.findByName(name)?.icon ?? 'conveyor';
  }

  /** Asset category names, plus any category already on a type. */
  categories(): string[] {
    return [...new Set([...this.categoryStore.all().map((c) => c.name), ...this.records.map((r) => r.category).filter(Boolean)])];
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

  protected toRecord(dto: AssetTypeDto): AssetTypeRecord {
    const blank = blankAssetType();
    return {
      ...blank,
      code: dto.code,
      name: dto.assetTypeName,
      category: dto.category?.name ?? '',
      assetClass: dto.assetClass ?? '',
      icon: (dto.icon as AssetModelIcon) ?? blank.icon,
      image: dto.image,
      description: dto.description ?? '',
      status: activeStatus(dto.status),
      subcategories: dto.subcategories ?? [],
      taggingFormat: dto.taggingFormat ?? '',
      lifecycleYears: dto.defaultLifetime ?? blank.lifecycleYears,
      maintenanceType: (dto.maintenanceType as MaintenanceType) ?? blank.maintenanceType,
      maintenancePlan: dto.maintenancePlan ?? '',
      warrantyMonths: dto.warrantyMonths ?? blank.warrantyMonths,
      serviceProvider: dto.serviceProvider ?? '',
      depreciationMethod: (dto.depreciationMethod as DepreciationMethod) ?? blank.depreciationMethod,
      usefulLifeMonths: dto.usefulLifeMonths ?? blank.usefulLifeMonths,
      residualPercent: dto.residualPercent ?? blank.residualPercent,
      costCenter: dto.costCenter ?? '',
      currency: dto.currency ?? blank.currency,
      capitalizationThreshold: dto.capitalizationThreshold ?? blank.capitalizationThreshold,
      procurementType: (dto.procurementType as ProcurementType) ?? blank.procurementType,
      defaultCondition: (dto.defaultCondition as AssetCondition) ?? blank.defaultCondition,
      trackingMethod: (dto.trackingMethod as TrackingMethod) ?? blank.trackingMethod,
      serialRequired: dto.serialRequired,
      assignable: dto.assignable,
      customFields: (dto.customFields ?? []).map((f): CustomField => ({ name: f.name, type: (f.type as CustomFieldType) ?? 'Text', required: f.required })),
      inspectionRequired: dto.inspectionRequired,
      allowTransfers: dto.allowTransfers,
      iotTracking: dto.iotTracking,
      disposalApproval: dto.disposalApproval,
      createdOn: localStamp(dto.createdAt),
      createdBy: dto.createdBy ?? '',
      updatedOn: localStamp(dto.updatedAt ?? dto.createdAt)
    };
  }

  protected toRequest(r: AssetTypeRecord): object {
    const category = this.categoryStore.findByName(r.category);
    const categoryId = category ? this.categoryStore.idOf(category.code) : undefined;
    return {
      assetTypeName: r.name,
      code: r.code,
      category: categoryId ? { id: categoryId, name: r.category } : null,
      assetClass: r.assetClass,
      description: r.description,
      depreciationMethod: r.depreciationMethod,
      defaultLifetime: r.lifecycleYears,
      icon: r.icon,
      image: r.image,
      subcategories: r.subcategories,
      taggingFormat: r.taggingFormat,
      maintenanceType: r.maintenanceType,
      maintenancePlan: r.maintenancePlan,
      warrantyMonths: r.warrantyMonths,
      serviceProvider: r.serviceProvider,
      usefulLifeMonths: r.usefulLifeMonths,
      residualPercent: r.residualPercent,
      costCenter: r.costCenter,
      currency: r.currency,
      capitalizationThreshold: r.capitalizationThreshold,
      procurementType: r.procurementType,
      defaultCondition: r.defaultCondition,
      trackingMethod: r.trackingMethod,
      serialRequired: r.serialRequired,
      assignable: r.assignable,
      customFields: r.customFields,
      inspectionRequired: r.inspectionRequired,
      allowTransfers: r.allowTransfers,
      iotTracking: r.iotTracking,
      disposalApproval: r.disposalApproval,
      status: r.status
    };
  }
}
