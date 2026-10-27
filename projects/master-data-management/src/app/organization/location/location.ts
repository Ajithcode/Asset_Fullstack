import { Component, ElementRef, HostListener, ViewChild, inject } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  ArrowDown,
  ArrowUp,
  Building2,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  Ellipsis,
  LucideAngularModule,
  MapPin,
  Plus,
  Search,
  TreePine
} from 'lucide-angular';
import { MASTER_DATA_BASE_PATH } from 'shared-ui';
import { LocationDetail } from './location-detail/location-detail';
import { LocationForm } from './location-form/location-form';
import { iconForType } from './location.icons';
import { LocationFilters, LocationRecord } from './location.model';
import { LocationService } from './location.service';

type SortKey = 'name' | 'code';

const EMPTY_FILTERS: LocationFilters = { search: '', siteId: '', buildingCode: '', floorCode: '', zoneCode: '' };

/** Master Data Management > Organization > Location */
@Component({
  selector: 'app-mdm-organization-location',
  imports: [FormsModule, RouterLink, DecimalPipe, LucideAngularModule, LocationForm, LocationDetail],
  templateUrl: './location.html',
  styleUrls: ['../../shared/styles/mdm-page.css', './location.css']
})
export class LocationPage {
  readonly service = inject(LocationService);
  readonly basePath = MASTER_DATA_BASE_PATH;
  readonly iconForType = iconForType;
  readonly icons = { MapPin, Building2, TreePine, Search, Plus, Ellipsis, ArrowUp, ArrowDown, ChevronLeft, ChevronRight, ChevronDown };
  readonly pageSizes = [10, 20, 50, 100];

  @ViewChild('detailEl') detailEl?: ElementRef<HTMLElement>;
  @ViewChild('formEl', { read: ElementRef }) formEl?: ElementRef<HTMLElement>;

  // Filters being edited in the bar; `applied` is what the table shows (Apply / Enter)
  draft: LocationFilters = { ...EMPTY_FILTERS };
  applied: LocationFilters = { ...EMPTY_FILTERS };

  sortKey: SortKey | null = null;
  sortDir: 1 | -1 = 1;
  page = 0;
  pageSize = 10;
  selected = new Set<string>();
  menuFor: string | null = null;

  selectedCode: string | null = 'LOC-001';
  // Drawer: closed, or open to add (optionally pre-filled, e.g. a duplicate) or edit
  formOpen = false;
  formMode: 'add' | 'edit' = 'add';
  formRecord: LocationRecord | null = null;

  pendingDelete: LocationRecord[] = [];
  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  get stats() {
    return this.service.stats();
  }

  get rows(): LocationRecord[] {
    const rows = this.service.filter(this.applied);
    if (!this.sortKey) return rows;
    const key = this.sortKey;
    return [...rows].sort((a, b) => this.sortDir * a[key].localeCompare(b[key], undefined, { numeric: true }));
  }

  get pageRows(): LocationRecord[] {
    return this.rows.slice(this.page * this.pageSize, (this.page + 1) * this.pageSize);
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.rows.length / this.pageSize));
  }

  // Up to five page buttons around the current page
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

  get selectedLocation(): LocationRecord | undefined {
    return this.service.find(this.selectedCode);
  }

  // ---- filters ----

  onDraftSiteChange(): void {
    this.draft.buildingCode = '';
    this.onDraftBuildingChange();
  }

  onDraftBuildingChange(): void {
    this.draft.floorCode = '';
    this.draft.zoneCode = '';
  }

  onDraftFloorChange(): void {
    this.draft.zoneCode = '';
  }

  apply(): void {
    this.applied = { ...this.draft };
    this.page = 0;
  }

  clear(): void {
    this.draft = { ...EMPTY_FILTERS };
    this.apply();
  }

  // ---- table ----

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

  onPageSizeChange(): void {
    this.page = 0;
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
    if (scroll) {
      setTimeout(() => this.detailEl?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' }));
    }
  }

  toggleMenu(code: string): void {
    this.menuFor = this.menuFor === code ? null : code;
  }

  @HostListener('document:click')
  closeMenu(): void {
    this.menuFor = null;
  }

  // ---- add / edit ----

  openAdd(prefill: LocationRecord | null = null): void {
    this.menuFor = null;
    this.formMode = 'add';
    this.formRecord = prefill;
    this.formOpen = true;
    this.revealForm();
  }

  openEdit(record: LocationRecord): void {
    this.menuFor = null;
    this.formMode = 'edit';
    this.formRecord = record;
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

  onSaved(event: { record: LocationRecord; originalCode: string | null }): void {
    this.service.save(event.record, event.originalCode);
    if (event.originalCode && this.selected.delete(event.originalCode)) {
      this.selected.add(event.record.code);
    }
    this.selectedCode = event.record.code;
    this.notify(event.originalCode ? `${event.record.name} updated.` : `${event.record.name} added.`);
    this.closeForm();
  }

  // Opens the Add drawer pre-filled with a copy; nothing is stored until it is saved
  duplicate(record: LocationRecord): void {
    let n = 1;
    while (this.service.isCodeTaken(`${record.code}-C${n}`, null)) n++;
    this.openAdd({ ...record, code: `${record.code}-C${n}`, name: `${record.name} (Copy)` });
  }

  toggleStatus(record: LocationRecord): void {
    const status = record.status === 'Active' ? 'Inactive' : 'Active';
    this.service.save({ ...record, status }, record.code);
    this.notify(`${record.name} marked ${status}.`);
  }

  // ---- delete ----

  askDelete(records: LocationRecord[]): void {
    this.menuFor = null;
    this.pendingDelete = records;
  }

  askDeleteSelected(): void {
    this.askDelete(this.service.all().filter((r) => this.selected.has(r.code)));
  }

  confirmDelete(): void {
    const codes = this.pendingDelete.map((r) => r.code);
    this.service.remove(codes);
    codes.forEach((c) => this.selected.delete(c));
    if (this.selectedCode && codes.includes(this.selectedCode)) {
      this.selectedCode = this.pageRows.find((r) => !codes.includes(r.code))?.code ?? null;
    }
    if (this.formMode === 'edit' && this.formRecord && codes.includes(this.formRecord.code)) this.closeForm();
    this.notify(codes.length === 1 ? `${this.pendingDelete[0].name} deleted.` : `${codes.length} locations deleted.`);
    this.pendingDelete = [];
    this.goTo(this.page);
  }

  cancelDelete(): void {
    this.pendingDelete = [];
  }

  private notify(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 3000);
  }
}
