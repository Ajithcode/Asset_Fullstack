import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LucideAngularModule, Check, Ellipsis, ExternalLink, Image, Info, X } from 'lucide-angular';
import { AuthService } from 'shared-ui';
import { InspectionApi, apiErrorMessage } from '../shared/inspection-api';
import { InspectionFinding } from '../shared/inspection.models';
import { fmtDate, fmtDateTime, initials, isoDay, pillClass } from '../shared/insp-utils';

type Tab = 'Overview' | 'Actions' | 'Activity';

export const WORKFLOW = ['Open', 'Action', 'Verify', 'Closed'];
const STATUS_FOR: Record<string, string> = { Open: 'Open', Action: 'In Progress', Verify: 'Pending Verification', Closed: 'Closed' };
const STEP_FOR: Record<string, string> = { Open: 'Open', Overdue: 'Action', 'In Progress': 'Action', 'Pending Verification': 'Verify', Closed: 'Closed' };
const ADVANCE_LABEL = ['Start Corrective Action', 'Submit for Verification', 'Verify & Close'];
const WO_STATUSES = ['Not Created', 'Open', 'In Progress', 'On Hold', 'Completed', 'Cancelled'];

/** Right-hand panel for the selected finding: overview, corrective action, activity and the closure workflow. */
@Component({
  selector: 'app-insp-finding-detail',
  imports: [FormsModule, RouterLink, LucideAngularModule],
  templateUrl: './finding-detail.html',
  styleUrls: ['../shared/insp-page.css', './finding-detail.css']
})
export class FindingDetail {
  private readonly api = inject(InspectionApi);
  private readonly auth = inject(AuthService);

  readonly finding = input.required<InspectionFinding>();
  readonly people = input<string[]>([]);
  readonly edit = output<void>();
  readonly notify = output<string>();

  readonly icons = { Check, Ellipsis, ExternalLink, Image, Info, X };
  readonly steps = WORKFLOW;
  readonly woStatuses = WO_STATUSES;
  readonly fmtDate = fmtDate;
  readonly fmtDateTime = fmtDateTime;
  readonly initials = initials;
  readonly pillClass = pillClass;

  readonly tab = signal<Tab>('Overview');
  readonly menuOpen = signal(false);
  readonly reassigning = signal(false);
  readonly showAllEvidence = signal(false);
  readonly preview = signal<string | null>(null);
  readonly saving = signal(false);
  newOwner = '';
  comment = '';
  actionForm = { correctiveAction: '', assignedTo: '', dueDate: '', workOrder: '', workOrderStatus: '' };
  private formFor: string | null = null;

  readonly stepIndex = computed(() => {
    const f = this.finding();
    const i = WORKFLOW.indexOf(f.workflowStep);
    return i >= 0 ? i : Math.max(0, WORKFLOW.indexOf(STEP_FOR[f.status ?? ''] ?? 'Open'));
  });

  readonly advanceLabel = computed(() => ADVANCE_LABEL[this.stepIndex()] ?? '');
  readonly overdue = computed(() => {
    const f = this.finding();
    return f.status !== 'Closed' && !!f.dueDate && f.dueDate.slice(0, 10) < isoDay(new Date());
  });
  readonly activity = computed(() => [...(this.finding().activity ?? [])].sort((a, b) => b.at.localeCompare(a.at)));
  readonly evidence = computed(() => {
    const all = this.finding().evidence ?? [];
    return this.showAllEvidence() ? all : all.slice(0, 2);
  });

  isImage(src: string): boolean {
    return src.startsWith('data:image') || /^https?:\/\//i.test(src);
  }

  evidenceLabel(src: string): string {
    return src.length > 28 ? `${src.slice(0, 26)}…` : src;
  }

  setTab(t: Tab): void {
    this.tab.set(t);
    if (t === 'Actions') this.resetActionForm();
  }

  resetActionForm(): void {
    const f = this.finding();
    this.formFor = f.id;
    this.actionForm = {
      correctiveAction: f.correctiveAction ?? '',
      assignedTo: f.assignedTo ?? '',
      dueDate: f.dueDate ? f.dueDate.slice(0, 10) : '',
      workOrder: f.workOrder ?? '',
      workOrderStatus: f.workOrderStatus ?? ''
    };
  }

  /** Re-seed the Actions form when another finding is selected while the tab is open. */
  actionFormReady(): boolean {
    if (this.formFor !== this.finding().id) this.resetActionForm();
    return true;
  }

  advance(): void {
    const f = this.finding();
    const i = this.stepIndex();
    if (i >= WORKFLOW.length - 1) return;
    const next = WORKFLOW[i + 1]!;
    if (next === 'Verify' && !f.correctiveAction?.trim()) {
      this.notify.emit('Record the corrective action before submitting for verification.');
      this.setTab('Actions');
      return;
    }
    const text =
      next === 'Action' ? 'Corrective action started' : next === 'Verify' ? 'Submitted for verification' : 'Verified and closed';
    this.update({ workflowStep: next, status: STATUS_FOR[next]! }, text, `Finding moved to ${next}.`);
  }

  stepBack(): void {
    const i = this.stepIndex();
    if (i <= 0) return;
    const prev = WORKFLOW[i - 1]!;
    const closed = WORKFLOW[i] === 'Closed';
    const step = closed ? 'Action' : prev;
    this.update(
      { workflowStep: step, status: STATUS_FOR[step]! },
      closed ? 'Finding reopened' : `Sent back to ${step}`,
      closed ? 'Finding reopened.' : `Finding sent back to ${step}.`
    );
  }

  startReassign(): void {
    this.newOwner = this.finding().owner ?? '';
    this.reassigning.set(true);
  }

  reassign(): void {
    const owner = this.newOwner.trim();
    const f = this.finding();
    if (!owner || owner === f.owner) return this.reassigning.set(false);
    this.update({ owner }, `Reassigned from ${f.owner || 'nobody'} to ${owner}`, `Finding reassigned to ${owner}.`, () =>
      this.reassigning.set(false)
    );
  }

  saveAction(): void {
    const a = this.actionForm;
    this.update(
      {
        correctiveAction: a.correctiveAction.trim(),
        assignedTo: a.assignedTo,
        dueDate: a.dueDate || null,
        workOrder: a.workOrder.trim(),
        workOrderStatus: a.workOrderStatus
      },
      'Corrective action updated',
      'Corrective action saved.'
    );
  }

  addComment(): void {
    const text = this.comment.trim();
    if (!text) return;
    this.update({}, text, 'Comment added.', () => (this.comment = ''));
  }

  private update(changes: Partial<InspectionFinding>, activityText: string, message: string, done?: () => void): void {
    const f = this.finding();
    const by = this.auth.getUser()?.userName || this.auth.getUserEmail() || 'Unknown user';
    const record: InspectionFinding = {
      ...f,
      ...changes,
      activity: [...(f.activity ?? []), { at: new Date().toISOString(), by, text: activityText }]
    };
    this.saving.set(true);
    this.api.findings.save(record).subscribe({
      next: () => {
        this.saving.set(false);
        this.notify.emit(message);
        done?.();
      },
      error: (e) => {
        this.saving.set(false);
        this.notify.emit(apiErrorMessage(e));
      }
    });
  }
}
