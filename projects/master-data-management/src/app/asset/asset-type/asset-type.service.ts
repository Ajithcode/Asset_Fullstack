import { Injectable } from '@angular/core';
import { AssetModelIcon } from '../asset-model/asset-model.icons';
import { TYPE_CATEGORIES, initialAssetTypes } from './asset-type.data';
import { AssetTypeFilters, AssetTypeRecord } from './asset-type.model';

/**
 * DEMO MODE: in-memory asset types; replace these methods with API calls when the backend is
 * ready. Has no dependencies so the Asset Model master can use it for its type lists and icons.
 */
@Injectable({ providedIn: 'root' })
export class AssetTypeService {
  private records: AssetTypeRecord[] = initialAssetTypes();

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

  /** Known categories plus any typed in on a record. */
  categories(): string[] {
    return [...new Set([...TYPE_CATEGORIES, ...this.records.map((r) => r.category)])];
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

  save(record: AssetTypeRecord, originalCode: string | null): void {
    const index = originalCode ? this.records.findIndex((r) => r.code === originalCode) : -1;
    if (index >= 0) {
      this.records[index] = record;
      this.records = [...this.records];
    } else {
      this.records = [record, ...this.records];
    }
  }

  remove(codes: string[]): void {
    this.records = this.records.filter((r) => !codes.includes(r.code));
  }
}
