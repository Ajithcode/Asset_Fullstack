import { Component, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  ArrowDown, ArrowUp, ArrowUpDown, Bookmark, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight, CircleX, Columns3, Download,
  EllipsisVertical, FileClock, FileText, Info, Lock, LucideAngularModule, Settings, ShieldAlert
} from 'lucide-angular';
import { AdminPage } from '../shared/admin-common';
import { Subscription, map } from 'rxjs';
import { ExportMenu, ExportRequest, ExportService } from 'shared-ui';
import { AuditCategory, AuditEvent, AuditFilters } from './audit-log.model';
import { AUDIT_DAY, RETENTION_DAYS } from './audit-log.data';
import { AuditLogService, AuditSortKey, AuditStats, auditErrorMessage, emptyFilters } from './audit-log.service';
import { copyText, downloadFile, formatAuditTime } from './audit-log.util';
import { AuditEventDrawer } from './audit-event-drawer/audit-event-drawer';
import { AuditFiltersPanel } from './audit-filters/audit-filters';

type SortKey = AuditSortKey;
type ColumnKey = 'event' | 'module' | 'user' | 'entity' | 'ip' | 'severity' | 'outcome';
type PageItem = number | '…';

const SEVERITY_TONES: Record<string, string> = { Critical: 'red', High: 'pink', Medium: 'orange', Low: 'blue' };
const OUTCOME_TONES: Record<string, string> = { Success: 'green', Failed: 'red', Blocked: 'grey' };

/** Administration > System Audit Log (read-only, immutable records) */
@Component({
  selector: 'app-admin-system-audit-log',
  imports: [FormsModule, RouterLink, LucideAngularModule, AuditFiltersPanel, AuditEventDrawer, ExportMenu],
  templateUrl: './system-audit-log.html',
  styleUrls: ['../shared/admin-workspace.css', './system-audit-log.css']
})
export class SystemAuditLogPage extends AdminPage {
  readonly service = inject(AuditLogService);
  private readonly exporter = inject(ExportService);
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

  /** The current page of events from the API (category chip, filters, sort and paging applied on the server). */
  rows: AuditEvent[] = [];
  /** Events in the current tab under the filters. */
  total = 0;
  /** All Events count under the filters (every tab together). */
  allCount = 0;
  categoryCounts = new Map<AuditCategory, number>();
  loading = false;
  stats: AuditStats | null = null;
  /** The event shown in the drawer, with the details from GET /api/audit-logs/{id}. */
  private detailed: AuditEvent | null = null;
  private querySub?: Subscription;

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
    this.service.loadFilterOptions().subscribe();
    this.service.stats().subscribe((s) => (this.stats = s));
    this.refresh(true);
  }

  // ----- Derived -----
  get selected(): AuditEvent | undefined {
    if (this.detailed?.id === this.selectedId) return this.detailed;
    return this.rows.find((e) => e.id === this.selectedId);
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.total / this.pageSize));
  }

  get pageRows(): AuditEvent[] {
    return this.rows;
  }

  get rangeLabel(): string {
    if (!this.total) return this.loading ? 'Loading events…' : 'Showing 0 of 0 events';
    const start = (this.page - 1) * this.pageSize + 1;
    const end = Math.min(this.total, start + this.rows.length - 1);
    return `Showing ${start}–${end} of ${this.total.toLocaleString('en-US')} events`;
  }

  /** Today against yesterday, e.g. "↑ 12%"; null when there is nothing to compare. */
  trend(today: number, yesterday: number): { text: string; up: boolean } | null {
    if (!yesterday) return null;
    const pct = Math.round(((today - yesterday) / yesterday) * 100);
    return { text: `${pct >= 0 ? '↑' : '↓'} ${Math.abs(pct)}%`, up: pct >= 0 };
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

  // ----- Filtering / sorting / paging (server-side: GET /api/audit-logs) -----
  private refresh(selectFirst = false, done?: () => void): void {
    this.querySub?.unsubscribe();
    this.loading = true;
    this.querySub = this.service
      .query({ filters: this.filters, category: this.category, sortKey: this.sortKey, sortDir: this.sortDir, page: this.page, pageSize: this.pageSize })
      .subscribe({
        next: (res) => {
          this.loading = false;
          this.rows = res.items;
          this.total = res.total;
          this.allCount = res.allCount;
          this.categoryCounts = res.categoryCounts;
          if (this.page > this.pageCount) {
            this.page = this.pageCount;
            this.refresh(selectFirst, done);
            return;
          }
          if (selectFirst && this.rows[0]) this.select(this.rows[0]);
          done?.();
        },
        error: (err) => {
          this.loading = false;
          this.rows = [];
          this.total = 0;
          this.allCount = 0;
          this.categoryCounts = new Map();
          this.notify(auditErrorMessage(err), 'error');
        }
      });
  }

  onApply(filters: AuditFilters): void {
    this.filters = filters;
    this.page = 1;
    this.refresh(false, () => this.notify(`${this.total.toLocaleString('en-US')} events match the filters.`));
  }

  setCategory(cat: '' | AuditCategory): void {
    this.category = cat;
    this.page = 1;
    this.refresh();
  }

  sortBy(key: SortKey): void {
    if (this.sortKey === key) this.sortDir = this.sortDir === 1 ? -1 : 1;
    else {
      this.sortKey = key;
      this.sortDir = key === 'timestamp' || key === 'severity' ? -1 : 1;
    }
    this.refresh();
  }

  goTo(p: PageItem): void {
    if (p === '…') return;
    const next = Math.min(Math.max(1, p), this.pageCount);
    if (next === this.page) return;
    this.page = next;
    this.refresh();
  }

  onPageSize(): void {
    this.pageSize = Number(this.pageSize);
    this.page = 1;
    this.refresh();
  }

  // ----- Selection + row menu -----
  select(e: AuditEvent): void {
    this.selectedId = e.id;
    this.drawerOpen = true;
    if (this.detailed?.id === e.id) return;
    this.service.detail(e).subscribe({
      next: (d) => {
        if (this.selectedId === d.id) this.detailed = d;
      },
      error: () => undefined
    });
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

  /** Export menu (CSV / PDF): every event matching the current tab, filters and sort, fetched page by page. */
  readonly exportRequest = (): ExportRequest<AuditEvent> => ({
    title: 'System Audit Log',
    fileBase: 'system-audit-log',
    columns: [
      { label: 'Event ID', key: 'id' },
      { label: 'Time', value: (e) => formatAuditTime(e.timestamp) },
      { label: 'Event', key: 'event' },
      { label: 'Category', key: 'category' },
      { label: 'Module', key: 'module' },
      { label: 'User', key: 'user' },
      { label: 'Entity / Record', key: 'entity' },
      { label: 'IP Address', key: 'ip' },
      { label: 'Severity', key: 'severity' },
      { label: 'Outcome', key: 'outcome' }
    ],
    filters: {
      Category: this.category,
      From: this.filters.from,
      To: this.filters.to,
      'Event type': this.filters.eventType,
      Module: this.filters.module,
      User: this.filters.user,
      Severity: this.filters.severity,
      'IP address': this.filters.ip,
      Outcome: this.filters.outcome
    },
    rows: () =>
      this.exporter.allPages<AuditEvent>(
        (page, size) =>
          this.service.query({ filters: this.filters, category: this.category, sortKey: this.sortKey, sortDir: this.sortDir, page, pageSize: size }).pipe(map((r) => ({ items: r.items, total: r.total }))),
        100
      )
  });

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

  override ngOnDestroy(): void {
    this.querySub?.unsubscribe();
    super.ngOnDestroy();
  }

  toggleColumn(key: ColumnKey): void {
    if (this.visible[key] && this.visibleColumnCount === 1) return;
    this.visible = { ...this.visible, [key]: !this.visible[key] };
  }
}
