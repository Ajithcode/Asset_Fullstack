import { Component, HostListener, OnDestroy, inject } from '@angular/core';
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

type Period = '7' | '30' | '90';
type LibraryTab = 'all' | 'favorites' | 'shared' | 'mine';
type Insight = '' | 'approval' | 'failed' | 'scheduled' | 'stale';
type SeriesKey = keyof Omit<ActivityDay, 'date'>;

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const DAY = 86_400_000;
// Activity chart geometry (SVG user units)
const CW = 640;
const CH = 200;
const CP = { l: 30, r: 8, t: 10, b: 22 };

@Component({
  selector: 'app-report',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './report.html',
  // header, filters, KPI cards, menus, tables and charts come from the IoT and Asset overviews
  styleUrls: ['../iot-tracking-overview/iot-tracking-overview.css', '../iot-tracking-overview/iot-tracking-overview-panels.css', '../asset-overview/asset-overview.css', './report.css'],
})
export class Report implements OnDestroy {
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
  readonly cw = CW;
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
  private toastTimer?: ReturnType<typeof setTimeout>;
  private readonly library: LibraryReport[] = LIBRARY.map((r) => ({ ...r }));
  private readonly router = inject(Router);
  private readonly today = new Date();

  constructor() {
    this.selectedId = [...this.library].sort((a, b) => b.views - a.views)[0]?.id ?? '';
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
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
    const slot = (CW - CP.l - CP.r) / vals.length;
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
