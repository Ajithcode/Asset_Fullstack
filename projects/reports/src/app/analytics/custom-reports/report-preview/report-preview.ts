import { Component, DoCheck, Input, OnDestroy, inject } from '@angular/core';
import { Subject, catchError, debounceTime, map, of, switchMap } from 'rxjs';
import { ChartColumn, ChartLine, ChartPie, LucideAngularModule, Table } from 'lucide-angular';
import { CartesianChart, ChartSeries, ChartSlice, DonutChart, RA_COLORS, RA_SERIES_COLORS, ValueFormat, compactNumber } from '../../shared/charts';
import { formatCell, outputColumns } from '../custom-reports.engine';
import { CustomReportsService, apiError, toApiDefinition } from '../custom-reports.service';
import { FieldKind, PreviewColumn, PreviewResult, ReportDefinition, ReportRow, VizType } from '../custom-reports.model';

/**
 * Renders a report definition as a table or chart from the Report Analytics preview API.
 * Reloads (debounced) only when the API definition changes, so chart inputs stay stable.
 */
@Component({
  selector: 'cr-report-preview',
  imports: [LucideAngularModule, CartesianChart, DonutChart],
  template: `
    <div class="cr-panel__head">
      <h3>{{ heading }}</h3>
      <div class="seg cr-seg-sm" role="group" aria-label="Preview type">
        <button type="button" [class.active]="mode === 'Table'" (click)="setMode('Table')"><lucide-icon [img]="Table" [size]="13" /> Table view</button>
        <button type="button" [class.active]="mode !== 'Table'" (click)="setMode(chartMode)"><lucide-icon [img]="chartIcon" [size]="13" /> {{ chartMode }} chart</button>
      </div>
    </div>

    @if (loading && !result.columns.length) {
      <p class="empty">Loading preview…</p>
    } @else if (error) {
      <p class="empty">{{ error }}</p>
    } @else if (!result.columns.length) {
      <p class="empty">Select fields to see a preview.</p>
    } @else {
      <p class="cr-preview__title">{{ mode === 'Table' ? reportName || 'Report preview' : result.title }}</p>
      @if (mode === 'Table') {
        <div class="table-wrap" [style.max-height.px]="compact ? 230 : 460">
          <table class="table">
            <thead>
              <tr>
                @for (c of result.columns; track c.key) { <th [class.num]="isNum(c)">{{ c.label }}</th> }
              </tr>
            </thead>
            <tbody>
              @for (r of result.rows; track $index) {
                <tr>
                  @for (c of result.columns; track c.key) { <td [class.num]="isNum(c)">{{ cell(c, r) }}</td> }
                </tr>
              } @empty {
                <tr><td [attr.colspan]="result.columns.length" class="empty">No records match the filters.</td></tr>
              }
            </tbody>
          </table>
        </div>
      } @else if (!canChart) {
        <p class="empty">A chart needs one text column and one numeric column. Add fields or group the report.</p>
      } @else if (mode === 'Donut') {
        <ra-donut-chart [slices]="slices" [size]="compact ? 150 : 200" [thickness]="compact ? 24 : 32" [format]="valueFormat" [centerLabel]="valueLabel" />
      } @else {
        <ra-cartesian-chart [labels]="labels" [series]="series" [kind]="mode === 'Line' ? 'line' : 'bar'" [height]="compact ? 200 : 300"
          [showValues]="mode === 'Bar'" [legend]="false" [format]="valueFormat" [ariaLabel]="result.title" />
      }
      <p class="muted cr-preview__foot">{{ loading ? 'Updating… · ' : '' }}Showing {{ result.rows.length }} of {{ result.matched }} {{ grouped ? 'groups' : 'records' }}</p>
    }
  `,
  styleUrls: ['../../shared/analytics-page.css', '../custom-reports.css']
})
export class ReportPreview implements DoCheck, OnDestroy {
  private readonly service = inject(CustomReportsService);

  @Input({ required: true }) definition!: ReportDefinition;
  @Input() reportName = '';
  @Input() heading = 'Preview';
  /** Smaller table/chart for the side panel */
  @Input() compact = false;
  /** Id of the saved report; with an unchanged definition the saved report is previewed (GET /{id}/preview) */
  @Input() savedId = '';
  /** API definition signature of the saved report (see previewSignature) */
  @Input() savedSignature = '';

  readonly Table = Table;

  result: PreviewResult = { columns: [], rows: [], matched: 0, labelKey: '', valueKey: '', title: '' };
  labels: string[] = [];
  series: ChartSeries[] = [];
  slices: ChartSlice[] = [];
  valueFormat: ValueFormat = compactNumber;
  valueLabel = '';
  grouped = false;
  loading = false;
  error = '';
  /** Local override of the visualization (Table view / chart toggle) */
  private override: VizType | null = null;
  private signature = '';
  private viz: VizType | null = null;
  /** null cancels a pending request (the definition has nothing to preview) */
  private readonly requests = new Subject<{ def: ReportDefinition; savedId: string | null } | null>();
  private readonly sub = this.requests
    .pipe(
      debounceTime(400),
      switchMap((req) => {
        if (!req) return of(null);
        const { def, savedId } = req;
        this.loading = true;
        return this.service.preview(def, savedId).pipe(
          map((r) => ({ r, err: '' })),
          catchError((err) => of({ r: null, err: apiError(err, 'Could not load the preview.') }))
        );
      })
    )
    .subscribe((res) => {
      if (!res) return;
      const { r, err } = res;
      this.loading = false;
      this.error = err;
      this.apply(r ?? EMPTY_RESULT);
    });

  get mode(): VizType {
    return this.override ?? this.definition.viz;
  }

  get chartMode(): VizType {
    return this.definition.viz === 'Table' ? 'Bar' : this.definition.viz;
  }

  get chartIcon(): typeof Table {
    return this.chartMode === 'Line' ? ChartLine : this.chartMode === 'Donut' ? ChartPie : ChartColumn;
  }

  get canChart(): boolean {
    return !!this.result.labelKey && !!this.result.valueKey && (this.result.rows.length > 0 || !!this.result.points?.length);
  }

  setMode(mode: VizType): void {
    this.override = mode === this.definition.viz ? null : mode;
  }

  ngDoCheck(): void {
    // A new visualization chosen in the builder wins over the local toggle
    if (this.viz !== null && this.viz !== this.definition.viz) this.override = null;
    this.viz = this.definition.viz;
    const sig = previewSignature(this.definition);
    if (sig === this.signature) return;
    this.signature = sig;
    this.grouped = !!this.definition.groupBy;
    if (!outputColumns(this.definition).length || !this.definition.sourceKey) {
      this.requests.next(null);
      this.loading = false;
      this.error = '';
      this.apply(EMPTY_RESULT);
      return;
    }
    const unchanged = !!this.savedId && sig === this.savedSignature;
    this.loading = true;
    this.requests.next({ def: JSON.parse(JSON.stringify(this.definition)) as ReportDefinition, savedId: unchanged ? this.savedId : null });
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }

  private apply(r: PreviewResult): void {
    // Ignore a late answer for a definition that has since lost its columns
    if (r !== EMPTY_RESULT && !outputColumns(this.definition).length) r = EMPTY_RESULT;
    this.result = r;
    const valueCol = r.columns.find((c) => c.key === r.valueKey);
    const kind: FieldKind = valueCol?.kind ?? 'number';
    this.valueLabel = valueCol?.label ?? '';
    this.valueFormat = kind === 'money' ? moneyShort : kind === 'percent' ? pct : compactNumber;
    // Chart points from the API cover every matching row; otherwise chart the preview rows
    const points = r.points?.length
      ? r.points
      : r.rows.map((row) => ({ label: String(row[r.labelKey] ?? ''), value: Number(row[r.valueKey]) || 0 }));
    // Charts read best with a handful of categories
    const top = points.slice(0, 10);
    this.labels = top.map((p) => shorten(p.label));
    this.series = [{ name: this.valueLabel, values: top.map((p) => p.value), color: RA_COLORS.purple }];
    const pie: ChartSlice[] = points.slice(0, 6).map((p, i) => ({ name: p.label, value: p.value, color: RA_SERIES_COLORS[i % RA_SERIES_COLORS.length] }));
    const rest = points.slice(6).reduce((sum, p) => sum + p.value, 0);
    if (rest > 0) pie.push({ name: 'Other', value: rest, color: RA_COLORS.grey });
    this.slices = pie;
  }

  isNum(c: PreviewColumn): boolean {
    return c.kind === 'number' || c.kind === 'money' || c.kind === 'percent';
  }

  cell(c: PreviewColumn, row: ReportRow): string {
    return formatCell(c.kind, row[c.key]);
  }
}

function moneyShort(v: number): string {
  return `AED ${compactNumber(v)}`;
}

function pct(v: number): string {
  return `${compactNumber(v)}%`;
}

function shorten(s: string): string {
  return s.length > 16 ? `${s.slice(0, 15)}…` : s;
}

const EMPTY_RESULT: PreviewResult = { columns: [], rows: [], matched: 0, labelKey: '', valueKey: '', title: '' };

/** What the API sees of a definition: the preview reloads only when this changes. */
export function previewSignature(def: ReportDefinition): string {
  return def.sourceKey ? JSON.stringify(toApiDefinition(def)) : '';
}
