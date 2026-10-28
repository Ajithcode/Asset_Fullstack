import { Component, ElementRef, HostListener, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ArrowDown, ArrowUp, Box, ChevronLeft, ChevronRight, Ellipsis, Factory, LucideAngularModule, Plus, Search } from 'lucide-angular';
import { MASTER_DATA_BASE_PATH } from 'shared-ui';
import { AssetModelService } from '../asset-model/asset-model.service';
import { badgeInk, badgeText } from './manufacturer.data';
import { ManufacturerFilters, ManufacturerRecord } from './manufacturer.model';
import { ManufacturerService } from './manufacturer.service';
import { MfrDetail } from './mfr-detail/mfr-detail';
import { MfrForm, MfrFormTab } from './mfr-form/mfr-form';

type SortKey = 'name' | 'code' | 'country' | 'models';

const EMPTY_FILTERS: ManufacturerFilters = { search: '', status: '', assetType: '' };

/** Master Data Management > Asset > Manufacturer */
@Component({
  selector: 'app-mdm-asset-manufacturer',
  imports: [FormsModule, RouterLink, LucideAngularModule, MfrForm, MfrDetail],
  templateUrl: './manufacturer.html',
  styleUrls: ['../../shared/styles/mdm-page.css', './manufacturer.css']
})
export class ManufacturerPage {
  readonly service = inject(ManufacturerService);
  readonly models = inject(AssetModelService);
  readonly basePath = MASTER_DATA_BASE_PATH;
  readonly badgeText = badgeText;
  readonly badgeInk = badgeInk;
  readonly icons = { Factory, Box, Search, Plus, Ellipsis, ArrowUp, ArrowDown, ChevronLeft, ChevronRight };
  readonly pageSizes = [10, 20, 50, 100];

  @ViewChild('detailEl') detailEl?: ElementRef<HTMLElement>;
  @ViewChild('formEl', { read: ElementRef }) formEl?: ElementRef<HTMLElement>;

  draft: ManufacturerFilters = { ...EMPTY_FILTERS };
  applied: ManufacturerFilters = { ...EMPTY_FILTERS };

  sortKey: SortKey | null = null;
  sortDir: 1 | -1 = 1;
  page = 0;
  pageSize = 10;
  selected = new Set<string>();
  menuFor: string | null = null;

  selectedCode: string | null = 'MFG-DELL';
  formOpen = false;
  formMode: 'add' | 'edit' = 'add';
  formRecord: ManufacturerRecord | null = null;
  formTab: MfrFormTab = 'basic';

  pendingDelete: ManufacturerRecord[] = [];
  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  get stats() {
    const all = this.service.all();
    const active = all.filter((r) => r.status === 'Active').length;
    return { total: all.length, active, inactive: all.length - active, models: this.models.all().length };
  }

  get rows(): ManufacturerRecord[] {
    const type = this.applied.assetType;
    const rows = this.service.filter(this.applied, (name) => this.models.ofManufacturer(name).some((m) => m.assetType === type));
    const key = this.sortKey;
    if (!key) return rows;
    return [...rows].sort((a, b) =>
      this.sortDir * (key === 'models' ? this.modelCount(a) - this.modelCount(b) : a[key].localeCompare(b[key], undefined, { numeric: true }))
    );
  }

  get pageRows(): ManufacturerRecord[] {
    return this.rows.slice(this.page * this.pageSize, (this.page + 1) * this.pageSize);
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.rows.length / this.pageSize));
  }

  get pageNumbers(): number[] {
    const count = this.pageCount;
    const start = Math.max(0, Math.min(this.page - 2, count - 5));
    return Array.from({ length: Math.min(5, count) }, (_, i) => start + i);
  }

  get showingFrom(): number {
    return this.rows.length ? this.page * this.pageSize + 1 : 0;
  }

  get showingTo(): number {
    return Math.min((this.page + 1) * this.pageSize, this.rows.length);
  }

  get allOnPageSelected(): boolean {
    return this.pageRows.length > 0 && this.pageRows.every((r) => this.selected.has(r.code));
  }

  get selectedManufacturer(): ManufacturerRecord | undefined {
    return this.service.find(this.selectedCode);
  }

  modelCount(r: ManufacturerRecord): number {
    return this.models.ofManufacturer(r.name).length;
  }

  apply(): void {
    this.applied = { ...this.draft };
    this.page = 0;
  }

  clear(): void {
    this.draft = { ...EMPTY_FILTERS };
    this.apply();
  }

  sortBy(key: SortKey): void {
    if (this.sortKey !== key) {
      this.sortKey = key;
      this.sortDir = 1;
    } else if (this.sortDir === 1) {
      this.sortDir = -1;
    } else {
      this.sortKey = null;
    }
    this.page = 0;
  }

  goTo(page: number): void {
    this.page = Math.min(Math.max(page, 0), this.pageCount - 1);
  }

  toggleRow(code: string): void {
    if (this.selected.has(code)) {
      this.selected.delete(code);
    } else {
      this.selected.add(code);
    }
  }

  togglePage(): void {
    const select = !this.allOnPageSelected;
    this.pageRows.forEach((r) => (select ? this.selected.add(r.code) : this.selected.delete(r.code)));
  }

  open(code: string, scroll = true): void {
    this.selectedCode = code;
    this.menuFor = null;
    if (scroll) setTimeout(() => this.detailEl?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' }));
  }

  toggleMenu(code: string): void {
    this.menuFor = this.menuFor === code ? null : code;
  }

  @HostListener('document:click')
  closeMenu(): void {
    this.menuFor = null;
  }

  openAdd(prefill: ManufacturerRecord | null = null): void {
    this.menuFor = null;
    this.formMode = 'add';
    this.formRecord = prefill;
    this.formTab = 'basic';
    this.formOpen = true;
    this.revealForm();
  }

  // A copy keeps the contacts and defaults; name and code must be new
  duplicate(m: ManufacturerRecord): void {
    this.openAdd({ ...structuredClone(m), code: '', name: `${m.name} (Copy)`, status: 'Active', documents: 0 });
  }

  openEdit(event: { manufacturer: ManufacturerRecord; tab?: MfrFormTab }): void {
    this.menuFor = null;
    this.formMode = 'edit';
    this.formRecord = event.manufacturer;
    this.formTab = event.tab ?? 'basic';
    this.formOpen = true;
    this.revealForm();
  }

  // The drawer sits at the top of the page; bring it into view when opened from further down
  private revealForm(): void {
    setTimeout(() => this.formEl?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' }));
  }

  closeForm(): void {
    this.formOpen = false;
    this.formRecord = null;
  }

  onSaved(event: { record: ManufacturerRecord; originalCode: string | null; originalName: string | null }): void {
    this.service.save(event.record, event.originalCode);
    // Asset models refer to the manufacturer by name
    if (event.originalName) this.models.renameManufacturer(event.originalName, event.record.name);
    if (event.originalCode && this.selected.delete(event.originalCode)) this.selected.add(event.record.code);
    this.open(event.record.code, false);
    this.notify(`${event.record.name} ${event.originalCode ? 'updated' : 'added'}.`);
    this.closeForm();
  }

  toggleStatus(m: ManufacturerRecord): void {
    this.menuFor = null;
    const status = m.status === 'Active' ? 'Inactive' : 'Active';
    this.service.save({ ...m, status }, m.code);
    this.notify(`${m.name} marked ${status}.`);
  }

  // Manufacturers with asset models can't be deleted; they can be marked inactive instead
  askDelete(list: ManufacturerRecord[]): void {
    this.menuFor = null;
    const blocked = list.find((m) => this.modelCount(m));
    if (blocked) {
      const count = this.modelCount(blocked);
      this.notify(`${blocked.name} has ${count} asset model${count === 1 ? '' : 's'}. Mark it Inactive instead.`);
      return;
    }
    this.pendingDelete = list;
  }

  askDeleteSelected(): void {
    this.askDelete(this.service.all().filter((r) => this.selected.has(r.code)));
  }

  confirmDelete(): void {
    const codes = this.pendingDelete.map((r) => r.code);
    this.service.remove(codes);
    codes.forEach((c) => this.selected.delete(c));
    if (this.selectedCode && codes.includes(this.selectedCode)) this.selectedCode = this.service.all()[0]?.code ?? null;
    if (this.formMode === 'edit' && this.formRecord && codes.includes(this.formRecord.code)) this.closeForm();
    this.notify(codes.length === 1 ? `${this.pendingDelete[0].name} deleted.` : `${codes.length} manufacturers deleted.`);
    this.pendingDelete = [];
    this.goTo(this.page);
  }

  cancelDelete(): void {
    this.pendingDelete = [];
  }

  notify(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 4000);
  }
}
