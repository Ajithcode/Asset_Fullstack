import { Component, HostListener, Input, inject } from '@angular/core';
import { Observable, forkJoin, map, of, switchMap } from 'rxjs';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  LucideAngularModule,
  Archive,
  ArchiveRestore,
  ChevronLeft,
  ChevronRight,
  Download,
  Ellipsis,
  EllipsisVertical,
  ExternalLink,
  Eye,
  Files,
  Inbox,
  Plus,
  Search,
  SquarePen,
  Trash2,
  Upload,
  X,
} from 'lucide-angular';
import type { AssetComponent } from '../asset-components';
import { FORMAT_ICONS, FilePreview, type FileFormat, formatOf, formatSize, saveBlob } from '../file-preview/file-preview';
import { AssetComponentsService, isoLocalDateTime, type AssetDocumentDto, type ComponentFileDetails, type UploadedFile } from '../asset-components.service';
import { apiErrorMessage } from '../../services/asset-api';

export type DocumentType =
  | 'Manual'
  | 'Installation'
  | 'Drawing'
  | 'Parts List'
  | 'Schedule'
  | 'Certificate'
  | 'Warranty'
  | 'Image'
  | 'Test Report'
  | 'Report';
export type DocumentStatus = 'Active' | 'Archived';

export interface ComponentDocument {
  id: number;
  name: string;
  fileName: string;
  type: DocumentType;
  version: string;
  /** ISO date-time (yyyy-mm-ddTHH:mm). */
  uploadedAt: string;
  uploadedBy: string;
  status: DocumentStatus;
  size: number;
  /** Page count for the preview pager; 1 for images and single-sheet files. */
  pages: number;
  description: string;
  /** Present only for files uploaded in this session. */
  file?: File;
  /** Stored file url (from the Media service). */
  url?: string;
  /** The asset document behind this row (/api/asset-documents). */
  dto?: AssetDocumentDto;
}

interface UploadForm {
  name: string;
  type: DocumentType;
  version: string;
  status: DocumentStatus;
  description: string;
}

const MAX_UPLOAD_BYTES = 25 * 1024 * 1024;
const ACCEPT = '.pdf,.png,.jpg,.jpeg,.doc,.docx,.xls,.xlsx';
let nextId = 1;

/** Bumps the last number of a "v2.1"-style version: v2.1 → v2.2, v3 → v4. */
function bumpVersion(v: string): string {
  const m = /^(.*?)(\d+)$/.exec(v.trim());
  return m ? `${m[1]}${Number(m[2]) + 1}` : `${v.trim() || 'v1'}.1`;
}

/** UI status ↔ API status (asset documents are Active / Inactive). */
const toApiStatus = (s: DocumentStatus): ComponentFileDetails['status'] => (s === 'Archived' ? 'Inactive' : 'Active');

/** Maps an asset document (tagged to a component) to a Documents-tab row. */
export function toDocument(dto: AssetDocumentDto): ComponentDocument {
  return {
    id: nextId++,
    name: dto.documentName,
    fileName: dto.fileName,
    type: dto.documentType as DocumentType,
    version: dto.version,
    uploadedAt: isoLocalDateTime(dto.uploadDate),
    uploadedBy: dto.uploadedBy ?? '',
    status: (dto.status ?? '').toLowerCase() === 'inactive' ? 'Archived' : 'Active',
    size: dto.fileSize ?? 0,
    pages: 1,
    description: dto.description ?? '',
    url: dto.fileUrl || undefined,
    dto,
  };
}

@Component({
  selector: 'app-component-documents',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideAngularModule, FilePreview],
  templateUrl: './component-documents.html',
  // Reuses the page's card, table, pill, menu and modal styles.
  styleUrls: ['../asset-components.css', './component-documents.css'],
})
export class ComponentDocuments {
  readonly icons = {
    Archive, ArchiveRestore, ChevronLeft, ChevronRight, Download, Ellipsis, EllipsisVertical, ExternalLink,
    Eye, Files, Inbox, Plus, Search, SquarePen, Trash2, Upload, X,
  };

  readonly formatIcons = FORMAT_ICONS;
  readonly formatSize = formatSize;

  readonly types: DocumentType[] = ['Manual', 'Installation', 'Drawing', 'Parts List', 'Schedule', 'Certificate', 'Warranty', 'Image', 'Test Report', 'Report'];
  readonly statuses: DocumentStatus[] = ['Active', 'Archived'];
  readonly pageSizeOptions = [10, 25, 50];
  readonly accept = ACCEPT;

  /** Shown on the sample title page. */
  @Input() manufacturer = '';

  private comp!: AssetComponent;

  private readonly api = inject(AssetComponentsService);

  loading = false;
  loadError = '';
  saving = false;
  formError = '';

  /** Documents are loaded from the API on first view and kept on the component, so they survive tab switches. */
  @Input({ required: true })
  set component(c: AssetComponent) {
    this.comp = c;
    if (!c.documents) {
      c.documents = [];
      this.load();
    }
    this.search = '';
    this.typeFilter = '';
    this.statusFilter = '';
    this.page = 1;
    this.checked.clear();
    this.openMenu = null;
    this.selectedId = this.sorted[0]?.id ?? null;
  }

  get component(): AssetComponent {
    return this.comp;
  }

  get docs(): ComponentDocument[] {
    return this.comp.documents ?? [];
  }

  /** Loads the component's documents from the API. */
  load(): void {
    const c = this.comp;
    if (!c.apiId || !c.assetRegistrationId) return;
    this.loading = true;
    this.loadError = '';
    this.api.listComponentFiles('document', c.assetRegistrationId, c.apiId).subscribe({
      next: (dtos) => {
        this.loading = false;
        c.documents = dtos.map(toDocument);
        if (c === this.comp && !this.selected) this.selectedId = this.sorted[0]?.id ?? null;
      },
      error: (err) => {
        this.loading = false;
        c.documents = undefined;
        this.loadError = apiErrorMessage(err, 'Could not load documents.');
      },
    });
  }

  private details(d: Pick<ComponentDocument, 'name' | 'type' | 'version' | 'status' | 'description'>): ComponentFileDetails {
    return { documentName: d.name, documentType: d.type, version: d.version, status: toApiStatus(d.status), description: d.description };
  }

  /** Saves a document's details (and optionally a new file) and updates the row from the response. */
  private saveDetails(d: ComponentDocument, changes: Partial<ComponentDocument>, file?: UploadedFile): Observable<ComponentDocument> {
    if (!d.dto) return of(Object.assign(d, changes));
    return this.api.updateComponentFile(d.dto, this.details({ ...d, ...changes }), file).pipe(
      map((dto) => {
        this.releaseFile(d);
        return Object.assign(d, { ...toDocument(dto), id: d.id, file: undefined });
      }),
    );
  }

  search = '';
  typeFilter: '' | DocumentType = '';
  statusFilter: '' | DocumentStatus = '';
  page = 1;
  pageSize = 10;
  checked = new Set<number>();
  openMenu: string | null = null;

  selectedId: number | null = null;

  // Upload / edit modal
  formOpen = false;
  /** 'new' uploads a document, 'version' replaces the file of `target`, 'edit' changes details only. */
  formMode: 'new' | 'version' | 'edit' = 'new';
  target: ComponentDocument | null = null;
  form: UploadForm = this.emptyForm();
  pendingFile: File | null = null;
  fileError = '';
  formSubmitted = false;

  deleteTarget: ComponentDocument | null = null;

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  // ===== Helpers =====

  format(d: ComponentDocument): FileFormat {
    return formatOf(d.fileName);
  }

  // ===== Table =====

  get sorted(): ComponentDocument[] {
    return [...this.docs].sort((a, b) => b.uploadedAt.localeCompare(a.uploadedAt) || b.id - a.id);
  }

  get filtered(): ComponentDocument[] {
    const q = this.search.trim().toLowerCase();
    return this.sorted.filter(
      (d) =>
        (!q || [d.name, d.fileName, d.type, d.uploadedBy, d.description].some((v) => v.toLowerCase().includes(q))) &&
        (!this.typeFilter || d.type === this.typeFilter) &&
        (!this.statusFilter || d.status === this.statusFilter),
    );
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.filtered.length / this.pageSize));
  }

  get currentPage(): number {
    return Math.min(this.page, this.totalPages);
  }

  get pageRows(): ComponentDocument[] {
    const start = (this.currentPage - 1) * this.pageSize;
    return this.filtered.slice(start, start + this.pageSize);
  }

  get rangeStart(): number {
    return this.filtered.length ? (this.currentPage - 1) * this.pageSize + 1 : 0;
  }

  get rangeEnd(): number {
    return Math.min(this.currentPage * this.pageSize, this.filtered.length);
  }

  get pageList(): number[] {
    return Array.from({ length: this.totalPages }, (_, i) => i + 1);
  }

  goToPage(p: number): void {
    if (p >= 1 && p <= this.totalPages) this.page = p;
  }

  onFilterChange(): void {
    this.page = 1;
  }

  get allChecked(): boolean {
    return this.pageRows.length > 0 && this.pageRows.every((d) => this.checked.has(d.id));
  }

  get someChecked(): boolean {
    return !this.allChecked && this.pageRows.some((d) => this.checked.has(d.id));
  }

  toggleAll(): void {
    const on = !this.allChecked;
    this.pageRows.forEach((d) => (on ? this.checked.add(d.id) : this.checked.delete(d.id)));
  }

  toggleRow(id: number): void {
    if (this.checked.has(id)) this.checked.delete(id);
    else this.checked.add(id);
  }

  setStatusForChecked(status: DocumentStatus): void {
    const rows = this.docs.filter((d) => this.checked.has(d.id));
    if (!rows.length || this.saving) return;
    this.saving = true;
    forkJoin(rows.map((d) => this.saveDetails(d, { status }))).subscribe({
      next: () => {
        this.saving = false;
        this.checked.clear();
        this.showToast(`${rows.length} document${rows.length === 1 ? '' : 's'} ${status === 'Archived' ? 'archived' : 'restored'}.`);
      },
      error: (err) => {
        this.saving = false;
        this.showToast(apiErrorMessage(err, 'Could not change the status.'));
        this.load();
      },
    });
  }

  exportList(): void {
    this.openMenu = null;
    const header = ['Document Name', 'File Name', 'Type', 'Version', 'Uploaded At', 'Uploaded By', 'Status', 'Size (KB)', 'Description'];
    const escape = (v: string) => (/[",\n]/.test(v) ? `"${v.replace(/"/g, '""')}"` : v);
    const lines = [header, ...this.filtered.map((d) => [d.name, d.fileName, d.type, d.version, d.uploadedAt.replace('T', ' '), d.uploadedBy, d.status, String(Math.round(d.size / 1024)), d.description])]
      .map((cols) => cols.map(escape).join(','));
    saveBlob(new Blob([lines.join('\n')], { type: 'text/csv;charset=utf-8' }), `${this.comp.tag}-documents.csv`);
    this.showToast(`Exported ${this.filtered.length} document${this.filtered.length === 1 ? '' : 's'}.`);
  }

  // ===== Selection =====

  get selected(): ComponentDocument | null {
    return this.docs.find((d) => d.id === this.selectedId) ?? null;
  }

  select(d: ComponentDocument): void {
    this.selectedId = d.id;
  }

  // ===== File actions =====

  download(d: ComponentDocument): void {
    this.openMenu = null;
    if (d.file) {
      saveBlob(d.file, d.fileName);
      return;
    }
    if (!d.url) {
      this.showToast('No file is stored for this document.');
      return;
    }
    this.api.fetchFile(d.url).subscribe({
      next: (blob) => saveBlob(blob, d.fileName),
      // Fall back to letting the browser open the stored file.
      error: () => window.open(d.url, '_blank', 'noopener'),
    });
  }

  openInNewTab(d: ComponentDocument): void {
    this.openMenu = null;
    if (d.url) window.open(d.url, '_blank', 'noopener');
  }

  toggleArchive(d: ComponentDocument): void {
    this.openMenu = null;
    if (this.saving) return;
    const status: DocumentStatus = d.status === 'Active' ? 'Archived' : 'Active';
    this.saving = true;
    this.saveDetails(d, { status }).subscribe({
      next: () => {
        this.saving = false;
        this.showToast(`${d.name} ${status === 'Archived' ? 'archived' : 'restored'}.`);
      },
      error: (err) => {
        this.saving = false;
        this.showToast(apiErrorMessage(err, `Could not update ${d.name}.`));
      },
    });
  }

  // ===== Menus =====

  toggleMenu(key: string, event: MouseEvent): void {
    event.stopPropagation();
    this.openMenu = this.openMenu === key ? null : key;
  }

  @HostListener('document:click')
  closeMenu(): void {
    this.openMenu = null;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.openMenu = null;
    this.formOpen = false;
    this.deleteTarget = null;
  }

  // ===== Upload / edit =====

  private emptyForm(): UploadForm {
    return { name: '', type: 'Manual', version: 'v1.0', status: 'Active', description: '' };
  }

  openUpload(): void {
    this.openMenu = null;
    this.formMode = 'new';
    this.target = null;
    this.form = this.emptyForm();
    this.pendingFile = null;
    this.fileError = '';
    this.formSubmitted = false;
    this.formError = '';
    this.formOpen = true;
  }

  openVersion(d: ComponentDocument): void {
    this.openMenu = null;
    this.formMode = 'version';
    this.target = d;
    this.form = { name: d.name, type: d.type, version: bumpVersion(d.version), status: 'Active', description: d.description };
    this.pendingFile = null;
    this.fileError = '';
    this.formSubmitted = false;
    this.formError = '';
    this.formOpen = true;
  }

  openEdit(d: ComponentDocument): void {
    this.openMenu = null;
    this.formMode = 'edit';
    this.target = d;
    this.form = { name: d.name, type: d.type, version: d.version, status: d.status, description: d.description };
    this.pendingFile = null;
    this.fileError = '';
    this.formSubmitted = false;
    this.formError = '';
    this.formOpen = true;
  }

  onFilePicked(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    input.value = '';
    this.fileError = '';
    if (!file) return;
    if (file.size > MAX_UPLOAD_BYTES) {
      this.fileError = `${file.name} is ${formatSize(file.size)}; the limit is 25 MB.`;
      return;
    }
    const ext = '.' + (file.name.split('.').pop()?.toLowerCase() ?? '');
    if (!ACCEPT.split(',').includes(ext)) {
      this.fileError = 'Unsupported file type. Use PDF, image, Word or Excel files.';
      return;
    }
    this.pendingFile = file;
    if (this.formMode === 'new' && !this.form.name.trim()) {
      this.form.name = file.name.replace(/\.[^.]+$/, '').replace(/[_-]+/g, ' ').trim();
    }
    if (this.formMode === 'new' && formatOf(file.name) === 'image') this.form.type = 'Image';
  }

  get needsFile(): boolean {
    return this.formMode !== 'edit';
  }

  saveForm(): void {
    this.formSubmitted = true;
    this.formError = '';
    const f = this.form;
    if (this.saving || !f.name.trim() || !f.version.trim() || (this.needsFile && !this.pendingFile)) return;
    const details = { name: f.name.trim(), type: f.type, version: f.version.trim(), status: f.status, description: f.description.trim() };
    const c = this.comp;
    const target = this.target;
    const mode = this.formMode;
    const file = this.pendingFile;

    if (mode === 'new' && (!c.apiId || !c.assetRegistrationId)) {
      this.formError = 'This component is not saved yet.';
      return;
    }

    const upload$: Observable<UploadedFile | undefined> = file && mode !== 'edit' ? this.api.uploadFile(file) : of(undefined);
    const save$: Observable<ComponentDocument> = upload$.pipe(
      switchMap((uploaded) => {
        if (mode === 'edit' && target) return this.saveDetails(target, details);
        if (mode === 'version' && target) return this.saveDetails(target, details, uploaded);
        return this.api
          .createComponentFile('document', c.assetRegistrationId!, c.apiId!, this.details(details), uploaded!)
          .pipe(map((dto) => toDocument(dto)));
      }),
    );

    this.saving = true;
    save$.subscribe({
      next: (doc) => {
        this.saving = false;
        this.formOpen = false;
        if (mode === 'edit') {
          this.showToast(`${details.name} updated.`);
        } else if (mode === 'version') {
          this.selectedId = doc.id;
          this.showToast(`${details.name} updated to ${details.version}.`);
        } else {
          c.documents = [...(c.documents ?? []), doc];
          this.selectedId = doc.id;
          this.page = 1;
          this.showToast(`${details.name} uploaded.`);
        }
      },
      error: (err) => {
        this.saving = false;
        this.formError = apiErrorMessage(err, mode === 'edit' ? 'Could not save the document.' : 'Could not upload the document.');
      },
    });
  }

  private releaseFile(d: ComponentDocument): void {
    if (d.url?.startsWith('blob:')) {
      URL.revokeObjectURL(d.url);
    }
  }

  // ===== Delete =====

  askDelete(d: ComponentDocument): void {
    this.openMenu = null;
    this.deleteTarget = d;
  }

  confirmDelete(): void {
    const d = this.deleteTarget;
    if (!d || this.saving) return;
    const c = this.comp;
    this.saving = true;
    (d.dto ? this.api.deleteComponentFile(d.dto.id) : of(undefined)).subscribe({
      next: () => {
        this.saving = false;
        this.releaseFile(d);
        c.documents = (c.documents ?? []).filter((x) => x !== d);
        this.checked.delete(d.id);
        if (this.selectedId === d.id) {
          this.selectedId = this.sorted[0]?.id ?? null;
        }
        this.deleteTarget = null;
        this.showToast(`${d.name} deleted.`);
      },
      error: (err) => {
        this.saving = false;
        this.deleteTarget = null;
        this.showToast(apiErrorMessage(err, `Could not delete ${d.name}.`));
      },
    });
  }

  trackById(_: number, row: { id: number }): number {
    return row.id;
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 3000);
  }
}
