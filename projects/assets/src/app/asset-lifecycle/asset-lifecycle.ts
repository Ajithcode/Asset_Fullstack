import { Component, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
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
export class AssetLifecycle {
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
  readonly image = 'asset-generator.svg';

  asset: AssetInfo = {
    tag: 'GEN-00045',
    name: 'Generator 250 kVA',
    category: 'Power Equipment',
    typeModel: 'Caterpillar C9.3',
    serial: 'CATC083KLB12345',
    location: 'Main Plant - Block A',
    custodian: 'Engineering Team',
    criticality: 'Critical',
  };

  dates: KeyDates = {
    purchase: '2023-01-10',
    installation: '2023-01-25',
    operationalSince: '2023-01-26',
    expectedUpgrade: '2028-01-26',
    expectedDisposal: '2033-01-26',
  };

  status: StatusInfo = {
    operational: 'Active',
    condition: 'Good',
    health: 85,
    utilization: 72,
    availability: 98,
    lastMaintenance: '2024-03-12',
    nextMaintenance: '2025-03-12',
    lastAssessment: '2025-09-01',
    nextAssessment: '2026-03-01',
  };

  stages: Stage[] = [
    { key: 'planning', label: 'Planning', status: 'Completed', date: '2023-01-05', owner: 'Engineering Team', notes: 'Capacity study approved for Block A backup power.' },
    { key: 'procurement', label: 'Procurement', status: 'Completed', date: '2023-01-10', owner: 'Procurement Team', notes: 'PO-2023-0012 raised with Cummins Arabia.' },
    { key: 'received', label: 'Received', status: 'Completed', date: '2023-01-18', owner: 'Store Team', notes: 'GRN-2023-0145; no transit damage.' },
    { key: 'installation', label: 'Installation', status: 'Completed', date: '2023-01-25', owner: 'Installation Team', notes: 'Installed on plinth P-04, Block A.' },
    { key: 'commissioning', label: 'Commissioning', status: 'Completed', date: '2023-01-26', owner: 'Engineering Team', notes: 'Load bank test passed at 100% for 4 hours.' },
    { key: 'operation', label: 'In Operation', status: 'Current', date: '2023-01-26', owner: 'Operations Team', notes: '' },
    { key: 'maintenance', label: 'Maintenance', status: 'Planned', date: '', owner: 'Maintenance Team', notes: 'Planned preventive maintenance every 12 months.' },
    { key: 'upgrade', label: 'Upgrade', status: 'Planned', date: '', owner: 'Engineering Team', notes: 'Control panel upgrade planned for 2028.' },
    { key: 'decommissioning', label: 'Decommissioning', status: 'Not Started', date: '', owner: '', notes: '' },
    { key: 'disposal', label: 'Disposal', status: 'Not Started', date: '', owner: '', notes: '' },
  ];

  events: LifecycleEvent[] = [
    { id: 1, date: '2023-01-05', title: 'Capacity planning approved', by: 'Engineering Team', kind: 'Procurement' },
    { id: 2, date: '2023-01-10', title: 'Purchase order received (PO-2023-0012)', by: 'Procurement Team', kind: 'Procurement' },
    { id: 3, date: '2023-01-18', title: 'Asset received (GRN-2023-0145)', by: 'Store Team', kind: 'Installation' },
    { id: 4, date: '2023-01-25', title: 'Installed at Main Plant - Block A', by: 'Installation Team', kind: 'Installation' },
    { id: 5, date: '2023-01-26', title: 'Commissioned and handed over', by: 'Engineering Team', kind: 'Installation' },
    { id: 6, date: '2023-01-26', title: 'Put into operation', by: 'Operations Team', kind: 'Operation' },
    { id: 7, date: '2024-03-12', title: 'Preventive maintenance completed (WO-2024-0032)', by: 'Maintenance Team', kind: 'Maintenance' },
    { id: 8, date: '2025-08-15', title: 'Minor repair completed (WO-2025-0121)', by: 'Maintenance Team', kind: 'Repair' },
  ];
  private nextEventId = 9;

  conditionHistory: ConditionPoint[] = [
    { date: '2023-01-26', condition: 'Excellent', notes: 'Commissioning inspection' },
    { date: '2023-07-20', condition: 'Excellent', notes: '' },
    { date: '2024-01-18', condition: 'Good', notes: 'Minor oil seepage noted' },
    { date: '2024-07-22', condition: 'Good', notes: '' },
    { date: '2025-01-15', condition: 'Fair', notes: 'Alternator bearing noise; repair scheduled' },
    { date: '2025-09-01', condition: 'Good', notes: 'After minor repair (WO-2025-0121)' },
  ];

  readonly financial = { acquisition: 220_000, operating: 50_000, maintenance: 45_000, upgrade: 35_000 };
  readonly warranty = { end: '2026-01-26', provider: 'Cummins Arabia', amcEnd: '2027-01-26', amcProvider: 'Al Masaood Power' };
  readonly maintenanceSummary = { workOrders: 12, openWorkOrders: 1, breakdowns: 2, downtimeHours: 16 };

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
    return (this.financial[key] / this.totalCost) * 100;
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
    this.showToast('Asset information updated.');
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
    this.showToast('Key dates updated.');
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
    this.showToast('Status and condition updated.');
  }

  saveCondition(): void {
    this.submitted = true;
    const d = this.conditionDraft;
    if (!d.date || d.date > this.todayIso) return;
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
    this.addEvent({ ...d, title: d.title.trim(), by: d.by.trim() || 'Rex Kumar' });
    this.closeModal();
    this.showToast('Lifecycle event added.');
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
    // Only one stage can be current; the previous one is marked completed.
    if (d.status === 'Current') {
      this.stages.filter((s) => s !== target && s.status === 'Current').forEach((s) => (s.status = 'Completed'));
    }
    const becameCurrent = d.status === 'Current' && target.status !== 'Current';
    Object.assign(target, { status: d.status, date: d.date, owner: d.owner.trim(), notes: d.notes.trim() });
    if (becameCurrent) this.addEvent({ date: d.date, title: `Moved to ${target.label} stage`, by: 'Rex Kumar', kind: 'Stage' });
    this.closeModal();
    this.showToast(`${target.label} stage updated.`);
  }

  confirmAdvance(): void {
    const current = this.currentStage;
    const next = this.nextStage;
    if (!next) return;
    if (current) current.status = 'Completed';
    next.status = 'Current';
    next.date = this.todayIso;
    this.addEvent({ date: this.todayIso, title: `Moved to ${next.label} stage`, by: 'Rex Kumar', kind: 'Stage' });
    this.closeModal();
    this.showToast(`${this.asset.tag} moved to ${next.label}.`);
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 3000);
  }
}
