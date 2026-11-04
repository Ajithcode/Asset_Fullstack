import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  LucideAngularModule,
  ArrowDown,
  ArrowRight,
  ArrowUp,
  ArrowUpDown,
  CalendarDays,
  ChartColumn,
  ChevronLeft,
  ChevronRight,
  Clock,
  Ellipsis,
  Gauge,
  Info,
  Plus,
  Search,
  TriangleAlert,
  Upload,
  User
} from 'lucide-angular';
import { InspectionApi, apiErrorMessage } from '../shared/inspection-api';
import { CalibrationInstrument } from '../shared/inspection.models';
import { Pager, downloadCsv, fmtDate, isoDay, pillClass } from '../shared/insp-utils';
import { CAL_STATUSES, calibrationStatus, certHref, certLabel } from './calibration-utils';
import { InstrumentDetail } from './instrument-detail';
import { CalibrationDrawer, DrawerMode } from './calibration-drawer';
import { ScheduleDialog } from './schedule-dialog';

type SortKey = 'name' | 'range' | 'last' | 'next' | 'status' | 'cert';
const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/** Calibration Management: instrument register, calibration results, certificates and upcoming calibrations. */
@Component({
  selector: 'app-insp-calibration',
  imports: [FormsModule, RouterLink, LucideAngularModule, InstrumentDetail, CalibrationDrawer, ScheduleDialog],
  templateUrl: './calibration.html',
  styleUrls: ['../shared/insp-page.css', './calibration.css']
})
export class CalibrationPage {
  private readonly api = inject(InspectionApi);
  private readonly queryCode = toSignal(inject(ActivatedRoute).queryParamMap, { requireSync: true });
  private handledCode: string | null = null;
  readonly icons = {
    ArrowDown, ArrowRight, ArrowUp, ArrowUpDown, CalendarDays, ChartColumn, ChevronLeft, ChevronRight,
    Clock, Ellipsis, Gauge, Info, Plus, Search, TriangleAlert, Upload, User
  };
  readonly fmtDate = fmtDate;
  readonly pillClass = pillClass;
  readonly certHref = certHref;
  readonly certLabel = certLabel;
  readonly statuses = CAL_STATUSES;
  readonly today = isoDay(new Date());
  readonly columns: { key: SortKey; label: string }[] = [
    { key: 'name', label: 'Instrument / ID' },
    { key: 'range', label: 'Range' },
    { key: 'last', label: 'Last Calibration' },
    { key: 'next', label: 'Next Due' },
    { key: 'status', label: 'Status' },
    { key: 'cert', label: 'Certificate' }
  ];

  readonly instruments = this.api.instruments;
  readonly pager = new Pager(6);

  readonly search = signal('');
  readonly statusFilter = signal('');
  readonly locationFilter = signal('');
  readonly sortKey = signal<SortKey>('name');
  readonly sortDir = signal<1 | -1>(1);
  readonly page = signal(1);
  readonly checked = signal<Set<string>>(new Set());
  readonly activeId = signal<string | null>(null);
  readonly menuId = signal<string | null>(null);
  readonly confirmDeleteId = signal<string | null>(null);
  readonly drawer = signal<{ mode: DrawerMode; instrument: CalibrationInstrument | null } | null>(null);
  readonly scheduleFor = signal<string | null | undefined>(undefined);
  readonly toast = signal<string | null>(null);
  private toastTimer?: ReturnType<typeof setTimeout>;

  readonly locations = computed(() => [...new Set(this.instruments.items().map((i) => i.location).filter(Boolean))].sort());

  readonly people = computed(() => {
    const names = new Set<string>();
    this.instruments.items().forEach((i) => {
      if (i.assignedTo) names.add(i.assignedTo);
      (i.history ?? []).forEach((h) => h.performedBy && names.add(h.performedBy));
    });
    this.api.inspections.items().forEach((i) => i.assignedTo && names.add(i.assignedTo));
    return [...names].sort();
  });

  readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    const st = this.statusFilter();
    const loc = this.locationFilter();
    const key = this.sortKey();
    const dir = this.sortDir();
    return this.instruments
      .items()
      .filter((i) => {
        if (st && this.status(i) !== st) return false;
        if (loc && i.location !== loc) return false;
        if (!q) return true;
        return [i.instrumentName, i.instrumentCode, i.serialNumber].some((v) => (v ?? '').toLowerCase().includes(q));
      })
      .sort((a, b) => this.sortValue(a, key).localeCompare(this.sortValue(b, key), undefined, { numeric: true }) * dir);
  });

  readonly rows = computed(() => {
    const all = this.filtered();
    this.pager.page = Math.min(this.page(), this.pager.pages(all.length));
    return this.pager.slice(all);
  });

  readonly active = computed(() => this.instruments.items().find((i) => i.id === this.activeId()) ?? null);

  readonly kpis = computed(() => {
    const all = this.instruments.items().map((i) => this.status(i));
    return {
      total: all.length,
      dueSoon: all.filter((s) => s === 'Due Soon').length,
      overdue: all.filter((s) => s === 'Overdue').length,
      oot: all.filter((s) => s === 'Out of Tolerance').length
    };
  });

  readonly upcoming = computed(() =>
    this.instruments
      .items()
      .filter((i) => !!i.nextDue && i.nextDue.slice(0, 10) >= this.today)
      .sort((a, b) => a.nextDue!.localeCompare(b.nextDue!))
      .slice(0, 3)
      .map((i) => {
        const d = new Date(`${i.nextDue!.slice(0, 10)}T00:00:00`);
        return { i, day: String(d.getDate()).padStart(2, '0'), month: MONTHS[d.getMonth()] };
      })
  );

  readonly allPageChecked = computed(() => {
    const rows = this.rows();
    const set = this.checked();
    return rows.length > 0 && rows.every((r) => set.has(r.id));
  });

  constructor() {
    this.api.instruments.load();
    this.api.inspections.load();
    // Deep link: /inspection/calibration?code=PG-001 selects that instrument and clears filters hiding it.
    effect(() => {
      const code = this.queryCode().get('code');
      const items = this.instruments.items();
      if (!code || code === this.handledCode || !items.length) return;
      const inst = items.find((x) => x.instrumentCode.toLowerCase() === code.toLowerCase());
      if (!inst) return;
      this.handledCode = code;
      if (!this.filtered().includes(inst)) {
        this.search.set('');
        this.statusFilter.set('');
        this.locationFilter.set('');
      }
      this.activeId.set(inst.id);
      this.page.set(Math.floor(this.filtered().indexOf(inst) / this.pager.size) + 1);
    });
    effect(() => {
      const rows = this.filtered();
      const id = this.activeId();
      if (rows.length && (!id || !this.instruments.items().some((i) => i.id === id))) this.activeId.set(rows[0]!.id);
    });
  }

  status(i: CalibrationInstrument): string {
    return calibrationStatus(i, this.today);
  }

  sortBy(key: SortKey): void {
    if (this.sortKey() === key) this.sortDir.update((d) => (d === 1 ? -1 : 1));
    else {
      this.sortKey.set(key);
      this.sortDir.set(1);
    }
  }

  setFilter(kind: 'search' | 'statusFilter' | 'locationFilter', value: string): void {
    this[kind].set(value);
    this.page.set(1);
  }

  pageCount(): number {
    return this.pager.pages(this.filtered().length);
  }

  goPage(p: number): void {
    this.page.set(Math.min(Math.max(1, p), this.pageCount()));
  }

  toggleCheck(id: string): void {
    this.checked.update((s) => {
      const next = new Set(s);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  togglePage(on: boolean): void {
    this.checked.update((s) => {
      const next = new Set(s);
      this.rows().forEach((r) => (on ? next.add(r.id) : next.delete(r.id)));
      return next;
    });
  }

  toggleMenu(id: string, ev: Event): void {
    ev.stopPropagation();
    this.menuId.update((cur) => (cur === id ? null : id));
  }

  viewAllCalibrations(): void {
    this.search.set('');
    this.statusFilter.set('');
    this.locationFilter.set('');
    this.sortKey.set('next');
    this.sortDir.set(1);
    this.page.set(1);
  }

  openDrawer(mode: DrawerMode, instrument: CalibrationInstrument | null = null): void {
    this.menuId.set(null);
    this.drawer.set({ mode, instrument });
  }

  openSchedule(id: string | null): void {
    this.menuId.set(null);
    this.scheduleFor.set(id);
  }

  onSaved(i: CalibrationInstrument, message: string): void {
    this.drawer.set(null);
    this.scheduleFor.set(undefined);
    this.activeId.set(i.id);
    this.flash(message);
  }

  askDelete(i: CalibrationInstrument): void {
    this.menuId.set(null);
    this.confirmDeleteId.set(i.id);
  }

  remove(i: CalibrationInstrument): void {
    this.confirmDeleteId.set(null);
    this.instruments.remove(i.id).subscribe({
      next: () => {
        this.checked.update((s) => {
          const next = new Set(s);
          next.delete(i.id);
          return next;
        });
        this.flash('Instrument deleted.');
      },
      error: (e) => this.flash(apiErrorMessage(e))
    });
  }

  export(): void {
    const set = this.checked();
    const source = set.size ? this.instruments.items().filter((i) => set.has(i.id)) : this.filtered();
    const rows = source.map((i) => ({
      'Instrument ID': i.instrumentCode,
      Instrument: i.instrumentName,
      'Serial Number': i.serialNumber,
      Location: i.location,
      Range: i.range,
      Tolerance: i.tolerance,
      'Interval (months)': i.intervalMonths,
      Provider: i.provider,
      'Last Calibration': i.lastCalibration ?? '',
      'Next Due': i.nextDue ?? '',
      'Last Result': i.lastResult,
      Status: this.status(i),
      Certificate: certLabel(i.certificate)
    }));
    if (!rows.length) return this.flash('No instruments to export.');
    downloadCsv(`calibration-instruments-${this.today}.csv`, rows);
  }

  flash(message: string): void {
    this.toast.set(message);
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => this.toast.set(null), 3000);
  }

  private sortValue(i: CalibrationInstrument, key: SortKey): string {
    switch (key) {
      case 'name':
        return i.instrumentName ?? '';
      case 'range':
        return i.range ?? '';
      case 'last':
        return i.lastCalibration ?? '';
      case 'next':
        return i.nextDue ?? '9999';
      case 'status':
        return this.status(i);
      case 'cert':
        return certLabel(i.certificate);
    }
  }
}
