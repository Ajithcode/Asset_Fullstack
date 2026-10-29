import { Injectable } from '@angular/core';
import { initialBusinessUnits } from './business-unit.data';
import { BusinessUnitFilters, BusinessUnitRecord } from './business-unit.model';

/**
 * DEMO MODE: in-memory business units; replace these methods with API calls when the backend is
 * ready. Has no dependencies so the Site master can use it for its Business Unit lists.
 */
@Injectable({ providedIn: 'root' })
export class BusinessUnitService {
  private records: BusinessUnitRecord[] = initialBusinessUnits();

  all(): BusinessUnitRecord[] {
    return this.records;
  }

  names(): string[] {
    return this.records.map((r) => r.name);
  }

  find(code: string | null): BusinessUnitRecord | undefined {
    return code ? this.records.find((r) => r.code === code) : undefined;
  }

  findByName(name: string): BusinessUnitRecord | undefined {
    return this.records.find((r) => r.name === name);
  }

  children(code: string): BusinessUnitRecord[] {
    return this.records.filter((r) => r.parentCode === code);
  }

  /** `code` and every unit under it, so none can become its own parent. */
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

  filter(f: BusinessUnitFilters): BusinessUnitRecord[] {
    const term = f.search.trim().toLowerCase();
    return this.records.filter(
      (r) =>
        (!f.status || r.status === f.status) &&
        (!f.organizationCode || r.organizationCode === f.organizationCode) &&
        (!term || [r.name, r.code, r.description].some((v) => v.toLowerCase().includes(term)))
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

  save(record: BusinessUnitRecord, originalCode: string | null): void {
    const index = originalCode ? this.records.findIndex((r) => r.code === originalCode) : -1;
    if (index >= 0) {
      this.records[index] = record;
      if (originalCode !== record.code) {
        this.records.forEach((r) => {
          if (r.parentCode === originalCode) r.parentCode = record.code;
        });
      }
      this.records = [...this.records];
    } else {
      this.records = [...this.records, record];
    }
  }

  remove(codes: string[]): void {
    this.records = this.records
      .filter((r) => !codes.includes(r.code))
      .map((r) => (r.parentCode && codes.includes(r.parentCode) ? { ...r, parentCode: null } : r));
  }
}
