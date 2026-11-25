import { Component, EventEmitter, Input, OnChanges, OnDestroy, Output, SimpleChanges, inject } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Subscription } from 'rxjs';
import { ExportMenu, ExportRequest } from 'shared-ui';
import { Download, LucideAngularModule, Pencil, Play, Trash2, X } from 'lucide-angular';
import { CartesianChart, ChartSeries, RA_COLORS, ValueFormat, compactNumber } from '../shared/charts';
import { StandardReport, SR_TIME_ZONES } from './standard-reports.data';
import { MovementRecord, PreviewRow, StandardReportDetails, StandardReportsService } from './standard-reports.service';
import { formatDay, formatPeriod, formatStamp } from './standard-reports.util';

/** Viewer: headline figure, a small chart and the breakdown table from GET /api/standard-reports/{id}/details. */
@Component({
  selector: 'app-sr-report-viewer',
  standalone: true,
  imports: [LucideAngularModule, CartesianChart, ExportMenu],
  template: `
    <div class="overlay" (click)="close.emit()">
      <div class="dialog dialog--wide" role="dialog" aria-modal="true" [attr.aria-label]="shown.name" (click)="stop($event)">
        <div class="sr-viewer__head">
          <div>
            <h2>{{ shown.name }}</h2>
            <p class="muted sr-viewer__meta">{{ shown.id }} · {{ shown.module }}{{ shown.reportType ? ' · ' + shown.reportType : '' }} · Owner {{ shown.owner }}</p>
          </div>
          <button type="button" class="icon-btn" aria-label="Close" (click)="close.emit()">
            <lucide-icon [img]="icons.X" [size]="18" />
          </button>
        </div>
        <p>{{ shown.description }}</p>
        <div class="mini-stats">
          <div class="mini-stat"><div><span>{{ headlineLabel }}</span><strong>{{ headline }}</strong></div></div>
          <div class="mini-stat"><div><span>Report period</span><strong class="sr-viewer__small">{{ period }}</strong></div></div>
          <div class="mini-stat"><div><span>Generated on</span><strong class="sr-viewer__small">{{ generated }}</strong></div></div>
          <div class="mini-stat"><div><span>Time zone</span><strong class="sr-viewer__small">{{ timeZone }}</strong></div></div>
        </div>
        @if (rows.length) {
        <ra-cartesian-chart
          [labels]="labels"
          [series]="series"
          [kind]="kind"
          [height]="220"
          [format]="format"
          [showValues]="kind === 'bar'"
          [legend]="false"
          [ariaLabel]="shown.preview.measure + ' by ' + dimensionLabel"
        />
        }
        <div class="table-wrap sr-viewer__table">
          <table class="table">
            <thead>
              <tr>
                <th>{{ dimensionLabel }}</th>
                <th class="num">{{ shown.preview.measure }}</th>
                <th class="num">{{ shareLabel }}</th>
              </tr>
            </thead>
            <tbody>
              @for (r of rows; track r.label) {
                <tr [class.sr-viewer__pick]="hasVisits" [class.sr-viewer__picked]="pickedDate === r.label" (click)="pickDate(r)">
                  <td>{{ rowLabel(r) }}</td>
                  <td class="num">{{ r.display }}</td>
                  <td class="num">{{ r.share }}</td>
                </tr>
              } @empty {
                <tr>
                  <td colspan="3" class="empty">
                    @if (loading) {
                      Loading report data…
                    } @else if (error) {
                      {{ error }}
                    } @else {
                      {{ details && !details.hasData ? 'This report type has no data behind it.' : 'No data available for this report.' }}
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        @if (hasVisits) {
          <div class="mini-stats">
            <div class="mini-stat"><div><span>Visits</span><strong>{{ visits.length.toLocaleString('en-US') }}</strong></div></div>
            <div class="mini-stat"><div><span>Assets</span><strong>{{ assetCount.toLocaleString('en-US') }}</strong></div></div>
            <div class="mini-stat"><div><span>Zones</span><strong>{{ zoneTotals.length }}</strong></div></div>
          </div>
          <div class="table-wrap sr-viewer__table">
            <table class="table">
              <thead><tr><th>Zone</th><th class="num">Visits</th><th class="num">Total time</th></tr></thead>
              <tbody>
                @for (z of zoneTotals; track z.zone) {
                  <tr><td>{{ z.zone }}</td><td class="num">{{ z.visits }}</td><td class="num">{{ z.time }}</td></tr>
                }
              </tbody>
            </table>
          </div>
          <p class="muted sr-viewer__note">
            {{ pickedDate ? 'Visits on ' + pickedDate + ' (click the date again to show all)' : 'All visits, sorted by Zone In (click a date above to drill down)' }}
            · showing {{ shownVisits.length.toLocaleString('en-US') }} of {{ filteredVisits.length.toLocaleString('en-US') }}
          </p>
          <div class="table-wrap sr-viewer__table sr-viewer__visits">
            <table class="table">
              <thead>
                <tr>
                  <th>Asset ID</th><th>Asset Name</th><th>Category</th><th>Sub Category</th><th>Tag</th><th>Zone</th>
                  <th>Gateway</th><th>Zone In</th><th>Zone Out</th><th class="num">Time Spent</th><th class="num">Readings</th>
                </tr>
              </thead>
              <tbody>
                @for (v of shownVisits; track $index) {
                  <tr>
                    <td>{{ v.assetId }}</td><td>{{ v.assetName || '—' }}</td><td>{{ v.category || '—' }}</td><td>{{ v.subCategory || '—' }}</td>
                    <td>{{ v.tagId }}</td><td>{{ v.zoneName || '—' }}</td><td>{{ v.deviceId }}</td>
                    <td>{{ v.zoneInLocal }}</td><td>{{ v.zoneOutLocal }}</td><td class="num">{{ v.timeSpent }}</td><td class="num">{{ v.readingCount }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
        @if (details?.truncated) {
          <p class="muted sr-viewer__note">The period has more readings than one report can hold; only the earliest ones are included.</p>
        }
        <div class="dialog__actions">
          <button type="button" class="btn" [disabled]="busy" (click)="remove.emit(shown)"><lucide-icon [img]="icons.Trash2" [size]="16" /> Delete</button>
          <button type="button" class="btn" [disabled]="busy" (click)="edit.emit(shown)"><lucide-icon [img]="icons.Pencil" [size]="16" /> Edit</button>
          <button type="button" class="btn" [disabled]="busy" (click)="generate.emit(shown)"><lucide-icon [img]="icons.Play" [size]="16" /> {{ busy ? 'Working…' : 'Generate' }}</button>
          <button type="button" class="btn" (click)="close.emit()">Close</button>
          <app-export-menu [disabled]="loading" [source]="exportFor(shown)" />
        </div>
      </div>
    </div>
  `,
  styleUrls: ['../shared/analytics-page.css', './standard-reports.css']
})
export class ReportViewerDialog implements OnChanges, OnDestroy {
  private readonly service = inject(StandardReportsService);

  @Input({ required: true }) report!: StandardReport;
  /** Generate / delete in progress on the page */
  @Input() busy = false;
  @Output() close = new EventEmitter<void>();
  /** Builds the CSV / PDF export of a report (page's per-report export). */
  @Input({ required: true }) exportFor!: (r: StandardReport) => () => ExportRequest;
  @Output() generate = new EventEmitter<StandardReport>();
  @Output() edit = new EventEmitter<StandardReport>();
  @Output() remove = new EventEmitter<StandardReport>();

  readonly icons = { X, Download, Pencil, Play, Trash2 };
  details: StandardReportDetails | null = null;
  loading = false;
  error = '';
  rows: PreviewRow[] = [];
  labels: string[] = [];
  series: ChartSeries[] = [];
  kind: 'bar' | 'line' = 'bar';
  format: ValueFormat = compactNumber;
  private sub: Subscription | null = null;
  pickedDate = '';
  private static readonly MAX_SHOWN = 500;

  get visits(): MovementRecord[] {
    return this.details?.records ?? [];
  }

  get hasVisits(): boolean {
    return this.visits.length > 0;
  }

  get filteredVisits(): MovementRecord[] {
    return this.pickedDate ? this.visits.filter((v) => v.zoneInLocal.startsWith(this.pickedDate)) : this.visits;
  }

  get shownVisits(): MovementRecord[] {
    return this.filteredVisits.slice(0, ReportViewerDialog.MAX_SHOWN);
  }

  get assetCount(): number {
    return new Set(this.visits.map((v) => v.assetId)).size;
  }

  /** Visits and total time per zone ('—' = gateway not plotted), longest first. */
  get zoneTotals(): { zone: string; visits: number; time: string }[] {
    const totals = new Map<string, { visits: number; seconds: number }>();
    for (const v of this.visits) {
      const key = v.zoneName ?? '—';
      const t = totals.get(key) ?? { visits: 0, seconds: 0 };
      t.visits++;
      t.seconds += v.timeSpentSeconds;
      totals.set(key, t);
    }
    const pad2 = (n: number) => String(n).padStart(2, '0');
    return [...totals].sort((a, b) => b[1].seconds - a[1].seconds).map(([zone, t]) => ({
      zone,
      visits: t.visits,
      time: `${pad2(Math.floor(t.seconds / 3600))}:${pad2(Math.floor((t.seconds % 3600) / 60))}:${pad2(t.seconds % 60)}`
    }));
  }

  pickDate(r: PreviewRow): void {
    if (this.hasVisits) this.pickedDate = this.pickedDate === r.label ? '' : r.label;
  }

  /** The report as the details call returned it (falls back to the table row). */
  get shown(): StandardReport {
    return this.details?.report ?? this.report;
  }

  get dimensionLabel(): string {
    const d = this.shown.preview.dimension;
    return d === 'category' ? 'Asset category' : d === 'site' ? 'Site' : d === 'date' ? 'Date' : 'Month';
  }

  get shareLabel(): string {
    return this.shown.preview.unit === 'percent' ? 'vs. average' : 'Share';
  }

  get headlineLabel(): string {
    const p = this.shown.preview;
    return p.unit === 'percent' ? `Average ${p.measure.toLowerCase()}` : `Total ${p.measure.toLowerCase()}`;
  }

  get headline(): string {
    return this.loading ? '…' : this.service.formatValue(this.shown, this.shown.preview.total);
  }

  get period(): string {
    return formatPeriod(this.shown.period);
  }

  get generated(): string {
    return formatStamp(this.shown.generatedOn || null);
  }

  get timeZone(): string {
    const tz = this.shown.period.timeZone;
    return SR_TIME_ZONES.find((z) => z.value === tz)?.label ?? tz;
  }

  rowLabel(r: PreviewRow): string {
    return this.shown.preview.dimension === 'date' && /^\d{4}-\d{2}-\d{2}$/.test(r.label) ? formatDay(r.label) : r.label;
  }

  ngOnChanges(changes: SimpleChanges): void {
    // Reload when a report opens or the page hands back an updated copy (generate / edit)
    if (changes['report']) this.load();
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  private load(): void {
    this.sub?.unsubscribe();
    this.loading = true;
    this.error = '';
    this.pickedDate = '';
    this.sub = this.service.details(this.report).subscribe({
      next: (d) => {
        this.loading = false;
        this.details = d;
        this.setRows(d.rows);
      },
      error: (err: HttpErrorResponse) => {
        this.loading = false;
        this.details = null;
        this.error = err.status === 404 ? 'This report no longer exists.' : 'Could not load the report data.';
        this.setRows([]);
      }
    });
  }

  private setRows(rows: PreviewRow[]): void {
    this.rows = rows;
    this.labels = rows.map((r) => this.rowLabel(r));
    const p = this.shown.preview;
    this.kind = p.dimension === 'month' || p.dimension === 'date' ? 'line' : 'bar';
    this.format = p.unit === 'percent' ? (v: number) => `${Math.round(v)}%` : compactNumber;
    this.series = [{ name: p.measure, values: rows.map((r) => r.value), color: RA_COLORS.purple, area: this.kind === 'line' }];
  }

  stop(event: Event): void {
    event.stopPropagation();
  }
}
