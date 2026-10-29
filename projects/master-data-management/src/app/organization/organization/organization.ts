import { Component, HostListener, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  ArrowDown,
  ArrowUp,
  Building2,
  ChevronLeft,
  ChevronRight,
  Ellipsis,
  LucideAngularModule,
  MapPin,
  Network,
  Plus,
  Search,
  Workflow
} from 'lucide-angular';
import { concat, toArray } from 'rxjs';
import { MASTER_DATA_BASE_PATH } from 'shared-ui';
import { apiErrorMessage } from '../../shared/api-error';
import { OrganizationForm } from './organization-form/organization-form';
import { ORGANIZATION_TYPES, OrganizationFilters, OrganizationRecord } from './organization.model';
import { OrganizationService } from './organization.service';

type SortKey = 'name' | 'code';

const EMPTY_FILTERS: OrganizationFilters = { search: '', status: '', type: '' };

/** Master Data Management > Organization > Organization */
@Component({
  selector: 'app-mdm-organization',
  imports: [FormsModule, RouterLink, LucideAngularModule, OrganizationForm],
  templateUrl: './organization.html',
  styleUrls: ['../../shared/styles/mdm-page.css', './organization.css']
})
export class OrganizationPage {
  readonly service = inject(OrganizationService);
  readonly basePath = MASTER_DATA_BASE_PATH;
  readonly types = ORGANIZATION_TYPES;
  readonly icons = { Building2, Network, Workflow, MapPin, Search, Plus, Ellipsis, ArrowUp, ArrowDown, ChevronLeft, ChevronRight };
  readonly pageSizes = [10, 20, 50];

  // Filters being edited in the bar; `applied` is what the table shows (Apply / Enter)
  draft: OrganizationFilters = { ...EMPTY_FILTERS };
  applied: OrganizationFilters = { ...EMPTY_FILTERS };

  sortKey: SortKey | null = null;
  sortDir: 1 | -1 = 1;
  page = 0;
  pageSize = 10;
  selected = new Set<string>();
  menuFor: string | null = null;

  formOpen = false;
  formMode: 'add' | 'edit' = 'add';
  formRecord: OrganizationRecord | null = null;

  pendingDelete: OrganizationRecord[] = [];
  toast = '';
  saving = false;
  private toastTimer?: ReturnType<typeof setTimeout>;

  constructor() {
    this.service.load();
  }

  get stats() {
    return this.service.stats();
  }

  get rows(): OrganizationRecord[] {
    const rows = this.service.filter(this.applied);
    if (!this.sortKey) return rows;
    const key = this.sortKey;
    return [...rows].sort((a, b) => this.sortDir * a[key].localeCompare(b[key], undefined, { numeric: true }));
  }

  get pageRows(): OrganizationRecord[] {
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

  parentName(r: OrganizationRecord): string {
    return this.service.find(r.parentCode)?.name ?? '-';
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

  toggleMenu(code: string): void {
    this.menuFor = this.menuFor === code ? null : code;
  }

  @HostListener('document:click')
  closeMenu(): void {
    this.menuFor = null;
  }

  openAdd(prefill: OrganizationRecord | null = null): void {
    this.menuFor = null;
    this.formMode = 'add';
    this.formRecord = prefill;
    this.formOpen = true;
  }

  openEdit(record: OrganizationRecord): void {
    this.menuFor = null;
    this.formMode = 'edit';
    this.formRecord = record;
    this.formOpen = true;
  }

  // Add drawer with the parent already chosen
  addChild(parent: OrganizationRecord): void {
    if (!parent.settings.allowSubOrganizations) {
      this.menuFor = null;
      this.notify(`${parent.name} does not allow sub-organizations. Turn it on in its Settings tab first.`);
      return;
    }
    const blank: OrganizationRecord = structuredClone(parent);
    this.openAdd({
      ...blank,
      id: '',
      code: '',
      name: '',
      type: 'Subsidiary',
      parentCode: parent.code,
      logo: null,
      registrationNumber: '',
      website: '',
      description: '',
      businessUnits: 0,
      sites: 0,
      status: 'Active',
      createdOn: '',
      updatedOn: ''
    });
  }

  closeForm(): void {
    this.formOpen = false;
    this.formRecord = null;
  }

  onSaved(event: { record: OrganizationRecord; originalCode: string | null }): void {
    if (this.saving) return;
    this.saving = true;
    this.service.save(event.record, event.originalCode).subscribe({
      next: (saved) => {
        this.saving = false;
        if (event.originalCode && this.selected.delete(event.originalCode)) {
          this.selected.add(saved.code);
        }
        this.notify(event.originalCode ? `${saved.name} updated.` : `${saved.name} added.`);
        this.closeForm();
      },
      error: (err) => {
        this.saving = false;
        this.notify(apiErrorMessage(err));
      }
    });
  }

  toggleStatus(record: OrganizationRecord): void {
    this.menuFor = null;
    const status = record.status === 'Active' ? 'Inactive' : 'Active';
    this.service.save({ ...record, status }, record.code).subscribe({
      next: () => this.notify(`${record.name} marked ${status}.`),
      error: (err) => this.notify(apiErrorMessage(err))
    });
  }

  // Organizations with sub-organizations can't be deleted until those are moved or deleted
  askDelete(records: OrganizationRecord[]): void {
    this.menuFor = null;
    const codes = new Set(records.map((r) => r.code));
    const blocked = records.find((r) => this.service.children(r.code).some((c) => !codes.has(c.code)));
    if (blocked) {
      const count = this.service.children(blocked.code).length;
      this.notify(`${blocked.name} has ${count} sub-organization${count === 1 ? '' : 's'}. Move or delete ${count === 1 ? 'it' : 'them'} first.`);
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
    this.pendingDelete = [];
    // Children first, so the API doesn't refuse a parent that still has sub-organizations in this batch
    const ordered = [...list].sort((a, b) => this.service.ancestry(b.code).length - this.service.ancestry(a.code).length);
    concat(...ordered.map((r) => this.service.remove(r))).pipe(toArray()).subscribe({
      next: () => {
        codes.forEach((c) => this.selected.delete(c));
        if (this.formMode === 'edit' && this.formRecord && codes.includes(this.formRecord.code)) this.closeForm();
        this.notify(codes.length === 1 ? `${list[0].name} deleted.` : `${codes.length} organizations deleted.`);
        this.goTo(this.page);
      },
      // Any organizations deleted before the failure are already gone from the table
      error: (err) => this.notify(apiErrorMessage(err))
    });
  }

  cancelDelete(): void {
    this.pendingDelete = [];
  }

  private notify(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 3500);
  }
}
