import { Component, ElementRef, HostListener, ViewChild, inject } from '@angular/core';
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
  Hexagon,
  Layers,
  LucideAngularModule,
  Plus,
  Search
} from 'lucide-angular';
import { concat, toArray } from 'rxjs';
import { MASTER_DATA_BASE_PATH } from 'shared-ui';
import { apiErrorMessage } from '../../shared/api-error';
import { DecimalPipe } from '@angular/common';
import { BfzDetail } from './bfz-detail/bfz-detail';
import { BfzForm } from './bfz-form/bfz-form';
import { ZONE_COLORS, blankBfz } from './bfz.data';
import { BfzFilters, BfzRecord, BfzType } from './bfz.model';
import { BfzService } from './bfz.service';

interface Row {
  record: BfzRecord;
  depth: number;
  hasChildren: boolean;
}

const EMPTY_FILTERS: BfzFilters = { search: '', siteId: '', type: '', status: '' };

/** Master Data Management > Organization > Building / Floor / Zone */
@Component({
  selector: 'app-mdm-organization-building-floor-zone',
  imports: [FormsModule, RouterLink, DecimalPipe, LucideAngularModule, BfzForm, BfzDetail],
  templateUrl: './building-floor-zone.html',
  styleUrls: ['../../shared/styles/mdm-page.css', './building-floor-zone.css']
})
export class BuildingFloorZonePage {
  readonly service = inject(BfzService);
  readonly basePath = MASTER_DATA_BASE_PATH;
  readonly types: BfzType[] = ['Building', 'Floor', 'Zone'];
  readonly icons = { Building2, Layers, Hexagon, Search, Plus, Ellipsis, ArrowUp, ArrowDown, ChevronLeft, ChevronRight, ChevronDown };
  readonly pageSizes = [10, 20, 50, 100];

  @ViewChild('detailEl') detailEl?: ElementRef<HTMLElement>;
  @ViewChild('formEl', { read: ElementRef }) formEl?: ElementRef<HTMLElement>;

  draft: BfzFilters = { ...EMPTY_FILTERS };
  applied: BfzFilters = { ...EMPTY_FILTERS };

  sortName: 1 | -1 | null = null;
  page = 0;
  pageSize = 10;
  selected = new Set<string>();
  // Buildings and floors opened in the tree
  expanded = new Set<string>();
  menuFor: string | null = null;
  addMenuOpen = false;

  selectedCode: string | null = null;
  formOpen = false;
  formMode: 'add' | 'edit' = 'add';
  formRecord: BfzRecord | null = null;

  pendingDelete: BfzRecord[] = [];
  toast = '';
  saving = false;
  private toastTimer?: ReturnType<typeof setTimeout>;

  constructor() {
    this.service.load();
  }

  get stats() {
    return this.service.stats();
  }

  // The tree is shown unless a search, type or status filter flattens the list
  get treeMode(): boolean {
    return !this.applied.search.trim() && !this.applied.type && !this.applied.status;
  }

  get rows(): Row[] {
    const byName = (a: BfzRecord, b: BfzRecord) => (this.sortName ? this.sortName * a.name.localeCompare(b.name, undefined, { numeric: true }) : 0);
    if (!this.treeMode) {
      const flat = this.service.filter(this.applied);
      return (this.sortName ? [...flat].sort(byName) : flat).map((record) => ({ record, depth: 0, hasChildren: false }));
    }
    const rows: Row[] = [];
    const add = (record: BfzRecord, depth: number) => {
      const children = this.service.children(record);
      rows.push({ record, depth, hasChildren: children.length > 0 });
      if (this.expanded.has(record.code)) {
        (this.sortName ? [...children].sort(byName) : children).forEach((c) => add(c, depth + 1));
      }
    };
    const buildings = this.service.buildings(this.applied.siteId);
    (this.sortName ? [...buildings].sort(byName) : buildings).forEach((b) => add(b, 0));
    return rows;
  }

  get pageRows(): Row[] {
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
    return this.pageRows.length > 0 && this.pageRows.every((r) => this.selected.has(r.record.code));
  }

  get selectedRecord(): BfzRecord | undefined {
    return this.service.find(this.selectedCode) ?? this.service.all()[0];
  }

  parentLabel(r: BfzRecord): string {
    const parent = this.service.parent(r);
    return parent ? parent.name : '-';
  }

  iconFor(r: BfzRecord) {
    return r.type === 'Building' ? this.icons.Building2 : r.type === 'Floor' ? this.icons.Layers : this.icons.Hexagon;
  }

  // ---- filters, sort, paging ----

  apply(): void {
    this.applied = { ...this.draft };
    this.page = 0;
  }

  clear(): void {
    this.draft = { ...EMPTY_FILTERS };
    this.apply();
  }

  sortByName(): void {
    this.sortName = this.sortName === null ? 1 : this.sortName === 1 ? -1 : null;
  }

  goTo(page: number): void {
    this.page = Math.min(Math.max(page, 0), this.pageCount - 1);
  }

  toggleExpand(code: string): void {
    if (this.expanded.has(code)) {
      this.expanded.delete(code);
    } else {
      this.expanded.add(code);
    }
    this.goTo(this.page);
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
    this.pageRows.forEach((r) => (select ? this.selected.add(r.record.code) : this.selected.delete(r.record.code)));
  }

  open(code: string, scroll = true): void {
    this.selectedCode = code;
    this.menuFor = null;
    // Make sure the record is visible in the tree
    const r = this.service.find(code);
    if (r?.buildingCode) this.expanded.add(r.buildingCode);
    if (r?.floorCode) this.expanded.add(r.floorCode);
    if (scroll) setTimeout(() => this.detailEl?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' }));
  }

  toggleMenu(code: string): void {
    this.menuFor = this.menuFor === code ? null : code;
  }

  @HostListener('document:click')
  closeMenus(): void {
    this.menuFor = null;
    this.addMenuOpen = false;
  }

  // ---- add / edit ----

  openAdd(type: BfzType = 'Building', parent: BfzRecord | null = null): void {
    this.menuFor = null;
    this.addMenuOpen = false;
    const prefill = blankBfz();
    prefill.type = type;
    if (type === 'Zone') prefill.color = ZONE_COLORS[0];
    if (parent) {
      prefill.siteId = parent.siteId;
      prefill.buildingCode = parent.type === 'Building' ? parent.code : parent.buildingCode;
      prefill.floorCode = parent.type === 'Floor' ? parent.code : null;
      if (type === 'Floor') {
        const floors = this.service.floorsOf(prefill.buildingCode);
        prefill.levelNumber = floors.length ? Math.max(...floors.map((f) => f.levelNumber ?? 0)) + 1 : 0;
      }
    }
    this.formMode = 'add';
    this.formRecord = prefill;
    this.formOpen = true;
    this.revealForm();
  }

  addChild(parent: BfzRecord): void {
    this.openAdd(parent.type === 'Building' ? 'Floor' : 'Zone', parent);
  }

  openEdit(record: BfzRecord): void {
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

  onSaved(event: { record: BfzRecord; originalCode: string | null }): void {
    if (this.saving) return;
    this.saving = true;
    this.service.save(event.record, event.originalCode).subscribe({
      next: (saved) => {
        this.saving = false;
        if (event.originalCode && this.expanded.delete(event.originalCode)) this.expanded.add(saved.code);
        if (event.originalCode && this.selected.delete(event.originalCode)) this.selected.add(saved.code);
        this.open(saved.code, false);
        this.notify(`${saved.type} ${saved.name} ${event.originalCode ? 'updated' : 'added'}.`);
        this.closeForm();
      },
      error: (err) => {
        this.saving = false;
        this.notify(apiErrorMessage(err));
      }
    });
  }

  toggleStatus(record: BfzRecord): void {
    this.menuFor = null;
    const status = record.status === 'Active' ? 'Inactive' : 'Active';
    this.service.save({ ...record, status }, record.code).subscribe({
      next: () => this.notify(`${record.name} marked ${status}.`),
      error: (err) => this.notify(apiErrorMessage(err))
    });
  }

  onAddImage(event: { building: BfzRecord; image: string }): void {
    // DEMO MODE: images aren't stored by the API yet, so this only lasts until the page reloads
    this.service.save({ ...event.building, images: [...event.building.images, event.image] }, event.building.code).subscribe({
      next: () => this.notify('Image added.'),
      error: (err) => this.notify(apiErrorMessage(err))
    });
  }

  // ---- delete ----

  // Buildings with floors and floors with zones must be emptied first
  askDelete(records: BfzRecord[]): void {
    this.menuFor = null;
    const codes = new Set(records.map((r) => r.code));
    const blocked = records.find((r) => this.service.children(r).some((c) => !codes.has(c.code)));
    if (blocked) {
      const kids = this.service.children(blocked);
      const noun = blocked.type === 'Building' ? 'floor' : 'zone';
      this.notify(`${blocked.name} has ${kids.length} ${noun}${kids.length === 1 ? '' : 's'}. Delete ${kids.length === 1 ? 'it' : 'them'} first.`);
      return;
    }
    this.pendingDelete = records;
  }

  askDeleteSelected(): void {
    this.askDelete(this.service.all().filter((r) => this.selected.has(r.code)));
  }

  confirmDelete(): void {
    const list = this.pendingDelete;
    const codes = list.map((r) => r.code);
    const fallback = list[0] && this.service.parent(list[0])?.code;
    this.pendingDelete = [];
    // Zones, then floors, then buildings, so the API doesn't refuse a parent emptied in this batch
    const rank: Record<BfzType, number> = { Zone: 0, Floor: 1, Building: 2 };
    const ordered = [...list].sort((a, b) => rank[a.type] - rank[b.type]);
    concat(...ordered.map((r) => this.service.remove(r))).pipe(toArray()).subscribe({
      next: () => {
        codes.forEach((c) => {
          this.selected.delete(c);
          this.expanded.delete(c);
        });
        if (this.selectedCode && codes.includes(this.selectedCode)) {
          this.selectedCode = fallback && !codes.includes(fallback) ? fallback : (this.service.buildings()[0]?.code ?? null);
        }
        if (this.formMode === 'edit' && this.formRecord && codes.includes(this.formRecord.code)) this.closeForm();
        this.notify(codes.length === 1 ? `${list[0].name} deleted.` : `${codes.length} records deleted.`);
        this.goTo(this.page);
      },
      // Any records deleted before the failure are already gone from the table
      error: (err) => this.notify(apiErrorMessage(err))
    });
  }

  cancelDelete(): void {
    this.pendingDelete = [];
  }

  notify(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 3500);
  }
}
