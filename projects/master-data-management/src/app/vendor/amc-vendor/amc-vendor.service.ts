import { Injectable, inject } from '@angular/core';
import { AssetModelService } from '../../asset/asset-model/asset-model.service';
import { MODEL_TYPES_FOR, initialAmcVendors } from './amc-vendor.data';
import { AmcVendorRecord } from './amc-vendor.model';

/** DEMO MODE: in-memory AMC vendors; replace these methods with API calls when the backend is ready. */
@Injectable({ providedIn: 'root' })
export class AmcVendorService {
  private readonly models = inject(AssetModelService);
  private records: AmcVendorRecord[] = this.seed();

  // Seeded contracts cover the vendor's own models of the contract's asset type
  private seed(): AmcVendorRecord[] {
    const { records, coverage } = initialAmcVendors();
    return records.map((r, i) => {
      const { manufacturer, assetType } = coverage[i];
      const types = MODEL_TYPES_FOR[assetType] ?? [];
      const codes = manufacturer ? this.models.ofManufacturer(manufacturer).filter((m) => types.includes(m.assetType)).map((m) => m.code) : [];
      return { ...r, coveredModels: codes };
    });
  }

  all(): AmcVendorRecord[] {
    return this.records;
  }

  find(code: string | null): AmcVendorRecord | undefined {
    return code ? this.records.find((r) => r.code === code) : undefined;
  }

  /** Registered assets of the covered models. */
  coveredAssets(r: AmcVendorRecord): number {
    return r.coveredModels.reduce((sum, code) => sum + (this.models.find(code)?.assetCount ?? 0), 0);
  }

  isCodeTaken(code: string, exceptCode: string | null): boolean {
    const wanted = code.trim().toLowerCase();
    return this.records.some((r) => r.code.toLowerCase() === wanted && r.code !== exceptCode);
  }

  isNameTaken(name: string, exceptCode: string | null): boolean {
    const wanted = name.trim().toLowerCase();
    return this.records.some((r) => r.name.toLowerCase() === wanted && r.code !== exceptCode);
  }

  save(record: AmcVendorRecord, originalCode: string | null): void {
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
