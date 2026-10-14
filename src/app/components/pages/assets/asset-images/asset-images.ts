import { Component, DestroyRef, HostListener, OnDestroy, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import {
  LucideAngularModule,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  CloudUpload,
  Download,
  EllipsisVertical,
  Eye,
  History,
  Image as ImageIcon,
  LayoutGrid,
  List,
  Maximize2,
  Pencil,
  Plus,
  Printer,
  RefreshCw,
  Search,
  Star,
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
import { AssetImage, ImageEvent, cloneDetail, loadAssetDetail } from '../asset-details/asset-details';

type ViewMode = 'grid' | 'list';

/** One file waiting in the upload dialog. */
interface PendingUpload {
  file: File;
  previewUrl: string;
  title: string;
  width: number;
  height: number;
}

interface UploadForm {
  items: PendingUpload[];
  type: string;
  tags: string;
  location: string;
  description: string;
  remarks: string;
}

interface EditForm {
  id: number;
  title: string;
  description: string;
  type: string;
  tags: string;
  location: string;
  remarks: string;
}

const CURRENT_USER = 'Rex Kumar';
const MAX_FILE_BYTES = 20 * 1024 * 1024;
const MB = 1024 * 1024;

@Component({
  selector: 'app-asset-images',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './asset-images.html',
  // Split to keep each stylesheet under the anyComponentStyle budget
  styleUrls: ['./asset-images.css', './asset-images-preview.css'],
})
export class AssetImages implements OnDestroy {
  readonly icons = {
    ChevronDown, ChevronLeft, ChevronRight, CloudUpload, Download, EllipsisVertical, Eye, History, ImageIcon,
    LayoutGrid, List, Maximize2, Pencil, Plus, Printer, RefreshCw, Search, Star, Trash2, Upload, X,
  };

  /** The asset named by ?tag=; when it is missing or unknown the page shows a "not found" message. */
  asset: AssetHeader = assetHeader(ASSET_RECORDS[0]);
  /** True while the asset's images are being fetched. */
  loading = false;
  notFound = false;
  requestedTag = '';
  readonly tabs: AssetTabLink[] = assetTabs('Images');
  readonly typeIcons = ASSET_TYPE_ICONS;
  readonly statusPill = statusPill;
  readonly conditionPill = conditionPill;
  readonly criticalityPill = criticalityPill;

  readonly imageTypes = ['Asset Overview', 'Component', 'Nameplate', 'Installation', 'Inspection', 'Maintenance', 'Damage / Defect', 'Other'];
  readonly pageSizes = [12, 24, 48];

  images: AssetImage[] = [];
  private nextId = 1;
  /** Image used as the asset photo in the header. */
  headerImageId = 0;

  // ===== Filters, view, paging, selection =====
  search = '';
  typeFilter = '';
  tagFilter = '';
  uploaderFilter = '';
  /** '', 'last30', 'last365', or a year such as '2024'. */
  dateFilter = '';
  view: ViewMode = 'grid';
  pageSize = 12;
  page = 1;
  readonly checked = new Set<number>();

  // ===== Menus =====
  actionsOpen = false;
  bulkOpen = false;
  menuId: number | null = null;

  // ===== Preview =====
  previewOpen = true;
  previewId: number | null = null;
  lightboxOpen = false;

  // ===== Modals =====
  upload: UploadForm | null = null;
  uploadError = '';
  isDragging = false;
  edit: EditForm | null = null;
  editError = '';
  historyId: number | null = null;
  deleteIds: number[] | null = null;
  /** Image whose file is being replaced through the hidden file input. */
  private replaceId: number | null = null;

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  constructor() {
    inject(ActivatedRoute)
      .queryParamMap.pipe(takeUntilDestroyed(inject(DestroyRef)))
      .subscribe((params) => this.load(params.get('tag')));
  }

  /** Shows the asset named in the URL, with the images from its data file. */
  private load(tag: string | null): void {
    const record = findAsset(tag);
    this.requestedTag = tag ?? '';
    this.notFound = !record;
    if (!record) return;
    this.asset = assetHeader(record);
    this.images.forEach((i) => i.file && URL.revokeObjectURL(i.src));
    this.images = [];
    this.headerImageId = 0;
    this.previewOpen = true;
    this.previewId = null;
    this.loading = true;
    loadAssetDetail(record.tag).then((detail) => {
      if (this.requestedTag !== record.tag) return; // the user moved on to another asset
      this.loading = false;
      this.images = detail ? cloneDetail(detail.images) : [];
      this.nextId = Math.max(0, ...this.images.map((i) => i.id)) + 1;
      this.headerImageId = detail?.headerImageId ?? 0;
      this.previewId = this.images[0]?.id ?? null;
    });
    this.lightboxOpen = false;
    this.checked.clear();
    this.clearFilters();
    this.closeMenus();
  }

  ngOnDestroy(): void {
    this.images.forEach((i) => i.file && URL.revokeObjectURL(i.src));
    this.upload?.items.forEach((p) => URL.revokeObjectURL(p.previewUrl));
    clearTimeout(this.toastTimer);
  }

  // ===== Lookups =====

  /** The chosen asset photo, else the asset's default picture, else null (the header shows the type icon). */
  get headerImage(): string | null {
    return this.images.find((i) => i.id === this.headerImageId)?.src ?? this.asset.image;
  }

  get tagOptions(): string[] {
    return [...new Set(this.images.flatMap((i) => i.tags))].sort((a, b) => a.localeCompare(b));
  }

  get uploaderOptions(): string[] {
    return [...new Set(this.images.map((i) => i.uploadedBy))].sort((a, b) => a.localeCompare(b));
  }

  get yearOptions(): string[] {
    return [...new Set(this.images.map((i) => i.uploadedAt.slice(0, 4)))].sort().reverse();
  }

  formatSize(bytes: number): string {
    return bytes >= MB ? `${(bytes / MB).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
  }

  private matchesDate(image: AssetImage): boolean {
    if (!this.dateFilter) return true;
    if (/^\d{4}$/.test(this.dateFilter)) return image.uploadedAt.startsWith(this.dateFilter);
    const days = this.dateFilter === 'last30' ? 30 : 365;
    const since = toIsoDate(new Date(Date.now() - days * 86_400_000));
    return image.uploadedAt.slice(0, 10) >= since;
  }

  get filtered(): AssetImage[] {
    const q = this.search.trim().toLowerCase();
    return this.images.filter(
      (i) =>
        (!this.typeFilter || i.type === this.typeFilter) &&
        (!this.tagFilter || i.tags.includes(this.tagFilter)) &&
        (!this.uploaderFilter || i.uploadedBy === this.uploaderFilter) &&
        this.matchesDate(i) &&
        (!q || [i.title, i.description, i.type, i.fileName, i.remarks, ...i.tags].some((v) => v.toLowerCase().includes(q))),
    );
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.filtered.length / this.pageSize));
  }

  get pageNumbers(): number[] {
    return Array.from({ length: this.pageCount }, (_, i) => i + 1);
  }

  get safePage(): number {
    return Math.min(this.page, this.pageCount);
  }

  get pageItems(): AssetImage[] {
    const start = (this.safePage - 1) * this.pageSize;
    return this.filtered.slice(start, start + this.pageSize);
  }

  get rangeLabel(): string {
    const total = this.filtered.length;
    if (!total) return 'No images';
    const start = (this.safePage - 1) * this.pageSize + 1;
    return `Showing ${start} - ${Math.min(start + this.pageSize - 1, total)} of ${total} images`;
  }

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
    return !!(this.search || this.typeFilter || this.tagFilter || this.uploaderFilter || this.dateFilter);
  }

  clearFilters(): void {
    this.search = this.typeFilter = this.tagFilter = this.uploaderFilter = this.dateFilter = '';
    this.page = 1;
  }

  // ===== Selection =====

  get selectedIds(): number[] {
    return this.images.filter((i) => this.checked.has(i.id)).map((i) => i.id);
  }

  get allPageChecked(): boolean {
    return this.pageItems.length > 0 && this.pageItems.every((i) => this.checked.has(i.id));
  }

  get somePageChecked(): boolean {
    return !this.allPageChecked && this.pageItems.some((i) => this.checked.has(i.id));
  }

  toggleAll(on: boolean): void {
    this.pageItems.forEach((i) => (on ? this.checked.add(i.id) : this.checked.delete(i.id)));
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

  toggleMenu(event: Event, id: number): void {
    event.stopPropagation();
    const open = this.menuId !== id;
    this.closeMenus();
    this.menuId = open ? id : null;
  }

  @HostListener('document:click')
  closeMenus(): void {
    this.actionsOpen = this.bulkOpen = false;
    this.menuId = null;
  }

  @HostListener('document:keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      this.closeMenus();
      if (this.lightboxOpen) {
        this.lightboxOpen = false;
        return;
      }
      this.upload = this.edit = null;
      this.historyId = null;
      this.deleteIds = null;
      return;
    }
    // Arrow keys page through images, unless the user is typing or a dialog is open
    const target = event.target as HTMLElement;
    const typing = /^(INPUT|SELECT|TEXTAREA)$/.test(target.tagName);
    const dialogOpen = !!(this.upload || this.edit || this.historyId !== null || this.deleteIds);
    if (typing || dialogOpen || !(this.previewOpen || this.lightboxOpen)) return;
    if (event.key === 'ArrowLeft') this.step(-1);
    if (event.key === 'ArrowRight') this.step(1);
  }

  tabClick(tab: AssetTabLink): void {
    if (!tab.path && !tab.active) this.showToast(`${tab.label} is not available in this demo.`);
  }

  // ===== Preview =====

  get previewImage(): AssetImage | undefined {
    return this.images.find((i) => i.id === this.previewId);
  }

  /** Position of the previewed image within the filtered list, e.g. "1 / 24". */
  get previewCounter(): string {
    const list = this.filtered;
    const index = list.findIndex((i) => i.id === this.previewId);
    return index >= 0 ? `${index + 1} / ${list.length}` : `– / ${list.length}`;
  }

  openPreview(image: AssetImage): void {
    this.closeMenus();
    this.previewOpen = true;
    this.previewId = image.id;
  }

  closePreview(): void {
    this.previewOpen = false;
  }

  /** Previous / next image in the filtered list, wrapping round. */
  step(direction: 1 | -1): void {
    const list = this.filtered;
    if (!list.length) return;
    const index = list.findIndex((i) => i.id === this.previewId);
    const next = index < 0 ? 0 : (index + direction + list.length) % list.length;
    this.previewId = list[next].id;
    // Keep the grid on the page that holds the previewed image
    this.page = Math.floor(next / this.pageSize) + 1;
  }

  openLightbox(image?: AssetImage): void {
    this.closeMenus();
    if (image) this.previewId = image.id;
    this.lightboxOpen = true;
  }

  setAsAssetPhoto(image: AssetImage): void {
    this.closeMenus();
    this.headerImageId = image.id;
    this.logEvent(image, 'Set as asset photo');
    this.showToast(`${image.title} is now the asset photo.`);
  }

  // ===== Download / export =====

  download(image: AssetImage): void {
    this.closeMenus();
    if (image.file) {
      saveUrl(image.src, image.fileName);
      return;
    }
    // Sample images are SVG illustrations: save them under the image's name with their real extension
    const ext = image.src.split('.').pop() ?? 'svg';
    saveUrl(image.src, image.fileName.replace(/\.[^.]+$/, '') + '.' + ext);
  }

  downloadSelected(): void {
    this.closeMenus();
    const items = this.images.filter((i) => this.checked.has(i.id));
    if (!items.length) {
      this.showToast('Select images first (switch to the list view to tick them).');
      return;
    }
    items.forEach((i) => this.download(i));
    this.showToast(`Downloading ${items.length} image(s).`);
  }

  exportCsv(): void {
    this.closeMenus();
    const head = ['#', 'Image Name', 'Image Type', 'Tags', 'Uploaded By', 'Upload Date', 'Location', 'File Name', 'File Size', 'Resolution', 'Description', 'Remarks'];
    const rows = this.filtered.map((i, n) => [
      n + 1, i.title, i.type, i.tags.join('; '), i.uploadedBy, i.uploadedAt.replace('T', ' '), i.location, i.fileName,
      this.formatSize(i.size), i.width ? `${i.width} x ${i.height}` : '-', i.description, i.remarks,
    ]);
    const csv = [head, ...rows].map((row) => row.map((v) => `"${String(v).replace(/"/g, '""')}"`).join(',')).join('\r\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }));
    saveUrl(url, `${this.asset.tag}-images.csv`);
    setTimeout(() => URL.revokeObjectURL(url));
  }

  print(): void {
    this.closeMenus();
    window.print();
  }

  // ===== Upload =====

  openUpload(): void {
    this.closeMenus();
    this.uploadError = '';
    this.upload = { items: [], type: '', tags: '', location: this.asset.location, description: '', remarks: '' };
  }

  closeUpload(): void {
    this.upload?.items.forEach((p) => URL.revokeObjectURL(p.previewUrl));
    this.upload = null;
  }

  onUploadFiles(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.addPending(Array.from(input.files ?? []));
    input.value = '';
  }

  onUploadDrop(event: DragEvent): void {
    event.preventDefault();
    this.isDragging = false;
    this.addPending(Array.from(event.dataTransfer?.files ?? []));
  }

  private addPending(files: File[]): void {
    const upload = this.upload;
    if (!upload) return;
    const rejected: string[] = [];
    for (const file of files) {
      if (!file.type.startsWith('image/') || file.size > MAX_FILE_BYTES) {
        rejected.push(file.name);
        continue;
      }
      const item: PendingUpload = {
        file,
        previewUrl: URL.createObjectURL(file),
        title: titleFromFileName(file.name),
        width: 0,
        height: 0,
      };
      upload.items = [...upload.items, item];
      readDimensions(item.previewUrl).then(([w, h]) => {
        item.width = w;
        item.height = h;
      });
    }
    this.uploadError = rejected.length ? `Not added (images only, up to 20 MB): ${rejected.join(', ')}` : '';
  }

  removePending(index: number): void {
    if (!this.upload) return;
    URL.revokeObjectURL(this.upload.items[index].previewUrl);
    this.upload.items = this.upload.items.filter((_, i) => i !== index);
  }

  saveUpload(): void {
    const u = this.upload;
    if (!u) return;
    if (!u.items.length) {
      this.uploadError = 'Choose at least one image.';
      return;
    }
    if (!u.type) {
      this.uploadError = 'Select the image type.';
      return;
    }
    if (u.items.some((p) => !p.title.trim())) {
      this.uploadError = 'Give every image a name.';
      return;
    }
    const at = toIsoDateTime(new Date());
    const added: AssetImage[] = u.items.map((p) => ({
      id: this.nextId++,
      title: p.title.trim(),
      description: u.description.trim(),
      type: u.type,
      tags: splitTags(u.tags),
      uploadedBy: CURRENT_USER,
      uploadedAt: at,
      location: u.location.trim(),
      fileName: p.file.name,
      size: p.file.size,
      width: p.width,
      height: p.height,
      remarks: u.remarks.trim(),
      // The dialog's preview URL becomes the image's own URL
      src: p.previewUrl,
      file: p.file,
      history: [{ action: 'Image uploaded', by: CURRENT_USER, at }],
    }));
    this.images = [...this.images, ...added];
    this.upload = null;
    this.clearFilters();
    this.page = this.pageCount;
    this.openPreview(added[0]);
    this.showToast(added.length === 1 ? `${added[0].title} uploaded.` : `${added.length} images uploaded.`);
  }

  // ===== Replace file =====

  startReplace(image: AssetImage, input: HTMLInputElement): void {
    this.closeMenus();
    this.replaceId = image.id;
    input.click();
  }

  onReplaceFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    const image = this.images.find((i) => i.id === this.replaceId);
    this.replaceId = null;
    if (!file || !image) return;
    if (!file.type.startsWith('image/') || file.size > MAX_FILE_BYTES) {
      this.showToast('Choose an image file up to 20 MB.');
      return;
    }
    if (image.file) URL.revokeObjectURL(image.src);
    image.file = file;
    image.src = URL.createObjectURL(file);
    image.fileName = file.name;
    image.size = file.size;
    image.width = image.height = 0;
    readDimensions(image.src).then(([w, h]) => {
      image.width = w;
      image.height = h;
    });
    this.logEvent(image, `Image replaced with ${file.name}`);
    this.openPreview(image);
    this.showToast(`${image.title} replaced.`);
  }

  // ===== Edit details =====

  openEdit(image: AssetImage): void {
    this.closeMenus();
    this.editError = '';
    this.edit = {
      id: image.id, title: image.title, description: image.description, type: image.type,
      tags: image.tags.join(', '), location: image.location, remarks: image.remarks,
    };
  }

  saveEdit(): void {
    const e = this.edit;
    if (!e) return;
    if (!e.title.trim() || !e.type) {
      this.editError = 'Enter the image name and type.';
      return;
    }
    const image = this.images.find((i) => i.id === e.id);
    if (!image) return;
    Object.assign(image, {
      title: e.title.trim(), description: e.description.trim(), type: e.type, tags: splitTags(e.tags),
      location: e.location.trim(), remarks: e.remarks.trim(),
    });
    this.logEvent(image, 'Details edited');
    this.edit = null;
    this.showToast('Image details saved.');
  }

  // ===== History =====

  openHistory(image: AssetImage): void {
    this.closeMenus();
    this.historyId = image.id;
  }

  get historyImage(): AssetImage | undefined {
    return this.images.find((i) => i.id === this.historyId);
  }

  get historyEvents(): ImageEvent[] {
    return [...(this.historyImage?.history ?? [])].reverse();
  }

  private logEvent(image: AssetImage, action: string): void {
    image.history = [...image.history, { action, by: CURRENT_USER, at: toIsoDateTime(new Date()) }];
  }

  // ===== Delete =====

  askDelete(ids: number[]): void {
    this.closeMenus();
    if (!ids.length) {
      this.showToast('Select images first (switch to the list view to tick them).');
      return;
    }
    this.deleteIds = ids;
  }

  get deleteLabel(): string {
    const ids = this.deleteIds ?? [];
    return ids.length === 1 ? this.images.find((i) => i.id === ids[0])?.title ?? '' : `${ids.length} images`;
  }

  confirmDelete(): void {
    const ids = new Set(this.deleteIds ?? []);
    this.images.filter((i) => ids.has(i.id) && i.file).forEach((i) => URL.revokeObjectURL(i.src));
    this.images = this.images.filter((i) => !ids.has(i.id));
    ids.forEach((id) => this.checked.delete(id));
    if (ids.has(this.headerImageId)) this.headerImageId = this.images[0]?.id ?? 0;
    if (this.previewId !== null && ids.has(this.previewId)) {
      this.previewId = this.filtered[0]?.id ?? null;
      this.lightboxOpen = false;
    }
    this.showToast(ids.size === 1 ? 'Image deleted.' : `${ids.size} images deleted.`);
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

function splitTags(text: string): string[] {
  return [...new Set(text.split(',').map((t) => t.trim()).filter(Boolean))];
}

/** "engine_bay-01.jpg" -> "Engine bay 01". */
function titleFromFileName(name: string): string {
  const base = name.replace(/\.[^.]+$/, '').replace(/[_-]+/g, ' ').trim();
  return base ? base.charAt(0).toUpperCase() + base.slice(1) : name;
}

/** Natural pixel size of an image URL; [0, 0] if it can't be read. */
function readDimensions(url: string): Promise<[number, number]> {
  return new Promise((resolve) => {
    const img = new Image();
    img.onload = () => resolve([img.naturalWidth, img.naturalHeight]);
    img.onerror = () => resolve([0, 0]);
    img.src = url;
  });
}

function saveUrl(url: string, name: string): void {
  const link = document.createElement('a');
  link.href = url;
  link.download = name;
  link.click();
}
