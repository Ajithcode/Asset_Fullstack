import { Injectable } from '@angular/core';
import { initialConditions } from './condition.data';
import { ConditionFilters, ConditionRecord } from './condition.model';

/** DEMO MODE: in-memory condition values; replace these methods with API calls when the backend is ready. */
@Injectable({ providedIn: 'root' })
export class ConditionService {
  private records: ConditionRecord[] = initialConditions();

  all(): ConditionRecord[] {
    return this.records;
  }

  find(code: string | null): ConditionRecord | undefined {
    return code ? this.records.find((r) => r.code === code) : undefined;
  }

  nameOf(code: string): string {
    return this.find(code)?.name ?? code;
  }

  filter(f: ConditionFilters): ConditionRecord[] {
    const term = f.search.trim().toLowerCase();
    return this.records.filter(
      (r) =>
        (!f.status || r.status === f.status) &&
        (!f.type || r.type === f.type) &&
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

  /**
   * Saves a record. Only one condition can be the default, a changed code is carried into other
   * conditions' transition rules, and "can move to / from" stay mirrored on both sides.
   */
  save(record: ConditionRecord, originalCode: string | null): void {
    const oldCode = originalCode ?? record.code;
    let list = this.records.map((r) => {
      const rename = (codes: string[]) => codes.map((c) => (c === oldCode ? record.code : c));
      return { ...r, isDefault: record.isDefault ? false : r.isDefault, canMoveFrom: rename(r.canMoveFrom), canMoveTo: rename(r.canMoveTo) };
    });
    const index = originalCode ? list.findIndex((r) => r.code === originalCode) : -1;
    if (index >= 0) list[index] = record;
    else list = [...list, record];

    // Mirror this record's rules onto the other conditions
    this.records = list.map((r) => {
      if (r.code === record.code) return r;
      const from = r.canMoveFrom.filter((c) => c !== record.code);
      const to = r.canMoveTo.filter((c) => c !== record.code);
      if (record.canMoveTo.includes(r.code)) from.push(record.code);
      if (record.canMoveFrom.includes(r.code)) to.push(record.code);
      return { ...r, canMoveFrom: from, canMoveTo: to };
    });
  }

  remove(codes: string[]): void {
    this.records = this.records
      .filter((r) => !codes.includes(r.code))
      .map((r) => ({ ...r, canMoveFrom: r.canMoveFrom.filter((c) => !codes.includes(c)), canMoveTo: r.canMoveTo.filter((c) => !codes.includes(c)) }));
  }
}
