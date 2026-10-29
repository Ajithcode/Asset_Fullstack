import { Injectable } from '@angular/core';
import { MasterRecord } from './master-table.model';

/**
 * DEMO MODE: keeps each master list in memory so edits survive moving between pages.
 * Swap these methods for HTTP calls when the master data API is ready.
 */
@Injectable({ providedIn: 'root' })
export class MasterDataStore {
  private readonly lists = new Map<string, MasterRecord[]>();

  list(key: string, seed: MasterRecord[]): MasterRecord[] {
    if (!this.lists.has(key)) {
      this.lists.set(key, seed.map((r) => ({ ...r })));
    }
    return this.lists.get(key)!;
  }

  save(key: string, record: MasterRecord, originalCode: string | null): void {
    const rows = this.lists.get(key) ?? [];
    const index = originalCode === null ? -1 : rows.findIndex((r) => r.code === originalCode);
    if (index >= 0) {
      rows[index] = record;
    } else {
      rows.unshift(record);
    }
    this.lists.set(key, rows);
  }

  remove(key: string, codes: string[]): void {
    const rows = this.lists.get(key) ?? [];
    this.lists.set(key, rows.filter((r) => !codes.includes(r.code)));
  }
}
