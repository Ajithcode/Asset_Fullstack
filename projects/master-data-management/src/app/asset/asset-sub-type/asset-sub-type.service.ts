import { Injectable, inject } from '@angular/core';
import { MdmAudit } from '../../shared/mdm-api';
import { AssetStore, activeStatus, localStamp } from '../asset-store';
import { AssetModelService } from '../asset-model/asset-model.service';
import { CategoryService } from '../category-subcategory/category.service';
import { blankAssetSubType } from './asset-sub-type.data';
import { AssetSubTypeFilters, AssetSubTypeRecord } from './asset-sub-type.model';

/** Asset sub type as `/api/master-data/asset-sub-types` returns it. */
interface AssetSubTypeDto extends MdmAudit {
  subTypeName: string;
  code: string;
  assetType: { id: string | null; name: string } | null;
  description: string | null;
  /** Everything the form edits beyond name/code/type/description, as one JSON document. */
  settingsJson: string | null;
}

type Settings = Partial<Omit<AssetSubTypeRecord, 'assetTypeId' | 'code' | 'name' | 'assetType' | 'description' | 'status' | 'category' | 'assetCount' | 'createdOn' | 'createdBy' | 'updatedOn'>>;

/**
 * Asset sub types from the master data API (`asset-sub-types`). The parent is an asset type of the
 * Asset Type master: sent by id, shown by name. Asset Registration reads this list per asset type.
 */
@Injectable({ providedIn: 'root' })
export class AssetSubTypeService extends AssetStore<AssetSubTypeRecord, AssetSubTypeDto> {
  protected readonly resource = 'asset-sub-types';
  private readonly models = inject(AssetModelService);
  private readonly categoryStore = inject(CategoryService);

  constructor() {
    super();
  }

  /** Names only need to be unique under the same parent type. */
  override isNameTaken(name: string, assetType: string | null, exceptCode: string | null = null): boolean {
    const n = name.trim().toLowerCase();
    return this.records.some((r) => r.assetType === assetType && (r.name ?? '').toLowerCase() === n && r.code !== exceptCode);
  }

  /** Asset types of the Asset service master (the list Asset Registration and Asset Model use). */
  loadTypes() {
    return this.models.loadCategoryTypes();
  }

  /** Active asset types for the form (plus the type already chosen), by id. */
  typeOptions(currentId = ''): { id: string; name: string }[] {
    return this.models.categoryTypes().filter((t) => t.status !== 'Inactive' || t.id === currentId);
  }

  /** Parent asset type names for the list filter: the master's types plus any already used by a sub type. */
  parentTypes(): string[] {
    const live = this.models.categoryTypes().filter((t) => t.status !== 'Inactive').map((t) => t.name);
    return [...new Set([...live, ...this.records.map((r) => r.assetType).filter(Boolean)])].sort();
  }

  /** The type's category (top-level) and sub category names, from its category id. */
  private placeOf(typeId: string): { category: string; sub: string } {
    const cats = this.categoryStore.all();
    const own = cats.find((c) => this.categoryStore.idOf(c.code) === this.models.categoryTypes().find((t) => t.id === typeId)?.categoryId);
    if (!own) return { category: '', sub: '' };
    const parent = own.parentCode ? cats.find((c) => c.code === own.parentCode) : undefined;
    return parent ? { category: parent.name, sub: own.name } : { category: own.name, sub: '' };
  }

  categoryOf(typeId: string): string {
    return this.placeOf(typeId).category;
  }

  subcategoriesOf(typeId: string): string[] {
    const sub = this.placeOf(typeId).sub;
    return sub ? [sub] : [];
  }

  filter(f: AssetSubTypeFilters): AssetSubTypeRecord[] {
    const term = f.search.trim().toLowerCase();
    return this.records.filter(
      (r) =>
        (!f.status || r.status === f.status) &&
        (!f.assetType || r.assetType === f.assetType) &&
        (!term || [r.name, r.code, r.assetType, r.category, r.description].some((v) => (v ?? '').toLowerCase().includes(term)))
    );
  }

  protected toRecord(dto: AssetSubTypeDto): AssetSubTypeRecord {
    const typeName = dto.assetType?.name ?? '';
    const typeId = dto.assetType?.id ?? '';
    return {
      ...blankAssetSubType(),
      ...this.parseSettings(dto.settingsJson),
      code: dto.code,
      name: dto.subTypeName,
      assetType: typeName,
      assetTypeId: typeId,
      category: this.categoryOf(typeId),
      description: dto.description ?? '',
      status: activeStatus(dto.status),
      createdOn: localStamp(dto.createdAt),
      createdBy: dto.createdBy ?? '',
      updatedOn: localStamp(dto.updatedAt ?? dto.createdAt)
    };
  }

  protected toRequest(r: AssetSubTypeRecord): object {
    const typeId = r.assetTypeId;
    const { assetTypeId: _i, code: _c, name: _n, assetType: _t, description: _d, status: _s, category: _g, assetCount: _a, createdOn: _co, createdBy: _cb, updatedOn: _u, ...settings } = r;
    return {
      subTypeName: r.name,
      code: r.code,
      assetType: typeId ? { id: typeId, name: r.assetType } : null,
      description: r.description,
      settingsJson: JSON.stringify(settings),
      status: r.status
    };
  }

  private parseSettings(json: string | null): Settings {
    try {
      return json ? (JSON.parse(json) as Settings) : {};
    } catch {
      return {};
    }
  }
}
