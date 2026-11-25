import { Component, ElementRef, HostListener, OnInit, ViewChild, inject } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight, Ellipsis, GitBranch, Layers, LucideAngularModule, Monitor, Plus, Search } from 'lucide-angular';
import { MASTER_DATA_BASE_PATH, pushToast } from 'shared-ui';
import { apiErrorMessage } from '../../shared/mdm-api';
import { CategoryService } from '../category-subcategory/category.service';
import { forkJoin } from 'rxjs';
import { MODEL_ICONS } from '../asset-model/asset-model.icons';
import { AssetSubTypeFilters, AssetSubTypeRecord } from './asset-sub-type.model';
import { AssetSubTypeService } from './asset-sub-type.service';
import { SubTypeDetail } from './sub-type-detail/sub-type-detail';
import { SubTypeForm, SubTypeFormTab } from './sub-type-form/sub-type-form';

type SortKey = 'name' | 'code' | 'assetType' | 'lifecycleYears';

const EMPTY_FILTERS: AssetSubTypeFilters = { search: '', assetType: '', status: '' };

/** Master Data Management > Asset > Asset Sub Type (`/api/master-data/asset-sub-types`) */
@Component({
  selector: 'app-mdm-asset-asset-sub-type',
  imports: [DecimalPipe, FormsModule, RouterLink, LucideAngularModule, SubTypeForm, SubTypeDetail],
  templateUrl: './asset-sub-type.html',
  styleUrls: ['../../shared/styles/mdm-page.css', '../asset-model/asset-model.css', '../../shared/styles/mdm-purple-theme.css']
})
export class AssetSubTypePage implements OnInit {
  readonly service = inject(AssetSubTypeService);
  private readonly categoryStore = inject(CategoryService);
  readonly basePath = MASTER_DATA_BASE_PATH;
  readonly typeIcons = MODEL_ICONS;
  readonly icons = { GitBranch, Monitor, Layers, Search, Plus, Ellipsis, ArrowUp, ArrowDown, ChevronLeft, ChevronRight };
  readonly pageSizes = [10, 20, 50, 100];

  @ViewChild('detailEl') detailEl?: ElementRef<HTMLElement>;
  @ViewChild('formEl', { read: ElementRef }) formEl?: ElementRef<HTMLElement>;

  draft: AssetSubTypeFilters = { ...EMPTY_FILTERS };
  applied: AssetSubTypeFilters = { ...EMPTY_FILTERS };

  sortKey: SortKey | null = null;
  sortDir: 1 | -1 = 1;
  page = 0;
  pageSize = 10;
  selected = new Set<string>();
  menuFor: string | null = null;

  selectedCode: string | null = null;
  saving = false;
  deleting = false;
  formOpen = false;
  formMode: 'add' | 'edit' = 'add';
  formRecord: AssetSubTypeRecord | null = null;
  formTab: SubTypeFormTab = 'basic';

  pendingDelete: AssetSubTypeRecord[] = [];
  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  ngOnInit(): void {
    // Parent types first, so each sub type shows its category
    forkJoin([this.categoryStore.reload(), this.service.loadTypes()]).subscribe({
      complete: () => this.loadSubTypes(),
      error: () => this.loadSubTypes()
    });
  }

  private loadSubTypes(): void {
    this.service.reload().subscribe({
      next: (list) => {
        if (!this.service.find(this.selectedCode)) this.selectedCode = list[0]?.code ?? null;
      },
      error: (err) => this.notify(`Could not load asset sub types: ${apiErrorMessage(err)}`)
    });
  }

  get stats() {
    const all = this.service.all();
    const active = all.filter((r) => r.status === 'Active').length;
    return { total: all.length, active, inactive: all.length - active, parents: new Set(all.map((r) => r.assetType)).size };
  }

  get rows(): AssetSubTypeRecord[] {
    const rows = this.service.filter(this.applied);
    const key = this.sortKey;
    if (!key) return rows;
    return [...rows].sort((a, b) =>
      this.sortDir * (key === 'lifecycleYears' ? a.lifecycleYears - b.lifecycleYears : a[key].localeCompare(b[key], undefined, { numeric: true }))
    );
  }

  get pageRows(): AssetSubTypeRecord[] {
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

  get selectedSubType(): AssetSubTypeRecord | undefined {
    return this.service.find(this.selectedCode);
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
    if (this.selected.has(code)) this.selected.delete(code);
    else this.selected.add(code);
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

  openAdd(prefill: AssetSubTypeRecord | null = null): void {
    this.menuFor = null;
    this.formMode = 'add';
    this.formRecord = prefill;
    this.formTab = 'basic';
    this.formOpen = true;
    this.revealForm();
  }

  duplicate(t: AssetSubTypeRecord): void {
    this.openAdd({ ...structuredClone(t), code: '', name: `${t.name} (Copy)`, status: 'Active', assetCount: 0 });
  }

  openEdit(event: { subType: AssetSubTypeRecord; tab?: SubTypeFormTab }): void {
    this.menuFor = null;
    this.formMode = 'edit';
    this.formRecord = event.subType;
    this.formTab = event.tab ?? 'basic';
    this.formOpen = true;
    this.revealForm();
  }

  private revealForm(): void {
    setTimeout(() => this.formEl?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' }));
  }

  closeForm(): void {
    this.formOpen = false;
    this.formRecord = null;
  }

  onSaved(event: { record: AssetSubTypeRecord; originalCode: string | null }): void {
    if (this.saving) return;
    this.saving = true;
    this.service.persist(event.record, event.originalCode).subscribe({
      next: (saved) => {
        this.saving = false;
        if (event.originalCode && this.selected.delete(event.originalCode)) this.selected.add(saved.code);
        this.open(saved.code, false);
        this.notify(`${saved.name} ${event.originalCode ? 'updated' : 'added'}.`);
        this.closeForm();
      },
      error: (err) => {
        this.saving = false;
        this.notify(apiErrorMessage(err));
      }
    });
  }

  toggleStatus(t: AssetSubTypeRecord): void {
    this.menuFor = null;
    const status = t.status === 'Active' ? 'Inactive' : 'Active';
    this.service.setStatus(t, status).subscribe({
      next: () => this.notify(`${t.name} marked ${status}.`),
      error: (err) => this.notify(apiErrorMessage(err))
    });
  }

  askDelete(list: AssetSubTypeRecord[]): void {
    this.menuFor = null;
    this.pendingDelete = list;
  }

  askDeleteSelected(): void {
    this.askDelete(this.service.all().filter((r) => this.selected.has(r.code)));
  }

  confirmDelete(): void {
    if (this.deleting) return;
    const list = this.pendingDelete;
    const codes = list.map((r) => r.code);
    const done = (message: string) => {
      // Records deleted before a failure are gone too
      const gone = codes.filter((c) => !this.service.find(c));
      gone.forEach((c) => this.selected.delete(c));
      if (this.selectedCode && gone.includes(this.selectedCode)) this.selectedCode = this.service.all()[0]?.code ?? null;
      if (this.formMode === 'edit' && this.formRecord && gone.includes(this.formRecord.code)) this.closeForm();
      this.notify(message);
      this.pendingDelete = [];
      this.deleting = false;
      this.goTo(this.page);
    };
    this.deleting = true;
    this.service.delete(codes).subscribe({
      complete: () => done(codes.length === 1 ? `${list[0].name} deleted.` : `${codes.length} asset sub types deleted.`),
      error: (err) => done(`Delete failed: ${apiErrorMessage(err)}`)
    });
  }

  cancelDelete(): void {
    this.pendingDelete = [];
  }

  notify(message: string): void {
    // shared toast: durations, hover-pause and close button live in shared-ui (toast.ts)
    pushToast(message);
  }
}
