import { Component, computed, effect, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LucideAngularModule, ChevronLeft, ChevronRight, User } from 'lucide-angular';
import { Inspection } from '../shared/inspection.models';
import { fmtDate, fmtTime, isoDay, pillClass } from '../shared/insp-utils';

type View = 'Month' | 'Week' | 'List';

interface CalEvent {
  day: string;
  status: string;
  cls: string;
  time: string;
  insp: Inspection;
}

interface Cell {
  day: string;
  date: number;
  monthLabel: string;
  inMonth: boolean;
  events: CalEvent[];
}

const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
const WEEKDAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
const CLASS: Record<string, string> = { Scheduled: 'ev--sched', 'In Progress': 'ev--prog', Completed: 'ev--done', Overdue: 'ev--over' };

function parseDay(day: string): Date {
  return new Date(`${day}T00:00:00`);
}

/** Inspection Schedule card: month / week / list of inspections by scheduled start (or due date), plus a day detail list. */
@Component({
  selector: 'app-insp-schedule-calendar',
  imports: [RouterLink, LucideAngularModule],
  templateUrl: './schedule-calendar.html',
  styleUrls: ['../shared/insp-page.css', './schedule-calendar.css']
})
export class ScheduleCalendar {
  readonly inspections = input<Inspection[]>([]);
  readonly loading = input(false);

  readonly icons = { ChevronLeft, ChevronRight, User };
  readonly pillClass = pillClass;
  readonly weekHeads = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
  readonly legend = [
    { label: 'Scheduled', cls: 'ev--sched' },
    { label: 'In Progress', cls: 'ev--prog' },
    { label: 'Completed', cls: 'ev--done' },
    { label: 'Overdue', cls: 'ev--over' }
  ];
  readonly today = isoDay(new Date());

  readonly view = signal<View>('Month');
  /** First day of the displayed month. */
  readonly cursor = signal(new Date(new Date().getFullYear(), new Date().getMonth(), 1));
  readonly selectedDay = signal<string>(this.today);
  private positioned = false;

  readonly events = computed<CalEvent[]>(() =>
    this.inspections()
      .map((i) => {
        const day = this.dayOf(i);
        if (!day) return null;
        const status = this.displayStatus(i, day);
        return { day, status, cls: CLASS[status] ?? 'ev--other', time: this.timeOf(i), insp: i };
      })
      .filter((e): e is CalEvent => !!e)
      .sort((a, b) => (a.day + a.time).localeCompare(b.day + b.time))
  );

  private readonly byDay = computed(() => {
    const map = new Map<string, CalEvent[]>();
    for (const e of this.events()) map.set(e.day, [...(map.get(e.day) ?? []), e]);
    return map;
  });

  readonly title = computed(() => {
    const c = this.cursor();
    return `${MONTHS[c.getMonth()]} ${c.getFullYear()}`;
  });

  readonly monthCells = computed<Cell[]>(() => {
    const first = this.cursor();
    const offset = (first.getDay() + 6) % 7;
    const daysInMonth = new Date(first.getFullYear(), first.getMonth() + 1, 0).getDate();
    const count = Math.ceil((offset + daysInMonth) / 7) * 7;
    const start = new Date(first.getFullYear(), first.getMonth(), 1 - offset);
    return Array.from({ length: count }, (_, k) => this.cell(new Date(start.getFullYear(), start.getMonth(), start.getDate() + k), first.getMonth()));
  });

  readonly weekCells = computed<Cell[]>(() => {
    const sel = parseDay(this.selectedDay());
    const monday = new Date(sel.getFullYear(), sel.getMonth(), sel.getDate() - ((sel.getDay() + 6) % 7));
    return Array.from({ length: 7 }, (_, k) => this.cell(new Date(monday.getFullYear(), monday.getMonth(), monday.getDate() + k), sel.getMonth()));
  });

  readonly monthEvents = computed(() => {
    const c = this.cursor();
    const prefix = `${c.getFullYear()}-${String(c.getMonth() + 1).padStart(2, '0')}`;
    return this.events().filter((e) => e.day.startsWith(prefix));
  });

  readonly dayEvents = computed(() => this.byDay().get(this.selectedDay()) ?? []);

  readonly dayTitle = computed(() => {
    const d = parseDay(this.selectedDay());
    return `${WEEKDAYS[d.getDay()]}, ${d.getDate()} ${MONTHS[d.getMonth()]} ${d.getFullYear()}`;
  });

  constructor() {
    // Start at the current month; if it has no inspections, jump to the month of the nearest one.
    effect(() => {
      const events = this.events();
      if (this.positioned || !events.length) return;
      this.positioned = true;
      const prefix = this.today.slice(0, 7);
      if (events.some((e) => e.day.startsWith(prefix))) {
        if (!this.byDay().has(this.today)) {
          const firstUpcoming = events.find((e) => e.day.startsWith(prefix) && e.day >= this.today);
          if (firstUpcoming) this.selectedDay.set(firstUpcoming.day);
        }
        return;
      }
      const now = parseDay(this.today).getTime();
      const nearest = events.reduce((best, e) =>
        Math.abs(parseDay(e.day).getTime() - now) < Math.abs(parseDay(best.day).getTime() - now) ? e : best
      );
      this.goTo(nearest.day);
    });
  }

  setView(v: View): void {
    this.view.set(v);
  }

  move(step: number): void {
    if (this.view() === 'Week') {
      const d = parseDay(this.selectedDay());
      this.goTo(isoDay(new Date(d.getFullYear(), d.getMonth(), d.getDate() + step * 7)));
      return;
    }
    const c = this.cursor();
    const next = new Date(c.getFullYear(), c.getMonth() + step, 1);
    this.cursor.set(next);
    const todayInMonth = this.today.startsWith(isoDay(next).slice(0, 7));
    this.selectedDay.set(todayInMonth ? this.today : isoDay(next));
  }

  goToday(): void {
    this.goTo(this.today);
  }

  select(day: string): void {
    this.goTo(day);
  }

  fmtDate = fmtDate;

  private goTo(day: string): void {
    const d = parseDay(day);
    this.cursor.set(new Date(d.getFullYear(), d.getMonth(), 1));
    this.selectedDay.set(day);
  }

  private cell(d: Date, month: number): Cell {
    const day = isoDay(d);
    const showMonth = d.getDate() === 1 || d.getMonth() !== month;
    return {
      day,
      date: d.getDate(),
      monthLabel: showMonth ? MONTHS[d.getMonth()]!.slice(0, 3) : '',
      inMonth: d.getMonth() === month,
      events: this.byDay().get(day) ?? []
    };
  }

  private dayOf(i: Inspection): string | null {
    const v = i.scheduledStart || i.dueDate;
    if (!v) return null;
    if (v.length === 10) return v;
    const d = new Date(v);
    return isNaN(d.getTime()) ? null : isoDay(d);
  }

  private timeOf(i: Inspection): string {
    if (!i.scheduledStart || i.scheduledStart.length <= 10) return '';
    const end = i.scheduledEnd && i.scheduledEnd.length > 10 ? fmtTime(i.scheduledEnd) : '';
    return end ? `${fmtTime(i.scheduledStart)} – ${end}` : fmtTime(i.scheduledStart);
  }

  /** Scheduled / draft inspections past their due date show as Overdue. */
  private displayStatus(i: Inspection, day: string): string {
    const status = i.status || 'Scheduled';
    if (['Completed', 'Cancelled', 'In Progress', 'Overdue'].includes(status)) return status;
    const due = i.dueDate ? i.dueDate.slice(0, 10) : day;
    return due < this.today ? 'Overdue' : status === 'Draft' ? 'Draft' : 'Scheduled';
  }
}
