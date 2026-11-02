import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  LucideAngularModule, Activity, ArrowDown, ArrowUp, CalendarDays, ChartColumn, CircleAlert, CircleCheck, EllipsisVertical, Filter, Plus, Search, TriangleAlert, X
} from 'lucide-angular';
import { AssetPicker } from '../shared/asset-picker';
import { CatalogAsset } from '../shared/asset-catalog';
import { BarChart } from '../shared/charts/bar-chart';
import { ChartCard } from '../shared/charts/chart-card';
import { C } from '../shared/charts/chart-colors';
import { DonutChart } from '../shared/charts/donut-chart';
import { HBarList } from '../shared/charts/hbar-list';
import { DateRangePicker } from '../shared/date-range-picker';
import { DateRange, RANGE_PRESETS, change, day, isBreach, monthlySeries, pct, pdmIsLive, riskOf, today } from '../shared/derive';
import { MaintenanceApi, apiErrorMessage } from '../shared/maintenance-api';
import { ALERT_CONDITIONS, PDM_STATUSES, PredictiveAnomaly, PredictiveMaintenance, RISK_LEVELS, SENSOR_TYPES } from '../shared/maintenance.models';
import { downloadCsv, fmtDate, fmtDateTime, isoDay, pillClass } from '../shared/mnt-utils';
import { TableFooter } from '../shared/table-footer';
import { TableState, Toast } from '../shared/table-state';
import { raiseWorkOrder } from '../shared/work-order-factory';

type PdmTab = 'predictions' | 'anomalies' | 'models' | 'sensors' | 'actions';

interface PdmForm {
  assetId: string;
  assetName: string;
  assetType: string;
  location: string;
  modelName: string;
  sensorType: string;
  dataSourceDeviceId: string;
  alertCondition: string;
  thresholdValue: number | null;
  lastReading: number | null;
  healthScore: number | null;
  riskLevel: string;
  predictedIssue: string;
  predictedFailureDate: string;
  confidence: number | null;
  recommendedAction: string;
  predictionModelOutput: string;
  status: string;
}

/** Unit shown next to readings for each sensor type. */
export const SENSOR_UNITS: Record<string, string> = {
  Temperature: '°C', Vibration: 'mm/s', Pressure: 'bar', Current: 'A', Voltage: 'V', Humidity: '%RH', 'Oil Quality': 'ppm', 'Runtime Hours': 'h'
};

/** Risk band suggested from a health score: below 50 High, below 70 At Risk, else Healthy. */
export function riskFromHealth(score: number): string {
  return score < 50 ? 'High' : score < 70 ? 'At Risk' : 'Healthy';
}

/** Predicted failure within ±7 days of the actual one counts as a hit for model accuracy. */
const HIT_WINDOW_DAYS = 7;

/** Predictive Maintenance: asset health, anomalies, model performance, sensor rules and recommended actions. */
@Component({
  selector: 'app-mnt-predictive',
  imports: [FormsModule, RouterLink, LucideAngularModule, AssetPicker, DateRangePicker, ChartCard, DonutChart, BarChart, HBarList, TableFooter],
  templateUrl: './predictive.html',
  styleUrls: ['../shared/mnt-base.css', '../shared/mnt-dash.css'],
  host: { '(document:click)': 'table.menuId.set(null)' }
})
export class PredictivePage {
  private readonly api = inject(MaintenanceApi);
  private readonly router = inject(Router);
  private readonly query = toSignal(inject(ActivatedRoute).queryParamMap, { requireSync: true });
  readonly icons = { Activity, ArrowDown, ArrowUp, CalendarDays, ChartColumn, CircleAlert, CircleCheck, EllipsisVertical, Filter, Plus, Search, TriangleAlert, X };
  readonly fmtDate = fmtDate;
  readonly fmtDateTime = fmtDateTime;
  readonly pillClass = pillClass;
  readonly riskOf = riskOf;
  readonly isBreach = isBreach;
  readonly risks = RISK_LEVELS;
  readonly sensors = SENSOR_TYPES;
  readonly conditions = ALERT_CONDITIONS;
  readonly statuses = PDM_STATUSES;
  readonly store = this.api.predictive;
  readonly toast = new Toast();

  readonly range = signal<DateRange>(RANGE_PRESETS[0]!.range());
  readonly tab = signal<PdmTab>('predictions');
  readonly search = signal('');
  readonly risk = signal('');
  readonly assetType = signal('');
  readonly location = signal('');
  readonly model = signal('');
  readonly advanced = signal(false);
  readonly sensor = signal('');
  readonly status = signal('');
  readonly includeResolved = signal(false);
  readonly viewId = signal<string | null>(null);
  readonly busyId = signal<string | null>(null);
  readonly formOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly saving = signal(false);
  readonly formError = signal<string | null>(null);
  form!: PdmForm;

  readonly tabs: { key: PdmTab; label: string }[] = [
    { key: 'predictions', label: 'Asset Predictions' },
    { key: 'anomalies', label: 'Anomalies' },
    { key: 'models', label: 'Model Performance' },
    { key: 'sensors', label: 'Sensor Data' },
    { key: 'actions', label: 'Recommended Actions' }
  ];

  constructor() {
    this.store.load();
    this.api.workOrders.load();
    // ?code=<asset tag> opens that asset's live prediction.
    effect(() => {
      const code = this.query().get('code');
      const items = this.store.items();
      if (!code || !items.length) return;
      const p = items.find((x) => x.assetId.toLowerCase() === code.toLowerCase() && pdmIsLive(x)) ?? items.find((x) => x.assetId.toLowerCase() === code.toLowerCase());
      if (p) this.viewId.set(p.id);
      queueMicrotask(() => this.router.navigate([], { queryParams: {}, replaceUrl: true }));
    });
  }

  unit(sensor: string | null | undefined): string {
    return SENSOR_UNITS[sensor ?? ''] ?? '';
  }

  private readonly live = computed(() => this.store.items().filter(pdmIsLive));

  /** Worst live risk per asset. */
  private readonly assetRisk = computed(() => {
    const order: Record<string, number> = { High: 0, 'At Risk': 1, Healthy: 2 };
    const map = new Map<string, string>();
    for (const p of this.live()) {
      const r = riskOf(p);
      const cur = map.get(p.assetId);
      if (!cur || order[r]! < order[cur]!) map.set(p.assetId, r);
    }
    return map;
  });

  readonly kpis = computed(() => {
    const risks = [...this.assetRisk().values()];
    const total = risks.length;
    const t = today();
    const in30 = isoDay(new Date(Date.now() + 30 * 86_400_000));
    const past30 = isoDay(new Date(Date.now() - 30 * 86_400_000));
    const next = this.live().filter((p) => riskOf(p) !== 'Healthy' && day(p.predictedFailureDate) >= t && day(p.predictedFailureDate) <= in30).length;
    const prev = this.store.items().filter((p) => day(p.predictedFailureDate) >= past30 && day(p.predictedFailureDate) < t).length;
    const n = (r: string) => risks.filter((x) => x === r).length;
    return { total, healthy: n('Healthy'), atRisk: n('At Risk'), high: n('High'), next30: next, next30Trend: change(next, prev), pct: (v: number) => pct(v, total) };
  });

  readonly healthDonut = computed(() => [
    { label: 'Healthy', value: this.kpis().healthy, color: C.green },
    { label: 'At Risk', value: this.kpis().atRisk, color: C.yellow },
    { label: 'High Risk', value: this.kpis().high, color: C.red }
  ]);

  readonly failureTrend = computed(() => {
    const items = this.store.items();
    const predicted = monthlySeries(items, this.range(), (p) => p.predictedFailureDate, (p) => (p.predictedIssue ? 'Predicted Failures' : null), ['Predicted Failures']);
    const actual = monthlySeries(items, this.range(), (p) => p.actualFailureDate, () => 'Actual Failures', ['Actual Failures']);
    return {
      labels: predicted.labels,
      series: [{ name: 'Predicted Failures', color: C.lavender, values: predicted.values['Predicted Failures']! }],
      line: { name: 'Actual Failures', color: C.violet, values: actual.values['Actual Failures']! }
    };
  });

  readonly topAtRisk = computed(() => {
    const counts = new Map<string, number>();
    this.live().filter((p) => riskOf(p) !== 'Healthy').forEach((p) => {
      const label = plural(p.assetType || 'Other');
      counts.set(label, (counts.get(label) ?? 0) + 1);
    });
    return [...counts].map(([label, value]) => ({ label, value }));
  });

  readonly assetTypes = computed(() => [...new Set(this.store.items().map((p) => p.assetType).filter((t): t is string => !!t))].sort());
  readonly locations = computed(() => [...new Set(this.store.items().map((p) => p.location).filter((t): t is string => !!t))].sort());
  readonly models = computed(() => [...new Set(this.store.items().map((p) => p.modelName).filter((t): t is string => !!t))].sort());

  /** Rows after the shared filters (all tabs use these). */
  readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    return this.store.items()
      .filter((p) => {
        if (!this.includeResolved() && !pdmIsLive(p) && this.status() !== 'Resolved') return false;
        if (this.risk() && riskOf(p) !== this.risk()) return false;
        if (this.assetType() && p.assetType !== this.assetType()) return false;
        if (this.location() && p.location !== this.location()) return false;
        if (this.model() && p.modelName !== this.model()) return false;
        if (this.sensor() && p.sensorType !== this.sensor()) return false;
        if (this.status() && p.status !== this.status()) return false;
        if (!q) return true;
        return [p.assetName, p.assetId, p.location, p.assetType, p.predictedIssue, p.dataSourceDeviceId].some((v) => (v ?? '').toLowerCase().includes(q));
      })
      .sort((a, b) => (a.healthScore ?? 100) - (b.healthScore ?? 100));
  });

  readonly table = new TableState(this.filtered);

  readonly anomalies = computed(() =>
    this.filtered()
      .flatMap((p) => p.anomalies.map((a) => ({ p, a })))
      .sort((x, y) => y.a.detectedAt.localeCompare(x.a.detectedAt))
  );

  readonly modelStats = computed(() => {
    const groups = new Map<string, PredictiveMaintenance[]>();
    this.filtered().concat(this.includeResolved() ? [] : this.store.items().filter((p) => !pdmIsLive(p) && (!this.model() || p.modelName === this.model())))
      .forEach((p) => groups.set(p.modelName || 'Unassigned', [...(groups.get(p.modelName || 'Unassigned') ?? []), p]));
    return [...groups].map(([name, rows]) => {
      const withConf = rows.filter((r) => r.confidence !== null);
      const outcomes = rows.filter((r) => !pdmIsLive(r) && r.predictedFailureDate);
      const hits = outcomes.filter((r) => r.actualFailureDate && Math.abs(new Date(r.actualFailureDate).getTime() - new Date(r.predictedFailureDate!).getTime()) <= HIT_WINDOW_DAYS * 86_400_000);
      return {
        name,
        assets: new Set(rows.filter(pdmIsLive).map((r) => r.assetId)).size,
        predictions: rows.filter((r) => r.predictedIssue).length,
        avgConfidence: withConf.length ? Math.round(withConf.reduce((s, r) => s + (r.confidence ?? 0), 0) / withConf.length) : null,
        outcomes: outcomes.length,
        accuracy: outcomes.length ? pct(hits.length, outcomes.length) : null,
        alerts: rows.filter((r) => r.status === 'Alert').length
      };
    }).sort((a, b) => b.predictions - a.predictions);
  });

  readonly actions = computed(() => this.filtered().filter((p) => pdmIsLive(p) && riskOf(p) !== 'Healthy' && !!p.recommendedAction));

  readonly viewing = computed(() => this.store.byId(this.viewId()) ?? null);

  // ---------------------------------------------------------------- filters

  set<K extends 'search' | 'risk' | 'assetType' | 'location' | 'model' | 'sensor' | 'status'>(kind: K, value: string): void {
    this[kind].set(value);
    this.table.reset();
  }

  setTab(t: PdmTab): void {
    this.tab.set(t);
    this.table.reset();
  }

  kpiFilter(risk: string): void {
    this.risk.set(risk);
    this.setTab('predictions');
  }

  // ---------------------------------------------------------------- actions

  workOrderFor(p: PredictiveMaintenance) {
    return p.workOrderId ? this.api.workOrders.items().find((w) => w.workOrderId === p.workOrderId) ?? null : null;
  }

  createWorkOrder(p: PredictiveMaintenance): void {
    this.table.menuId.set(null);
    this.busyId.set(p.id);
    const due = day(p.predictedFailureDate);
    const lead = due ? isoDay(new Date(new Date(`${due}T00:00:00`).getTime() - 3 * 86_400_000)) : '';
    raiseWorkOrder(this.api, {
      assetId: p.assetId,
      assetName: p.assetName,
      workType: 'Predictive',
      category: 'Routine',
      title: `${p.assetName} - ${p.recommendedAction || 'Predictive intervention'}`,
      description: [p.predictedIssue && `Predicted issue: ${p.predictedIssue}`, p.predictionModelOutput].filter(Boolean).join('\n') || null,
      priority: riskOf(p) === 'High' ? 'High' : 'Medium',
      // Plan the job 3 days ahead of the predicted failure (never in the past).
      dueDate: due ? `${(lead > today() ? lead : today())}T00:00:00Z` : null,
      source: 'Predictive Alert',
      sourceReference: `${p.assetId} · ${p.modelName ?? p.sensorType}`
    }).subscribe({
      next: (wo) => {
        this.store.save({ ...p, workOrderId: wo.workOrderId, status: 'Work Order Raised' }).subscribe({
          next: () => { this.busyId.set(null); this.toast.show(`Work order ${wo.workOrderId} created for ${p.assetName}.`); },
          error: (e) => { this.busyId.set(null); this.toast.show(`Work order ${wo.workOrderId} created, but the prediction was not updated: ${apiErrorMessage(e)}`); }
        });
      },
      error: (e) => { this.busyId.set(null); this.toast.show(apiErrorMessage(e)); }
    });
  }

  setStatus(p: PredictiveMaintenance, status: string): void {
    this.table.menuId.set(null);
    this.store.save({ ...p, status }).subscribe({
      next: () => this.toast.show(`${p.assetName} set to ${status}.`),
      error: (e) => this.toast.show(apiErrorMessage(e))
    });
  }

  /** Records that the predicted failure happened today (feeds the actual-failures line and model accuracy). */
  recordFailure(p: PredictiveMaintenance): void {
    this.table.menuId.set(null);
    this.store.save({ ...p, actualFailureDate: `${today()}T00:00:00Z`, status: 'Resolved' }).subscribe({
      next: () => this.toast.show(`Failure recorded for ${p.assetName}.`),
      error: (e) => this.toast.show(apiErrorMessage(e))
    });
  }

  remove(p: PredictiveMaintenance): void {
    this.table.confirmDeleteId.set(null);
    if (this.viewId() === p.id) this.viewId.set(null);
    this.store.remove(p.id).subscribe({
      next: () => this.toast.show(`Prediction for ${p.assetName} deleted.`),
      error: (e) => this.toast.show(apiErrorMessage(e))
    });
  }

  // ---------------------------------------------------------------- form

  openNew(): void {
    this.editingId.set(null);
    this.form = {
      assetId: '', assetName: '', assetType: '', location: '', modelName: this.models()[0] ?? '', sensorType: 'Vibration', dataSourceDeviceId: '',
      alertCondition: 'Above Threshold', thresholdValue: null, lastReading: null, healthScore: null, riskLevel: 'Healthy', predictedIssue: '',
      predictedFailureDate: '', confidence: null, recommendedAction: '', predictionModelOutput: '', status: 'Monitoring'
    };
    this.formError.set(null);
    this.formOpen.set(true);
  }

  edit(p: PredictiveMaintenance): void {
    this.table.menuId.set(null);
    this.editingId.set(p.id);
    this.form = {
      assetId: p.assetId, assetName: p.assetName, assetType: p.assetType ?? '', location: p.location ?? '', modelName: p.modelName ?? '',
      sensorType: p.sensorType, dataSourceDeviceId: p.dataSourceDeviceId ?? '', alertCondition: p.alertCondition ?? 'Above Threshold',
      thresholdValue: p.thresholdValue, lastReading: p.lastReading, healthScore: p.healthScore, riskLevel: riskOf(p), predictedIssue: p.predictedIssue ?? '',
      predictedFailureDate: day(p.predictedFailureDate), confidence: p.confidence, recommendedAction: p.recommendedAction ?? '',
      predictionModelOutput: p.predictionModelOutput ?? '', status: p.status ?? 'Monitoring'
    };
    this.formError.set(null);
    this.formOpen.set(true);
  }

  onAsset(a: CatalogAsset): void {
    this.form.assetType = a.type;
    this.form.location = a.site;
  }

  onHealth(v: number | null): void {
    this.form.healthScore = v;
    if (v !== null && (v as unknown) !== '' && !isNaN(Number(v))) this.form.riskLevel = riskFromHealth(Number(v));
  }

  save(): void {
    const f = this.form;
    const num = (v: number | null) => (v === null || (v as unknown) === '' || isNaN(Number(v)) ? null : Number(v));
    const health = num(f.healthScore);
    const confidence = num(f.confidence);
    const problem = !f.assetId.trim() ? 'Asset tag is required.'
      : !f.sensorType ? 'Sensor type is required.'
      : num(f.thresholdValue) === null ? 'Threshold value is required.'
      : health !== null && (health < 0 || health > 100) ? 'Health score must be between 0 and 100.'
      : confidence !== null && (confidence < 0 || confidence > 100) ? 'Confidence must be between 0 and 100.'
      : null;
    if (problem) return this.formError.set(problem);
    const existing = this.store.byId(this.editingId());
    const reading = num(f.lastReading);
    const body: Partial<PredictiveMaintenance> = {
      ...(existing ?? {}),
      assetId: f.assetId.trim(), assetName: f.assetName.trim(), assetType: f.assetType.trim() || null, location: f.location.trim() || null,
      modelName: f.modelName.trim() || null, sensorType: f.sensorType, dataSourceDeviceId: f.dataSourceDeviceId.trim() || null,
      alertCondition: f.alertCondition, thresholdValue: num(f.thresholdValue)!, lastReading: reading,
      lastReadingAt: reading !== existing?.lastReading ? new Date().toISOString() : existing?.lastReadingAt ?? null,
      healthScore: health, riskLevel: f.riskLevel, predictedIssue: f.predictedIssue.trim() || null,
      predictedFailureDate: f.predictedFailureDate ? `${f.predictedFailureDate}T00:00:00Z` : null, confidence,
      recommendedAction: f.recommendedAction.trim() || null, predictionModelOutput: f.predictionModelOutput.trim() || null, status: f.status
    };
    // A reading that breaches the rule is logged as an anomaly.
    if (reading !== null && reading !== existing?.lastReading && isBreach(body as PredictiveMaintenance)) {
      const anomaly: PredictiveAnomaly = {
        detectedAt: new Date().toISOString(), metric: f.sensorType, value: reading,
        expected: `${f.alertCondition === 'Below Threshold' ? '>' : '<'} ${f.thresholdValue}`, severity: f.riskLevel === 'High' ? 'High' : 'Medium', note: 'Reading entered manually.'
      };
      body.anomalies = [anomaly, ...(existing?.anomalies ?? [])];
    }
    this.saving.set(true);
    this.formError.set(null);
    this.store.save(body).subscribe({
      next: (saved) => { this.saving.set(false); this.formOpen.set(false); this.toast.show(`Prediction for ${saved.assetName} saved.`); },
      error: (e) => { this.saving.set(false); this.formError.set(apiErrorMessage(e)); }
    });
  }

  // ---------------------------------------------------------------- export

  export(): void {
    const rows = this.filtered();
    if (!rows.length) return this.toast.show('Nothing to export.');
    downloadCsv(`asset-predictions-${today()}.csv`, rows.map((p) => ({
      'Asset Name': p.assetName, 'Asset Tag': p.assetId, 'Asset Type': p.assetType ?? '', Location: p.location ?? '', 'Health Score': p.healthScore ?? '',
      'Risk Level': riskOf(p), 'Predicted Issue': p.predictedIssue ?? '', 'Predicted Date': day(p.predictedFailureDate), Confidence: p.confidence ?? '',
      'Recommended Action': p.recommendedAction ?? '', Model: p.modelName ?? '', Status: p.status ?? ''
    })));
  }

  exportHealth(): void {
    downloadCsv('asset-health.csv', this.healthDonut().map((s) => ({ Health: s.label, Assets: s.value })));
  }

  exportTrend(): void {
    const t = this.failureTrend();
    downloadCsv('predicted-failures-trend.csv', t.labels.map((label, i) => ({ Month: label, Predicted: t.series[0]!.values[i], Actual: t.line.values[i] })));
  }

  exportTop(): void {
    downloadCsv('top-assets-at-risk.csv', this.topAtRisk().map((r) => ({ 'Asset Type': r.label, 'At Risk': r.value })));
  }

  healthColor(score: number | null): string {
    if (score === null) return '#cbd5e1';
    return score < 50 ? C.red : score < 70 ? C.yellow : '#4ade80';
  }
}

function plural(type: string): string {
  if (/s$/i.test(type)) return type;
  if (/(ch|sh|x)$/i.test(type)) return `${type}es`;
  return /^(UPS|AHU|HVAC)$/i.test(type) ? `${type} Systems` : `${type}s`;
}
