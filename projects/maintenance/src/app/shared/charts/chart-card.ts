import { Component, input, output, signal } from '@angular/core';
import { LucideAngularModule, Ellipsis } from 'lucide-angular';

/** Card frame for a chart: title, "..." menu with Export data, and the chart as content. */
@Component({
  selector: 'app-mnt-chart-card',
  imports: [LucideAngularModule],
  host: { '(document:click)': 'open.set(false)' },
  template: `
    <section class="cc">
      <header class="cc-head">
        <h3>{{ title() }}</h3>
        <span class="cc-menu">
          <button type="button" class="cc-btn" aria-label="Chart options" (click)="$event.stopPropagation(); open.set(!open())"><lucide-icon [img]="Ellipsis" [size]="18"></lucide-icon></button>
          @if (open()) {
            <div class="cc-pop" (click)="$event.stopPropagation()"><button type="button" (click)="open.set(false); exportData.emit()">Export data (CSV)</button></div>
          }
        </span>
      </header>
      <ng-content />
    </section>
  `,
  styles: `
    :host { display: block; min-width: 0; }
    .cc { height: 100%; box-sizing: border-box; padding: 14px 18px 16px; background: #fff; border: 1px solid #e8e6f0; border-radius: 12px; box-shadow: 0 1px 2px rgba(22, 20, 39, 0.04); }
    .cc-head { display: flex; align-items: center; margin-bottom: 10px; }
    .cc-head h3 { flex: 1; margin: 0; font-size: 16.5px; font-weight: 800; color: #1c1442; }
    .cc-menu { position: relative; }
    .cc-btn { display: inline-flex; border: 0; background: none; color: #1c1442; cursor: pointer; padding: 4px; border-radius: 6px; }
    .cc-btn:hover { background: #f1ecff; }
    .cc-pop { position: absolute; right: 0; top: 30px; z-index: 30; min-width: 160px; padding: 6px; background: #fff; border: 1px solid #e8e6f0; border-radius: 8px; box-shadow: 0 8px 24px rgba(0, 0, 0, 0.12); }
    .cc-pop button { width: 100%; padding: 8px 10px; border: 0; border-radius: 6px; background: none; font: inherit; font-size: 13px; text-align: left; cursor: pointer; }
    .cc-pop button:hover { background: #f1ecff; }
  `
})
export class ChartCard {
  readonly title = input.required<string>();
  readonly exportData = output<void>();
  readonly open = signal(false);
  readonly Ellipsis = Ellipsis;
}
