import { Component, DestroyRef, HostListener, OnDestroy, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import {
  LucideAngularModule,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  CloudUpload,
  Download,
  EllipsisVertical,
  ExternalLink,
  Eye,
  FileImage,
  FileStack,
  FileText,
  History,
  LayoutGrid,
  List,
  Maximize2,
  Minus,
  Pencil,
  Plus,
  Printer,
  RotateCw,
  Search,
  Trash2,
  Upload,
  X,
} from 'lucide-angular';
import {
  ASSET_TYPE_ICONS,
  AssetHeader,
  AssetTabLink,
  assetTabs,
  conditionPill,
  criticalityPill,
  statusPill,
} from 'shared-ui';
import { Observable, Subscription, finalize, of, switchMap } from 'rxjs';
import { apiErrorMessage } from '../services/asset-api';
import { AssetLookupService, AssetOption } from './asset-lookup.service';
import { ApiDocVersion, ApiDocument, AssetDocumentsService, DocumentFormValue, StoredFile } from './asset-documents.service';

type DocStatus = 'Active' | 'Expired' | 'Draft';
type ViewMode = 'list' | 'grid';

interface UploadForm {
  mode: 'new' | 'version';
  /** Assets that receive a new document (each gets its own copy). */
  assets: string[];
  /** Document receiving a new version (mode 'version'). */
  docId: number | null;
  file: File | null;
  name: string;
  type: string;
  stage: string;
  version: string;
  expiryDate: string;
  draft: boolean;
  description: string;
  tags: string;
  note: string;
}

interface EditForm {
  id: number;
  name: string;
  type: string;
  stage: string;
  expiryDate: string;
  draft: boolean;
  description: string;
  tags: string;
}

const MAX_FILE_BYTES = 25 * 1024 * 1024;
const ACCEPTED_EXTENSIONS = ['pdf', 'jpg', 'jpeg', 'png', 'doc', 'docx', 'xls', 'xlsx'];
const ZOOM_MIN = 50;
const ZOOM_MAX = 200;
const ZOOM_STEP = 10;

/** Pill colour per lifecycle stage (see .stage--* in the stylesheet). */
const STAGE_TONES: Record<string, string> = {
  Procurement: 'rose',
  Installation: 'blue',
  Commissioning: 'violet',
  'In Operation': 'violet',
  Operations: 'violet',
  Warranty: 'violet',
  Maintenance: 'amber',
  Compliance: 'amber',
  Decommissioning: 'red',
  Disposal: 'red',
};

const MB = 1024 * 1024;

@Component({
  selector: 'app-asset-documents',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './asset-documents.html',
  // Split to keep each stylesheet under the anyComponentStyle budget
  styleUrls: ['./asset-documents.css', './asset-documents-preview.css', './asset-documents-all.css'],
})
export class AssetDocuments implements OnDestroy {
  readonly icons = {
    ChevronDown, ChevronLeft, ChevronRight, CloudUpload, Download, EllipsisVertical, ExternalLink, Eye, FileImage,
    FileStack, FileText, History, LayoutGrid, List, Maximize2, Minus, Pencil, Plus, Printer, RotateCw, Search, Trash2,
    Upload, X,
  };

  /** The asset named by ?tag=; when it is missing or unknown the page shows a "not found" message. */
  asset: AssetHeader = EMPTY_HEADER;
  /** The registered asset behind `asset` (per-asset page). */
  private currentAsset: AssetOption | null = null;
  private loadSub?: Subscription;
  notFound = false;
  requestedTag = '';
  readonly tabs: AssetTabLink[] = assetTabs('Documents');
  readonly typeIcons = ASSET_TYPE_ICONS;
  /** Picture drawn on the mock-up pages of the sample manuals: the asset's photo. */
  sampleImage = '';
  /** True while the asset's documents are being fetched. */
  loading = false;
  /** Shown in the list when the documents could not be loaded. */
  loadError = '';
  /** True while an upload, edit or delete is being saved. */
  saving = false;
  readonly statusPill = statusPill;
  readonly conditionPill = conditionPill;
  readonly criticalityPill = criticalityPill;

  readonly documentTypes = [
    'Purchase Order', 'Invoice', 'Delivery Note', 'Goods Receipt', 'Installation Report', 'Commissioning', 'User Manual',
    'Technical Document', 'Drawing', 'Maintenance Manual', 'Warranty', 'Service Contract', 'Inspection Report',
    'Overhaul Report', 'Compliance', 'Image', 'Handover', 'Licence', 'Work Order', 'Fault Report', 'Quotation', 'Checkout Form',
    'Decommissioning', 'Disposal', 'Other',
  ];
  readonly stages = Object.keys(STAGE_TONES);
  readonly statuses: DocStatus[] = ['Active', 'Expired', 'Draft'];
  readonly pageSizes = [10, 20, 50];

  documents: ApiDocument[] = [];

  /** Asset Documents menu page: documents of every asset, with their asset name and ID. */
  allAssets = false;
  /** Registered assets for the asset filter and the upload dialog's asset picker. */
  assetOptions: AssetOption[] = [];
  assetFilter = '';
  /** Asset dropdown in the upload dialog. */
  assetPickerOpen = false;
  assetPickerSearch = '';

  // ===== Filters, view, paging, selection =====
  search = '';
  typeFilter = '';
  stageFilter = '';
  statusFilter: DocStatus | '' = '';
  view: ViewMode = 'list';
  pageSize = 20;
  page = 1;
  readonly checked = new Set<number>();

  // ===== Menus =====
  actionsOpen = false;
  bulkOpen = false;
  /** Id of the document whose row menu is open. */
  rowMenuId: number | null = null;
  viewerMenuOpen = false;

  // ===== Preview =====
  previewOpen = true;
  previewId: number | null = null;
  previewPage = 1;
  zoom = 100;
  rotation = 0;
  /** Blob URL of the previewed document's real file, if it has one. */
  private fileUrl: string | null = null;
  fileResourceUrl: SafeResourceUrl | null = null;

  // ===== Modals =====
  upload: UploadForm | null = null;
  uploadError = '';
  isDragging = false;
  edit: EditForm | null = null;
  editError = '';
  historyId: number | null = null;
  deleteIds: number[] | null = null;

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  /** Today as yyyy-MM-dd in local time, for expiry checks. */
  private readonly today = toIsoDate(new Date());

  private readonly api = inject(AssetDocumentsService);
  private readonly lookup = inject(AssetLookupService);
  private readonly destroyRef = inject(DestroyRef);

  constructor(private readonly sanitizer: DomSanitizer) {
    const route = inject(ActivatedRoute);
    this.allAssets = route.snapshot.data['allAssets'] === true;
    if (this.allAssets) {
      this.loadAll();
      return;
    }
    route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => this.load(params.get('tag')));
  }

  /** Lists the documents of every asset, and the registered assets for the picker and filter. */
  private loadAll(): void {
    this.reset();
    this.loading = true;
    this.loadError = '';
    this.lookup.listAssets().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (assets) => (this.assetOptions = assets),
      error: (err) => this.showToast(apiErrorMessage(err, 'Could not load the asset list.')),
    });
    this.api.list().pipe(takeUntilDestroyed(this.destroyRef), finalize(() => (this.loading = false))).subscribe({
      next: (docs) => {
        this.documents = docs;
        this.previewId = docs[0]?.id ?? null;
        this.refreshFileUrl();
      },
      error: (err) => (this.loadError = apiErrorMessage(err, 'Could not load the documents.')),
    });
  }

  /** Shows the asset named in the URL (asset tag or registration id), with its documents. */
  private load(tag: string | null): void {
    this.loadSub?.unsubscribe(); // the user moved on to another asset
    this.requestedTag = tag ?? '';
    this.notFound = false;
    this.documents = [];
    this.previewId = null;
    this.currentAsset = null;
    this.reset();
    this.loading = true;
    this.loadError = '';
    this.loadSub = this.lookup
      .findAsset(tag)
      .pipe(
        switchMap((asset) => {
          if (!asset) {
            this.notFound = true;
            return of(null);
          }
          this.currentAsset = asset;
          this.asset = asset.header;
          this.sampleImage = asset.header.image ?? '';
          this.assetOptions = [asset];
          return this.api.list(asset.id);
        }),
        finalize(() => (this.loading = false)),
      )
      .subscribe({
        next: (docs) => {
          if (!docs) return;
          this.documents = docs;
          this.previewId = docs[0]?.id ?? null;
          this.refreshFileUrl();
        },
        error: (err) => {
          this.loadError = apiErrorMessage(err, 'Could not load the documents.');
          this.showToast(this.loadError);
        },
      });
  }

  /** Clears the per-asset view state. */
  private reset(): void {
    this.checked.clear();
    this.clearFilters();
    this.closeMenus();
    this.previewOpen = true;
    this.previewPage = 1;
    this.zoom = 100;
    this.rotation = 0;
    this.refreshFileUrl();
  }

  ngOnDestroy(): void {
    this.loadSub?.unsubscribe();
    this.revokeFileUrl();
    clearTimeout(this.toastTimer);
  }

  // ===== Lookups =====

  assetName(doc: ApiDocument): string {
    return doc.assetName || this.assetOptions.find((a) => a.id === doc.assetRegistrationId)?.name || '-';
  }

  /** Picture for the mock-up pages of a document without a file: the photo of the document's asset. */
  imageFor(doc: ApiDocument): string {
    return this.assetOptions.find((a) => a.id === doc.assetRegistrationId)?.header.image ?? this.sampleImage;
  }

  current(doc: ApiDocument): ApiDocVersion | undefined {
    return doc.versions[doc.versions.length - 1];
  }

  /** Drafts stay drafts; otherwise a past expiry date means Expired. */
  statusOf(doc: ApiDocument): DocStatus {
    if (doc.draft) return 'Draft';
    return doc.expiryDate && doc.expiryDate < this.today ? 'Expired' : 'Active';
  }

  stageTone(stage: string): string {
    return STAGE_TONES[stage] ?? 'violet';
  }

  isImage(doc: ApiDocument): boolean {
    return /\.(jpe?g|png)$/i.test(this.current(doc)?.fileName ?? doc.name);
  }

  formatSize(bytes: number): string {
    return bytes >= MB ? `${(bytes / MB).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
  }

  get filtered(): ApiDocument[] {
    const q = this.search.trim().toLowerCase();
    return this.documents.filter(
      (d) =>
        (!this.assetFilter || d.assetTag === this.assetFilter) &&
        (!this.typeFilter || d.type === this.typeFilter) &&
        (!this.stageFilter || d.stage === this.stageFilter) &&
        (!this.statusFilter || this.statusOf(d) === this.statusFilter) &&
        (!q ||
          [d.name, d.type, d.stage, this.current(d)?.uploadedBy ?? '', d.assetTag ?? '', this.allAssets ? this.assetName(d) : '', ...d.tags]
            .some((v) => v.toLowerCase().includes(q))),
    );
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.filtered.length / this.pageSize));
  }

  get pageNumbers(): number[] {
    return Array.from({ length: this.pageCount }, (_, i) => i + 1);
  }

  get pageRows(): ApiDocument[] {
    const start = (this.safePage - 1) * this.pageSize;
    return this.filtered.slice(start, start + this.pageSize);
  }

  /** The page number kept within range after filtering or deleting. */
  get safePage(): number {
    return Math.min(this.page, this.pageCount);
  }

  get rangeLabel(): string {
    const total = this.filtered.length;
    if (!total) return 'No documents';
    const start = (this.safePage - 1) * this.pageSize + 1;
    return `Showing ${start} - ${Math.min(start + this.pageSize - 1, total)} of ${total} documents`;
  }

  /** Row number across pages (1-based). */
  rowNumber(index: number): number {
    return (this.safePage - 1) * this.pageSize + index + 1;
  }

  goToPage(page: number): void {
    this.page = Math.min(Math.max(1, page), this.pageCount);
  }

  onFilterChange(): void {
    this.page = 1;
  }

  get hasFilters(): boolean {
    return !!(this.search || this.assetFilter || this.typeFilter || this.stageFilter || this.statusFilter);
  }

  clearFilters(): void {
    this.search = this.assetFilter = this.typeFilter = this.stageFilter = this.statusFilter = '';
    this.page = 1;
  }

  // ===== Selection =====

  get allPageChecked(): boolean {
    return this.pageRows.length > 0 && this.pageRows.every((d) => this.checked.has(d.id));
  }

  get somePageChecked(): boolean {
    return !this.allPageChecked && this.pageRows.some((d) => this.checked.has(d.id));
  }

  toggleAll(on: boolean): void {
    this.pageRows.forEach((d) => (on ? this.checked.add(d.id) : this.checked.delete(d.id)));
  }

  toggleChecked(id: number, on: boolean): void {
    if (on) this.checked.add(id);
    else this.checked.delete(id);
  }

  // ===== Menus =====

  toggleActions(event: Event): void {
    event.stopPropagation();
    const open = !this.actionsOpen;
    this.closeMenus();
    this.actionsOpen = open;
  }

  toggleBulk(event: Event): void {
    event.stopPropagation();
    const open = !this.bulkOpen;
    this.closeMenus();
    this.bulkOpen = open;
  }

  toggleRowMenu(event: Event, id: number): void {
    event.stopPropagation();
    const open = this.rowMenuId !== id;
    this.closeMenus();
    this.rowMenuId = open ? id : null;
  }

  toggleViewerMenu(event: Event): void {
    event.stopPropagation();
    const open = !this.viewerMenuOpen;
    this.closeMenus();
    this.viewerMenuOpen = open;
  }

  @HostListener('document:click')
  closeMenus(): void {
    this.actionsOpen = this.bulkOpen = this.viewerMenuOpen = this.assetPickerOpen = false;
    this.rowMenuId = null;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeMenus();
    this.upload = this.edit = null;
    this.historyId = null;
    this.deleteIds = null;
  }

  tabClick(tab: AssetTabLink): void {
    if (!tab.path && !tab.active) this.showToast(`${tab.label} is not available in this demo.`);
  }

  // ===== Preview =====

  get previewDoc(): ApiDocument | undefined {
    return this.documents.find((d) => d.id === this.previewId);
  }

  /** The previewed document's stored file (current version). */
  get previewFile(): ApiDocVersion | undefined {
    const doc = this.previewDoc;
    const version = doc ? this.current(doc) : undefined;
    return version?.url ? version : undefined;
  }

  get previewIsPdfFile(): boolean {
    const f = this.previewFile;
    return !!f && (/\.pdf$/i.test(f.fileName) || f.contentType === 'application/pdf');
  }

  get previewIsImageFile(): boolean {
    const f = this.previewFile;
    return !!f && (/\.(jpe?g|png|webp|gif|bmp)$/i.test(f.fileName) || !!f.contentType?.startsWith('image/'));
  }

  /** URL of the stored file, for <img>. */
  get previewFileUrl(): string | null {
    return this.fileUrl;
  }

  openPreview(doc: ApiDocument): void {
    this.closeMenus();
    this.previewOpen = true;
    if (this.previewId === doc.id) return;
    this.previewId = doc.id;
    this.previewPage = 1;
    this.zoom = 100;
    this.rotation = 0;
    this.refreshFileUrl();
  }

  closePreview(): void {
    this.previewOpen = false;
  }

  /** Sample page numbers shown as thumbnails. */
  get previewPages(): number[] {
    const doc = this.previewDoc;
    return doc ? Array.from({ length: doc.pages }, (_, i) => i + 1) : [];
  }

  setPage(page: number): void {
    const doc = this.previewDoc;
    if (doc) this.previewPage = Math.min(Math.max(1, page), doc.pages);
  }

  zoomBy(direction: 1 | -1): void {
    this.zoom = Math.min(ZOOM_MAX, Math.max(ZOOM_MIN, this.zoom + direction * ZOOM_STEP));
  }

  get canZoomIn(): boolean {
    return this.zoom < ZOOM_MAX;
  }

  get canZoomOut(): boolean {
    return this.zoom > ZOOM_MIN;
  }

  fitWidth(): void {
    this.zoom = 100;
    this.rotation = 0;
  }

  rotate(): void {
    this.rotation = (this.rotation + 90) % 360;
  }

  openInNewTab(): void {
    this.closeMenus();
    if (this.fileUrl) window.open(this.fileUrl, '_blank', 'noopener');
    else this.showToast('This document has no stored file to open.');
  }

  printPreview(): void {
    this.closeMenus();
    if (this.fileUrl) {
      // The browser's own viewer prints the real file
      window.open(this.fileUrl, '_blank', 'noopener');
    } else {
      this.showToast('This document has no stored file to print.');
    }
  }

  private refreshFileUrl(): void {
    this.revokeFileUrl();
    const file = this.previewFile;
    if (!file) return;
    this.fileUrl = file.url;
    // URL of a file stored by the Media service for this document, so it is trusted for the viewer frame
    this.fileResourceUrl = this.sanitizer.bypassSecurityTrustResourceUrl(this.fileUrl);
  }

  private revokeFileUrl(): void {
    this.fileUrl = null;
    this.fileResourceUrl = null;
  }

  // ===== Download / export =====

  download(doc: ApiDocument, version?: ApiDocVersion): void {
    this.closeMenus();
    const v = version ?? this.current(doc);
    if (!v?.url) {
      this.showToast(`${doc.name} has no stored file to download.`);
      return;
    }
    this.lookup.download(v.url, downloadName(doc.name, v.fileName));
  }

  downloadSelected(): void {
    this.closeMenus();
    const docs = this.documents.filter((d) => this.checked.has(d.id));
    if (!docs.length) {
      this.showToast('Select documents first.');
      return;
    }
    const withFiles = docs.filter((d) => this.current(d)?.url);
    withFiles.forEach((d) => this.lookup.download(this.current(d)!.url, downloadName(d.name, this.current(d)!.fileName)));
    const skipped = docs.length - withFiles.length;
    this.showToast(
      withFiles.length
        ? `Downloading ${withFiles.length} file(s).${skipped ? ` ${skipped} record(s) have no file.` : ''}`
        : 'The selected documents have no stored files.',
    );
  }

  exportCsv(): void {
    this.closeMenus();
    const head = ['#', 'Document Name', ...(this.allAssets ? ['Asset Name', 'Asset ID'] : []), 'Document Type', 'Lifecycle Stage', 'Version', 'Upload Date', 'Uploaded By', 'Expiry Date', 'Status', 'Description', 'Tags'];
    const rows = this.filtered.map((d, i) => {
      const v = this.current(d);
      return [i + 1, d.name, ...(this.allAssets ? [this.assetName(d), d.assetTag ?? ''] : []), d.type, d.stage, v?.version ?? '-', v?.uploadedAt.slice(0, 10) ?? '-', v?.uploadedBy ?? '-',
        d.expiryDate || '-', this.statusOf(d), d.description, d.tags.join('; ')];
    });
    const csv = [head, ...rows].map((row) => row.map((v) => `"${String(v).replace(/"/g, '""')}"`).join(',')).join('\r\n');
    saveBlob(new Blob([csv], { type: 'text/csv' }), this.allAssets ? 'asset-documents.csv' : `${this.asset.tag}-documents.csv`);
  }

  print(): void {
    this.closeMenus();
    window.print();
  }

  // ===== Upload (new document or new version) =====

  openUpload(doc?: ApiDocument): void {
    this.closeMenus();
    this.uploadError = '';
    this.upload = doc
      ? {
          mode: 'version', assets: [], docId: doc.id, file: null, name: doc.name, type: doc.type, stage: doc.stage,
          version: nextVersion(this.current(doc)?.version), expiryDate: doc.expiryDate, draft: false,
          description: doc.description, tags: doc.tags.join(', '), note: '',
        }
      : {
          mode: 'new', assets: this.allAssets || !this.currentAsset ? [] : [this.currentAsset.tag], docId: null, file: null, name: '', type: '', stage: '', version: '1.0', expiryDate: '',
          draft: false, description: '', tags: '', note: '',
        };
  }

  // Asset dropdown (all-assets page): pick one or more assets by name or ID

  toggleAssetPicker(event: Event): void {
    event.stopPropagation();
    this.assetPickerOpen = !this.assetPickerOpen;
    this.assetPickerSearch = '';
  }

  get pickerAssets(): AssetOption[] {
    const q = this.assetPickerSearch.trim().toLowerCase();
    return q ? this.assetOptions.filter((a) => a.name.toLowerCase().includes(q) || a.tag.toLowerCase().includes(q)) : this.assetOptions;
  }

  toggleUploadAsset(tag: string): void {
    const u = this.upload;
    if (!u) return;
    u.assets = u.assets.includes(tag) ? u.assets.filter((t) => t !== tag) : [...u.assets, tag];
  }

  get allPickerAssetsChosen(): boolean {
    const u = this.upload;
    return !!u && this.pickerAssets.length > 0 && this.pickerAssets.every((a) => u.assets.includes(a.tag));
  }

  toggleAllUploadAssets(on: boolean): void {
    const u = this.upload;
    if (!u) return;
    const tags = this.pickerAssets.map((a) => a.tag);
    u.assets = on ? [...new Set([...u.assets, ...tags])] : u.assets.filter((t) => !tags.includes(t));
  }

  /** Summary shown on the closed dropdown. */
  get uploadAssetsLabel(): string {
    const tags = this.upload?.assets ?? [];
    if (!tags.length) return 'Select assets';
    if (tags.length === 1) return `${this.assetOptions.find((a) => a.tag === tags[0])?.name ?? tags[0]} (${tags[0]})`;
    return `${tags.length} assets selected`;
  }

  onUploadFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.setUploadFile(input.files?.[0]);
    input.value = '';
  }

  onUploadDrop(event: DragEvent): void {
    event.preventDefault();
    this.isDragging = false;
    this.setUploadFile(event.dataTransfer?.files?.[0]);
  }

  private setUploadFile(file: File | undefined): void {
    if (!file || !this.upload) return;
    const ext = file.name.split('.').pop()?.toLowerCase() ?? '';
    if (!ACCEPTED_EXTENSIONS.includes(ext)) {
      this.uploadError = 'Only PDF, JPG, PNG, DOC and XLS files can be uploaded.';
      return;
    }
    if (file.size > MAX_FILE_BYTES) {
      this.uploadError = 'Files can be up to 25 MB.';
      return;
    }
    this.uploadError = '';
    this.upload.file = file;
    if (this.upload.mode === 'new' && !this.upload.name) this.upload.name = file.name;
    if (this.upload.mode === 'new' && !this.upload.type && /\.(jpe?g|png)$/i.test(file.name)) this.upload.type = 'Image';
  }

  saveUpload(): void {
    const u = this.upload;
    if (!u || this.saving) return;
    if (!u.file) {
      this.uploadError = 'Choose a file to upload.';
      return;
    }
    if (u.mode === 'new' && !u.assets.length) {
      this.uploadError = 'Select at least one asset.';
      return;
    }
    if (u.mode === 'new' && (!u.name.trim() || !u.type || !u.stage)) {
      this.uploadError = 'Enter the document name, type and lifecycle stage.';
      return;
    }
    if (!u.version.trim()) {
      this.uploadError = 'Enter the version.';
      return;
    }
    const value: DocumentFormValue = {
      name: u.name, type: u.type, stage: u.stage, version: u.version, expiryDate: u.expiryDate, draft: u.draft,
      description: u.description, tags: splitTags(u.tags),
    };
    const note = u.note.trim() || (u.mode === 'new' ? 'Initial upload' : 'New version');

    if (u.mode === 'version') {
      const doc = this.documents.find((d) => d.id === u.docId);
      if (!doc) return;
      // A new version is no longer a draft
      this.runSave(
        this.api.uploadFile(u.file).pipe(switchMap((file: StoredFile) => this.api.update(doc, { ...value, draft: false }, file))),
        (updated) => {
          const latest = updated.versions[updated.versions.length - 1];
          if (latest) latest.note = note;
          // The API keeps only the current file; earlier versions seen in this session stay listed
          updated.versions = [...doc.versions, ...updated.versions];
          this.replaceDoc(updated);
          this.showToast(`${updated.name} updated to version ${latest?.version ?? value.version}.`);
          this.upload = null;
          if (this.previewId === updated.id) this.refreshFileUrl();
          else this.openPreview(updated);
        },
        (message) => (this.uploadError = message),
      );
      return;
    }

    // One document per selected asset; they share the uploaded file
    const ids = u.assets.map((tag) => this.assetOptions.find((a) => a.tag === tag)?.id ?? tag);
    this.runSave(
      this.api.uploadToAssets(ids, value, u.file),
      (created) => {
        if (!created.length) {
          // The API saved the document but returned no record: reload the list to show it
          this.upload = null;
          this.showToast(`${value.name.trim()} uploaded.`);
          if (this.allAssets) this.loadAll();
          else this.load(this.requestedTag || null);
          return;
        }
        created.forEach((d) => d.versions.forEach((v) => (v.note = note)));
        this.documents = [...created, ...this.documents];
        this.upload = null;
        this.clearFilters();
        this.page = 1;
        this.openPreview(created[0]);
        this.showToast(created.length === 1 ? `${created[0].name} uploaded.` : `${created[0].name} uploaded to ${created.length} assets.`);
      },
      (message) => (this.uploadError = message),
    );
  }

  /** Runs a save request with the saving flag set, reporting errors through `onError`. */
  private runSave<T>(request: Observable<T>, onDone: (result: T) => void, onError: (message: string) => void): void {
    this.saving = true;
    request.pipe(takeUntilDestroyed(this.destroyRef), finalize(() => (this.saving = false))).subscribe({
      next: onDone,
      error: (err) => onError(apiErrorMessage(err)),
    });
  }

  /** Puts an updated document in place of the one with the same local id. */
  private replaceDoc(updated: ApiDocument): void {
    this.documents = this.documents.map((d) => (d.id === updated.id ? updated : d));
  }

  // ===== Edit details =====

  openEdit(doc: ApiDocument): void {
    this.closeMenus();
    this.editError = '';
    this.edit = {
      id: doc.id, name: doc.name, type: doc.type, stage: doc.stage, expiryDate: doc.expiryDate,
      draft: doc.draft, description: doc.description, tags: doc.tags.join(', '),
    };
  }

  saveEdit(): void {
    const e = this.edit;
    if (!e) return;
    if (!e.name.trim() || !e.type || !e.stage) {
      this.editError = 'Enter the document name, type and lifecycle stage.';
      return;
    }
    const doc = this.documents.find((d) => d.id === e.id);
    if (!doc) return;
    if (!e.draft && !doc.versions.length) {
      this.editError = 'Upload a file before marking this document Active.';
      return;
    }
    if (this.saving) return;
    const value: DocumentFormValue = {
      name: e.name, type: e.type, stage: e.stage, version: this.current(doc)?.version ?? '1.0', expiryDate: e.expiryDate,
      draft: e.draft, description: e.description, tags: splitTags(e.tags),
    };
    this.runSave(
      this.api.update(doc, value),
      (updated) => {
        // Keep the version list (and notes) seen in this session
        updated.versions = doc.versions;
        this.replaceDoc(updated);
        this.edit = null;
        this.showToast('Document details saved.');
      },
      (message) => (this.editError = message),
    );
  }

  // ===== History =====

  openHistory(doc: ApiDocument): void {
    this.closeMenus();
    this.historyId = doc.id;
  }

  get historyDoc(): ApiDocument | undefined {
    return this.documents.find((d) => d.id === this.historyId);
  }

  /** Newest first for the history list. */
  get historyVersions(): ApiDocVersion[] {
    return [...(this.historyDoc?.versions ?? [])].reverse();
  }

  // ===== Delete =====

  askDelete(ids: number[]): void {
    this.closeMenus();
    if (!ids.length) {
      this.showToast('Select documents first.');
      return;
    }
    this.deleteIds = ids;
  }

  get selectedIds(): number[] {
    return this.documents.filter((d) => this.checked.has(d.id)).map((d) => d.id);
  }

  get deleteLabel(): string {
    const ids = this.deleteIds ?? [];
    return ids.length === 1 ? this.documents.find((d) => d.id === ids[0])?.name ?? '' : `${ids.length} documents`;
  }

  confirmDelete(): void {
    if (this.saving) return;
    const ids = new Set(this.deleteIds ?? []);
    const apiIds = this.documents.filter((d) => ids.has(d.id)).map((d) => d.apiId);
    if (!apiIds.length) {
      this.deleteIds = null;
      return;
    }
    const request: Observable<unknown> = apiIds.length === 1 ? this.api.delete(apiIds[0]) : this.api.deleteMany(apiIds);
    this.runSave(
      request,
      () => {
        this.documents = this.documents.filter((d) => !ids.has(d.id));
        ids.forEach((id) => this.checked.delete(id));
        if (this.previewId !== null && ids.has(this.previewId)) {
          this.previewId = this.documents[0]?.id ?? null;
          this.previewPage = 1;
          this.refreshFileUrl();
        }
        this.showToast(ids.size === 1 ? 'Document deleted.' : `${ids.size} documents deleted.`);
        this.deleteIds = null;
      },
      (message) => {
        this.deleteIds = null;
        this.showToast(message);
      },
    );
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 3500);
  }
}

// ===== Helpers =====

function pad(n: number): string {
  return String(n).padStart(2, '0');
}

function toIsoDate(d: Date): string {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

/** Header shown until the asset is loaded. */
const EMPTY_HEADER: AssetHeader = {
  tag: '', name: '', category: '-', model: '-', location: '-', status: 'Active', condition: 'Good', criticality: 'Medium',
  image: null, icon: 'transformer',
};

/** Download name: the document name, with the stored file's extension when the name has none. */
function downloadName(name: string, fileName: string): string {
  const ext = /\.[^.]+$/.exec(fileName)?.[0] ?? '';
  return ext && !name.toLowerCase().endsWith(ext.toLowerCase()) ? `${name}${ext}` : name;
}

/** "1.0" -> "1.1", "2.9" -> "2.10"; anything unparseable -> "1.0". */
function nextVersion(current: string | undefined): string {
  const match = /^(\d+)\.(\d+)$/.exec(current ?? '');
  return match ? `${match[1]}.${Number(match[2]) + 1}` : '1.0';
}

function splitTags(text: string): string[] {
  return [...new Set(text.split(',').map((t) => t.trim()).filter(Boolean))];
}

function saveBlob(blob: Blob, name: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = name;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url));
}
