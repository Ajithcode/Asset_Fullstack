import { Component, DestroyRef, HostListener, OnDestroy, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { Observable, Subject, catchError, combineLatest, forkJoin, map, of, switchMap, tap } from 'rxjs';
import {
  LucideAngularModule,
  ArrowDownUp,
  Boxes,
  Building2,
  CalendarDays,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  CircleCheck,
  EllipsisVertical,
  File,
  FileImage,
  FilePlus,
  FileSpreadsheet,
  FileText,
  Folder,
  LayoutGrid,
  Link2,
  List,
  MapPin,
  MessageSquare,
  Music,
  Package,
  Pencil,
  Plus,
  Search,
  TriangleAlert,
  Upload,
  Video,
  Wrench,
  X,
} from 'lucide-angular';
import { ASSET_RECORDS, ASSET_TYPE_ICONS, AssetRecord, AssetStatus, AuthService, findAsset } from 'shared-ui';
import { loadAssetDetail } from '../../asset-details/asset-details';
import { AssetDetailsService, fileUrl, iconForAsset, isoDay } from '../../asset-details/asset-details.service';
import { apiErrorMessage } from '../../services/asset-api';
import {
  MaintenanceWorkOrderDto,
  SR_API_PRIORITIES,
  SR_API_REQUEST_TYPES,
  ServiceRequestApiService,
  ServiceRequestDto,
  ServiceRequestRelatedDto,
  SrAttachmentRequest,
  SrUpdateDto,
  SrWorkOrderDto,
  fmtFileSize,
  localStamp,
  toSaveBody,
} from '../service-request-api.service';

type Tab = 'details' | 'updates' | 'attachments' | 'related';
type FileKind = 'Image' | 'PDF' | 'Document' | 'Spreadsheet' | 'Audio' | 'Video' | 'File';
type UpdateKind = 'created' | 'status' | 'comment' | 'file';

/** The request as the page shows it, mapped from the API's AssetServiceRequestResponse. */
interface SrView {
  /** Request number, e.g. SR-2025-028. */
  id: string;
  /** API id used for every call. */
  apiId: string;
  title: string;
  description: string;
  category: string;
  priority: string;
  status: string;
  /** yyyy-MM-ddTHH:mm (local) */
  createdAt: string;
  requestedBy: string;
  requesterDept: string;
  assignedTo: string;
  /** Actual start, or ''. */
  respondedAt: string;
  /** Actual end, or ''. */
  resolvedAt: string;
  targetDate: string;
  workOrder: string;
  /** No rating in the API. */
  rating: number;
  location?: string;
  downtimeHours?: number;
  plannedStart?: string;
  plannedEnd?: string;
  relatedRequest?: string;
  tags?: string[];
  reference?: string;
}

interface UpdateRow {
  at: string;
  by: string;
  text: string;
  kind: UpdateKind;
  action: string;
  /** Status the request moved to with this update, if any. */
  status: string | null;
}

interface AttachmentRow {
  /** Attachment id. */
  key: string;
  name: string;
  size: string;
  description?: string;
  url?: string;
  kind: FileKind;
  by: string;
  at: string;
}

interface WoRow {
  id: string;
  date: string;
  type: string;
  description: string;
  status: string;
  technician: string;
  priority?: string;
  downtimeMin?: number;
}

interface ComponentRow {
  tag: string;
  name: string;
  type: string;
  relationship: string;
}

interface DocRow {
  name: string;
  kind: FileKind;
  at: string;
  by: string;
  relatedTo: string;
  url?: string;
}

interface EditForm {
  title: string;
  description: string;
  category: string;
  priority: string;
  assignedTo: string;
  targetDate: string;
}

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
/** No people API in the Asset service yet: the assignable technicians are a fixed list. */
const TECHNICIANS = ['Ahmed Khan', 'Ravi Patel', 'Nisha Patel', 'Ramesh Kumar', 'Salim Ahmed', 'Khalid Mansoori', 'Priya Nair'];
const KIND_BY_EXT: Record<string, FileKind> = {
  jpg: 'Image', jpeg: 'Image', png: 'Image', gif: 'Image', webp: 'Image', pdf: 'PDF', doc: 'Document', docx: 'Document', txt: 'Document',
  xls: 'Spreadsheet', xlsx: 'Spreadsheet', csv: 'Spreadsheet', m4a: 'Audio', mp3: 'Audio', wav: 'Audio', mp4: 'Video', mov: 'Video',
};
const UPLOAD_EXTS = ['pdf', 'jpg', 'jpeg', 'png', 'doc', 'docx', 'xls', 'xlsx', 'mp4'];
const MAX_UPLOAD = 50 * 1024 * 1024;
const TABS: Tab[] = ['details', 'updates', 'attachments', 'related'];
/** Status moves offered in More Actions, by current status (the API's update statuses). */
const STATUS_ACTIONS: Record<string, { label: string; status: string }[]> = {
  Open: [{ label: 'Start work', status: 'In Progress' }, { label: 'Approve', status: 'Approved' }, { label: 'Put on hold', status: 'On Hold' }, { label: 'Close request', status: 'Closed' }, { label: 'Cancel request', status: 'Cancelled' }],
  'In Progress': [{ label: 'Put on hold', status: 'On Hold' }, { label: 'Close request', status: 'Closed' }, { label: 'Cancel request', status: 'Cancelled' }],
  'On Hold': [{ label: 'Resume work', status: 'In Progress' }, { label: 'Close request', status: 'Closed' }, { label: 'Cancel request', status: 'Cancelled' }],
  Approved: [{ label: 'Start work', status: 'In Progress' }, { label: 'Close request', status: 'Closed' }, { label: 'Cancel request', status: 'Cancelled' }],
  Closed: [{ label: 'Reopen', status: 'Open' }],
};

/** One service request on its own page: asset header, the request's tabs and a summary column. Data from /api/service-requests. */
@Component({
  selector: 'app-service-request',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './service-request.html',
  // shares the maintenance page's base styles (header, cards, tables, buttons, dialogs)
  styleUrls: [
    '../../asset-maintenance/asset-maintenance.css',
    '../../asset-maintenance/asset-maintenance-charts.css',
    '../../asset-maintenance/service-requests/service-requests.css',
    './service-request.css',
  ],
})
export class ServiceRequest implements OnDestroy {
  readonly icons = {
    ArrowDownUp, Boxes, Building2, CalendarDays, ChevronDown, ChevronLeft, ChevronRight, CircleCheck, EllipsisVertical, File, FileImage, FilePlus, FileSpreadsheet, FileText, Folder,
    LayoutGrid, Link2, List, MapPin, MessageSquare, Music, Package, Pencil, Plus, Search, TriangleAlert, Upload, Video, Wrench, X,
  };
  readonly typeIcons = ASSET_TYPE_ICONS;
  readonly tabs = TABS;
  categories: string[] = SR_API_REQUEST_TYPES;
  priorities: string[] = SR_API_PRIORITIES;
  readonly technicians = TECHNICIANS;
  readonly pageSizes = [10, 25, 50];
  readonly uploadAccept = UPLOAD_EXTS.map((e) => '.' + e).join(',');

  asset: AssetRecord = findAsset('GEN-00045') ?? ASSET_RECORDS[0];
  /** The asset's AssetRegistrationId, once known. */
  private assetId = '';
  requestId = '';
  tab: Tab = 'details';
  health: number | null = null;
  loading = false;
  busy = false;
  loadError = '';

  private dto: ServiceRequestDto | null = null;
  request: SrView | null = null;
  private apiUpdates: SrUpdateDto[] = [];
  private related: ServiceRequestRelatedDto | null = null;
  private assetComponents: ComponentRow[] = [];
  private assetWorkOrders: MaintenanceWorkOrderDto[] = [];

  // Updates
  updateFilter: '' | 'comment' | 'status' = '';
  comment = '';

  // Attachments
  fileSearch = '';
  kindFilter: FileKind | '' = '';
  byFilter = '';
  dateOrder: 'desc' | 'asc' = 'desc';
  fileView: 'list' | 'grid' = 'list';
  pageSize = 10;
  page = 0;
  readonly checked = new Set<string>();
  dragging = false;

  menuOpen = '';
  edit: EditForm | null = null;
  editError = '';
  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;
  private loadedKey = '';
  private readonly load$ = new Subject<{ tag: string | null; id: string | null }>();
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
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

    this.load$.pipe(switchMap((k) => this.loadAll(k.tag, k.id)), takeUntilDestroyed(destroyRef)).subscribe();

    combineLatest([this.route.paramMap, this.route.queryParamMap])
      .pipe(takeUntilDestroyed(destroyRef))
      .subscribe(([p, q]) => {
        const tag = q.get('tag');
        const id = p.get('id');
        const key = `${tag ?? ''}|${id ?? ''}`;
        if (key !== this.loadedKey) {
          this.loadedKey = key;
          this.requestId = id ?? '';
          this.resetFiles();
          this.load$.next({ tag, id });
        }
        const tab = q.get('tab') as Tab | null;
        this.tab = tab && TABS.includes(tab) ? tab : 'details';
      });
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
  }

  // ===== Loading =====

  /** Asset by tag, then the request (by id or number, or the asset's latest), then its updates and related items. */
  private loadAll(tag: string | null, id: string | null): Observable<unknown> {
    this.loading = true;
    this.loadError = '';
    this.setRequest(null);
    this.apiUpdates = [];
    this.related = null;
    this.assetComponents = [];
    this.assetWorkOrders = [];
    this.assetId = '';
    const fallback = findAsset(tag);
    if (fallback) this.setAssetRecord(fallback);

    const asset$ = tag
      ? this.assets.getByTag(tag, fallback).pipe(catchError(() => of(null)))
      : of(null);

    return asset$.pipe(
      tap((a) => {
        if (a) {
          this.assetId = a.registration.assetRegistrationId;
          this.setAssetRecord(a.record);
        }
      }),
      switchMap(() => {
        if (id) return this.api.find(id, this.assetId || undefined);
        if (!this.assetId) return of(null);
        return this.api.list({ assetRegistrationId: this.assetId, pageSize: 1 }).pipe(
          switchMap((res) => (res.items[0] ? this.api.get(res.items[0].id) : of(null))),
        );
      }),
      tap((dto) => {
        this.loading = false;
        this.setRequest(dto);
        if (dto) {
          this.requestId = dto.requestNumber;
          if (!this.assetId) {
            this.assetId = dto.asset.assetRegistrationId;
            this.setAssetRecord(this.recordFromRequest(dto, fallback));
          }
        }
      }),
      switchMap((dto) => (dto ? this.loadExtras(dto.id) : of(null))),
      catchError((err) => {
        this.loading = false;
        this.loadError = apiErrorMessage(err, 'Could not load the service request.');
        return of(null);
      }),
    );
  }

  /** Updates, related items, the asset's components and its work orders (each optional). */
  private loadExtras(apiId: string): Observable<unknown> {
    const assetId = this.assetId;
    return forkJoin({
      updates: this.api.getUpdates(apiId).pipe(catchError(() => of([] as SrUpdateDto[]))),
      related: this.api.getRelated(apiId).pipe(catchError(() => of(null))),
      components: assetId ? this.assets.getComponents(assetId).pipe(catchError(() => of([]))) : of([]),
      workOrders: assetId ? this.api.workOrdersOfAsset(assetId).pipe(catchError(() => of([] as MaintenanceWorkOrderDto[]))) : of([]),
    }).pipe(
      tap((r) => {
        if (this.dto?.id !== apiId) return;
        this.apiUpdates = r.updates;
        this.related = r.related;
        this.assetComponents = r.components.map((c) => ({ tag: c.componentTag, name: c.componentName, type: 'Component', relationship: `Part of ${this.asset.tag}` }));
        this.assetWorkOrders = r.workOrders;
      }),
    );
  }

  /** Re-reads updates and related items after a change. */
  private refreshExtras(): void {
    const id = this.dto?.id;
    if (!id) return;
    forkJoin({
      updates: this.api.getUpdates(id).pipe(catchError(() => of(this.apiUpdates))),
      related: this.api.getRelated(id).pipe(catchError(() => of(this.related))),
    }).subscribe((r) => {
      if (this.dto?.id !== id) return;
      this.apiUpdates = r.updates;
      this.related = r.related;
    });
  }

  private setAssetRecord(a: AssetRecord): void {
    const changed = a.tag !== this.asset.tag;
    this.asset = a;
    if (changed || this.health === null) {
      this.health = null;
      loadAssetDetail(a.tag).then((d) => {
        if (this.asset.tag === a.tag && d) this.health = d.health.score;
      });
    }
  }

  /** Header record for an asset known only from the request's copy of it. */
  private recordFromRequest(dto: ServiceRequestDto, fallback?: AssetRecord): AssetRecord {
    const a = dto.asset;
    return {
      id: fallback?.id ?? 0,
      name: a.assetName ?? fallback?.name ?? '',
      tag: a.assetTag ?? fallback?.tag ?? '',
      category: a.category ?? fallback?.category ?? '',
      type: fallback?.type ?? '',
      site: a.location ?? fallback?.site ?? '',
      area: fallback && !a.location ? fallback.area : '',
      custodian: fallback?.custodian ?? '',
      department: a.department ?? fallback?.department ?? '',
      status: (a.status ?? fallback?.status ?? 'Active') as AssetStatus,
      criticality: fallback?.criticality ?? 'Medium',
      manufacturer: fallback?.manufacturer ?? '',
      model: fallback?.model ?? '',
      nextMaintenance: fallback?.nextMaintenance ?? null,
      icon: fallback?.icon ?? iconForAsset(a.category, a.assetName),
      image: fileUrl(a.imageUrl) || fallback?.image || '',
    };
  }

  private setRequest(dto: ServiceRequestDto | null): void {
    this.dto = dto;
    this.request = dto ? this.toView(dto) : null;
  }

  private toView(d: ServiceRequestDto): SrView {
    return {
      id: d.requestNumber,
      apiId: d.id,
      title: d.title,
      description: d.description ?? '',
      category: d.requestType ?? '',
      priority: d.priority ?? '',
      status: d.status ?? '',
      createdAt: localStamp(d.requestedDate ?? d.submittedAt ?? d.createdAt),
      requestedBy: d.requestedBy?.name ?? d.createdBy ?? '',
      requesterDept: d.department?.name ?? '',
      assignedTo: d.assignedTo?.name ?? '',
      respondedAt: localStamp(d.actualStart),
      resolvedAt: localStamp(d.actualEnd),
      targetDate: isoDay(d.targetDate),
      workOrder: d.workOrder?.workOrderId ?? '',
      rating: 0,
      location: d.location?.name ?? undefined,
      downtimeHours: d.estimatedDowntimeHours ?? undefined,
      plannedStart: localStamp(d.plannedStart),
      plannedEnd: localStamp(d.plannedEnd),
      relatedRequest: d.relatedServiceRequest ?? undefined,
      tags: d.tags ?? [],
      reference: d.referenceNumber ?? undefined,
    };
  }

  private resetFiles(): void {
    this.fileSearch = '';
    this.kindFilter = '';
    this.byFilter = '';
    this.page = 0;
    this.checked.clear();
    this.comment = '';
  }

  get location(): string {
    return [this.asset.site, this.asset.area].filter(Boolean).join(' - ') || '-';
  }

  get workOrder(): WoRow | null {
    const w = this.related?.workOrders.find((x) => x.workOrderId === this.request?.workOrder) ?? this.dto?.workOrder ?? null;
    return w ? this.toWoRow(w) : null;
  }

  private toWoRow(w: SrWorkOrderDto): WoRow {
    return {
      id: w.workOrderId, date: isoDay(w.plannedStart), type: w.workType ?? '-', description: w.title ?? '', status: w.status ?? 'Open',
      technician: w.assignedTo ?? '-', priority: w.priority ?? undefined,
    };
  }

  // ===== Tabs =====

  tabLabel(t: Tab, r: SrView): string {
    switch (t) {
      case 'details': return 'Details';
      case 'updates': return `Updates (${this.apiUpdates.length || this.dto?.counts.updatesCount || 0})`;
      case 'attachments': return `Attachments (${this.attachments.length})`;
      case 'related': return `Related (${this.relatedCount})`;
    }
  }

  setTab(t: Tab): void {
    this.menuOpen = '';
    this.router.navigate([], { relativeTo: this.route, queryParams: { tab: t }, queryParamsHandling: 'merge', replaceUrl: true });
  }

  back(): void {
    this.router.navigate(['/assets/maintenance'], { queryParams: { tag: this.asset.tag, tab: 'requests' } });
  }

  goAsset(): void {
    this.router.navigate(['/assets/status-condition'], { queryParams: { tag: this.asset.tag } });
  }

  newRequest(): void {
    this.router.navigate(['/assets/service-requests/new'], { queryParams: { tag: this.asset.tag } });
  }

  /** Maintenance page, Work Orders tab; with an id that work order opens. */
  goWorkOrders(wo?: string): void {
    this.router.navigate(['/assets/maintenance'], { queryParams: { tag: this.asset.tag, tab: 'workOrders', ...(wo ? { wo } : {}) } });
  }

  goParts(): void {
    this.router.navigate(['/assets/maintenance'], { queryParams: { tag: this.asset.tag, tab: 'parts' } });
  }

  // ===== Formatting =====

  fmtDate(iso: string | undefined | null): string {
    return iso ? `${iso.slice(8, 10)} ${MONTHS[Number(iso.slice(5, 7)) - 1]} ${iso.slice(0, 4)}` : '-';
  }

  fmtDateTime(v: string | undefined | null): string {
    return v ? `${this.fmtDate(v.slice(0, 10))}${v.length > 10 ? ' ' + v.slice(11, 16) : ''}` : '-';
  }

  initials(name: string): string {
    return name.split(/\s+/).filter(Boolean).slice(0, 2).map((w) => w[0].toUpperCase()).join('');
  }

  /** Stable avatar colour per person. */
  avatarClass(name: string): string {
    let h = 0;
    for (const ch of name) h = (h * 31 + ch.charCodeAt(0)) >>> 0;
    return 'av--' + (h % 5);
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

  // ===== Updates =====

  private toRow(u: SrUpdateDto): UpdateRow {
    const kind: UpdateKind =
      u.updateType === 'Created' ? 'created'
        : u.updateType === 'Status Change' || u.updateType === 'Work Order' ? 'status'
          : !u.message && u.attachments?.length ? 'file' : 'comment';
    const text = [u.message, u.recommendedAction ? `Recommended action: ${u.recommendedAction}` : '', u.attachments?.length ? `Attachments: ${u.attachments.map((a) => a.fileName).join(', ')}` : '']
      .filter(Boolean).join(' · ') || u.action;
    return { at: localStamp(u.createdAt), by: u.userName || 'System', text, kind, action: u.action, status: u.status ?? null };
  }

  get updates(): UpdateRow[] {
    return this.apiUpdates
      .map((u) => this.toRow(u))
      .filter((u) => !this.updateFilter || (this.updateFilter === 'comment' ? u.kind === 'comment' || u.kind === 'file' : u.kind === 'status' || u.kind === 'created'));
  }

  updateIcon(kind: UpdateKind) {
    return { created: this.icons.FilePlus, status: this.icons.Wrench, comment: this.icons.MessageSquare, file: this.icons.FileText }[kind];
  }

  addComment(): void {
    const r = this.request;
    const text = this.comment.trim();
    if (!r || !text || this.busy) return;
    this.run(this.api.addUpdate(r.apiId, { message: text }), 'Comment added.', () => (this.comment = ''));
  }

  // ===== Attachments =====

  kindOf(name: string): FileKind {
    return KIND_BY_EXT[name.split('.').pop()?.toLowerCase() ?? ''] ?? 'File';
  }

  kindIcon(kind: FileKind) {
    return { Image: this.icons.FileImage, PDF: this.icons.FileText, Document: this.icons.FileText, Spreadsheet: this.icons.FileSpreadsheet, Audio: this.icons.Music, Video: this.icons.Video, File: this.icons.File }[kind];
  }

  get attachments(): AttachmentRow[] {
    const d = this.dto;
    const r = this.request;
    if (!d || !r) return [];
    return (d.attachments ?? []).map((a) => ({
      key: a.attachmentId,
      name: a.fileName,
      size: fmtFileSize(a.fileSize),
      description: a.description ?? '',
      url: fileUrl(a.fileUrl) || undefined,
      kind: this.kindOf(a.fileName),
      by: a.uploadedBy || r.requestedBy,
      at: localStamp(a.uploadedAt) || r.createdAt,
    }));
  }

  get kinds(): FileKind[] {
    return [...new Set(this.attachments.map((a) => a.kind))].sort();
  }

  get uploaders(): string[] {
    return [...new Set(this.attachments.map((a) => a.by))].sort();
  }

  get fileRows(): AttachmentRow[] {
    const q = this.fileSearch.trim().toLowerCase();
    const dir = this.dateOrder === 'desc' ? -1 : 1;
    return this.attachments
      .filter((a) => (!this.kindFilter || a.kind === this.kindFilter) && (!this.byFilter || a.by === this.byFilter))
      .filter((a) => !q || `${a.name} ${a.kind} ${a.description ?? ''}`.toLowerCase().includes(q))
      .sort((a, b) => dir * a.at.localeCompare(b.at));
  }

  get filePage(): AttachmentRow[] {
    return this.fileRows.slice(this.page * this.pageSize, (this.page + 1) * this.pageSize);
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.fileRows.length / this.pageSize));
  }

  get pages(): number[] {
    return Array.from({ length: this.pageCount }, (_, i) => i);
  }

  get showing(): string {
    const n = this.fileRows.length;
    return n ? `Showing ${this.page * this.pageSize + 1} to ${Math.min(n, (this.page + 1) * this.pageSize)} of ${n} records` : 'No records';
  }

  onFileFilter(): void {
    this.page = 0;
  }

  get allChecked(): boolean {
    return this.filePage.length > 0 && this.filePage.every((a) => this.checked.has(a.key));
  }

  toggleAll(on: boolean): void {
    for (const a of this.filePage) on ? this.checked.add(a.key) : this.checked.delete(a.key);
  }

  toggleCheck(key: string, on: boolean): void {
    on ? this.checked.add(key) : this.checked.delete(key);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragging = false;
    if (event.dataTransfer?.files.length) this.addFiles(event.dataTransfer.files);
  }

  onPick(input: HTMLInputElement): void {
    if (input.files?.length) this.addFiles(input.files);
    input.value = '';
  }

  /** Uploads the files through the Media service, then attaches them to the request. */
  private addFiles(files: FileList): void {
    const r = this.request;
    if (!r || this.busy) return;
    const skipped: string[] = [];
    const ok: File[] = [];
    for (const f of Array.from(files)) {
      const ext = f.name.split('.').pop()?.toLowerCase() ?? '';
      if (!UPLOAD_EXTS.includes(ext) || f.size > MAX_UPLOAD) skipped.push(f.name);
      else ok.push(f);
    }
    if (!ok.length) return this.showToast(`Not added (type or size): ${skipped.join(', ')}.`);
    this.busy = true;
    const failed: string[] = [];
    forkJoin(ok.map((f) => this.api.upload(f).pipe(catchError(() => (failed.push(f.name), of(null))))))
      .pipe(
        map((list) => list.filter((x): x is SrAttachmentRequest => !!x)),
        switchMap((list) => (list.length ? this.api.addAttachments(r.apiId, list).pipe(map((dto) => ({ dto, n: list.length }))) : of({ dto: null, n: 0 }))),
      )
      .subscribe({
        next: ({ dto, n }) => {
          this.busy = false;
          if (dto) this.applyDto(dto);
          this.showToast([
            n ? `${n} file${n > 1 ? 's' : ''} attached.` : '',
            skipped.length ? `Not added (type or size): ${skipped.join(', ')}.` : '',
            failed.length ? `Upload failed: ${failed.join(', ')}.` : '',
          ].filter(Boolean).join(' '));
        },
        error: (err) => this.fail(err),
      });
  }

  openFile(a: { name: string; url?: string }): void {
    this.menuOpen = '';
    if (a.url) window.open(a.url, '_blank', 'noopener');
    else this.showToast(`${a.name} has no file link.`);
  }

  removeFile(a: AttachmentRow): void {
    this.menuOpen = '';
    const r = this.request;
    if (!r || !confirm(`Remove ${a.name}?`)) return;
    this.run(this.api.deleteAttachments(r.apiId, [a.key]), `${a.name} removed.`, () => {
      this.checked.delete(a.key);
      if (this.page >= this.pageCount) this.page = this.pageCount - 1;
    });
  }

  removeChecked(): void {
    this.menuOpen = '';
    const r = this.request;
    if (!r || !this.checked.size || !confirm(`Remove ${this.checked.size} attachment(s)?`)) return;
    this.run(this.api.deleteAttachments(r.apiId, [...this.checked]), 'Attachments removed.', () => {
      this.checked.clear();
      this.page = 0;
    });
  }

  // ===== Related =====

  get relatedWorkOrders(): WoRow[] {
    const list = this.related?.workOrders ?? (this.dto?.workOrder ? [this.dto.workOrder] : []);
    return list.map((w) => this.toWoRow(w));
  }

  /** Work orders that could still be linked: open ones on this asset in the Maintenance service. */
  get linkableWorkOrders(): WoRow[] {
    const linked = new Set(this.relatedWorkOrders.map((w) => w.id.toLowerCase()));
    return this.assetWorkOrders
      .filter((w) => !/completed|closed|cancel/i.test(w.status ?? '') && !linked.has(w.workOrderId.toLowerCase()))
      .map((w) => ({ id: w.workOrderId, date: isoDay(w.scheduledDate), type: w.workType, description: w.title, status: w.status ?? 'Open', technician: w.assignedTechnician ?? '-', priority: w.priority }));
  }

  /** Assets and components linked to the request; the asset's own components when none are linked. */
  get components(): ComponentRow[] {
    const linked = this.related?.relatedAssets ?? [];
    if (linked.length) return linked.map((x) => ({ tag: x.number ?? x.referenceId, name: x.name ?? '-', type: x.type, relationship: x.relationship ?? '-' }));
    return this.assetComponents;
  }

  get relatedParts(): { partNo: string; name: string; qty: number; date: string; wo: string }[] {
    return (this.related?.spareParts ?? []).map((p) => ({ partNo: p.partId, name: p.partName ?? p.partId, qty: p.quantityUsed, date: isoDay(p.usedDate), wo: p.workOrderId ?? '' }));
  }

  get relatedPartsQty(): number {
    return this.relatedParts.reduce((s, p) => s + p.qty, 0);
  }

  /** Request attachments and linked asset documents. */
  get relatedDocs(): DocRow[] {
    const docs = this.related?.documents;
    if (!docs) return this.attachments.map((a) => ({ name: a.name, kind: a.kind, at: a.at, by: a.by, relatedTo: 'Service Request', url: a.url }));
    return docs.map((d) => ({
      name: d.documentName ?? '-', kind: this.kindOf(d.documentName ?? ''), at: localStamp(d.uploadedAt), by: d.uploadedBy ?? '-', relatedTo: d.relatedTo, url: fileUrl(d.fileUrl) || undefined,
    }));
  }

  get relatedCount(): number {
    return this.related?.counts.relatedCount ?? this.dto?.counts.relatedCount ?? 0;
  }

  linkWorkOrder(w: WoRow): void {
    const r = this.request;
    this.menuOpen = '';
    if (!r) return;
    this.run(this.api.linkWorkOrder(r.apiId, w.id), `${r.id} linked to ${w.id}.`);
  }

  unlinkWorkOrder(id?: string): void {
    const r = this.request;
    this.menuOpen = '';
    const wo = id ?? r?.workOrder;
    if (!r || !wo) return;
    this.run(this.api.unlinkWorkOrder(r.apiId, wo), `${wo} unlinked.`);
  }

  // ===== Actions =====

  /** Statuses More Actions can move the request to. */
  statusActions(r: SrView): { label: string; status: string }[] {
    return STATUS_ACTIONS[r.status] ?? [];
  }

  /** Locked statuses: the API refuses edits on closed or cancelled requests. */
  isLocked(r: SrView): boolean {
    return r.status === 'Closed' || r.status === 'Cancelled';
  }

  /** Creates the work order in the Maintenance service, then links it to the request (which moves to In Progress). */
  convert(): void {
    const r = this.request;
    const d = this.dto;
    this.menuOpen = '';
    if (!r || !d || this.busy) return;
    // stays on this page: the work order shows under Related
    if (r.workOrder) return this.setTab('related');
    if (r.status === 'Draft') return this.showToast('Submit the request before converting it to a work order.');
    const now = new Date();
    const workOrderId = `WO-${now.getFullYear()}-${String(now.getTime()).slice(-6)}`;
    const assetId = this.assetId || d.asset.assetRegistrationId;
    const create$ = this.api.createWorkOrder({
      workOrderId, assetId, assetName: d.asset.assetName, workType: d.requestType || 'Corrective', title: d.title, description: d.description,
      priority: d.priority || 'Medium', scheduledDate: d.plannedStart ?? d.targetDate, assignedTechnician: d.assignedTo?.name ?? null,
      createdBy: this.currentUser, status: 'Open',
    });
    const flow$ = create$.pipe(
      switchMap((wo) => this.api.convertToWorkOrder(r.apiId, wo.workOrderId || workOrderId, false)),
      switchMap((dto) => (dto.status === 'Open' ? this.api.addUpdate(dto.id, { status: 'In Progress', message: `Work started on ${workOrderId}.` }) : of(dto))),
    );
    this.run(flow$, `${workOrderId} created from ${r.id} and linked.`, () => this.setTab('related'));
  }

  setStatus(to: string): void {
    const r = this.request;
    this.menuOpen = '';
    if (!r || this.busy) return;
    if (to === 'Cancelled' && !confirm(`Cancel service request ${r.id}? A cancelled request can no longer be changed.`)) return;
    this.run(this.api.addUpdate(r.apiId, { status: to, message: `Status changed to ${to}.` }), `${r.id} is now ${to}.`);
  }

  /** Submits a draft (status Open). */
  submitDraft(): void {
    const r = this.request;
    this.menuOpen = '';
    if (!r || this.busy) return;
    this.run(this.api.submit(r.apiId), `${r.id} submitted.`);
  }

  /** Deletes a draft; a submitted request is cancelled instead. */
  remove(): void {
    const r = this.request;
    this.menuOpen = '';
    if (!r) return;
    if (r.status !== 'Draft') return this.setStatus('Cancelled');
    if (!confirm(`Delete service request ${r.id}?`)) return;
    this.busy = true;
    this.api.delete(r.apiId).subscribe({
      next: () => {
        this.busy = false;
        this.back();
      },
      error: (err) => this.fail(err),
    });
  }

  copyLink(): void {
    this.menuOpen = '';
    navigator.clipboard?.writeText(location.href).then(() => this.showToast('Link copied.'), () => this.showToast('Could not copy the link.'));
  }

  print(): void {
    this.menuOpen = '';
    window.print();
  }

  openEdit(): void {
    const r = this.request;
    this.menuOpen = '';
    if (!r || this.isLocked(r)) return;
    this.editError = '';
    this.edit = { title: r.title, description: r.description, category: r.category, priority: r.priority, assignedTo: r.assignedTo, targetDate: r.targetDate };
  }

  /** Assignable people: the fixed technicians plus whoever the request is assigned to now. */
  get assignees(): string[] {
    const current = this.request?.assignedTo;
    return current && !TECHNICIANS.includes(current) ? [current, ...TECHNICIANS] : TECHNICIANS;
  }

  saveEdit(): void {
    const r = this.request;
    const d = this.dto;
    const f = this.edit;
    if (!r || !d || !f || this.busy) return;
    if (!f.title.trim()) return void (this.editError = 'Enter the subject.');
    if (!f.targetDate) return void (this.editError = 'Enter the target date.');
    const assigned = !!f.assignedTo && f.assignedTo !== r.assignedTo;
    const body = toSaveBody(d);
    body.title = f.title.trim();
    body.description = f.description.trim() || null;
    body.requestType = f.category || null;
    body.priority = f.priority || null;
    body.targetDate = f.targetDate;
    body.assignedTo = f.assignedTo
      ? f.assignedTo === d.assignedTo?.name ? body.assignedTo : { userId: null, name: f.assignedTo, role: 'Technician' }
      : null;
    const save$ = this.api.update(r.apiId, body, r.status === 'Draft').pipe(
      switchMap((dto) => (assigned && dto.status === 'Open' ? this.api.addUpdate(dto.id, { status: 'In Progress', message: `Assigned to ${f.assignedTo}.` }) : of(dto))),
    );
    this.busy = true;
    this.editError = '';
    save$.subscribe({
      next: (dto) => {
        this.busy = false;
        this.applyDto(dto);
        this.edit = null;
        this.showToast(`${r.id} updated.`);
      },
      error: (err) => {
        this.busy = false;
        this.editError = apiErrorMessage(err, 'Could not save the request.');
      },
    });
  }

  // ===== API helpers =====

  private get currentUser(): string | null {
    return this.auth.getUser()?.userName || this.auth.getUserEmail();
  }

  private applyDto(dto: ServiceRequestDto): void {
    this.setRequest(dto);
    this.refreshExtras();
  }

  /** Runs a call that returns the updated request, then shows it. */
  private run(call$: Observable<ServiceRequestDto>, done: string, after?: () => void): void {
    this.busy = true;
    call$.subscribe({
      next: (dto) => {
        this.busy = false;
        this.applyDto(dto);
        after?.();
        this.showToast(done);
      },
      error: (err) => this.fail(err),
    });
  }

  private fail(err: unknown): void {
    this.busy = false;
    this.showToast(apiErrorMessage(err));
  }

  // ===== Menus & toast =====

  toggleMenu(name: string, event: Event): void {
    event.stopPropagation();
    this.menuOpen = this.menuOpen === name ? '' : name;
  }

  @HostListener('document:click')
  closeMenus(): void {
    this.menuOpen = '';
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.menuOpen = '';
    this.edit = null;
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 4000);
  }
}
