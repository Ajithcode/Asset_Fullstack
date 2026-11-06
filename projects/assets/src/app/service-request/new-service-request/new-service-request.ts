import { Component, DestroyRef, HostListener, OnDestroy, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  LucideAngularModule,
  Building2,
  Clock,
  File,
  Folder,
  MapPin,
  Save,
  Send,
  TriangleAlert,
  Upload,
  User,
  X,
} from 'lucide-angular';
import { ASSET_RECORDS, ASSET_TYPE_ICONS, AssetRecord, TODAY, assetLocation, findAsset, toIsoDate } from 'shared-ui';
import { loadAssetDetail } from '../../asset-details/asset-details';
import { MaintenanceData, dataFor } from '../../asset-maintenance/asset-maintenance';
import { SR_CATEGORIES, SR_PRIORITIES, SR_SLA, SrCategory, SrPriority, SrStatus, ServiceRequest as SrRecord } from '../../asset-maintenance/maintenance-sr';

interface NewSrForm {
  title: string;
  category: SrCategory;
  priority: SrPriority;
  requestedDate: string;
  targetDate: string;
  description: string;
  location: string;
  department: string;
  requestedBy: string;
  assignedTo: string;
  downtimeHours: number | null;
  plannedStart: string;
  plannedEnd: string;
  workOrder: string;
  relatedRequest: string;
  tags: string[];
  reference: string;
}

interface PendingFile {
  name: string;
  size: string;
  url: string;
}

type FieldKey = 'title' | 'description' | 'requestedDate' | 'targetDate' | 'location' | 'requestedBy' | 'plannedEnd';

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const CURRENT_USER = 'Rex Kumar';
const TECHNICIANS = ['Ahmed Khan', 'Ravi Patel', 'Nisha Patel', 'Ramesh Kumar', 'Salim Ahmed', 'Khalid Mansoori', 'Priya Nair'];
const TAG_SUGGESTIONS = ['Noise', 'Vibration', 'Leak', 'Overheating', 'Electrical', 'Operation', 'Safety', 'Inspection'];
const UPLOAD_EXTS = ['pdf', 'jpg', 'jpeg', 'png', 'doc', 'docx', 'xls', 'xlsx'];
const MAX_UPLOAD = 10 * 1024 * 1024;
const MAX_DESCRIPTION = 1000;
/** Drafts kept for the session, one per asset. */
const DRAFTS = new Map<string, { form: NewSrForm; files: PendingFile[] }>();

/** Full-page form that raises a service request against one asset. */
@Component({
  selector: 'app-new-service-request',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './new-service-request.html',
  // shares the maintenance page's base styles (header, cards, buttons, controls)
  styleUrls: [
    '../../asset-maintenance/asset-maintenance.css',
    '../../asset-maintenance/asset-maintenance-charts.css',
    '../../asset-maintenance/service-requests/service-requests.css',
    // summary column, drop zone and section headings match the request page
    '../service-request/service-request.css',
    './new-service-request.css',
  ],
})
export class NewServiceRequest implements OnDestroy {
  readonly icons = { Building2, Clock, File, Folder, MapPin, Save, Send, TriangleAlert, Upload, User, X };
  readonly typeIcons = ASSET_TYPE_ICONS;
  readonly categories = SR_CATEGORIES;
  readonly priorities = SR_PRIORITIES;
  readonly technicians = TECHNICIANS;
  readonly maxDescription = MAX_DESCRIPTION;
  readonly uploadAccept = UPLOAD_EXTS.map((e) => '.' + e).join(',');
  readonly locations = [...new Set(ASSET_RECORDS.map(assetLocation))].sort();

  asset: AssetRecord = findAsset('GEN-00045') ?? ASSET_RECORDS[0];
  data: MaintenanceData = dataFor(this.asset);
  health: number | null = null;
  form: NewSrForm = this.blankForm();
  files: PendingFile[] = [];
  /** Target date follows the priority's SLA until the user picks one. */
  private targetTouched = false;
  tagInput = '';
  tagsOpen = false;
  dragging = false;
  submitted = false;
  readonly errors: Partial<Record<FieldKey, string>> = {};
  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;
  private readonly router = inject(Router);

  constructor() {
    inject(ActivatedRoute)
      .queryParamMap.pipe(takeUntilDestroyed(inject(DestroyRef)))
      .subscribe((p) => this.setAsset(findAsset(p.get('tag')) ?? this.asset));
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
  }

  private setAsset(a: AssetRecord): void {
    this.asset = a;
    this.data = dataFor(a);
    this.health = null;
    const draft = DRAFTS.get(a.tag);
    this.form = draft ? structuredClone(draft.form) : this.blankForm();
    this.files = draft ? [...draft.files] : [];
    this.targetTouched = !!draft;
    loadAssetDetail(a.tag).then((d) => {
      if (this.asset.tag === a.tag && d) this.health = d.health.score;
    });
  }

  private blankForm(): NewSrForm {
    const a = this.asset;
    return {
      title: '', category: 'Mechanical', priority: 'Medium', requestedDate: toIsoDate(TODAY), targetDate: this.targetFor('Medium', toIsoDate(TODAY)), description: '',
      location: assetLocation(a), department: a.department, requestedBy: CURRENT_USER, assignedTo: '', downtimeHours: null, plannedStart: '', plannedEnd: '',
      workOrder: '', relatedRequest: '', tags: [], reference: '',
    };
  }

  private targetFor(p: SrPriority, from: string): string {
    const d = new Date((from || toIsoDate(TODAY)) + 'T00:00');
    d.setDate(d.getDate() + SR_SLA[p].resolutionDays);
    return toIsoDate(d);
  }

  get location(): string {
    return assetLocation(this.asset);
  }

  // ===== Options =====

  get departments(): string[] {
    return [...new Set([...ASSET_RECORDS.map((a) => a.department), 'Operations', 'Maintenance', this.form.department].filter(Boolean))].sort();
  }

  get people(): string[] {
    return [...new Set([CURRENT_USER, ...TECHNICIANS, ...this.data.serviceRequests.map((r) => r.requestedBy), this.asset.custodian].filter(Boolean))].sort();
  }

  get recent(): SrRecord[] {
    return [...this.data.serviceRequests].sort((a, b) => b.createdAt.localeCompare(a.createdAt)).slice(0, 3);
  }

  get requestsByDate(): SrRecord[] {
    return [...this.data.serviceRequests].sort((a, b) => b.createdAt.localeCompare(a.createdAt));
  }

  // ===== Field behaviour =====

  onSchedule(): void {
    if (!this.targetTouched) this.form.targetDate = this.targetFor(this.form.priority, this.form.requestedDate);
    this.validate();
  }

  onTarget(): void {
    this.targetTouched = true;
    this.validate();
  }

  /** End defaults to start plus the estimated downtime. */
  onPlanned(): void {
    const f = this.form;
    if (f.plannedStart && !f.plannedEnd && f.downtimeHours) {
      const d = new Date(f.plannedStart);
      d.setMinutes(d.getMinutes() + Math.round(f.downtimeHours * 60));
      f.plannedEnd = this.stamp(d);
    }
    this.validate();
  }

  get tagOptions(): string[] {
    const q = this.tagInput.trim().toLowerCase();
    return TAG_SUGGESTIONS.filter((t) => !this.form.tags.includes(t) && (!q || t.toLowerCase().includes(q)));
  }

  addTag(value = this.tagInput): void {
    const t = value.trim().replace(/,$/, '');
    if (t && !this.form.tags.some((x) => x.toLowerCase() === t.toLowerCase())) this.form.tags.push(t);
    this.tagInput = '';
  }

  removeTag(t: string): void {
    this.form.tags = this.form.tags.filter((x) => x !== t);
  }

  onTagKey(event: KeyboardEvent): void {
    if (event.key === 'Enter' || event.key === ',') {
      event.preventDefault();
      this.addTag();
    } else if (event.key === 'Backspace' && !this.tagInput && this.form.tags.length) {
      this.form.tags.pop();
    }
  }

  // ===== Attachments =====

  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragging = false;
    if (event.dataTransfer?.files.length) this.addFiles(event.dataTransfer.files);
  }

  onPick(input: HTMLInputElement): void {
    if (input.files?.length) this.addFiles(input.files);
    input.value = '';
  }

  private addFiles(list: FileList): void {
    const skipped: string[] = [];
    for (const f of Array.from(list)) {
      const ext = f.name.split('.').pop()?.toLowerCase() ?? '';
      if (!UPLOAD_EXTS.includes(ext) || f.size > MAX_UPLOAD) {
        skipped.push(f.name);
        continue;
      }
      if (this.files.some((x) => x.name === f.name)) continue;
      this.files.push({ name: f.name, size: this.fmtSize(f.size), url: URL.createObjectURL(f) });
    }
    if (skipped.length) this.showToast(`Not added (type or over 10 MB): ${skipped.join(', ')}.`);
  }

  removeFile(f: PendingFile): void {
    this.files = this.files.filter((x) => x !== f);
  }

  private fmtSize(bytes: number): string {
    return bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
  }

  // ===== Save =====

  /** Errors show once the user has tried to submit. */
  validate(): boolean {
    const f = this.form;
    const e = this.errors;
    for (const k of Object.keys(e) as FieldKey[]) delete e[k];
    if (!f.title.trim()) e.title = 'Enter the title.';
    if (!f.description.trim()) e.description = 'Describe the problem.';
    if (!f.requestedDate) e.requestedDate = 'Enter the requested date.';
    if (f.targetDate && f.requestedDate && f.targetDate < f.requestedDate) e.targetDate = 'Target date is before the requested date.';
    if (!f.location) e.location = 'Choose the location.';
    if (!f.requestedBy) e.requestedBy = 'Choose who requested it.';
    if (f.plannedStart && f.plannedEnd && f.plannedEnd <= f.plannedStart) e.plannedEnd = 'End must be after the start.';
    return !Object.keys(e).length;
  }

  submit(): void {
    this.submitted = true;
    if (!this.validate()) return void this.showToast('Fix the highlighted fields.');
    const f = this.form;
    const year = TODAY.getFullYear();
    const n = Math.max(0, ...this.data.serviceRequests.map((r) => Number(r.id.split('-')[2]) || 0)) + 1;
    const now = this.stamp(new Date());
    // a back-dated request keeps its date; today's gets the current time
    const createdAt = f.requestedDate === now.slice(0, 10) ? now : `${f.requestedDate}T09:00`;
    const by = f.requestedBy;
    const status: SrStatus = f.assignedTo || f.workOrder ? 'In Progress' : 'Open';
    const r: SrRecord = {
      id: `SR-${year}-${String(n).padStart(3, '0')}`, date: f.requestedDate, title: f.title.trim(), description: f.description.trim(), category: f.category, priority: f.priority,
      status, createdAt, requestedBy: by, requesterDept: f.department, assignedTo: f.assignedTo, respondedAt: status === 'In Progress' ? now : '', resolvedAt: '',
      targetDate: f.targetDate || this.targetFor(f.priority, f.requestedDate), workOrder: f.workOrder, rating: 0,
      updates: [{ at: createdAt, by, text: 'Service request created.' }],
      attachments: this.files.map((x) => ({ name: x.name, size: x.size, by: CURRENT_USER, at: now, description: '', url: x.url })),
      location: f.location, downtimeHours: f.downtimeHours ?? undefined, plannedStart: f.plannedStart, plannedEnd: f.plannedEnd,
      relatedRequest: f.relatedRequest, tags: [...f.tags], reference: f.reference.trim(),
    };
    if (f.assignedTo) r.updates.push({ at: now, by: CURRENT_USER, text: `Assigned to ${f.assignedTo}.` });
    if (f.workOrder) r.updates.push({ at: now, by: CURRENT_USER, text: `Converted to ${f.workOrder}.` });
    this.data.serviceRequests.unshift(r);
    DRAFTS.delete(this.asset.tag);
    this.router.navigate(['/assets/service-requests', r.id], { queryParams: { tag: this.asset.tag, tab: 'details' } });
  }

  saveDraft(): void {
    DRAFTS.set(this.asset.tag, { form: structuredClone(this.form), files: [...this.files] });
    this.showToast('Draft saved. It reopens the next time you raise a request for this asset.');
  }

  cancel(): void {
    this.router.navigate(['/assets/maintenance'], { queryParams: { tag: this.asset.tag, tab: 'requests' } });
  }

  discardDraft(): void {
    DRAFTS.delete(this.asset.tag);
    this.form = this.blankForm();
    this.files = [];
    this.targetTouched = false;
    this.submitted = false;
    this.validate();
  }

  get hasDraft(): boolean {
    return DRAFTS.has(this.asset.tag);
  }

  openRequest(r: SrRecord): void {
    this.router.navigate(['/assets/service-requests', r.id], { queryParams: { tag: this.asset.tag } });
  }

  // ===== Formatting =====

  private stamp(d: Date): string {
    const p = (n: number) => String(n).padStart(2, '0');
    return `${toIsoDate(d)}T${p(d.getHours())}:${p(d.getMinutes())}`;
  }

  fmtDate(iso: string): string {
    return iso ? `${iso.slice(8, 10)} ${MONTHS[Number(iso.slice(5, 7)) - 1]} ${iso.slice(0, 4)}` : '-';
  }

  fmtDateTime(v: string): string {
    return v ? `${this.fmtDate(v.slice(0, 10))} ${v.slice(11, 16)}` : '-';
  }

  statusClass(s: SrStatus): string {
    return 'sr-st--' + s.toLowerCase().replace(' ', '-');
  }

  prioClass(p: SrPriority): string {
    return 'prio--' + p.toLowerCase();
  }

  // ===== Health gauge =====

  get healthBand(): string {
    const s = this.health ?? 0;
    return s >= 70 ? 'Excellent' : s >= 50 ? 'Good' : s >= 30 ? 'Fair' : 'Poor';
  }

  gaugeDash(score: number): string {
    const c = 2 * Math.PI * 34;
    return `${(c * 0.75 * Math.max(0, Math.min(100, score))) / 100} ${c}`;
  }

  get gaugeTrack(): string {
    const c = 2 * Math.PI * 34;
    return `${c * 0.75} ${c}`;
  }

  @HostListener('document:click')
  closeMenus(): void {
    this.tagsOpen = false;
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 4000);
  }
}
