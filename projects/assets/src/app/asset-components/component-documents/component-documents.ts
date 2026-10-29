import { Component, HostListener, Input } from '@angular/core';
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
  /** Present only for files uploaded in this session; sample documents have no file behind them. */
  file?: File;
  url?: string;
}

interface UploadForm {
  name: string;
  type: DocumentType;
  version: string;
  status: DocumentStatus;
  description: string;
}

const MAX_UPLOAD_BYTES = 25 * 1024 * 1024;
const ACCEPT = '.pdf,.png,.jpg,.jpeg,.webp,.doc,.docx,.xls,.xlsx,.csv';
const CURRENT_USER = 'Rex Kumar';

let nextId = 1;

function nowIso(): string {
  const d = new Date();
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** Bumps the last number of a "v2.1"-style version: v2.1 → v2.2, v3 → v4. */
function bumpVersion(v: string): string {
  const m = /^(.*?)(\d+)$/.exec(v.trim());
  return m ? `${m[1]}${Number(m[2]) + 1}` : `${v.trim() || 'v1'}.1`;
}

type Seed = [name: string, type: DocumentType, version: string, monthsAgo: number, by: string, ext: string, pages: number, sizeKb: number, description: string];

/** Sample document set; dated relative to today so the list stays plausible. */
function seedsFor(c: AssetComponent): Seed[] {
  const model = c.model || c.tag;
  return [
    ['Technical Manual', 'Manual', 'v2.1', 9, CURRENT_USER, 'pdf', 52, 5530, `Operation and maintenance manual for ${model}. Includes safety instructions, operating procedures and maintenance guidelines.`],
    ['Installation Guide', 'Installation', 'v1.3', 9, 'DCS Service', 'pdf', 18, 2140, 'Site preparation, mounting and commissioning steps.'],
    ['Wiring Diagram', 'Drawing', 'v1.0', 8, 'Supplier', 'pdf', 4, 860, 'Control and power wiring schematic.'],
    ['Spare Parts List', 'Parts List', 'v1.2', 7, 'DCS Service', 'pdf', 12, 640, 'Recommended spares with part numbers and quantities.'],
    ['Maintenance Schedule', 'Schedule', 'v1.0', 7, CURRENT_USER, 'xlsx', 1, 96, 'Planned maintenance intervals by running hours.'],
    ['Certificate of Compliance', 'Certificate', 'v1.0', 9, 'Supplier', 'pdf', 2, 310, 'Manufacturer compliance certificate.'],
    ['Warranty Document', 'Warranty', 'v1.0', 9, 'Supplier', 'pdf', 3, 280, 'Warranty terms, coverage period and claim procedure.'],
    ['Site Photo', 'Image', 'v1.0', 8, CURRENT_USER, 'jpg', 1, 2380, 'Installed unit at site.'],
    ['Test Report', 'Test Report', 'v1.1', 7, 'DCS Service', 'pdf', 6, 720, 'Factory and site acceptance test results.'],
    ['Installation Report', 'Report', 'v1.0', 6, 'DCS Service', 'docx', 5, 410, 'Installation sign-off report.'],
    ['Load Bank Test Certificate', 'Certificate', 'v1.0', 5, 'DCS Service', 'pdf', 2, 190, 'Load bank test at 100% rated load.'],
    ['Old Wiring Diagram', 'Drawing', 'v0.9', 11, 'Supplier', 'pdf', 4, 820, 'Superseded by v1.0.'],
  ];
}

export function buildMockDocuments(c: AssetComponent): ComponentDocument[] {
  const base = new Date();
  return seedsFor(c).map(([name, type, version, monthsAgo, by, ext, pages, sizeKb, description], i) => {
    const d = new Date(base.getFullYear(), base.getMonth() - monthsAgo, 1 + ((i * 7 + c.id) % 27), 9 + (i % 7), (i * 13) % 60);
    const pad = (n: number) => String(n).padStart(2, '0');
    return {
      id: nextId++,
      name,
      fileName: `${(c.model || c.tag).replace(/\s+/g, '')}_${name.replace(/\s+/g, '_')}_${version}.${ext}`,
      type,
      version,
      uploadedAt: `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`,
      uploadedBy: by,
      status: name.startsWith('Old ') ? 'Archived' : 'Active',
      size: sizeKb * 1024,
      pages,
      description,
    };
  });
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

  /** Sample documents are created on first view and kept on the component, so they survive tab switches. */
  @Input({ required: true })
  set component(c: AssetComponent) {
    c.documents ??= buildMockDocuments(c);
    this.comp = c;
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
    return this.comp.documents!;
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
    rows.forEach((d) => (d.status = status));
    this.checked.clear();
    this.showToast(`${rows.length} document${rows.length === 1 ? '' : 's'} ${status === 'Archived' ? 'archived' : 'restored'}.`);
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
    if (!d.file) {
      this.showToast('This is a sample document with no file attached. Upload a file to download it.');
      return;
    }
    saveBlob(d.file, d.fileName);
  }

  openInNewTab(d: ComponentDocument): void {
    this.openMenu = null;
    if (d.url) window.open(d.url, '_blank', 'noopener');
  }

  toggleArchive(d: ComponentDocument): void {
    this.openMenu = null;
    d.status = d.status === 'Active' ? 'Archived' : 'Active';
    this.showToast(`${d.name} ${d.status === 'Archived' ? 'archived' : 'restored'}.`);
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
    const f = this.form;
    if (!f.name.trim() || !f.version.trim() || (this.needsFile && !this.pendingFile)) return;
    const details = { name: f.name.trim(), type: f.type, version: f.version.trim(), status: f.status, description: f.description.trim() };

    if (this.formMode === 'edit' && this.target) {
      Object.assign(this.target, details);
      this.showToast(`${details.name} updated.`);
    } else if (this.pendingFile) {
      const fileFields = {
        fileName: this.pendingFile.name,
        size: this.pendingFile.size,
        pages: 1,
        uploadedAt: nowIso(),
        uploadedBy: CURRENT_USER,
        file: this.pendingFile,
        url: URL.createObjectURL(this.pendingFile),
      };
      if (this.formMode === 'version' && this.target) {
        this.releaseFile(this.target);
        Object.assign(this.target, details, fileFields);
        this.selectedId = this.target.id;
        this.showToast(`${details.name} updated to ${details.version}.`);
      } else {
        const created: ComponentDocument = { id: nextId++, ...details, ...fileFields };
        this.docs.push(created);
        this.selectedId = created.id;
        this.page = 1;
        this.showToast(`${details.name} uploaded.`);
      }
    }
    this.formOpen = false;
  }

  private releaseFile(d: ComponentDocument): void {
    if (d.url) {
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
    if (!d) return;
    this.releaseFile(d);
    this.comp.documents = this.docs.filter((x) => x !== d);
    this.checked.delete(d.id);
    if (this.selectedId === d.id) {
      this.selectedId = this.sorted[0]?.id ?? null;
    }
    this.deleteTarget = null;
    this.showToast(`${d.name} deleted.`);
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
