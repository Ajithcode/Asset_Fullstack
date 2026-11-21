import { Component, ElementRef, HostListener, Input, ViewChild, inject } from '@angular/core';
import { Observable, concat, map, of, switchMap, toArray } from 'rxjs';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  LucideAngularModule,
  ChevronLeft,
  ChevronRight,
  CloudUpload,
  Download,
  Ellipsis,
  EllipsisVertical,
  ExternalLink,
  Eye,
  FileText,
  Inbox,
  Paperclip,
  Plus,
  Replace,
  Search,
  SquarePen,
  Trash2,
  X,
} from 'lucide-angular';
import type { AssetComponent } from '../asset-components';
import {
  FORMAT_ICONS,
  FORMAT_LABELS,
  FilePreview,
  type FileFormat,
  formatOf,
  formatSize,
  saveBlob,
} from '../file-preview/file-preview';
import { AssetComponentsService, isoLocalDateTime, type AssetDocumentDto, type ComponentFileDetails, type UploadedFile } from '../asset-components.service';
import { apiErrorMessage } from '../../services/asset-api';

export type AttachmentCategory = 'Manual' | 'Drawing' | 'Parts List' | 'Image' | 'Report' | 'Certificate' | 'Other';

export interface ComponentAttachment {
  id: number;
  fileName: string;
  category: AttachmentCategory;
  size: number;
  uploadedBy: string;
  /** ISO date-time (yyyy-mm-ddTHH:mm). */
  uploadedAt: string;
  description: string;
  /** Page count for the preview pager on sample files. */
  pages: number;
  /** Present only for files uploaded in this session. */
  file?: File;
  /** Stored file url (from the Media service). */
  url?: string;
  /** The asset document behind this row (/api/asset-documents). */
  dto?: AssetDocumentDto;
}

interface EditForm {
  baseName: string;
  category: AttachmentCategory;
  description: string;
}

const MAX_UPLOAD_BYTES = 25 * 1024 * 1024;
const EXTENSIONS = ['pdf', 'doc', 'docx', 'xls', 'xlsx', 'jpg', 'jpeg', 'png'];
const ACCEPT = EXTENSIONS.map((e) => '.' + e).join(',');
let nextId = 1;

function extensionOf(fileName: string): string {
  const i = fileName.lastIndexOf('.');
  return i > 0 ? fileName.slice(i) : '';
}

/** Best guess at a category from the file's name and type; the user can change it afterwards. */
function guessCategory(fileName: string): AttachmentCategory {
  const n = fileName.toLowerCase();
  if (formatOf(fileName) === 'image') return 'Image';
  if (/manual|guide|handbook/.test(n)) return 'Manual';
  if (/diagram|drawing|schematic|layout/.test(n)) return 'Drawing';
  if (/part|spares?|bom/.test(n)) return 'Parts List';
  if (/certificate|compliance|cert/.test(n)) return 'Certificate';
  if (/report|inspection|test/.test(n)) return 'Report';
  return 'Other';
}

const CATEGORIES: AttachmentCategory[] = ['Manual', 'Drawing', 'Parts List', 'Image', 'Report', 'Certificate', 'Other'];

/** Attachments are stored as asset documents: the display name is the document name, the category its type. */
function toAttachment(dto: AssetDocumentDto): ComponentAttachment {
  return {
    id: nextId++,
    fileName: dto.documentName || dto.fileName,
    category: CATEGORIES.find((c) => c === dto.documentType) ?? 'Other',
    size: dto.fileSize ?? 0,
    uploadedBy: dto.uploadedBy ?? '',
    uploadedAt: isoLocalDateTime(dto.uploadDate),
    description: dto.description ?? '',
    pages: 1,
    url: dto.fileUrl || undefined,
    dto,
  };
}

function detailsOf(a: Pick<ComponentAttachment, 'fileName' | 'category' | 'description'>, version = 'v1.0'): ComponentFileDetails {
  return { documentName: a.fileName, documentType: a.category, version, status: 'Active', description: a.description };
}

@Component({
  selector: 'app-component-attachments',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideAngularModule, FilePreview],
  templateUrl: './component-attachments.html',
  // Reuses the page's card, table, pill, menu and modal styles.
  styleUrls: ['../asset-components.css', './component-attachments.css'],
})
export class ComponentAttachments {
  @ViewChild('replaceInput') private replaceInput?: ElementRef<HTMLInputElement>;

  readonly icons = {
    ChevronLeft, ChevronRight, CloudUpload, Download, Ellipsis, EllipsisVertical, ExternalLink, Eye, FileText, Inbox,
    Paperclip, Plus, Replace, Search, SquarePen, Trash2, X,
  };

  readonly formatIcons = FORMAT_ICONS;
  readonly formatLabels = FORMAT_LABELS;
  readonly formatSize = formatSize;
  readonly categories: AttachmentCategory[] = ['Manual', 'Drawing', 'Parts List', 'Image', 'Report', 'Certificate', 'Other'];
  readonly fileTypes: FileFormat[] = ['pdf', 'word', 'sheet', 'image', 'zip'];
  readonly pageSizeOptions = [10, 25, 50];
  readonly accept = ACCEPT;
  readonly supportedLabel = EXTENSIONS.filter((e) => e !== 'jpeg').map((e) => e.toUpperCase()).join(', ');

  /** Shown on the sample title page. */
  @Input() manufacturer = '';

  private comp!: AssetComponent;

  private readonly api = inject(AssetComponentsService);

  loading = false;
  loadError = '';
  /** An upload / edit / delete is in flight. */
  saving = false;

  /** Attachments are loaded from the API on first view and kept on the component, so they survive tab switches. */
  @Input({ required: true })
  set component(c: AssetComponent) {
    this.comp = c;
    if (!c.attachments) {
      c.attachments = [];
      this.load();
    }
    this.search = '';
    this.typeFilter = '';
    this.categoryFilter = '';
    this.page = 1;
    this.checked.clear();
    this.openMenu = null;
    this.selectedId = this.sorted[0]?.id ?? null;
  }

  get component(): AssetComponent {
    return this.comp;
  }

  get items(): ComponentAttachment[] {
    return this.comp.attachments ?? [];
  }

  /** Loads the component's attachments from the API. */
  load(): void {
    const c = this.comp;
    if (!c.apiId || !c.assetRegistrationId) return;
    this.loading = true;
    this.loadError = '';
    this.api.listComponentFiles('attachment', c.assetRegistrationId, c.apiId).subscribe({
      next: (dtos) => {
        this.loading = false;
        c.attachments = dtos.map(toAttachment);
        if (c === this.comp && !this.selected) this.selectedId = this.sorted[0]?.id ?? null;
      },
      error: (err) => {
        this.loading = false;
        c.attachments = undefined;
        this.loadError = apiErrorMessage(err, 'Could not load attachments.');
      },
    });
  }

  /** Saves an attachment's details (and optionally a new file) and updates the row from the response. */
  private saveAttachment(a: ComponentAttachment, changes: Partial<ComponentAttachment>, file?: UploadedFile): Observable<ComponentAttachment> {
    if (!a.dto) return of(Object.assign(a, changes));
    return this.api.updateComponentFile(a.dto, detailsOf({ ...a, ...changes }, a.dto.version || 'v1.0'), file).pipe(
      map((dto) => {
        this.release(a);
        return Object.assign(a, { ...toAttachment(dto), id: a.id, file: undefined });
      }),
    );
  }

  search = '';
  typeFilter: '' | FileFormat = '';
  categoryFilter: '' | AttachmentCategory = '';
  page = 1;
  pageSize = 10;
  checked = new Set<number>();
  openMenu: string | null = null;
  selectedId: number | null = null;

  dragging = false;
  /** Files rejected by the last upload, shown under the drop zone. */
  rejected: string[] = [];

  editTarget: ComponentAttachment | null = null;
  editForm: EditForm = { baseName: '', category: 'Other', description: '' };
  editSubmitted = false;

  /** One attachment, or the checked set when deleting in bulk. */
  deleteTargets: ComponentAttachment[] = [];

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  format(a: ComponentAttachment): FileFormat {
    return formatOf(a.fileName);
  }

  /** "Engine_Manual.pdf" → "Engine Manual", for the sample title page. */
  titleOf(a: ComponentAttachment): string {
    return a.fileName.slice(0, a.fileName.length - extensionOf(a.fileName).length).replace(/[_-]+/g, ' ');
  }

  // ===== Upload =====

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.dragging = true;
  }

  onDragLeave(event: DragEvent): void {
    // Ignore leave events fired when moving over the zone's own children.
    if (!(event.currentTarget as HTMLElement).contains(event.relatedTarget as Node)) this.dragging = false;
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragging = false;
    this.addFiles(Array.from(event.dataTransfer?.files ?? []));
  }

  onFilesPicked(event: Event): void {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    input.value = '';
    this.addFiles(files);
  }

  private validate(file: File): string | null {
    const ext = file.name.split('.').pop()?.toLowerCase() ?? '';
    if (!EXTENSIONS.includes(ext)) return `${file.name}: unsupported file type`;
    if (file.size > MAX_UPLOAD_BYTES) return `${file.name}: ${formatSize(file.size)} is over 25 MB`;
    return null;
  }

  private addFiles(files: File[]): void {
    if (!files.length) return;
    this.rejected = [];
    const c = this.comp;
    if (!c.apiId || !c.assetRegistrationId) {
      this.showToast('This component is not saved yet.');
      return;
    }
    const valid: File[] = [];
    for (const file of files) {
      const error = this.validate(file);
      if (error) this.rejected.push(error);
      else valid.push(file);
    }
    if (!valid.length) return;

    // One at a time: upload the file, then save it as an attachment of this component.
    const added: ComponentAttachment[] = [];
    this.saving = true;
    concat(
      ...valid.map((file) =>
        this.api.uploadFile(file).pipe(
          switchMap((uploaded) =>
            this.api.createComponentFile(
              'attachment',
              c.assetRegistrationId!,
              c.apiId!,
              detailsOf({ fileName: file.name, category: guessCategory(file.name), description: '' }),
              uploaded,
            ),
          ),
          map((dto) => {
            const a = toAttachment(dto);
            c.attachments = [...(c.attachments ?? []), a];
            added.push(a);
            return a;
          }),
        ),
      ),
    )
      .pipe(toArray())
      .subscribe({
        next: () => {
          this.saving = false;
          this.afterAdd(added);
        },
        error: (err) => {
          this.saving = false;
          this.afterAdd(added);
          this.rejected.push(apiErrorMessage(err, 'Upload failed.'));
        },
      });
  }

  private afterAdd(added: ComponentAttachment[]): void {
    if (!added.length) return;
    this.selectedId = added[0].id;
    this.search = '';
    this.typeFilter = '';
    this.categoryFilter = '';
    this.page = 1;
    this.showToast(`${added.length} file${added.length === 1 ? '' : 's'} uploaded.`);
  }

  /** Swaps the file behind the selected attachment, keeping its category and description. */
  onReplacePicked(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    const a = this.selected;
    if (!file || !a || this.saving) return;
    const error = this.validate(file);
    if (error) {
      this.showToast(error + '.');
      return;
    }
    this.saving = true;
    this.api
      .uploadFile(file)
      .pipe(switchMap((uploaded) => this.saveAttachment(a, { fileName: file.name }, uploaded)))
      .subscribe({
        next: () => {
          this.saving = false;
          this.showToast(`File replaced with ${file.name}.`);
        },
        error: (err) => {
          this.saving = false;
          this.showToast(apiErrorMessage(err, 'Could not replace the file.'));
        },
      });
  }

  pickReplacement(): void {
    this.openMenu = null;
    this.replaceInput?.nativeElement.click();
  }

  // ===== Table =====

  get sorted(): ComponentAttachment[] {
    return [...this.items].sort((a, b) => b.uploadedAt.localeCompare(a.uploadedAt) || b.id - a.id);
  }

  get filtered(): ComponentAttachment[] {
    const q = this.search.trim().toLowerCase();
    return this.sorted.filter(
      (a) =>
        (!q || [a.fileName, a.category, a.uploadedBy, a.description].some((v) => v.toLowerCase().includes(q))) &&
        (!this.typeFilter || this.format(a) === this.typeFilter) &&
        (!this.categoryFilter || a.category === this.categoryFilter),
    );
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.filtered.length / this.pageSize));
  }

  get currentPage(): number {
    return Math.min(this.page, this.totalPages);
  }

  get pageRows(): ComponentAttachment[] {
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
    return this.pageRows.length > 0 && this.pageRows.every((a) => this.checked.has(a.id));
  }

  get someChecked(): boolean {
    return !this.allChecked && this.pageRows.some((a) => this.checked.has(a.id));
  }

  toggleAll(): void {
    const on = !this.allChecked;
    this.pageRows.forEach((a) => (on ? this.checked.add(a.id) : this.checked.delete(a.id)));
  }

  toggleRow(id: number): void {
    if (this.checked.has(id)) this.checked.delete(id);
    else this.checked.add(id);
  }

  get checkedItems(): ComponentAttachment[] {
    return this.items.filter((a) => this.checked.has(a.id));
  }

  downloadChecked(): void {
    const withFiles = this.checkedItems.filter((a) => a.file || a.url);
    withFiles.forEach((a) => this.saveFile(a));
    const skipped = this.checkedItems.length - withFiles.length;
    this.showToast(
      withFiles.length
        ? `Downloading ${withFiles.length} file${withFiles.length === 1 ? '' : 's'}${skipped ? `; ${skipped} without a stored file skipped` : ''}.`
        : 'The selected attachments have no stored file.',
    );
  }

  private saveFile(a: ComponentAttachment): void {
    if (a.file) {
      saveBlob(a.file, a.fileName);
      return;
    }
    if (!a.url) return;
    this.api.fetchFile(a.url).subscribe({
      next: (blob) => saveBlob(blob, a.fileName),
      // Fall back to letting the browser open the stored file.
      error: () => window.open(a.url, '_blank', 'noopener'),
    });
  }

  // ===== Selection & file actions =====

  get selected(): ComponentAttachment | null {
    return this.items.find((a) => a.id === this.selectedId) ?? null;
  }

  select(a: ComponentAttachment): void {
    this.selectedId = a.id;
  }

  download(a: ComponentAttachment): void {
    this.openMenu = null;
    if (!a.file && !a.url) {
      this.showToast('No file is stored for this attachment.');
      return;
    }
    this.saveFile(a);
  }

  openInNewTab(a: ComponentAttachment): void {
    this.openMenu = null;
    if (a.url) window.open(a.url, '_blank', 'noopener');
  }

  private release(a: ComponentAttachment): void {
    if (a.url?.startsWith('blob:')) URL.revokeObjectURL(a.url);
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
    this.editTarget = null;
    this.deleteTargets = [];
  }

  // ===== Edit =====

  openEdit(a: ComponentAttachment): void {
    this.openMenu = null;
    this.editTarget = a;
    this.editForm = { baseName: a.fileName.slice(0, a.fileName.length - extensionOf(a.fileName).length), category: a.category, description: a.description };
    this.editSubmitted = false;
  }

  get editNameInvalid(): boolean {
    return !this.editForm.baseName.trim() || /[\\/:*?"<>|]/.test(this.editForm.baseName);
  }

  saveEdit(): void {
    this.editSubmitted = true;
    const a = this.editTarget;
    if (!a || this.editNameInvalid || this.saving) return;
    const changes = {
      fileName: this.editForm.baseName.trim() + extensionOf(a.fileName),
      category: this.editForm.category,
      description: this.editForm.description.trim(),
    };
    this.saving = true;
    this.saveAttachment(a, changes).subscribe({
      next: () => {
        this.saving = false;
        this.editTarget = null;
        this.showToast(`${a.fileName} updated.`);
      },
      error: (err) => {
        this.saving = false;
        this.showToast(apiErrorMessage(err, 'Could not save the attachment.'));
      },
    });
  }

  // ===== Delete =====

  askDelete(targets: ComponentAttachment[]): void {
    this.openMenu = null;
    this.deleteTargets = targets;
  }

  confirmDelete(): void {
    const targets = this.deleteTargets;
    if (!targets.length || this.saving) return;
    const c = this.comp;
    const ids = targets.map((a) => a.dto?.id).filter((id): id is string => !!id);
    const delete$: Observable<unknown> =
      ids.length === 0 ? of(null) : ids.length === 1 ? this.api.deleteComponentFile(ids[0]) : this.api.deleteComponentFiles(ids);
    this.saving = true;
    delete$.subscribe({
      next: () => {
        this.saving = false;
        targets.forEach((a) => {
          this.release(a);
          this.checked.delete(a.id);
        });
        c.attachments = (c.attachments ?? []).filter((a) => !targets.includes(a));
        if (targets.some((a) => a.id === this.selectedId)) this.selectedId = this.sorted[0]?.id ?? null;
        this.deleteTargets = [];
        this.showToast(targets.length === 1 ? `${targets[0].fileName} deleted.` : `${targets.length} attachments deleted.`);
      },
      error: (err) => {
        this.saving = false;
        this.deleteTargets = [];
        this.showToast(apiErrorMessage(err, 'Could not delete the attachment.'));
        this.load();
      },
    });
  }

  trackById(_: number, row: { id: number }): number {
    return row.id;
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 3200);
  }
}
