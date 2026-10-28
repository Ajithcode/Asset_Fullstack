import { Component, Input } from '@angular/core';
import { ChartArea, ChartBar, ChartColumn, ChartLine, ChartPie, Donut, Gauge, LucideAngularModule, Sheet } from 'lucide-angular';
import { ChartPreview, ChartTypeRecord } from '../system-config.model';

export const CHART_PREVIEWS: { id: ChartPreview; label: string }[] = [
  { id: 'column', label: 'Column' },
  { id: 'bar', label: 'Bar' },
  { id: 'line', label: 'Line' },
  { id: 'pie', label: 'Pie' },
  { id: 'doughnut', label: 'Doughnut' },
  { id: 'area', label: 'Area' },
  { id: 'gauge', label: 'Gauge' },
  { id: 'table', label: 'Table' }
];

const ICONS = { column: ChartColumn, bar: ChartBar, line: ChartLine, pie: ChartPie, doughnut: Donut, area: ChartArea, gauge: Gauge, table: Sheet };
const COLORS: Record<ChartPreview, string> = {
  column: '#1d4ed8',
  bar: '#2563eb',
  line: '#0f766e',
  pie: '#ea580c',
  doughnut: '#1e3a8a',
  area: '#4f46e5',
  gauge: '#dc2626',
  table: '#2563eb'
};

/** Small picture of a chart type: its uploaded image, or a built-in icon in the chart's colour. */
@Component({
  selector: 'app-chart-preview',
  imports: [LucideAngularModule],
  template: `
    @if (chart.image) {
      <img [src]="chart.image" [alt]="chart.name + ' preview'" />
    } @else {
      <lucide-icon [img]="icons[chart.preview]" [size]="size" [style.color]="colors[chart.preview]"></lucide-icon>
    }
  `,
  styles: [
    `
      :host {
        display: inline-flex;
        align-items: center;
        justify-content: center;
      }

      img {
        width: 28px;
        height: 28px;
        object-fit: contain;
      }
    `
  ]
})
export class ChartPreviewIcon {
  @Input({ required: true }) chart!: Pick<ChartTypeRecord, 'name' | 'preview' | 'image'>;
  @Input() size = 24;

  readonly icons = ICONS;
  readonly colors = COLORS;
}
