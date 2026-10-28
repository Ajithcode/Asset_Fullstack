import { Injectable } from '@angular/core';
import { initialSuppliers } from './supplier.data';
import { SupplierRecord } from './supplier.model';

/** DEMO MODE: in-memory suppliers; replace these methods with API calls when the backend is ready. */
@Injectable({ providedIn: 'root' })
export class SupplierService {
  private records: SupplierRecord[] = initialSuppliers();

  all(): SupplierRecord[] {
    return this.records;
  }

  find(code: string | null): SupplierRecord | undefined {
    return code ? this.records.find((r) => r.code === code) : undefined;
  }

  countries(): string[] {
    return [...new Set(this.records.map((r) => r.country))].sort((a, b) => a.localeCompare(b));
  }

  isCodeTaken(code: string, exceptCode: string | null): boolean {
    const wanted = code.trim().toLowerCase();
    return this.records.some((r) => r.code.toLowerCase() === wanted && r.code !== exceptCode);
  }

  isNameTaken(name: string, exceptCode: string | null): boolean {
    const wanted = name.trim().toLowerCase();
    return this.records.some((r) => r.name.toLowerCase() === wanted && r.code !== exceptCode);
  }

  save(record: SupplierRecord, originalCode: string | null): void {
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
