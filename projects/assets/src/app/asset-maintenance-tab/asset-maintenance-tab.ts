import { Component, DestroyRef, HostListener, OnDestroy, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  LucideAngularModule,
  CalendarCheck,
  CalendarClock,
  ChevronDown,
  CircleCheck,
  ClipboardList,
  Clock,
  Coins,
  Download,
  ExternalLink,
  History,
  MessageSquarePlus,
  Plus,
  Printer,
  Puzzle,
  Star,
  Timer,
  Wrench,
} from 'lucide-angular';
import { ASSET_RECORDS, ASSET_TYPE_ICONS, AssetHeader, AssetRecord, AssetTabLink, TODAY, assetHeader, assetTabs, findAsset, toIsoDate } from 'shared-ui';
import { loadAssetDetail } from '../asset-details/asset-details';
import { MaintenanceData, WorkOrder, dataFor } from '../asset-maintenance/asset-maintenance';
import { PM_STATUS_COLORS, PmSchedule, PmStatus, daysUntil, pmStatus, schedulesFromPlan } from '../asset-maintenance/maintenance-pm';
import { SR_STATUS_COLORS, ServiceRequest, isOpen, srKpis } from '../asset-maintenance/maintenance-sr';
import { AssetComponent, componentsOf } from '../asset-components/asset-components';
import { ComponentMaintenance, MaintenanceSchedule, buildMockMaintenance } from '../asset-components/component-maintenance/component-maintenance';
import { ComponentServiceHistory, ServiceRecord, buildMockServiceHistory } from '../asset-components/component-service-history/component-service-history';

/** Which asset detail tab this page is: route data { view }. */
type View = 'maintenance' | 'service';

/** One line of the asset's service history: a completed work order or a resolved service request. */
interface ServiceEvent {
  date: string;
  source: 'Work Order' | 'Service Request';
  ref: string;
  kind: string;
  description: string;
  by: string;
  cost: number | null;
}

const WO_TYPE_TONES: Record<string, string> = { Preventive: 'good', Corrective: 'serious', Inspection: 'warn', Service: 'good' };
const PRIORITY_TONES: Record<string, string> = { High: 'critical', Medium: 'warn', Low: 'good' };
const HISTORY_ROWS = 8;

/**
 * Maintenance and Service History tabs of the asset detail page: the asset's preventive maintenance, work orders and
 * service requests (from the Maintenance module's data for the asset), plus each component's maintenance and service
 * records. The full Maintenance module stays one click away.
 */
@Component({
  selector: 'app-asset-maintenance-tab',
  standalone: true,
  imports: [CommonModule, RouterModule, LucideAngularModule, ComponentMaintenance, ComponentServiceHistory],
  templateUrl: './asset-maintenance-tab.html',
  // Shares the Status & Condition page's header, tab, KPI, table and pill styles
  styleUrls: [
    '../asset-status-condition/asset-status-condition.css',
    '../asset-status-condition/asset-status-condition-panels.css',
    './asset-maintenance-tab.css',
  ],
})
export class AssetMaintenanceTab implements OnDestroy {
  readonly icons = {
    CalendarCheck, CalendarClock, ChevronDown, CircleCheck, ClipboardList, Clock, Coins, Download, ExternalLink, History, MessageSquarePlus, Plus,
    Printer, Puzzle, Star, Timer, Wrench,
  };
  readonly typeIcons = ASSET_TYPE_ICONS;
  readonly pmColors = PM_STATUS_COLORS;
  readonly srColors = SR_STATUS_COLORS;
  readonly today = toIsoDate(TODAY);
  readonly Math = Math;

  view: View = 'maintenance';
  tabs: AssetTabLink[] = assetTabs('Maintenance');

  /** The asset named by ?tag=; when it is missing or unknown the page shows a "not found" message. */
  asset: AssetHeader = assetHeader(ASSET_RECORDS[0]);
  private record: AssetRecord = ASSET_RECORDS[0];
  notFound = false;
  requestedTag = '';

  data: MaintenanceData = dataFor(ASSET_RECORDS[0]);
  pm: PmSchedule[] = [];
  loading = false;
  components: AssetComponent[] = [];
  /** Component shown in the component section below its summary table. */
  componentId: number | null = null;

  historyAll = false;
  actionsOpen = false;
  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;
  private readonly router = inject(Router);

  constructor() {
    const route = inject(ActivatedRoute);
    this.view = route.snapshot.data['view'] === 'service' ? 'service' : 'maintenance';
    this.tabs = assetTabs(this.view === 'service' ? 'Service History' : 'Maintenance');
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
    this.data = dataFor(record);
    this.pm = this.data.pm ?? [];
    this.historyAll = false;
    this.closeMenus();
    this.components = componentsOf(record.tag);
    for (const c of this.components) {
      c.maintenance ??= buildMockMaintenance(c);
      c.serviceHistory ??= buildMockServiceHistory(c);
    }
    this.componentId = this.components[0]?.id ?? null;
    if (this.data.pm) return;
    // assets without recorded schedules start from their health maintenance plan, as on the Maintenance module
    this.loading = true;
    loadAssetDetail(record.tag).then((d) => {
      if (this.record !== record) return;
      this.loading = false;
      if (d?.health.maintenancePlan.length && !this.data.pm) this.data.pm = schedulesFromPlan(d.health.maintenancePlan, record.manufacturer);
      this.pm = this.data.pm ?? [];
    });
  }

  // ===== Tones =====

  statusTone(status: string): string {
    return status === 'Active' || status === 'Checked Out' ? 'good' : status === 'Disposed' ? 'critical' : 'warn';
  }

  conditionTone(condition: string): string {
    return condition === 'Good' ? 'good' : condition === 'Fair' ? 'warn' : 'critical';
  }

  criticalityTone(level: string): string {
    return level === 'Critical' ? 'critical' : level === 'High' ? 'serious' : level === 'Medium' ? 'warn' : 'good';
  }

  woTone(type: string): string {
    return WO_TYPE_TONES[type] ?? 'warn';
  }

  priorityTone(p: string | undefined): string {
    return PRIORITY_TONES[p ?? ''] ?? 'warn';
  }

  // ===== Maintenance =====

  cost(w: WorkOrder): number {
    return w.labor + w.parts + w.service;
  }

  pmStatus(s: PmSchedule): PmStatus {
    return pmStatus(s, this.today);
  }

  get schedules(): PmSchedule[] {
    return [...this.pm].sort((a, b) => a.nextDue.localeCompare(b.nextDue));
  }

  get nextPm(): PmSchedule | null {
    return this.schedules[0] ?? null;
  }

  daysTo(iso: string): number {
    return daysUntil(iso, this.today);
  }

  get completed(): WorkOrder[] {
    return this.data.workOrders.filter((w) => w.status === 'Completed').sort((a, b) => b.date.localeCompare(a.date));
  }

  get openOrders(): WorkOrder[] {
    return this.data.workOrders.filter((w) => w.status !== 'Completed').sort((a, b) => a.date.localeCompare(b.date));
  }

  isOverdue(w: WorkOrder): boolean {
    return w.status !== 'Completed' && w.date < this.today;
  }

  get overdueOrders(): number {
    return this.openOrders.filter((w) => this.isOverdue(w)).length;
  }

  get pmCompliance(): number {
    return this.pm.length ? Math.round((this.pm.filter((s) => this.pmStatus(s) !== 'Overdue').length / this.pm.length) * 100) : 100;
  }

  private ytd(list: WorkOrder[]): WorkOrder[] {
    return list.filter((w) => w.date.startsWith(String(this.data.year)));
  }

  get costYtd(): number {
    return this.ytd(this.completed).reduce((s, w) => s + this.cost(w), 0);
  }

  get downtimeYtdHours(): number {
    return Math.round(this.ytd(this.completed).reduce((s, w) => s + w.downtimeMin, 0) / 6) / 10;
  }

  get historyRows(): WorkOrder[] {
    return this.historyAll ? this.completed : this.completed.slice(0, HISTORY_ROWS);
  }

  // ===== Service history =====

  get requests(): ServiceRequest[] {
    return [...this.data.serviceRequests].sort((a, b) => b.createdAt.localeCompare(a.createdAt));
  }

  get openRequests(): number {
    return this.data.serviceRequests.filter(isOpen).length;
  }

  get srFigures() {
    return srKpis(this.data.serviceRequests, this.data.srLastMonth);
  }

  /** Completed work orders and resolved requests, newest first. */
  get serviceEvents(): ServiceEvent[] {
    const fromWo: ServiceEvent[] = this.completed.map((w) => ({
      date: w.date, source: 'Work Order', ref: w.id, kind: w.type, description: w.description, by: w.technician, cost: this.cost(w),
    }));
    const fromSr: ServiceEvent[] = this.data.serviceRequests.filter((r) => r.resolvedAt).map((r) => ({
      date: r.resolvedAt.slice(0, 10), source: 'Service Request', ref: r.id, kind: r.category, description: r.title, by: r.assignedTo || r.requestedBy, cost: null,
    }));
    return [...fromWo, ...fromSr].sort((a, b) => b.date.localeCompare(a.date));
  }

  get serviceRows(): ServiceEvent[] {
    return this.historyAll ? this.serviceEvents : this.serviceEvents.slice(0, HISTORY_ROWS);
  }

  get lastService(): ServiceEvent | null {
    return this.serviceEvents[0] ?? null;
  }

  // ===== Components =====

  get component(): AssetComponent | null {
    return this.components.find((c) => c.id === this.componentId) ?? null;
  }

  pickComponent(c: AssetComponent): void {
    this.componentId = c.id;
  }

  /** Earliest scheduled task of the component. */
  nextTask(c: AssetComponent): MaintenanceSchedule | null {
    return [...(c.maintenance?.schedules ?? [])].sort((a, b) => a.nextDue.localeCompare(b.nextDue))[0] ?? null;
  }

  lastDone(c: AssetComponent): string {
    return (c.maintenance?.history ?? []).filter((h) => h.status === 'Completed').map((h) => h.date).sort().pop() ?? '';
  }

  serviceCount(c: AssetComponent): number {
    return (c.serviceHistory ?? []).filter((s) => s.status === 'Completed').length;
  }

  lastServiceOf(c: AssetComponent): ServiceRecord | null {
    return [...(c.serviceHistory ?? [])].filter((s) => s.status === 'Completed').sort((a, b) => b.date.localeCompare(a.date))[0] ?? null;
  }

  componentTone(status: string): string {
    return status === 'Active' ? 'good' : status === 'Under Maintenance' ? 'warn' : status === 'Needs Replacement' ? 'critical' : 'serious';
  }

  healthTone(h: string): string {
    return h === 'Good' ? 'good' : h === 'Fair' ? 'warn' : 'critical';
  }

  get componentLocation(): string {
    return this.asset.location;
  }

  // ===== Actions =====

  openModule(tab?: string): void {
    this.router.navigate(['/assets/maintenance'], { queryParams: { tag: this.asset.tag, ...(tab ? { tab } : {}) } });
  }

  openWorkOrder(w: WorkOrder): void {
    this.router.navigate(['/assets/maintenance'], { queryParams: { tag: this.asset.tag, tab: 'workOrders', wo: w.id } });
  }

  openRequest(r: ServiceRequest): void {
    this.router.navigate(['/assets/service-requests', r.id], { queryParams: { tag: this.asset.tag } });
  }

  openEvent(e: ServiceEvent): void {
    if (e.source === 'Service Request') this.router.navigate(['/assets/service-requests', e.ref], { queryParams: { tag: this.asset.tag } });
    else this.router.navigate(['/assets/maintenance'], { queryParams: { tag: this.asset.tag, tab: 'history', wo: e.ref } });
  }

  newRequest(): void {
    this.router.navigate(['/assets/service-requests/new'], { queryParams: { tag: this.asset.tag } });
  }

  openComponents(): void {
    this.router.navigate(['/assets/components'], { queryParams: { tag: this.asset.tag } });
  }

  exportCsv(): void {
    this.closeMenus();
    const lines =
      this.view === 'maintenance'
        ? [
            ['Work Order', 'Date', 'Type', 'Description', 'Technician', 'Status', 'Cost', 'Downtime (min)'],
            ...this.data.workOrders.map((w) => [w.id, w.date, w.type, w.description, w.technician, w.status, this.cost(w), w.downtimeMin]),
          ]
        : [
            ['Date', 'Source', 'Reference', 'Type', 'Description', 'By', 'Cost'],
            ...this.serviceEvents.map((e) => [e.date, e.source, e.ref, e.kind, e.description, e.by, e.cost ?? '']),
          ];
    const csv = lines.map((l) => l.map((v) => `"${String(v).replace(/"/g, '""')}"`).join(',')).join('\r\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }));
    const a = document.createElement('a');
    a.href = url;
    a.download = `${this.asset.tag}_${this.view === 'maintenance' ? 'maintenance' : 'service_history'}.csv`;
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
