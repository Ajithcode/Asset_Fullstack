import { Component, HostListener, OnDestroy, OnInit, inject } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Subject, Subscription, debounceTime, firstValueFrom } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  ArrowDown, ArrowUp, ArrowUpDown, Box, CalendarClock, CalendarDays, CircleAlert, CircleCheck, CircleDollarSign, CircleX, ClipboardCheck,
  Eye, Factory, FileCheck, FileText, Funnel, Info, LucideAngularModule, MapPin, Package, Plus, Radio, RefreshCw, Search, ShieldCheck, Upload, Wrench, X
} from 'lucide-angular';
import { ExportMenu, ExportRequest } from 'shared-ui';
import { AnalyticsPage } from '../shared/analytics-common';
import { ReportCalendar } from './report-calendar';
import { ReportCreateDialog } from './report-create-dialog';
import { ReportScheduleDialog } from './report-schedule-dialog';
import { ReportViewerDialog } from './report-viewer-dialog';
import { ReportingPeriodPanel } from './reporting-period-panel';
import {
  ReportFrequency, ReportPeriod, ReportSchedule, ReportStatus, SR_STATUSES, StandardReport
} from './standard-reports.data';
import {
  CalendarMarks, NewStandardReport, SR_SORT_FIELDS, StandardReportQuery, StandardReportSummary, StandardReportsService, toApiDate
} from './standard-reports.service';
import { formatDay, formatPeriod, formatStamp } from './standard-reports.util';

type SortKey = 'name' | 'module' | 'period' | 'generatedOn' | 'nextScheduled' | 'frequency' | 'status';

const PAGE_SIZE = 10;

/** Icon / tone for well-known category names; other categories get a tone picked from their name. */
const MODULE_STYLE: Record<string, { icon: typeof Box; tone: string }> = {
  Assets: { icon: Box, tone: 'green' },
  'Tracking & IoT': { icon: Radio, tone: 'blue' },
  'Movement & Custody': { icon: MapPin, tone: 'purple' },
  Maintenance: { icon: Wrench, tone: 'orange' },
  Inspection: { icon: ClipboardCheck, tone: 'purple' },
  Inventory: { icon: Package, tone: 'blue' },
  'WIP / Operations': { icon: Factory, tone: 'blue' },
  'Audit & Compliance': { icon: ShieldCheck, tone: 'pink' },
  'Contracts & Warranty': { icon: FileCheck, tone: 'pink' },
  Financial: { icon: CircleDollarSign, tone: 'orange' }
};

const CATEGORY_TONES = ['green', 'blue', 'purple', 'orange', 'pink'];

function categoryStyle(name: string): { icon: typeof Box; tone: string } {
  const known = MODULE_STYLE[name];
  if (known) return known;
  let hash = 0;
  for (const ch of name) hash = (hash * 31 + ch.charCodeAt(0)) >>> 0;
  return { icon: Box, tone: CATEGORY_TONES[hash % CATEGORY_TONES.length] };
}

const STATUS_TONES: Record<ReportStatus, string> = { Ready: 'green', Scheduled: 'blue', 'Needs review': 'pink', Draft: 'orange' };
const FREQUENCY_TONES: Record<ReportFrequency, string> = { Daily: 'green', Weekly: 'blue', Monthly: 'blue', Quarterly: 'purple', 'On demand': 'purple' };
const FREQUENCY_ORDER: ReportFrequency[] = ['Daily', 'Weekly', 'Monthly', 'Quarterly', 'On demand'];

type AlertKind = 'success' | 'error' | 'warning' | 'info';

interface PageAlert {
  id: number;
  kind: AlertKind;
  title: string;
  message: string;
  /** Time on screen before it closes itself. */
  ms: number;
  paused?: boolean;
  timer?: ReturnType<typeof setTimeout>;
}

const ALERT_TITLES: Record<AlertKind, string> = { success: 'Success', error: 'Error', warning: 'Warning', info: 'Info' };
const MAX_ALERTS = 4;

/** Reports & Analytics > Standard Reports: reporting period, report calendar and the standard report catalogue. */
@Component({
  selector: 'app-ra-standard-reports',
  standalone: true,
  imports: [FormsModule, RouterLink, LucideAngularModule, ReportingPeriodPanel, ReportCalendar, ReportViewerDialog, ReportScheduleDialog, ReportCreateDialog, ExportMenu],
  templateUrl: './standard-reports.html',
  // Alerts split out to keep each stylesheet under the anyComponentStyle budget
  styleUrls: ['../shared/analytics-page.css', './standard-reports.css', './standard-reports-alerts.css']
})
export class StandardReportsPage extends AnalyticsPage implements OnInit, OnDestroy {
  readonly service = inject(StandardReportsService);

  readonly icons = { FileText, Funnel, CalendarDays, RefreshCw, Search, Eye, CalendarClock, Upload, X, Plus };
  /** Category filter / New report options, from GET /api/standard-reports/options. */
  categoryOptions: string[] = [];
  reportTypeOptions: string[] = [];
  categoriesLoading = false;
  /** Tiles: GET /api/standard-reports/summary (all reports, not just this page). */
  summary: StandardReportSummary = { totalReports: 0, withDateFilters: 0, scheduled: 0, updatedThisMonth: 0 };
  /** Calendar markers: GET /api/standard-reports/calendar for the shown month. */
  calendarMarks: Record<string, CalendarMarks> = {};
  private calendarMonth = { year: new Date().getFullYear(), month: new Date().getMonth() + 1 };
  /** Report open in the edit dialog (PUT /api/standard-reports/{id}). */
  editing: StandardReport | null = null;
  /** Generate / delete in progress from the viewer. */
  rowBusy = false;
  readonly frequencyOptions = FREQUENCY_ORDER;
  readonly statusOptions = SR_STATUSES;
  readonly columns: { key: SortKey; label: string }[] = [
    { key: 'name', label: 'Report Name' },
    { key: 'module', label: 'Category' },
    { key: 'period', label: 'Report Period' },
    { key: 'generatedOn', label: 'Generated On' },
    { key: 'nextScheduled', label: 'Next Scheduled' },
    { key: 'frequency', label: 'Frequency' },
    { key: 'status', label: 'Status' }
  ];

  search = '';
  dayFilter: string | null = null;
  sortKey: SortKey = 'name';
  sortDir: 1 | -1 = 1;
  page = 1;
  viewing: StandardReport | null = null;
  scheduling: StandardReport | null = null;

  // Server-side filters (GET /api/standard-reports)
  categoryFilter = '';
  frequencyFilter = '';
  statusFilter = '';
  loading = false;
  loadError = '';

  // New report dialog (POST /api/standard-reports)
  creating = false;
  saving = false;
  createError = '';

  // Schedule dialog (PUT /api/standard-reports/{reportId}/schedule)
  scheduleSaving = false;
  scheduleError = '';

  /** Top-right alert stack, newest first. */
  alerts: PageAlert[] = [];
  private alertSeq = 0;

  private loadSub: Subscription | null = null;
  private categorySub: Subscription | null = null;
  private readonly searchInput = new Subject<void>();
  private readonly searchSub = this.searchInput.pipe(debounceTime(300)).subscribe(() => this.load());

  ngOnInit(): void {
    this.load();
    this.loadCategories();
    this.loadSummary();
    this.loadCalendar();
  }

  /** Tiles: GET /api/standard-reports/summary. */
  loadSummary(): void {
    this.service.summary(this.service.period.timeZone).subscribe({
      next: (s) => (this.summary = s),
      error: (err: HttpErrorResponse) => console.warn('[standard-reports] summary failed', err)
    });
  }

  /** Calendar markers for the shown month: GET /api/standard-reports/calendar. */
  loadCalendar(month = this.calendarMonth): void {
    this.calendarMonth = month;
    this.service.calendar(month.year, month.month, this.service.period.timeZone).subscribe({
      next: (marks) => {
        if (this.calendarMonth === month) this.calendarMarks = marks;
      },
      error: (err: HttpErrorResponse) => {
        console.warn('[standard-reports] calendar failed', err);
        this.calendarMarks = {};
      }
    });
  }

  /** After a write: the table, tiles and calendar all change. */
  private reloadAll(): void {
    this.load();
    this.loadSummary();
    this.loadCalendar();
  }

  /** Category / report type dropdowns: GET /api/standard-reports/options. */
  loadCategories(): void {
    this.categoriesLoading = true;
    this.categorySub?.unsubscribe();
    this.categorySub = this.service.options().subscribe(({ categories: names, reportTypes }) => {
      this.categoriesLoading = false;
      this.categoryOptions = names;
      this.reportTypeOptions = reportTypes;
      // Drop a filter value the master no longer has
      if (this.categoryFilter && !names.includes(this.categoryFilter)) {
        this.categoryFilter = '';
        this.filterChanged();
      }
    });
  }

  override ngOnDestroy(): void {
    super.ngOnDestroy();
    this.searchSub.unsubscribe();
    this.loadSub?.unsubscribe();
    this.categorySub?.unsubscribe();
    this.alerts.forEach((a) => clearTimeout(a.timer));
  }

  /** List / export query: search, filters, applied period, sort and page. */
  private query(): StandardReportQuery {
    const p = this.service.period;
    return {
      search: this.search.trim(),
      category: this.categoryFilter,
      frequency: this.frequencyFilter,
      status: this.statusFilter,
      periodFrom: toApiDate(p.start, p.startTime ?? '00:00'),
      periodTo: toApiDate(p.end, p.endTime ?? '23:59'),
      timeZone: p.timeZone,
      includeTime: !!(p.startTime && p.endTime),
      sortBy: SR_SORT_FIELDS[this.sortKey],
      sortDirection: this.sortDir === 1 ? 'asc' : 'desc',
      page: this.page,
      pageSize: PAGE_SIZE
    };
  }

  /** GET /api/standard-reports with the search, filters, period, sort and page. */
  load(): void {
    this.loadSub?.unsubscribe();
    this.loading = true;
    this.loadError = '';
    this.loadSub = this.service
      .load(this.query())
      .subscribe({
        next: () => {
          this.loading = false;
          this.updatedMinutes = 0;
          // Page emptied (e.g. after a filter change): step back
          if (this.page > this.pageCount) {
            this.page = this.pageCount;
            this.load();
          }
        },
        error: (err: HttpErrorResponse) => {
          this.loading = false;
          this.service.reports = [];
          this.service.totalCount = 0;
          this.loadError = apiMessage(err, 'Could not load standard reports.');
          this.showAlert('error', this.loadError);
        }
      });
  }

  override refresh(): void {
    if (this.loading) return;
    this.reloadAll();
  }

  filterChanged(): void {
    this.page = 1;
    this.load();
  }

  openCreate(): void {
    this.createError = '';
    this.editing = null;
    this.creating = true;
  }

  /** Edit from the viewer: the same form, saved with PUT /api/standard-reports/{id}. */
  openEdit(r: StandardReport): void {
    this.createError = '';
    this.editing = r;
    this.viewing = null;
    this.creating = true;
  }

  /** New report (POST) or edited report (PUT). */
  createReport(form: NewStandardReport): void {
    const editing = this.editing;
    this.saving = true;
    this.createError = '';
    const save$ = editing ? this.service.edit(editing, form) : this.service.create(form);
    save$.subscribe({
      next: (r) => {
        this.saving = false;
        this.creating = false;
        this.editing = null;
        this.notify(editing ? `Report “${r.name}” updated.` : `Report “${r.name}” created.`);
        this.reloadAll();
      },
      error: (err: HttpErrorResponse) => {
        this.saving = false;
        this.createError = apiMessage(err, editing ? 'Could not save the report.' : 'Could not create the report.');
        this.showAlert('error', this.createError);
      }
    });
  }

  /** POST /api/standard-reports/{id}/generate. */
  generate(r: StandardReport): void {
    if (this.rowBusy) return;
    this.rowBusy = true;
    this.service.generate(r).subscribe({
      next: (updated) => {
        this.rowBusy = false;
        if (this.viewing) this.viewing = updated;
        this.notify(`“${updated.name}” generated.`);
        this.reloadAll();
      },
      error: (err: HttpErrorResponse) => {
        this.rowBusy = false;
        this.showAlert('error', apiMessage(err, 'Could not generate the report.'));
      }
    });
  }

  /** DELETE /api/standard-reports/{id}. */
  deleteReport(r: StandardReport): void {
    if (this.rowBusy || !confirm(`Delete “${r.name}”? This cannot be undone.`)) return;
    this.rowBusy = true;
    this.service.remove(r).subscribe({
      next: () => {
        this.rowBusy = false;
        this.viewing = null;
        this.notify(`Report “${r.name}” deleted.`);
        this.reloadAll();
      },
      error: (err: HttpErrorResponse) => {
        this.rowBusy = false;
        this.showAlert('error', apiMessage(err, 'Could not delete the report.'));
      }
    });
  }

  /** List Export (CSV / PDF): every report matching the current search, filters, period and sort (all pages). */
  exportList = (): ExportRequest<StandardReport> => {
    const q = this.query();
    return {
      title: 'Standard Reports',
      fileBase: 'standard-reports',
      columns: [
        { label: 'Report ID', key: 'id' },
        { label: 'Report Name', key: 'name' },
        { label: 'Category', key: 'module' },
        { label: 'Report Type', key: 'reportType' },
        { label: 'Report Period', value: (r) => formatPeriod(r.period) },
        { label: 'Generated On', key: 'generatedOn' },
        { label: 'Next Scheduled', key: 'nextScheduled' },
        { label: 'Frequency', key: 'frequency' },
        { label: 'Status', key: 'status' },
        { label: 'Owner', key: 'owner' }
      ],
      filters: {
        Search: q.search,
        Category: q.category,
        Frequency: q.frequency,
        Status: q.status,
        Period: formatPeriod(this.service.period)
      },
      rows: () => this.service.loadAll(q)
    };
  };

  private readonly rowExports = new Map<string, () => ExportRequest>();

  /** Per-report Export (CSV / PDF): the report's own rows from GET /api/standard-reports/{id}/details (one line per reading). */
  exportFor = (r: StandardReport): (() => ExportRequest) => {
    let fn = this.rowExports.get(r.id);
    if (!fn) {
      fn = () => ({
        title: r.name,
        fileBase: `${r.name.toLowerCase().replace(/[^a-z0-9]+/g, '-')}_${r.period.start}_${r.period.end}`,
        columns: [
          { label: 'Timestamp', key: 'timestamp' },
          { label: 'Local time', key: 'localTime' },
          { label: 'Tag ID', key: 'tagId' },
          { label: 'Asset ID', key: 'assetId' },
          { label: 'Asset name', key: 'assetName' },
          { label: 'Category', key: 'category' },
          { label: 'Sub-category', key: 'subCategory' },
          { label: 'Device ID', key: 'deviceId' },
          { label: 'Zone', key: 'zoneName' },
          { label: 'Zone code', key: 'zoneCode' }
        ],
        filters: { Report: r.id, Category: r.module, Period: formatPeriod(r.period) },
        rows: () =>
          firstValueFrom(this.service.details(r)).then((d) => {
            if (!d.hasData) throw new Error(`“${r.name}” has no data to export for its report type.`);
            return d.records;
          })
      });
      this.rowExports.set(r.id, fn);
    }
    return fn;
  };

  monthChanged(month: { year: number; month: number }): void {
    this.loadCalendar(month);
  }

  // ===== Stat tiles (GET /api/standard-reports/summary) =====
  get total(): number {
    return this.summary.totalReports;
  }

  get withDateFilters(): number {
    return this.summary.withDateFilters;
  }

  get scheduledCount(): number {
    return this.summary.scheduled;
  }

  get updatedThisMonth(): number {
    return this.summary.updatedThisMonth;
  }

  /** Every report shares the applied reporting period. */
  get reportingPeriod(): ReportPeriod {
    return this.service.period;
  }

  // ===== Table =====
  /** Current server page, sorted the same way the API was asked to (keeps order if the API ignores sortBy). */
  get filtered(): StandardReport[] {
    const day = this.dayFilter;
    const rows = this.service.reports.filter((r) => !day || r.generatedOn.startsWith(day) || (r.nextScheduled ?? '').startsWith(day));
    return rows.sort((a, b) => this.sortValue(a).localeCompare(this.sortValue(b)) * this.sortDir);
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.service.totalCount / PAGE_SIZE));
  }

  get currentPage(): number {
    return Math.min(this.page, this.pageCount);
  }

  get pageRows(): StandardReport[] {
    // The API may ignore paging and return everything: slice locally in that case
    const rows = this.filtered;
    return rows.length > PAGE_SIZE ? rows.slice((this.currentPage - 1) * PAGE_SIZE, this.currentPage * PAGE_SIZE) : rows;
  }

  get pages(): number[] {
    return Array.from({ length: this.pageCount }, (_, i) => i + 1);
  }

  get rangeText(): string {
    const n = this.service.totalCount;
    if (n === 0) return '0 reports';
    return `${(this.currentPage - 1) * PAGE_SIZE + 1}–${Math.min(this.currentPage * PAGE_SIZE, n)} of ${n} reports`;
  }

  get dayFilterText(): string {
    return this.dayFilter ? formatDay(this.dayFilter) : '';
  }

  sortBy(key: SortKey): void {
    if (this.sortKey === key) {
      this.sortDir = this.sortDir === 1 ? -1 : 1;
    } else {
      this.sortKey = key;
      this.sortDir = 1;
    }
    this.page = 1;
    this.load();
  }

  sortIcon(key: SortKey): typeof ArrowUp {
    return this.sortKey !== key ? ArrowUpDown : this.sortDir === 1 ? ArrowUp : ArrowDown;
  }

  ariaSort(key: SortKey): string {
    return this.sortKey !== key ? 'none' : this.sortDir === 1 ? 'ascending' : 'descending';
  }

  searchChanged(): void {
    this.page = 1;
    this.searchInput.next();
  }

  goTo(page: number): void {
    const next = Math.min(Math.max(1, page), this.pageCount);
    if (next === this.page) return;
    this.page = next;
    // Reload unless the API returned every row at once (then the pager slices locally)
    if (this.service.reports.length <= PAGE_SIZE) this.load();
  }

  selectDay(day: string | null): void {
    this.dayFilter = day;
    this.page = 1;
  }

  // ===== Display helpers =====
  moduleIcon(r: StandardReport): typeof Box {
    return categoryStyle(r.module).icon;
  }

  moduleTone(r: StandardReport): string {
    return categoryStyle(r.module).tone;
  }

  statusTone(r: StandardReport): string {
    return STATUS_TONES[r.status] ?? 'purple';
  }

  frequencyTone(r: StandardReport): string {
    return FREQUENCY_TONES[r.frequency] ?? 'purple';
  }

  periodText(r: StandardReport): string {
    return formatPeriod(r.period);
  }

  stamp(value: string | null): string {
    return formatStamp(value);
  }

  // ===== Actions =====
  /**
   * Apply Dates: PUT /api/standard-reports/report-period saves the period on the reports in the table (this page),
   * then the table is reloaded filtered to the new period.
   */
  applyPeriod(period: ReportPeriod): void {
    const ids = this.service.reports.map((r) => r.id).filter(Boolean);
    const done = (message: string) => {
      this.service.applyPeriod(period);
      this.page = 1;
      this.reloadAll();
      this.notify(message);
    };
    if (!ids.length) {
      done(`Reporting period ${formatPeriod(period)} applied.`);
      return;
    }
    this.service.applyPeriodTo(ids, period).subscribe({
      next: (res) => {
        const n = res?.updatedCount ?? ids.length;
        done(`Reporting period ${formatPeriod(period)} applied to ${n} report${n === 1 ? '' : 's'}.`);
      },
      error: (err: HttpErrorResponse) => this.showAlert('error', apiMessage(err, 'Could not apply the reporting period.'))
    });
  }

  view(r: StandardReport): void {
    this.viewing = r;
  }

  openSchedule(r: StandardReport): void {
    this.scheduleError = '';
    this.scheduling = r;
  }

  saveSchedule(schedule: ReportSchedule): void {
    const report = this.scheduling;
    if (!report || this.scheduleSaving) return;
    this.scheduleSaving = true;
    this.scheduleError = '';
    this.service.saveSchedule(report, schedule).subscribe({
      next: (updated) => {
        this.scheduleSaving = false;
        this.scheduling = null;
        this.notify(`“${updated.name}” scheduled ${schedule.frequency.toLowerCase()}, next run ${formatStamp(updated.nextScheduled)}.`);
        this.reloadAll();
      },
      error: (err: HttpErrorResponse) => {
        this.scheduleSaving = false;
        this.scheduleError = apiMessage(err, 'Could not save the schedule.');
        this.showAlert('error', this.scheduleError);
      }
    });
  }

  // ===== Alerts (top-right) =====
  /** Success messages from the base page (create, export, schedule, ...) go to the alert stack. */
  override notify(message: string): void {
    this.showAlert('success', message);
  }

  /** Shows an alert in the top-right stack. Errors stay longer; the same text is not stacked twice. */
  showAlert(kind: AlertKind, message: string, title = ALERT_TITLES[kind]): void {
    if (!message) return;
    const same = this.alerts.find((a) => a.kind === kind && a.message === message);
    if (same) this.dismissAlert(same.id);
    const alert: PageAlert = { id: ++this.alertSeq, kind, title, message, ms: kind === 'error' || kind === 'warning' ? 8000 : 5000 };
    this.alerts = [alert, ...this.alerts];
    this.alerts.slice(MAX_ALERTS).forEach((a) => this.dismissAlert(a.id));
    this.startAlertTimer(alert);
  }

  dismissAlert(id: number): void {
    const alert = this.alerts.find((a) => a.id === id);
    if (!alert) return;
    clearTimeout(alert.timer);
    this.alerts = this.alerts.filter((a) => a.id !== id);
  }

  /** Hovering an alert keeps it open; leaving restarts its timer. */
  holdAlert(alert: PageAlert, hold: boolean): void {
    clearTimeout(alert.timer);
    alert.paused = hold;
    if (!hold) this.startAlertTimer(alert);
  }

  alertIcon(kind: AlertKind): typeof Info {
    return kind === 'success' ? CircleCheck : kind === 'error' ? CircleX : kind === 'warning' ? CircleAlert : Info;
  }

  private startAlertTimer(alert: PageAlert): void {
    alert.timer = setTimeout(() => this.dismissAlert(alert.id), alert.ms);
  }

  @HostListener('document:keydown.escape')
  closeDialogs(): void {
    this.viewing = null;
    if (!this.scheduleSaving) this.scheduling = null;
    if (!this.saving) {
      this.creating = false;
      this.editing = null;
    }
  }

  private sortValue(r: StandardReport): string {
    switch (this.sortKey) {
      case 'module':
        return r.module;
      case 'period':
        return r.period.start + r.period.end;
      case 'generatedOn':
        return r.generatedOn;
      case 'nextScheduled':
        // Unscheduled reports sort last
        return r.nextScheduled ?? '9999';
      case 'frequency':
        return String(FREQUENCY_ORDER.indexOf(r.frequency));
      case 'status':
        return r.status;
      default:
        return r.name;
    }
  }
}

function apiMessage(err: HttpErrorResponse, fallback: string): string {
  if (err.status === 0) return `${fallback} The Report Analytics service is not reachable.`;
  if (err.status === 401) return 'Sign in to continue.';
  if (err.status === 403) return 'You do not have permission for standard reports.';
  const body = err.error as { message?: string; title?: string; errors?: Record<string, string[]> } | string | null;
  if (typeof body === 'string' && body) return body;
  if (body && typeof body === 'object') {
    const first = body.errors ? Object.values(body.errors).flat()[0] : '';
    return body.message || first || body.title || fallback;
  }
  return fallback;
}
