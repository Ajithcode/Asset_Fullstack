import { Component, DestroyRef, HostListener, OnDestroy, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { combineLatest } from 'rxjs';
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
import { ASSET_RECORDS, ASSET_TYPE_ICONS, AssetRecord, TODAY, assetLocation, findAsset, toIsoDate } from '../../../../../assets/asset-data';
import { RelatedPart, RelationshipLink, loadAssetDetail } from '../../../../../assets/asset-details/asset-details';
import { MaintenanceData, WorkOrder, dataFor } from '../../../../../assets/asset-maintenance/asset-maintenance';
import { SR_CATEGORIES, SR_PRIORITIES, SrCategory, SrPriority, SrStatus, ServiceRequest as SrRecord } from '../../../../../assets/asset-maintenance/maintenance-sr';
import { lineCost } from '../../../../../assets/asset-maintenance/maintenance-parts';

type Tab = 'details' | 'updates' | 'attachments' | 'related';
type Attachment = SrRecord['attachments'][number];
type FileKind = 'Image' | 'PDF' | 'Document' | 'Spreadsheet' | 'Audio' | 'Video' | 'File';
type UpdateKind = 'created' | 'status' | 'comment' | 'file';

interface UpdateRow {
  at: string;
  by: string;
  text: string;
  kind: UpdateKind;
  action: string;
  /** Status the request moved to with this update, if any. */
  status: SrStatus | null;
}

interface AttachmentRow extends Attachment {
  kind: FileKind;
  by: string;
  at: string;
}

interface EditForm {
  title: string;
  description: string;
  category: SrCategory;
  priority: SrPriority;
  assignedTo: string;
  targetDate: string;
}

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const CURRENT_USER = 'Rex Kumar';
const TECHNICIANS = ['Ahmed Khan', 'Ravi Patel', 'Nisha Patel', 'Ramesh Kumar', 'Salim Ahmed', 'Khalid Mansoori', 'Priya Nair'];
const KIND_BY_EXT: Record<string, FileKind> = {
  jpg: 'Image', jpeg: 'Image', png: 'Image', gif: 'Image', webp: 'Image', pdf: 'PDF', doc: 'Document', docx: 'Document', txt: 'Document',
  xls: 'Spreadsheet', xlsx: 'Spreadsheet', csv: 'Spreadsheet', m4a: 'Audio', mp3: 'Audio', wav: 'Audio', mp4: 'Video', mov: 'Video',
};
const UPLOAD_EXTS = ['pdf', 'jpg', 'jpeg', 'png', 'doc', 'docx', 'xls', 'xlsx', 'mp4'];
const MAX_UPLOAD = 50 * 1024 * 1024;
const TABS: Tab[] = ['details', 'updates', 'attachments', 'related'];

/** One service request on its own page: asset header, the request's tabs and a summary column. */
@Component({
  selector: 'app-service-request',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './service-request.html',
  // shares the maintenance page's base styles (header, cards, tables, buttons, dialogs)
  styleUrls: [
    '../../../../../assets/asset-maintenance/asset-maintenance.css',
    '../../../../../assets/asset-maintenance/asset-maintenance-charts.css',
    '../../../../../assets/asset-maintenance/service-requests/service-requests.css',
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
  readonly categories = SR_CATEGORIES;
  readonly priorities = SR_PRIORITIES;
  readonly technicians = TECHNICIANS;
  readonly pageSizes = [10, 25, 50];
  readonly uploadAccept = UPLOAD_EXTS.map((e) => '.' + e).join(',');

  asset: AssetRecord = findAsset('GEN-00045') ?? ASSET_RECORDS[0];
  data: MaintenanceData = dataFor(this.asset);
  requestId = '';
  tab: Tab = 'details';
  health: number | null = null;
  components: { link: RelationshipLink; part: RelatedPart | undefined }[] = [];
  private loadedTag = '';

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
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  constructor() {
    combineLatest([this.route.paramMap, this.route.queryParamMap])
      .pipe(takeUntilDestroyed(inject(DestroyRef)))
      .subscribe(([p, q]) => {
        const asset = findAsset(q.get('tag')) ?? this.asset;
        if (asset.tag !== this.loadedTag) this.setAsset(asset);
        const id = p.get('id') ?? this.latestId;
        if (id !== this.requestId) this.resetFiles();
        this.requestId = id;
        const tab = q.get('tab') as Tab | null;
        this.tab = tab && TABS.includes(tab) ? tab : 'details';
      });
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
  }

  private setAsset(a: AssetRecord): void {
    this.asset = a;
    this.loadedTag = a.tag;
    this.data = dataFor(a);
    this.health = null;
    this.components = [];
    loadAssetDetail(a.tag).then((d) => {
      if (this.asset.tag !== a.tag || !d) return;
      this.health = d.health.score;
      const parts = new Map(d.relationships.parts.map((x) => [x.tag, x]));
      this.components = d.relationships.links.filter((l) => l.type === 'Component' && l.status === 'Active').map((link) => ({ link, part: parts.get(link.tag) }));
    });
  }

  private resetFiles(): void {
    this.fileSearch = '';
    this.kindFilter = '';
    this.byFilter = '';
    this.page = 0;
    this.checked.clear();
    this.comment = '';
  }

  /** Most recently raised request of the asset (opened from the sidebar, where no id is given). */
  private get latestId(): string {
    return this.data.serviceRequests.reduce<SrRecord | null>((a, r) => (!a || r.createdAt > a.createdAt ? r : a), null)?.id ?? '';
  }

  get request(): SrRecord | null {
    return this.data.serviceRequests.find((r) => r.id === this.requestId) ?? null;
  }

  get location(): string {
    return assetLocation(this.asset);
  }

  get workOrder(): WorkOrder | null {
    const id = this.request?.workOrder;
    return (id && this.data.workOrders.find((w) => w.id === id)) || null;
  }

  // ===== Tabs =====

  tabLabel(t: Tab, r: SrRecord): string {
    switch (t) {
      case 'details': return 'Details';
      case 'updates': return `Updates (${r.updates.length})`;
      case 'attachments': return `Attachments (${r.attachments.length})`;
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

  fmtDate(iso: string | undefined): string {
    return iso ? `${iso.slice(8, 10)} ${MONTHS[Number(iso.slice(5, 7)) - 1]} ${iso.slice(0, 4)}` : '-';
  }

  fmtDateTime(v: string | undefined): string {
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

  // ===== Updates =====

  /** Updates are stored as text; the kind and resulting status are read from it. */
  private toRow(u: SrRecord['updates'][number], first: boolean): UpdateRow {
    const t = u.text.toLowerCase();
    if (first || t.startsWith('service request created')) return { ...u, kind: 'created', action: 'Created the Service Request', status: 'Open' };
    if (t.startsWith('attachment added')) return { ...u, kind: 'file', action: 'Added Attachment', status: null };
    const status: SrStatus | null = t.startsWith('resolved') ? 'Resolved' : t.startsWith('closed') ? 'Closed' : t.startsWith('reopened') ? 'Open'
      : t.startsWith('assigned') || t.startsWith('converted') ? 'In Progress' : null;
    return status ? { ...u, kind: 'status', action: 'Updated Status', status } : { ...u, kind: 'comment', action: 'Added Comment', status: null };
  }

  get updates(): UpdateRow[] {
    const r = this.request;
    if (!r) return [];
    return r.updates
      .map((u, i) => this.toRow(u, i === 0))
      .filter((u) => !this.updateFilter || (this.updateFilter === 'comment' ? u.kind === 'comment' : u.kind === 'status' || u.kind === 'created'));
  }

  updateIcon(kind: UpdateKind) {
    return { created: this.icons.FilePlus, status: this.icons.Wrench, comment: this.icons.MessageSquare, file: this.icons.FileText }[kind];
  }

  addComment(): void {
    const r = this.request;
    const text = this.comment.trim();
    if (!r || !text) return;
    this.log(r, text);
    this.comment = '';
    this.showToast('Comment added.');
  }

  private log(r: SrRecord, text: string): void {
    r.updates.push({ at: this.nowStamp(), by: CURRENT_USER, text });
  }

  private nowStamp(): string {
    const d = new Date();
    const p = (n: number) => String(n).padStart(2, '0');
    return `${toIsoDate(d)}T${p(d.getHours())}:${p(d.getMinutes())}`;
  }

  // ===== Attachments =====

  kindOf(name: string): FileKind {
    return KIND_BY_EXT[name.split('.').pop()?.toLowerCase() ?? ''] ?? 'File';
  }

  kindIcon(kind: FileKind) {
    return { Image: this.icons.FileImage, PDF: this.icons.FileText, Document: this.icons.FileText, Spreadsheet: this.icons.FileSpreadsheet, Audio: this.icons.Music, Video: this.icons.Video, File: this.icons.File }[kind];
  }

  /** Files raised with the request carry no uploader: they came from the requester. */
  get attachments(): AttachmentRow[] {
    const r = this.request;
    return r ? r.attachments.map((a) => ({ ...a, kind: this.kindOf(a.name), by: a.by ?? r.requestedBy, at: a.at ?? r.createdAt })) : [];
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
    return this.filePage.length > 0 && this.filePage.every((a) => this.checked.has(a.name));
  }

  toggleAll(on: boolean): void {
    for (const a of this.filePage) on ? this.checked.add(a.name) : this.checked.delete(a.name);
  }

  toggleCheck(name: string, on: boolean): void {
    on ? this.checked.add(name) : this.checked.delete(name);
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

  private addFiles(files: FileList): void {
    const r = this.request;
    if (!r) return;
    const skipped: string[] = [];
    let added = 0;
    for (const f of Array.from(files)) {
      const ext = f.name.split('.').pop()?.toLowerCase() ?? '';
      if (!UPLOAD_EXTS.includes(ext) || f.size > MAX_UPLOAD) {
        skipped.push(f.name);
        continue;
      }
      // object URL lives for the session, like the attachment itself
      const url = URL.createObjectURL(f);
      r.attachments.push({ name: this.uniqueName(r, f.name), size: this.fmtSize(f.size), by: CURRENT_USER, at: this.nowStamp(), description: '', url });
      this.log(r, `Attachment added: ${f.name}.`);
      added++;
    }
    this.showToast([added ? `${added} file${added > 1 ? 's' : ''} attached.` : '', skipped.length ? `Not added (type or size): ${skipped.join(', ')}.` : ''].filter(Boolean).join(' '));
  }

  private uniqueName(r: SrRecord, name: string): string {
    if (!r.attachments.some((a) => a.name === name)) return name;
    const dot = name.lastIndexOf('.');
    const [base, ext] = dot > 0 ? [name.slice(0, dot), name.slice(dot)] : [name, ''];
    let n = 2;
    while (r.attachments.some((a) => a.name === `${base} (${n})${ext}`)) n++;
    return `${base} (${n})${ext}`;
  }

  private fmtSize(bytes: number): string {
    return bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
  }

  openFile(a: Attachment): void {
    this.menuOpen = '';
    if (a.url) window.open(a.url, '_blank', 'noopener');
    else this.showToast(`${a.name} is not available for preview in demo mode.`);
  }

  removeFile(a: Attachment): void {
    this.menuOpen = '';
    const r = this.request;
    if (!r || !confirm(`Remove ${a.name}?`)) return;
    r.attachments = r.attachments.filter((x) => x.name !== a.name);
    this.checked.delete(a.name);
    if (this.page >= this.pageCount) this.page = this.pageCount - 1;
  }

  removeChecked(): void {
    this.menuOpen = '';
    const r = this.request;
    if (!r || !this.checked.size || !confirm(`Remove ${this.checked.size} attachment(s)?`)) return;
    r.attachments = r.attachments.filter((x) => !this.checked.has(x.name));
    this.checked.clear();
    this.page = 0;
  }

  // ===== Related =====

  get relatedWorkOrders(): WorkOrder[] {
    const w = this.workOrder;
    return w ? [w] : [];
  }

  /** Work orders that could still be linked: open ones on this asset. */
  get linkableWorkOrders(): WorkOrder[] {
    return this.data.workOrders.filter((w) => w.status !== 'Completed' && w.id !== this.request?.workOrder);
  }

  get relatedParts(): { partNo: string; name: string; qty: number; date: string; wo: string; cost: number }[] {
    const wo = this.request?.workOrder;
    if (!wo) return [];
    const names = new Map(this.data.spareParts.map((p) => [p.partNo, p.name]));
    return this.data.partsUsage.filter((u) => u.wo === wo).map((u) => ({ partNo: u.partNo, name: names.get(u.partNo) ?? u.partNo, qty: u.qty, date: u.date, wo: u.wo, cost: lineCost(u) }));
  }

  get relatedCount(): number {
    return this.relatedWorkOrders.length + this.components.length + this.relatedParts.length + (this.request?.attachments.length ?? 0);
  }

  linkWorkOrder(w: WorkOrder): void {
    const r = this.request;
    this.menuOpen = '';
    if (!r) return;
    r.workOrder = w.id;
    if (!r.assignedTo) r.assignedTo = w.technician;
    if (!r.respondedAt) r.respondedAt = this.nowStamp();
    if (r.status === 'Open') r.status = 'In Progress';
    this.log(r, `Converted to ${w.id}.`);
    this.showToast(`${r.id} linked to ${w.id}.`);
  }

  unlinkWorkOrder(): void {
    const r = this.request;
    this.menuOpen = '';
    if (!r?.workOrder) return;
    this.log(r, `Unlinked from ${r.workOrder}.`);
    r.workOrder = '';
  }

  // ===== Actions =====

  convert(): void {
    const r = this.request;
    this.menuOpen = '';
    if (!r) return;
    // stays on this page: the work order shows under Related
    if (r.workOrder) return this.setTab('related');
    const n = Math.max(0, ...this.data.workOrders.map((w) => Number(w.id.split('-')[2]) || 0)) + 1;
    const id = `WO-${this.data.year}-${String(n).padStart(3, '0')}`;
    const technician = r.assignedTo || TECHNICIANS[0];
    this.data.workOrders = [
      { id, date: toIsoDate(TODAY), type: 'Corrective', description: r.title, labor: 0, parts: 0, service: 0, downtimeMin: 0, reason: '', status: 'Open', technician, priority: r.priority },
      ...this.data.workOrders,
    ];
    r.workOrder = id;
    r.assignedTo = technician;
    if (!r.respondedAt) r.respondedAt = this.nowStamp();
    if (r.status === 'Open') r.status = 'In Progress';
    this.log(r, `Converted to ${id}.`);
    this.setTab('related');
    this.showToast(`${id} created from ${r.id} and linked.`);
  }

  setStatus(to: 'Resolved' | 'Closed'): void {
    const r = this.request;
    this.menuOpen = '';
    if (!r) return;
    const at = this.nowStamp();
    if (!r.respondedAt) r.respondedAt = at;
    if (!r.resolvedAt) r.resolvedAt = at;
    r.status = to;
    this.log(r, `${to}.`);
    this.showToast(`${r.id} marked ${to.toLowerCase()}.`);
  }

  reopen(): void {
    const r = this.request;
    this.menuOpen = '';
    if (!r) return;
    r.status = r.assignedTo ? 'In Progress' : 'Open';
    r.resolvedAt = '';
    this.log(r, 'Reopened.');
    this.showToast(`${r.id} reopened.`);
  }

  remove(): void {
    const r = this.request;
    this.menuOpen = '';
    if (!r || !confirm(`Delete service request ${r.id}?`)) return;
    this.data.serviceRequests.splice(this.data.serviceRequests.indexOf(r), 1);
    this.back();
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
    if (!r) return;
    this.editError = '';
    this.edit = { title: r.title, description: r.description, category: r.category, priority: r.priority, assignedTo: r.assignedTo, targetDate: r.targetDate };
  }

  saveEdit(): void {
    const r = this.request;
    const f = this.edit;
    if (!r || !f) return;
    if (!f.title.trim()) return void (this.editError = 'Enter the subject.');
    if (!f.targetDate) return void (this.editError = 'Enter the target date.');
    const assigned = f.assignedTo && f.assignedTo !== r.assignedTo;
    Object.assign(r, { title: f.title.trim(), description: f.description.trim(), category: f.category, priority: f.priority, assignedTo: f.assignedTo, targetDate: f.targetDate });
    if (assigned) {
      if (!r.respondedAt) r.respondedAt = this.nowStamp();
      if (r.status === 'Open') r.status = 'In Progress';
      this.log(r, `Assigned to ${f.assignedTo}.`);
    }
    this.log(r, 'Request details updated.');
    this.edit = null;
    this.showToast(`${r.id} updated.`);
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
