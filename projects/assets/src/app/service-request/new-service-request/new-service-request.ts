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
import { Observable, Subject, catchError, forkJoin, map, of, switchMap, tap } from 'rxjs';
import { ASSET_RECORDS, ASSET_TYPE_ICONS, AssetRecord, AuthService, TODAY, assetLocation, findAsset, toIsoDate } from 'shared-ui';
import { loadAssetDetail } from '../../asset-details/asset-details';
import { AssetDetailsService, AssetRegistrationDto } from '../../asset-details/asset-details.service';
import { SR_SLA, SrPriority } from '../../asset-maintenance/maintenance-sr';
import { apiErrorMessage } from '../../services/asset-api';
import {
  MaintenanceWorkOrderDto,
  SR_API_PRIORITIES,
  SR_API_REQUEST_TYPES,
  SaveServiceRequestBody,
  ServiceRequestApiService,
  ServiceRequestDto,
  SrAttachmentRequest,
  fmtFileSize,
  localStamp,
  localToIso,
} from '../service-request-api.service';

interface NewSrForm {
  title: string;
  category: string;
  priority: string;
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
  /** A file picked in this session, uploaded on save. */
  file?: File;
  /** A file already uploaded with a saved draft. */
  saved?: SrAttachmentRequest;
}

/** A row of Recent Service Requests / the Related Service Request list. */
interface SrRow {
  /** Request number. */
  id: string;
  apiId: string;
  title: string;
  status: string;
  /** yyyy-MM-dd */
  date: string;
}

interface WoOption {
  id: string;
  description: string;
}

type FieldKey = 'title' | 'description' | 'requestedDate' | 'targetDate' | 'location' | 'requestedBy' | 'plannedEnd';

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
/** Used when the signed-in user has no name. */
const FALLBACK_USER = 'Rex Kumar';
/** No people API in the Asset service yet: the assignable technicians are a fixed list. */
const TECHNICIANS = ['Ahmed Khan', 'Ravi Patel', 'Nisha Patel', 'Ramesh Kumar', 'Salim Ahmed', 'Khalid Mansoori', 'Priya Nair'];
const TAG_SUGGESTIONS = ['Noise', 'Vibration', 'Leak', 'Overheating', 'Electrical', 'Operation', 'Safety', 'Inspection'];
/** The Media service stores images and PDFs only. */
const UPLOAD_EXTS = ['pdf', 'jpg', 'jpeg', 'png', 'webp'];
const MAX_UPLOAD = 10 * 1024 * 1024;
const MAX_DESCRIPTION = 1000;

/** Full-page form that raises a service request against one asset (POST /api/service-requests). */
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
  categories: string[] = SR_API_REQUEST_TYPES;
  priorities: string[] = SR_API_PRIORITIES;
  readonly technicians = TECHNICIANS;
  readonly maxDescription = MAX_DESCRIPTION;
  readonly uploadAccept = UPLOAD_EXTS.map((e) => '.' + e).join(',');

  asset: AssetRecord = findAsset('GEN-00045') ?? ASSET_RECORDS[0];
  /** The asset's registration in the Asset API; null until loaded or when the tag is not registered. */
  registration: AssetRegistrationDto | null = null;
  assetLoading = false;
  health: number | null = null;
  form: NewSrForm = this.blankForm();
  files: PendingFile[] = [];
  /** The asset's requests, newest first. */
  requests: SrRow[] = [];
  workOrders: WoOption[] = [];
  /** Saved draft of this asset being continued, if any. */
  private draftId = '';
  /** Target date follows the priority's SLA until the user picks one. */
  private targetTouched = false;
  tagInput = '';
  tagsOpen = false;
  dragging = false;
  submitted = false;
  busy = false;
  readonly errors: Partial<Record<FieldKey, string>> = {};
  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;
  private readonly load$ = new Subject<string | null>();
  private readonly router = inject(Router);
  private readonly api = inject(ServiceRequestApiService);
  private readonly assets = inject(AssetDetailsService);
  private readonly auth = inject(AuthService);

  constructor() {
    const destroyRef = inject(DestroyRef);
    this.api.options().pipe(takeUntilDestroyed(destroyRef)).subscribe({
      next: (o) => {
        if (o.requestTypes?.length) this.categories = o.requestTypes;
        if (o.priorities?.length) this.priorities = o.priorities;
      },
      error: () => undefined, // keep the built-in lists
    });
    this.load$.pipe(switchMap((tag) => this.loadAsset(tag)), takeUntilDestroyed(destroyRef)).subscribe();
    inject(ActivatedRoute)
      .queryParamMap.pipe(takeUntilDestroyed(destroyRef))
      .subscribe((p) => this.load$.next(p.get('tag')));
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
  }

  // ===== Loading =====

  /** The asset by tag, then its requests (and any saved draft) and its work orders. */
  private loadAsset(tag: string | null): Observable<unknown> {
    const fallback = findAsset(tag) ?? (tag ? undefined : this.asset);
    if (fallback) this.setAssetRecord(fallback);
    this.registration = null;
    this.requests = [];
    this.workOrders = [];
    this.draftId = '';
    this.files = [];
    this.form = this.blankForm();
    this.targetTouched = false;
    this.submitted = false;
    const key = tag ?? fallback?.tag;
    if (!key) return of(null);
    this.assetLoading = true;
    return this.assets.getByTag(key, fallback).pipe(
      catchError((err) => {
        this.showToast(apiErrorMessage(err, 'Could not load the asset.'));
        return of(null);
      }),
      tap((a) => {
        this.assetLoading = false;
        if (!a) return;
        this.registration = a.registration;
        this.setAssetRecord(a.record);
        this.form = this.blankForm();
      }),
      switchMap((a) => {
        if (!a) return of(null);
        const id = a.registration.assetRegistrationId;
        return forkJoin({
          list: this.api.list({ assetRegistrationId: id, pageSize: 50 }).pipe(catchError(() => of(null))),
          workOrders: this.api.workOrdersOfAsset(id).pipe(catchError(() => of([] as MaintenanceWorkOrderDto[]))),
        }).pipe(
          tap((r) => {
            this.requests = (r.list?.items ?? []).map((i) => ({ id: i.requestNumber, apiId: i.id, title: i.title, status: i.status ?? '', date: localStamp(i.requestedDate).slice(0, 10) }));
            this.workOrders = r.workOrders.map((w) => ({ id: w.workOrderId, description: w.title }));
          }),
          switchMap(() => {
            const draft = this.requests.find((x) => x.status === 'Draft');
            return draft ? this.api.get(draft.apiId).pipe(catchError(() => of(null))) : of(null);
          }),
          tap((draft) => draft && this.restoreDraft(draft)),
        );
      }),
    );
  }

  private setAssetRecord(a: AssetRecord): void {
    const changed = a.tag !== this.asset.tag || this.health === null;
    this.asset = a;
    if (!changed) return;
    this.health = null;
    loadAssetDetail(a.tag).then((d) => {
      if (this.asset.tag === a.tag && d) this.health = d.health.score;
    });
  }

  private restoreDraft(d: ServiceRequestDto): void {
    this.draftId = d.id;
    this.targetTouched = true;
    this.form = {
      title: d.title, category: d.requestType ?? this.categories[0], priority: d.priority ?? 'Medium', requestedDate: localStamp(d.requestedDate).slice(0, 10) || toIsoDate(TODAY),
      targetDate: (d.targetDate ?? '').slice(0, 10), description: d.description ?? '', location: d.location?.name ?? this.location, department: d.department?.name ?? this.asset.department,
      requestedBy: d.requestedBy?.name ?? this.currentUser, assignedTo: d.assignedTo?.name ?? '', downtimeHours: d.estimatedDowntimeHours, plannedStart: localStamp(d.plannedStart),
      plannedEnd: localStamp(d.plannedEnd), workOrder: d.workOrder?.workOrderId ?? '', relatedRequest: d.relatedServiceRequest ?? '', tags: [...(d.tags ?? [])], reference: d.referenceNumber ?? '',
    };
    this.files = (d.attachments ?? []).map((a) => ({
      name: a.fileName, size: fmtFileSize(a.fileSize), url: a.fileUrl,
      saved: { fileName: a.fileName, fileUrl: a.fileUrl, contentType: a.contentType, fileSize: a.fileSize, thumbnailUrl: a.thumbnailUrl, description: a.description },
    }));
  }

  private get currentUser(): string {
    return this.auth.getUser()?.userName || this.auth.getUserEmail() || FALLBACK_USER;
  }

  private blankForm(): NewSrForm {
    const a = this.asset;
    const today = toIsoDate(TODAY);
    return {
      title: '', category: this.categories?.[0] ?? 'Corrective', priority: 'Medium', requestedDate: today, targetDate: this.targetFor('Medium', today), description: '',
      location: this.location, department: a.department, requestedBy: this.currentUser, assignedTo: '', downtimeHours: null, plannedStart: '', plannedEnd: '',
      workOrder: '', relatedRequest: '', tags: [], reference: '',
    };
  }

  private targetFor(p: string, from: string): string {
    const d = new Date((from || toIsoDate(TODAY)) + 'T00:00');
    d.setDate(d.getDate() + (SR_SLA[p as SrPriority]?.resolutionDays ?? 1));
    return toIsoDate(d);
  }

  get location(): string {
    const path = this.registration?.locationAssignment?.location?.locationPath;
    if (path) return path;
    const a = this.asset;
    return a ? [a.site, a.area].filter(Boolean).join(' - ') : '';
  }

  // ===== Options =====

  get locations(): string[] {
    return [...new Set([this.location, ...ASSET_RECORDS.map(assetLocation), this.form?.location].filter(Boolean))].sort();
  }

  get departments(): string[] {
    return [...new Set([...ASSET_RECORDS.map((a) => a.department), 'Operations', 'Maintenance', this.asset.department, this.form.department].filter(Boolean))].sort();
  }

  get people(): string[] {
    return [...new Set([this.currentUser, ...TECHNICIANS, this.asset.custodian, this.form.requestedBy].filter(Boolean))].sort();
  }

  get recent(): SrRow[] {
    return this.requests.filter((r) => r.status !== 'Draft').slice(0, 3);
  }

  get requestsByDate(): SrRow[] {
    return this.requests.filter((r) => r.status !== 'Draft');
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
      this.files.push({ name: f.name, size: fmtFileSize(f.size), url: URL.createObjectURL(f), file: f });
    }
    if (skipped.length) this.showToast(`Not added (PDF, JPG, PNG or WEBP up to 10 MB only): ${skipped.join(', ')}.`);
  }

  removeFile(f: PendingFile): void {
    this.files = this.files.filter((x) => x !== f);
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

  /** Uploads the new files through the Media service; the saved ones are kept as they are. */
  private uploadFiles(): Observable<SrAttachmentRequest[]> {
    if (!this.files.length) return of([]);
    return forkJoin(
      this.files.map((f) =>
        f.saved
          ? of(f.saved)
          : this.api.upload(f.file!).pipe(
              tap((a) => {
                f.saved = a; // a retry does not upload it again
                f.url = a.fileUrl;
              }),
            ),
      ),
    );
  }

  private body(attachments: SrAttachmentRequest[]): SaveServiceRequestBody {
    const f = this.form;
    const reg = this.registration!;
    const now = new Date();
    // today's request gets the current time; a back-dated one keeps its date
    const requestedDate = f.requestedDate === toIsoDate(now) ? now.toISOString() : f.requestedDate;
    const ownLocation = f.location === this.location;
    const assetLoc = reg.locationAssignment?.location;
    return {
      assetRegistrationId: reg.assetRegistrationId,
      title: f.title.trim(),
      requestType: f.category || null,
      priority: f.priority || null,
      requestedDate,
      targetDate: f.targetDate || null,
      description: f.description.trim() || null,
      location: f.location
        ? { id: ownLocation ? assetLoc?.roomId ?? assetLoc?.zoneId ?? assetLoc?.floorId ?? assetLoc?.buildingId ?? assetLoc?.siteId ?? null : null, name: f.location }
        : null,
      department: f.department ? { id: null, name: f.department } : null,
      requestedBy: f.requestedBy ? { userId: null, name: f.requestedBy, role: null } : null,
      assignedTo: f.assignedTo ? { userId: null, name: f.assignedTo, role: 'Technician' } : null,
      estimatedDowntimeHours: f.downtimeHours ?? null,
      plannedStart: localToIso(f.plannedStart),
      plannedEnd: localToIso(f.plannedEnd),
      actualStart: null,
      actualEnd: null,
      relatedWorkOrderId: f.workOrder || null,
      relatedServiceRequest: f.relatedRequest || null,
      attachments,
      tags: [...f.tags],
      referenceNumber: f.reference.trim() || null,
      createdBy: this.auth.getUserEmail() ?? this.currentUser,
    };
  }

  /** Save (create or update the draft) through the API. */
  private save(asDraft: boolean): Observable<ServiceRequestDto> {
    return this.uploadFiles().pipe(
      switchMap((attachments) => {
        const body = this.body(attachments);
        if (!this.draftId) return this.api.create(body, asDraft);
        return this.api.update(this.draftId, body, asDraft).pipe(switchMap((d) => (asDraft ? of(d) : this.api.submit(d.id))));
      }),
    );
  }

  private get canSave(): boolean {
    if (this.busy) return false;
    if (!this.registration) {
      this.showToast(this.assetLoading ? 'The asset is still loading.' : `${this.asset.tag} is not registered in the Asset API, so a request cannot be raised for it.`);
      return false;
    }
    return true;
  }

  submit(): void {
    this.submitted = true;
    if (!this.validate()) return void this.showToast('Fix the highlighted fields.');
    if (!this.canSave) return;
    const assigned = this.form.assignedTo;
    this.busy = true;
    this.save(false)
      .pipe(
        // a request raised with an assignee starts as In Progress, as before
        switchMap((dto) =>
          assigned && dto.status === 'Open'
            ? this.api.addUpdate(dto.id, { status: 'In Progress', message: `Assigned to ${assigned}.` }).pipe(catchError(() => of(dto)))
            : of(dto),
        ),
      )
      .subscribe({
        next: (dto) => {
          this.busy = false;
          this.router.navigate(['/assets/service-requests', dto.id], { queryParams: { tag: this.asset.tag, tab: 'details' } });
        },
        error: (err) => {
          this.busy = false;
          this.showToast(apiErrorMessage(err, 'Could not submit the service request.'));
        },
      });
  }

  saveDraft(): void {
    if (!this.form.title.trim()) {
      this.submitted = true;
      this.validate();
      return void this.showToast('Enter a title to save a draft.');
    }
    if (!this.canSave) return;
    this.busy = true;
    this.save(true).subscribe({
      next: (dto) => {
        this.busy = false;
        this.draftId = dto.id;
        this.showToast(`Draft ${dto.requestNumber} saved. It reopens the next time you raise a request for this asset.`);
      },
      error: (err) => {
        this.busy = false;
        this.showToast(apiErrorMessage(err, 'Could not save the draft.'));
      },
    });
  }

  cancel(): void {
    this.router.navigate(['/assets/maintenance'], { queryParams: { tag: this.asset.tag, tab: 'requests' } });
  }

  /** Deletes the saved draft and starts a blank form. */
  discardDraft(): void {
    const reset = () => {
      this.draftId = '';
      this.form = this.blankForm();
      this.files = [];
      this.targetTouched = false;
      this.submitted = false;
      this.validate();
    };
    if (!this.draftId) return reset();
    this.busy = true;
    const id = this.draftId;
    this.api.delete(id).subscribe({
      next: () => {
        this.busy = false;
        this.requests = this.requests.filter((r) => r.apiId !== id);
        reset();
      },
      error: (err) => {
        this.busy = false;
        this.showToast(apiErrorMessage(err, 'Could not discard the draft.'));
      },
    });
  }

  get hasDraft(): boolean {
    return !!this.draftId;
  }

  openRequest(r: SrRow): void {
    this.router.navigate(['/assets/service-requests', r.apiId], { queryParams: { tag: this.asset.tag } });
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

  statusClass(s: string): string {
    return 'sr-st--' + s.toLowerCase().replace(' ', '-');
  }

  prioClass(p: string): string {
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
