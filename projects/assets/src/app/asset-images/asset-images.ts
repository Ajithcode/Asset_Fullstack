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
  assetLocation,
  assetTabs,
  conditionPill,
  criticalityPill,
  findAsset,
  statusPill,
} from '../asset-data';
import { AssetDetail, AssetImage, ImageEvent, cloneDetail, loadAssetDetail } from '../asset-details/asset-details';

type ViewMode = 'grid' | 'list';

/** Images of one asset uploaded together: one row in the all-assets list. */
interface ImageGroup {
  key: string;
  images: AssetImage[];
}

/** Sample images have no batch, so those of one asset uploaded on the same day by the same person count as one upload. */
function uploadKey(img: AssetImage): string {
  return img.batch ?? `${img.assetTag}|${img.uploadedAt.slice(0, 10)}|${img.uploadedBy}`;
}

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
  /** Assets that receive the images (each gets its own copy). */
  assets: string[];
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

/** One asset's images, kept in memory so its Images tab and the all-assets page show the same list (DEMO MODE). */
interface ImageStore {
  images: AssetImage[];
  headerImageId: number;
}

const IMG_STORE = new Map<string, ImageStore>();
/** Image ids are unique across assets so images of several assets can be listed together. */
let nextImageId = 1;

/** The stored images of an asset, created from its data file the first time. */
function storeFor(tag: string, detail: AssetDetail | null): ImageStore {
  let store = IMG_STORE.get(tag);
  if (!store) {
    const images = detail ? cloneDetail(detail.images) : [];
    let headerImageId = 0;
    for (const img of images) {
      const id = nextImageId++;
      if (img.id === detail?.headerImageId) headerImageId = id;
      img.id = id;
      img.assetTag = tag;
    }
    store = { images, headerImageId };
    // A failed download is not stored, so a later visit tries again
    if (detail) IMG_STORE.set(tag, store);
  }
  return store;
}

/** Frees an uploaded image's file URL once no stored image uses it (copies on several assets share one). */
function releaseSrc(src: string): void {
  if (!src.startsWith('blob:')) return;
  const used = [...IMG_STORE.values()].some((s) => s.images.some((i) => i.src === src));
  if (!used) URL.revokeObjectURL(src);
}

@Component({
  selector: 'app-asset-images',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './asset-images.html',
  // Split to keep each stylesheet under the anyComponentStyle budget
  styleUrls: ['./asset-images.css', './asset-images-preview.css', '../asset-documents/asset-documents-all.css'],
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
  /** Asset Images menu page: images of every asset, with their asset name and ID. */
  allAssets = false;
  readonly assetOptions = ASSET_RECORDS;
  assetFilter = '';
  /** Asset dropdown in the upload dialog. */
  assetPickerOpen = false;
  assetPickerSearch = '';
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
  /** Gallery popup for one upload's images (all-assets list). */
  gallery: AssetImage[] | null = null;
  galleryIndex = 0;

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
    const route = inject(ActivatedRoute);
    this.allAssets = route.snapshot.data['allAssets'] === true;
    if (this.allAssets) {
      // The menu page opens on the table (one row per upload); the asset tab keeps the grid
      this.view = 'list';
      this.loadAll();
      return;
    }
    route.queryParamMap.pipe(takeUntilDestroyed(inject(DestroyRef))).subscribe((params) => this.load(params.get('tag')));
  }

  /** Lists the images of every asset in the register. */
  private loadAll(): void {
    this.loading = true;
    Promise.all(ASSET_RECORDS.map((a) => loadAssetDetail(a.tag).then((d) => storeFor(a.tag, d)))).then((stores) => {
      this.loading = false;
      this.images = stores.flatMap((s) => s.images);
      this.previewId = this.images[0]?.id ?? null;
    });
  }

  /** Writes uploads and deletes back to each asset's stored images. */
  private persist(): void {
    for (const tag of this.allAssets ? ASSET_RECORDS.map((a) => a.tag) : [this.asset.tag]) {
      const store = IMG_STORE.get(tag);
      if (store) store.images = this.images.filter((i) => i.assetTag === tag);
    }
  }

  assetName(img: AssetImage): string {
    return findAsset(img.assetTag)?.name ?? '-';
  }

  /** True for the image used as its asset's photo. */
  isAssetPhoto(img: AssetImage): boolean {
    return (img.assetTag ? IMG_STORE.get(img.assetTag)?.headerImageId : this.headerImageId) === img.id;
  }

  /** Shows the asset named in the URL, with the images from its data file. */
  private load(tag: string | null): void {
    tag ??= ASSET_RECORDS[0].tag; // opened from the menu without an asset: show the first one
    const record = findAsset(tag);
    this.requestedTag = tag ?? '';
    this.notFound = !record;
    if (!record) return;
    this.asset = assetHeader(record);
    this.images = [];
    this.headerImageId = 0;
    this.previewOpen = true;
    this.previewId = null;
    this.loading = true;
    loadAssetDetail(record.tag).then((detail) => {
      if (this.requestedTag !== record.tag) return; // the user moved on to another asset
      this.loading = false;
      const store = storeFor(record.tag, detail);
      this.images = store.images;
      this.headerImageId = store.headerImageId;
      this.previewId = this.images[0]?.id ?? null;
    });
    this.lightboxOpen = false;
    this.checked.clear();
    this.clearFilters();
    this.closeMenus();
  }

  ngOnDestroy(): void {
    // Uploaded images stay in the shared store, so their file URLs are kept
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
        (!this.assetFilter || i.assetTag === this.assetFilter) &&
        (!this.typeFilter || i.type === this.typeFilter) &&
        (!this.tagFilter || i.tags.includes(this.tagFilter)) &&
        (!this.uploaderFilter || i.uploadedBy === this.uploaderFilter) &&
        this.matchesDate(i) &&
        (!q ||
          [i.title, i.description, i.type, i.fileName, i.remarks, i.assetTag ?? '', this.allAssets ? this.assetName(i) : '', ...i.tags]
            .some((v) => v.toLowerCase().includes(q))),
    );
  }

  /** The all-assets list shows one row per upload instead of one per image. */
  get listGrouped(): boolean {
    return this.allAssets && this.view === 'list';
  }

  /** Filtered images grouped by upload, in list order. */
  get groups(): ImageGroup[] {
    const map = new Map<string, ImageGroup>();
    for (const img of this.filtered) {
      const key = uploadKey(img);
      const group = map.get(key);
      if (group) group.images.push(img);
      else map.set(key, { key, images: [img] });
    }
    return [...map.values()];
  }

  get pageGroups(): ImageGroup[] {
    const start = (this.safePage - 1) * this.pageSize;
    return this.groups.slice(start, start + this.pageSize);
  }

  get pageCount(): number {
    const total = this.listGrouped ? this.groups.length : this.filtered.length;
    return Math.max(1, Math.ceil(total / this.pageSize));
  }

  get pageNumbers(): number[] {
    return Array.from({ length: this.pageCount }, (_, i) => i + 1);
  }

  get safePage(): number {
    return Math.min(this.page, this.pageCount);
  }

  get pageItems(): AssetImage[] {
    if (this.listGrouped) return this.pageGroups.flatMap((g) => g.images);
    const start = (this.safePage - 1) * this.pageSize;
    return this.filtered.slice(start, start + this.pageSize);
  }

  get rangeLabel(): string {
    const total = this.listGrouped ? this.groups.length : this.filtered.length;
    if (!total) return 'No images';
    const start = (this.safePage - 1) * this.pageSize + 1;
    const range = `Showing ${start} - ${Math.min(start + this.pageSize - 1, total)} of ${total}`;
    return this.listGrouped ? `${range} uploads (${this.filtered.length} images)` : `${range} images`;
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
    return !!(this.search || this.assetFilter || this.typeFilter || this.tagFilter || this.uploaderFilter || this.dateFilter);
  }

  clearFilters(): void {
    this.search = this.assetFilter = this.typeFilter = this.tagFilter = this.uploaderFilter = this.dateFilter = '';
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

  groupChecked(g: ImageGroup): boolean {
    return g.images.every((i) => this.checked.has(i.id));
  }

  groupPartlyChecked(g: ImageGroup): boolean {
    return !this.groupChecked(g) && g.images.some((i) => this.checked.has(i.id));
  }

  toggleGroup(g: ImageGroup, on: boolean): void {
    g.images.forEach((i) => (on ? this.checked.add(i.id) : this.checked.delete(i.id)));
  }

  /** Distinct values across a group, e.g. its image types or tags. */
  distinct(g: ImageGroup, pick: (i: AssetImage) => string[]): string[] {
    return [...new Set(g.images.flatMap(pick))];
  }

  readonly typeOf = (i: AssetImage): string[] => [i.type];
  readonly tagsOf = (i: AssetImage): string[] => i.tags;

  groupIds(g: ImageGroup): number[] {
    return g.images.map((i) => i.id);
  }

  isGroupSelected(g: ImageGroup): boolean {
    return this.previewOpen && g.images.some((i) => i.id === this.previewId);
  }

  downloadGroup(g: ImageGroup): void {
    this.closeMenus();
    g.images.forEach((i) => this.download(i));
  }

  groupSize(g: ImageGroup): number {
    return g.images.reduce((sum, i) => sum + i.size, 0);
  }

  // ===== Gallery popup =====

  openGallery(g: ImageGroup, index = 0): void {
    this.closeMenus();
    this.gallery = g.images;
    this.galleryIndex = index;
  }

  get galleryImage(): AssetImage | undefined {
    return this.gallery?.[this.galleryIndex];
  }

  galleryStep(direction: 1 | -1): void {
    const n = this.gallery?.length ?? 0;
    if (n) this.galleryIndex = (this.galleryIndex + direction + n) % n;
  }

  /** Closes the popup and shows the image in the preview panel. */
  galleryDetails(img: AssetImage): void {
    this.gallery = null;
    this.openPreview(img);
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
    this.actionsOpen = this.bulkOpen = this.assetPickerOpen = false;
    this.menuId = null;
  }

  @HostListener('document:keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      this.closeMenus();
      if (this.gallery) {
        this.gallery = null;
        return;
      }
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
    if (this.gallery && !typing) {
      if (event.key === 'ArrowLeft') this.galleryStep(-1);
      if (event.key === 'ArrowRight') this.galleryStep(1);
      return;
    }
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
    const store = image.assetTag ? IMG_STORE.get(image.assetTag) : undefined;
    if (store) store.headerImageId = image.id;
    if (!this.allAssets) this.headerImageId = image.id;
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
    const head = ['#', ...(this.allAssets ? ['Asset Name', 'Asset ID'] : []), 'Image Name', 'Image Type', 'Tags', 'Uploaded By', 'Upload Date', 'Location', 'File Name', 'File Size', 'Resolution', 'Description', 'Remarks'];
    const rows = this.filtered.map((i, n) => [
      n + 1, ...(this.allAssets ? [this.assetName(i), i.assetTag ?? ''] : []), i.title, i.type, i.tags.join('; '), i.uploadedBy, i.uploadedAt.replace('T', ' '), i.location, i.fileName,
      this.formatSize(i.size), i.width ? `${i.width} x ${i.height}` : '-', i.description, i.remarks,
    ]);
    const csv = [head, ...rows].map((row) => row.map((v) => `"${String(v).replace(/"/g, '""')}"`).join(',')).join('\r\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }));
    saveUrl(url, this.allAssets ? 'asset-images.csv' : `${this.asset.tag}-images.csv`);
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
    this.upload = {
      items: [], assets: this.allAssets ? [] : [this.asset.tag], type: '', tags: '',
      // On the all-assets page an empty location means "each asset's own location"
      location: this.allAssets ? '' : this.asset.location, description: '', remarks: '',
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
    if (!u.assets.length) {
      this.uploadError = 'Select at least one asset.';
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
    // Every image goes to every selected asset; the copies share the uploaded file
    const batchStamp = Date.now();
    const added: AssetImage[] = u.assets.flatMap((assetTag) => u.items.map((p) => ({
      id: nextImageId++,
      assetTag,
      batch: `upload-${batchStamp}-${assetTag}`,
      title: p.title.trim(),
      description: u.description.trim(),
      type: u.type,
      tags: splitTags(u.tags),
      uploadedBy: CURRENT_USER,
      uploadedAt: at,
      location: u.location.trim() || assetLocation(findAsset(assetTag)!),
      fileName: p.file.name,
      size: p.file.size,
      width: p.width,
      height: p.height,
      remarks: u.remarks.trim(),
      // The dialog's preview URL becomes the image's own URL
      src: p.previewUrl,
      file: p.file,
      history: [{ action: 'Image uploaded', by: CURRENT_USER, at }],
    })));
    this.images = [...this.images, ...added];
    this.persist();
    this.upload = null;
    this.clearFilters();
    this.page = this.pageCount;
    this.openPreview(added[0]);
    const perAsset = u.items.length;
    this.showToast(
      u.assets.length > 1
        ? `${perAsset} image${perAsset > 1 ? 's' : ''} uploaded to ${u.assets.length} assets.`
        : added.length === 1 ? `${added[0].title} uploaded.` : `${added.length} images uploaded.`,
    );
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
    const oldSrc = image.src;
    image.file = file;
    image.src = URL.createObjectURL(file);
    releaseSrc(oldSrc);
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
    const removed = this.images.filter((i) => ids.has(i.id));
    if (this.gallery) {
      this.gallery = this.gallery.filter((i) => !ids.has(i.id));
      if (!this.gallery.length) this.gallery = null;
      else this.galleryIndex = Math.min(this.galleryIndex, this.gallery.length - 1);
    }
    this.images = this.images.filter((i) => !ids.has(i.id));
    this.persist();
    removed.forEach((i) => releaseSrc(i.src));
    // An asset whose photo was deleted falls back to its first image
    for (const store of IMG_STORE.values()) if (ids.has(store.headerImageId)) store.headerImageId = store.images[0]?.id ?? 0;
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
