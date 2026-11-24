import { Component, DestroyRef, OnDestroy, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  LucideAngularModule,
  Activity,
  Boxes,
  CalendarDays,
  Check,
  CircleAlert,
  CircleCheck,
  CircleX,
  Clock,
  ClipboardList,
  ExternalLink,
  FileText,
  Heart,
  History,
  Info,
  Layers,
  Link,
  MapPin,
  Network,
  Paperclip,
  Search,
  Send,
  Save,
  ShieldCheck,
  Trash2,
  Upload,
  User,
  X,
  Zap,
} from 'lucide-angular';
import { Subject, Subscription, catchError, debounceTime, forkJoin, of, switchMap } from 'rxjs';
import { AuthService } from 'shared-ui';
import { apiErrorMessage } from '../../services/asset-api';
import {
  Approval,
  Evidence,
  SaveChangeRequest,
  StatusConditionAsset,
  StatusConditionChange,
  StatusConditionLookups,
  StatusConditionOptions,
  StatusConditionService,
  conditionTone,
  formatDate,
  formatDateTime,
  stateTone,
  statusTone,
} from '../status-condition.service';

type Step = 1 | 2 | 3;
type CheckState = 'ok' | 'warn' | 'error';

interface ChangeForm {
  newStatus: string;
  effectiveDate: string;
  effectiveTime: string;
  newCondition: string;
  assessmentDate: string;
  assessmentTime: string;
  reasonCode: string;
  assessmentMethod: string;
  assessmentSource: string;
  assessedBy: string;
  nextAssessmentDue: string;
  remarks: string;
  operationalImpact: string;
  relatedWorkOrder: string;
  relatedInspection: string;
  downtimeEstimate: number | null;
  downtimeUnit: string;
  evidence: Evidence[];
  approvers: string[];
}

interface ValidationCheck {
  state: CheckState;
  text: string;
}

/** Fields checked in step 1, in display order, with the label used in error messages. */
const REQUIRED: [keyof ChangeForm, string][] = [
  ['newStatus', 'New status'],
  ['effectiveDate', 'Status effective date'],
  ['effectiveTime', 'Status effective time'],
  ['newCondition', 'New condition'],
  ['assessmentDate', 'Condition assessment date'],
  ['assessmentTime', 'Condition assessment time'],
  ['reasonCode', 'Reason / failure code'],
  ['assessmentMethod', 'Assessment method'],
  ['assessmentSource', 'Assessment source'],
  ['assessedBy', 'Assessed by'],
  ['remarks', 'Remarks / justification'],
];

const REMARKS_MAX = 500;

/** Dropdown options come only from GET /api/asset-status-condition/options; empty until it answers. */
const EMPTY_OPTIONS: StatusConditionOptions = {
  statuses: [],
  conditions: [],
  states: [],
  reasons: [],
  assessmentMethods: [],
  assessmentSources: [],
  operationalImpacts: [],
  downtimeUnits: [],
  approvalLevels: [],
  reasonsByStatus: {},
  impactsByStatus: {},
  methodsBySource: {},
};

const EMPTY_LOOKUPS: StatusConditionLookups = { workOrders: [], inspections: [], failureReasons: [] };

/** Unique, non-empty values in order; `keep` (the current value) is added when missing so it is never lost. */
function uniqueWith(values: string[], keep: string): string[] {
  const out = [...new Set(values.filter(Boolean))];
  if (keep && !out.includes(keep)) out.unshift(keep);
  return out;
}
const DEFAULT_LEVELS = [
  { level: 1, role: 'Maintenance Manager' },
  { level: 2, role: 'Asset Manager' },
];

function pad(n: number): string {
  return String(n).padStart(2, '0');
}

/** Local yyyy-MM-dd and HH:mm of an ISO time; empty when missing. */
function splitLocal(iso: string | null | undefined): [string, string] {
  if (!iso) return ['', ''];
  const d = new Date(iso);
  if (isNaN(d.getTime())) return ['', ''];
  return [`${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`, `${pad(d.getHours())}:${pad(d.getMinutes())}`];
}

/** ISO (UTC) of a local date and time; null when the date is missing. */
function joinLocal(date: string, time: string): string | null {
  if (!date) return null;
  const d = new Date(`${date}T${time || '00:00'}`);
  return isNaN(d.getTime()) ? null : d.toISOString();
}

function emptyForm(user: string): ChangeForm {
  const [today, now] = splitLocal(new Date().toISOString());
  return {
    newStatus: '',
    effectiveDate: today,
    effectiveTime: now,
    newCondition: '',
    assessmentDate: today,
    assessmentTime: now,
    reasonCode: '',
    assessmentMethod: '',
    assessmentSource: '',
    assessedBy: user,
    nextAssessmentDue: '',
    remarks: '',
    operationalImpact: '',
    relatedWorkOrder: '',
    relatedInspection: '',
    downtimeEstimate: null,
    downtimeUnit: 'Hours',
    evidence: [],
    approvers: ['', ''],
  };
}

/**
 * Assets > Asset Status & Condition > Update Status & Condition.
 * ?tag= preselects the asset; ?change= reopens a draft or rejected change.
 * Step 1 status, condition and assessment; step 2 evidence and links; step 3 review and submit for approval.
 */
@Component({
  selector: 'app-update-status-condition',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './update-status-condition.html',
  styleUrls: [
    '../../asset-status-condition/asset-status-condition.css',
    '../../asset-status-condition/asset-status-condition-panels.css',
    './update-status-condition.css',
  ],
})
export class UpdateStatusCondition implements OnDestroy {
  readonly icons = {
    Activity, Boxes, CalendarDays, Check, CircleAlert, CircleCheck, CircleX, Clock, ClipboardList, ExternalLink, FileText, Heart,
    History, Info, Layers, Link, MapPin, Network, Paperclip, Search, Send, Save, ShieldCheck, Trash2, Upload, User, X, Zap,
  };
  /** Users of this tenant, suggested as approvers. */
  approverChoices: string[] = [];
  /** Users and technicians, suggested for Assessed By. */
  assessorChoices: string[] = [];
  get statuses(): readonly string[] { return this.options.statuses; }
  get conditions(): readonly string[] { return this.options.conditions; }
  readonly statusTone = statusTone;
  readonly conditionTone = conditionTone;
  readonly stateTone = stateTone;
  readonly formatDate = formatDate;
  readonly formatDateTime = formatDateTime;
  readonly remarksMax = REMARKS_MAX;
  readonly steps: { n: Step; title: string; sub: string }[] = [
    { n: 1, title: 'Update Details', sub: 'Status & condition change' },
    { n: 2, title: 'Evidence & Links', sub: 'Supporting information' },
    { n: 3, title: 'Review & Submit', sub: 'Approval and confirmation' },
  ];

  private readonly api = inject(StatusConditionService);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly pickerSearch$ = new Subject<string>();
  private subs: Subscription[] = [];

  readonly userName = this.auth.getUser()?.userName || this.auth.getUserEmail() || '';

  options: StatusConditionOptions = EMPTY_OPTIONS;
  /** The asset's work orders and inspections and the failure reasons master data. */
  lookups: StatusConditionLookups = EMPTY_LOOKUPS;

  // Dropdown choices for the current status / source; rebuilt by refreshChoices() (not getters, so lists keep identity).
  reasonChoices: string[] = [];
  /** Failure reasons from the Inspection master data not already in reasonChoices. */
  failureReasonChoices: string[] = [];
  methodChoices: string[] = [];
  /** Inspection types of this asset, offered as extra assessment methods. */
  inspectionMethodChoices: string[] = [];
  impactChoices: string[] = [];
  asset: StatusConditionAsset | null = null;
  /** The draft / rejected change being edited; null for a new one. */
  change: StatusConditionChange | null = null;
  form: ChangeForm = emptyForm(this.userName);
  step: Step = 1;

  loading = true;
  loadError = '';
  saving = false;
  uploading = false;
  /** Field errors shown under the controls after Next / Submit. */
  errors: Partial<Record<keyof ChangeForm | 'asset' | 'evidence', string>> = {};
  formError = '';
  /** Set after a successful submit: the page shows the confirmation. */
  submitted: StatusConditionChange | null = null;

  // Asset picker (when opened without ?tag=)
  pickerQuery = '';
  pickerResults: StatusConditionAsset[] = [];
  pickerLoading = false;

  // Add link (step 2)
  linkName = '';
  linkUrl = '';

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  constructor() {
    this.subs.push(
      this.pickerSearch$
        .pipe(
          debounceTime(250),
          switchMap((q) => {
            this.pickerLoading = true;
            return this.api.assets({ search: q, pageSize: 8 }).pipe(
              catchError((err) => {
                this.loadError = apiErrorMessage(err, 'Could not search the assets.');
                return of({ items: [] as StatusConditionAsset[], totalCount: 0, page: 1, pageSize: 8 });
              }),
            );
          }),
        )
        .subscribe({
          next: (res) => {
            this.pickerLoading = false;
            this.pickerResults = res.items;
          },
          error: () => (this.pickerLoading = false),
        }),
    );

    inject(ActivatedRoute)
      .queryParamMap.pipe(takeUntilDestroyed(inject(DestroyRef)))
      .subscribe((params) => this.load(params.get('tag'), params.get('change')));
  }

  ngOnDestroy(): void {
    this.subs.forEach((s) => s.unsubscribe());
    clearTimeout(this.toastTimer);
  }

  // ======================= Loading =======================

  private requestedTag: string | null = null;
  private requestedChange: string | null = null;

  private load(tag: string | null, changeId: string | null): void {
    this.requestedTag = tag;
    this.requestedChange = changeId;
    // Saving a new draft adds ?change= to the URL; keep the page as it is.
    if (changeId && this.change?.id === changeId) return;

    this.loading = true;
    this.loadError = '';
    this.submitted = null;
    this.errors = {};
    this.formError = '';
    this.step = 1;
    this.change = null;
    this.asset = null;
    this.form = emptyForm(this.userName);

    // People for the Assessed By and approver pickers (users + technicians); free text still works without them.
    this.subs.push(
      this.api.people().subscribe((p) => {
        this.approverChoices = p.users;
        this.assessorChoices = [...new Set([...p.users, ...p.technicians])].sort((a, b) => a.localeCompare(b));
      }),
    );

    // Options (statuses, conditions, reasons, methods, sources...) come from the API only.
    this.lookups = EMPTY_LOOKUPS;
    this.refreshChoices();
    this.subs.push(
      this.api.options().subscribe({
        next: (options) => {
          this.options = { ...EMPTY_OPTIONS, ...options };
          this.refreshChoices();
        },
        error: (err) => (this.loadError = apiErrorMessage(err, 'Could not load the status and condition options.')),
      }),
    );

    if (changeId) {
      this.subs.push(
        this.api
          .change(changeId)
          .pipe(switchMap((change) => {
            this.change = change;
            this.fillForm(change);
            this.refreshChoices();
            return this.api.asset(change.assetRegistrationId);
          }))
          .subscribe({ next: (a) => this.setAsset(a), error: (err) => this.fail(err) }),
      );
    } else if (tag) {
      this.subs.push(this.api.asset(tag).subscribe({ next: (a) => this.setAsset(a), error: (err) => this.fail(err) }));
    } else {
      this.loading = false;
      this.searchAssets('');
    }
  }

  /** The form stays on screen; the error is shown above it and Retry reloads. */
  private fail(err: unknown): void {
    this.loading = false;
    this.loadError = apiErrorMessage(err, 'Could not load the asset.');
  }

  private setAsset(asset: StatusConditionAsset): void {
    this.loading = false;
    this.asset = asset;
    delete this.errors.asset;
    // Start from the current values: the user changes the status, the condition, or both.
    if (!this.form.newStatus) this.form.newStatus = asset.status ?? '';
    if (!this.form.newCondition) this.form.newCondition = asset.condition ?? '';
    this.refreshChoices();
    // Work orders, inspections and failure reasons for the dropdowns; the form still works without them.
    this.subs.push(
      this.api.lookups(asset.assetRegistrationId).subscribe({
        next: (lookups) => {
          this.lookups = lookups;
          this.refreshChoices();
        },
        error: () => undefined,
      }),
    );
    if (this.change && this.change.state === 'Rejected') {
      this.showToast(`${this.change.changeNumber} was rejected. Revise it and submit again.`);
    }
  }

  private fillForm(c: StatusConditionChange): void {
    const [effectiveDate, effectiveTime] = splitLocal(c.statusEffectiveFrom);
    const [assessmentDate, assessmentTime] = splitLocal(c.conditionAssessmentDate);
    const base = emptyForm(this.userName);
    this.form = {
      newStatus: c.newStatus || '',
      effectiveDate: effectiveDate || base.effectiveDate,
      effectiveTime: effectiveTime || base.effectiveTime,
      newCondition: c.newCondition || '',
      assessmentDate: assessmentDate || base.assessmentDate,
      assessmentTime: assessmentTime || base.assessmentTime,
      reasonCode: c.reasonCode ?? '',
      assessmentMethod: c.assessmentMethod ?? '',
      assessmentSource: c.assessmentSource ?? '',
      assessedBy: c.assessedBy ?? base.assessedBy,
      nextAssessmentDue: splitLocal(c.nextAssessmentDue)[0],
      remarks: c.remarks ?? '',
      operationalImpact: c.operationalImpact ?? '',
      relatedWorkOrder: c.relatedWorkOrder ?? '',
      relatedInspection: c.relatedInspection ?? '',
      downtimeEstimate: c.downtimeEstimate,
      downtimeUnit: c.downtimeUnit || 'Hours',
      evidence: c.evidence.map((e) => ({ ...e })),
      approvers: this.levels.map((l) => c.approvals.find((a) => a.level === l.level)?.approver ?? ''),
    };
  }

  // ======================= Related dropdown data =======================

  /** Rebuilds the Reason, Method and Impact choices for the chosen status and source. */
  refreshChoices(): void {
    const f = this.form;
    const o = this.options;
    this.reasonChoices = uniqueWith(o.reasonsByStatus?.[f.newStatus] ?? o.reasons, '');
    this.failureReasonChoices = uniqueWith(
      this.lookups.failureReasons.map((r) => r.name).filter((n) => !this.reasonChoices.includes(n)),
      '',
    );
    if (f.reasonCode && !this.reasonChoices.includes(f.reasonCode) && !this.failureReasonChoices.includes(f.reasonCode)) {
      this.reasonChoices = uniqueWith(this.reasonChoices, f.reasonCode);
    }

    this.methodChoices = uniqueWith(o.methodsBySource?.[f.assessmentSource] ?? o.assessmentMethods, '');
    this.inspectionMethodChoices = uniqueWith(
      this.lookups.inspections.filter((i) => i.kind === 'Inspection Type').map((i) => i.name).filter((n) => !this.methodChoices.includes(n)),
      '',
    );
    if (f.assessmentMethod && !this.methodChoices.includes(f.assessmentMethod) && !this.inspectionMethodChoices.includes(f.assessmentMethod)) {
      this.methodChoices = uniqueWith(this.methodChoices, f.assessmentMethod);
    }

    this.impactChoices = uniqueWith(o.impactsByStatus?.[f.newStatus] ?? o.operationalImpacts, f.operationalImpact);
  }

  /** New status picked: narrow Reason and Impact, clearing a choice that no longer fits. */
  onStatusChange(): void {
    this.clearError('newStatus');
    const o = this.options;
    const reasons = o.reasonsByStatus?.[this.form.newStatus];
    const failureNames = this.lookups.failureReasons.map((r) => r.name);
    if (reasons && this.form.reasonCode && !reasons.includes(this.form.reasonCode) && !failureNames.includes(this.form.reasonCode)) {
      this.form.reasonCode = '';
    }
    const impacts = o.impactsByStatus?.[this.form.newStatus];
    if (impacts && this.form.operationalImpact && !impacts.includes(this.form.operationalImpact)) {
      this.form.operationalImpact = '';
    }
    if (!this.form.operationalImpact && impacts?.length) this.form.operationalImpact = impacts[0];
    this.refreshChoices();
  }

  /** Source picked: narrow Method, clearing a choice that no longer fits. */
  onSourceChange(): void {
    this.clearError('assessmentSource');
    const methods = this.options.methodsBySource?.[this.form.assessmentSource];
    const inspectionNames = this.lookups.inspections.map((i) => i.name);
    if (methods && this.form.assessmentMethod && !methods.includes(this.form.assessmentMethod) && !inspectionNames.includes(this.form.assessmentMethod)) {
      this.form.assessmentMethod = '';
    }
    this.refreshChoices();
  }

  /** "WO-045872 · Replace bearing · Open" label for the work order list. */
  workOrderLabel(w: StatusConditionLookups['workOrders'][number]): string {
    return [w.title, w.workType, w.status, w.scheduledDate ? formatDate(w.scheduledDate) : ''].filter(Boolean).join(' · ');
  }

  retry(): void {
    this.load(this.requestedTag, this.requestedChange);
  }

  // ======================= Asset picker =======================

  searchAssets(q: string): void {
    this.pickerSearch$.next(q);
  }

  pickAsset(a: StatusConditionAsset): void {
    this.router.navigate([], { queryParams: { tag: a.assetTag || a.assetRegistrationId }, replaceUrl: true });
  }

  changeAsset(): void {
    if (this.change) return;
    this.router.navigate([], { queryParams: {}, replaceUrl: true });
  }

  // ======================= Derived state =======================

  get levels() {
    return this.options.approvalLevels?.length ? this.options.approvalLevels : DEFAULT_LEVELS;
  }

  get statusChanged(): boolean {
    return !!this.form.newStatus && this.form.newStatus !== (this.asset?.status ?? '');
  }

  get conditionChanged(): boolean {
    return !!this.form.newCondition && this.form.newCondition !== (this.asset?.condition ?? '');
  }

  get needsApproval(): boolean {
    return this.statusChanged || this.conditionChanged;
  }

  /** Another change of this asset is waiting for approval. */
  get blockedByPending(): boolean {
    return !!this.asset?.pendingChangeId && this.asset.pendingChangeId !== this.change?.id;
  }

  get remarksLength(): number {
    return this.form.remarks.length;
  }

  get healthScore(): number | null {
    return this.asset?.healthScore ?? null;
  }

  get healthTone(): string {
    const h = this.healthScore ?? 0;
    return h >= 80 ? 'meter-fill--good' : h >= 60 ? 'meter-fill--warn' : h >= 40 ? 'meter-fill--serious' : 'meter-fill--critical';
  }

  /** Approval rows: the saved ones of the change, else the levels a submit would create. */
  get approvalRows(): Approval[] {
    if (this.change && this.change.approvals.length && this.change.state !== 'Draft') return this.change.approvals;
    return this.levels.map((l, i) => ({
      level: l.level,
      role: l.role,
      approver: this.form.approvers[i] || null,
      status: 'Not Started',
      actedBy: null,
      actedAt: null,
      comments: null,
    }));
  }

  /**
   * Required fields for this change: status and condition are independent, so the status dates are only needed when
   * the status changes and the assessment fields only when the condition is reassessed.
   */
  private get requiredFields(): [keyof ChangeForm, string][] {
    const statusOnly: (keyof ChangeForm)[] = ['effectiveDate', 'effectiveTime'];
    const conditionOnly: (keyof ChangeForm)[] = ['assessmentDate', 'assessmentTime', 'assessedBy'];
    return REQUIRED.filter(([key]) =>
      statusOnly.includes(key) ? this.statusChanged : conditionOnly.includes(key) ? this.conditionChanged : true,
    );
  }

  get missingFields(): string[] {
    return this.requiredFields.filter(([key]) => !`${this.form[key] ?? ''}`.trim()).map(([, label]) => label);
  }

  get checks(): ValidationCheck[] {
    const a = this.asset;
    if (!a) return [{ state: 'warn', text: 'Select an asset to update.' }];
    const list: ValidationCheck[] = [];

    if (a.status === 'Disposed') list.push({ state: 'error', text: 'Asset is disposed and cannot be updated.' });
    else if (this.blockedByPending) list.push({ state: 'error', text: `Change ${a.pendingChangeNumber} is already waiting for approval.` });
    else list.push({ state: 'ok', text: `Asset is ${a.status ? a.status.toLowerCase() : 'registered'} and eligible for update.` });

    const missing = this.missingFields;
    list.push(missing.length
      ? { state: 'warn', text: `${missing.length} mandatory field${missing.length === 1 ? '' : 's'} still empty.` }
      : { state: 'ok', text: 'All mandatory fields are completed.' });

    if (!this.form.newStatus) list.push({ state: 'warn', text: 'Choose the new operational status.' });
    else if (this.statusChanged) list.push({ state: 'ok', text: `New status transition is allowed (${a.status || 'None'} → ${this.form.newStatus}).` });
    else list.push({ state: 'ok', text: `Operational status stays ${this.form.newStatus}.` });

    if (this.form.newCondition && this.form.newCondition !== a.condition && ['Poor', 'Critical'].includes(this.form.newCondition) && this.form.newStatus === 'Active') {
      list.push({ state: 'warn', text: `Asset stays Active with ${this.form.newCondition} condition. Consider Under Maintenance.` });
    }

    list.push(!this.needsApproval && (!this.form.newStatus || !this.form.newCondition)
      ? { state: 'warn', text: 'Approval is needed when the status or condition changes.' }
      : this.needsApproval
      ? { state: 'ok', text: `Status / condition change requires approval (${this.levels.length} levels).` }
      : { state: 'ok', text: 'Re-assessment only: applied without approval.' });

    if (this.form.newStatus === 'Under Maintenance' || this.form.newStatus === 'Out of Service') {
      list.push(this.form.relatedWorkOrder.trim()
        ? { state: 'ok', text: 'Related work order is linked.' }
        : { state: 'warn', text: 'No related work order linked.' });
    }

    const assessed = joinLocal(this.form.assessmentDate, this.form.assessmentTime);
    if (assessed && new Date(assessed).getTime() > Date.now() + 60_000) {
      list.push({ state: 'error', text: 'Condition assessment date is in the future.' });
    }
    if (this.form.nextAssessmentDue && this.form.assessmentDate && this.form.nextAssessmentDue <= this.form.assessmentDate) {
      list.push({ state: 'error', text: 'Next assessment must be after the assessment date.' });
    }
    return list;
  }

  /** A submitted or approved change is shown but cannot be edited here. */
  get readOnly(): boolean {
    return !!this.change && (this.change.state === 'Pending Approval' || this.change.state === 'Approved');
  }

  get canSubmit(): boolean {
    return !this.readOnly && !!this.asset && !this.saving && !this.uploading && !this.checks.some((c) => c.state === 'error');
  }

  // ======================= Steps =======================

  /** Tick on a step tab: only Update Details, once its required fields are filled; never the open step. */
  stepDone(n: Step): boolean {
    if (n === this.step) return false;
    if (n === 1) return !!this.asset && !this.missingFields.length;
    // Evidence & Links is optional and Review & Submit is the last step: both always show their number.
    return false;
  }

  /** Steps can be opened in any order; the fields are checked on Submit. */
  goTo(step: Step): void {
    this.step = step;
    this.formError = '';
    if (typeof window !== 'undefined') window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  next(): void {
    if (this.step < 3) this.goTo((this.step + 1) as Step);
  }

  back(): void {
    if (this.step > 1) this.goTo((this.step - 1) as Step);
  }

  /** Checks step 1; fills `errors` and returns whether it is complete. */
  private validateDetails(): boolean {
    const errors: typeof this.errors = {};
    if (!this.asset) errors.asset = 'Select an asset.';
    for (const [key, label] of this.requiredFields) {
      if (!`${this.form[key] ?? ''}`.trim()) errors[key] = `${label} is required.`;
    }
    if (this.asset && !this.statusChanged && !this.conditionChanged) errors.newStatus = 'Change the operational status, the condition, or both.';
    if (this.form.remarks.length > REMARKS_MAX) errors.remarks = `Remarks cannot be longer than ${REMARKS_MAX} characters.`;
    const assessed = joinLocal(this.form.assessmentDate, this.form.assessmentTime);
    if (assessed && new Date(assessed).getTime() > Date.now() + 60_000) errors.assessmentDate = 'Assessment date cannot be in the future.';
    if (this.form.nextAssessmentDue && this.form.assessmentDate && this.form.nextAssessmentDue <= this.form.assessmentDate) {
      errors.nextAssessmentDue = 'Must be after the assessment date.';
    }
    if (this.form.downtimeEstimate != null && (this.form.downtimeEstimate < 0 || this.form.downtimeEstimate > 100000)) {
      errors.downtimeEstimate = 'Enter 0 to 100000.';
    }
    this.errors = errors;
    if (Object.keys(errors).length) {
      this.formError = 'Complete the highlighted fields.';
      return false;
    }
    this.formError = '';
    return true;
  }

  clearError(key: keyof ChangeForm): void {
    if (this.errors[key]) delete this.errors[key];
  }

  // ======================= Evidence =======================

  onFiles(event: Event): void {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    input.value = '';
    if (!files.length) return;
    const tooBig = files.find((f) => f.size > 20 * 1024 * 1024);
    if (tooBig) {
      this.errors = { ...this.errors, evidence: `${tooBig.name} is larger than 20 MB.` };
      return;
    }
    this.uploading = true;
    delete this.errors.evidence;
    this.subs.push(
      forkJoin(files.map((f) => this.api.upload(f))).subscribe({
        next: (urls) => {
          this.uploading = false;
          urls.forEach((url, i) => this.form.evidence.push({ name: files[i].name, url, kind: 'File', sizeBytes: files[i].size }));
        },
        error: (err) => {
          this.uploading = false;
          this.errors = { ...this.errors, evidence: apiErrorMessage(err, 'Upload failed.') };
        },
      }),
    );
  }

  addLink(): void {
    const url = this.linkUrl.trim();
    if (!/^https?:\/\/\S+$/i.test(url)) {
      this.errors = { ...this.errors, evidence: 'Enter a full link, e.g. https://…' };
      return;
    }
    delete this.errors.evidence;
    this.form.evidence.push({ name: this.linkName.trim() || url, url, kind: 'Link', sizeBytes: null });
    this.linkName = '';
    this.linkUrl = '';
  }

  removeEvidence(i: number): void {
    this.form.evidence.splice(i, 1);
  }

  sizeLabel(bytes: number | null): string {
    if (!bytes) return '';
    return bytes > 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
  }

  // ======================= Save / submit =======================

  private request(submit: boolean): SaveChangeRequest {
    const f = this.form;
    const text = (v: string) => (v.trim() ? v.trim() : null);
    return {
      assetRegistrationId: this.asset!.assetRegistrationId,
      newStatus: text(f.newStatus),
      statusEffectiveFrom: joinLocal(f.effectiveDate, f.effectiveTime),
      newCondition: text(f.newCondition),
      conditionAssessmentDate: joinLocal(f.assessmentDate, f.assessmentTime),
      reasonCode: text(f.reasonCode),
      assessmentMethod: text(f.assessmentMethod),
      assessmentSource: text(f.assessmentSource),
      assessedBy: text(f.assessedBy),
      nextAssessmentDue: f.nextAssessmentDue || null,
      remarks: text(f.remarks),
      operationalImpact: text(f.operationalImpact),
      relatedWorkOrder: text(f.relatedWorkOrder),
      relatedInspection: text(f.relatedInspection),
      downtimeEstimate: f.downtimeEstimate == null || `${f.downtimeEstimate}` === '' ? null : Number(f.downtimeEstimate),
      downtimeUnit: f.downtimeEstimate == null ? null : f.downtimeUnit,
      evidence: f.evidence,
      approvers: f.approvers.map((a) => a.trim() || null),
      submit,
    };
  }

  saveDraft(): void {
    if (this.readOnly) return;
    if (!this.asset) {
      this.errors = { asset: 'Select an asset.' };
      return;
    }
    this.persist(false);
  }

  submit(): void {
    if (!this.validateDetails()) {
      this.step = 1;
      return;
    }
    if (!this.canSubmit) {
      this.formError = this.checks.find((c) => c.state === 'error')?.text ?? 'This change cannot be submitted.';
      return;
    }
    this.persist(true);
  }

  private persist(submit: boolean): void {
    this.saving = true;
    this.formError = '';
    this.subs.push(
      this.api.save(this.change?.id ?? null, this.request(submit)).subscribe({
        next: (saved) => {
          this.saving = false;
          this.change = saved;
          if (submit) {
            this.submitted = saved;
            return;
          }
          this.router.navigate([], { queryParams: { change: saved.id }, replaceUrl: true });
          this.showToast(`Draft ${saved.changeNumber} saved.`);
        },
        error: (err) => {
          this.saving = false;
          this.formError = apiErrorMessage(err, submit ? 'Could not submit the change.' : 'Could not save the draft.');
        },
      }),
    );
  }

  cancel(): void {
    this.router.navigate(['/assets/asset-status-condition']);
  }

  newUpdate(): void {
    this.router.navigate(['/assets/asset-status-condition/update']);
  }

  trackLevel = (_: number, a: Approval) => a.level;
  trackCheck = (_: number, c: ValidationCheck) => c.text;

  showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 4000);
  }
}
