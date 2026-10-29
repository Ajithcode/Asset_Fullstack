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
  ASSET_RECORDS,
  ASSET_TYPE_ICONS,
  AssetHeader,
  AssetTabLink,
  assetHeader,
  assetTabs,
  conditionPill,
  criticalityPill,
  findAsset,
  statusPill,
} from '../asset-data';
import { AssetDetail, AssetDocument, DocVersion, cloneDetail, loadAssetDetail } from '../asset-details/asset-details';

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

const CURRENT_USER = 'Rex Kumar';
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

/** One asset's documents, kept in memory so its Documents tab and the all-assets page show the same list (DEMO MODE). */
interface DocStore {
  docs: AssetDocument[];
  previewId: number | null;
}

const DOC_STORE = new Map<string, DocStore>();
/** Document ids are unique across assets so documents of several assets can be listed together. */
let nextDocId = 1;

/** The stored documents of an asset, created from its data file the first time. */
function storeFor(tag: string, detail: AssetDetail | null): DocStore {
  let store = DOC_STORE.get(tag);
  if (!store) {
    const docs = detail ? cloneDetail(detail.documents) : [];
    let previewId: number | null = null;
    for (const d of docs) {
      const id = nextDocId++;
      if (d.id === detail?.previewDocumentId) previewId = id;
      d.id = id;
      d.assetTag = tag;
    }
    store = { docs, previewId: previewId ?? docs[0]?.id ?? null };
    // A failed download is not stored, so a later visit tries again
    if (detail) DOC_STORE.set(tag, store);
  }
  return store;
}

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
  asset: AssetHeader = assetHeader(ASSET_RECORDS[0]);
  notFound = false;
  requestedTag = '';
  readonly tabs: AssetTabLink[] = assetTabs('Documents');
  readonly typeIcons = ASSET_TYPE_ICONS;
  /** Picture drawn on the mock-up pages of the sample manuals: the asset's photo. */
  sampleImage = '';
  /** True while the asset's documents are being fetched. */
  loading = false;
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

  documents: AssetDocument[] = [];

  /** Asset Documents menu page: documents of every asset, with their asset name and ID. */
  allAssets = false;
  readonly assetOptions = ASSET_RECORDS;
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

  constructor(private readonly sanitizer: DomSanitizer) {
    const route = inject(ActivatedRoute);
    this.allAssets = route.snapshot.data['allAssets'] === true;
    if (this.allAssets) {
      this.loadAll();
      return;
    }
    route.queryParamMap.pipe(takeUntilDestroyed(inject(DestroyRef))).subscribe((params) => this.load(params.get('tag')));
  }

  /** Lists the documents of every asset in the register. */
  private loadAll(): void {
    this.reset();
    this.loading = true;
    Promise.all(ASSET_RECORDS.map((a) => loadAssetDetail(a.tag).then((d) => storeFor(a.tag, d)))).then((stores) => {
      this.loading = false;
      this.documents = stores.flatMap((s) => s.docs);
      this.previewId = stores.find((s) => s.previewId !== null)?.previewId ?? null;
      this.refreshFileUrl();
    });
  }

  /** Writes uploads and deletes back to each asset's stored documents. */
  private persist(): void {
    for (const tag of this.allAssets ? ASSET_RECORDS.map((a) => a.tag) : [this.asset.tag]) {
      const store = DOC_STORE.get(tag);
      if (store) store.docs = this.documents.filter((d) => d.assetTag === tag);
    }
  }

  /** Shows the asset named in the URL, with the documents from its data file. */
  private load(tag: string | null): void {
    tag ??= ASSET_RECORDS[0].tag; // opened from the menu without an asset: show the first one
    const record = findAsset(tag);
    this.requestedTag = tag ?? '';
    this.notFound = !record;
    if (!record) return;
    this.asset = assetHeader(record);
    this.sampleImage = record.image;
    this.documents = [];
    this.previewId = null;
    this.reset();
    this.loading = true;
    loadAssetDetail(record.tag).then((detail) => {
      if (this.requestedTag !== record.tag) return; // the user moved on to another asset
      this.loading = false;
      const store = storeFor(record.tag, detail);
      this.documents = store.docs;
      this.previewId = store.previewId;
      this.refreshFileUrl();
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
    this.revokeFileUrl();
    clearTimeout(this.toastTimer);
  }

  // ===== Lookups =====

  assetName(doc: AssetDocument): string {
    return findAsset(doc.assetTag)?.name ?? '-';
  }

  /** Picture for the mock-up pages of a sample manual: the photo of the document's asset. */
  imageFor(doc: AssetDocument): string {
    return findAsset(doc.assetTag)?.image ?? this.sampleImage;
  }

  current(doc: AssetDocument): DocVersion | undefined {
    return doc.versions[doc.versions.length - 1];
  }

  /** Drafts stay drafts; otherwise a past expiry date means Expired. */
  statusOf(doc: AssetDocument): DocStatus {
    if (doc.draft) return 'Draft';
    return doc.expiryDate && doc.expiryDate < this.today ? 'Expired' : 'Active';
  }

  stageTone(stage: string): string {
    return STAGE_TONES[stage] ?? 'violet';
  }

  isImage(doc: AssetDocument): boolean {
    return /\.(jpe?g|png)$/i.test(doc.name);
  }

  formatSize(bytes: number): string {
    return bytes >= MB ? `${(bytes / MB).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
  }

  get filtered(): AssetDocument[] {
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

  get pageRows(): AssetDocument[] {
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

  get previewDoc(): AssetDocument | undefined {
    return this.documents.find((d) => d.id === this.previewId);
  }

  /** The previewed document's real file, when one was uploaded in this session. */
  get previewFile(): File | undefined {
    const doc = this.previewDoc;
    return doc ? this.current(doc)?.file : undefined;
  }

  get previewIsPdfFile(): boolean {
    return !!this.previewFile && /\.pdf$/i.test(this.previewFile.name);
  }

  get previewIsImageFile(): boolean {
    return !!this.previewFile && this.previewFile.type.startsWith('image/');
  }

  /** Raw blob URL, for <img> (Angular allows blob: image URLs). */
  get previewFileUrl(): string | null {
    return this.fileUrl;
  }

  openPreview(doc: AssetDocument): void {
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
    else this.showToast('This is a sample record with no stored file to open.');
  }

  printPreview(): void {
    this.closeMenus();
    if (this.fileUrl) {
      // The browser's own viewer prints the real file
      window.open(this.fileUrl, '_blank', 'noopener');
    } else {
      this.showToast('This is a sample record with no stored file to print.');
    }
  }

  private refreshFileUrl(): void {
    this.revokeFileUrl();
    const file = this.previewFile;
    if (!file) return;
    this.fileUrl = URL.createObjectURL(file);
    // Blob URL created here from the user's own file, so it is safe to frame
    this.fileResourceUrl = this.sanitizer.bypassSecurityTrustResourceUrl(this.fileUrl);
  }

  private revokeFileUrl(): void {
    if (this.fileUrl) URL.revokeObjectURL(this.fileUrl);
    this.fileUrl = null;
    this.fileResourceUrl = null;
  }

  // ===== Download / export =====

  download(doc: AssetDocument, version?: DocVersion): void {
    this.closeMenus();
    const v = version ?? this.current(doc);
    if (!v) {
      this.showToast(`${doc.name} is a draft with no file uploaded yet.`);
      return;
    }
    if (!v.file) {
      this.showToast(`${doc.name} is a sample record with no stored file to download.`);
      return;
    }
    saveBlob(v.file, doc.name);
  }

  downloadSelected(): void {
    this.closeMenus();
    const docs = this.documents.filter((d) => this.checked.has(d.id));
    if (!docs.length) {
      this.showToast('Select documents first.');
      return;
    }
    const withFiles = docs.filter((d) => this.current(d)?.file);
    withFiles.forEach((d) => saveBlob(this.current(d)!.file!, d.name));
    const skipped = docs.length - withFiles.length;
    this.showToast(
      withFiles.length
        ? `Downloading ${withFiles.length} file(s).${skipped ? ` ${skipped} sample/draft record(s) have no file.` : ''}`
        : 'The selected documents are sample or draft records with no stored files.',
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

  openUpload(doc?: AssetDocument): void {
    this.closeMenus();
    this.uploadError = '';
    this.upload = doc
      ? {
          mode: 'version', assets: [], docId: doc.id, file: null, name: doc.name, type: doc.type, stage: doc.stage,
          version: nextVersion(this.current(doc)?.version), expiryDate: doc.expiryDate, draft: false,
          description: doc.description, tags: doc.tags.join(', '), note: '',
        }
      : {
          mode: 'new', assets: this.allAssets ? [] : [this.asset.tag], docId: null, file: null, name: '', type: '', stage: '', version: '1.0', expiryDate: '',
          draft: false, description: '', tags: '', note: '',
        };
  }

  // Asset dropdown (all-assets page): pick one or more assets by name or ID

  toggleAssetPicker(event: Event): void {
    event.stopPropagation();
    this.assetPickerOpen = !this.assetPickerOpen;
    this.assetPickerSearch = '';
  }

  get pickerAssets(): typeof ASSET_RECORDS {
    const q = this.assetPickerSearch.trim().toLowerCase();
    return q ? ASSET_RECORDS.filter((a) => a.name.toLowerCase().includes(q) || a.tag.toLowerCase().includes(q)) : ASSET_RECORDS;
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
    if (tags.length === 1) return `${findAsset(tags[0])?.name} (${tags[0]})`;
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
    if (!u) return;
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
    const version: DocVersion = {
      version: u.version.trim(),
      uploadedAt: toIsoDateTime(new Date()),
      uploadedBy: CURRENT_USER,
      size: u.file.size,
      note: u.note.trim() || (u.mode === 'new' ? 'Initial upload' : 'New version'),
      file: u.file,
    };

    if (u.mode === 'version') {
      const doc = this.documents.find((d) => d.id === u.docId);
      if (!doc) return;
      doc.versions = [...doc.versions, version];
      doc.draft = false;
      this.showToast(`${doc.name} updated to version ${version.version}.`);
      this.upload = null;
      if (this.previewId === doc.id) this.refreshFileUrl();
      else this.openPreview(doc);
      return;
    }

    // One document per selected asset; they share the uploaded file
    const created: AssetDocument[] = u.assets.map((assetTag) => ({
      id: nextDocId++,
      assetTag,
      name: u.name.trim(),
      type: u.type,
      stage: u.stage,
      description: u.description.trim(),
      tags: splitTags(u.tags),
      expiryDate: u.expiryDate,
      draft: u.draft,
      versions: [{ ...version }],
      pages: 1,
      issuer: '',
      coverTitle: '',
      coverSubtitle: '',
      showAssetImage: false,
    }));
    this.documents = [...this.documents, ...created];
    this.persist();
    this.upload = null;
    this.clearFilters();
    this.page = this.pageCount;
    this.openPreview(created[0]);
    this.showToast(created.length === 1 ? `${created[0].name} uploaded.` : `${created[0].name} uploaded to ${created.length} assets.`);
  }

  // ===== Edit details =====

  openEdit(doc: AssetDocument): void {
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
    Object.assign(doc, {
      name: e.name.trim(), type: e.type, stage: e.stage, expiryDate: e.expiryDate, draft: e.draft,
      description: e.description.trim(), tags: splitTags(e.tags),
    });
    this.edit = null;
    this.showToast('Document details saved.');
  }

  // ===== History =====

  openHistory(doc: AssetDocument): void {
    this.closeMenus();
    this.historyId = doc.id;
  }

  get historyDoc(): AssetDocument | undefined {
    return this.documents.find((d) => d.id === this.historyId);
  }

  /** Newest first for the history list. */
  get historyVersions(): DocVersion[] {
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
    const ids = new Set(this.deleteIds ?? []);
    this.documents = this.documents.filter((d) => !ids.has(d.id));
    this.persist();
    ids.forEach((id) => this.checked.delete(id));
    if (this.previewId !== null && ids.has(this.previewId)) {
      this.previewId = this.documents[0]?.id ?? null;
      this.previewPage = 1;
      this.refreshFileUrl();
    }
    this.showToast(ids.size === 1 ? 'Document deleted.' : `${ids.size} documents deleted.`);
    this.deleteIds = null;
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

function toIsoDateTime(d: Date): string {
  return `${toIsoDate(d)}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
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
