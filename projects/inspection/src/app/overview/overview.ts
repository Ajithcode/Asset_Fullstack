import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import {
  LucideAngularModule,
  Box,
  Calendar,
  CalendarDays,
  ChevronLeft,
  ChevronRight,
  CircleAlert,
  CircleCheck,
  CirclePlay,
  ClipboardList,
  Download,
  EllipsisVertical,
  ExternalLink,
  Eye,
  FileText,
  List,
  Play,
  Plus,
  Search,
  Wrench
} from 'lucide-angular';
import { InspectionApi, apiErrorMessage } from '../shared/inspection-api';
import { Inspection, InspectionFinding } from '../shared/inspection.models';
import { OverviewCalendar, inspectionDay } from './overview-calendar';
import { Pager, downloadCsv, fmtDate, fmtTime, initials, isoDay, pillClass } from '../shared/insp-utils';

type FilterKey = 'site' | 'type' | 'status' | 'search' | 'from' | 'to';

/** Statuses that never turn Overdue (work already started, finished or not yet submitted). */
const CLOSED = ['Completed', 'Cancelled', 'Draft', 'In Progress'];
const SEVERITY_ORDER: Record<string, number> = { Critical: 0, High: 1, Medium: 2, Low: 3 };

function uniq(values: (string | null | undefined)[]): string[] {
  return [...new Set(values.filter((v): v is string => !!v))].sort();
}

@Component({
  selector: 'app-insp-overview',
  standalone: true,
  imports: [FormsModule, RouterLink, LucideAngularModule, OverviewCalendar],
  templateUrl: './overview.html',
  styleUrls: ['../shared/insp-page.css', './overview.css']
})
export class OverviewPage {
  private readonly api = inject(InspectionApi);
  private readonly router = inject(Router);

  readonly icons = {
    Box, Calendar, CalendarDays, ChevronLeft, ChevronRight, CircleAlert, CircleCheck, CirclePlay, ClipboardList, Download,
    EllipsisVertical, ExternalLink, Eye, FileText, List, Play, Plus, Search, Wrench
  };
  readonly fmtDate = fmtDate;
  readonly fmtTime = fmtTime;
  readonly initials = initials;
  readonly pillClass = pillClass;
  readonly statuses = ['Draft', 'Scheduled', 'In Progress', 'Overdue', 'Completed', 'Cancelled'];

  readonly inspections = this.api.inspections;
  readonly findings = this.api.findings;
  readonly today = isoDay(new Date());

  readonly search = signal('');
  readonly site = signal('');
  readonly type = signal('');
  readonly status = signal('');
  readonly from = signal('');
  readonly to = signal('');
  readonly view = signal<'list' | 'calendar'>('list');
  readonly pager = new Pager(7);
  menuFor: string | null = null;
  toast = '';
  actionError = '';

  constructor() {
    this.inspections.load();
    this.findings.load();
    this.api.plans.load();
  }

  /** Status shown in the register: scheduled inspections past their due date read as Overdue. */
  statusOf(i: Inspection): string {
    const s = i.status ?? '';
    if (!CLOSED.includes(s) && i.dueDate && i.dueDate.slice(0, 10) < this.today) return 'Overdue';
    return s;
  }

  readonly statusFn = (i: Inspection): string => this.statusOf(i);

  typeLabel(i: Inspection): string {
    return i.typeShort || i.inspectionTypeName || '';
  }

  readonly sites = computed(() => uniq(this.inspections.items().map((i) => i.siteName)));
  readonly types = computed(() => uniq(this.inspections.items().map((i) => this.typeLabel(i))));

  readonly kpi = computed(() => {
    const rows = this.inspections.items();
    const count = (s: string) => rows.filter((i) => this.statusOf(i) === s).length;
    return {
      scheduled: count('Scheduled'),
      inProgress: count('In Progress'),
      overdue: count('Overdue'),
      openFindings: this.findings.items().filter((f) => f.status !== 'Closed').length
    };
  });

  readonly rows = computed(() => {
    const q = this.search().trim().toLowerCase();
    const from = this.from();
    const to = this.to();
    return this.inspections
      .items()
      .filter((i) => {
        if (this.site() && i.siteName !== this.site()) return false;
        if (this.type() && this.typeLabel(i) !== this.type()) return false;
        if (this.status() && this.statusOf(i) !== this.status()) return false;
        const due = (i.dueDate ?? '').slice(0, 10);
        if (from && (!due || due < from)) return false;
        if (to && (!due || due > to)) return false;
        if (!q) return true;
        return [i.inspectionCode, i.title, i.assetCode, i.assetName, i.location, i.assignedTo, i.siteName].some((v) =>
          (v ?? '').toLowerCase().includes(q)
        );
      })
      .sort((a, b) => (a.dueDate ?? '9999').localeCompare(b.dueDate ?? '9999'));
  });

  /** Today's inspections, or those of the next day that has any. */
  readonly schedule = computed(() => {
    const open = this.inspections.items().filter((i) => !['Cancelled', 'Draft'].includes(i.status ?? '') && !!inspectionDay(i));
    let day = this.today;
    let items = open.filter((i) => inspectionDay(i) === day);
    if (!items.length) {
      const next = open.map(inspectionDay).filter((d) => d > this.today).sort()[0];
      if (next) {
        day = next;
        items = open.filter((i) => inspectionDay(i) === next);
      }
    }
    items = [...items].sort((a, b) => (a.scheduledStart ?? '').localeCompare(b.scheduledStart ?? ''));
    const label = new Date(`${day}T00:00:00`).toLocaleDateString('en-GB', { weekday: 'long', day: '2-digit', month: 'short', year: 'numeric' });
    return { isToday: day === this.today, label, items };
  });

  readonly actions = computed(() =>
    this.findings
      .items()
      .filter((f) => f.status !== 'Closed')
      .sort((a, b) => (SEVERITY_ORDER[a.severity] ?? 9) - (SEVERITY_ORDER[b.severity] ?? 9))
      .slice(0, 5)
  );

  readonly lifecycle = computed(() => {
    const f = this.findings.items();
    const k = this.kpi();
    return [
      { label: 'Plan', hint: 'Define scope and frequency', icon: this.icons.ClipboardList, count: this.api.plans.items().length },
      { label: 'Schedule', hint: 'Assign and plan inspections', icon: this.icons.CalendarDays, count: k.scheduled },
      { label: 'Execute', hint: 'Capture data in the field', icon: this.icons.Play, count: k.inProgress },
      { label: 'Findings', hint: 'Record and assess findings', icon: this.icons.FileText, count: f.filter((x) => x.status === 'Open').length },
      { label: 'Corrective Action', hint: 'Raise and track actions', icon: this.icons.Wrench, count: f.filter((x) => x.status === 'In Progress').length },
      { label: 'Verify & Close', hint: 'Confirm resolution', icon: this.icons.CircleCheck, count: f.filter((x) => x.status === 'Pending Verification').length }
    ];
  });

  typeClass(i: Inspection): string {
    const t = this.typeLabel(i).toLowerCase();
    if (t.includes('safety')) return 'chip chip--red';
    if (t.includes('statutory')) return 'chip chip--amber';
    if (t.includes('pre')) return 'chip chip--green';
    return 'chip chip--blue';
  }

  severityClass(f: InspectionFinding): string {
    return `ca ca--${(f.severity || 'low').toLowerCase()}`;
  }

  setFilter(kind: FilterKey, value: string): void {
    this[kind].set(value);
    this.pager.page = 1;
  }

  showStatus(s: string): void {
    this.setFilter('status', this.status() === s ? '' : s);
    this.view.set('list');
  }

  /** Drafts reopen in the wizard; everything else opens in Execution. */
  open(i: Inspection): void {
    if (i.status === 'Draft') this.router.navigate(['/inspection/new'], { queryParams: { id: i.id } });
    else this.router.navigate(['/inspection/execution'], { queryParams: { code: i.inspectionCode } });
  }

  toggleMenu(i: Inspection, ev: Event): void {
    ev.stopPropagation();
    this.menuFor = this.menuFor === i.id ? null : i.id;
  }

  editDraft(i: Inspection): void {
    this.menuFor = null;
    this.router.navigate(['/inspection/new'], { queryParams: { id: i.id } });
  }

  execute(i: Inspection): void {
    this.menuFor = null;
    this.router.navigate(['/inspection/execution'], { queryParams: { code: i.inspectionCode } });
  }

  cancel(i: Inspection): void {
    this.menuFor = null;
    if (!confirm(`Cancel inspection ${i.inspectionCode}?`)) return;
    this.actionError = '';
    this.inspections.save({ ...i, status: 'Cancelled' }).subscribe({
      next: () => this.flash(`${i.inspectionCode} cancelled`),
      error: (e) => (this.actionError = apiErrorMessage(e))
    });
  }

  export(): void {
    const rows = this.rows();
    if (!rows.length) return this.flash('Nothing to export');
    downloadCsv(
      `inspections-${this.today}.csv`,
      rows.map((i) => ({
        'Inspection ID': i.inspectionCode,
        Title: i.title,
        Type: i.inspectionTypeName,
        Asset: i.assetName,
        'Asset Code': i.assetCode,
        Location: i.location,
        Site: i.siteName,
        'Assigned To': i.assignedTo,
        Team: i.assignedTeam,
        Priority: i.priority,
        'Due Date': i.dueDate ?? '',
        Status: this.statusOf(i),
        Findings: i.findingsCount
      }))
    );
  }

  private flash(msg: string): void {
    this.toast = msg;
    setTimeout(() => (this.toast = ''), 2500);
  }
}
