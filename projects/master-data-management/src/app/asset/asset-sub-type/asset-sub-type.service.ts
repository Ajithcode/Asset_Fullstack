import { Injectable, inject } from '@angular/core';
import { MdmAudit } from '../../shared/mdm-api';
import { AssetStore, activeStatus, localStamp } from '../asset-store';
import { AssetTypeService } from '../asset-type/asset-type.service';
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

type Settings = Partial<Omit<AssetSubTypeRecord, 'code' | 'name' | 'assetType' | 'description' | 'status' | 'category' | 'assetCount' | 'createdOn' | 'createdBy' | 'updatedOn'>>;

/**
 * Asset sub types from the master data API (`asset-sub-types`). The parent is an asset type of the
 * Asset Type master: sent by id, shown by name. Asset Registration reads this list per asset type.
 */
@Injectable({ providedIn: 'root' })
export class AssetSubTypeService extends AssetStore<AssetSubTypeRecord, AssetSubTypeDto> {
  protected readonly resource = 'asset-sub-types';
  private readonly types = inject(AssetTypeService);

  constructor() {
    super();
  }

  /** Names only need to be unique under the same parent type. */
  override isNameTaken(name: string, assetType: string | null, exceptCode: string | null = null): boolean {
    const n = name.trim().toLowerCase();
    return this.records.some((r) => r.assetType === assetType && (r.name ?? '').toLowerCase() === n && r.code !== exceptCode);
  }

  /** Parent asset type names: the Asset Type master's active types, plus any already used by a sub type. */
  parentTypes(): string[] {
    const live = this.types.active().map((t) => t.name);
    return [...new Set([...live, ...this.records.map((r) => r.assetType).filter(Boolean)])].sort();
  }

  categoryOf(typeName: string): string {
    return this.types.findByName(typeName)?.category ?? '';
  }

  subcategoriesOf(typeName: string): string[] {
    return this.types.findByName(typeName)?.subcategories ?? [];
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
    return {
      ...blankAssetSubType(),
      ...this.parseSettings(dto.settingsJson),
      code: dto.code,
      name: dto.subTypeName,
      assetType: typeName,
      category: this.categoryOf(typeName),
      description: dto.description ?? '',
      status: activeStatus(dto.status),
      createdOn: localStamp(dto.createdAt),
      createdBy: dto.createdBy ?? '',
      updatedOn: localStamp(dto.updatedAt ?? dto.createdAt)
    };
  }

  protected toRequest(r: AssetSubTypeRecord): object {
    const type = this.types.findByName(r.assetType);
    const typeId = type ? this.types.idOf(type.code) : undefined;
    const { code: _c, name: _n, assetType: _t, description: _d, status: _s, category: _g, assetCount: _a, createdOn: _co, createdBy: _cb, updatedOn: _u, ...settings } = r;
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
