import { Component, EventEmitter, Input, OnDestroy, OnInit, Output, inject } from '@angular/core';
import { Subscription } from 'rxjs';
import { DashboardStore } from '../dashboard-store.service';
import { DashboardWidget } from '../dashboard-widget/dashboard-widget';
import { FormsModule } from '@angular/forms';
import {
  DashFilters, METRICS, MetricDef, MetricKey, WIDGET_SIZES, WIDGET_TYPES, WidgetConfig, WidgetData, WidgetSpan, WidgetType
} from '../interactive-dashboards.data';

export interface NewWidget {
  type: WidgetType;
  metric: MetricKey;
  title: string;
  span: WidgetSpan;
}

/** Asks which data source a new widget shows, its title and size. */
@Component({
  selector: 'app-idb-add-widget-dialog',
  standalone: true,
  imports: [FormsModule, DashboardWidget],
  template: `
    <div class="overlay" (click)="cancel.emit()">
      <form class="dialog" (click)="$event.stopPropagation()" (ngSubmit)="submit()" novalidate role="dialog" aria-modal="true" aria-label="Add widget">
        <h2>Add widget</h2>
        <p>Choose what the widget shows. Values follow the dashboard filters.</p>
        <div class="field" [class.invalid]="tried && !type">
          <label for="aw-type">Widget type <span class="req">*</span></label>
          <select id="aw-type" name="type" [(ngModel)]="type" (ngModelChange)="onTypeChange()">
            <option value="" disabled>Select Widget Type</option>
            @for (t of types; track t.type) { <option [value]="t.type">{{ t.label }}</option> }
          </select>
          @if (tried && !type) { <small>Choose a widget type.</small> }
        </div>
        <div class="field" [class.invalid]="tried && !metric">
          <label for="aw-metric">Data source / metric <span class="req">*</span></label>
          <select id="aw-metric" name="metric" [(ngModel)]="metric" (ngModelChange)="onMetricChange()">
            <option value="">Select Metric</option>
            @for (m of metrics; track m.key) { <option [value]="m.key">{{ m.label }}</option> }
          </select>
          @if (tried && !metric) { <small>Choose the metric this widget shows.</small> }
          @else if (selected) { <small>{{ selected.description }}</small> }
        </div>
        <div class="field" [class.invalid]="tried && !title.trim()">
          <label for="aw-title">Title <span class="req">*</span></label>
          <input id="aw-title" name="title" [(ngModel)]="title" maxlength="60" />
          @if (tried && !title.trim()) { <small>Enter a title.</small> }
        </div>
        <div class="field" [class.invalid]="tried && span === null">
          <label for="aw-size">Size <span class="req">*</span></label>
          <select id="aw-size" name="span" [(ngModel)]="span">
            <option [ngValue]="null" disabled>Select Size</option>
            @for (s of sizes; track s.span) { <option [ngValue]="s.span">{{ s.label }}</option> }
          </select>
          @if (tried && span === null) { <small>Choose a size.</small> }
        </div>
        @if (previewConfig) {
          <div class="field">
            <span>Preview</span>
            <app-idb-widget [config]="previewConfig" [filters]="filters" [data]="previewData" [height]="180" [bare]="true" />
          </div>
        }
        <div class="dialog__actions">
          <button type="button" class="btn" (click)="cancel.emit()">Cancel</button>
          <button type="submit" class="btn btn--primary">Add widget</button>
        </div>
      </form>
    </div>
  `,
  styleUrls: ['../../shared/analytics-page.css']
})
export class AddWidgetDialog implements OnInit, OnDestroy {
  private readonly store = inject(DashboardStore);
  /** Page filters, for the preview (POST /api/interactive-dashboards/widgets/preview) */
  @Input({ required: true }) filters!: DashFilters;
  @Input() initialType: WidgetType | '' = '';
  @Output() added = new EventEmitter<NewWidget>();
  @Output() cancel = new EventEmitter<void>();

  readonly types = WIDGET_TYPES;
  readonly sizes = WIDGET_SIZES;
  type: WidgetType | '' = '';
  metric: MetricKey | '' = '';
  title = '';
  span: WidgetSpan | null = null;
  tried = false;
  previewConfig: WidgetConfig | null = null;
  previewData: WidgetData = { empty: true, message: 'Loading preview…' };
  private previewSub: Subscription | null = null;

  ngOnInit(): void {
    this.type = this.initialType;
    this.onTypeChange();
  }

  get metrics(): MetricDef[] {
    const type = this.type;
    return type ? METRICS.filter((m) => m.types.includes(type)) : [];
  }

  get selected(): MetricDef | undefined {
    return METRICS.find((m) => m.key === this.metric);
  }

  onTypeChange(): void {
    if (this.type) this.span = WIDGET_TYPES.find((t) => t.type === this.type)?.span ?? 6;
    if (!this.metrics.some((m) => m.key === this.metric)) this.metric = '';
    if (this.metrics.length === 1) this.metric = this.metrics[0].key;
    this.onMetricChange();
  }

  onMetricChange(): void {
    this.loadPreview();
    // Keep a title the user typed; otherwise follow the metric
    if (this.title.trim() && !METRICS.some((m) => m.label === this.title)) return;
    this.title = this.selected?.label ?? '';
  }

  ngOnDestroy(): void {
    this.previewSub?.unsubscribe();
  }

  /** Data for the chosen type and metric: POST /widgets/preview. */
  private loadPreview(): void {
    this.previewSub?.unsubscribe();
    const type = this.type;
    const metric = this.metric;
    if (!type || !metric) {
      this.previewConfig = null;
      return;
    }
    const label = this.selected?.label ?? metric;
    this.previewConfig = { id: 'preview', type, metric, title: label, span: 12 };
    this.previewData = { empty: true, message: 'Loading preview…' };
    this.previewSub = this.store.preview(type, metric, label, this.filters).subscribe({
      next: (d) => (this.previewData = d),
      error: () => (this.previewData = { empty: true, message: 'Preview not available.' })
    });
  }

  submit(): void {
    this.tried = true;
    if (!this.type || !this.metric || !this.title.trim() || this.span === null) return;
    this.added.emit({ type: this.type, metric: this.metric, title: this.title.trim(), span: this.span });
  }
}
