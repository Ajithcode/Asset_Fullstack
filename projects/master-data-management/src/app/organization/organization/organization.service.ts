import { Injectable } from '@angular/core';
import { initialOrganizations } from './organization.data';
import { OrganizationFilters, OrganizationRecord } from './organization.model';

/** DEMO MODE: in-memory organizations; replace these methods with API calls when the backend is ready. */
@Injectable({ providedIn: 'root' })
export class OrganizationService {
  private records: OrganizationRecord[] = initialOrganizations();

  all(): OrganizationRecord[] {
    return this.records;
  }

  find(code: string | null): OrganizationRecord | undefined {
    return code ? this.records.find((r) => r.code === code) : undefined;
  }

  children(code: string): OrganizationRecord[] {
    return this.records.filter((r) => r.parentCode === code);
  }

  /** Top-level organization first, ending with `code` itself. */
  ancestry(code: string | null): OrganizationRecord[] {
    const chain: OrganizationRecord[] = [];
    for (let r = this.find(code); r && chain.length < 20; r = this.find(r.parentCode)) {
      chain.unshift(r);
    }
    return chain;
  }

  /** `code` and everything below it, so it can't be chosen as its own parent. */
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

  filter(f: OrganizationFilters): OrganizationRecord[] {
    const term = f.search.trim().toLowerCase();
    return this.records.filter(
      (r) =>
        (!f.status || r.status === f.status) &&
        (!f.type || r.type === f.type) &&
        (!term ||
          [r.name, r.code, r.type, this.find(r.parentCode)?.name ?? ''].some((v) => v.toLowerCase().includes(term)))
    );
  }

  stats() {
    const active = this.records.filter((r) => r.status === 'Active').length;
    return {
      total: this.records.length,
      active,
      inactive: this.records.length - active,
      topLevel: this.records.filter((r) => !r.parentCode).length,
      businessUnits: this.records.reduce((sum, r) => sum + r.businessUnits, 0),
      sites: this.records.reduce((sum, r) => sum + r.sites, 0)
    };
  }

  isCodeTaken(code: string, exceptCode: string | null): boolean {
    const wanted = code.trim().toLowerCase();
    return this.records.some((r) => r.code.toLowerCase() === wanted && r.code !== exceptCode);
  }

  save(record: OrganizationRecord, originalCode: string | null): void {
    const index = originalCode ? this.records.findIndex((r) => r.code === originalCode) : -1;
    if (index >= 0) {
      this.records[index] = record;
      if (originalCode !== record.code) {
        this.records.forEach((r) => {
          if (r.parentCode === originalCode) r.parentCode = record.code;
        });
      }
    } else {
      this.records.push(record);
    }
    this.records = [...this.records];
  }

  remove(codes: string[]): void {
    this.records = this.records.filter((r) => !codes.includes(r.code));
  }
}
