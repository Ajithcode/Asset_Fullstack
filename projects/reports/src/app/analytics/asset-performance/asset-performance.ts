import { Component } from '@angular/core';
import { Subscription, catchError, map, of, switchMap } from 'rxjs';
import { NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  ArrowDown, ArrowUp, ArrowUpDown, Bookmark, Box, Calendar, ChartColumn, Clock, Download, GitCompareArrows, GripVertical, Heart, House, Info,
  LucideAngularModule, RefreshCw, Settings, ShieldCheck, TriangleAlert, Wrench
} from 'lucide-angular';
import { downloadCsv, percent } from '../shared/analytics-common';
import { AnalyticsGroup, AnalyticsQuery, apiMessage, filterValue, periodQuery, previousQuery } from '../shared/analytics-api.service';
import { CartesianChart, HBarChart } from '../shared/charts';
import { RaAsset } from './kit/asset-universe';
import { RaInsight, RaInsightCard, RaKpiCard } from './kit/ra-cards';
import { RaActionAsset, RaActionDialog, RaActionForm, RaDetailDialog, RaExpandDialog, RaRecords, RaRecordsDialog, RaSaveViewDialog } from './kit/ra-dialogs';
import { RaMenu } from './kit/ra-menu';
import { SavedView, ScopedAnalyticsPage, SortDir, sortRows } from './kit/scoped-page';
import {
  ApAssetDto, ApAssetRow, ApFiltersDto, ApInsightDto, ApLocationRow, ApOverviewDto, AssetPerformanceView, emptyAssetPerformance, insightAsset,
  mapAssetPerformance, toRaAsset
} from './asset-performance.data';

type AssetSortKey = 'id' | 'name' | 'location' | 'health' | 'utilization' | 'availability';
type LocationSortKey = 'name' | 'health' | 'utilization' | 'availability';

const GROUP: AnalyticsGroup = 'asset-performance';

/** Insight key → card id used by the template actions. */
const INSIGHT_IDS: Record<string, string> = { below_health_threshold: 'critical', maintenance_intervention: 'intervention', utilization_below_target: 'utilization' };
const INSIGHT_TONES: Record<string, RaInsight['tone']> = { critical: 'red', warning: 'orange', info: 'blue', success: 'green' };

/** Reports & Analytics > Asset Performance: health, utilization, reliability, downtime and asset ranking. */
@Component({
  standalone: true,
  selector: 'app-ra-asset-performance',
  imports: [
    FormsModule, RouterLink, NgTemplateOutlet, LucideAngularModule, CartesianChart, HBarChart, RaMenu, RaKpiCard, RaInsightCard,
    RaRecordsDialog, RaDetailDialog, RaActionDialog, RaSaveViewDialog, RaExpandDialog
  ],
  templateUrl: './asset-performance.html',
  styleUrls: ['../shared/analytics-page.css', './kit/ra-page.css', './asset-performance.css']
})
export class AssetPerformancePage extends ScopedAnalyticsPage {
  readonly icons = {
    House, RefreshCw, GitCompareArrows, Download, Bookmark, ChartColumn, Calendar, GripVertical, Info, ArrowUp, ArrowDown, ArrowUpDown, Settings, Clock
  };
  readonly rowMenu = ['View details', 'Create work order'];
  readonly percentFormat = (v: number) => `${v}%`;
  readonly mtbfFormat = (v: number) => `${Math.round(v).toLocaleString('en-US')} hrs`;
  readonly mttrFormat = (v: number) => `${Math.round(v * 10) / 10} hrs`;
  protected readonly fileStem = 'asset-performance';
  protected override readonly viewGroup = GROUP;

  view: AssetPerformanceView = emptyAssetPerformance();
  /** The first overview response has arrived (the empty notice waits for it) */
  loaded = false;
  insights: RaInsight[] = [];
  /** Technicians a work order can be assigned to */
  assignees: string[] = [];
  currentView = '';
  assetSort: AssetSortKey = 'health';
  assetDir: SortDir = -1;
  locationSort: LocationSortKey = 'health';
  locationDir: SortDir = -1;
  private loadSub: Subscription | null = null;

  constructor() {
    super();
    this.views = [{ name: 'All Assets', filters: this.snapshot() }];
    this.analyticsApi.filters<ApFiltersDto>(GROUP).subscribe({
      next: (f) => this.applyFilterOptions(f),
      error: (err) => console.warn('[asset-performance] filters failed', err)
    });
    this.loadViews();
    this.analyticsApi.assignees().subscribe((names) => (this.assignees = names));
    this.recompute();
  }

  get utilizationTarget(): number {
    return this.view.utilizationTarget;
  }

  get deltas() {
    return this.view.deltas;
  }

  /** Filter bar as query parameters of the Asset Performance endpoints. */
  private get query(): AnalyticsQuery {
    return {
      ...periodQuery(this.period),
      organization: filterValue(this.organization),
      location: filterValue(this.location),
      category: filterValue(this.category),
      assetType: filterValue(this.assetType)
    };
  }

  /** Reloads every widget from GET api/asset-performance/overview (plus the previous period when comparing). */
  recompute(): void {
    this.loadSub?.unsubscribe();
    this.refreshing = true;
    const query = this.query;
    const compare = this.compare;
    this.loadSub = this.analyticsApi
      .overview<ApOverviewDto>(GROUP, query)
      .pipe(
        switchMap((o) =>
          compare
            ? this.analyticsApi.overview<ApOverviewDto>(GROUP, { ...query, datePreset: undefined, ...previousQuery(o.period) }).pipe(
                map((p): [ApOverviewDto, ApOverviewDto | null] => [o, p]),
                catchError(() => of<[ApOverviewDto, ApOverviewDto | null]>([o, null]))
              )
            : of<[ApOverviewDto, ApOverviewDto | null]>([o, null])
        )
      )
      .subscribe({
        next: ([o, previous]) => this.show(mapAssetPerformance(o, previous)),
        error: (err) => {
          this.show(emptyAssetPerformance());
          this.notify(apiMessage(err, 'Could not load asset performance.'));
        }
      });
  }

  override refresh(): void {
    if (!this.refreshing) this.recompute();
  }

  private show(v: AssetPerformanceView): void {
    this.view = v;
    this.loaded = true;
    this.refreshing = false;
    this.updatedMinutes = 0;
    const d = v.deltas;
    this.kpis = [
      { id: 'total', label: 'Total Assets', value: v.total.toLocaleString('en-US'), delta: d.total, icon: Box, tone: 'purple' },
      { id: 'health', label: 'Average Health Score', value: this.fmt(v.health, (x) => `${Math.round(x)}%`), delta: d.health, icon: Heart, tone: 'blue' },
      { id: 'utilization', label: 'Utilization Rate', value: this.fmt(v.utilization, (x) => `${Math.round(x)}%`), delta: d.utilization, icon: ChartColumn, tone: 'orange' },
      { id: 'availability', label: 'Availability', value: this.fmt(v.availability, (x) => percent(x)), delta: d.availability, icon: ShieldCheck, tone: 'green' },
      { id: 'mtbf', label: 'MTBF', value: this.fmt(v.mtbf, (x) => `${Math.round(x).toLocaleString('en-US')} hrs`), delta: d.mtbf, icon: Settings, tone: 'purple' }
    ];
    this.insights = v.total ? v.insights.map((i) => this.toInsight(i)) : [];
  }

  /** '—' while the API has no value (e.g. no asset has a health score yet). */
  fmt(value: number | null, format: (v: number) => string = (v) => String(Math.round(v * 10) / 10)): string {
    return value === null ? '—' : format(value);
  }

  private toInsight(i: ApInsightDto): RaInsight {
    const id = INSIGHT_IDS[i.key] ?? i.key;
    const icon = id === 'critical' ? (i.count ? TriangleAlert : ShieldCheck) : id === 'intervention' ? Wrench : ChartColumn;
    const actions: RaInsight['actions'] = [];
    for (const a of i.actions) {
      if (a === 'view_assets') actions.push({ id: `${id}-view`, label: 'View assets' });
      else if (a === 'view_locations') actions.push({ id: `${id}-view`, label: 'View locations' });
      else if (a === 'create_work_order' && i.assets.length) actions.push({ id: `${id}-wo`, label: 'Create work order', primary: true });
    }
    return { id, tone: i.count ? INSIGHT_TONES[i.severity] ?? 'blue' : 'green', icon, title: i.title, text: i.message, actions: i.count || id === 'utilization' ? actions : [] };
  }

  // ----- Saved views -----
  override confirmSaveView(name: string): void {
    super.confirmSaveView(name);
    this.currentView = name;
  }

  protected override viewSaveFailed(view: SavedView): void {
    if (this.currentView === view.name) this.currentView = '';
  }

  // ----- Tables -----
  get assetRows(): ApAssetRow[] {
    return sortRows(this.view.topBottom, this.assetSort, this.assetDir);
  }

  get locationRows(): ApLocationRow[] {
    return sortRows(this.view.locations, this.locationSort, this.locationDir);
  }

  sortAssets(key: AssetSortKey): void {
    this.assetDir = this.assetSort === key ? (this.assetDir === 1 ? -1 : 1) : key === 'id' || key === 'name' || key === 'location' ? 1 : -1;
    this.assetSort = key;
  }

  sortLocations(key: LocationSortKey): void {
    this.locationDir = this.locationSort === key ? (this.locationDir === 1 ? -1 : 1) : key === 'name' ? 1 : -1;
    this.locationSort = key;
  }

  assetSortIcon(key: AssetSortKey) {
    return this.assetSort !== key ? this.icons.ArrowUpDown : this.assetDir === 1 ? this.icons.ArrowUp : this.icons.ArrowDown;
  }

  locationSortIcon(key: LocationSortKey) {
    return this.locationSort !== key ? this.icons.ArrowUpDown : this.locationDir === 1 ? this.icons.ArrowUp : this.icons.ArrowDown;
  }

  scoreTone(score: number): string {
    return score >= 60 ? 'green' : score >= 40 ? 'orange' : 'red';
  }

  scoreColor(score: number): string {
    return score >= 80 ? '#059669' : score >= 60 ? '#2563eb' : score >= 40 ? '#d97706' : '#dc2626';
  }

  meterColor(utilization: number): string {
    return utilization >= this.utilizationTarget ? '#10b981' : utilization >= this.utilizationTarget - 10 ? '#f59e0b' : '#ef4444';
  }

  private woRequest(a: RaAsset, priority: string) {
    return {
      kind: 'Work order' as const,
      title: `Performance review – ${a.name}`,
      context: `${a.id} · ${a.site.name}`,
      priority,
      assets: [{ assetId: a.id, assetName: a.name, location: a.site.name }]
    };
  }

  rowAction(item: string, row: ApAssetRow): void {
    if (item === 'Create work order') {
      const r = this.woRequest(row.asset, row.health < 40 ? 'Critical' : 'Medium');
      this.detail = null;
      this.action = r;
    } else this.showAsset(row.asset);
  }

  showAsset(a: RaAsset): void {
    this.openDetail(
      {
        title: a.name,
        subtitle: `${a.id} · ${a.site.name}`,
        fields: [
          { label: 'Category', value: a.category },
          { label: 'Asset type', value: a.type },
          { label: 'Health score', value: `${a.health} / 100` },
          { label: 'Utilization', value: percent(a.utilization) },
          { label: 'Availability', value: percent(a.availability) },
          { label: 'MTBF', value: `${a.mtbf.toLocaleString('en-US')} hrs` }
        ],
        primary: 'Create work order'
      },
      this.woRequest(a, a.health < 40 ? 'Critical' : 'Medium')
    );
  }

  showLocation(l: ApLocationRow): void {
    this.openDetail({
      title: l.name,
      subtitle: l.status,
      fields: [
        { label: 'Assets', value: l.assets.toLocaleString('en-US') },
        { label: 'Average health score', value: l.health },
        { label: 'Utilization', value: `${l.utilization}% (target ${this.utilizationTarget}%)` },
        { label: 'Availability', value: percent(l.availability) }
      ]
    });
  }

  // ----- Work orders -----
  /** Creates one work order per asset in the Maintenance service (POST api/work-orders). */
  override submitAction(form: RaActionForm): void {
    const assets: RaActionAsset[] = this.action?.assets ?? [];
    this.action = null;
    this.analyticsApi
      .createWorkOrders(
        assets.map((a) => ({
          assetId: a.assetId,
          assetName: a.assetName,
          title: assets.length > 1 ? `${form.title} – ${a.assetName}` : form.title,
          workType: 'Corrective',
          priority: form.priority,
          assignedTechnician: form.owner,
          dueDate: form.due,
          notes: form.notes,
          site: a.location,
          sourceReference: 'Asset Performance'
        }))
      )
      .subscribe({
        next: (created) =>
          this.notify(created.length === 1 ? `Work order ${created[0].workOrderId} created for ${form.owner}, due ${form.due}.` : `${created.length} work orders created for ${form.owner}, due ${form.due}.`),
        error: (err) => this.notify(apiMessage(err, 'Could not create the work order.'))
      });
  }

  // ----- Insights -----
  private insight(id: string): ApInsightDto | undefined {
    return this.view.insights.find((i) => INSIGHT_IDS[i.key] === id);
  }

  insightAction(id: string): void {
    const [key, act] = [id.slice(0, id.lastIndexOf('-')), id.slice(id.lastIndexOf('-') + 1)];
    const insight = this.insight(key);
    if (!insight) return;
    if (key === 'utilization') {
      this.showLocations(insight);
      return;
    }
    const priority = key === 'critical' ? 'Critical' : 'High';
    if (act === 'wo') {
      const list = insight.assets.map(insightAsset);
      this.openAction('Work order', key === 'critical' ? `Condition assessment – ${list.length} critical assets` : `Preventive service – ${list.length} assets`, this.assetList(list), priority);
      if (this.action) this.action.assets = list.map((a) => ({ assetId: a.id, assetName: a.name, location: a.site.name }));
      return;
    }
    // View assets: GET api/asset-performance/assets with the insight's query (healthBelow=… / interventionOnly=true)
    const extra = Object.fromEntries(new URLSearchParams(insight.query));
    this.analyticsApi
      .get<{ items: ApAssetDto[] }>(GROUP, 'assets', { ...this.query, ...extra, sortBy: 'healthScore', sortDirection: 'asc', pageSize: 200 })
      .subscribe({
        next: (res) => (this.records = this.assetRecords(insight.title, insight.message, res.items.map(toRaAsset), `${insight.key.replace(/_/g, '-')}.csv`)),
        error: (err) => this.notify(apiMessage(err, 'Could not load the assets.'))
      });
  }

  private showLocations(insight: ApInsightDto): void {
    const target = this.utilizationTarget;
    const below = new Set(insight.locations);
    this.records = {
      title: 'Utilization by location',
      subtitle: `Target ${target}%. Lowest utilization first.`,
      columns: [
        { key: 'location', label: 'Location' }, { key: 'assets', label: 'Assets', num: true }, { key: 'utilization', label: 'Utilization %', num: true },
        { key: 'gap', label: 'Gap to target (pts)', num: true }, { key: 'availability', label: 'Availability %', num: true }
      ],
      rows: sortRows(this.view.locations, 'utilization', 1)
        .filter((l) => !below.size || below.has(l.name))
        .map((l) => ({ location: l.name, assets: l.assets, utilization: l.utilization, gap: l.utilization - target, availability: l.availability })),
      file: 'utilization-by-location.csv'
    };
  }

  private assetList(list: RaAsset[]): string {
    const names = list.slice(0, 4).map((a) => a.name).join(', ');
    return list.length > 4 ? `${names} and ${list.length - 4} more.` : `${names}.`;
  }

  private assetRecords(title: string, subtitle: string, list: RaAsset[], file: string): RaRecords {
    return {
      title: `${title} (${list.length})`,
      subtitle,
      columns: [
        { key: 'id', label: 'Asset ID' }, { key: 'name', label: 'Asset Name' }, { key: 'location', label: 'Location' }, { key: 'category', label: 'Category' },
        { key: 'health', label: 'Health', num: true }, { key: 'utilization', label: 'Utilization %', num: true }, { key: 'availability', label: 'Availability %', num: true }
      ],
      rows: list.map((a) => ({
        id: a.id, name: a.name, location: a.site.name, category: a.category, health: a.health, utilization: a.utilization, availability: a.availability
      })),
      file
    };
  }

  // ----- Export -----
  protected kpiTarget(id: string): string {
    const targets: Record<string, string> = { total: 'ap-distribution', health: 'ap-trend', utilization: 'ap-category', availability: 'ap-reliability', mtbf: 'ap-reliability' };
    return targets[id] ?? 'ap-trend';
  }

  /** Excel workbook of every section for the current filters (GET api/asset-performance/export). */
  exportPage(): void {
    this.analyticsApi.export(GROUP, this.query).subscribe({
      next: () => this.notify(`Exported ${this.view.total.toLocaleString('en-US')} assets.`),
      error: (err) => this.notify(apiMessage(err, 'Export failed.'))
    });
  }

  exportWidget(id: string): void {
    const v = this.view;
    const months = v.labels.map((l) => l.replace('\n', ' '));
    switch (id) {
      case 'trend':
        downloadCsv('asset-health-trend.csv', ['Month', ...v.healthTrend.map((s) => `${s.name} (%)`)], months.map((m, i) => [m, ...v.healthTrend.map((s) => s.values[i])]));
        break;
      case 'category':
        downloadCsv('utilization-by-category.csv', ['Category', ...v.categoryUtilization.map((s) => `${s.name} (%)`)],
          v.categoryLabels.map((l, i) => [l.split('\n')[0], ...v.categoryUtilization.map((s) => s.values[i])]));
        break;
      case 'distribution':
        downloadCsv('performance-score-distribution.csv', ['Band', 'Assets', 'Share'], v.distribution.map((r) => [r.label, r.value, r.display ?? '']));
        break;
      case 'reliability':
        downloadCsv('reliability-availability.csv', ['Month', ...v.reliability.map((s) => s.name)], months.map((m, i) => [m, ...v.reliability.map((s) => s.values[i])]));
        break;
      case 'downtime':
        downloadCsv('downtime-analysis.csv', ['Month', 'Planned (hrs)', 'Unplanned (hrs)'], months.map((m, i) => [m, v.downtime[0]?.values[i] ?? 0, v.downtime[1]?.values[i] ?? 0]));
        break;
      case 'assets':
        downloadCsv('top-bottom-assets.csv', ['Asset ID', 'Asset Name', 'Location', 'Health Score', 'Utilization %', 'Availability %', 'Status'],
          this.assetRows.map((r) => [r.id, r.name, r.location, r.health, r.utilization, r.availability, r.status]));
        break;
      case 'locations':
        downloadCsv('asset-performance-by-location.csv', ['Location', 'Assets', 'Avg Health Score', 'Utilization %', 'Availability %'],
          this.locationRows.map((l) => [l.name, l.assets, l.health, l.utilization, l.availability]));
        break;
    }
  }
}
