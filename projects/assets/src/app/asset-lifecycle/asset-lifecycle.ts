import { Component, DestroyRef, HostListener, OnDestroy, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { AssetLifecycleService, LifecycleLoad, LifecycleRecordDto } from './asset-lifecycle.service';
import { isoDay } from '../asset-maintenance/asset-lookup.service';
import { apiErrorMessage } from '../services/asset-api';
import {
  LucideAngularModule,
  ArrowRight,
  Archive,
  CalendarDays,
  Check,
  ChevronDown,
  CircleDollarSign,
  ClipboardList,
  Clock,
  Coins,
  Download,
  Hourglass,
  LayoutGrid,
  Pencil,
  Play,
  Plus,
  Printer,
  RefreshCw,
  Search,
  ShieldCheck,
  SkipForward,
  Stethoscope,
  ChartColumn,
  Trash2,
  Wrench,
  X,
  HeartPulse,
} from 'lucide-angular';

type StageKey =
  | 'planning'
  | 'procurement'
  | 'received'
  | 'installation'
  | 'commissioning'
  | 'operation'
  | 'maintenance'
  | 'upgrade'
  | 'decommissioning'
  | 'disposal';
type StageStatus = 'Completed' | 'Current' | 'Planned' | 'Not Started';
type EventKind = 'Procurement' | 'Installation' | 'Operation' | 'Maintenance' | 'Repair' | 'Condition' | 'Stage' | 'Other';
type Condition = 'Excellent' | 'Good' | 'Fair' | 'Poor';
type LifecycleTab =
  | 'overview'
  | 'stages'
  | 'procurement'
  | 'installation'
  | 'operational'
  | 'maintenance'
  | 'warranty'
  | 'upgrade'
  | 'decommissioning'
  | 'disposal'
  | 'financial'
  | 'history';
type ModalKind = 'info' | 'dates' | 'status' | 'condition' | 'event' | 'stage' | 'advance';

interface Stage {
  key: StageKey;
  label: string;
  status: StageStatus;
  /** ISO date (yyyy-mm-dd); empty while the stage hasn't happened. */
  date: string;
  owner: string;
  notes: string;
}

interface LifecycleEvent {
  id: number;
  date: string;
  title: string;
  by: string;
  kind: EventKind;
}

interface ConditionPoint {
  date: string;
  condition: Condition;
  notes: string;
}

interface AssetInfo {
  tag: string;
  name: string;
  category: string;
  typeModel: string;
  serial: string;
  location: string;
  custodian: string;
  criticality: 'Critical' | 'High' | 'Medium' | 'Low';
}

interface KeyDates {
  purchase: string;
  installation: string;
  operationalSince: string;
  expectedUpgrade: string;
  expectedDisposal: string;
}

interface StatusInfo {
  operational: 'Active' | 'Under Maintenance' | 'Standby' | 'Out of Service';
  condition: Condition;
  health: number;
  utilization: number;
  availability: number;
  lastMaintenance: string;
  nextMaintenance: string;
  lastAssessment: string;
  nextAssessment: string;
}

const CONDITIONS: Condition[] = ['Excellent', 'Good', 'Fair', 'Poor'];
const CONDITION_LEVEL: Record<Condition, number> = { Excellent: 4, Good: 3, Fair: 2, Poor: 1 };
const EVENT_KINDS: EventKind[] = ['Procurement', 'Installation', 'Operation', 'Maintenance', 'Repair', 'Condition', 'Stage', 'Other'];
/** Stage(s) and event kinds each stage-focused tab shows. */
const TAB_SCOPE: Partial<Record<LifecycleTab, { stages: StageKey[]; kinds: EventKind[] }>> = {
  procurement: { stages: ['planning', 'procurement', 'received'], kinds: ['Procurement'] },
  installation: { stages: ['installation', 'commissioning'], kinds: ['Installation'] },
  operational: { stages: ['operation'], kinds: ['Operation', 'Condition'] },
  maintenance: { stages: ['maintenance'], kinds: ['Maintenance', 'Repair'] },
  upgrade: { stages: ['upgrade'], kinds: [] },
  decommissioning: { stages: ['decommissioning'], kinds: [] },
  disposal: { stages: ['disposal'], kinds: [] },
};

function parseDate(iso: string): Date {
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(y, m - 1, d);
}

function toIso(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function startOfToday(): Date {
  const t = new Date();
  return new Date(t.getFullYear(), t.getMonth(), t.getDate());
}

/** Whole calendar months from a to b (negative when b is earlier). */
function monthsBetween(a: Date, b: Date): number {
  return (b.getFullYear() - a.getFullYear()) * 12 + (b.getMonth() - a.getMonth()) - (b.getDate() < a.getDate() ? 1 : 0);
}

function daysBetween(a: Date, b: Date): number {
  return Math.round((b.getTime() - a.getTime()) / 86_400_000);
}

function yearsMonths(months: number): string {
  const y = Math.floor(months / 12);
  const m = months % 12;
  const parts = [];
  if (y) parts.push(`${y} Year${y === 1 ? '' : 's'}`);
  if (m || !y) parts.push(`${m} Month${m === 1 ? '' : 's'}`);
  return parts.join(' ');
}

function addMonths(iso: string, months: number): string {
  const d = parseDate(iso);
  d.setMonth(d.getMonth() + months);
  return toIso(d);
}

@Component({
  standalone: true,
  selector: 'app-asset-lifecycle',
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './asset-lifecycle.html',
  styleUrls: ['./asset-lifecycle.css'],
})
export class AssetLifecycle implements OnDestroy {
  readonly icons = {
    ArrowRight, Archive, CalendarDays, Check, ChevronDown, CircleDollarSign, ClipboardList, Clock, Coins, Download, Hourglass,
    LayoutGrid, Pencil, Play, Plus, Printer, RefreshCw, Search, ShieldCheck, SkipForward, Stethoscope, ChartColumn, Trash2, Wrench, X,
    HeartPulse,
  };
  readonly stageIcons: Partial<Record<StageKey, typeof Wrench>> = {
    maintenance: Wrench,
    upgrade: RefreshCw,
    decommissioning: Archive,
    disposal: Trash2,
  };

  readonly tabs: { key: LifecycleTab; label: string }[] = [
    { key: 'overview', label: 'Overview' },
    { key: 'stages', label: 'Lifecycle Stages' },
    { key: 'procurement', label: 'Procurement' },
    { key: 'installation', label: 'Installation & Commissioning' },
    { key: 'operational', label: 'Operational' },
    { key: 'maintenance', label: 'Maintenance' },
    { key: 'warranty', label: 'Warranty & Contract' },
    { key: 'upgrade', label: 'Upgrade / Refurbishment' },
    { key: 'decommissioning', label: 'Decommissioning' },
    { key: 'disposal', label: 'Disposal' },
    { key: 'financial', label: 'Financial' },
    { key: 'history', label: 'History' },
  ];
  readonly conditions = CONDITIONS;
  readonly eventKinds = EVENT_KINDS;
  readonly stageStatuses: StageStatus[] = ['Completed', 'Current', 'Planned', 'Not Started'];
  readonly operationalStatuses: StatusInfo['operational'][] = ['Active', 'Under Maintenance', 'Standby', 'Out of Service'];
  readonly criticalities: AssetInfo['criticality'][] = ['Critical', 'High', 'Medium', 'Low'];
  image = 'asset-generator.svg';

  asset: AssetInfo = { tag: '', name: '', category: '', typeModel: '', serial: '', location: '', custodian: '', criticality: 'Medium' };

  dates: KeyDates = { purchase: '', installation: '', operationalSince: '', expectedUpgrade: '', expectedDisposal: '' };

  status: StatusInfo = {
    operational: 'Active',
    condition: 'Good',
    health: 0,
    utilization: 0,
    availability: 0,
    lastMaintenance: '',
    nextMaintenance: '',
    lastAssessment: '',
    nextAssessment: '',
  };

  /** Stage skeleton; dates, owners and statuses come from the asset's records once loaded. */
  stages: Stage[] = [
    { key: 'planning', label: 'Planning', status: 'Not Started', date: '', owner: '', notes: '' },
    { key: 'procurement', label: 'Procurement', status: 'Not Started', date: '', owner: '', notes: '' },
    { key: 'received', label: 'Received', status: 'Not Started', date: '', owner: '', notes: '' },
    { key: 'installation', label: 'Installation', status: 'Not Started', date: '', owner: '', notes: '' },
    { key: 'commissioning', label: 'Commissioning', status: 'Not Started', date: '', owner: '', notes: '' },
    { key: 'operation', label: 'In Operation', status: 'Not Started', date: '', owner: '', notes: '' },
    { key: 'maintenance', label: 'Maintenance', status: 'Not Started', date: '', owner: '', notes: '' },
    { key: 'upgrade', label: 'Upgrade', status: 'Not Started', date: '', owner: '', notes: '' },
    { key: 'decommissioning', label: 'Decommissioning', status: 'Not Started', date: '', owner: '', notes: '' },
    { key: 'disposal', label: 'Disposal', status: 'Not Started', date: '', owner: '', notes: '' },
  ];

  events: LifecycleEvent[] = [];
  private nextEventId = 1;

  conditionHistory: ConditionPoint[] = [];

  financial = { acquisition: 0, operating: 0, maintenance: 0, upgrade: 0 };
  warranty = { end: '', provider: '', amcEnd: '', amcProvider: '' };
  maintenanceSummary = { workOrders: 0, openWorkOrders: 0, breakdowns: 0, downtimeHours: 0 };

  activeTab: LifecycleTab = 'overview';
  actionsOpen = false;
  historySearch = '';
  historyKind: '' | EventKind = '';

  modal: ModalKind | null = null;
  submitted = false;
  infoDraft: AssetInfo = { ...this.asset };
  datesDraft: KeyDates = { ...this.dates };
  statusDraft: StatusInfo = { ...this.status };
  conditionDraft: ConditionPoint = { date: '', condition: 'Good', notes: '' };
  eventDraft: Omit<LifecycleEvent, 'id'> = { date: '', title: '', by: '', kind: 'Other' };
  stageDraft: Stage | null = null;
  private stageTarget: Stage | null = null;

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  private readonly today = startOfToday();
  readonly todayIso = toIso(this.today);

  // ===== API =====

  /** Asset registration id in the Asset API; stage changes and events are saved against it. */
  registrationId = '';
  loading = false;
  loadError = '';
  saving = false;
  /** False when /api/asset-lifecycle is not available (stage changes then stay on this page). */
  private lifecycleApi = false;
  private loadSub?: Subscription;
  private readonly api = inject(AssetLifecycleService);

  constructor() {
    // ?tag=<asset tag> opens an asset's lifecycle
    inject(ActivatedRoute)
      .queryParamMap.pipe(takeUntilDestroyed(inject(DestroyRef)))
      .subscribe((p) => {
        const tag = p.get('tag') || this.asset.tag;
        if (tag && (tag !== this.registrationTag || !this.loadSub)) this.load(tag);
      });
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
    this.loadSub?.unsubscribe();
  }

  /** Tag the page was opened with (?tag=). */
  registrationTag = '';

  /** Reads the asset's registration and records and works out the lifecycle from them. */
  load(tag = this.asset.tag): void {
    this.registrationTag = tag;
    this.loadSub?.unsubscribe();
    this.loading = true;
    this.loadError = '';
    this.loadSub = this.api.load(tag).subscribe({
      next: (l) => {
        this.loading = false;
        this.apply(l);
        if (l.errors.length) this.loadError = `Some records could not be loaded. ${l.errors.join(' · ')}`;
      },
      error: (err) => {
        this.loading = false;
        this.loadError = err instanceof Error && !('status' in err) ? err.message : apiErrorMessage(err, 'The asset lifecycle could not be loaded.');
      },
    });
  }

  private apply(l: LifecycleLoad): void {
    const reg = l.registration;
    const basic = reg.basicInformation;
    const cls = reg.classification;
    const loc = reg.locationAssignment?.location;
    const asg = reg.locationAssignment?.assignment;
    const purchase = reg.purchaseWarranty?.purchase;
    const warranty = reg.purchaseWarranty?.warranty;
    const tech = reg.additionalInformation?.technical;
    const ops = reg.additionalInformation?.operational;
    this.registrationId = reg.assetRegistrationId;
    this.lifecycleApi = !!l.lifecycle;
    if (basic?.images?.[0] || l.item.imageUrl) this.image = basic?.images?.[0] || l.item.imageUrl || this.image;

    const crit = (basic?.criticality || cls?.criticality || 'Medium') as AssetInfo['criticality'];
    this.asset = {
      tag: basic?.assetTag || l.item.assetTag || this.registrationTag,
      name: basic?.assetName || l.item.assetName || '',
      category: cls?.category || l.item.category || '',
      typeModel: [basic?.manufacturer || purchase?.manufacturer, basic?.model].filter(Boolean).join(' ') || cls?.assetType || '',
      serial: basic?.serialNumber || '',
      location: loc?.locationPath || [loc?.siteName, loc?.buildingName, loc?.roomName].filter(Boolean).join(' - ') || '',
      custodian: asg?.assignedToName || asg?.custodianDepartment || asg?.responsibleTeam || l.item.custodianName || '',
      criticality: this.criticalities.includes(crit) ? crit : 'Medium',
    };

    // ----- Key dates -----
    const assignments = [...l.assignments].sort((a, b) => (a.fromDate ?? '').localeCompare(b.fromDate ?? ''));
    const purchased = isoDay(purchase?.purchaseDate) || isoDay(reg.createdAt) || this.todayIso;
    const received = isoDay(purchase?.deliveryDate);
    const installed = isoDay(asg?.assignmentStartDate) || isoDay(assignments[0]?.fromDate) || received || purchased;
    const lifeMonths = tech?.expectedLifespan ? tech.expectedLifespan * (/month/i.test(tech.expectedLifespanUnit ?? '') ? 1 : 12) : 120;
    this.dates = {
      purchase: purchased,
      installation: installed < purchased ? purchased : installed,
      operationalSince: installed < purchased ? purchased : installed,
      expectedUpgrade: addMonths(installed, Math.round(lifeMonths / 2)),
      expectedDisposal: addMonths(installed, lifeMonths),
    };

    // ----- Work orders -----
    const wos = l.workOrders.map((w) => ({
      id: w.workOrderId || w.id, type: w.workType || 'Corrective', title: w.title, status: (w.status ?? 'Open').toLowerCase(),
      date: isoDay(w.actualEndDate ?? w.scheduledDate ?? w.createdAt), cost: (w.labourCost ?? 0) + (w.partsCost ?? 0) + (w.additionalCost ?? 0),
    }));
    const done = wos.filter((w) => w.status === 'completed' || w.status === 'closed').sort((a, b) => a.date.localeCompare(b.date));
    const open = wos.filter((w) => !['completed', 'closed', 'cancelled'].includes(w.status)).sort((a, b) => a.date.localeCompare(b.date));

    // ----- Condition -----
    const toCondition = (c: string | null | undefined): Condition => {
      const v = (c ?? '').toLowerCase();
      return v === 'excellent' ? 'Excellent' : v === 'fair' ? 'Fair' : v === 'poor' || v === 'critical' ? 'Poor' : 'Good';
    };
    const assessments = [...l.assessments].sort((a, b) => a.assessmentDate.localeCompare(b.assessmentDate));
    this.conditionHistory = assessments.map((a) => ({ date: isoDay(a.assessmentDate), condition: toCondition(a.overallCondition), notes: a.remarks ?? '' }));
    const latest = assessments[assessments.length - 1];
    const regStatus = (basic?.status || l.item.status || 'Active').toLowerCase();
    const utilization = parseFloat(ops?.utilization ?? '');
    this.status = {
      ...this.status,
      operational: regStatus.includes('maint') || regStatus.includes('repair') ? 'Under Maintenance' : regStatus.includes('storage') ? 'Standby' : regStatus.includes('dispos') ? 'Out of Service' : 'Active',
      condition: latest ? toCondition(latest.overallCondition) : toCondition(basic?.condition),
      health: latest ? Math.round(Number(latest.healthScore)) : this.status.health,
      utilization: Number.isFinite(utilization) ? Math.round(utilization) : this.status.utilization,
      lastMaintenance: done[done.length - 1]?.date || isoDay(ops?.lastMaintenanceDate),
      nextMaintenance: isoDay(ops?.nextMaintenanceDate) || open[0]?.date || '',
      lastAssessment: latest ? isoDay(latest.assessmentDate) : '',
      nextAssessment: latest ? addMonths(isoDay(latest.assessmentDate), 6) : '',
    };

    // ----- Warranty, money, maintenance -----
    const amc = [...l.amc].sort((a, b) => (b.endDate ?? '').localeCompare(a.endDate ?? ''))[0];
    this.warranty = {
      end: isoDay(warranty?.warrantyEndDate),
      provider: warranty?.serviceProvider || purchase?.supplierVendor || '-',
      amcEnd: isoDay(amc?.endDate),
      amcProvider: amc?.vendorName || (warranty?.amcIncluded ? warranty.serviceProvider || '-' : '-'),
    };
    this.financial = { acquisition: Number(purchase?.totalCost ?? purchase?.purchaseCost ?? 0), operating: 0, maintenance: wos.reduce((t, w) => t + w.cost, 0), upgrade: 0 };
    this.maintenanceSummary = {
      workOrders: wos.length,
      openWorkOrders: open.length,
      breakdowns: wos.filter((w) => /corrective|breakdown/i.test(w.type)).length,
      downtimeHours: this.maintenanceSummary.downtimeHours,
    };

    // ----- Stages (worked out from the records, then the stored stage changes on top) -----
    const disposal = l.disposals.filter((d) => !/reject|cancel/i.test(d.status ?? '')).sort((a, b) => (b.requestDate ?? '').localeCompare(a.requestDate ?? ''))[0];
    const disposed = regStatus.includes('dispos') || /complet|dispos/i.test(disposal?.status ?? '');
    const owner = (v: string | null | undefined, fallback: string) => v || fallback;
    const stages: Stage[] = [
      { key: 'planning', label: 'Planning', status: 'Completed', date: purchased, owner: owner(reg.createdBy, 'Engineering Team'), notes: 'Asset registered.' },
      { key: 'procurement', label: 'Procurement', status: 'Completed', date: purchased, owner: 'Procurement Team', notes: [purchase?.purchaseOrderNumber, purchase?.supplierVendor].filter(Boolean).join(' · ') },
      { key: 'received', label: 'Received', status: 'Completed', date: received || purchased, owner: 'Store Team', notes: '' },
      { key: 'installation', label: 'Installation', status: 'Completed', date: this.dates.installation, owner: owner(asg?.responsibleTeam, 'Installation Team'), notes: this.asset.location ? `Installed at ${this.asset.location}.` : '' },
      { key: 'commissioning', label: 'Commissioning', status: 'Completed', date: this.dates.operationalSince, owner: 'Engineering Team', notes: '' },
      { key: 'operation', label: 'In Operation', status: disposal ? 'Completed' : 'Current', date: this.dates.operationalSince, owner: owner(asg?.custodianDepartment, 'Operations Team'), notes: '' },
      { key: 'maintenance', label: 'Maintenance', status: 'Planned', date: this.status.nextMaintenance, owner: 'Maintenance Team', notes: open.length ? `${open.length} open work order(s).` : '' },
      { key: 'upgrade', label: 'Upgrade', status: 'Planned', date: '', owner: '', notes: '' },
      { key: 'decommissioning', label: 'Decommissioning', status: disposal ? (disposed ? 'Completed' : 'Current') : 'Not Started', date: isoDay(disposal?.requestDate), owner: disposal?.requestedBy ?? '', notes: disposal ? `${disposal.requestNumber} · ${disposal.status ?? ''}` : '' },
      { key: 'disposal', label: 'Disposal', status: disposed ? 'Completed' : disposal ? 'Planned' : 'Not Started', date: isoDay(disposal?.expectedDisposalDate), owner: '', notes: disposal?.disposalMethod ?? '' },
    ];
    if (disposed) stages.find((s) => s.key === 'decommissioning')!.status = 'Completed';
    this.stages = this.withStored(stages, l.lifecycle);

    // ----- Events -----
    const events: Omit<LifecycleEvent, 'id'>[] = [];
    if (purchase?.purchaseDate) events.push({ date: purchased, title: `Purchased${purchase.purchaseOrderNumber ? ` (${purchase.purchaseOrderNumber})` : ''}`, by: purchase.supplierVendor || 'Procurement Team', kind: 'Procurement' });
    if (received) events.push({ date: received, title: 'Asset received', by: 'Store Team', kind: 'Installation' });
    events.push({ date: isoDay(reg.createdAt) || purchased, title: 'Asset registered', by: reg.createdBy || 'System', kind: 'Other' });
    for (const a of assignments) {
      if (a.fromDate) events.push({ date: isoDay(a.fromDate), title: `Assigned to ${a.assignedToName || a.assignTo} (${a.assignmentNumber})`, by: a.createdBy || '', kind: 'Operation' });
    }
    for (const a of assessments) events.push({ date: isoDay(a.assessmentDate), title: `Condition assessed as ${toCondition(a.overallCondition)}`, by: a.assessedBy, kind: 'Condition' });
    for (const w of done) events.push({ date: w.date, title: `${w.type} maintenance completed (${w.id})`, by: 'Maintenance Team', kind: /corrective|breakdown/i.test(w.type) ? 'Repair' : 'Maintenance' });
    for (const r of l.serviceRequests) {
      if (r.requestedDate) events.push({ date: isoDay(r.requestedDate), title: `Service request ${r.requestNumber}: ${r.title}`, by: r.requestedBy || '', kind: 'Repair' });
    }
    if (disposal?.requestDate) events.push({ date: isoDay(disposal.requestDate), title: `Disposal requested (${disposal.requestNumber})`, by: disposal.requestedBy || '', kind: 'Other' });
    for (const e of l.lifecycle?.events ?? []) events.push({ date: isoDay(e.date), title: e.title, by: e.by ?? '', kind: (EVENT_KINDS.includes(e.kind as EventKind) ? e.kind : 'Other') as EventKind });
    this.events = events.filter((e) => e.date).map((e, i) => ({ id: i + 1, ...e }));
    this.nextEventId = this.events.length + 1;
  }

  /** Stored stage changes override the worked-out stages; only one stays current. */
  private withStored(stages: Stage[], record: LifecycleRecordDto | null): Stage[] {
    const stored = record?.stages ?? [];
    if (stored.some((s) => s.status === 'Current')) stages.forEach((s) => s.status === 'Current' && (s.status = 'Completed'));
    for (const st of stored) {
      const s = stages.find((x) => x.key === st.key);
      if (!s) continue;
      s.status = (this.stageStatuses.includes(st.status as StageStatus) ? st.status : s.status) as StageStatus;
      s.date = isoDay(st.date) || s.date;
      s.owner = st.owner ?? s.owner;
      s.notes = st.notes ?? s.notes;
    }
    return stages;
  }

  /** Saves a stage change; without the lifecycle API (or a registered asset) it stays on this page. */
  private persistStage(target: Stage, d: { status: StageStatus; date: string; owner: string; notes: string }, completePrevious: string | undefined, done: () => void): void {
    if (!this.registrationId || !this.lifecycleApi) {
      done();
      return;
    }
    this.saving = true;
    this.api.saveStage(this.registrationId, target.key, { ...d, completePrevious }).subscribe({
      next: () => {
        this.saving = false;
        done();
      },
      error: (err) => {
        this.saving = false;
        this.showToast(apiErrorMessage(err, `${target.label} stage could not be saved.`));
      },
    });
  }

  // ===== KPIs =====

  get currentStage(): Stage | undefined {
    return this.stages.find((s) => s.status === 'Current');
  }

  private get ageMonths(): number {
    return Math.max(0, monthsBetween(parseDate(this.dates.operationalSince), this.today));
  }

  private get lifeMonths(): number {
    return Math.max(1, monthsBetween(parseDate(this.dates.operationalSince), parseDate(this.dates.expectedDisposal)));
  }

  get assetAge(): string {
    return yearsMonths(this.ageMonths);
  }

  get usefulLife(): string {
    return yearsMonths(this.lifeMonths);
  }

  get remainingLife(): string {
    return yearsMonths(Math.max(0, this.lifeMonths - this.ageMonths));
  }

  get progress(): number {
    return Math.min(100, Math.round((this.ageMonths / this.lifeMonths) * 100));
  }

  /** Stroke offset for the progress ring (r = 18). */
  get progressOffset(): number {
    const c = 2 * Math.PI * 18;
    return c - (c * this.progress) / 100;
  }

  get totalCost(): number {
    const f = this.financial;
    return f.acquisition + f.operating + f.maintenance + f.upgrade;
  }

  readonly costRows: { key: keyof AssetLifecycle['financial']; label: string; color: string }[] = [
    { key: 'acquisition', label: 'Acquisition Cost', color: '#4f2bd8' },
    { key: 'operating', label: 'Operating Cost', color: '#3b82f6' },
    { key: 'maintenance', label: 'Total Maintenance Cost', color: '#0ca30c' },
    { key: 'upgrade', label: 'Upgrade Cost', color: '#fab219' },
  ];

  costShare(key: keyof AssetLifecycle['financial']): number {
    return this.totalCost ? (this.financial[key] / this.totalCost) * 100 : 0;
  }

  // ===== Dates and deadlines =====

  /** "2023-01-26" → "26 Jan 2023"; parsed as a local date so it never shifts a day. */
  fmt(iso: string): string {
    return iso ? parseDate(iso).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }) : '-';
  }

  /** Short axis label, e.g. "Jan 2023". */
  fmtMonth(iso: string): string {
    return parseDate(iso).toLocaleDateString('en-GB', { month: 'short', year: 'numeric' });
  }

  get totalLifecycle(): string {
    return this.usefulLife;
  }

  daysUntil(iso: string): number {
    return daysBetween(this.today, parseDate(iso));
  }

  isPast(iso: string): boolean {
    return !!iso && this.daysUntil(iso) < 0;
  }

  /** "230 Days Left", "1.4 Years Left" or "Expired". */
  timeLeft(iso: string): string {
    if (!iso) return '-';
    const days = this.daysUntil(iso);
    if (days < 0) return 'Expired';
    if (days > 365) return `${(days / 365).toFixed(1)} Years Left`;
    return `${days} Day${days === 1 ? '' : 's'} Left`;
  }

  get contractActive(): boolean {
    return !this.isPast(this.warranty.amcEnd);
  }

  // ===== Stages =====

  stageIndex(s: Stage): number {
    return this.stages.indexOf(s);
  }

  stageBadge(s: Stage): string {
    return s.status === 'Current' ? 'Current Stage' : s.status;
  }

  /** The connector after a stage is solid once the next stage has been reached. */
  connectorDone(i: number): boolean {
    const next = this.stages[i + 1];
    return !!next && (next.status === 'Completed' || next.status === 'Current');
  }

  get nextStage(): Stage | undefined {
    const current = this.currentStage;
    const from = current ? this.stages.indexOf(current) + 1 : 0;
    return this.stages.slice(from).find((s) => s.status !== 'Completed');
  }

  // ===== Events =====

  get sortedEvents(): LifecycleEvent[] {
    return [...this.events].sort((a, b) => a.date.localeCompare(b.date) || a.id - b.id);
  }

  /** Overview shows the seven most recent events, oldest first. */
  get recentEvents(): LifecycleEvent[] {
    return this.sortedEvents.slice(-7);
  }

  get historyRows(): LifecycleEvent[] {
    const q = this.historySearch.trim().toLowerCase();
    return this.sortedEvents
      .filter((e) => (!this.historyKind || e.kind === this.historyKind) && (!q || [e.title, e.by].some((v) => v.toLowerCase().includes(q))))
      .reverse();
  }

  eventsFor(kinds: EventKind[]): LifecycleEvent[] {
    return this.sortedEvents.filter((e) => kinds.includes(e.kind)).reverse();
  }

  kindClass(kind: EventKind): string {
    return 'kind--' + kind.toLowerCase();
  }

  // ===== Tabs =====

  get scope(): { stages: Stage[]; kinds: EventKind[] } | null {
    const s = TAB_SCOPE[this.activeTab];
    return s ? { stages: this.stages.filter((x) => s.stages.includes(x.key)), kinds: s.kinds } : null;
  }

  get activeTabLabel(): string {
    return this.tabs.find((t) => t.key === this.activeTab)!.label;
  }

  openHistory(kind: '' | EventKind = ''): void {
    this.historyKind = kind;
    this.historySearch = '';
    this.activeTab = 'history';
  }

  // ===== Condition chart =====

  readonly chart = { width: 360, height: 130, left: 62, right: 14, top: 12, bottom: 26 };

  private get chartRange(): [number, number] {
    const times = this.conditionHistory.map((p) => parseDate(p.date).getTime());
    const min = Math.min(...times);
    const max = Math.max(...times);
    return [min, max === min ? min + 1 : max];
  }

  chartX(iso: string): number {
    const [min, max] = this.chartRange;
    const c = this.chart;
    return c.left + ((parseDate(iso).getTime() - min) / (max - min)) * (c.width - c.left - c.right);
  }

  chartY(level: number): number {
    const c = this.chart;
    return c.top + ((4 - level) / 3) * (c.height - c.top - c.bottom);
  }

  get sortedConditions(): ConditionPoint[] {
    return [...this.conditionHistory].sort((a, b) => a.date.localeCompare(b.date));
  }

  get chartPoints(): { x: number; y: number; p: ConditionPoint }[] {
    return this.sortedConditions.map((p) => ({ x: this.chartX(p.date), y: this.chartY(CONDITION_LEVEL[p.condition]), p }));
  }

  get chartSegments(): { x1: number; y1: number; x2: number; y2: number; condition: Condition }[] {
    const pts = this.chartPoints;
    return pts.slice(1).map((b, i) => ({ x1: pts[i].x, y1: pts[i].y, x2: b.x, y2: b.y, condition: b.p.condition }));
  }

  readonly chartLevels = CONDITIONS.map((c) => ({ label: c, level: CONDITION_LEVEL[c] }));

  conditionColor(c: Condition): string {
    return c === 'Poor' ? '#d03b3b' : c === 'Fair' ? '#fab219' : '#0ca30c';
  }

  // ===== Actions menu =====

  toggleActions(event: MouseEvent): void {
    event.stopPropagation();
    this.actionsOpen = !this.actionsOpen;
  }

  @HostListener('document:click')
  closeMenus(): void {
    this.actionsOpen = false;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.actionsOpen = false;
    this.closeModal();
  }

  exportCsv(): void {
    this.actionsOpen = false;
    const esc = (v: string | number) => `"${String(v).replace(/"/g, '""')}"`;
    const rows: (string | number)[][] = [
      ['Section', 'Item', 'Date', 'Status / Value', 'By / Notes'],
      ...this.stages.map((s) => ['Stage', s.label, s.date, s.status, s.owner]),
      ...this.sortedEvents.map((e) => ['Event', e.title, e.date, e.kind, e.by]),
      ...this.sortedConditions.map((c) => ['Condition', 'Assessment', c.date, c.condition, c.notes]),
      ...this.costRows.map((r) => ['Financial', r.label, '', this.financial[r.key], 'AED']),
    ];
    const url = URL.createObjectURL(new Blob([rows.map((r) => r.map(esc).join(',')).join('\n')], { type: 'text/csv' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = `${this.asset.tag}-lifecycle.csv`;
    link.click();
    URL.revokeObjectURL(url);
    this.showToast('Lifecycle exported as CSV.');
  }

  print(): void {
    this.actionsOpen = false;
    window.print();
  }

  // ===== Modals =====

  open(kind: ModalKind, stage?: Stage): void {
    this.actionsOpen = false;
    this.submitted = false;
    if (kind === 'info') this.infoDraft = { ...this.asset };
    if (kind === 'dates') this.datesDraft = { ...this.dates };
    if (kind === 'status') this.statusDraft = { ...this.status };
    if (kind === 'condition') this.conditionDraft = { date: this.todayIso, condition: this.status.condition, notes: '' };
    if (kind === 'event') this.eventDraft = { date: this.todayIso, title: '', by: '', kind: 'Other' };
    if (kind === 'stage' && stage) {
      this.stageTarget = stage;
      this.stageDraft = { ...stage };
    }
    if (kind === 'advance' && !this.nextStage) {
      this.showToast('There is no later stage to move to.');
      return;
    }
    this.modal = kind;
  }

  closeModal(): void {
    this.modal = null;
    this.stageDraft = null;
    this.stageTarget = null;
  }

  saveInfo(): void {
    this.submitted = true;
    const d = this.infoDraft;
    if (![d.category, d.typeModel, d.serial, d.location, d.custodian].every((v) => v.trim())) return;
    this.asset = { ...d };
    this.closeModal();
    this.showToast('Asset information updated on this page. Edit the asset registration to save it.');
  }

  get datesError(): string | null {
    const d = this.datesDraft;
    if (![d.purchase, d.installation, d.operationalSince, d.expectedDisposal].every(Boolean)) return 'Purchase, installation, operational and disposal dates are required.';
    if (d.installation < d.purchase) return 'Installation can\'t be before purchase.';
    if (d.operationalSince < d.installation) return 'Operational date can\'t be before installation.';
    if (d.expectedDisposal <= d.operationalSince) return 'Expected disposal must be after the operational date.';
    if (d.expectedUpgrade && (d.expectedUpgrade < d.operationalSince || d.expectedUpgrade > d.expectedDisposal)) {
      return 'Expected upgrade must fall between the operational and disposal dates.';
    }
    return null;
  }

  saveDates(): void {
    this.submitted = true;
    if (this.datesError) return;
    this.dates = { ...this.datesDraft };
    this.closeModal();
    this.showToast('Key dates updated on this page. Edit the asset registration to save them.');
  }

  get statusError(): string | null {
    const s = this.statusDraft;
    const pct = [s.health, s.utilization, s.availability];
    if (pct.some((v) => v === null || v === undefined || !Number.isFinite(Number(v)) || v < 0 || v > 100)) return 'Health, utilization and availability must be between 0 and 100.';
    if (s.lastMaintenance && s.nextMaintenance && s.nextMaintenance < s.lastMaintenance) return 'Next maintenance can\'t be before the last one.';
    if (s.lastAssessment && s.nextAssessment && s.nextAssessment < s.lastAssessment) return 'Next assessment can\'t be before the last one.';
    return null;
  }

  saveStatus(): void {
    this.submitted = true;
    if (this.statusError) return;
    this.status = { ...this.statusDraft, health: Number(this.statusDraft.health), utilization: Number(this.statusDraft.utilization), availability: Number(this.statusDraft.availability) };
    this.closeModal();
    this.showToast('Status updated on this page. Edit the asset registration to save it.');
  }

  saveCondition(): void {
    this.submitted = true;
    const d = this.conditionDraft;
    if (!d.date || d.date > this.todayIso) return;
    if (this.registrationId) {
      // condition assessments are kept in the Asset API
      this.saving = true;
      this.api.addAssessment(this.registrationId, { date: d.date, condition: d.condition, healthScore: this.status.health, remarks: d.notes.trim() }).subscribe({
        next: () => {
          this.saving = false;
          this.applyCondition(d);
        },
        error: (err) => {
          this.saving = false;
          this.showToast(apiErrorMessage(err, 'The condition assessment could not be saved.'));
        },
      });
      return;
    }
    this.applyCondition(d);
  }

  private applyCondition(d: ConditionPoint): void {
    this.conditionHistory.push({ ...d, notes: d.notes.trim() });
    // The latest assessment sets the current condition and the next due date (every six months).
    if (d.date >= this.status.lastAssessment) {
      this.status = { ...this.status, condition: d.condition, lastAssessment: d.date, nextAssessment: addMonths(d.date, 6) };
    }
    this.addEvent({ date: d.date, title: `Condition assessed as ${d.condition}`, by: 'Engineering Team', kind: 'Condition' });
    this.closeModal();
    this.showToast('Condition assessment recorded.');
  }

  saveEvent(): void {
    this.submitted = true;
    const d = this.eventDraft;
    if (!d.date || !d.title.trim()) return;
    const e = { ...d, title: d.title.trim(), by: d.by.trim() || this.api.user || '' };
    const apply = () => {
      this.addEvent(e);
      this.closeModal();
      this.showToast('Lifecycle event added.');
    };
    if (!this.registrationId || !this.lifecycleApi) return apply();
    this.saving = true;
    this.api.addEvent(this.registrationId, e).subscribe({
      next: () => {
        this.saving = false;
        apply();
      },
      error: (err) => {
        this.saving = false;
        this.showToast(apiErrorMessage(err, 'The event could not be saved.'));
      },
    });
  }

  private addEvent(e: Omit<LifecycleEvent, 'id'>): void {
    this.events.push({ id: this.nextEventId++, ...e });
  }

  get stageError(): string | null {
    const d = this.stageDraft;
    if (!d) return null;
    if ((d.status === 'Completed' || d.status === 'Current') && !d.date) return 'Completed and current stages need a date.';
    return null;
  }

  saveStage(): void {
    this.submitted = true;
    const target = this.stageTarget;
    const d = this.stageDraft;
    if (!target || !d || this.stageError) return;
    const prev = d.status === 'Current' ? this.stages.find((s) => s !== target && s.status === 'Current') : undefined;
    this.persistStage(target, { status: d.status, date: d.date, owner: d.owner.trim(), notes: d.notes.trim() }, prev?.key, () => {
      // Only one stage can be current; the previous one is marked completed.
      if (d.status === 'Current') {
        this.stages.filter((s) => s !== target && s.status === 'Current').forEach((s) => (s.status = 'Completed'));
      }
      const becameCurrent = d.status === 'Current' && target.status !== 'Current';
      Object.assign(target, { status: d.status, date: d.date, owner: d.owner.trim(), notes: d.notes.trim() });
      if (becameCurrent) this.addEvent({ date: d.date, title: `Moved to ${target.label} stage`, by: this.api.user || '', kind: 'Stage' });
      this.closeModal();
      this.showToast(`${target.label} stage updated.`);
    });
  }

  confirmAdvance(): void {
    const current = this.currentStage;
    const next = this.nextStage;
    if (!next) return;
    this.persistStage(next, { status: 'Current', date: this.todayIso, owner: next.owner, notes: next.notes }, current?.key, () => {
      if (current) current.status = 'Completed';
      next.status = 'Current';
      next.date = this.todayIso;
      this.addEvent({ date: this.todayIso, title: `Moved to ${next.label} stage`, by: this.api.user || '', kind: 'Stage' });
      this.closeModal();
      this.showToast(`${this.asset.tag} moved to ${next.label}.`);
    });
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 3000);
  }
}
