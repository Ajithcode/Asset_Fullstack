import { Component, ElementRef, HostListener, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight, Ellipsis, LucideAngularModule, MapPin, Network, Plus, Search, Users } from 'lucide-angular';
import { MASTER_DATA_BASE_PATH } from 'shared-ui';
import { DepartmentService } from '../department/department.service';
import { OrganizationService } from '../organization/organization.service';
import { SiteService } from '../site/site.service';
import { BuDetail } from './bu-detail/bu-detail';
import { BuForm } from './bu-form/bu-form';
import { blankBusinessUnit } from './business-unit.data';
import { MASTER_ICONS } from '../../shared/master-icons';
import { BusinessUnitFilters, BusinessUnitRecord } from './business-unit.model';
import { BusinessUnitService } from './business-unit.service';

type SortKey = 'name' | 'code';

const EMPTY_FILTERS: BusinessUnitFilters = { search: '', status: '', organizationCode: '' };

/** Master Data Management > Organization > Business Unit */
@Component({
  selector: 'app-mdm-organization-business-unit',
  imports: [FormsModule, RouterLink, LucideAngularModule, BuForm, BuDetail],
  templateUrl: './business-unit.html',
  styleUrl: '../../shared/styles/mdm-page.css'
})
export class BusinessUnitPage {
  readonly service = inject(BusinessUnitService);
  readonly sites = inject(SiteService);
  readonly departments = inject(DepartmentService);
  readonly organizations = inject(OrganizationService);
  readonly basePath = MASTER_DATA_BASE_PATH;
  readonly logoIcons = MASTER_ICONS;
  readonly icons = { Network, MapPin, Users, Search, Plus, Ellipsis, ArrowUp, ArrowDown, ChevronLeft, ChevronRight };
  readonly pageSizes = [10, 20, 50];

  @ViewChild('detailEl') detailEl?: ElementRef<HTMLElement>;
  @ViewChild('formEl', { read: ElementRef }) formEl?: ElementRef<HTMLElement>;

  draft: BusinessUnitFilters = { ...EMPTY_FILTERS };
  applied: BusinessUnitFilters = { ...EMPTY_FILTERS };

  sortKey: SortKey | null = null;
  sortDir: 1 | -1 = 1;
  page = 0;
  pageSize = 10;
  selected = new Set<string>();
  menuFor: string | null = null;

  selectedCode: string | null = 'MFG';
  formOpen = false;
  formMode: 'add' | 'edit' = 'add';
  formRecord: BusinessUnitRecord | null = null;

  pendingDelete: BusinessUnitRecord[] = [];
  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  constructor() {
    // "Go to Business Unit" links pass ?code=MFG to open that unit
    const code = inject(ActivatedRoute).snapshot.queryParamMap.get('code');
    if (code && this.service.find(code)) this.selectedCode = code;
  }

  get stats() {
    const all = this.service.all();
    const active = all.filter((r) => r.status === 'Active').length;
    return {
      total: all.length,
      active,
      inactive: all.length - active,
      sites: this.sites.all().length,
      departments: this.departments.all().length
    };
  }

  get rows(): BusinessUnitRecord[] {
    const rows = this.service.filter(this.applied);
    if (!this.sortKey) return rows;
    const key = this.sortKey;
    return [...rows].sort((a, b) => this.sortDir * a[key].localeCompare(b[key], undefined, { numeric: true }));
  }

  get pageRows(): BusinessUnitRecord[] {
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

  get selectedUnit(): BusinessUnitRecord | undefined {
    return this.service.find(this.selectedCode);
  }

  organizationName(code: string): string {
    return this.organizations.find(code)?.name ?? code;
  }

  departmentCount(unit: BusinessUnitRecord): number {
    return this.departments.ofBusinessUnit(unit.code).length;
  }

  siteCount(unit: BusinessUnitRecord): number {
    return this.sites.ofBusinessUnit(unit.name).length;
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

  openAdd(prefill: BusinessUnitRecord | null = null): void {
    this.menuFor = null;
    this.formMode = 'add';
    this.formRecord = prefill;
    this.formOpen = true;
    this.revealForm();
  }

  addSubUnit(parent: BusinessUnitRecord): void {
    this.openAdd({ ...blankBusinessUnit(), organizationCode: parent.organizationCode, parentCode: parent.code, currency: parent.currency });
  }

  openEdit(unit: BusinessUnitRecord): void {
    this.menuFor = null;
    this.formMode = 'edit';
    this.formRecord = unit;
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

  onSaved(event: { record: BusinessUnitRecord; originalCode: string | null; originalName: string | null }): void {
    this.service.save(event.record, event.originalCode);
    // Sites refer to the unit by name
    if (event.originalName) this.sites.renameBusinessUnit(event.originalName, event.record.name);
    if (event.originalCode) this.departments.renameBusinessUnit(event.originalCode, event.record.code);
    if (event.originalCode && this.selected.delete(event.originalCode)) this.selected.add(event.record.code);
    this.open(event.record.code, false);
    this.notify(`${event.record.name} ${event.originalCode ? 'updated' : 'added'}.`);
    this.closeForm();
  }

  toggleStatus(unit: BusinessUnitRecord): void {
    this.menuFor = null;
    const status = unit.status === 'Active' ? 'Inactive' : 'Active';
    this.service.save({ ...unit, status }, unit.code);
    this.notify(`${unit.name} marked ${status}.`);
  }

  // Units that still have departments or sites can't be deleted
  askDelete(units: BusinessUnitRecord[]): void {
    this.menuFor = null;
    const withDepartments = units.find((u) => this.departmentCount(u));
    if (withDepartments) {
      const count = this.departmentCount(withDepartments);
      this.notify(`${withDepartments.name} has ${count} department${count === 1 ? '' : 's'}. Move or delete ${count === 1 ? 'it' : 'them'} on the Department page first.`);
      return;
    }
    const blocked = units.find((u) => this.siteCount(u));
    if (blocked) {
      const count = this.siteCount(blocked);
      this.notify(`${blocked.name} is used by ${count} site${count === 1 ? '' : 's'}. Move ${count === 1 ? 'it' : 'them'} to another business unit first.`);
      return;
    }
    this.pendingDelete = units;
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
    this.notify(codes.length === 1 ? `${this.pendingDelete[0].name} deleted.` : `${codes.length} business units deleted.`);
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
