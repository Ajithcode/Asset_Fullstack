import { Injectable } from '@angular/core';
import { initialDepartments } from './department.data';
import { DepartmentFilters, DepartmentRecord } from './department.model';

/**
 * DEMO MODE: in-memory departments; replace these methods with API calls when the backend is
 * ready. Has no dependencies so the Business Unit and Site masters can use it.
 */
@Injectable({ providedIn: 'root' })
export class DepartmentService {
  private records: DepartmentRecord[] = initialDepartments();

  all(): DepartmentRecord[] {
    return this.records;
  }

  find(code: string | null): DepartmentRecord | undefined {
    return code ? this.records.find((r) => r.code === code) : undefined;
  }

  ofBusinessUnit(businessUnitCode: string): DepartmentRecord[] {
    return this.records.filter((r) => r.businessUnitCode === businessUnitCode);
  }

  /** Department names for a business unit (by its name or code), for pickers on other masters. */
  names(businessUnitCode = ''): string[] {
    return this.records.filter((r) => !businessUnitCode || r.businessUnitCode === businessUnitCode).map((r) => r.name);
  }

  children(code: string): DepartmentRecord[] {
    return this.records.filter((r) => r.parentCode === code);
  }

  ancestry(code: string | null): DepartmentRecord[] {
    const chain: DepartmentRecord[] = [];
    for (let r = this.find(code); r && chain.length < 20; r = this.find(r.parentCode)) chain.unshift(r);
    return chain;
  }

  /** `code` and every department under it, so none can become its own parent. */
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

  filter(f: DepartmentFilters, businessUnitName: (code: string) => string): DepartmentRecord[] {
    const term = f.search.trim().toLowerCase();
    return this.records.filter(
      (r) =>
        (!f.businessUnitCode || r.businessUnitCode === f.businessUnitCode) &&
        (!f.status || r.status === f.status) &&
        (!term || [r.name, r.code, r.description, businessUnitName(r.businessUnitCode)].some((v) => v.toLowerCase().includes(term)))
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

  /** Keeps departments attached when their business unit's code changes. */
  renameBusinessUnit(oldCode: string, newCode: string): void {
    if (oldCode === newCode) return;
    this.records = this.records.map((r) => (r.businessUnitCode === oldCode ? { ...r, businessUnitCode: newCode } : r));
  }

  save(record: DepartmentRecord, originalCode: string | null): void {
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
