import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subscription, catchError, map, of, switchMap } from 'rxjs';
import {
  ArrowUp, Bookmark, ChartColumn, CircleCheck, ClipboardList, FileText, GitCompareArrows, LucideAngularModule, LucideIconData,
  RefreshCw, Repeat, ShieldCheck, TriangleAlert, Upload, Wrench
} from 'lucide-angular';
import { AnalyticsPage, exportTable, isExportAction, percent, tablesRequest } from '../shared/analytics-common';
import {
  AnalyticsApiService, AnalyticsGroup, AnalyticsQuery, KpiDto, apiMessage, filterValue, optionLabels, periodQuery, previousQuery, viewDates
} from '../shared/analytics-api.service';
import { CartesianChart, ChartSeries, ChartSlice, DonutChart, GaugeChart, RA_COLORS } from '../shared/charts';
import {
  ActionPlan, DetailField, RaActionPlanDialog, RaDetailDialog, RaKpiCard, RaListDialog, RaSaveViewDialog, RaWidget
} from './kit';
import {
  ALL_INSPECTION_TYPES, ALL_INSPECTORS, IaFiltersDto, IaInsightDto, IaOverviewDto, InspectionFinding, InspectionView, InspectorRow, SEVERITY_RANK, Severity,
  emptyInspectionView, mapInspection
} from './inspection-analytics.data';
import { InspectionFindingsTable } from './findings-table/findings-table';
import { InspectorTable } from './inspector-table/inspector-table';
import { ExportMenu } from 'shared-ui';

interface KpiTile {
  key: 'total' | 'rate' | 'open' | 'critical' | 'due';
  label: string;
  value: string;
  delta: string;
  up: boolean;
  good: boolean;
  icon: LucideIconData;
  tone: string;
}

type InsightAction = 'critical' | 'due' | 'recurring' | 'recurring-detail' | 'safety' | 'plan';

interface Insight {
  key: string;
  tone: string;
  icon: LucideIconData;
  title: string;
  text: string;
  primary: { label: string; action: InsightAction };
  secondary: { label: string; action: InsightAction };
}

interface ListSpec {
  title: string;
  subtitle: string;
  header: string[];
  rows: (string | number)[][];
  fileName: string;
}

interface DetailSpec {
  title: string;
  subtitle: string;
  fields: DetailField[];
  finding: InspectionFinding | null;
}

const GROUP: AnalyticsGroup = 'inspection-analytics';
const SEVERITIES: Severity[] = ['Critical', 'High', 'Medium', 'Low'];
const SEVERITY_COLORS: Record<Severity, string> = { Critical: RA_COLORS.red, High: RA_COLORS.pink, Medium: RA_COLORS.orange, Low: RA_COLORS.blue };
const FINDING_HEADER = ['Finding ID', 'Asset ID', 'Asset', 'Location', 'Category', 'Severity', 'Due Date', 'Owner', 'Status'];
const INSIGHT_TONES: Record<string, string> = { critical: 'red', warning: 'orange', info: 'blue', success: 'green' };

/** Insight cards by API key: icon and the two buttons. */
const INSIGHT_CARDS: Record<string, { icon: LucideIconData; primary: Insight['primary']; secondary: Insight['secondary'] }> = {
  critical_findings: { icon: TriangleAlert, primary: { label: 'View findings', action: 'critical' }, secondary: { label: 'Create action plan', action: 'plan' } },
  corrective_actions_due: { icon: ChartColumn, primary: { label: 'View findings', action: 'due' }, secondary: { label: 'Create action plan', action: 'plan' } },
  recurring_findings: { icon: Repeat, primary: { label: 'View assets', action: 'recurring' }, secondary: { label: 'Open asset details', action: 'recurring-detail' } },
  safety_compliance: { icon: ShieldCheck, primary: { label: 'View details', action: 'safety' }, secondary: { label: 'Create action plan', action: 'plan' } }
};

/** Reports & Analytics > Inspection Analytics */
@Component({
  selector: 'app-ra-inspection-analytics',
  imports: [
    FormsModule, RouterLink, LucideAngularModule, CartesianChart, DonutChart, GaugeChart, RaWidget, RaKpiCard, RaListDialog,
    RaDetailDialog, RaActionPlanDialog, RaSaveViewDialog, InspectionFindingsTable, InspectorTable
  , ExportMenu],
  templateUrl: './inspection-analytics.html',
  styleUrls: ['../shared/analytics-page.css', './inspection-analytics.css']
})
export class InspectionAnalyticsPage extends AnalyticsPage {
  private readonly api = inject(AnalyticsApiService);
  readonly icons = { ArrowUp, Bookmark, GitCompareArrows, RefreshCw, Upload };
  categories = ['All categories'];
  types = [ALL_INSPECTION_TYPES];
  inspectorOptions = [ALL_INSPECTORS];
  readonly percentFormat = (v: number) => `${Math.round(v)}%`;

  category = '';
  type = '';
  inspector = '';
  compare = false;

  view: InspectionView = emptyInspectionView();
  kpis: KpiTile[] = [];
  insights: Insight[] = [];
  trendSeries: ChartSeries[] = [];
  severitySeries: ChartSeries[] = [];
  statusSlices: ChartSlice[] = [];
  categorySeries: ChartSeries[] = [];
  complianceSeries: ChartSeries[] = [];
  repeatSeries: ChartSeries[] = [];

  list: ListSpec | null = null;
  detail: DetailSpec | null = null;
  plan: { context: string; title: string; owner: string } | null = null;
  saveOpen = false;
  private loadSub: Subscription | null = null;

  constructor() {
    super();
    this.api.filters<IaFiltersDto>(GROUP).subscribe({
      next: (f) => {
        this.organizations = optionLabels('All organizations', f.organizations);
        this.locations = optionLabels('All locations', f.locations);
        this.categories = optionLabels('All categories', f.assetCategories);
        this.types = optionLabels(ALL_INSPECTION_TYPES, f.inspectionTypes);
        this.inspectorOptions = optionLabels(ALL_INSPECTORS, f.inspectors);
      },
      error: (err) => console.warn('[inspection-analytics] filters failed', err)
    });
    this.buildWidgets();
    this.apply();
  }

  get complianceTarget(): number {
    return this.view.complianceTarget;
  }

  get findingTypes(): string[] {
    return this.view.findingTypes;
  }

  /** Filter bar as query parameters of the Inspection Analytics endpoints. */
  private get query(): AnalyticsQuery {
    return {
      ...periodQuery(this.period),
      organization: filterValue(this.organization),
      location: filterValue(this.location),
      category: filterValue(this.category),
      inspectionType: filterValue(this.type),
      inspector: filterValue(this.inspector)
    };
  }

  /** Reloads every widget from GET api/inspection-analytics/overview (plus the previous period when comparing). */
  apply(): void {
    this.loadSub?.unsubscribe();
    this.refreshing = true;
    const query = this.query;
    const compare = this.compare;
    this.loadSub = this.api
      .overview<IaOverviewDto>(GROUP, { ...query, findingsSize: 200, inspectorLimit: 100 })
      .pipe(
        switchMap((o) =>
          compare
            ? this.api.overview<IaOverviewDto>(GROUP, { ...query, datePreset: undefined, ...previousQuery(o.period), findingsSize: 1, inspectorLimit: 1 }).pipe(
                map((p): [IaOverviewDto, IaOverviewDto | null] => [o, p]),
                catchError(() => of<[IaOverviewDto, IaOverviewDto | null]>([o, null]))
              )
            : of<[IaOverviewDto, IaOverviewDto | null]>([o, null])
        )
      )
      .subscribe({
        next: ([o, previous]) => this.show(mapInspection(o, previous)),
        error: (err) => {
          this.show(emptyInspectionView());
          this.notify(apiMessage(err, 'Could not load inspection analytics.'));
        }
      });
  }

  override refresh(): void {
    if (!this.refreshing) this.apply();
  }

  private show(v: InspectionView): void {
    this.view = v;
    this.refreshing = false;
    this.updatedMinutes = 0;
    this.buildWidgets();
  }

  private buildWidgets(): void {
    const v = this.view;
    const k = v.kpis;
    const tile = (key: KpiTile['key'], label: string, value: string, dto: KpiDto | undefined, lowerIsBetter: boolean, icon: LucideIconData, tone: string): KpiTile => {
      const change = dto?.change ?? null;
      const unit = dto?.changeUnit === 'points' ? ' pts' : '%';
      return {
        key, label, value, icon, tone,
        delta: change === null ? '' : `${change > 0 ? '+' : ''}${Number(change.toFixed(1))}${unit}`,
        up: (change ?? 0) >= 0,
        good: change === null || change === 0 ? true : (change > 0) !== lowerIsBetter
      };
    };
    this.kpis = [
      tile('total', 'Total Inspections', v.totalInspections.toLocaleString('en-US'), k?.totalInspections, false, ClipboardList, 'purple'),
      tile('rate', 'Completion Rate', v.completionRate === null ? '—' : percent(v.completionRate), k?.completionRate, false, CircleCheck, 'green'),
      tile('open', 'Open Findings', String(v.openFindings), k?.openFindings, true, FileText, 'orange'),
      tile('critical', 'Critical Findings', String(v.critical), k?.criticalFindings, true, TriangleAlert, 'red'),
      tile('due', 'Corrective Actions Due', String(v.actionsDue), k?.correctiveActionsDue, true, Wrench, 'blue')
    ];
    this.buildTrend();
    this.severitySeries = SEVERITIES.map((s) => ({ name: s, values: v.severity[s], color: SEVERITY_COLORS[s] }));
    this.statusSlices = [
      { name: 'Completed', value: v.status.completed, color: RA_COLORS.green },
      { name: 'In Progress', value: v.status.inProgress, color: RA_COLORS.blue },
      { name: 'Scheduled', value: v.status.scheduled, color: RA_COLORS.violet },
      { name: 'Overdue', value: v.status.overdue, color: RA_COLORS.red }
    ];
    this.categorySeries = [{ name: 'Findings', values: v.byFindingType, color: RA_COLORS.purple }];
    this.complianceSeries = [
      { name: 'Compliance', values: v.rateSeries, color: RA_COLORS.green, area: true },
      { name: 'Target', values: v.rateSeries.map(() => v.complianceTarget), color: RA_COLORS.grey, dashed: true }
    ];
    this.repeatSeries = [
      { name: 'Repeated Defects', values: v.repeated, color: RA_COLORS.pink },
      { name: 'Resolved Defects', values: v.resolved, color: RA_COLORS.blue }
    ];
    this.insights = v.insights
      .filter((i) => INSIGHT_CARDS[i.key])
      .map((i) => ({ key: i.key, tone: INSIGHT_TONES[i.severity] ?? 'blue', title: i.title, text: i.message, ...INSIGHT_CARDS[i.key] }));
  }

  resetFilters(): void {
    this.period = '';
    this.organization = '';
    this.location = '';
    this.category = '';
    this.type = '';
    this.inspector = '';
    this.apply();
  }

  toggleCompare(): void {
    this.compare = !this.compare;
    this.apply();
    this.notify(this.compare ? 'Comparing completed inspections with the previous period.' : 'Period comparison turned off.');
  }

  private buildTrend(): void {
    const v = this.view;
    this.trendSeries = [
      { name: 'Planned', values: v.planned, color: RA_COLORS.blue },
      { name: 'Completed', values: v.completed, color: RA_COLORS.green },
      { name: 'Overdue', values: v.overdue, color: RA_COLORS.red, dashed: true }
    ];
    if (this.compare && v.completedPrevious.length) this.trendSeries.push({ name: 'Completed (previous period)', values: v.completedPrevious, color: RA_COLORS.grey, dashed: true });
  }

  get statusTotal(): string {
    const s = this.view.status;
    return (s.completed + s.inProgress + s.scheduled + s.overdue).toLocaleString('en-US');
  }

  // ===== Header actions =====

  /** Page Export (CSV / PDF): open inspection findings + inspector performance in one table (Section column). */
  exportRequest = () =>
    tablesRequest({ title: 'Inspection Analytics', fileBase: 'inspection-analytics', filters: this.exportFilters() }, () => {
      this.exportFindings();
      this.exportInspectors();
    });

  /** Save View: POST api/inspection-analytics/views with the current filters. */
  saveNamedView(name: string): void {
    this.saveOpen = false;
    const filters = {
      organization: filterValue(this.organization) ?? null,
      location: filterValue(this.location) ?? null,
      category: filterValue(this.category) ?? null,
      inspectionType: filterValue(this.type) ?? null,
      inspector: filterValue(this.inspector) ?? null,
      ...viewDates(this.period)
    };
    this.api.createView(GROUP, name, filters).subscribe({
      next: () => this.saveView(name),
      error: (err) => this.notify(apiMessage(err, 'Could not save the view.'))
    });
  }

  // ===== KPI cards =====

  kpiAction(kpi: KpiTile, action: string): void {
    if (isExportAction(action)) {
      exportTable(`inspection-${kpi.key}.csv`, ['KPI', 'Value', 'Change vs previous period', 'Period', 'Location'], [[kpi.label, kpi.value, kpi.delta || '—', this.periodLabel, this.locationLabel]]);
      return;
    }
    const v = this.view;
    const count = (n: number) => n.toLocaleString('en-US');
    let fields: DetailField[];
    switch (kpi.key) {
      case 'total':
        fields = [
          { label: 'Completed', value: count(v.status.completed) },
          { label: 'In progress', value: count(v.status.inProgress) },
          { label: 'Scheduled', value: count(v.status.scheduled) },
          { label: 'Overdue', value: count(v.status.overdue) }
        ];
        break;
      case 'rate':
        fields = [
          { label: 'Definition', value: 'Completed ÷ (completed + overdue + in progress)' },
          { label: 'Target', value: `${v.complianceTarget}%` }
        ];
        break;
      case 'open':
        fields = SEVERITIES.map((s) => ({ label: s, value: String(v.findings.filter((r) => r.severity === s).length) }));
        break;
      case 'critical':
        fields = [{ label: 'Earliest due date', value: this.earliestDue(v.findings.filter((r) => r.severity === 'Critical')) }];
        break;
      default:
        fields = [
          { label: 'Window', value: 'Due today up to 7 days ahead' },
          { label: 'Already overdue', value: String(v.findings.filter((r) => r.dueInDays < 0).length) }
        ];
    }
    fields.push({ label: 'Change vs previous period', value: kpi.delta || '—' }, { label: 'Period', value: this.periodLabel }, { label: 'Location', value: this.locationLabel });
    this.detail = { title: kpi.label, subtitle: kpi.value, fields, finding: null };
  }

  private earliestDue(rows: InspectionFinding[]): string {
    return rows.slice().sort((a, b) => a.dueInDays - b.dueInDays)[0]?.due ?? '—';
  }

  // ===== Widget exports =====

  private monthRows(...cols: number[][]): (string | number)[][] {
    return this.view.labels.map((l, i) => [l.replace('\n', ' '), ...cols.map((c) => c[i] ?? '')]);
  }

  exportTrend(): void {
    const v = this.view;
    exportTable('inspection-completion-trend.csv', ['Month', 'Planned', 'Completed', 'Overdue', 'Completed (previous period)'], this.monthRows(v.planned, v.completed, v.overdue, v.completedPrevious));
  }

  exportSeverity(): void {
    const s = this.view.severity;
    exportTable('findings-by-severity.csv', ['Month', ...SEVERITIES], this.monthRows(s.Critical, s.High, s.Medium, s.Low));
  }

  exportStatus(): void {
    exportTable('inspection-status.csv', ['Status', 'Inspections'], this.statusSlices.map((s) => [s.name, s.value]));
  }

  exportCategories(): void {
    exportTable('findings-by-category.csv', ['Category', 'Findings'], this.view.findingTypes.map((t, i) => [t, this.view.byFindingType[i]]));
  }

  exportCompliance(): void {
    const v = this.view;
    exportTable('inspection-compliance.csv', ['Month', 'Compliance %', 'Target %'], this.monthRows(v.rateSeries, v.rateSeries.map(() => v.complianceTarget)));
  }

  exportRepeat(): void {
    exportTable('repeat-findings.csv', ['Month', 'Repeated', 'Resolved'], this.monthRows(this.view.repeated, this.view.resolved));
  }

  exportFindings(): void {
    exportTable('open-inspection-findings.csv', FINDING_HEADER, this.findingRows(this.view.findings));
  }

  exportInspectors(): void {
    exportTable('inspector-performance.csv', ['Inspector', 'Inspections Completed', 'Avg Duration (hrs)', 'Finding Rate %', 'Closure Rate %'],
      this.view.inspectors.map((r) => [r.name, r.inspections, r.avgHours, r.findingRate, r.closureRate]));
  }

  // ===== Tables =====

  openFinding(row: InspectionFinding): void {
    this.detail = {
      title: row.id,
      subtitle: `${row.assetName} · ${row.site}`,
      finding: row,
      fields: [
        { label: 'Asset ID', value: row.assetId },
        { label: 'Asset', value: row.assetName },
        { label: 'Location', value: row.site },
        { label: 'Finding category', value: row.findingType },
        { label: 'Description', value: row.description || '—' },
        { label: 'Severity', value: row.severity },
        { label: 'Status', value: row.status },
        { label: 'Due date', value: row.dueInDays < 0 ? `${row.due} (overdue)` : row.due },
        { label: 'Owner', value: row.owner }
      ]
    };
  }

  planFor(row: InspectionFinding): void {
    this.detail = null;
    this.plan = {
      context: `${row.id} · ${row.severity} ${row.findingType.toLowerCase()} finding on ${row.assetName} (${row.site}).`,
      title: `Corrective action for ${row.id}`,
      owner: row.owner === '—' ? '' : row.owner
    };
  }

  /** Local only: the Report Analytics API has no endpoint to change a finding's status. */
  markInProgress(row: InspectionFinding): void {
    row.status = 'In Progress';
    this.view = { ...this.view, findings: [...this.view.findings] };
    this.notify(`${row.id} marked as in progress.`);
  }

  viewAllFindings(): void {
    this.showFindings('Open inspection findings', this.view.findings);
  }

  openInspector(row: InspectorRow): void {
    this.detail = {
      title: row.name,
      subtitle: 'Inspector performance',
      finding: null,
      fields: [
        { label: 'Inspections completed', value: row.inspections.toLocaleString('en-US') },
        { label: 'Average duration', value: `${row.avgHours} hrs` },
        { label: 'Finding rate', value: `${row.findingRate}%` },
        { label: 'Closure rate', value: `${row.closureRate}%` }
      ]
    };
  }

  detailAction(): void {
    if (this.detail?.finding) this.planFor(this.detail.finding);
  }

  // ===== Insights =====

  private insightDto(insight: Insight): IaInsightDto | undefined {
    return this.view.insights.find((i) => i.key === insight.key);
  }

  insightAction(insight: Insight, action: InsightAction): void {
    const v = this.view;
    const dto = this.insightDto(insight);
    switch (action) {
      case 'critical':
        this.showFindings('Critical findings', v.findings.filter((r) => r.severity === 'Critical'));
        break;
      case 'due': {
        const ids = new Set((dto?.findings ?? []).map((f) => f.findingId ?? f.id));
        this.showFindings('Corrective actions due within 7 days', v.findings.filter((r) => ids.has(r.id)));
        break;
      }
      case 'recurring':
        this.list = {
          title: 'Assets with recurring findings',
          subtitle: dto?.message ?? '',
          header: ['Asset ID', 'Asset', 'Location', 'Repeat findings'],
          rows: (dto?.assets ?? []).map((a) => [a.assetId ?? a.id, a.assetName ?? '—', a.location ?? '—', a.reasons.join('; ')]),
          fileName: 'recurring-findings-assets.csv'
        };
        break;
      case 'recurring-detail':
        this.openRecurringAsset(dto);
        break;
      case 'safety':
        this.list = {
          title: 'Safety inspection compliance by location',
          subtitle: dto?.message ?? '',
          header: ['Location', 'Status'],
          rows: (dto?.locations ?? []).map((l) => [l, 'Below target']),
          fileName: 'safety-compliance-by-location.csv'
        };
        break;
      case 'plan':
        this.plan = { context: insight.title + '.', title: insight.title, owner: '' };
        break;
    }
  }

  private openRecurringAsset(dto: IaInsightDto | undefined): void {
    const top = dto?.assets[0];
    if (!top) {
      this.notify('No assets with recurring findings for the current filters.');
      return;
    }
    const assetId = top.assetId ?? top.id;
    const rows = this.view.findings.filter((r) => r.assetId === assetId);
    const worst = rows.slice().sort((a, b) => SEVERITY_RANK[b.severity] - SEVERITY_RANK[a.severity])[0];
    this.detail = {
      title: top.assetName ?? assetId,
      subtitle: `${assetId} · ${top.location ?? '—'}`,
      finding: null,
      fields: [
        { label: 'Repeat findings', value: top.reasons.join('; ') || '—' },
        { label: 'Open findings', value: String(rows.length) },
        { label: 'Highest open severity', value: worst?.severity ?? '—' },
        { label: 'Finding IDs', value: rows.map((r) => r.id).join(', ') || '—' }
      ]
    };
  }

  private showFindings(title: string, rows: InspectionFinding[]): void {
    this.list = {
      title,
      subtitle: `${rows.length} finding${rows.length === 1 ? '' : 's'} · ${this.locationLabel} · ${this.periodLabel}`,
      header: FINDING_HEADER,
      rows: this.findingRows(rows),
      fileName: title.toLowerCase().replace(/[^a-z0-9]+/g, '-') + '.csv'
    };
  }

  private findingRows(rows: InspectionFinding[]): (string | number)[][] {
    return rows.map((r) => [r.id, r.assetId, r.assetName, r.site, r.findingType, r.severity, r.due, r.owner, r.status]);
  }

  planSaved(plan: ActionPlan): void {
    this.plan = null;
    this.notify(`DEMO MODE: action plan “${plan.title}” created for ${plan.owner}, due ${plan.due}.`);
  }
}
