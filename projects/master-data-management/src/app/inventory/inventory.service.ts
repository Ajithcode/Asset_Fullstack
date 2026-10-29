import { Injectable, inject } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { EmployeeService } from '../people/employee/employee.service';
import { ApiCodeStore, MasterDto } from '../shared/api-code-store';
import { blankBin, blankItem, blankItemCategory, blankStockStatus, blankStore, blankUom, blankWarehouse } from './inventory.data';
import { BinRecord, ItemCategoryRecord, ItemRecord, StockStatusRecord, StoreRecord, UomRecord, UomType, WarehouseRecord, StockStatusType } from './inventory.model';

/*
 * Inventory masters from {projectApiUrl}api/master-data/... Each store waits for the masters it
 * links to by id (store -> warehouse, bin -> store, item -> category / unit / bin) so ids can be
 * shown as codes. Fields the API doesn't store yet are kept for the session only.
 */

interface ReferenceDto {
  id: string | null;
  name: string;
}

const ref = (r: { id: string; name: string } | undefined) => (r ? { id: r.id, name: r.name } : null);

interface ItemCategoryDto extends MasterDto {
  categoryName: string;
  parentCategory: ReferenceDto | null;
  description: string | null;
}

/** Item categories; default unit, valuation and the two settings are UI-only for now. */
@Injectable({ providedIn: 'root' })
export class ItemCategoryService extends ApiCodeStore<ItemCategoryRecord, ItemCategoryDto> {
  constructor() {
    super('item-categories');
    this.load();
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

  /** Saves; sub-categories follow a code change. */
  override save(record: ItemCategoryRecord, originalCode: string | null = null): Observable<ItemCategoryRecord> {
    return super.save(record, originalCode).pipe(
      tap((saved) => {
        if (originalCode && originalCode !== saved.code) {
          this.records = this.records.map((r) => (r.parentCode === originalCode ? { ...r, parentCode: saved.code } : r));
        }
      })
    );
  }

  protected toRecord(dto: ItemCategoryDto, cached: ItemCategoryRecord | undefined): ItemCategoryRecord {
    const parentId = dto.parentCategory?.id;
    return {
      ...(cached ?? blankItemCategory()),
      name: dto.categoryName,
      parentCode: parentId ? (this.records.find((r) => r.id === parentId)?.code ?? null) : null,
      description: dto.description ?? ''
    };
  }

  protected toRequest(r: ItemCategoryRecord) {
    return { categoryName: r.name, code: r.code, parentCategory: ref(this.find(r.parentCode)), description: r.description };
  }
}

interface UomDto extends MasterDto {
  uomName: string;
  type: string;
  symbol: string;
  conversionFactor: number | null;
  baseUom: string | null;
  description: string | null;
}

/** Units of measure; decimal settings are UI-only for now. */
@Injectable({ providedIn: 'root' })
export class UomService extends ApiCodeStore<UomRecord, UomDto> {
  constructor() {
    super('uoms');
    this.load();
  }

  label(code: string): string {
    return this.find(code)?.symbol ?? code;
  }

  /** Units based on `code`. */
  derived(code: string): UomRecord[] {
    return this.records.filter((r) => r.baseUom === code);
  }

  /** Saves; units based on this one follow a code change. */
  override save(record: UomRecord, originalCode: string | null = null): Observable<UomRecord> {
    return super.save(record, originalCode).pipe(
      tap((saved) => {
        if (originalCode && originalCode !== saved.code) {
          this.records = this.records.map((r) => (r.baseUom === originalCode ? { ...r, baseUom: saved.code } : r));
        }
      })
    );
  }

  protected toRecord(dto: UomDto, cached: UomRecord | undefined): UomRecord {
    return {
      ...(cached ?? blankUom()),
      name: dto.uomName,
      type: dto.type as UomType,
      symbol: dto.symbol,
      conversionFactor: dto.conversionFactor ?? 1,
      baseUom: dto.baseUom ?? '',
      description: dto.description ?? ''
    };
  }

  protected toRequest(r: UomRecord) {
    return { uomName: r.name, code: r.code, type: r.type, symbol: r.symbol, conversionFactor: r.conversionFactor, baseUom: r.baseUom, description: r.description };
  }
}

interface StockStatusDto extends MasterDto {
  statusName: string;
  type: string;
  color: string | null;
  description: string | null;
  usage: string | null;
  systemDefault: boolean;
}

/** Stock statuses; the usage rules and settings switches are UI-only for now. */
@Injectable({ providedIn: 'root' })
export class StockStatusService extends ApiCodeStore<StockStatusRecord, StockStatusDto> {
  constructor() {
    super('stock-statuses');
    this.load();
  }

  /** Only one status can be the system default; the API clears the others, and so does the cache. */
  override save(record: StockStatusRecord, originalCode: string | null = null): Observable<StockStatusRecord> {
    return super.save(record, originalCode).pipe(
      tap((saved) => {
        if (saved.isDefault) this.records = this.records.map((r) => (r.id === saved.id ? r : { ...r, isDefault: false }));
      })
    );
  }

  protected toRecord(dto: StockStatusDto, cached: StockStatusRecord | undefined): StockStatusRecord {
    const base = cached ?? blankStockStatus();
    return {
      ...base,
      name: dto.statusName,
      type: dto.type as StockStatusType,
      color: dto.color?.toLowerCase() ?? base.color,
      description: dto.description ?? '',
      usage: dto.usage ?? '',
      isDefault: dto.systemDefault
    };
  }

  protected toRequest(r: StockStatusRecord) {
    return { statusName: r.name, code: r.code, type: r.type, color: r.color, description: r.description, usage: r.usage, systemDefault: r.isDefault };
  }
}

interface WarehouseDto extends MasterDto {
  warehouseName: string;
  location: string | null;
  manager: ReferenceDto | null;
  description: string | null;
}

/** Warehouses; the manager is an employee, kept as their employee number. Address and contact are UI-only. */
@Injectable({ providedIn: 'root' })
export class WarehouseService extends ApiCodeStore<WarehouseRecord, WarehouseDto> {
  private readonly employees = inject(EmployeeService);

  constructor() {
    super('warehouses');
    this.load();
  }

  protected override waitFor() {
    return [this.employees.settled$];
  }

  protected toRecord(dto: WarehouseDto, cached: WarehouseRecord | undefined): WarehouseRecord {
    const managerId = dto.manager?.id;
    return {
      ...(cached ?? blankWarehouse()),
      name: dto.warehouseName,
      location: dto.location ?? '',
      managerId: managerId ? (this.employees.all().find((e) => e.recordId === managerId)?.id ?? '') : '',
      description: dto.description ?? ''
    };
  }

  protected toRequest(r: WarehouseRecord) {
    const manager = this.employees.find(r.managerId);
    return {
      warehouseName: r.name,
      code: r.code,
      location: r.location,
      manager: manager ? { id: manager.recordId, name: manager.name } : null,
      description: r.description
    };
  }
}

interface StoreDto extends MasterDto {
  storeName: string;
  warehouse: ReferenceDto | null;
  description: string | null;
}

/** Stores; type, keeper and the two settings are UI-only for now. */
@Injectable({ providedIn: 'root' })
export class StoreService extends ApiCodeStore<StoreRecord, StoreDto> {
  private readonly warehouses = inject(WarehouseService);

  constructor() {
    super('stores');
    this.load();
  }

  protected override waitFor() {
    return [this.warehouses.settled$];
  }

  ofWarehouse(code: string): StoreRecord[] {
    return this.records.filter((r) => r.warehouseCode === code);
  }

  moveWarehouse(from: string, to: string): void {
    this.records = this.records.map((r) => (r.warehouseCode === from ? { ...r, warehouseCode: to } : r));
  }

  protected toRecord(dto: StoreDto, cached: StoreRecord | undefined): StoreRecord {
    const warehouseId = dto.warehouse?.id;
    return {
      ...(cached ?? blankStore()),
      name: dto.storeName,
      warehouseCode: this.warehouses.all().find((w) => w.id === warehouseId)?.code ?? '',
      description: dto.description ?? ''
    };
  }

  protected toRequest(r: StoreRecord) {
    return { storeName: r.name, code: r.code, warehouse: ref(this.warehouses.find(r.warehouseCode)), description: r.description };
  }
}

interface BinDto extends MasterDto {
  binName: string;
  store: ReferenceDto | null;
  warehouse: ReferenceDto | null;
  description: string | null;
}

/** Bins; the page has no bin name, so the code is sent as the name. Rack, shelf, type and capacity are UI-only. */
@Injectable({ providedIn: 'root' })
export class BinService extends ApiCodeStore<BinRecord, BinDto> {
  private readonly stores = inject(StoreService);

  constructor() {
    super('bins');
    this.load();
  }

  protected override waitFor() {
    return [this.stores.settled$];
  }

  ofStore(code: string): BinRecord[] {
    return this.records.filter((r) => r.storeCode === code);
  }

  moveStore(from: string, to: string): void {
    this.records = this.records.map((r) => (r.storeCode === from ? { ...r, storeCode: to } : r));
  }

  protected toRecord(dto: BinDto, cached: BinRecord | undefined): BinRecord {
    const storeId = dto.store?.id;
    return {
      ...(cached ?? blankBin()),
      storeCode: this.stores.all().find((s) => s.id === storeId)?.code ?? '',
      description: dto.description ?? ''
    };
  }

  protected toRequest(r: BinRecord) {
    const store = this.stores.find(r.storeCode);
    return { binName: r.code, code: r.code, store: store ? { id: store.id, name: store.name } : null, description: r.description };
  }
}

export type StockState = 'In Stock' | 'Low Stock' | 'Reorder' | 'Out of Stock';

interface ItemDto extends MasterDto {
  itemName: string;
  itemCode: string;
  partNumber: string | null;
  category: ReferenceDto | null;
  uom: ReferenceDto | null;
  description: string | null;
  imageUrl: string | null;
  currentStock: number | null;
  reorderLevel: number | null;
  minStock: number | null;
  bin: ReferenceDto | null;
}

/**
 * Items. Category, unit and bin are kept as codes and linked by id; the warehouse and store come
 * from the bin. Reorder quantity, maximum stock, financial and additional details and settings are
 * UI-only for now.
 */
@Injectable({ providedIn: 'root' })
export class ItemService extends ApiCodeStore<ItemRecord, ItemDto> {
  private readonly categories = inject(ItemCategoryService);
  private readonly uoms = inject(UomService);
  private readonly bins = inject(BinService);
  private readonly stores = inject(StoreService);

  constructor() {
    super('items');
    this.load();
  }

  protected override waitFor() {
    return [this.categories.settled$, this.uoms.settled$, this.bins.settled$];
  }

  protected override codeOf(dto: ItemDto): string {
    return dto.itemCode;
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

  /** Items take their warehouse and store from their bin; keeps cached rows in step when one moves. */
  followLocation(match: (item: ItemRecord) => boolean, location: Partial<Pick<ItemRecord, 'warehouseCode' | 'storeCode'>>): void {
    this.records = this.records.map((r) => (match(r) ? { ...r, ...location } : r));
  }

  /** Carries a renamed category, unit, warehouse, store or bin code into the items. */
  recode(field: 'category' | 'uom' | 'warehouseCode' | 'storeCode' | 'binCode', from: string, to: string): void {
    if (from === to) return;
    this.records = this.records.map((r) => (r[field] === from ? { ...r, [field]: to } : r));
  }

  protected toRecord(dto: ItemDto, cached: ItemRecord | undefined): ItemRecord {
    const base = cached ?? blankItem();
    const bin = this.bins.all().find((b) => b.id === dto.bin?.id);
    const store = this.stores.find(bin?.storeCode);
    return {
      ...base,
      name: dto.itemName,
      partNumber: dto.partNumber ?? '',
      category: this.categories.all().find((c) => c.id === dto.category?.id)?.code ?? '',
      uom: this.uoms.all().find((u) => u.id === dto.uom?.id)?.code ?? '',
      description: dto.description ?? '',
      // TODO: upload through the Media service and keep its URL; the picked image is sent as a data URL
      image: dto.imageUrl,
      currentStock: dto.currentStock ?? 0,
      reorderLevel: dto.reorderLevel ?? 0,
      minStock: dto.minStock ?? 0,
      binCode: bin?.code ?? '',
      storeCode: store?.code ?? (bin ? '' : base.storeCode),
      warehouseCode: store?.warehouseCode ?? (bin ? '' : base.warehouseCode)
    };
  }

  protected toRequest(r: ItemRecord) {
    const bin = this.bins.find(r.binCode);
    return {
      itemName: r.name,
      itemCode: r.code,
      partNumber: r.partNumber,
      category: ref(this.categories.find(r.category)),
      uom: ref(this.uoms.find(r.uom)),
      description: r.description,
      imageUrl: r.image,
      currentStock: r.currentStock,
      reorderLevel: r.reorderLevel,
      minStock: r.minStock,
      bin: bin ? { id: bin.id, name: bin.code } : null
    };
  }
}
