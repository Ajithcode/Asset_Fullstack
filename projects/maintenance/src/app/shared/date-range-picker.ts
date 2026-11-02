import { Component, computed, model, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LucideAngularModule, CalendarDays, ChevronDown } from 'lucide-angular';
import { DateRange, RANGE_PRESETS } from './derive';
import { fmtDate } from './mnt-utils';

/** "01 Jan 2026 - 31 Dec 2026" button with presets and a custom from / to. Two-way bound via [(range)]. */
@Component({
  selector: 'app-mnt-date-range',
  imports: [FormsModule, LucideAngularModule],
  host: { '(document:click)': 'open.set(false)' },
  template: `
    <div class="dr" (click)="$event.stopPropagation()">
      <button type="button" class="dr-btn" (click)="toggle()" [attr.aria-expanded]="open()">
        <lucide-icon [img]="icons.CalendarDays" [size]="17"></lucide-icon>
        <span class="dr-text">{{ text() }}</span>
        <span class="dr-caret"><lucide-icon [img]="icons.ChevronDown" [size]="17"></lucide-icon></span>
      </button>
      @if (open()) {
        <div class="dr-pop">
          @for (p of presets; track p.key) { <button type="button" class="dr-preset" (click)="pick(p.range())">{{ p.label }}</button> }
          <div class="dr-custom">
            <label>From <input type="date" [(ngModel)]="from" /></label>
            <label>To <input type="date" [(ngModel)]="to" /></label>
            <button type="button" class="dr-apply" [disabled]="!from || !to || to < from" (click)="pick({ from, to })">Apply</button>
          </div>
        </div>
      }
    </div>
  `,
  styles: `
    .dr { position: relative; }
    .dr-btn { display: inline-flex; align-items: center; gap: 10px; height: 40px; padding: 0 0 0 14px; border: 1px solid #d6d3e0; border-radius: 8px; background: #fff; font: inherit; font-size: 13.5px; color: #1c1442; cursor: pointer; }
    .dr-text { white-space: nowrap; }
    .dr-caret { display: inline-flex; align-items: center; height: 100%; padding: 0 10px; border-left: 1px solid #d6d3e0; margin-left: 12px; }
    .dr-pop { position: absolute; right: 0; top: 46px; z-index: 40; width: 270px; padding: 8px; background: #fff; border: 1px solid #e8e6f0; border-radius: 10px; box-shadow: 0 12px 28px rgba(0, 0, 0, 0.14); }
    .dr-preset { display: block; width: 100%; padding: 8px 10px; border: 0; border-radius: 6px; background: none; font: inherit; font-size: 13px; text-align: left; cursor: pointer; }
    .dr-preset:hover { background: #f1ecff; }
    .dr-custom { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; margin-top: 6px; padding-top: 8px; border-top: 1px solid #eceaf3; font-size: 12px; color: #6b6880; }
    .dr-custom label { display: flex; flex-direction: column; gap: 4px; }
    .dr-custom input { height: 32px; padding: 0 6px; border: 1px solid #d6d3e0; border-radius: 6px; font: inherit; font-size: 12.5px; }
    .dr-apply { grid-column: 1 / -1; height: 32px; border: 0; border-radius: 6px; background: #4318ff; color: #fff; font: inherit; font-weight: 600; cursor: pointer; }
    .dr-apply:disabled { opacity: 0.5; cursor: not-allowed; }
  `
})
export class DateRangePicker {
  readonly range = model.required<DateRange>();
  readonly open = signal(false);
  readonly presets = RANGE_PRESETS;
  readonly icons = { CalendarDays, ChevronDown };
  from = '';
  to = '';

  readonly text = computed(() => `${fmtDate(this.range().from)} - ${fmtDate(this.range().to)}`);

  toggle(): void {
    this.from = this.range().from;
    this.to = this.range().to;
    this.open.update((o) => !o);
  }

  pick(r: DateRange): void {
    this.range.set(r);
    this.open.set(false);
  }
}
