import { AfterViewInit, Component, ElementRef, HostListener, OnDestroy, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import L from 'leaflet';
import * as XLSX from 'xlsx';
import {
  LucideAngularModule,
  ArrowDown,
  ArrowUp,
  ArrowUpDown,
  Bell,
  BellRing,
  CalendarDays,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  ClipboardList,
  Cog,
  Database,
  Download,
  Ellipsis,
  ExternalLink,
  FileText,
  Heart,
  House,
  Maximize,
  MapPin,
  Minus,
  Plus,
  Power,
  Radio,
  TriangleAlert,
  Warehouse,
  Wifi,
  WifiOff,
  Zap,
} from 'lucide-angular';
import {
  ALERTS,
  ASSET_TYPES,
  DEVICES,
  Device,
  DeviceStatus,
  EVENT_HISTORY,
  EventSeries,
  HEALTH_COLORS,
  HealthBand,
  IotAlert,
  LATEST_FIRMWARE,
  PREVIOUS,
  SERIES,
  SITES,
  STATUSES,
  STATUS_COLORS,
  healthBand,
} from './iot-overview-data';

type Period = '7' | '30' | '90';
type QuickTab = 'overview' | 'location' | 'telemetry' | 'maintenance';
type AlertSort = 'time' | 'asset';

interface AlertRow extends IotAlert {
  device: Device;
}

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const TYPE_IMAGES: Record<string, string> = {
  Generators: '/asset-generator.svg',
  Forklifts: '/asset-images/forklift-overview.svg',
  'HVAC Units': '/asset-images/ahu-overview.svg',
  Compressors: '/asset-images/chiller-compressor.svg',
};
// Event timeline geometry (SVG user units)
const TW = 520;
const TH = 170;
const TP = { l: 30, r: 8, t: 10, b: 22 };
/** Coverage: located within the last day. Freshness: reporting devices that updated within the hour. */
const COVERAGE_MIN = 24 * 60;
const FRESH_MIN = 60;

@Component({
  selector: 'app-iot-tracking-overview',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './iot-tracking-overview.html',
  styleUrls: ['./iot-tracking-overview.css', './iot-tracking-overview-panels.css'],
})
export class IotTrackingOverview implements AfterViewInit, OnDestroy {
  readonly icons = {
    ArrowDown, ArrowUp, ArrowUpDown, Bell, BellRing, CalendarDays, ChevronDown, ChevronLeft, ChevronRight, ClipboardList, Cog, Database, Download, Ellipsis, ExternalLink, FileText,
    Heart, House, Maximize, MapPin, Minus, Plus, Power, Radio, TriangleAlert, Warehouse, Wifi, WifiOff, Zap,
  };
  readonly sites = SITES.map((s) => s.name);
  readonly types = ASSET_TYPES;
  readonly statuses = STATUSES;
  readonly statusColors = STATUS_COLORS;
  readonly healthColors = HEALTH_COLORS;
  readonly series = SERIES;
  readonly latestFirmware = LATEST_FIRMWARE;
  readonly healthBand = healthBand;
  readonly tw = TW;
  readonly th = TH;
  readonly tp = TP;
  readonly Math = Math;

  // Filters
  period: Period = '30';
  site = '';
  type = '';
  status: DeviceStatus | '' = '';
  periodOpen = false;

  // Cards
  showAllSites = false;
  hiddenSeries = new Set<EventSeries>();
  timelinePeriod: Period = '30';
  hoverDay: number | null = null;
  hoverStatus: DeviceStatus | null = null;
  hoverHealth: HealthBand | null = null;
  alertSort: AlertSort = 'time';
  alertDesc = true;
  showAllAlerts = false;
  alertPage = 0;
  readonly alertPageSize = 10;
  menuOpen = '';
  selectedId = '';
  quickTab: QuickTab = 'overview';
  toast = '';

  @ViewChild('mapEl') private mapEl?: ElementRef<HTMLDivElement>;
  private map?: L.Map;
  private markerLayer?: L.LayerGroup;
  private selectedRing?: L.CircleMarker;
  private toastTimer?: ReturnType<typeof setTimeout>;
  private readonly router = inject(Router);
  private readonly now = new Date();

  /** Filtered fleet; rebuilt by apply() whenever a filter changes. */
  devices: Device[] = DEVICES;

  constructor() {
    // open on the device behind the latest critical alert
    const first = ALERTS.find((a) => a.severity === 'Critical') ?? ALERTS[0];
    this.selectedId = first?.deviceId ?? DEVICES[0].id;
  }

  ngAfterViewInit(): void {
    this.initMap();
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
    this.map?.remove();
  }

  apply(): void {
    this.devices = DEVICES.filter((d) => (!this.site || d.site === this.site) && (!this.type || d.type === this.type) && (!this.status || d.status === this.status));
    this.alertPage = 0;
    this.drawMarkers();
    this.fitToFilter();
  }

  setPeriod(p: Period): void {
    this.period = p;
    this.timelinePeriod = p;
    this.periodOpen = false;
    this.alertPage = 0;
    // alert markers follow the period
    this.drawMarkers();
  }

  get periodLabel(): string {
    return `Last ${this.period} Days`;
  }

  get filtered(): boolean {
    return !!(this.site || this.type || this.status);
  }

  private minutesSince(v: string): number {
    return (this.now.getTime() - new Date(v).getTime()) / 60_000;
  }

  /** Local yyyy-MM-ddTHH:mm, comparable with the alert timestamps. */
  private sinceIso(days: number): string {
    const d = new Date(this.now.getTime() - days * 86_400_000);
    const p = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
  }

  // ===== KPIs =====

  get kpis() {
    const list = this.devices;
    const total = list.length;
    const online = list.filter((d) => d.status === 'Online').length;
    const reporting = list.filter((d) => d.status === 'Online' || d.status === 'Idle');
    const located = list.filter((d) => this.minutesSince(d.lastUpdate) <= COVERAGE_MIN).length;
    const fresh = reporting.filter((d) => this.minutesSince(d.lastUpdate) <= FRESH_MIN).length;
    const alerts = this.periodAlerts;
    const pct = (n: number, of: number) => (of ? Math.round((n / of) * 1000) / 10 : 0);
    const coverage = pct(located, total);
    const freshness = pct(fresh, reporting.length);
    return {
      connected: total,
      // change against the previous period only makes sense for the whole fleet
      connectedChange: this.filtered ? null : Math.round(((total - PREVIOUS.connected) / PREVIOUS.connected) * 1000) / 10,
      online,
      onlinePct: pct(online, total),
      alerts: alerts.length,
      critical: alerts.filter((a) => a.severity === 'Critical').length,
      coverage,
      coverageChange: this.filtered ? null : Math.round((coverage - PREVIOUS.coverage) * 10) / 10,
      freshness,
      freshnessChange: this.filtered ? null : Math.round((freshness - PREVIOUS.freshness) * 10) / 10,
    };
  }

  // ===== Sites =====

  get siteStats() {
    const rows = SITES.map((s) => {
      const list = this.devices.filter((d) => d.site === s.name);
      const count = (st: DeviceStatus) => list.filter((d) => d.status === st).length;
      const online = count('Online');
      return {
        site: s, total: list.length, online, idle: count('Idle'), offline: count('Offline') + count('Unknown'),
        onlinePct: list.length ? Math.round((online / list.length) * 1000) / 10 : 0,
      };
    })
      .filter((r) => r.total)
      .sort((a, b) => b.total - a.total);
    return this.showAllSites ? rows : rows.slice(0, 3);
  }

  filterSite(name: string): void {
    this.site = this.site === name ? '' : name;
    this.apply();
  }

  // ===== Tracking health =====

  get connectivity() {
    const total = this.devices.length;
    const C = 2 * Math.PI * 48;
    let acc = 0;
    const rows = STATUSES.map((s) => {
      const count = this.devices.filter((d) => d.status === s).length;
      const len = total ? (count / total) * C : 0;
      const row = { status: s, count, color: STATUS_COLORS[s], dash: `${Math.max(0, len - (count ? 2 : 0))} ${C}`, offset: -acc };
      acc += len;
      return row;
    });
    return { total, rows };
  }

  get firmware() {
    const total = this.devices.length;
    const latest = this.devices.filter((d) => d.firmwareLatest).length;
    return { total, latest, pct: total ? Math.round((latest / total) * 100) : 0 };
  }

  get attention(): number {
    return this.devices.filter((d) => d.abnormal).length;
  }

  showAttention(): void {
    const d = this.devices.find((x) => x.abnormal);
    if (d) this.select(d.id, true);
  }

  // ===== Asset health & utilization =====

  get health() {
    const total = this.devices.length;
    const C = 2 * Math.PI * 48;
    let acc = 0;
    const bands: HealthBand[] = ['Healthy', 'Warning', 'Critical', 'Unknown'];
    const rows = bands.map((b) => {
      const count = this.devices.filter((d) => healthBand(d.health) === b).length;
      const len = total ? (count / total) * C : 0;
      const row = { band: b, count, pct: total ? Math.round((count / total) * 100) : 0, color: HEALTH_COLORS[b], dash: `${Math.max(0, len - (count ? 2 : 0))} ${C}`, offset: -acc };
      acc += len;
      return row;
    });
    return { total, rows, healthyPct: rows[0].pct };
  }

  /** Running share (blue) and idle share (amber) per asset type. */
  get utilization() {
    return ASSET_TYPES.map((t) => {
      const list = this.devices.filter((d) => d.type === t);
      const n = list.length || 1;
      const running = Math.round(list.reduce((s, d) => s + d.utilization, 0) / n);
      const idle = Math.min(100 - running, Math.round((list.filter((d) => d.status === 'Idle').length / n) * 100 + (list.length ? 12 : 0)));
      return { type: t, running, idle: list.length ? idle : 0, count: list.length };
    });
  }

  // ===== Events timeline =====

  get timeline() {
    const days = EVENT_HISTORY.slice(-Number(this.timelinePeriod));
    // the fleet filter scales the event volume to the share of devices shown
    const scale = this.devices.length / DEVICES.length;
    const vals = days.map((d) => ({ date: d.date, telemetry: Math.round(d.telemetry * scale), location: Math.round(d.location * scale), geofence: Math.round(d.geofence * scale), alerts: Math.round(d.alerts * scale) }));
    const visible = SERIES.filter((s) => !this.hiddenSeries.has(s.key));
    const max = Math.max(10, ...vals.flatMap((v) => visible.map((s) => v[s.key])));
    const step = max > 150 ? 50 : max > 60 ? 20 : 10;
    const top = Math.ceil(max / step) * step;
    const plotW = TW - TP.l - TP.r;
    const plotH = TH - TP.t - TP.b;
    const x = (i: number) => TP.l + (vals.length > 1 ? (i / (vals.length - 1)) * plotW : plotW / 2);
    const y = (v: number) => TH - TP.b - (v / top) * plotH;
    const lines = visible.map((s) => ({ ...s, d: vals.map((v, i) => `${i ? 'L' : 'M'}${x(i).toFixed(1)},${y(v[s.key]).toFixed(1)}`).join(''), pts: vals.map((v, i) => ({ x: x(i), y: y(v[s.key]) })) }));
    const ticks = Array.from({ length: top / step + 1 }, (_, i) => ({ y: y(i * step), label: String(i * step) }));
    const every = Math.max(1, Math.round(vals.length / 7));
    const labels = vals.map((v, i) => ({ x: x(i), label: this.shortDate(v.date), show: i % every === 0 })).filter((l) => l.show);
    const slot = plotW / Math.max(1, vals.length - 1);
    return { vals, lines, ticks, labels, x, slot };
  }

  toggleSeries(key: EventSeries): void {
    this.hiddenSeries.has(key) ? this.hiddenSeries.delete(key) : this.hiddenSeries.add(key);
    // keep at least one line
    if (this.hiddenSeries.size === SERIES.length) this.hiddenSeries.delete(key);
  }

  // ===== Alerts =====

  get periodAlerts(): AlertRow[] {
    const since = this.sinceIso(Number(this.period));
    const ids = new Set(this.devices.map((d) => d.id));
    const byId = new Map(DEVICES.map((d) => [d.id, d]));
    return ALERTS.filter((a) => a.at >= since && ids.has(a.deviceId)).map((a) => ({ ...a, device: byId.get(a.deviceId)! }));
  }

  get alertRows(): AlertRow[] {
    const dir = this.alertDesc ? -1 : 1;
    return [...this.periodAlerts].sort((a, b) => dir * (this.alertSort === 'time' ? a.at.localeCompare(b.at) : a.device.assetId.localeCompare(b.device.assetId)));
  }

  get alertPageRows(): AlertRow[] {
    return this.showAllAlerts ? this.alertRows.slice(this.alertPage * this.alertPageSize, (this.alertPage + 1) * this.alertPageSize) : this.alertRows.slice(0, 5);
  }

  get alertPages(): number {
    return Math.max(1, Math.ceil(this.alertRows.length / this.alertPageSize));
  }

  sortAlerts(by: AlertSort): void {
    if (this.alertSort === by) this.alertDesc = !this.alertDesc;
    else {
      this.alertSort = by;
      this.alertDesc = by === 'time';
    }
    this.alertPage = 0;
  }

  setAlertStatus(a: AlertRow, status: IotAlert['status']): void {
    this.menuOpen = '';
    const src = ALERTS.find((x) => x.id === a.id);
    if (src) src.status = status;
    this.showToast(`${a.event} on ${a.device.assetId} marked ${status.toLowerCase()}.`);
  }

  // ===== Quick view =====

  get selected(): Device | null {
    return DEVICES.find((d) => d.id === this.selectedId) ?? null;
  }

  select(id: string, pan = false): void {
    this.menuOpen = '';
    this.selectedId = id;
    this.quickTab = 'overview';
    this.markSelected(pan);
  }

  imageOf(d: Device): string | null {
    return TYPE_IMAGES[d.type] ?? null;
  }

  alertsOf(d: Device): AlertRow[] {
    return ALERTS.filter((a) => a.deviceId === d.id).map((a) => ({ ...a, device: d }));
  }

  locate(d: Device): void {
    this.select(d.id, true);
    this.mapEl?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }

  // ===== Map =====

  private initMap(): void {
    const el = this.mapEl?.nativeElement;
    if (!el) return;
    this.map = L.map(el, { zoomControl: false, attributionControl: true, preferCanvas: true, scrollWheelZoom: false });
    L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', {
      maxZoom: 19,
      attribution: 'Imagery &copy; Esri',
    }).addTo(this.map);
    for (const s of SITES) {
      L.polygon(s.zone, { color: s.color, weight: 2, dashArray: '6 5', fillColor: s.color, fillOpacity: 0.16 })
        .addTo(this.map)
        .bindTooltip(s.name, { permanent: true, direction: 'center', className: 'iot-zone-label' })
        .on('click', () => this.filterSite(s.name));
    }
    this.markerLayer = L.layerGroup().addTo(this.map);
    this.drawMarkers();
    this.fitToFilter();
    // the card can resize after first paint (fonts, sidebar)
    setTimeout(() => this.map?.invalidateSize());
  }

  private drawMarkers(): void {
    if (!this.map || !this.markerLayer) return;
    this.markerLayer.clearLayers();
    const alerting = new Set(this.periodAlerts.filter((a) => a.status !== 'Resolved' && a.severity === 'Critical').map((a) => a.deviceId));
    for (const d of this.devices) {
      const alert = alerting.has(d.id);
      L.circleMarker([d.lat, d.lng], {
        radius: alert ? 7 : 5, weight: alert ? 2 : 1.5, color: '#fff', fillColor: alert ? '#f59e0b' : STATUS_COLORS[d.status], fillOpacity: 1,
      })
        .bindTooltip(`<strong>${d.assetId}</strong><br>${d.name}<br>${d.status}${alert ? ' · Alert' : ''}`, { direction: 'top' })
        .on('click', () => this.select(d.id))
        .addTo(this.markerLayer);
    }
    this.markSelected(false);
  }

  private markSelected(pan: boolean): void {
    const d = this.selected;
    if (!this.map || !d) return;
    this.selectedRing?.remove();
    this.selectedRing = L.circleMarker([d.lat, d.lng], { radius: 12, weight: 3, color: '#4f2bd8', fill: false, interactive: false }).addTo(this.map);
    if (pan) this.map.setView([d.lat, d.lng], Math.max(this.map.getZoom(), 18));
  }

  private fitToFilter(): void {
    if (!this.map) return;
    const zones = SITES.filter((s) => !this.site || s.name === this.site).flatMap((s) => s.zone);
    this.map.fitBounds(L.latLngBounds(zones), { padding: [16, 16] });
  }

  zoom(delta: number): void {
    delta > 0 ? this.map?.zoomIn() : this.map?.zoomOut();
  }

  resetView(): void {
    this.site = '';
    this.apply();
  }

  get legend() {
    const alerts = new Set(this.periodAlerts.filter((a) => a.status !== 'Resolved' && a.severity === 'Critical').map((a) => a.deviceId)).size;
    return [...(['Online', 'Idle', 'Offline'] as DeviceStatus[]).map((s) => ({ label: s, count: this.devices.filter((d) => d.status === s).length, color: STATUS_COLORS[s], alert: false })), { label: 'Alert', count: alerts, color: '#f59e0b', alert: true }];
  }

  // ===== Navigation, export =====

  go(path: string): void {
    this.router.navigate([path]);
  }

  exportReport(): void {
    const k = this.kpis;
    const book = XLSX.utils.book_new();
    const filters = `Period: ${this.periodLabel}; Site: ${this.site || 'All'}; Type: ${this.type || 'All'}; Status: ${this.status || 'All'}`;
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet([
      { Metric: 'Filters', Value: filters },
      { Metric: 'Connected Assets', Value: k.connected }, { Metric: 'Online Now', Value: k.online }, { Metric: 'Online %', Value: k.onlinePct },
      { Metric: 'Alerts', Value: k.alerts }, { Metric: 'Critical Alerts', Value: k.critical }, { Metric: 'Coverage %', Value: k.coverage }, { Metric: 'Data Freshness %', Value: k.freshness },
      { Metric: 'Devices on latest firmware', Value: `${this.firmware.latest} / ${this.firmware.total}` }, { Metric: 'Sensors requiring attention', Value: this.attention },
    ]), 'Summary');
    const showAll = this.showAllSites;
    this.showAllSites = true;
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet(this.siteStats.map((s) => ({ Site: s.site.name, Assets: s.total, Online: s.online, Idle: s.idle, 'Offline / Unknown': s.offline, 'Online %': s.onlinePct }))), 'Sites');
    this.showAllSites = showAll;
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet(this.alertRows.map((a) => ({ Time: a.at.replace('T', ' '), 'Asset ID': a.device.assetId, 'Asset Name': a.device.name, Site: a.device.site, 'Event Type': a.event, Severity: a.severity, Status: a.status }))), 'Alerts');
    XLSX.writeFile(book, `IoT_Tracking_Overview_${this.sinceIso(0).slice(0, 10)}.xlsx`);
  }

  // ===== Formatting =====

  shortDate(iso: string): string {
    return `${MONTHS[Number(iso.slice(5, 7)) - 1]} ${Number(iso.slice(8, 10))}`;
  }

  fmtDateTime(v: string): string {
    return `${MONTHS[Number(v.slice(5, 7)) - 1]} ${Number(v.slice(8, 10))}, ${v.slice(0, 4)} ${v.slice(11, 16)}`;
  }

  fmtDate(iso: string): string {
    return `${MONTHS[Number(iso.slice(5, 7)) - 1]} ${Number(iso.slice(8, 10))}, ${iso.slice(0, 4)}`;
  }

  ago(v: string): string {
    const m = Math.round(this.minutesSince(v));
    return m < 60 ? `${m} min ago` : m < 1440 ? `${Math.round(m / 60)} h ago` : `${Math.round(m / 1440)} d ago`;
  }

  sevClass(s: string): string {
    return 'sev--' + s.toLowerCase();
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
