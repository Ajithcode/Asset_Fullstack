import { Component, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  ArrowDown, ArrowUp, ArrowUpDown, Bookmark, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight, CircleX, Columns3, Download,
  EllipsisVertical, FileClock, FileText, Info, Lock, LucideAngularModule, Settings, ShieldAlert
} from 'lucide-angular';
import { AdminPage } from '../shared/admin-common';
import { AuditCategory, AuditEvent, AuditFilters } from './audit-log.model';
import { AUDIT_DAY, RETENTION_DAYS } from './audit-log.data';
import { AuditLogService, emptyFilters } from './audit-log.service';
import { copyText, downloadFile, formatAuditTime, toCsv } from './audit-log.util';
import { AuditEventDrawer } from './audit-event-drawer/audit-event-drawer';
import { AuditFiltersPanel } from './audit-filters/audit-filters';

type SortKey = 'timestamp' | 'event' | 'user' | 'ip' | 'severity';
type ColumnKey = 'event' | 'module' | 'user' | 'entity' | 'ip' | 'severity' | 'outcome';
type PageItem = number | '…';

const SEVERITY_RANK: Record<string, number> = { Critical: 4, High: 3, Medium: 2, Low: 1 };
const SEVERITY_TONES: Record<string, string> = { Critical: 'red', High: 'pink', Medium: 'orange', Low: 'blue' };
const OUTCOME_TONES: Record<string, string> = { Success: 'green', Failed: 'red', Blocked: 'grey' };

/** Administration > System Audit Log (read-only, immutable records) */
@Component({
  selector: 'app-admin-system-audit-log',
  imports: [FormsModule, RouterLink, LucideAngularModule, AuditFiltersPanel, AuditEventDrawer],
  templateUrl: './system-audit-log.html',
  styleUrls: ['../shared/admin-workspace.css', './system-audit-log.css']
})
export class SystemAuditLogPage extends AdminPage {
  readonly service = inject(AuditLogService);
  @ViewChild(AuditFiltersPanel) filterPanel?: AuditFiltersPanel;

  readonly icons = {
    FileText, ShieldAlert, Settings, CircleX, Download, Bookmark, FileClock, Columns3, ArrowUp, ArrowDown, ArrowUpDown,
    EllipsisVertical, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight, Info, Lock
  };
  readonly retentionDays = RETENTION_DAYS;
  readonly pageSizes = [5, 10, 25, 50, 100];
  readonly formatAuditTime = formatAuditTime;
  readonly columns: { key: ColumnKey; label: string }[] = [
    { key: 'event', label: 'Event' },
    { key: 'module', label: 'Module' },
    { key: 'user', label: 'User' },
    { key: 'entity', label: 'Entity / Record' },
    { key: 'ip', label: 'IP Address' },
    { key: 'severity', label: 'Severity' },
    { key: 'outcome', label: 'Outcome' }
  ];
  visible: Record<ColumnKey, boolean> = { event: true, module: true, user: true, entity: true, ip: true, severity: true, outcome: true };

  filters: AuditFilters = emptyFilters();
  category: '' | AuditCategory = '';
  sortKey: SortKey = 'timestamp';
  sortDir: 1 | -1 = -1;
  page = 1;
  pageSize = 5;

  /** Rows after the filter panel (chip counts come from these). */
  base: AuditEvent[] = [];
  /** Rows after the category chip and sort. */
  rows: AuditEvent[] = [];
  categoryCounts = new Map<AuditCategory, number>();

  selectedId = '';
  drawerOpen = true;
  menuId = '';
  columnMenuOpen = false;
  retentionOpen = false;
  saveOpen = false;
  saveName = '';
  saveError = '';

  constructor() {
    super();
    this.refresh();
    this.selectedId = this.rows[0]?.id ?? '';
  }

  // ----- Derived -----
  get selected(): AuditEvent | undefined {
    return this.service.all().find((e) => e.id === this.selectedId);
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.rows.length / this.pageSize));
  }

  get pageRows(): AuditEvent[] {
    const start = (this.page - 1) * this.pageSize;
    return this.rows.slice(start, start + this.pageSize);
  }

  get rangeLabel(): string {
    if (!this.rows.length) return 'Showing 0 of 0 events';
    const start = (this.page - 1) * this.pageSize + 1;
    const end = Math.min(this.rows.length, start + this.pageSize - 1);
    return `Showing ${start}–${end} of ${this.rows.length.toLocaleString('en-US')} events`;
  }

  /** 1 2 3 4 5 … 250 style page list. */
  get pageItems(): PageItem[] {
    const total = this.pageCount;
    if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);
    const p = this.page;
    const lo = p <= 4 ? 1 : p >= total - 3 ? total - 4 : p - 1;
    const hi = p <= 4 ? 5 : p >= total - 3 ? total : p + 1;
    const items: PageItem[] = [];
    if (lo > 1) items.push(1);
    if (lo > 2) items.push('…');
    for (let i = lo; i <= hi; i++) items.push(i);
    if (hi < total - 1) items.push('…');
    if (hi < total) items.push(total);
    return items;
  }

  get visibleColumnCount(): number {
    return this.columns.filter((c) => this.visible[c.key]).length;
  }

  get isFiltered(): boolean {
    return JSON.stringify(this.filters) !== JSON.stringify(emptyFilters()) || !!this.category;
  }

  countOf(cat: AuditCategory): number {
    return this.categoryCounts.get(cat) ?? 0;
  }

  severityTone(s: string): string {
    return SEVERITY_TONES[s] ?? 'grey';
  }

  outcomeTone(o: string): string {
    return OUTCOME_TONES[o] ?? 'grey';
  }

  sortIcon(key: SortKey) {
    if (this.sortKey !== key) return this.icons.ArrowUpDown;
    return this.sortDir === 1 ? this.icons.ArrowUp : this.icons.ArrowDown;
  }

  ariaSort(key: SortKey): string {
    if (this.sortKey !== key) return 'none';
    return this.sortDir === 1 ? 'ascending' : 'descending';
  }

  // ----- Filtering / sorting / paging -----
  private refresh(): void {
    this.base = this.service.filter(this.filters);
    this.categoryCounts = new Map();
    for (const e of this.base) this.categoryCounts.set(e.category, (this.categoryCounts.get(e.category) ?? 0) + 1);
    this.applyCategoryAndSort();
  }

  private applyCategoryAndSort(): void {
    const key = this.sortKey;
    const dir = this.sortDir;
    const value = (e: AuditEvent): string | number => (key === 'severity' ? SEVERITY_RANK[e.severity] : key === 'ip' ? this.ipKey(e.ip) : e[key]);
    this.rows = this.base
      .filter((e) => !this.category || e.category === this.category)
      .sort((a, b) => {
        const va = value(a);
        const vb = value(b);
        const c = va < vb ? -1 : va > vb ? 1 : 0;
        return c !== 0 ? c * dir : b.timestamp.localeCompare(a.timestamp);
      });
    this.page = Math.min(this.page, this.pageCount);
  }

  /** Zero-padded IP so 10.20.4.18 sorts before 10.20.10.1. */
  private ipKey(ip: string): string {
    return ip.split('.').map((p) => p.padStart(3, '0')).join('.');
  }

  onApply(filters: AuditFilters): void {
    this.filters = filters;
    this.page = 1;
    this.refresh();
    this.notify(`${this.rows.length.toLocaleString('en-US')} events match the filters.`);
  }

  setCategory(cat: '' | AuditCategory): void {
    this.category = cat;
    this.page = 1;
    this.applyCategoryAndSort();
  }

  sortBy(key: SortKey): void {
    if (this.sortKey === key) this.sortDir = this.sortDir === 1 ? -1 : 1;
    else {
      this.sortKey = key;
      this.sortDir = key === 'timestamp' || key === 'severity' ? -1 : 1;
    }
    this.applyCategoryAndSort();
  }

  goTo(p: PageItem): void {
    if (p === '…') return;
    this.page = Math.min(Math.max(1, p), this.pageCount);
  }

  onPageSize(): void {
    this.pageSize = Number(this.pageSize);
    this.page = 1;
  }

  // ----- Selection + row menu -----
  select(e: AuditEvent): void {
    this.selectedId = e.id;
    this.drawerOpen = true;
  }

  toggleMenu(e: AuditEvent, ev: MouseEvent): void {
    ev.stopPropagation();
    this.menuId = this.menuId === e.id ? '' : e.id;
  }

  closeMenus(): void {
    this.menuId = '';
    this.columnMenuOpen = false;
  }

  viewFromMenu(e: AuditEvent): void {
    this.menuId = '';
    this.select(e);
  }

  // ----- Export / copy -----
  async copyId(e: AuditEvent): Promise<void> {
    this.menuId = '';
    this.notify((await copyText(e.id)) ? `Event ID ${e.id} copied.` : `Copy failed. Event ID: ${e.id}`);
  }

  exportEvent(e: AuditEvent): void {
    this.menuId = '';
    const payload = { ...e, before: e.before ? JSON.parse(e.before) : null, after: e.after ? JSON.parse(e.after) : null, timezone: 'GMT+4' };
    downloadFile(`${e.id}.json`, JSON.stringify(payload, null, 2), 'application/json');
    this.notify(`${e.id} exported as JSON.`);
  }

  exportLog(): void {
    if (!this.rows.length) {
      this.notify('No events to export for these filters.');
      return;
    }
    const header = ['Event ID', 'Timestamp (GMT+4)', 'Event', 'Category', 'Module', 'Action', 'User', 'User ID', 'Role', 'Entity Type',
      'Entity / Record', 'Record ID', 'IP Address', 'Device / Browser', 'Location', 'Request ID', 'Severity', 'Outcome', 'MFA', 'Auth Method', 'Risk Score'];
    const rows = this.rows.map((e) => [
      e.id, formatAuditTime(e.timestamp), e.event, e.category, e.module, e.action, e.user, e.userId, e.role, e.entityType,
      e.entity, e.recordId, e.ip, e.device, e.location, e.requestId, e.severity, e.outcome, e.mfa, e.authMethod, e.riskScore
    ]);
    downloadFile(`audit-log_${AUDIT_DAY}.csv`, toCsv(header, rows), 'text/csv;charset=utf-8');
    this.notify(`Exported ${this.rows.length.toLocaleString('en-US')} events to CSV.`);
  }

  // ----- Saved filters -----
  openSave(): void {
    this.saveName = '';
    this.saveError = '';
    this.saveOpen = true;
  }

  confirmSave(): void {
    const name = this.saveName.trim();
    if (!name) {
      this.saveError = 'Give the filter a name.';
      return;
    }
    if (name.length > 40) {
      this.saveError = 'Keep the name under 40 characters.';
      return;
    }
    const filters = this.filterPanel?.current() ?? this.filters;
    this.service.saveFilter({ name, filters, category: this.category });
    this.saveOpen = false;
    this.notify(`Filter “${name}” saved.`);
  }

  useSaved(s: { name: string; filters: AuditFilters; category: '' | AuditCategory }): void {
    this.filters = { ...s.filters };
    this.category = s.category;
    this.page = 1;
    this.refresh();
    this.notify(`Applied saved filter “${s.name}”.`);
  }

  toggleColumn(key: ColumnKey): void {
    if (this.visible[key] && this.visibleColumnCount === 1) return;
    this.visible = { ...this.visible, [key]: !this.visible[key] };
  }
}
