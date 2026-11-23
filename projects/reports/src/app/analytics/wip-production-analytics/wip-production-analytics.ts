import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subscription, catchError, map, of, switchMap } from 'rxjs';
import {
  ChartColumn, ChartColumnIncreasing, CircleCheck, CircleDollarSign, Factory, LucideAngularModule, Settings, TriangleAlert
} from 'lucide-angular';
import { downloadCsv, money } from '../shared/analytics-common';
import {
  AnalyticsApiService, AnalyticsGroup, AnalyticsQuery, apiMessage, filterValue, optionLabels, periodQuery, previousQuery, seriesValues, viewDates
} from '../shared/analytics-api.service';
import { CartesianChart, ChartSeries, ChartSlice, DonutChart, RA_COLORS } from '../shared/charts';
import {
  ALL_LINES, ALL_PLANTS, ALL_PRODUCTS, ALL_WORK_CENTERS, DOWNTIME_CAUSES, PaFiltersDto, PaInsightDto, PaOrderDto, PaOverviewDto, ProductionOrder, ProductionView,
  WIP_STAGES, WIP_STAGE_COLORS, WORK_ORDER_COLORS, WORK_ORDER_STATUSES, WorkCenter, emptyProductionView, mapProduction, toOrder
} from './wip-production-analytics.data';
import { OrderMenuAction, ProductionOrderTable, WorkCenterTable } from './production-tables';
import {
  InsightCard, InsightPick, KpiCardData, RaDashboardPage, RaDetailsDialog, RaFormDialog, RaInsightsPanel, RaKpiCard, RaListDialog, RaPageHead,
  RaWidget, fmtDate, fmtInt, sumOf
} from '../inventory-analytics/parts';

const GROUP: AnalyticsGroup = 'production-analytics';
const INSIGHT_IDS: Record<string, string> = { line_utilization: 'line', orders_at_risk: 'risk', quality_hold: 'hold', material_shortage: 'shortage' };
const INSIGHT_ICONS: Record<string, InsightCard['icon']> = { line: TriangleAlert, risk: ChartColumnIncreasing, hold: TriangleAlert, shortage: Settings };
const INSIGHT_TONES: Record<string, InsightCard['tone']> = { critical: 'red', warning: 'orange', info: 'blue', success: 'green' };

/** Reports & Analytics > WIP / Production Analytics */
@Component({
  selector: 'app-ra-wip-production-analytics',
  imports: [
    FormsModule, LucideAngularModule, CartesianChart, DonutChart, RaPageHead, RaKpiCard, RaWidget, RaInsightsPanel, RaListDialog, RaDetailsDialog,
    RaFormDialog, ProductionOrderTable, WorkCenterTable
  ],
  templateUrl: './wip-production-analytics.html',
  styleUrls: ['../shared/analytics-page.css', './wip-production-analytics.css']
})
export class WipProductionAnalyticsPage extends RaDashboardPage implements OnInit {
  private readonly router = inject(Router);
  private readonly api = inject(AnalyticsApiService);
  protected readonly slug = 'wip-production-analytics';

  plants = [ALL_PLANTS];
  workCenters = [ALL_WORK_CENTERS];
  products = [ALL_PRODUCTS];
  plant = '';
  line = '';
  workCenter = '';
  product = '';
  lineOptions: string[] = [ALL_LINES];

  view: ProductionView = emptyProductionView('');
  kpis: KpiCardData[] = [];
  insights: InsightCard[] = [];

  // Chart inputs, rebuilt when the data changes so the charts only re-render then
  outputSeries: ChartSeries[] = [];
  stageSeries: ChartSeries[] = [];
  statusSlices: ChartSlice[] = [];
  lineLabels: string[] = [];
  lineSeries: ChartSeries[] = [];
  downtimeSeries: ChartSeries[] = [];
  efficiencySeries: ChartSeries[] = [];
  totalOrdersText = '';

  readonly downtimeLabels = DOWNTIME_CAUSES.map((c) => c.replace(' ', '\n'));
  readonly pct = (v: number) => `${Math.round(v)}%`;
  readonly hours = (v: number) => `${Math.round(v)} h`;
  private loadSub: Subscription | null = null;

  ngOnInit(): void {
    this.api.filters<PaFiltersDto>(GROUP).subscribe({
      next: (f) => {
        this.organizations = optionLabels('All organizations', f.organizations);
        this.plants = optionLabels(ALL_PLANTS, f.plants);
        this.lineOptions = optionLabels(ALL_LINES, f.productionLines);
        this.workCenters = optionLabels(ALL_WORK_CENTERS, f.workCenters);
        this.products = optionLabels(ALL_PRODUCTS, f.products);
      },
      error: (err) => console.warn('[production-analytics] filters failed', err)
    });
    this.apply();
  }

  override get scopeText(): string {
    return `${this.periodLabel} · ${this.organizationLabel} · ${this.plant || ALL_PLANTS}`;
  }

  setCompare(on: boolean): void {
    this.compare = on;
    this.apply();
  }

  /** Filter bar as query parameters of the Production Analytics endpoints. */
  private get query(): AnalyticsQuery {
    return {
      ...periodQuery(this.period),
      organization: filterValue(this.organization),
      plant: filterValue(this.plant),
      productionLine: filterValue(this.line),
      workCenter: filterValue(this.workCenter),
      product: filterValue(this.product)
    };
  }

  /** Reloads every widget from GET api/production-analytics/overview (plus the previous period when comparing). */
  apply(): void {
    this.loadSub?.unsubscribe();
    this.refreshing = true;
    const query = this.query;
    const compare = this.compare;
    this.loadSub = this.api
      .overview<PaOverviewDto>(GROUP, { ...query, ordersSize: 200, bottleneckLimit: 50 })
      .pipe(
        switchMap((o) =>
          compare
            ? this.api.overview<PaOverviewDto>(GROUP, { ...query, datePreset: undefined, ...previousQuery(o.period), ordersSize: 1, bottleneckLimit: 1 }).pipe(
                map((p): [PaOverviewDto, PaOverviewDto | null] => [o, p]),
                catchError(() => of<[PaOverviewDto, PaOverviewDto | null]>([o, null]))
              )
            : of<[PaOverviewDto, PaOverviewDto | null]>([o, null])
        )
      )
      .subscribe({
        next: ([o, previous]) => this.show(mapProduction(o, this.periodLabel), previous),
        error: (err) => {
          this.show(emptyProductionView(this.periodLabel), null);
          this.notify(apiMessage(err, 'Could not load production analytics.'));
        }
      });
  }

  override refresh(): void {
    if (!this.refreshing) this.apply();
  }

  private show(v: ProductionView, previous: PaOverviewDto | null): void {
    this.view = v;
    this.refreshing = false;
    this.updatedMinutes = 0;
    this.buildKpis(v);
    this.buildCharts(v, previous);
    this.buildInsights(v);
  }

  private buildKpis(v: ProductionView): void {
    const d = v.deltas;
    const p = v.previous;
    const pctText = (x: number | null) => (x === null ? '—' : `${x.toFixed(1)}%`);
    const unit = v.outputUnit;
    this.kpis = [
      { id: 'production-output', label: 'Production Output', value: `${fmtInt(v.output)} ${unit}`, delta: d.output, goodWhenUp: true, previous: p.output === null ? '—' : `${fmtInt(p.output)} ${unit}`, tone: 'purple', icon: Factory },
      { id: 'wip-value', label: 'WIP Value', value: money(v.wipValue), delta: d.wip, goodWhenUp: true, previous: p.wip === null ? '—' : money(p.wip), tone: 'green', icon: CircleDollarSign },
      { id: 'on-time-completion', label: 'On-Time Completion', value: pctText(v.onTime), delta: d.onTime, goodWhenUp: true, previous: pctText(p.onTime), tone: 'orange', icon: CircleCheck },
      { id: 'equipment-utilization', label: 'Equipment Utilization', value: v.utilization === null ? '—' : `${Math.round(v.utilization)}%`, delta: d.utilization, goodWhenUp: true, previous: pctText(p.utilization), tone: 'blue', icon: Settings },
      { id: 'oee', label: 'Overall Equipment Effectiveness', value: pctText(v.oee), delta: d.oee, goodWhenUp: true, previous: pctText(p.oee), tone: 'purple', icon: ChartColumn }
    ];
  }

  private buildCharts(v: ProductionView, previous: PaOverviewDto | null): void {
    const target = v.utilizationTarget;
    this.outputSeries = [
      { name: 'Actual', values: v.actual, color: RA_COLORS.purple, type: 'bar' },
      { name: 'Planned', values: v.planned, color: RA_COLORS.green, type: 'line' }
    ];
    if (v.target.length) this.outputSeries.push({ name: 'Target', values: v.target, color: RA_COLORS.pink, type: 'line', dashed: true });
    if (previous) {
      this.outputSeries.push({ name: 'Actual (previous period)', values: seriesValues(previous.outputTrend, 'Actual'), color: RA_COLORS.grey, type: 'line', dashed: true });
    }
    this.stageSeries = WIP_STAGES.map((name, i) => ({ name, values: v.stageStacks[i], color: WIP_STAGE_COLORS[i], type: 'bar' as const }));
    this.statusSlices = WORK_ORDER_STATUSES.map((name, i) => ({ name, value: v.workOrders[i], color: WORK_ORDER_COLORS[i] }));
    this.totalOrdersText = fmtInt(sumOf(v.workOrders));

    // Bars are coloured by band (one stacked series per band, only one band has a value per line)
    this.lineLabels = v.lines.map((l) => `Production\n${l.name}`);
    const band = (lo: number, hi: number) => v.lines.map((l) => (l.utilization >= lo && l.utilization < hi ? l.utilization : 0));
    this.lineSeries = [
      { name: `At target (≥ ${target}%)`, values: band(target, 101), color: RA_COLORS.green, type: 'bar' },
      { name: `Watch (${target - 10}–${target}%)`, values: band(target - 10, target), color: RA_COLORS.orange, type: 'bar' },
      { name: `Below (< ${target - 10}%)`, values: band(0, target - 10), color: RA_COLORS.red, type: 'bar' },
      { name: 'Target', values: v.lines.map(() => target), color: RA_COLORS.purple, type: 'line', dashed: true }
    ];

    this.downtimeSeries = [{ name: 'Downtime', values: v.downtime, color: RA_COLORS.pink, type: 'bar' }];
    this.efficiencySeries = [
      { name: 'OEE', values: v.oeeTrend, color: RA_COLORS.purple },
      { name: 'Availability', values: v.availability, color: RA_COLORS.green },
      { name: 'Performance', values: v.performance, color: RA_COLORS.orange },
      { name: 'Quality', values: v.quality, color: RA_COLORS.pink }
    ];
    if (previous) {
      this.efficiencySeries.push({ name: 'OEE (previous period)', values: seriesValues(previous.efficiencyTrend, 'OEE'), color: RA_COLORS.grey, dashed: true });
    }
  }

  /** Insight cards from the API; cards with nothing to report are left out, as before. */
  private buildInsights(v: ProductionView): void {
    this.insights = v.insights
      .filter((i) => INSIGHT_IDS[i.key] && i.count > 0)
      .map((i) => {
        const id = INSIGHT_IDS[i.key];
        const actions = i.actions.map((a) =>
          a === 'open_work_center' ? { id: 'open', label: 'Open work center', primary: true }
            : a === 'create_action_plan' ? { id: 'plan', label: 'Create action plan', primary: true }
              : { id: 'view', label: 'View orders' }
        );
        return { id, tone: INSIGHT_TONES[i.severity] ?? 'blue', icon: INSIGHT_ICONS[id], title: i.title, text: i.message, actions };
      });
  }

  private insightDto(id: string): PaInsightDto | undefined {
    return this.view.insights.find((i) => INSIGHT_IDS[i.key] === id);
  }

  // ===== Insight + table actions =====

  onInsight(p: InsightPick): void {
    const dto = this.insightDto(p.insight.id);
    if (!dto) return;
    if (p.action.id === 'open') {
      this.router.navigateByUrl('/wip-operations/resources');
      return;
    }
    if (p.action.id === 'plan') {
      this.openActionPlan(dto.title);
      return;
    }
    // View orders: GET api/production-analytics/orders with the insight's filter
    const extra: AnalyticsQuery = Object.fromEntries(new URLSearchParams(dto.query));
    if (dto.key === 'material_shortage') delete extra['cause'];
    if (dto.key === 'line_utilization' && !extra['productionLine'] && dto.lines.length) {
      const lines = new Set(dto.lines);
      this.showOrders(this.view.orders.filter((o) => lines.has(o.line)), p.insight.title);
      return;
    }
    this.api.get<{ items: PaOrderDto[] }>(GROUP, 'orders', { ...this.query, ...extra, pageSize: 200 }).subscribe({
      next: (res) => this.showOrders(res.items.map(toOrder), p.insight.title),
      error: (err) => this.notify(apiMessage(err, 'Could not load the orders.'))
    });
  }

  private showOrders(rows: ProductionOrder[], title: string): void {
    this.openList({
      title,
      subtitle: this.scopeText,
      columns: ['Order No', 'Product', 'Line', 'Work Center', 'Planned Qty', 'Completed Qty', 'WIP Qty', 'WIP Value', 'Due Date', 'Status', 'Priority'],
      numeric: [4, 5, 6, 7],
      rows: [...rows].sort((a, b) => a.due.localeCompare(b.due))
        .map((o) => [o.code, o.product, o.line, o.workCenter, o.planned, o.completed, o.wip, money(o.wipValue), fmtDate(o.due), o.status, o.priority]),
      fileName: 'production-orders.csv'
    });
  }

  orderAction(e: { order: ProductionOrder; action: OrderMenuAction }): void {
    const o = e.order;
    if (e.action === 'plan') {
      this.openActionPlan(`Action plan for ${o.code} ${o.product} (${o.status})`);
      return;
    }
    this.openDetails({
      title: `${o.code} · ${o.product}`,
      subtitle: `${o.line} · ${o.site}`,
      items: [
        { label: 'Status', value: `${o.orderStatus} · ${o.schedule}` },
        { label: 'Stage', value: o.stage },
        { label: 'Current work center', value: o.workCenter },
        { label: 'Progress', value: `${o.completed.toLocaleString('en-US')} of ${o.planned.toLocaleString('en-US')} (${Math.round(o.progress)}%)` },
        { label: 'WIP', value: `${o.wip.toLocaleString('en-US')} ${o.unit} · ${money(o.wipValue)}` },
        { label: 'Due date', value: fmtDate(o.due) },
        { label: 'Priority', value: o.priority }
      ]
    });
  }

  viewAllOrders(): void {
    this.showOrders(this.view.orders, 'Active production orders');
  }

  viewWorkCenter(c: WorkCenter & { risk: string }): void {
    const orders = this.view.orders.filter((o) => o.workCenter === c.code).length;
    this.openDetails({
      title: c.code,
      subtitle: `${c.name} · ${this.scopeText}`,
      items: [
        { label: 'Queue', value: `${c.queue} orders` },
        { label: 'In progress', value: `${c.inProgress} orders` },
        { label: 'Average wait', value: `${c.avgWait} hrs` },
        { label: 'Capacity used', value: `${c.capacity}%` },
        { label: 'Bottleneck risk', value: c.risk },
        { label: 'Active orders here', value: String(orders) }
      ]
    });
  }

  // ===== Saved views =====

  /** Save View: POST api/production-analytics/views with the current filters. */
  override saveView(name: string): void {
    const filters = {
      organization: filterValue(this.organization) ?? null,
      plant: filterValue(this.plant) ?? null,
      productionLine: filterValue(this.line) ?? null,
      workCenter: filterValue(this.workCenter) ?? null,
      product: filterValue(this.product) ?? null,
      ...viewDates(this.period)
    };
    this.api.createView(GROUP, name, filters).subscribe({
      next: () => super.saveView(name),
      error: (err) => this.notify(apiMessage(err, 'Could not save the view.'))
    });
  }

  // ===== Exports =====

  /** Excel workbook of every section for the current filters (GET api/production-analytics/export). */
  exportPage(): void {
    this.api.export(GROUP, this.query).subscribe({
      next: () => this.notify('Production analytics exported.'),
      error: (err) => this.notify(apiMessage(err, 'Export failed.'))
    });
  }

  exportOrders(): void {
    downloadCsv('active-production-orders.csv', ['Order No', 'Product', 'Line', 'Plant', 'Work Center', 'Planned Qty', 'Completed Qty', 'WIP Qty', 'WIP Value (AED)', 'Due Date', 'Status'],
      this.view.orders.map((o) => [o.code, o.product, o.line, o.site, o.workCenter, o.planned, o.completed, o.wip, Math.round(o.wipValue), o.due, o.status]));
  }

  exportWorkCenters(): void {
    downloadCsv('bottleneck-work-centers.csv', ['Work Center', 'Line', 'Queue (Orders)', 'Avg. Wait (hrs)', 'Capacity %', 'Risk'],
      this.view.workCenters.map((c) => [c.code, c.name, c.queue, c.avgWait, c.capacity, c.risk]));
  }

  private exportSeries(file: string, labels: string[], series: ChartSeries[]): void {
    downloadCsv(file, ['', ...series.map((s) => s.name)], labels.map((l, i) => [l.replace('\n', ' '), ...series.map((s) => Math.round((s.values[i] ?? 0) * 10) / 10)]));
  }

  exportOutput(): void {
    this.exportSeries('production-output.csv', this.view.window.labels, this.outputSeries);
  }

  exportStages(): void {
    this.exportSeries('wip-by-stage.csv', this.view.window.labels, this.stageSeries);
  }

  exportStatus(): void {
    downloadCsv('work-order-status.csv', ['Status', 'Work Orders'], this.statusSlices.map((s) => [s.name, s.value]));
  }

  exportLines(): void {
    downloadCsv('equipment-utilization-by-line.csv', ['Line', 'Work Centers', 'Utilization %', 'Target %'], this.view.lines.map((l) => [l.name, l.workCenters, l.utilization, l.target]));
  }

  exportDowntime(): void {
    downloadCsv('downtime-by-cause.csv', ['Cause', 'Hours'], DOWNTIME_CAUSES.map((c, i) => [c, this.view.downtime[i]]));
  }

  exportEfficiency(): void {
    this.exportSeries('production-efficiency.csv', this.view.window.labels, this.efficiencySeries);
  }
}
