import { Injectable } from '@angular/core';
import { CodeStore } from '../shared/code-store';
import { initialBins, initialItemCategories, initialItems, initialStockStatuses, initialStores, initialUoms, initialWarehouses } from './inventory.data';
import { BinRecord, ItemCategoryRecord, ItemRecord, StockStatusRecord, StoreRecord, UomRecord, WarehouseRecord } from './inventory.model';

/*
 * DEMO MODE: in-memory inventory masters; replace with API calls when the backend is ready.
 * None of these services depend on each other; pages combine them.
 */

@Injectable({ providedIn: 'root' })
export class ItemCategoryService extends CodeStore<ItemCategoryRecord> {
  constructor() {
    super(initialItemCategories());
  }

  children(code: string): ItemCategoryRecord[] {
    return this.records.filter((r) => r.parentCode === code);
  }

  /** Parents from the top down, not including `code` itself. */
  ancestry(code: string | null): ItemCategoryRecord[] {
    const chain: ItemCategoryRecord[] = [];
    for (let c = this.find(code); c && chain.length < 20; c = this.find(c.parentCode)) chain.unshift(c);
    return chain;
  }

  /** `code` and every category under it, so none can become its own parent. */
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

  override save(record: ItemCategoryRecord, originalCode: string | null): void {
    super.save(record, originalCode);
    // Sub-categories follow a code change
    if (originalCode && originalCode !== record.code) {
      this.records = this.records.map((r) => (r.parentCode === originalCode ? { ...r, parentCode: record.code } : r));
    }
  }
}

@Injectable({ providedIn: 'root' })
export class UomService extends CodeStore<UomRecord> {
  constructor() {
    super(initialUoms());
  }

  label(code: string): string {
    return this.find(code)?.symbol ?? code;
  }

  /** Units based on `code`. */
  derived(code: string): UomRecord[] {
    return this.records.filter((r) => r.baseUom === code);
  }

  override save(record: UomRecord, originalCode: string | null): void {
    super.save(record, originalCode);
    if (originalCode && originalCode !== record.code) {
      this.records = this.records.map((r) => (r.baseUom === originalCode ? { ...r, baseUom: record.code } : r));
    }
  }
}

@Injectable({ providedIn: 'root' })
export class StockStatusService extends CodeStore<StockStatusRecord> {
  constructor() {
    super(initialStockStatuses());
  }


  /** Only one status can be the system default, so saving a default clears the others. */
  override save(record: StockStatusRecord, originalCode: string | null): void {
    if (record.isDefault) this.records = this.records.map((r) => ({ ...r, isDefault: false }));
    super.save(record, originalCode);
  }
}

@Injectable({ providedIn: 'root' })
export class WarehouseService extends CodeStore<WarehouseRecord> {
  constructor() {
    super(initialWarehouses());
  }
}

@Injectable({ providedIn: 'root' })
export class StoreService extends CodeStore<StoreRecord> {
  constructor() {
    super(initialStores());
  }

  ofWarehouse(code: string): StoreRecord[] {
    return this.records.filter((r) => r.warehouseCode === code);
  }

  moveWarehouse(from: string, to: string): void {
    this.records = this.records.map((r) => (r.warehouseCode === from ? { ...r, warehouseCode: to } : r));
  }
}

@Injectable({ providedIn: 'root' })
export class BinService extends CodeStore<BinRecord> {
  constructor() {
    super(initialBins());
  }

  ofStore(code: string): BinRecord[] {
    return this.records.filter((r) => r.storeCode === code);
  }

  moveStore(from: string, to: string): void {
    this.records = this.records.map((r) => (r.storeCode === from ? { ...r, storeCode: to } : r));
  }
}

export type StockState = 'In Stock' | 'Low Stock' | 'Reorder' | 'Out of Stock';

@Injectable({ providedIn: 'root' })
export class ItemService extends CodeStore<ItemRecord> {
  constructor() {
    super(initialItems());
  }

  /** Out of stock at 0, Reorder at or below the minimum, Low Stock at or below the reorder level. */
  stockState(item: ItemRecord): StockState {
    if (item.currentStock <= 0) return 'Out of Stock';
    if (item.currentStock <= item.minStock) return 'Reorder';
    if (item.currentStock <= item.reorderLevel) return 'Low Stock';
    return 'In Stock';
  }

  nextCode(): string {
    const max = this.records.reduce((m, r) => Math.max(m, Number(r.code.replace(/\D/g, '')) || 0), 0);
    return `ITM-${String(max + 1).padStart(4, '0')}`;
  }

  usingCategory(code: string): ItemRecord[] {
    return this.records.filter((r) => r.category === code);
  }

  usingUom(code: string): ItemRecord[] {
    return this.records.filter((r) => r.uom === code);
  }

  inWarehouse(code: string): ItemRecord[] {
    return this.records.filter((r) => r.warehouseCode === code);
  }

  inStore(code: string): ItemRecord[] {
    return this.records.filter((r) => r.storeCode === code);
  }

  inBin(code: string): ItemRecord[] {
    return this.records.filter((r) => r.binCode === code);
  }

  /** Carries a renamed category, unit, warehouse, store or bin code into the items. */
  recode(field: 'category' | 'uom' | 'warehouseCode' | 'storeCode' | 'binCode', from: string, to: string): void {
    if (from === to) return;
    this.records = this.records.map((r) => (r[field] === from ? { ...r, [field]: to } : r));
  }
}
