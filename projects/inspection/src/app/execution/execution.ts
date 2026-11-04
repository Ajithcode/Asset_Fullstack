import { Component, DestroyRef, computed, effect, inject, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { map } from 'rxjs';
import {
  LucideAngularModule,
  Box,
  Calendar,
  ChevronRight,
  CircleAlert,
  CircleCheck,
  Circle,
  ClipboardList,
  Clock,
  CloudCheck,
  FileText,
  Image as ImageIcon,
  MapPin,
  Pause,
  Play,
  Save,
  ShieldCheck,
  User
} from 'lucide-angular';
import { AuthService } from 'shared-ui';
import { InspectionApi, apiErrorMessage } from '../shared/inspection-api';
import { Checklist, Inspection, InspectionFinding, InspectionResponse } from '../shared/inspection.models';
import { fmtDateTime, fmtTime, isoDay, pillClass } from '../shared/insp-utils';
import { ExecTask, TaskRef, evaluateReading, isImage } from './exec-task';

interface SectionView {
  name: string;
  tasks: TaskRef[];
  done: number;
  pct: number;
}

const ACTIVE = ['In Progress', 'Scheduled', 'Overdue'];
const pad = (n: number) => String(n).padStart(2, '0');

@Component({
  selector: 'app-insp-execution',
  standalone: true,
  imports: [FormsModule, RouterLink, LucideAngularModule, ExecTask],
  templateUrl: './execution.html',
  styleUrls: ['../shared/insp-page.css', './execution.css']
})
export class ExecutionPage {
  private readonly api = inject(InspectionApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly icons = {
    Box, Calendar, ChevronRight, CircleAlert, CircleCheck, Circle, ClipboardList, Clock, CloudCheck, FileText, ImageIcon, MapPin,
    Pause, Play, Save, ShieldCheck, User
  };
  readonly pillClass = pillClass;
  readonly fmtDateTime = fmtDateTime;

  readonly inspections = this.api.inspections;
  readonly findingsStore = this.api.findings;

  private readonly code = toSignal(this.route.queryParamMap.pipe(map((p) => p.get('code'))), { initialValue: null });

  /** Inspection from ?code, else the first In Progress, else the first Scheduled. */
  readonly selected = computed<Inspection | undefined>(() => {
    const rows = this.inspections.items();
    const code = this.code();
    if (code) return rows.find((i) => i.inspectionCode === code);
    return rows.find((i) => i.status === 'In Progress') ?? rows.find((i) => i.status === 'Scheduled');
  });

  readonly choices = computed(() => {
    const cur = this.selected();
    return this.inspections.items().filter((i) => ACTIVE.includes(i.status ?? '') || i.id === cur?.id);
  });

  /** Working copy edited in place; saved with PUT. */
  private readonly workSig = signal<Inspection | null>(null);
  /** Bumped whenever the working copy is mutated in place. */
  private readonly rev = signal(0);
  readonly cur = signal(0);
  dirty = false;
  paused = false;
  readonly saving = signal(false);
  lastSynced: Date | null = null;
  error = '';
  toast = '';
  private saveTimer: ReturnType<typeof setTimeout> | null = null;
  private resave = false;
  readonly now = signal(Date.now());

  constructor() {
    for (const s of [this.inspections, this.findingsStore, this.api.tasks, this.api.checklists]) s.load();

    effect(() => {
      const sel = this.selected();
      if (sel && sel.id !== untracked(this.workSig)?.id) untracked(() => this.adopt(sel));
      if (!sel) untracked(() => this.workSig.set(null));
    });

    const tick = setInterval(() => !this.paused && this.now.set(Date.now()), 1000);
    inject(DestroyRef).onDestroy(() => {
      clearInterval(tick);
      if (this.saveTimer) clearTimeout(this.saveTimer);
    });
  }

  get work(): Inspection | null {
    return this.workSig();
  }

  private bump(): void {
    this.rev.update((n) => n + 1);
  }

  private adopt(sel: Inspection): void {
    const w = structuredClone(sel);
    w.responses ??= [];
    w.signOff ??= [];
    w.responses.forEach((r) => (r.evidence ??= []));
    this.workSig.set(w);
    this.dirty = false;
    this.paused = false;
    this.lastSynced = sel.updatedAt ? new Date(sel.updatedAt) : null;
    const firstPending = w.responses.findIndex((r) => !r.result);
    this.cur.set(firstPending >= 0 ? firstPending : 0);
  }

  switchTo(code: string): void {
    if (this.dirty) this.save();
    this.router.navigate([], { relativeTo: this.route, queryParams: { code } });
  }

  get readOnly(): boolean {
    return !this.work || ['Completed', 'Cancelled', 'Draft'].includes(this.work.status ?? '');
  }

  // ===== Derived views (the working copy is mutated in place; `rev` signals each change) =====
  readonly responses = computed<InspectionResponse[]>(() => {
    this.rev();
    return [...(this.workSig()?.responses ?? [])];
  });

  readonly sections = computed<SectionView[]>(() => {
    const out: SectionView[] = [];
    this.responses().forEach((r, i) => {
      const name = r.sectionName || 'Checklist';
      let s = out.find((x) => x.name === name);
      if (!s) out.push((s = { name, tasks: [], done: 0, pct: 0 }));
      s.tasks.push({ r, i });
      if (r.result) s.done++;
    });
    for (const s of out) s.pct = Math.round((s.done / s.tasks.length) * 100);
    return out;
  });

  readonly currentTask = computed<TaskRef | null>(() => {
    const r = this.responses()[this.cur()];
    return r ? { r, i: this.cur() } : null;
  });

  readonly currentSection = computed<SectionView | undefined>(() => {
    const name = this.responses()[this.cur()]?.sectionName || 'Checklist';
    return this.sections().find((s) => s.name === name);
  });

  readonly counts = computed(() => {
    const rs = this.responses();
    const done = rs.filter((r) => r.result).length;
    return {
      done,
      total: rs.length,
      pct: rs.length ? Math.round((done / rs.length) * 100) : 0,
      pass: rs.filter((r) => r.result === 'Pass').length,
      fail: rs.filter((r) => r.result === 'Fail').length,
      na: rs.filter((r) => r.result === 'N/A').length,
      pending: rs.length - done
    };
  });

  readonly requirements = computed(() => {
    const rs = this.responses();
    const work = this.workSig();
    const mandatory = rs.filter((r) => r.required);
    const answered = mandatory.filter((r) => r.result).length;
    const evidenceNeeded = !!work?.evidenceRequired;
    const failed = rs.filter((r) => r.result === 'Fail');
    const withEvidence = failed.filter((r) => r.evidence?.length).length;
    const inspector = work?.signOff.find((s) => s.role === 'Inspector');
    const remaining = mandatory.length - answered + (evidenceNeeded ? failed.length - withEvidence : 0);
    return {
      answered,
      mandatory: mandatory.length,
      evidenceNeeded,
      failed: failed.length,
      withEvidence,
      signed: inspector?.state === 'Signed',
      remaining
    };
  });

  readonly evidence = computed(() => {
    const all = this.responses().flatMap((r) => r.evidence ?? []);
    const photos = all.filter(isImage).length;
    return { photos, documents: all.length - photos };
  });

  readonly findings = computed<InspectionFinding[]>(() => {
    const code = this.workSig()?.inspectionCode;
    return code ? this.findingsStore.items().filter((f) => f.inspectionCode === code) : [];
  });

  readonly findingTasks = computed(() => this.findings().map((f) => f.sourceTask));

  get elapsed(): string {
    const start = this.work?.startedAt ? new Date(this.work.startedAt).getTime() : NaN;
    if (isNaN(start)) return '00:00:00';
    const end = this.work?.completedAt ? new Date(this.work.completedAt).getTime() : this.now();
    const s = Math.max(0, Math.floor((end - start) / 1000));
    return `${pad(Math.floor(s / 3600))}:${pad(Math.floor((s % 3600) / 60))}:${pad(s % 60)}`;
  }

  get syncLabel(): string {
    if (this.saving()) return 'Saving…';
    if (this.dirty) return 'Unsaved changes';
    return this.lastSynced ? `All changes saved • Last synced ${fmtTime(this.lastSynced.toISOString())}` : 'No changes yet';
  }

  sectionIcon(s: SectionView) {
    return s.pct === 100 ? this.icons.CircleCheck : s.done ? this.icons.Clock : this.icons.Circle;
  }

  // ===== Actions =====
  goToSection(s: SectionView): void {
    const pending = s.tasks.find((t) => !t.r.result);
    this.cur.set((pending ?? s.tasks[0]!).i);
  }

  go(i: number): void {
    if (i >= 0 && i < this.responses().length) this.cur.set(i);
  }

  start(): void {
    if (!this.work || this.work.startedAt) return;
    this.work.startedAt = new Date().toISOString();
    if (this.work.status !== 'Completed') this.work.status = 'In Progress';
  }

  onChanged(): void {
    this.start();
    this.bump();
    this.dirty = true;
    if (this.saveTimer) clearTimeout(this.saveTimer);
    this.saveTimer = setTimeout(() => this.save(), 2500);
  }

  togglePause(): void {
    this.paused = !this.paused;
    if (this.paused && this.dirty) this.save();
    this.flash(this.paused ? 'Inspection paused – progress saved' : 'Inspection resumed');
  }

  save(after?: () => void): void {
    if (!this.work) return;
    if (this.saveTimer) clearTimeout(this.saveTimer);
    this.saveTimer = null;
    if (this.saving()) {
      this.resave = true;
      return;
    }
    this.saving.set(true);
    this.error = '';
    this.dirty = false;
    const id = this.work.id;
    this.inspections.save(structuredClone(this.work)).subscribe({
      next: () => {
        this.saving.set(false);
        this.lastSynced = new Date();
        if (this.resave && this.work?.id === id) {
          this.resave = false;
          this.save(after);
        } else after?.();
      },
      error: (e) => {
        this.saving.set(false);
        this.dirty = true;
        this.resave = false;
        this.error = apiErrorMessage(e);
      }
    });
  }

  saveAndExit(): void {
    if (!this.dirty) {
      this.router.navigate(['/inspection/overview']);
      return;
    }
    this.save(() => this.router.navigate(['/inspection/overview']));
  }

  complete(): void {
    const w = this.work;
    if (!w || this.requirements().remaining > 0 || !this.responses().length) return;
    if (!confirm('Complete this inspection? Responses can no longer be edited afterwards.')) return;
    const at = new Date().toISOString();
    this.start();
    w.status = 'Completed';
    w.completedAt = at;
    const name = w.assignedTo || this.auth.getUserEmail() || '';
    const step = w.signOff.find((s) => s.role === 'Inspector');
    if (step) Object.assign(step, { name: step.name || name, state: 'Signed', signedAt: at });
    else w.signOff.unshift({ role: 'Inspector', name, state: 'Signed', signedAt: at });
    const reviewer = w.signOff.find((s) => s.role !== 'Inspector' && s.state === 'Pending');
    if (reviewer) reviewer.state = 'Awaiting review';
    this.bump();
    this.save(() => this.flash(`${w.inspectionCode} completed`));
  }

  /** Builds pending responses from the inspection's checklist when it has none yet. */
  loadChecklistTasks(): void {
    const w = this.work;
    const chk: Checklist | undefined = this.api.checklists.items().find((c) => c.checklistCode === w?.checklistCode);
    if (!w || !chk) return;
    const lib = new Map(this.api.tasks.items().map((t) => [t.taskCode, t] as const));
    w.responses = [...chk.sections]
      .sort((a, b) => a.order - b.order)
      .flatMap((s) =>
        [...s.tasks]
          .sort((a, b) => a.order - b.order)
          .map((t) => {
            const k = lib.get(t.taskCode);
            return {
              sectionName: s.sectionName, taskCode: t.taskCode, taskTitle: t.taskTitle || k?.taskTitle || t.taskCode,
              responseType: t.responseType || k?.responseType || 'Pass / Fail', unit: k?.unit ?? '', minValue: k?.minValue ?? null,
              maxValue: k?.maxValue ?? null, procedure: k?.procedure || t.acceptanceCriteria || '', required: t.isMandatory,
              reading: '', result: '', notes: '', evidence: []
            };
          })
      );
    this.cur.set(0);
    this.onChanged();
  }

  get checklistAvailable(): boolean {
    return !!this.api.checklists.items().find((c) => c.checklistCode === this.work?.checklistCode);
  }

  createFinding(t: TaskRef): void {
    const w = this.work;
    if (!w) return;
    const r = t.r;
    const lib = this.api.tasks.items().find((x) => x.taskCode === r.taskCode);
    const ev = evaluateReading(r);
    const unit = r.unit ? ` ${r.unit}` : '';
    const range =
      r.minValue != null && r.maxValue != null ? `${r.minValue} – ${r.maxValue}${unit}` : r.maxValue != null ? `≤ ${r.maxValue}${unit}` : r.minValue != null ? `≥ ${r.minValue}${unit}` : '';
    const due = new Date();
    due.setDate(due.getDate() + 7);
    const user = w.assignedTo || this.auth.getUserEmail() || 'Inspector';
    const finding: Partial<InspectionFinding> = {
      title: `${r.taskTitle} – failed`,
      inspectionCode: w.inspectionCode,
      assetCode: w.assetCode,
      assetName: w.assetName,
      sourceTask: r.taskTitle,
      severity: lib?.defaultSeverity || 'Medium',
      owner: w.assignedTo,
      dueDate: isoDay(due),
      recordedValue: r.reading ? `${r.reading}${unit}` : r.result,
      acceptance: range || r.procedure,
      observation: r.notes || (ev && !ev.pass ? ev.text : ''),
      evidence: [...(r.evidence ?? [])],
      correctiveAction: '',
      assignedTo: '',
      workOrder: '',
      workOrderStatus: '',
      workflowStep: 'Open',
      activity: [{ at: new Date().toISOString(), by: user, text: `Finding raised from ${w.inspectionCode} – ${r.taskTitle}` }],
      status: 'Open'
    };
    this.findingsStore.save(finding).subscribe({
      next: (saved) => {
        w.findingsCount = (w.findingsCount ?? 0) + 1;
        this.flash(`${saved.findingCode || 'Finding'} created`);
        this.onChanged();
      },
      error: (e) => (this.error = apiErrorMessage(e))
    });
  }

  private flash(msg: string): void {
    this.toast = msg;
    setTimeout(() => (this.toast = ''), 2500);
  }
}
