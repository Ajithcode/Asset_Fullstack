import { Component, ElementRef, HostListener, NgZone, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  LucideAngularModule,
  ArrowDown,
  ArrowLeftRight,
  ArrowUp,
  ArrowUpDown,
  Boxes,
  CalendarDays,
  ChevronLeft,
  ChevronRight,
  Clock,
  Download,
  EllipsisVertical,
  List,
  Map as MapIcon,
  MapPin,
  MapPinX,
  Plus,
  Search,
  Table,
  X,
  Box,
} from 'lucide-angular';
import { ASSET_TYPE_ICONS, pushToast } from 'shared-ui';
import { LocationHistory } from './location-history';
import { LocationEventDto, LocationHistoryService, MovementType } from './location-history.service';
import { SITE_POINTS, ZONE_POINTS } from './location-overview.data';
import { LocationOverviewMap, MapZone } from './location-overview-map';
import { AssetListItemDto, toAssetRecord } from '../asset-maintenance/asset-lookup.service';
import { ExportMenu, ExportRequest } from 'shared-ui';

export type LocSource = 'BLE' | 'Manual' | 'QR Scan' | 'GPS' | 'RFID';
const SOURCES: LocSource[] = ['BLE', 'Manual', 'QR Scan', 'GPS', 'RFID'];
const SOURCE_COLORS: Record<LocSource, string> = { BLE: '#7c5cf5', Manual: '#2196f3', 'QR Scan': '#22a06b', GPS: '#f5b83d', RFID: '#ec4899' };

/** One location event with its asset and the zone the asset came from. */
interface EventRow {
  id: string;
  eventId: string;
  at: Date;
  tag: string;
  name: string;
  zone: string;
  prevZone: string;
  eventType: string;
  source: LocSource;
  details: string;
  by: string;
  asset: AssetListItemDto | null;
}

/** One asset with its latest location (Asset Summary tab). */
interface SummaryRow {
  tag: string;
  name: string;
  type: string;
  category: string;
  site: string;
  status: string;
  icon: string;
  current: string;
  previous: string;
  last: Date;
  source: LocSource;
  changes: number;
  asset: AssetListItemDto | null;
}

interface Kpi {
  value: string;
  delta: number | null;
  note: string;
}

interface MovementForm {
  tag: string;
  location: string;
  movement: MovementType;
  at: string;
  source: LocSource;
  remarks: string;
}

const DAY = 86_400_000;
const pad = (n: number) => String(n).padStart(2, '0');
const stamp = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
const startOfDay = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate());

/** Source of an event: named in its details (Record Movement writes "remarks (source)"), else manual when a user recorded it, else the IoT tags (BLE). */
function sourceOf(e: LocationEventDto): LocSource {
  const t = `${e.details ?? ''}`.toLowerCase();
  if (/\bble\b|bluetooth|beacon/.test(t)) return 'BLE';
  if (/\bqr\b|barcode/.test(t)) return 'QR Scan';
  if (/rfid/.test(t)) return 'RFID';
  if (/\bgps\b/.test(t)) return 'GPS';
  if (/manual/.test(t)) return 'Manual';
  return e.createdBy && !/^system$/i.test(e.createdBy) ? 'Manual' : 'BLE';
}

function pct(cur: number, prev: number): number | null {
  if (!prev) return cur ? null : 0;
  return Math.round(((cur - prev) / prev) * 1000) / 10;
}

function fmtDwell(ms: number): string {
  const min = Math.round(ms / 60_000);
  const d = Math.floor(min / 1440);
  const h = Math.floor((min % 1440) / 60);
  return d ? `${d}d ${h}h` : h ? `${h}h ${min % 60}m` : `${min}m`;
}

/** Assets › Location History: every asset's latest location and all location-change events; ?tag= opens one asset's history. */
@Component({
  selector: 'app-location-overview',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule, LocationHistory, LocationOverviewMap, ExportMenu],
  templateUrl: './location-overview.html',
  styleUrls: ['./location-history.css', './location-history-table.css', './location-overview.css'],
})
export class LocationOverview implements OnInit, OnDestroy {
  readonly icons = {
    ArrowDown, ArrowLeftRight, ArrowUp, ArrowUpDown, Box, Boxes, CalendarDays, ChevronLeft, ChevronRight, Clock, Download, EllipsisVertical, List, MapIcon, MapPin,
    MapPinX, Plus, Search, Table, X,
  };
  readonly typeIcons = ASSET_TYPE_ICONS as Record<string, typeof Box>;
  readonly sources = SOURCES;
  readonly sourceColors = SOURCE_COLORS;
  readonly pageSizes = [10, 25, 50];
  readonly ranges = [
    { value: 1, label: 'Last 24 Hours' },
    { value: 7, label: 'Last 7 Days' },
    { value: 30, label: 'Last 30 Days' },
  ];

  private readonly router = inject(Router);
  private readonly api = inject(LocationHistoryService);

  /** ?tag= shows that asset's history instead of the overview. */
  tag = '';
  saving = false;

  loading = false;

  /** Location events (Tracking service) and registered assets (Asset API). */
  private raw: LocationEventDto[] = [];
  private events: EventRow[] = [];
  private assets: AssetListItemDto[] = [];

  // Filters
  /** Days; the default is the last 24 hours (the movement API allows at most 31 days). */
  range = 1;
  site = '';
  category = '';
  type = '';
  zone = '';
  source: LocSource | '' = '';
  status = '';
  search = '';

  tab: 'summary' | 'events' = 'summary';
  view: 'table' | 'map' = 'table';
  sortKey: 'tag' | 'last' = 'last';
  sortDir: 1 | -1 = -1;
  pageSize = 10;
  page = 0;
  selected = new Set<string>();
  menuOpen = '';

  form: MovementForm | null = null;
  formError = '';
  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  /** Derived lists, rebuilt when the data or a filter changes (not on every change detection). */
  filteredEvents: EventRow[] = [];
  summary: SummaryRow[] = [];
  /** Map view: a pin per current location (rebuilt with the filters, so the map only redraws when they change). */
  mapZones: MapZone[] = [];
  readonly mapSites = SITE_POINTS;
  kpis: Kpi[] = [];
  trend = { points: '', area: '', dots: [] as { x: number; y: number; label: string; n: number }[], ticks: [] as { x: number; label: string }[], yTicks: [] as { y: number; label: number }[] };
  donut: { source: LocSource; n: number; pct: number; dash: string; offset: number }[] = [];

  /** Trend chart drawing size; the height follows the card's height so the chart fills it. */
  chart = { w: 320, h: 170, left: 30, right: 8, top: 10, bottom: 24 };
  private trendArgs: [number, number, number] = [30, 0, 0];
  private trendResize?: ResizeObserver;
  private readonly ngZone = inject(NgZone);

  @ViewChild('trendBox') set trendBox(el: ElementRef<HTMLDivElement> | undefined) {
    this.trendResize?.disconnect();
    if (!el || typeof ResizeObserver === 'undefined') return;
    this.trendResize = new ResizeObserver(([entry]) => {
      const { width, height } = entry.contentRect;
      if (!width || !height) return;
      const h = Math.max(150, Math.round((height * this.chart.w) / width));
      if (h === this.chart.h) return;
      this.ngZone.run(() => {
        this.chart = { ...this.chart, h };
        this.buildTrend(...this.trendArgs);
      });
    });
    this.trendResize.observe(el.nativeElement);
  }

  constructor() {
    inject(ActivatedRoute)
      .queryParamMap.pipe(takeUntilDestroyed())
      .subscribe((p) => (this.tag = p.get('tag') ?? ''));
  }

  ngOnInit(): void {
    this.reload();
  }

  ngOnDestroy(): void {
    this.trendResize?.disconnect();
    clearTimeout(this.toastTimer);
  }

  onRange(): void {
    this.reload();
  }

  reload(): void {
    this.loading = true;
    this.api.overview(this.range).subscribe({
      next: ({ assets, events }) => {
        this.assets = assets;
        this.raw = events;
        this.loading = false;
        this.rebuild();
      },
      error: () => {
        this.loading = false;
        this.raw = [];
        this.rebuild();
        this.showToast('Could not load the location events.');
      },
    });
  }

  private rebuild(): void {
    this.events = this.toRows(this.raw, this.assets);
    this.refresh();
  }

  private toRows(events: LocationEventDto[], assets: AssetListItemDto[]): EventRow[] {
    const byTag = new Map(assets.filter((a) => a.assetTag).map((a) => [a.assetTag!.toUpperCase(), a]));
    const sorted = [...events].filter((e) => e.assetId).sort((a, b) => a.eventTime.localeCompare(b.eventTime));
    const lastZone = new Map<string, string>();
    const rows = sorted.map((e) => {
      const key = e.assetId.toUpperCase();
      const asset = byTag.get(key) ?? null;
      const row: EventRow = {
        id: e.id,
        eventId: e.eventId,
        at: new Date(e.eventTime),
        tag: asset?.assetTag ?? e.assetId,
        name: asset?.assetName || e.assetName || e.assetId,
        zone: e.zoneName || '—',
        prevZone: lastZone.get(key) ?? '—',
        eventType: e.eventType,
        source: sourceOf(e),
        details: e.details ?? '',
        by: e.createdBy || 'System',
        asset,
      };
      lastZone.set(key, row.zone);
      return row;
    });
    return rows.reverse();
  }

  // ===== Filter options =====

  private uniq(values: (string | null | undefined)[]): string[] {
    return [...new Set(values.filter((v): v is string => !!v && v !== '—'))].sort((a, b) => a.localeCompare(b));
  }

  get siteOptions(): string[] {
    return this.uniq(this.events.map((e) => e.asset?.siteName));
  }
  get categoryOptions(): string[] {
    return this.uniq(this.events.map((e) => e.asset?.category));
  }
  get typeOptions(): string[] {
    return this.uniq(this.events.map((e) => e.asset?.assetType));
  }
  get zoneOptions(): string[] {
    return this.uniq(this.events.map((e) => e.zone));
  }
  get statusOptions(): string[] {
    return this.uniq(this.events.map((e) => e.asset?.status || 'Active'));
  }
  get assetOptions(): { tag: string; name: string }[] {
    const map = new Map<string, string>();
    for (const a of this.assets) if (a.assetTag) map.set(a.assetTag, a.assetName ?? a.assetTag);
    for (const e of this.events) if (!map.has(e.tag)) map.set(e.tag, e.name);
    return [...map].map(([tag, name]) => ({ tag, name })).sort((a, b) => a.tag.localeCompare(b.tag));
  }

  get filtersActive(): boolean {
    return !!(this.site || this.category || this.type || this.zone || this.source || this.status || this.search || this.range !== 1);
  }

  resetFilters(): void {
    this.site = this.category = this.type = this.zone = this.status = this.search = '';
    this.source = '';
    const reload = this.range !== 1;
    this.range = 1;
    if (reload) this.reload();
    else this.refresh();
  }

  // ===== Derived data =====

  private matches(e: EventRow, q: string): boolean {
    const a = e.asset;
    return (
      (!this.site || a?.siteName === this.site) &&
      (!this.category || a?.category === this.category) &&
      (!this.type || a?.assetType === this.type) &&
      (!this.zone || e.zone === this.zone) &&
      (!this.source || e.source === this.source) &&
      (!this.status || (a?.status || 'Active') === this.status) &&
      (!q || `${e.tag} ${e.name} ${e.zone} ${e.prevZone}`.toLowerCase().includes(q))
    );
  }

  /** Rebuilds the tables, KPIs and charts from the events and the filters. */
  refresh(): void {
    this.page = 0;
    this.selected.clear();
    const now = Date.now();
    const q = this.search.trim().toLowerCase();
    const days = this.range;
    const from = days === 1 ? now - DAY : startOfDay(new Date(now - (days - 1) * DAY)).getTime();
    const prevFrom = days ? from - days * DAY : 0;
    const scoped = this.events.filter((e) => this.matches(e, q));
    this.filteredEvents = scoped.filter((e) => e.at.getTime() >= from);
    const previous = days ? scoped.filter((e) => e.at.getTime() >= prevFrom && e.at.getTime() < from) : [];

    // Asset summary: latest event per asset in range (events are newest first)
    const seen = new Map<string, SummaryRow>();
    for (const e of this.filteredEvents) {
      const s = seen.get(e.tag);
      if (s) {
        s.changes++;
        continue;
      }
      const rec = e.asset ? toAssetRecord(e.asset) : null;
      seen.set(e.tag, {
        tag: e.tag,
        name: e.name,
        type: e.asset?.assetType || '—',
        category: e.asset?.category || '—',
        site: e.asset?.siteName || '—',
        status: e.asset?.status || 'Active',
        icon: rec?.icon ?? 'transformer',
        current: e.zone,
        previous: e.prevZone,
        last: e.at,
        source: e.source,
        changes: 1,
        asset: e.asset,
      });
    }
    this.summary = [...seen.values()];
    this.sortSummary();

    // KPIs
    const assetsNow = this.summary.length;
    const assetsPrev = new Set(previous.map((e) => e.tag)).size;
    const today = startOfDay(new Date(now)).getTime();
    const movedToday = new Set(scoped.filter((e) => e.at.getTime() >= today).map((e) => e.tag)).size;
    const movedYesterday = new Set(scoped.filter((e) => e.at.getTime() >= today - DAY && e.at.getTime() < today).map((e) => e.tag)).size;
    // Dwell: time between consecutive location changes of an asset
    const gaps: number[] = [];
    const lastAt = new Map<string, number>();
    for (const e of this.filteredEvents) {
      const later = lastAt.get(e.tag);
      if (later !== undefined) gaps.push(later - e.at.getTime());
      lastAt.set(e.tag, e.at.getTime());
    }
    const avgDwell = gaps.length ? gaps.reduce((a, b) => a + b, 0) / gaps.length : 0;
    const unverified = this.summary.filter((s) => this.isUnverified(s)).length;
    const vs = days ? (days === 1 ? 'vs. previous 24 hours' : `vs. previous ${days} days`) : 'all recorded events';
    this.kpis = [
      { value: assetsNow.toLocaleString(), delta: days ? pct(assetsNow, assetsPrev) : null, note: vs },
      { value: this.filteredEvents.length.toLocaleString(), delta: days ? pct(this.filteredEvents.length, previous.length) : null, note: vs },
      { value: movedToday.toLocaleString(), delta: pct(movedToday, movedYesterday), note: 'vs. yesterday' },
      { value: gaps.length ? fmtDwell(avgDwell) : '—', delta: null, note: '(per location)' },
      { value: unverified.toLocaleString(), delta: null, note: 'not in registry or zone mismatch' },
    ];

    this.buildMapZones();
    this.buildTrend(days, from, now);
    this.buildDonut();
  }

  /** Latest tracked zone differs from the registered location, or the tag is not a registered asset. */
  isUnverified(s: SummaryRow): boolean {
    const a = s.asset;
    if (!a) return true;
    const registered = [a.zoneName, a.roomName, a.floorName, a.buildingName, a.locationPath].filter(Boolean).map((v) => v!.toLowerCase());
    if (!registered.length) return false;
    const cur = s.current.toLowerCase();
    return !registered.some((r) => r.includes(cur) || cur.includes(r));
  }

  private buildTrend(days: number, from: number, now: number): void {
    this.trendArgs = [days, from, now];
    const c = this.chart;
    // All Time: the span of the data, at most a year
    let start = from;
    if (!days) {
      const oldest = this.filteredEvents.length ? this.filteredEvents[this.filteredEvents.length - 1].at.getTime() : now;
      start = startOfDay(new Date(Math.max(oldest, now - 364 * DAY))).getTime();
    }
    const n = Math.max(1, Math.round((startOfDay(new Date(now)).getTime() - start) / DAY) + 1);
    // Group by day, or by week past 90 days
    const bucket = n > 90 ? 7 : 1;
    const count = Math.ceil(n / bucket);
    const counts = new Array<number>(count).fill(0);
    for (const e of this.filteredEvents) {
      const i = Math.floor(Math.floor((e.at.getTime() - start) / DAY) / bucket);
      if (i >= 0 && i < count) counts[i]++;
    }
    const max = Math.max(4, ...counts);
    const step = Math.pow(10, Math.floor(Math.log10(max / 4)));
    const yStep = [1, 2, 5, 10].map((m) => m * step).find((s) => max / s <= 4) ?? step * 10;
    const yMax = Math.ceil(max / yStep) * yStep;
    const iw = c.w - c.left - c.right;
    const ih = c.h - c.top - c.bottom;
    const x = (i: number) => c.left + (count === 1 ? iw / 2 : (i / (count - 1)) * iw);
    const y = (v: number) => c.top + ih - (v / yMax) * ih;
    const label = (i: number) => new Date(start + i * bucket * DAY).toLocaleDateString('en-GB', { day: '2-digit', month: 'short' });
    const dots = counts.map((v, i) => ({ x: x(i), y: y(v), n: v, label: label(i) }));
    const pts = dots.map((d) => `${d.x.toFixed(1)},${d.y.toFixed(1)}`).join(' ');
    const tickEvery = Math.max(1, Math.ceil(count / 8));
    this.trend = {
      points: pts,
      area: count ? `${x(0).toFixed(1)},${y(0)} ${pts} ${x(count - 1).toFixed(1)},${y(0)}` : '',
      dots: count <= 45 ? dots : [],
      ticks: dots.filter((_, i) => i % tickEvery === 0).map((d) => ({ x: d.x, label: d.label })),
      yTicks: Array.from({ length: yMax / yStep + 1 }, (_, i) => ({ y: y(i * yStep), label: i * yStep })),
    };
  }

  private buildDonut(): void {
    const total = this.filteredEvents.length;
    const circ = 2 * Math.PI * 40;
    let offset = 0;
    this.donut = SOURCES.map((source) => {
      const n = this.filteredEvents.filter((e) => e.source === source).length;
      const len = total ? (n / total) * circ : 0;
      const seg = { source, n, pct: total ? Math.round((n / total) * 1000) / 10 : 0, dash: `${Math.max(0, len - (len > 2 ? 1.5 : 0))} ${circ}`, offset: -offset };
      offset += len;
      return seg;
    }).sort((a, b) => b.n - a.n);
  }

  get totalChanges(): number {
    return this.filteredEvents.length;
  }

  // ===== Table =====

  sortBy(key: 'tag' | 'last'): void {
    this.sortDir = this.sortKey === key ? ((-this.sortDir) as 1 | -1) : key === 'last' ? -1 : 1;
    this.sortKey = key;
    this.sortSummary();
  }

  private sortSummary(): void {
    const d = this.sortDir;
    this.summary.sort((a, b) => d * (this.sortKey === 'tag' ? a.tag.localeCompare(b.tag) : a.last.getTime() - b.last.getTime()));
  }

  private get listLength(): number {
    return this.tab === 'summary' ? this.summary.length : this.filteredEvents.length;
  }

  get pageSummary(): SummaryRow[] {
    return this.summary.slice(this.page * this.pageSize, (this.page + 1) * this.pageSize);
  }

  get pageEvents(): EventRow[] {
    return this.filteredEvents.slice(this.page * this.pageSize, (this.page + 1) * this.pageSize);
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.listLength / this.pageSize));
  }

  /** Page buttons: first three, an ellipsis, the last (like 1 2 3 … 125), around the current page. */
  get pages(): (number | null)[] {
    const n = this.pageCount;
    if (n <= 6) return Array.from({ length: n }, (_, i) => i);
    const start = Math.min(Math.max(0, this.page - 1), n - 4);
    const out: (number | null)[] = [start, start + 1, start + 2];
    if (start + 3 < n - 1) out.push(null);
    out.push(n - 1);
    if (start > 0) out.unshift(0, ...(start > 1 ? [null] : []));
    return out;
  }

  get showingLabel(): string {
    const n = this.listLength;
    const what = this.tab === 'summary' ? 'assets' : 'events';
    if (!n) return `No ${what}`;
    return `Showing ${this.page * this.pageSize + 1} – ${Math.min(n, (this.page + 1) * this.pageSize)} of ${n.toLocaleString()} ${what}`;
  }

  setTab(t: 'summary' | 'events'): void {
    this.tab = t;
    this.page = 0;
  }

  get allSelected(): boolean {
    return this.pageSummary.length > 0 && this.pageSummary.every((r) => this.selected.has(r.tag));
  }

  toggleAll(on: boolean): void {
    for (const r of this.pageSummary) on ? this.selected.add(r.tag) : this.selected.delete(r.tag);
  }

  toggleRow(tag: string, on: boolean): void {
    on ? this.selected.add(tag) : this.selected.delete(tag);
  }

  /** Assets grouped by current location, placed at the location's map position (unknown locations: beside their site). */
  private buildMapZones(): void {
    const groups = new Map<string, SummaryRow[]>();
    for (const r of this.summary) groups.set(r.current, [...(groups.get(r.current) ?? []), r]);
    let extra = 0;
    this.mapZones = [...groups].map(([zone, rows]) => {
      const known = ZONE_POINTS.find((p) => p.zone.toLowerCase() === zone.toLowerCase());
      const site = known?.site ?? rows[0].asset?.siteName ?? '';
      const centre = SITE_POINTS.find((p) => p.site === site) ?? SITE_POINTS[0];
      const n = extra++;
      const lat = known?.lat ?? centre.lat - 0.0003 + (n % 3) * 0.0003;
      const lng = known?.lng ?? centre.lng - 0.0003 + Math.floor(n / 3) * 0.0003;
      return {
        zone,
        site: site || '—',
        lat,
        lng,
        assets: rows.map((r) => ({
          tag: r.tag,
          name: r.name,
          since: stamp(r.last).replace('T', ' '),
          source: r.source,
          unverified: this.isUnverified(r),
        })),
      };
    });
  }

  sourceClass(s: LocSource): string {
    return 'src src--' + s.replace(' ', '').toLowerCase();
  }

  statusClass(s: string): string {
    const v = s.toLowerCase();
    return v === 'active' ? 'st st--green' : /dispos|retir|lost|stolen/.test(v) ? 'st st--red' : 'st st--amber';
  }

  icon(key: string): typeof Box {
    return this.typeIcons[key] ?? Box;
  }

  // ===== Navigation =====

  openHistory(tag: string): void {
    this.menuOpen = '';
    this.router.navigate([], { queryParams: { tag } });
  }

  liveLocation(tag?: string): void {
    this.menuOpen = '';
    this.router.navigate(['/tracking-iot/real-time-location'], tag ? { queryParams: { tag } } : {});
  }

  // ===== Movement event =====

  openMovement(tag = ''): void {
    this.menuOpen = '';
    this.formError = '';
    this.form = { tag, location: '', movement: '' as MovementType, at: stamp(new Date()), source: '' as LocSource, remarks: '' };
  }

  saveMovement(): void {
    const f = this.form;
    if (!f) return;
    if (!f.tag || !f.location.trim() || !f.at) return void (this.formError = 'Choose the asset, the location and the date and time.');
    const opt = this.assetOptions.find((a) => a.tag === f.tag);
    // Movement type and source are optional; fall back to the usual values when left unselected.
    const movement: MovementType = f.movement || 'In';
    const source: LocSource = f.source || 'Manual';
    const location = f.location.trim();
    this.saving = true;
    this.formError = '';
    this.api
      .record({ tag: f.tag, name: opt?.name ?? f.tag }, { at: f.at, location, movement, source, remarks: f.remarks.trim() || 'Location updated' })
      .subscribe({
        next: (created) => {
          this.saving = false;
          this.raw = [...this.raw, created];
          this.form = null;
          this.rebuild();
          this.showToast(`${f.tag} moved to ${location}.`);
        },
        error: (err) => {
          this.saving = false;
          this.formError = err?.error?.detail || err?.error?.title || err?.error?.message || 'Could not save the movement.';
        },
      });
  }

  // ===== Export =====

  /** Selected assets, or else every asset in the summary table. */
  exportRequest = (): ExportRequest<SummaryRow> => ({
    title: 'Location History Overview',
    fileBase: 'location-history',
    columns: [
      { label: 'Asset ID', key: 'tag' }, { label: 'Asset Name', key: 'name', width: 2 }, { label: 'Asset Type', key: 'type' }, { label: 'Category', key: 'category' },
      { label: 'Site', key: 'site' }, { label: 'Current Location', key: 'current', width: 2 }, { label: 'Previous Location', key: 'previous', width: 2 },
      { label: 'Last Movement', value: (s) => stamp(s.last).replace('T', ' ') }, { label: 'Source', key: 'source' }, { label: 'Status', key: 'status' },
      { label: 'Location Changes', key: 'changes', align: 'right' },
    ],
    filters: this.selected.size
      ? { Selection: `${this.selected.size} selected` }
      : { Site: this.site, Category: this.category, 'Asset Type': this.type, Zone: this.zone, Source: this.source, Status: this.status, Search: this.search },
    rows: () => (this.selected.size ? this.summary.filter((s) => this.selected.has(s.tag)) : this.summary),
  });

  // ===== Menus =====

  toggleMenu(name: string, event: Event): void {
    event.stopPropagation();
    this.menuOpen = this.menuOpen === name ? '' : name;
  }

  @HostListener('document:click')
  closeMenus(): void {
    this.menuOpen = '';
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.menuOpen = '';
    this.form = null;
  }

  private showToast(message: string): void {
    // shared toast: durations, hover-pause and close button live in shared-ui (toast.ts)
    pushToast(message);
  }
}
