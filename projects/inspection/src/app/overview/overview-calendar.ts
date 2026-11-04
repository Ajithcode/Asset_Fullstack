import { Component, computed, input, output, signal } from '@angular/core';
import { LucideAngularModule, ChevronLeft, ChevronRight } from 'lucide-angular';
import { Inspection } from '../shared/inspection.models';
import { isoDay, pillClass } from '../shared/insp-utils';

interface CalDay {
  key: string;
  day: number;
  inMonth: boolean;
  isToday: boolean;
  items: Inspection[];
}

/** Day an inspection belongs to: scheduled start if set, else due date (local YYYY-MM-DD). */
export function inspectionDay(i: Inspection): string {
  if (i.scheduledStart) {
    const d = new Date(i.scheduledStart);
    if (!isNaN(d.getTime())) return isoDay(d);
  }
  return (i.dueDate ?? '').slice(0, 10);
}

/** Month grid (Monday first, 6 weeks) of inspections placed on their day. */
@Component({
  selector: 'app-insp-overview-calendar',
  standalone: true,
  imports: [LucideAngularModule],
  template: `
    <div class="cal">
      <div class="cal-head">
        <button type="button" class="icon-btn" aria-label="Previous month" (click)="shift(-1)"><lucide-icon [img]="icons.ChevronLeft" [size]="16"></lucide-icon></button>
        <strong>{{ monthLabel() }}</strong>
        <button type="button" class="icon-btn" aria-label="Next month" (click)="shift(1)"><lucide-icon [img]="icons.ChevronRight" [size]="16"></lucide-icon></button>
      </div>
      <div class="cal-grid">
        @for (w of weekdays; track w) { <div class="cal-wd">{{ w }}</div> }
        @for (d of days(); track d.key) {
          <div class="cal-day" [class.out]="!d.inMonth" [class.today]="d.isToday">
            <span class="cal-n">{{ d.day }}</span>
            @for (i of d.items.slice(0, 3); track i.id) {
              <button type="button" [class]="'cal-ev ' + pillClass(statusOf()(i))" [title]="i.title + ' – ' + statusOf()(i)" (click)="open.emit(i)">
                {{ i.inspectionCode || 'Draft' }}
              </button>
            }
            @if (d.items.length > 3) { <span class="muted">+{{ d.items.length - 3 }} more</span> }
          </div>
        }
      </div>
    </div>
  `,
  styleUrls: ['../shared/insp-page.css', './overview-calendar.css']
})
export class OverviewCalendar {
  readonly rows = input.required<Inspection[]>();
  readonly statusOf = input.required<(i: Inspection) => string>();
  readonly open = output<Inspection>();

  readonly icons = { ChevronLeft, ChevronRight };
  readonly pillClass = pillClass;
  readonly weekdays = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
  private readonly today = isoDay(new Date());
  readonly month = signal(new Date(new Date().getFullYear(), new Date().getMonth(), 1));
  readonly monthLabel = computed(() => this.month().toLocaleDateString('en-GB', { month: 'long', year: 'numeric' }));

  readonly days = computed<CalDay[]>(() => {
    const m = this.month();
    const byDay = new Map<string, Inspection[]>();
    for (const i of this.rows()) {
      const k = inspectionDay(i);
      if (k) byDay.set(k, [...(byDay.get(k) ?? []), i]);
    }
    const offset = (m.getDay() + 6) % 7;
    return Array.from({ length: 42 }, (_, n) => {
      const d = new Date(m.getFullYear(), m.getMonth(), 1 - offset + n);
      const key = isoDay(d);
      return { key, day: d.getDate(), inMonth: d.getMonth() === m.getMonth(), isToday: key === this.today, items: byDay.get(key) ?? [] };
    });
  });

  shift(delta: number): void {
    const m = this.month();
    this.month.set(new Date(m.getFullYear(), m.getMonth() + delta, 1));
  }
}
