import { Injectable } from '@angular/core';
import { initialManufacturers } from './manufacturer.data';
import { ManufacturerFilters, ManufacturerRecord } from './manufacturer.model';

/**
 * DEMO MODE: in-memory manufacturers; replace these methods with API calls when the backend is
 * ready. Has no dependencies so the Asset Model master can use it for its Manufacturer list.
 */
@Injectable({ providedIn: 'root' })
export class ManufacturerService {
  private records: ManufacturerRecord[] = initialManufacturers();

  all(): ManufacturerRecord[] {
    return this.records;
  }

  find(code: string | null): ManufacturerRecord | undefined {
    return code ? this.records.find((r) => r.code === code) : undefined;
  }

  // Asset models refer to their manufacturer by name
  findByName(name: string): ManufacturerRecord | undefined {
    return this.records.find((r) => r.name === name);
  }

  /** Active manufacturers A-Z, plus `keep` so an existing choice stays selectable. */
  activeNames(keep = ''): string[] {
    return this.records
      .filter((r) => r.status === 'Active' || r.name === keep)
      .map((r) => r.name)
      .sort((a, b) => a.localeCompare(b));
  }

  // `makesType` answers whether a manufacturer has a model of the chosen asset type
  filter(f: ManufacturerFilters, makesType: (name: string) => boolean): ManufacturerRecord[] {
    const term = f.search.trim().toLowerCase();
    return this.records.filter(
      (r) =>
        (!f.status || r.status === f.status) &&
        (!f.assetType || makesType(r.name)) &&
        (!term || [r.name, r.code, r.country, r.primaryContact, r.email].some((v) => v.toLowerCase().includes(term)))
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

  save(record: ManufacturerRecord, originalCode: string | null): void {
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
