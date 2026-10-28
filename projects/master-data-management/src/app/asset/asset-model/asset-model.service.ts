import { Injectable } from '@angular/core';
import { initialAssetModels } from './asset-model.data';
import { AssetModelFilters, AssetModelRecord } from './asset-model.model';

/** DEMO MODE: in-memory asset models; replace these methods with API calls when the backend is ready. */
@Injectable({ providedIn: 'root' })
export class AssetModelService {
  private records: AssetModelRecord[] = initialAssetModels();

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

  save(record: AssetModelRecord, originalCode: string | null): void {
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
