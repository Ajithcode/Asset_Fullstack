import { Component, ElementRef, HostListener, Input, ViewChild } from '@angular/core';
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
  /** Present only for files uploaded in this session; sample attachments have no file behind them. */
  file?: File;
  url?: string;
}

interface EditForm {
  baseName: string;
  category: AttachmentCategory;
  description: string;
}

const MAX_UPLOAD_BYTES = 25 * 1024 * 1024;
const EXTENSIONS = ['pdf', 'doc', 'docx', 'xls', 'xlsx', 'jpg', 'jpeg', 'png', 'zip'];
const ACCEPT = EXTENSIONS.map((e) => '.' + e).join(',');
const CURRENT_USER = 'Rex Kumar';

let nextId = 1;

function isoAt(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

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

type Seed = [suffix: string, category: AttachmentCategory, sizeMb: number, by: string, monthsAgo: number, pages: number, description: string];

const SEEDS: Seed[] = [
  ['Manual.pdf', 'Manual', 5.4, CURRENT_USER, 9, 52, 'Operation and maintenance manual. Includes safety instructions, operating procedures and maintenance guidelines.'],
  ['Wiring_Diagram.pdf', 'Drawing', 2.1, 'DCS Service', 9, 4, 'Control and power wiring schematic.'],
  ['Spare_Parts_List.xlsx', 'Parts List', 1.2, 'Supplier', 9, 1, 'Recommended spares with part numbers.'],
  ['Photo.jpg', 'Image', 3.8, CURRENT_USER, 10, 1, 'Photo taken at commissioning.'],
  ['Inspection_Report.docx', 'Report', 1.6, 'DCS Service', 10, 3, 'Pre-commissioning inspection report.'],
  ['Certificate_of_Compliance.pdf', 'Certificate', 0.9, 'Supplier', 10, 2, 'Manufacturer compliance certificate.'],
];

export function buildMockAttachments(c: AssetComponent): ComponentAttachment[] {
  const prefix = c.name.split(/\s+/)[0].replace(/[^A-Za-z0-9]/g, '') || c.tag;
  const now = new Date();
  return SEEDS.map(([suffix, category, sizeMb, by, monthsAgo, pages, description], i) => ({
    id: nextId++,
    fileName: `${prefix}_${suffix}`,
    category,
    size: Math.round(sizeMb * 1_048_576),
    uploadedBy: by,
    uploadedAt: isoAt(new Date(now.getFullYear(), now.getMonth() - monthsAgo, 1 + ((i * 9 + c.id) % 27), 9 + i, (i * 17) % 60)),
    description,
    pages,
  }));
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

  /** Sample attachments are created on first view and kept on the component, so they survive tab switches. */
  @Input({ required: true })
  set component(c: AssetComponent) {
    c.attachments ??= buildMockAttachments(c);
    this.comp = c;
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
    return this.comp.attachments!;
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
    const added: ComponentAttachment[] = [];
    for (const file of files) {
      const error = this.validate(file);
      if (error) {
        this.rejected.push(error);
        continue;
      }
      const a: ComponentAttachment = {
        id: nextId++,
        fileName: file.name,
        category: guessCategory(file.name),
        size: file.size,
        uploadedBy: CURRENT_USER,
        uploadedAt: isoAt(new Date()),
        description: '',
        pages: 1,
        file,
        url: URL.createObjectURL(file),
      };
      this.items.push(a);
      added.push(a);
    }
    if (added.length) {
      this.selectedId = added[0].id;
      this.search = '';
      this.typeFilter = '';
      this.categoryFilter = '';
      this.page = 1;
      this.showToast(`${added.length} file${added.length === 1 ? '' : 's'} uploaded.`);
    }
  }

  /** Swaps the file behind the selected attachment, keeping its category and description. */
  onReplacePicked(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    const a = this.selected;
    if (!file || !a) return;
    const error = this.validate(file);
    if (error) {
      this.showToast(error + '.');
      return;
    }
    this.release(a);
    Object.assign(a, { fileName: file.name, size: file.size, uploadedBy: CURRENT_USER, uploadedAt: isoAt(new Date()), pages: 1, file, url: URL.createObjectURL(file) });
    this.showToast(`File replaced with ${file.name}.`);
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
    const withFiles = this.checkedItems.filter((a) => a.file);
    withFiles.forEach((a) => saveBlob(a.file!, a.fileName));
    const skipped = this.checkedItems.length - withFiles.length;
    this.showToast(
      withFiles.length
        ? `Downloading ${withFiles.length} file${withFiles.length === 1 ? '' : 's'}${skipped ? `; ${skipped} sample file${skipped === 1 ? '' : 's'} skipped` : ''}.`
        : 'The selected attachments are samples with no file attached.',
    );
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
    if (!a.file) {
      this.showToast('This is a sample attachment with no file behind it. Upload a file to download it.');
      return;
    }
    saveBlob(a.file, a.fileName);
  }

  openInNewTab(a: ComponentAttachment): void {
    this.openMenu = null;
    if (a.url) window.open(a.url, '_blank', 'noopener');
  }

  private release(a: ComponentAttachment): void {
    if (a.url) URL.revokeObjectURL(a.url);
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
    if (!a || this.editNameInvalid) return;
    a.fileName = this.editForm.baseName.trim() + extensionOf(a.fileName);
    a.category = this.editForm.category;
    a.description = this.editForm.description.trim();
    this.editTarget = null;
    this.showToast(`${a.fileName} updated.`);
  }

  // ===== Delete =====

  askDelete(targets: ComponentAttachment[]): void {
    this.openMenu = null;
    this.deleteTargets = targets;
  }

  confirmDelete(): void {
    const targets = this.deleteTargets;
    if (!targets.length) return;
    targets.forEach((a) => {
      this.release(a);
      this.checked.delete(a.id);
    });
    this.comp.attachments = this.items.filter((a) => !targets.includes(a));
    if (targets.some((a) => a.id === this.selectedId)) this.selectedId = this.sorted[0]?.id ?? null;
    this.deleteTargets = [];
    this.showToast(targets.length === 1 ? `${targets[0].fileName} deleted.` : `${targets.length} attachments deleted.`);
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
