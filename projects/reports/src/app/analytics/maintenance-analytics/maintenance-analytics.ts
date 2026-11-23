import { Component, inject } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Subscription, catchError, forkJoin, of } from 'rxjs';
import {
  ArrowDown, ArrowUp, ArrowUpDown, Bookmark, ChartColumn, Clock, Download, FileText, GitCompareArrows, GripVertical, House, Info,
  LucideAngularModule, Package, RefreshCw, Settings, ShieldCheck, TriangleAlert, Wrench
} from 'lucide-angular';
import { downloadCsv, money, percent } from '../shared/analytics-common';
import { AnalyticsGroup, AnalyticsQuery, apiMessage, filterValue, periodQuery } from '../shared/analytics-api.service';
import { CartesianChart, DonutChart, GaugeChart, RA_COLORS, compactNumber } from '../shared/charts';
import { RaInsight, RaInsightCard, RaKpiCard } from '../asset-performance/kit/ra-cards';
import { RaActionDialog, RaDetailDialog, RaExpandDialog, RaRecords, RaRecordsDialog, RaSaveViewDialog } from '../asset-performance/kit/ra-dialogs';
import { RaMenu } from '../asset-performance/kit/ra-menu';
import { ScopedAnalyticsPage, SortDir, sortRows } from '../asset-performance/kit/scoped-page';
import {
  BacklogRow, MAINTENANCE_TYPES, MaBacklogDto, MaCompareDto, MaFiltersDto, MaInsightDto, MaOverviewDto, MaintenanceView, PRIORITY_RANK, TechnicianRow,
  emptyMaintenance, mapMaintenance, toBacklogRow
} from './maintenance-analytics.data';

type BacklogSortKey = 'priority' | 'id' | 'asset' | 'location' | 'age' | 'technician' | 'status';
type TechSortKey = 'name' | 'completed' | 'firstTimeFix' | 'response' | 'sla';

const GROUP: AnalyticsGroup = 'maintenance-analytics';
const PRIORITY_TONES: Record<string, string> = { Critical: 'red', High: 'pink', Medium: 'orange', Low: 'blue' };
const STATUS_TONES: Record<string, string> = { Open: 'blue', 'In Progress': 'orange', 'On Hold': 'pink', Completed: 'green', Overdue: 'red' };

/** Insight key → card id used by insightAction, with its icon. */
const INSIGHTS: Record<string, { id: string; icon: RaInsight['icon'] }> = {
  overdue_work_orders: { id: 'overdue', icon: TriangleAlert },
  emergency_cost: { id: 'emergency', icon: ChartColumn },
  assets_approaching_failure: { id: 'failure', icon: Settings },
  spare_parts_shortage: { id: 'parts', icon: Package }
};
const INSIGHT_ACTIONS: Record<string, { suffix: string; label: string; primary?: boolean }> = {
  view_work_orders: { suffix: 'view', label: 'View work orders' },
  view_details: { suffix: 'view', label: 'View details' },
  view_assets: { suffix: 'view', label: 'View assets' },
  create_action_plan: { suffix: 'plan', label: 'Create action plan', primary: true },
  open_inventory: { suffix: 'inventory', label: 'Open inventory', primary: true }
};
const INSIGHT_TONES: Record<string, RaInsight['tone']> = { critical: 'red', warning: 'orange', info: 'blue', success: 'green' };

/** Reports & Analytics > Maintenance Analytics: cost, work orders, PM compliance, reliability, backlog and technicians. */
@Component({
  standalone: true,
  selector: 'app-ra-maintenance-analytics',
  imports: [
    FormsModule, RouterLink, NgTemplateOutlet, LucideAngularModule, CartesianChart, DonutChart, GaugeChart, RaMenu, RaKpiCard, RaInsightCard,
    RaRecordsDialog, RaDetailDialog, RaActionDialog, RaSaveViewDialog, RaExpandDialog
  ],
  templateUrl: './maintenance-analytics.html',
  styleUrls: ['../shared/analytics-page.css', '../asset-performance/kit/ra-page.css', './maintenance-analytics.css']
})
export class MaintenanceAnalyticsPage extends ScopedAnalyticsPage {
  private readonly router = inject(Router);
  readonly icons = { House, RefreshCw, GitCompareArrows, Download, Bookmark, GripVertical, Info, ArrowUp, ArrowDown, ArrowUpDown };
  readonly maintenanceTypes = MAINTENANCE_TYPES;
  readonly colors = RA_COLORS;
  readonly rowMenu = ['View details', 'Escalate'];
  readonly costFormat = (v: number) => `AED ${compactNumber(v)}`;
  readonly percentFormat = (v: number) => `${v}%`;
  readonly pctFormat = (v: number) => percent(v);
  protected readonly fileStem = 'maintenance-analytics';
  protected override readonly viewGroup = GROUP;

  maintenanceType = '';
  view: MaintenanceView = emptyMaintenance();
  loaded = false;
  insights: RaInsight[] = [];
  backlogSort: BacklogSortKey = 'priority';
  backlogDir: SortDir = -1;
  techSort: TechSortKey = 'completed';
  techDir: SortDir = -1;
  private loadSub: Subscription | null = null;

  constructor() {
    super();
    this.analyticsApi.filters<MaFiltersDto>(GROUP).subscribe({
      next: (f) => this.applyFilterOptions(f),
      error: (err) => console.warn('[maintenance-analytics] filters failed', err)
    });
    this.loadViews();
    this.recompute();
  }

  get pmTarget(): number {
    return this.view.pmTarget;
  }

  get deltas() {
    return this.view.deltas;
  }

  /** Filter bar as query parameters of the Maintenance Analytics endpoints. */
  private get query(): AnalyticsQuery {
    return {
      ...periodQuery(this.period),
      organization: filterValue(this.organization),
      location: filterValue(this.location),
      category: filterValue(this.category),
      assetType: filterValue(this.assetType),
      maintenanceType: filterValue(this.maintenanceType)
    };
  }

  /** Reloads every widget from GET api/maintenance-analytics/overview (plus /compare-periods when comparing). */
  recompute(): void {
    this.loadSub?.unsubscribe();
    this.refreshing = true;
    const query = this.query;
    this.loadSub = forkJoin([
      this.analyticsApi.overview<MaOverviewDto>(GROUP, { ...query, backlogSize: 200, technicianLimit: 100 }),
      this.compare ? this.analyticsApi.get<MaCompareDto>(GROUP, 'compare-periods', query).pipe(catchError(() => of(null))) : of(null)
    ]).subscribe({
      next: ([o, compare]) => this.show(mapMaintenance(o, compare)),
      error: (err) => {
        this.show(emptyMaintenance());
        this.notify(apiMessage(err, 'Could not load maintenance analytics.'));
      }
    });
  }

  override refresh(): void {
    if (!this.refreshing) this.recompute();
  }

  private show(v: MaintenanceView): void {
    this.view = v;
    this.loaded = true;
    this.refreshing = false;
    this.updatedMinutes = 0;
    const d = v.deltas;
    this.kpis = [
      { id: 'cost', label: 'Maintenance Cost', value: money(v.cost), delta: d.cost, lowerIsBetter: true, icon: Wrench, tone: 'purple' },
      { id: 'open', label: 'Open Work Orders', value: String(v.open), delta: d.open, lowerIsBetter: true, icon: FileText, tone: 'blue' },
      { id: 'pm', label: 'Preventive Compliance', value: this.fmt(v.pmCompliance, this.pctFormat), delta: d.pm, icon: ShieldCheck, tone: 'green' },
      { id: 'mttr', label: 'MTTR', value: this.fmt(v.mttr, (x) => `${Math.round(x * 10) / 10} hrs`), delta: d.mttr, lowerIsBetter: true, icon: Clock, tone: 'orange' },
      { id: 'mtbf', label: 'MTBF', value: this.fmt(v.mtbf, (x) => `${Math.round(x).toLocaleString('en-US')} hrs`), delta: d.mtbf, icon: Settings, tone: 'purple' }
    ];
    this.insights = v.insights.map((i) => this.toInsight(i));
  }

  /** '—' while the API has no value for the period. */
  fmt(value: number | null, format: (v: number) => string): string {
    return value === null ? '—' : format(value);
  }

  private toInsight(i: MaInsightDto): RaInsight {
    const meta = INSIGHTS[i.key] ?? { id: i.key, icon: ChartColumn };
    return {
      id: meta.id,
      tone: INSIGHT_TONES[i.severity] ?? 'blue',
      icon: meta.icon,
      title: i.title,
      text: i.message,
      actions: i.actions.filter((a) => INSIGHT_ACTIONS[a]).map((a) => ({ id: `${meta.id}-${INSIGHT_ACTIONS[a].suffix}`, label: INSIGHT_ACTIONS[a].label, primary: INSIGHT_ACTIONS[a].primary }))
    };
  }

  // ----- Saved views include the maintenance type -----
  protected override snapshot(): Record<string, string> {
    return { ...super.snapshot(), maintenanceType: this.maintenanceType };
  }

  protected override restoreFilters(f: Record<string, string>): void {
    super.restoreFilters(f);
    this.maintenanceType = f['maintenanceType'] ?? '';
  }

  override resetFilters(): void {
    this.maintenanceType = '';
    super.resetFilters();
  }

  get typeLabels(): string[] {
    return this.view.typeSeries.map((s) => s.name);
  }

  // ----- Tables -----
  get backlogRows(): BacklogRow[] {
    const dir = this.backlogDir;
    const key = this.backlogSort;
    const value = (o: BacklogRow): string | number =>
      key === 'priority' ? (PRIORITY_RANK[o.priority] ?? 0) * 1000 + o.ageDays : key === 'age' ? o.ageDays : key === 'asset' ? o.asset
        : key === 'location' ? o.location : key === 'technician' ? o.technician : key === 'status' ? o.status : o.id;
    return [...this.view.backlog]
      .sort((a, b) => {
        const x = value(a);
        const y = value(b);
        return (typeof x === 'number' && typeof y === 'number' ? x - y : String(x).localeCompare(String(y), 'en', { numeric: true })) * dir;
      })
      .slice(0, 5);
  }

  get technicianRows(): TechnicianRow[] {
    return sortRows(this.view.technicians, this.techSort, this.techDir).slice(0, 5);
  }

  sortBacklog(key: BacklogSortKey): void {
    this.backlogDir = this.backlogSort === key ? (this.backlogDir === 1 ? -1 : 1) : key === 'priority' || key === 'age' ? -1 : 1;
    this.backlogSort = key;
  }

  sortTechs(key: TechSortKey): void {
    this.techDir = this.techSort === key ? (this.techDir === 1 ? -1 : 1) : key === 'name' || key === 'response' ? 1 : -1;
    this.techSort = key;
  }

  backlogIcon(key: BacklogSortKey) {
    return this.backlogSort !== key ? this.icons.ArrowUpDown : this.backlogDir === 1 ? this.icons.ArrowUp : this.icons.ArrowDown;
  }

  techIcon(key: TechSortKey) {
    return this.techSort !== key ? this.icons.ArrowUpDown : this.techDir === 1 ? this.icons.ArrowUp : this.icons.ArrowDown;
  }

  priorityTone(p: string): string {
    return PRIORITY_TONES[p] ?? 'grey';
  }

  statusTone(s: string): string {
    return STATUS_TONES[s] ?? 'grey';
  }

  ageTone(days: number): string {
    return days > 14 ? 'red' : days > 7 ? 'orange' : 'green';
  }

  rateColor(v: number): string {
    return v >= 85 ? '#10b981' : v >= 75 ? '#f59e0b' : '#ef4444';
  }

  rowAction(item: string, o: BacklogRow): void {
    if (item === 'Escalate') this.openAction('Action plan', `Escalate ${o.id} – ${o.asset}`, `${o.type} · ${o.status} · ${o.ageDays} days old · assigned to ${o.technician}`, 'High');
    else this.showOrder(o);
  }

  showOrder(o: BacklogRow): void {
    this.openDetail(
      {
        title: o.id,
        subtitle: `${o.type} maintenance · ${o.asset}`,
        fields: [
          { label: 'Asset', value: `${o.asset} (${o.assetId})` },
          { label: 'Location', value: o.location },
          { label: 'Priority', value: o.priority },
          { label: 'Status', value: o.status },
          { label: 'Due date', value: o.due },
          { label: 'Age', value: `${o.ageDays} days` },
          { label: 'Assigned to', value: o.technician },
          { label: 'Waiting for parts', value: o.partsShortage ? 'Yes' : 'No' }
        ],
        primary: o.status === 'Completed' ? undefined : 'Escalate'
      },
      { kind: 'Action plan', title: `Escalate ${o.id} – ${o.asset}`, context: `${o.type} · ${o.status} · ${o.ageDays} days old`, priority: 'High' }
    );
  }

  showTechnician(t: TechnicianRow): void {
    this.openDetail({
      title: t.name,
      subtitle: 'Technician performance',
      fields: [
        { label: 'Assigned', value: t.assigned },
        { label: 'Completed', value: t.completed },
        { label: 'First-time fix rate', value: `${t.firstTimeFix}%` },
        { label: 'Average response', value: `${t.response} hrs` },
        { label: 'SLA compliance', value: `${t.sla}%` }
      ]
    });
  }

  openBacklog(): void {
    this.records = this.orderRecords('Maintenance backlog', 'All work orders not yet completed, highest priority first.', this.view.backlog, 'maintenance-backlog.csv');
  }

  // ----- Insights -----
  private insight(id: string): MaInsightDto | undefined {
    return this.view.insights.find((i) => (INSIGHTS[i.key]?.id ?? i.key) === id);
  }

  /** Work orders behind an insight: GET api/maintenance-analytics/backlog with the insight's filter. */
  private showBacklog(title: string, subtitle: string, extra: AnalyticsQuery, file: string): void {
    this.analyticsApi.get<{ items: MaBacklogDto[] }>(GROUP, 'backlog', { ...this.query, ...extra, pageSize: 200 }).subscribe({
      next: (res) => (this.records = this.orderRecords(title, subtitle, res.items.map(toBacklogRow), file)),
      error: (err) => this.notify(apiMessage(err, 'Could not load the work orders.'))
    });
  }

  insightAction(id: string): void {
    const v = this.view;
    const card = id.slice(0, id.lastIndexOf('-'));
    const insight = this.insight(card);
    switch (id) {
      case 'overdue-view':
        this.showBacklog('Overdue work orders', 'Past their due date, oldest first.', { status: 'Overdue', sortBy: 'ageDays', sortDirection: 'desc' }, 'overdue-work-orders.csv');
        break;
      case 'overdue-plan':
        this.openAction('Action plan', `Clear ${insight?.count ?? 0} overdue work orders`, this.orderList(insight), 'Critical');
        break;
      case 'emergency-view':
        this.records = this.emergencyRecords();
        break;
      case 'emergency-plan':
        this.openAction('Action plan', 'Reduce emergency maintenance spend', insight?.message ?? `Emergency cost ${money(v.emergencyCost)}.`, 'High');
        break;
      case 'failure-view':
        this.records = this.assetRecords(insight);
        break;
      case 'failure-plan':
        this.openAction('Action plan', `Prevent failure of ${insight?.count ?? 0} high-risk assets`, insight?.assets.map((a) => a.assetName ?? a.assetId).join(', ') || 'No assets in this view.', 'Critical');
        break;
      case 'parts-view':
        this.showBacklog('Work orders waiting for spare parts', 'On hold until the required parts arrive.', { partsShortage: true }, 'work-orders-waiting-for-parts.csv');
        break;
      case 'parts-plan':
        this.openAction('Action plan', 'Resolve spare parts shortages', insight?.message ?? '', 'High');
        break;
      case 'parts-inventory':
        this.router.navigate([this.basePath, 'inventory-analytics']);
        break;
    }
  }

  private orderList(insight: MaInsightDto | undefined): string {
    const ids = (insight?.workOrders ?? []).map((o) => o.workOrderId ?? o.id);
    const head = ids.slice(0, 5).join(', ');
    return ids.length > 5 ? `${head} and ${ids.length - 5} more.` : head ? `${head}.` : 'No work orders in this view.';
  }

  private orderRecords(title: string, subtitle: string, list: BacklogRow[], file: string): RaRecords {
    return {
      title: `${title} (${list.length})`,
      subtitle,
      columns: [
        { key: 'id', label: 'Work Order' }, { key: 'type', label: 'Type' }, { key: 'priority', label: 'Priority' }, { key: 'asset', label: 'Asset' },
        { key: 'location', label: 'Location' }, { key: 'age', label: 'Age (days)', num: true }, { key: 'technician', label: 'Assigned To' },
        { key: 'status', label: 'Status' }, { key: 'due', label: 'Due Date' }
      ],
      rows: list.map((o) => ({
        id: o.id, type: o.type, priority: o.priority, asset: o.asset, location: o.location, age: o.ageDays, technician: o.technician, status: o.status, due: o.due
      })),
      file
    };
  }

  private assetRecords(insight: MaInsightDto | undefined): RaRecords {
    const list = insight?.assets ?? [];
    return {
      title: `Assets approaching failure threshold (${list.length})`,
      subtitle: insight?.message ?? '',
      columns: [
        { key: 'id', label: 'Asset ID' }, { key: 'name', label: 'Asset Name' }, { key: 'location', label: 'Location' }, { key: 'health', label: 'Health', num: true },
        { key: 'reasons', label: 'Reasons' }
      ],
      rows: list.map((a) => ({
        id: a.assetId ?? a.id, name: a.assetName ?? '—', location: a.location ?? '—', health: a.healthScore ?? '—', reasons: a.reasons.join('; ')
      })),
      file: 'assets-approaching-failure.csv'
    };
  }

  private emergencyRecords(): RaRecords {
    const v = this.view;
    const emergency = v.monthlyEmergency;
    const totals = v.monthlyTotal;
    const target = v.emergencyTarget;
    return {
      title: 'Emergency maintenance cost',
      subtitle: target === null ? `Period total ${money(v.emergencyCost)}.` : `Period total ${money(v.emergencyCost)} against a target of ${money(target)}.`,
      columns: [
        { key: 'month', label: 'Month' }, { key: 'emergency', label: 'Emergency (AED)', num: true }, { key: 'total', label: 'Total (AED)', num: true },
        { key: 'share', label: 'Share of spend %', num: true }
      ],
      rows: v.labels.map((l, i) => ({
        month: l.replace('\n', ' '),
        emergency: emergency[i] ?? 0,
        total: totals[i] ?? 0,
        share: totals[i] ? Math.round(((emergency[i] ?? 0) / totals[i]) * 1000) / 10 : 0
      })),
      file: 'emergency-maintenance-cost.csv'
    };
  }

  // ----- Export -----
  protected kpiTarget(id: string): string {
    const targets: Record<string, string> = { cost: 'ma-cost', open: 'ma-status', pm: 'ma-pm', mttr: 'ma-reliability', mtbf: 'ma-reliability' };
    return targets[id] ?? 'ma-cost';
  }

  /** Excel workbook of every section for the current filters (GET api/maintenance-analytics/export). */
  exportPage(): void {
    this.analyticsApi.export(GROUP, this.query).subscribe({
      next: () => this.notify(`Exported ${this.view.total} work orders.`),
      error: (err) => this.notify(apiMessage(err, 'Export failed.'))
    });
  }

  exportWidget(id: string): void {
    const v = this.view;
    const months = v.labels.map((l) => l.replace('\n', ' '));
    const series = (file: string, list: { name: string; values: number[] }[]) =>
      downloadCsv(file, ['Month', ...list.map((s) => s.name)], months.map((m, i) => [m, ...list.map((s) => s.values[i])]));
    switch (id) {
      case 'cost':
        series('maintenance-cost-trend.csv', v.costTrend);
        break;
      case 'types':
        downloadCsv('work-orders-by-type.csv', ['Type', 'Work Orders'], v.typeSeries.map((s, i) => [s.name, s.values[i]]));
        break;
      case 'status':
        downloadCsv('work-order-status.csv', ['Status', 'Work Orders'], v.statusSlices.map((s) => [s.name, s.value]));
        break;
      case 'pm':
        series('preventive-maintenance-compliance.csv', v.pmTrend);
        break;
      case 'reliability':
        series('mttr-vs-mtbf.csv', v.reliability);
        break;
      case 'downtime':
        series('downtime-by-cause.csv', v.downtimeByCause);
        break;
      case 'backlog':
        downloadCsv('maintenance-backlog.csv', ['Priority', 'Work Order', 'Asset', 'Location', 'Age (days)', 'Assigned To', 'Status'],
          v.backlog.map((o) => [o.priority, o.id, o.asset, o.location, o.ageDays, o.technician, o.status]));
        break;
      case 'technicians':
        downloadCsv('technician-performance.csv', ['Technician', 'Completed', 'First-Time Fix %', 'Avg. Response (hrs)', 'SLA Compliance %'],
          v.technicians.map((t) => [t.name, t.completed, t.firstTimeFix, t.response, t.sla]));
        break;
    }
  }
}
