import { AfterViewInit, Component, ElementRef, HostListener, NgZone, OnDestroy, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import * as XLSX from 'xlsx';
import {
  LucideAngularModule,
  ArrowDown,
  ArrowUp,
  CalendarDays,
  ChartColumn,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  Clock,
  Database,
  Download,
  Ellipsis,
  Eye,
  FilePlus,
  FileText,
  History,
  House,
  LayoutTemplate,
  Lightbulb,
  PieChart,
  Plus,
  Search,
  Settings,
  ShieldCheck,
  Star,
  TriangleAlert,
  Upload,
  Users,
  CalendarClock,
  CircleCheck,
  Box,
  Heart,
} from 'lucide-angular';
import {
  ACTIVITY, ActivityDay, CHANNELS, CURRENT_USER, FILE_TYPES, FileType, LIBRARY, LibraryReport, MODULES, OWNERS, PREVIEW_STATS, RECENT, SCHEDULES, SERIES, SITES,
  ScheduledReport, isoDate,
} from './report-data';
import { DrillDialog } from '../shared/drill-dialog/drill-dialog';
import { Drill, DrillColumn, DrillRow, DrillValue, pct } from '../shared/drill-dialog/drill';

type Period = '7' | '30' | '90';
type LibraryTab = 'all' | 'favorites' | 'shared' | 'mine';
type Insight = '' | 'approval' | 'failed' | 'scheduled' | 'stale';
type SeriesKey = keyof Omit<ActivityDay, 'date'>;

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const DAY = 86_400_000;
// Activity chart geometry (SVG user units); the width follows the card's width (1 unit = 1px)
const CW = 640;
const CW_MIN = 280;
const CH = 200;
const CP = { l: 30, r: 8, t: 10, b: 22 };
const TYPE_COLORS: Record<string, string> = { PDF: '#dc2626', XLS: '#15803d', CSV: '#1d4ed8' };
const DATA_COLORS: Record<string, string> = { Fresh: '#16a34a', Stale: '#dc2626' };

@Component({
  selector: 'app-report',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule, DrillDialog],
  templateUrl: './report.html',
  // header, filters, KPI cards, menus, tables and charts come from the IoT and Asset overviews
  styleUrls: ['../iot-tracking-overview/iot-tracking-overview.css', '../iot-tracking-overview/iot-tracking-overview-panels.css', '../asset-overview/asset-overview.css', './report.css'],
})
export class Report implements AfterViewInit, OnDestroy {
  readonly icons = {
    ArrowDown, ArrowUp, Box, CalendarClock, CalendarDays, ChartColumn, ChevronDown, ChevronLeft, ChevronRight, CircleCheck, Clock, Database, Download, Ellipsis, Eye, FilePlus,
    FileText, Heart, History, House, LayoutTemplate, Lightbulb, PieChart, Plus, Search, Settings, ShieldCheck, Star, TriangleAlert, Upload, Users,
  };
  readonly scheduleIcons: Record<ScheduledReport['icon'], typeof Star> = { file: FileText, alert: TriangleAlert, chart: ChartColumn, shield: ShieldCheck };
  readonly previewIcons = { blue: Box, green: Heart, red: TriangleAlert };
  readonly modules = MODULES;
  readonly moduleColor: Record<string, string> = Object.fromEntries(MODULES.map((m) => [m.name, m.color]));
  readonly sites = SITES;
  readonly owners = OWNERS;
  readonly fileTypes = FILE_TYPES;
  readonly series = SERIES;
  cw = CW;
  readonly ch = CH;
  readonly cp = CP;

  // Page filters
  period: Period = '30';
  periodOpen = false;
  module = '';
  fileType: FileType | '' = '';
  site = '';
  owner = '';

  // Activity chart
  hiddenSeries = new Set<SeriesKey>();
  hoverDay: number | null = null;
  hoverModule: string | null = null;

  // Library
  tab: LibraryTab = 'all';
  search = '';
  libModule = '';
  libType: FileType | '' = '';
  insight: Insight = '';
  page = 0;
  readonly pageSize = 6;
  menuOpen = '';
  selectedId = '';

  // Schedules
  schedules: ScheduledReport[] = SCHEDULES.map((s) => ({ ...s }));
  showAllSchedules = false;
  showAllRecent = false;

  toast = '';
  /** Drill-down popup for the clicked widget element. */
  drill: Drill | null = null;
  private toastTimer?: ReturnType<typeof setTimeout>;
  private readonly library: LibraryReport[] = LIBRARY.map((r) => ({ ...r }));
  @ViewChild('chartEl') private chartEl?: ElementRef<HTMLDivElement>;
  private chartResize?: ResizeObserver;
  private readonly router = inject(Router);
  private readonly zone = inject(NgZone);
  private readonly today = new Date();

  constructor() {
    this.selectedId = [...this.library].sort((a, b) => b.views - a.views)[0]?.id ?? '';
  }

  /** Redraws the activity chart at its card's width as the grid columns change (window, breakpoints, sidebar). */
  ngAfterViewInit(): void {
    const el = this.chartEl?.nativeElement;
    if (!el) return;
    this.chartResize = new ResizeObserver(() => {
      if (!el.clientWidth) return;
      const cw = Math.max(CW_MIN, Math.round(el.clientWidth));
      // ResizeObserver callbacks run outside Angular's zone
      if (cw !== this.cw) this.zone.run(() => (this.cw = cw));
    });
    this.chartResize.observe(el);
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
    this.chartResize?.disconnect();
  }

  // ===== Filters =====

  get periodLabel(): string {
    return `Last ${this.period} Days`;
  }

  setPeriod(v: Period): void {
    this.period = v;
    this.periodOpen = false;
    this.hoverDay = null;
  }

  /** Reports that pass the page filters. */
  get reports(): LibraryReport[] {
    return this.library.filter((r) => (!this.module || r.module === this.module) && (!this.fileType || r.type === this.fileType)
      && (!this.site || r.site === this.site) && (!this.owner || r.owner === this.owner));
  }

  /** Share of the catalogue the filters keep; scales the activity and delivery figures. */
  private get share(): number {
    return this.reports.length / this.library.length;
  }

  onFilter(): void {
    this.page = 0;
  }

  // ===== KPIs =====

  private days(offset: number): ActivityDay[] {
    const n = Number(this.period);
    return ACTIVITY.slice(Math.max(0, ACTIVITY.length - n * (offset + 1)), ACTIVITY.length - n * offset);
  }

  get kpis() {
    const list = this.reports;
    const n = Number(this.period);
    const start = isoDate(new Date(this.today.getTime() - n * DAY));
    const before = list.filter((r) => r.created < start).length;
    const generated = Math.round(this.days(0).reduce((s, d) => s + d.generated, 0) * this.share);
    const prevGenerated = Math.round(this.days(1).reduce((s, d) => s + d.generated, 0) * this.share);
    const top = [...list].sort((a, b) => b.views - a.views)[0] ?? null;
    const scheduled = list.filter((r) => r.scheduled).length;
    const change = (a: number, b: number) => (b ? Math.round(((a - b) / b) * 1000) / 10 : null);
    return {
      total: list.length,
      totalChange: change(list.length, before),
      scheduled,
      scheduledPct: list.length ? Math.round((scheduled / list.length) * 1000) / 10 : 0,
      generated,
      generatedChange: change(generated, prevGenerated),
      top,
      freshness: list.length ? Math.round((1 - list.filter((r) => r.stale).length / list.length) * 1000) / 10 : 100,
      pending: list.filter((r) => r.pendingApproval).length,
    };
  }

  // ===== Reporting activity =====

  toggleSeries(key: SeriesKey): void {
    if (this.hiddenSeries.has(key)) this.hiddenSeries.delete(key);
    else this.hiddenSeries.add(key);
  }

  get activity() {
    const k = this.share;
    const vals = this.days(0).map((d) => ({
      date: d.date, generated: Math.round(d.generated * k), viewed: Math.round(d.viewed * k), exported: Math.round(d.exported * k), scheduled: Math.round(d.scheduled * k),
    }));
    const bars = SERIES.filter((s) => s.key !== 'scheduled' && !this.hiddenSeries.has(s.key));
    const showLine = !this.hiddenSeries.has('scheduled');
    const max = Math.max(1, ...vals.flatMap((v) => [...bars.map((s) => v[s.key]), showLine ? v.scheduled : 0]));
    const step = max > 200 ? 50 : max > 80 ? 20 : max > 40 ? 10 : 5;
    const top = Math.ceil(max / step) * step;
    const plotH = CH - CP.t - CP.b;
    const slot = (this.cw - CP.l - CP.r) / vals.length;
    const groupW = slot * 0.78;
    const barW = bars.length ? groupW / bars.length : 0;
    const x = (i: number) => CP.l + slot * i + slot / 2;
    const y = (v: number) => CH - CP.b - (v / top) * plotH;
    const rects = vals.flatMap((v, i) => bars.map((s, j) => ({
      x: x(i) - groupW / 2 + j * barW, y: y(v[s.key]), w: Math.max(0.6, barW - (barW > 3 ? 0.8 : 0.2)), h: CH - CP.b - y(v[s.key]), color: s.color,
    })));
    const pts = vals.map((v, i) => ({ x: x(i), y: y(v.scheduled) }));
    const ticks = Array.from({ length: top / step + 1 }, (_, i) => ({ y: y(i * step), label: String(i * step) }));
    const every = Math.ceil(vals.length / 10);
    const labels = vals.map((v, i) => ({ i, v })).filter(({ i }) => i % every === 0).map(({ i, v }) => ({ x: x(i), label: this.fmtShort(v.date) }));
    return { vals, rects, line: showLine ? pts.map((p, i) => `${i ? 'L' : 'M'}${p.x},${p.y}`).join(' ') : '', pts: showLine && vals.length <= 31 ? pts : [], ticks, labels, slot, x };
  }

  // ===== Reports by module =====

  get byModule() {
    const list = this.reports;
    const total = list.length;
    const C = 2 * Math.PI * 46;
    let acc = 0;
    const rows = MODULES.map((m) => {
      const count = list.filter((r) => r.module === m.name).length;
      const len = total ? (count / total) * C : 0;
      const row = { ...m, count, pct: total ? Math.round((count / total) * 1000) / 10 : 0, dash: `${Math.max(0, len - (count && count < total ? 1.5 : 0))} ${C}`, offset: -acc };
      acc += len;
      return row;
    });
    return { total, rows };
  }

  moduleCount(name: string): number {
    return this.reports.filter((r) => r.module === name).length;
  }

  pickModule(name: string): void {
    this.module = this.module === name ? '' : name;
    this.onFilter();
  }

  // ===== Insights =====

  get failedDeliveries(): number {
    return Math.round(CHANNELS.reduce((s, c) => s + c.failed, 0) * this.share * 0.09);
  }

  get insights(): { key: Insight; count: number; label: string; icon: typeof Star; tone: string }[] {
    const list = this.reports;
    return [
      { key: 'approval', count: list.filter((r) => r.pendingApproval).length, label: 'reports awaiting approval', icon: FileText, tone: 'amber' },
      { key: 'failed', count: this.failedDeliveries, label: 'failed deliveries', icon: TriangleAlert, tone: 'red' },
      { key: 'scheduled', count: list.filter((r) => r.scheduled).length, label: 'active schedules', icon: CalendarDays, tone: 'blue' },
      { key: 'stale', count: list.filter((r) => r.stale).length, label: 'reports with stale data', icon: Database, tone: 'violet' },
    ];
  }

  openInsight(key: Insight): void {
    if (key === 'failed') {
      this.showToast(`${this.failedDeliveries} deliveries failed in the ${this.periodLabel.toLowerCase()}. Retry them from Export & Delivery.`);
      return;
    }
    this.insight = this.insight === key ? '' : key;
    this.tab = 'all';
    this.page = 0;
  }

  // ===== Library =====

  setTab(t: LibraryTab): void {
    this.tab = t;
    this.page = 0;
  }

  get libraryRows(): LibraryReport[] {
    const q = this.search.trim().toLowerCase();
    return this.reports
      .filter((r) => this.tab !== 'favorites' || r.favorite)
      .filter((r) => this.tab !== 'shared' || r.sharedWithMe)
      .filter((r) => this.tab !== 'mine' || r.owner === CURRENT_USER)
      .filter((r) => this.insight !== 'approval' || r.pendingApproval)
      .filter((r) => this.insight !== 'scheduled' || r.scheduled)
      .filter((r) => this.insight !== 'stale' || r.stale)
      .filter((r) => (!this.libModule || r.module === this.libModule) && (!this.libType || r.type === this.libType))
      .filter((r) => !q || r.name.toLowerCase().includes(q) || r.id.toLowerCase().includes(q) || r.owner.toLowerCase().includes(q))
      .sort((a, b) => b.updated.localeCompare(a.updated));
  }

  get pageRows(): LibraryReport[] {
    return this.libraryRows.slice(this.page * this.pageSize, (this.page + 1) * this.pageSize);
  }

  get pages(): number {
    return Math.max(1, Math.ceil(this.libraryRows.length / this.pageSize));
  }

  get insightLabel(): string {
    return { '': '', approval: 'Awaiting approval', failed: '', scheduled: 'Scheduled', stale: 'Stale data' }[this.insight];
  }

  toggleFavorite(r: LibraryReport): void {
    r.favorite = !r.favorite;
  }

  select(r: LibraryReport): void {
    this.selectedId = r.id;
  }

  get selected(): LibraryReport | null {
    return this.library.find((r) => r.id === this.selectedId) ?? null;
  }

  previewStats(r: LibraryReport) {
    return PREVIEW_STATS[r.module] ?? [];
  }

  // ===== Performance, delivery, schedules, recent =====

  get topViewed(): LibraryReport[] {
    return [...this.reports].sort((a, b) => b.views - a.views).slice(0, 5);
  }

  get topMax(): number {
    return Math.max(1, ...this.topViewed.map((r) => r.views));
  }

  get delivery() {
    const k = this.share;
    const C = 2 * Math.PI * 16;
    const rows = CHANNELS.map((c) => {
      const ok = Math.round(c.ok * k);
      const all = ok + Math.round(c.failed * k);
      const rate = all ? Math.round((ok / all) * 1000) / 10 : 0;
      return { ...c, ok, all, rate, dash: `${(rate / 100) * C} ${C}` };
    });
    const ok = rows.reduce((s, r) => s + r.ok, 0);
    const all = rows.reduce((s, r) => s + r.all, 0);
    const rate = all ? Math.round((ok / all) * 1000) / 10 : 0;
    const CB = 2 * Math.PI * 30;
    return { rows, ok, all, rate, dash: `${(rate / 100) * CB} ${CB}` };
  }

  get scheduleRows(): ScheduledReport[] {
    const list = this.schedules.filter((s) => !this.module || s.module === this.module);
    return this.showAllSchedules ? list : list.slice(0, 4);
  }

  toggleSchedule(s: ScheduledReport): void {
    s.active = !s.active;
    this.showToast(`${s.name} ${s.active ? 'resumed' : 'paused'}.`);
  }

  get recent() {
    const list = [...RECENT].sort((a, b) => b.at.localeCompare(a.at));
    return this.showAllRecent ? list : list.slice(0, 3);
  }

  // ===== Drill-down popup =====

  private get scope(): string {
    return `${this.periodLabel} · ${[this.module, this.fileType, this.site, this.owner].filter(Boolean).join(' · ') || 'All modules, types, sites and owners'}`;
  }

  private reportColumns(...extra: DrillColumn[]): DrillColumn[] {
    return [
      { key: 'id', label: 'Report ID' }, { key: 'name', label: 'Report Name' }, { key: 'module', label: 'Module' }, { key: 'site', label: 'Site' },
      { key: 'owner', label: 'Owner' }, { key: 'type', label: 'Type', pill: TYPE_COLORS }, ...extra, { key: 'views', label: 'Views', num: true }, { key: 'updated', label: 'Last Updated' },
    ];
  }

  private reportRows(list: LibraryReport[], extra: (r: LibraryReport) => Record<string, DrillValue> = () => ({})): DrillRow[] {
    return list.map((r) => ({
      cells: { id: r.id, name: r.name, module: r.module, site: r.site, owner: r.owner, type: r.type, views: r.views, updated: r.updated.replace('T', ' ').slice(0, 16), ...extra(r) },
      open: () => this.reportDrill(r),
    }));
  }

  private reportDrill(r: LibraryReport): Drill {
    return {
      title: r.name,
      subtitle: `${r.id} · ${r.module} · ${r.site}`,
      stats: [
        { label: 'Views', value: r.views.toLocaleString() },
        { label: 'Type', value: r.type, color: TYPE_COLORS[r.type] },
        { label: 'Approval', value: r.pendingApproval ? 'Pending' : 'Approved', color: r.pendingApproval ? '#d97706' : '#16a34a' },
        { label: 'Data', value: r.stale ? 'Stale' : 'Fresh', color: DATA_COLORS[r.stale ? 'Stale' : 'Fresh'] },
        ...this.previewStats(r).map((s) => ({ label: s.label, value: s.value })),
      ],
      fields: [
        { label: 'Owner', value: r.owner }, { label: 'Scheduled', value: r.scheduled ? 'Yes' : 'No' },
        { label: 'Created', value: this.fmtDate(r.created.slice(0, 10)) }, { label: 'Last Updated', value: this.fmtDateTime(r.updated) },
        { label: 'Favorite', value: r.favorite ? 'Yes' : 'No' }, { label: 'Shared with me', value: r.sharedWithMe ? 'Yes' : 'No' },
      ],
      actions: [
        { label: `Download ${r.type}`, run: () => this.download(r) },
        { label: 'Show in Preview', run: () => this.select(r) },
        { label: 'View Report', run: () => this.viewReport(r) },
      ],
    };
  }

  private dayDrill(v: ActivityDay): Drill {
    const updated = this.reports.filter((r) => r.updated.startsWith(v.date)).sort((a, b) => b.updated.localeCompare(a.updated));
    return {
      title: `Reporting Activity · ${this.fmtDate(v.date)}`, subtitle: this.scope,
      stats: SERIES.map((s) => ({ label: s.label, value: v[s.key], color: s.color })),
      tableTitle: 'Reports updated this day', columns: this.reportColumns(), rows: this.reportRows(updated), empty: 'No reports were updated this day.',
    };
  }

  private channelRows() {
    const k = this.share;
    return CHANNELS.map((c) => {
      const ok = Math.round(c.ok * k);
      const failed = Math.round(c.failed * k);
      return { channel: c.channel, color: c.color, ok, failed, recent: Math.round(c.failed * k * 0.09), rate: pct(ok, ok + failed) };
    });
  }

  openKpi(kind: 'total' | 'scheduled' | 'generated' | 'freshness' | 'pending'): void {
    const list = this.reports;
    const k = this.kpis;
    if (kind === 'total') {
      this.drill = {
        title: 'Total Reports', subtitle: this.scope,
        stats: [{ label: 'Reports', value: k.total }, ...FILE_TYPES.map((t) => ({ label: t, value: list.filter((r) => r.type === t).length, color: TYPE_COLORS[t] }))],
        columns: this.reportColumns(), rows: this.reportRows(list),
      };
    } else if (kind === 'scheduled') {
      const sch = list.filter((r) => r.scheduled);
      this.drill = {
        title: 'Scheduled Reports', subtitle: this.scope,
        stats: [{ label: 'Scheduled', value: k.scheduled, color: '#16a34a' }, { label: 'Of all reports', value: `${k.scheduledPct}%` }, { label: 'Active schedules', value: this.schedules.filter((s) => s.active).length }],
        columns: this.reportColumns(), rows: this.reportRows(sch),
        actions: [{ label: 'Show in library', run: () => this.showInsight('scheduled') }],
      };
    } else if (kind === 'generated') {
      const vals = this.activity.vals;
      const sum = (key: SeriesKey) => vals.reduce((s, v) => s + v[key], 0);
      this.drill = {
        title: 'Reports Generated', subtitle: this.scope,
        stats: SERIES.map((s) => ({ label: s.label, value: sum(s.key).toLocaleString(), color: s.color })),
        tableTitle: 'Per day',
        columns: [{ key: 'date', label: 'Date' }, ...SERIES.map((s) => ({ key: s.key, label: s.label, num: true }))],
        rows: [...vals].reverse().map((v) => ({ cells: { date: v.date, generated: v.generated, viewed: v.viewed, exported: v.exported, scheduled: v.scheduled }, open: () => this.dayDrill(v) })),
      };
    } else if (kind === 'freshness') {
      this.drill = {
        title: 'Data Freshness', subtitle: `Reports whose source data is within SLA · ${this.scope}`,
        stats: [{ label: 'Freshness', value: `${k.freshness}%` }, { label: 'Fresh', value: list.filter((r) => !r.stale).length, color: '#16a34a' }, { label: 'Stale', value: list.filter((r) => r.stale).length, color: '#dc2626' }, { label: 'Target', value: '95%' }],
        columns: this.reportColumns({ key: 'data', label: 'Data', pill: DATA_COLORS }),
        rows: this.reportRows([...list].sort((a, b) => Number(b.stale) - Number(a.stale)), (r) => ({ data: r.stale ? 'Stale' : 'Fresh' })),
      };
    } else {
      const pending = list.filter((r) => r.pendingApproval);
      this.drill = {
        title: 'Pending Approvals', subtitle: this.scope,
        stats: [{ label: 'Awaiting review', value: k.pending, color: '#d97706' }, { label: 'Of all reports', value: `${pct(k.pending, k.total)}%` }],
        columns: this.reportColumns(), rows: this.reportRows(pending), empty: 'No reports awaiting approval.',
        actions: [{ label: 'Show in library', run: () => this.showInsight('approval') }],
      };
    }
  }

  openDay(i: number): void {
    const v = this.activity.vals[i];
    if (v) this.drill = this.dayDrill(v);
  }

  openModule(name: string): void {
    const list = this.reports.filter((r) => r.module === name);
    const on = this.module === name;
    this.drill = {
      title: `${name} Reports`, subtitle: `Reports by module · ${this.scope}`,
      stats: [
        { label: 'Reports', value: list.length, color: this.moduleColor[name] }, { label: 'Of all reports', value: `${pct(list.length, this.reports.length)}%` },
        { label: 'Views', value: list.reduce((s, r) => s + r.views, 0).toLocaleString() }, { label: 'Schedules', value: this.schedules.filter((s) => s.module === name).length },
      ],
      columns: this.reportColumns(), rows: this.reportRows([...list].sort((a, b) => b.views - a.views)),
      actions: [{ label: on ? 'Clear module filter' : `Filter dashboard to ${name}`, run: () => this.pickModule(name) }],
    };
  }

  openInsightDrill(key: Insight): void {
    if (key === 'failed') {
      this.openDelivery('', true);
      return;
    }
    const i = this.insights.find((x) => x.key === key);
    const list = this.reports.filter((r) => (key === 'approval' ? r.pendingApproval : key === 'scheduled' ? r.scheduled : r.stale));
    this.drill = {
      title: (i?.label ?? '').replace(/^./, (c) => c.toUpperCase()), subtitle: this.scope,
      stats: [{ label: 'Reports', value: list.length }, { label: 'Of all reports', value: `${pct(list.length, this.reports.length)}%` }],
      columns: this.reportColumns(), rows: this.reportRows(list),
      actions: [{ label: this.insight === key ? 'Clear library filter' : 'Show in library', run: () => this.openInsight(key) }],
    };
  }

  /** Export & Delivery: one channel, or all channels (failed: the failed deliveries in the period). */
  openDelivery(channel = '', failed = false): void {
    const rows = this.channelRows();
    const c = rows.find((r) => r.channel === channel);
    const ok = c ? c.ok : rows.reduce((s, r) => s + r.ok, 0);
    const bad = c ? c.failed : rows.reduce((s, r) => s + r.failed, 0);
    this.drill = {
      title: failed ? 'Failed Deliveries' : c ? `${c.channel} Delivery` : 'Export & Delivery',
      subtitle: this.scope,
      stats: failed
        ? [{ label: `Failed in the ${this.periodLabel.toLowerCase()}`, value: this.failedDeliveries, color: '#dc2626' }, { label: 'Failed overall', value: bad.toLocaleString() }]
        : [{ label: 'Successful', value: ok.toLocaleString(), color: '#16a34a' }, { label: 'Failed', value: bad.toLocaleString(), color: '#dc2626' }, { label: 'Success rate', value: `${pct(ok, ok + bad)}%` }],
      tableTitle: 'By channel',
      columns: [
        { key: 'channel', label: 'Channel' }, { key: 'ok', label: 'Successful', num: true }, { key: 'failed', label: 'Failed', num: true },
        ...(failed ? [{ key: 'recent', label: 'Failed in period', num: true }] : []), { key: 'rate', label: 'Success %', num: true },
      ],
      rows: rows.map((r) => ({ cells: { channel: r.channel, ok: r.ok, failed: r.failed, recent: r.recent, rate: r.rate } })),
    };
  }

  openSchedule(s: ScheduledReport): void {
    const list = this.reports.filter((r) => r.module === s.module && r.scheduled);
    this.drill = {
      title: s.name, subtitle: `${s.module} · ${s.cadence}`,
      stats: [
        { label: 'Status', value: s.active ? 'Active' : 'Paused', color: s.active ? '#16a34a' : '#6b7280' },
        { label: 'Next run', value: s.active ? this.fmtDate(s.nextRun) : '-' }, { label: 'Recipients', value: s.recipients },
      ],
      tableTitle: `Scheduled ${s.module} reports`, columns: this.reportColumns(), rows: this.reportRows(list), empty: 'No scheduled reports in this module.',
      actions: [{ label: s.active ? 'Pause schedule' : 'Resume schedule', run: () => this.toggleSchedule(s) }],
    };
  }

  openRecent(e: { owner: string; action: string; report: string; at: string }): void {
    // the report itself first, then the user's other reports
    const report = this.library.filter((r) => r.name === e.report);
    const owned = this.library.filter((r) => r.owner === e.owner && r.name !== e.report).sort((a, b) => b.updated.localeCompare(a.updated));
    this.drill = {
      title: `${e.action} "${e.report}"`, subtitle: `${e.owner} · ${this.ago(e.at)}`,
      fields: [
        { label: 'User', value: e.owner }, { label: 'Action', value: e.action },
        { label: 'Report', value: e.report }, { label: 'When', value: this.fmtDateTime(e.at) },
      ],
      tableTitle: `Related reports (${e.report} and other reports by ${e.owner})`, columns: this.reportColumns(), rows: this.reportRows([...report, ...owned]),
    };
  }

  openReport(r: LibraryReport): void {
    this.select(r);
    this.drill = this.reportDrill(r);
  }

  private showInsight(key: Insight): void {
    if (this.insight !== key) this.openInsight(key);
  }

  // ===== Actions =====

  go(path: string): void {
    this.router.navigate([path]);
  }

  viewReport(r: LibraryReport): void {
    r.views++;
    this.menuOpen = '';
    this.router.navigate(['/report']);
  }

  /** Downloads the report's summary in its own format (PDF reports go out as a printable sheet). */
  download(r: LibraryReport): void {
    this.menuOpen = '';
    const rows = [
      { Field: 'Report ID', Value: r.id }, { Field: 'Report Name', Value: r.name }, { Field: 'Module', Value: r.module }, { Field: 'Site', Value: r.site },
      { Field: 'Owner', Value: r.owner }, { Field: 'Last Updated', Value: this.fmtDateTime(r.updated) }, { Field: 'Views', Value: r.views },
      ...this.previewStats(r).map((s) => ({ Field: s.label, Value: s.value })),
    ];
    const book = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet(rows), 'Report');
    const file = r.name.replace(/[^\w]+/g, '_');
    if (r.type === 'CSV') XLSX.writeFile(book, `${file}.csv`, { bookType: 'csv' });
    else if (r.type === 'XLS') XLSX.writeFile(book, `${file}.xlsx`);
    else this.printSummary(r, rows);
  }

  private printSummary(r: LibraryReport, rows: { Field: string; Value: string | number }[]): void {
    const w = window.open('', '_blank', 'width=720,height=640');
    if (!w) {
      this.showToast('Allow pop-ups to save the PDF.');
      return;
    }
    const esc = (v: string | number) => String(v).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c] as string);
    w.document.write(`<html><head><title>${esc(r.name)}</title><style>body{font-family:sans-serif;padding:24px}h1{font-size:20px;color:#1e1b6b}td{padding:6px 12px;border-bottom:1px solid #eee}td:first-child{font-weight:600}</style></head>
      <body><h1>${esc(r.name)}</h1><table>${rows.map((x) => `<tr><td>${esc(x.Field)}</td><td>${esc(x.Value)}</td></tr>`).join('')}</table></body></html>`);
    w.document.close();
    w.focus();
    w.print();
  }

  exportAll(): void {
    const book = XLSX.utils.book_new();
    const k = this.kpis;
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet([
      { Metric: 'Period', Value: this.periodLabel },
      { Metric: 'Filters', Value: `Module: ${this.module || 'All'}; Type: ${this.fileType || 'All'}; Site: ${this.site || 'All'}; Owner: ${this.owner || 'All'}` },
      { Metric: 'Total Reports', Value: k.total }, { Metric: 'Scheduled Reports', Value: k.scheduled }, { Metric: 'Reports Generated', Value: k.generated },
      { Metric: 'Most Viewed Report', Value: k.top?.name ?? '-' }, { Metric: 'Data Freshness %', Value: k.freshness }, { Metric: 'Pending Approvals', Value: k.pending },
    ]), 'Summary');
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet(this.reports.map((r) => ({
      'Report ID': r.id, 'Report Name': r.name, Module: r.module, Site: r.site, Owner: r.owner, Type: r.type, Views: r.views, 'Last Updated': this.fmtDateTime(r.updated),
      Scheduled: r.scheduled ? 'Yes' : 'No', 'Pending Approval': r.pendingApproval ? 'Yes' : 'No',
    }))), 'Report Library');
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet(this.activity.vals.map((v) => ({
      Date: v.date, Generated: v.generated, Viewed: v.viewed, Exported: v.exported, Scheduled: v.scheduled,
    }))), 'Activity');
    XLSX.writeFile(book, `Reports_${isoDate(this.today)}.xlsx`);
  }

  // ===== Formatting =====

  initials(name: string): string {
    return name.split(' ').map((p) => p[0]).join('').slice(0, 2).toUpperCase();
  }

  avatarTone(name: string): string {
    return 'av--' + (OWNERS.indexOf(name) % 3 + 3) % 3;
  }

  fmtShort(iso: string): string {
    return `${MONTHS[Number(iso.slice(5, 7)) - 1]} ${iso.slice(8, 10)}`;
  }

  fmtDate(iso: string): string {
    return `${MONTHS[Number(iso.slice(5, 7)) - 1]} ${Number(iso.slice(8, 10))}, ${iso.slice(0, 4)}`;
  }

  fmtDateTime(v: string): string {
    return `${this.fmtDate(v.slice(0, 10))} ${v.slice(11, 16)}`;
  }

  ago(v: string): string {
    const h = Math.max(0, Math.round((this.today.getTime() - new Date(v).getTime()) / 3_600_000));
    return h < 1 ? 'Just now' : h < 24 ? `${h} hour${h === 1 ? '' : 's'} ago` : `${Math.round(h / 24)} day${h < 36 ? '' : 's'} ago`;
  }

  toggleMenu(name: string, event: Event): void {
    event.stopPropagation();
    this.menuOpen = this.menuOpen === name ? '' : name;
    this.periodOpen = false;
  }

  @HostListener('document:click')
  closeMenus(): void {
    this.menuOpen = '';
    this.periodOpen = false;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeMenus();
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 4000);
  }
}
