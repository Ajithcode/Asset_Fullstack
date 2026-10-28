import { Component, Input, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight, Ellipsis, LucideAngularModule, Search } from 'lucide-angular';
import { ListPage } from '../../../shared/list-page';
import { BIN_TYPES, blankBin } from '../../inventory.data';
import { BinRecord, RecordStatus } from '../../inventory.model';
import { BinService, ItemService, StoreService, WarehouseService } from '../../inventory.service';
import { BinForm } from '../bin-form/bin-form';

type SortKey = 'code' | 'store' | 'binType' | 'items';
interface Filters {
  search: string;
  store: string;
  binType: string;
  status: '' | RecordStatus;
}

/** Bin tab of Warehouse / Store / Bin. */
@Component({
  selector: 'app-bin-list',
  imports: [FormsModule, LucideAngularModule, BinForm],
  templateUrl: './bin-list.html',
  styleUrls: ['../../../shared/styles/mdm-page.css', '../../inventory.css']
})
export class BinList extends ListPage<BinRecord, Filters, SortKey> implements OnInit {
  // Opens the tab filtered to one store (from its bin count)
  @Input() storeFilter = '';

  readonly service = inject(BinService);
  readonly stores = inject(StoreService);
  private readonly warehouses = inject(WarehouseService);
  private readonly items = inject(ItemService);
  readonly binTypes = BIN_TYPES;
  readonly icons = { Search, Ellipsis, ArrowUp, ArrowDown, ChevronLeft, ChevronRight };

  constructor() {
    super({ search: '', store: '', binType: '', status: '' });
  }

  ngOnInit(): void {
    if (this.storeFilter) {
      this.draft.store = this.storeFilter;
      this.apply();
    }
  }

  keyOf(r: BinRecord): string {
    return r.code;
  }

  nameOf(r: BinRecord): string {
    return `Bin ${r.code}`;
  }

  protected all(): BinRecord[] {
    return this.service.all();
  }

  protected matches(r: BinRecord, f: Filters): boolean {
    const term = f.search.trim().toLowerCase();
    return (
      (!f.store || r.storeCode === f.store) &&
      (!f.binType || r.binType === f.binType) &&
      (!f.status || r.status === f.status) &&
      (!term || [r.code, r.description, this.storeName(r)].some((v) => v.toLowerCase().includes(term)))
    );
  }

  protected sortValue(r: BinRecord, key: SortKey): string | number {
    return key === 'items' ? this.itemCount(r) : key === 'store' ? this.storeName(r) : r[key];
  }

  protected removeRecords(codes: string[]): void {
    this.service.remove(codes);
  }

  protected override deleteBlocker(r: BinRecord): string | null {
    const items = this.itemCount(r);
    return items ? `Bin ${r.code} holds ${items} item${items === 1 ? '' : 's'}. Move them first.` : null;
  }

  // New bins go into the store the list is filtered to
  addNew(): void {
    this.openAdd(blankBin(this.applied.store));
  }

  storeName(r: BinRecord): string {
    return this.stores.find(r.storeCode)?.name ?? r.storeCode;
  }

  warehouseName(r: BinRecord): string {
    const store = this.stores.find(r.storeCode);
    return store ? (this.warehouses.find(store.warehouseCode)?.name ?? store.warehouseCode) : '';
  }

  itemCount(r: BinRecord): number {
    return this.items.inBin(r.code).length;
  }

  onSaved(event: { record: BinRecord; originalCode: string | null }): void {
    this.service.save(event.record, event.originalCode);
    if (event.originalCode) this.items.recode('binCode', event.originalCode, event.record.code);
    // Items in the bin follow it to another store
    const store = this.stores.find(event.record.storeCode);
    for (const item of this.items.inBin(event.record.code)) {
      if (store && item.storeCode !== store.code) this.items.save({ ...item, storeCode: store.code, warehouseCode: store.warehouseCode }, item.code);
    }
    this.carrySelection(event.originalCode, event.record.code);
    this.notify(`Bin ${event.record.code} ${event.originalCode ? 'updated' : 'added'}.`);
    this.closeForm();
  }

  toggleStatus(r: BinRecord): void {
    this.menuFor = null;
    const status = r.status === 'Active' ? 'Inactive' : 'Active';
    this.service.save({ ...r, status }, r.code);
    this.notify(`Bin ${r.code} marked ${status}.`);
  }
}
