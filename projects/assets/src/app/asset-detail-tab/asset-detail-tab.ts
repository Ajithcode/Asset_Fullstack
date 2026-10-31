import { Component, DestroyRef, HostListener, OnDestroy, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  LucideAngularModule,
  Activity,
  CalendarClock,
  ChevronDown,
  ClipboardList,
  Download,
  ExternalLink,
  FileText,
  HeartPulse,
  History,
  Images,
  Info,
  LayoutGrid,
  MessageSquarePlus,
  Network,
  Power,
  Printer,
  Puzzle,
  ShieldAlert,
  Timer,
  TriangleAlert,
  Wrench,
  X,
} from 'lucide-angular';
import { ASSET_RECORDS, ASSET_TYPE_ICONS, AssetHeader, AssetRecord, AssetTabLink, TODAY, assetHeader, assetLocation, assetTabs, findAsset, toIsoDate } from 'shared-ui';
import { AssetDetail, Assessment, loadAssetDetail } from '../asset-details/asset-details';
import { MaintenanceData, dataFor } from '../asset-maintenance/asset-maintenance';
import { isOpen } from '../asset-maintenance/maintenance-sr';
import { AssetComponent, COMPONENT_ICONS, componentTreeOf, componentsOf } from '../asset-components/asset-components';
import { ComponentOverview } from '../asset-components/component-overview/component-overview';
import { buildMockFields } from '../asset-custom-fields/asset-custom-fields';
import { CustomField } from '../asset-custom-fields/custom-fields.model';

/** Which asset detail tab this page is: route data { view }. */
type View = 'overview' | 'components' | 'fields' | 'history';

type HistoryKind = 'Status' | 'Condition' | 'Health' | 'Criticality' | 'Maintenance' | 'Service Request' | 'Document' | 'Image' | 'Relationship';

/** One entry of the asset's history, with the detail tab it belongs to. */
interface HistoryEvent {
  /** yyyy-MM-dd or yyyy-MM-ddTHH:mm */
  at: string;
  kind: HistoryKind;
  title: string;
  detail: string;
  by: string;
  path: string;
}

interface FieldRow {
  field: CustomField;
  value: string;
}

const VIEW_TABS: Record<View, string> = { overview: 'Overview', components: 'Components', fields: 'Custom Fields', history: 'History' };
const KIND_TONES: Record<HistoryKind, string> = {
  Status: 'good', Condition: 'warn', Health: 'good', Criticality: 'serious', Maintenance: 'warn', 'Service Request': 'serious', Document: 'good', Image: 'good', Relationship: 'warn',
};
const HISTORY_KINDS = Object.keys(KIND_TONES) as HistoryKind[];
const HISTORY_PAGE = 15;
/** Field definitions, shared by every asset (DEMO MODE: the Custom Fields page keeps its own editable copy). */
const FIELDS = buildMockFields();

/**
 * Overview, Components, Custom Fields and History tabs of the asset detail page. Every figure comes from the one asset
 * named by ?tag=: its register record, detail file, maintenance data and components.
 */
@Component({
  selector: 'app-asset-detail-tab',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule, ComponentOverview],
  templateUrl: './asset-detail-tab.html',
  // Shares the Status & Condition page's header, tab, KPI, table and pill styles
  styleUrls: [
    '../asset-status-condition/asset-status-condition.css',
    '../asset-status-condition/asset-status-condition-panels.css',
    '../asset-maintenance-tab/asset-maintenance-tab.css',
    './asset-detail-tab.css',
  ],
})
export class AssetDetailTab implements OnDestroy {
  readonly icons = {
    Activity, CalendarClock, ChevronDown, ClipboardList, Download, ExternalLink, FileText, HeartPulse, History, Images, Info, LayoutGrid, MessageSquarePlus, Network,
    Power, Printer, Puzzle, ShieldAlert, Timer, TriangleAlert, Wrench, X,
  };
  readonly typeIcons = ASSET_TYPE_ICONS;
  readonly kinds = HISTORY_KINDS;
  readonly today = toIsoDate(TODAY);
  readonly iconFor = (c: AssetComponent) => COMPONENT_ICONS[c.icon];

  view: View = 'overview';
  tabs: AssetTabLink[] = assetTabs('Overview');

  /** The asset named by ?tag=; when it is missing or unknown the page shows a "not found" message. */
  asset: AssetHeader = assetHeader(ASSET_RECORDS[0]);
  record: AssetRecord = ASSET_RECORDS[0];
  notFound = false;
  requestedTag = '';
  loading = false;
  detail: AssetDetail | null = null;
  maintenance: MaintenanceData = dataFor(ASSET_RECORDS[0]);
  tree: AssetComponent[] = [];
  components: AssetComponent[] = [];
  selectedComponent: AssetComponent | null = null;

  // History filters
  history: HistoryEvent[] = [];
  kindFilter: HistoryKind | '' = '';
  search = '';
  historyLimit = HISTORY_PAGE;

  // Custom fields
  fieldGroups: { name: string; rows: FieldRow[] }[] = [];
  emptyOnly = false;

  actionsOpen = false;
  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;
  private readonly router = inject(Router);

  constructor() {
    const route = inject(ActivatedRoute);
    const v = route.snapshot.data['view'] as View;
    this.view = v in VIEW_TABS ? v : 'overview';
    this.tabs = assetTabs(VIEW_TABS[this.view]);
    route.queryParamMap.pipe(takeUntilDestroyed(inject(DestroyRef))).subscribe((p) => this.load(p.get('tag')));
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
  }

  // ===== Loading =====

  private load(tag: string | null): void {
    tag ??= ASSET_RECORDS[0].tag; // opened without an asset: show the first one
    const record = findAsset(tag);
    this.requestedTag = tag;
    this.notFound = !record;
    if (!record) return;
    this.record = record;
    this.asset = assetHeader(record);
    this.maintenance = dataFor(record);
    this.tree = componentTreeOf(record.tag);
    this.components = componentsOf(record.tag);
    this.selectedComponent = null;
    this.kindFilter = '';
    this.search = '';
    this.historyLimit = HISTORY_PAGE;
    this.closeMenus();
    this.detail = null;
    this.rebuild();
    this.loading = true;
    loadAssetDetail(record.tag).then((d) => {
      if (this.record !== record) return; // the user moved on to another asset
      this.loading = false;
      this.detail = d;
      this.rebuild();
    });
  }

  private rebuild(): void {
    this.history = this.buildHistory();
    this.fieldGroups = this.buildFields();
  }

  get title(): string {
    return VIEW_TABS[this.view];
  }

  // ===== Tones =====

  statusTone(status: string): string {
    return status === 'Active' || status === 'Checked Out' ? 'good' : status === 'Disposed' ? 'critical' : 'warn';
  }

  conditionTone(condition: string | undefined): string {
    return condition === 'Excellent' || condition === 'Good' ? 'good' : condition === 'Fair' ? 'warn' : 'critical';
  }

  criticalityTone(level: string): string {
    return level === 'Critical' ? 'critical' : level === 'High' ? 'serious' : level === 'Medium' ? 'warn' : 'good';
  }

  healthTone(status: string | undefined): string {
    return status === 'Healthy' || status === 'Good' ? 'good' : status === 'Fair' ? 'warn' : status === 'Poor' ? 'serious' : 'critical';
  }

  kindTone(k: HistoryKind): string {
    return KIND_TONES[k];
  }

  componentTone(status: string): string {
    return status === 'Active' ? 'good' : status === 'Under Maintenance' ? 'warn' : status === 'Needs Replacement' ? 'critical' : 'serious';
  }

  // ===== Overview =====

  get location(): string {
    return assetLocation(this.record);
  }

  /** Most recent condition assessment. */
  get latestAssessment(): Assessment | null {
    return [...(this.detail?.status.assessments ?? [])].sort((a, b) => b.date.localeCompare(a.date))[0] ?? null;
  }

  get openWorkOrders(): number {
    return this.maintenance.workOrders.filter((w) => w.status !== 'Completed').length;
  }

  get openRequests(): number {
    return this.maintenance.serviceRequests.filter(isOpen).length;
  }

  get openAlerts(): number {
    return (this.detail?.status.alerts ?? []).filter((a) => a.status !== 'Resolved').length;
  }

  get nextMaintenance(): string {
    return this.detail?.status.lifecycle.nextMaintenance || this.record.nextMaintenance || '';
  }

  /** Counts that link to the other detail tabs. */
  get glance(): { label: string; value: number; icon: typeof Info; path: string }[] {
    const d = this.detail;
    return [
      { label: 'Components', value: this.components.length, icon: Puzzle, path: '/assets/component-details' },
      { label: 'Relationships', value: d?.relationships.links.length ?? 0, icon: Network, path: '/assets/relationships' },
      { label: 'Documents', value: d?.documents.length ?? 0, icon: FileText, path: '/assets/documents' },
      { label: 'Images', value: d?.images.length ?? 0, icon: Images, path: '/assets/images' },
      { label: 'Work Orders', value: this.maintenance.workOrders.length, icon: Wrench, path: '/assets/maintenance-details' },
      { label: 'Service Requests', value: this.maintenance.serviceRequests.length, icon: MessageSquarePlus, path: '/assets/service-history' },
      { label: 'Open Alerts', value: this.openAlerts, icon: TriangleAlert, path: '/assets/status-condition' },
    ];
  }

  get recentHistory(): HistoryEvent[] {
    return this.history.slice(0, 6);
  }

  // ===== Components =====

  pickComponent(c: AssetComponent): void {
    this.selectedComponent = this.selectedComponent === c ? null : c;
  }

  onComponentTab(e: { tab: 'service' | 'documents'; component: AssetComponent | null }): void {
    if (e.tab === 'service') this.go('/assets/service-history');
    else this.router.navigate(['/assets/components'], { queryParams: { tag: this.asset.tag } });
  }

  parentOf(c: AssetComponent): AssetComponent | null {
    return this.components.find((p) => p.children.includes(c)) ?? null;
  }

  lifePct(c: AssetComponent): number {
    return c.lifeTotal ? Math.min(100, Math.round((c.lifeUsed / c.lifeTotal) * 100)) : 0;
  }

  // ===== Custom fields =====

  /** The asset's value for a field, from its record and detail data; '' when it has none. */
  private valueOf(f: CustomField): string {
    const r = this.record;
    const d = this.detail;
    const op = d?.status.operational;
    const life = d?.status.lifecycle;
    const m = this.maintenance.overview;
    const purchase = r.purchaseDate ?? '';
    const warrantyEnd = purchase ? `${Number(purchase.slice(0, 4)) + m.warrantyYears}${purchase.slice(4)}` : '';
    const values: Record<string, string | number | undefined> = {
      warranty_expiry: warrantyEnd,
      asset_condition: this.latestAssessment?.overall,
      installation_date: life?.commissionedOn,
      power_rating: op && /kVA/i.test(op.ratedUnit) ? op.ratedCapacity : undefined,
      serial_number: m.serialNumber,
      criticality: d?.criticality.level ?? r.criticality,
      location_zone: r.site,
      manufacturer: r.manufacturer,
      model: r.model,
      purchase_date: purchase,
      warranty_period: m.warrantyYears * 12,
      warranty_provider: m.amcVendor,
      assigned_department: r.department,
      custodian: r.custodian,
      building_floor: r.area,
      last_service_date: d?.status.lastMaintenance,
      next_service_date: life?.nextMaintenance,
      service_provider: m.amcVendor,
      runtime_hours: op?.runningHours,
      maintenance_frequency: d?.health.maintenancePlan[0]?.frequency,
      rated_output: op && /kVA|kW/i.test(op.ratedUnit) ? `${op.ratedCapacity} ${op.ratedUnit}` : undefined,
      cooling_capacity_tr: op && /TR/.test(op.ratedUnit) ? op.ratedCapacity : undefined,
    };
    const v = values[f.name];
    return v === undefined || v === null || v === '' ? f.defaultValue : String(v);
  }

  private buildFields(): { name: string; rows: FieldRow[] }[] {
    const fields = FIELDS.filter(
      (f) => f.status === 'Active' && (f.scope === 'asset' || (f.scope === 'category' && f.appliesTo === this.record.category)),
    );
    const groups = new Map<string, FieldRow[]>();
    for (const f of fields) {
      const name = f.group || 'Other';
      if (!groups.has(name)) groups.set(name, []);
      groups.get(name)!.push({ field: f, value: this.valueOf(f) });
    }
    return [...groups].map(([name, rows]) => ({ name, rows: rows.sort((a, b) => a.field.order - b.field.order) }));
  }

  get fieldCount(): number {
    return this.fieldGroups.reduce((s, g) => s + g.rows.length, 0);
  }

  get filledCount(): number {
    return this.fieldGroups.reduce((s, g) => s + g.rows.filter((r) => r.value).length, 0);
  }

  get missingRequired(): number {
    return this.fieldGroups.reduce((s, g) => s + g.rows.filter((r) => r.field.required && !r.value).length, 0);
  }

  get shownGroups(): { name: string; rows: FieldRow[] }[] {
    if (!this.emptyOnly) return this.fieldGroups;
    return this.fieldGroups.map((g) => ({ name: g.name, rows: g.rows.filter((r) => !r.value) })).filter((g) => g.rows.length);
  }

  displayValue(r: FieldRow): string {
    if (!r.value) return '';
    if (r.field.type === 'Checkbox') return r.value === 'true' ? 'Yes' : 'No';
    return r.value;
  }

  // ===== History =====

  private buildHistory(): HistoryEvent[] {
    const d = this.detail;
    const out: HistoryEvent[] = [];
    const add = (at: string, kind: HistoryKind, title: string, detail: string, by: string, path: string) => {
      if (at && at.slice(0, 10) <= this.today) out.push({ at, kind, title, detail, by, path });
    };
    if (d) {
      for (const e of d.status.timeline) add(e.date, 'Status', e.title, [e.status, e.condition].filter(Boolean).join(' · '), e.by ?? '', '/assets/status-condition');
      for (const a of d.status.assessments) add(a.date, 'Condition', `Condition assessed: ${a.overall}`, `Health ${a.healthScore}%${a.remarks ? ' · ' + a.remarks : ''}`, a.assessedBy, '/assets/status-condition');
      for (const h of d.health.history) add(h.date, 'Health', `Health score ${h.score} (${h.status})`, `${h.trigger}${h.comments ? ' · ' + h.comments : ''}`, h.assessedBy, '/assets/health-score');
      for (const c of d.criticality.history) add(c.date, 'Criticality', `Criticality reviewed: ${c.level} (${c.score})`, c.remarks, c.assessedBy, '/assets/criticality');
      for (const doc of d.documents) {
        for (const v of doc.versions) add(v.uploadedAt, 'Document', `${doc.name} uploaded (v${v.version})`, v.note || doc.type, v.uploadedBy, '/assets/documents');
      }
      for (const img of d.images) {
        for (const h of img.history) add(h.at, 'Image', `${img.title}: ${h.action}`, img.type, h.by, '/assets/images');
      }
      for (const l of d.relationships.links) add(l.since, 'Relationship', `${l.type} linked: ${l.tag}`, l.description, '', '/assets/relationships');
    }
    for (const w of this.maintenance.workOrders) {
      add(w.date, 'Maintenance', `${w.id} · ${w.type} ${w.status.toLowerCase()}`, w.description, w.technician, '/assets/maintenance-details');
    }
    for (const r of this.maintenance.serviceRequests) {
      add(r.createdAt, 'Service Request', `${r.id} raised`, r.title, r.requestedBy, '/assets/service-history');
      add(r.resolvedAt, 'Service Request', `${r.id} resolved`, r.title, r.assignedTo, '/assets/service-history');
    }
    return out.sort((a, b) => b.at.localeCompare(a.at));
  }

  get filteredHistory(): HistoryEvent[] {
    const q = this.search.trim().toLowerCase();
    return this.history.filter(
      (e) => (!this.kindFilter || e.kind === this.kindFilter) && (!q || [e.title, e.detail, e.by].some((v) => v.toLowerCase().includes(q))),
    );
  }

  get shownHistory(): HistoryEvent[] {
    return this.filteredHistory.slice(0, this.historyLimit);
  }

  kindCount(k: HistoryKind): number {
    return this.history.filter((e) => e.kind === k).length;
  }

  // ===== Actions =====

  go(path: string): void {
    this.router.navigate([path], { queryParams: { tag: this.asset.tag } });
  }

  exportCsv(): void {
    this.closeMenus();
    const lines =
      this.view === 'fields'
        ? [['Group', 'Field', 'Value', 'Required'], ...this.fieldGroups.flatMap((g) => g.rows.map((r) => [g.name, r.field.label, this.displayValue(r), r.field.required ? 'Yes' : 'No']))]
        : this.view === 'components'
          ? [['Tag', 'Component', 'Status', 'Manufacturer', 'Model', 'Serial Number', 'Installed'], ...this.components.map((c) => [c.tag, c.name, c.status, c.manufacturer, c.model, c.serialNumber, c.installedDate])]
          : [['Date', 'Type', 'Event', 'Detail', 'By'], ...this.filteredHistory.map((e) => [e.at.replace('T', ' '), e.kind, e.title, e.detail, e.by])];
    const csv = lines.map((l) => l.map((v) => `"${String(v ?? '').replace(/"/g, '""')}"`).join(',')).join('\r\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }));
    const a = document.createElement('a');
    a.href = url;
    a.download = `${this.asset.tag}_${VIEW_TABS[this.view].toLowerCase().replace(/ /g, '_')}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }

  print(): void {
    this.closeMenus();
    window.print();
  }

  // ===== Menus =====

  toggleActions(event: Event): void {
    event.stopPropagation();
    this.actionsOpen = !this.actionsOpen;
  }

  @HostListener('document:click')
  closeMenus(): void {
    this.actionsOpen = false;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeMenus();
    this.selectedComponent = null;
  }

  tabClick(tab: AssetTabLink): void {
    if (!tab.path && !tab.active) this.showToast(`${tab.label} is not available in this demo.`);
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 3000);
  }
}
