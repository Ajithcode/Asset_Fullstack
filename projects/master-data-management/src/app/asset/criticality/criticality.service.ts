import { Injectable } from '@angular/core';
import { initialCriticalities } from './criticality.data';
import { CriticalityFilters, CriticalityRecord } from './criticality.model';

/** DEMO MODE: in-memory criticality levels; replace these methods with API calls when the backend is ready. */
@Injectable({ providedIn: 'root' })
export class CriticalityService {
  private records: CriticalityRecord[] = initialCriticalities();

  all(): CriticalityRecord[] {
    return this.records;
  }

  find(code: string | null): CriticalityRecord | undefined {
    return code ? this.records.find((r) => r.code === code) : undefined;
  }

  filter(f: CriticalityFilters): CriticalityRecord[] {
    const term = f.search.trim().toLowerCase();
    return this.records.filter(
      (r) => (!f.status || r.status === f.status) && (!term || [r.name, r.code, r.description].some((v) => v.toLowerCase().includes(term)))
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

  /** Saves a record; only one level can be the default, so saving a default clears the others. */
  save(record: CriticalityRecord, originalCode: string | null): void {
    const others = record.isDefault ? this.records.map((r) => ({ ...r, isDefault: false })) : [...this.records];
    const index = originalCode ? others.findIndex((r) => r.code === originalCode) : -1;
    if (index >= 0) {
      others[index] = record;
      this.records = others;
    } else {
      this.records = [...others, record];
    }
  }

  remove(codes: string[]): void {
    this.records = this.records.filter((r) => !codes.includes(r.code));
  }
}
