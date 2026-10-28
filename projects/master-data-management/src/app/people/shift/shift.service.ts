import { Injectable } from '@angular/core';
import { initialShifts } from './shift.data';
import { ShiftRecord } from './shift.model';

/** DEMO MODE: in-memory shifts; replace these methods with API calls when the backend is ready. */
@Injectable({ providedIn: 'root' })
export class ShiftService {
  private records: ShiftRecord[] = initialShifts();

  all(): ShiftRecord[] {
    return this.records;
  }

  find(code: string | null): ShiftRecord | undefined {
    return code ? this.records.find((r) => r.code === code) : undefined;
  }

  isCodeTaken(code: string, exceptCode: string | null): boolean {
    const wanted = code.trim().toLowerCase();
    return this.records.some((r) => r.code.toLowerCase() === wanted && r.code !== exceptCode);
  }

  isNameTaken(name: string, exceptCode: string | null): boolean {
    const wanted = name.trim().toLowerCase();
    return this.records.some((r) => r.name.toLowerCase() === wanted && r.code !== exceptCode);
  }

  save(record: ShiftRecord, originalCode: string | null): void {
    const index = originalCode ? this.records.findIndex((r) => r.code === originalCode) : -1;
    if (index >= 0) {
      this.records[index] = record;
      this.records = [...this.records];
    } else {
      this.records = [...this.records, record];
    }
  }

  remove(codes: string[]): void {
    this.records = this.records.filter((r) => !codes.includes(r.code));
  }
}
