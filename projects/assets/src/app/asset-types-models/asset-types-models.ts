import { Component, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import {
  LucideAngularModule,
  AirVent,
  Archive,
  Armchair,
  BatteryCharging,
  BellRing,
  Boxes,
  Car,
  Cctv,
  Check,
  ChevronLeft,
  ChevronRight,
  CircuitBoard,
  Cog,
  Container,
  Drill,
  EllipsisVertical,
  ExternalLink,
  Eye,
  Fan,
  FileStack,
  Flame,
  Forklift,
  Hexagon,
  ImageUp,
  Inbox,
  KeyRound,
  Laptop,
  Layers,
  Monitor,
  Network,
  Package,
  Plus,
  Power,
  PowerOff,
  Printer,
  Ruler,
  Search,
  Server,
  Snowflake,
  SquarePen,
  ToggleRight,
  Trash2,
  Truck,
  Wind,
  X,
  Zap,
} from 'lucide-angular';

type Status = 'Active' | 'Inactive';

interface AssetModel {
  id: number;
  name: string;
  code: string;
  manufacturer: string;
  assetCount: number;
  status: Status;
}

interface AssetType {
  id: number;
  name: string;
  code: string;
  category: string;
  description: string;
  status: Status;
  icon: keyof AssetTypesModels['typeIcons'];
  /** Uploaded image as a data URL; the icon is shown when absent. */
  image?: string;
  models: AssetModel[];
}

interface TypeForm {
  name: string;
  code: string;
  category: string;
  description: string;
  status: Status;
}

interface ModelForm {
  name: string;
  code: string;
  manufacturer: string;
  status: Status;
}

/**
 * Client-side pagination state for one table. `page` is the requested page; reads clamp it to the
 * row count so deleting or filtering rows never leaves the table on an empty page.
 */
class Pager {
  page = 1;
  size = 10;

  totalPages(count: number): number {
    return Math.max(1, Math.ceil(count / this.size));
  }

  current(count: number): number {
    return Math.min(this.page, this.totalPages(count));
  }

  pages(count: number): number[] {
    return Array.from({ length: this.totalPages(count) }, (_, i) => i + 1);
  }

  slice<T>(rows: T[]): T[] {
    const start = (this.current(rows.length) - 1) * this.size;
    return rows.slice(start, start + this.size);
  }

  start(count: number): number {
    return count ? (this.current(count) - 1) * this.size + 1 : 0;
  }

  end(count: number): number {
    return Math.min(this.current(count) * this.size, count);
  }

  go(p: number, count: number): void {
    if (p >= 1 && p <= this.totalPages(count)) this.page = p;
  }
}

let nextId = 1;

interface TypeSeed {
  name: string;
  code: string;
  category: string;
  description: string;
  icon: AssetType['icon'];
  manufacturers: string[];
  modelCount: number;
  status?: Status;
}

const TYPE_SEEDS: TypeSeed[] = [
  { name: 'Generator', code: 'GEN', category: 'Power Generation', description: 'Diesel / Gas Generators', icon: 'generator', manufacturers: ['Caterpillar', 'Cummins', 'Perkins', 'Kohler'], modelCount: 8 },
  { name: 'Transformer', code: 'TRF', category: 'Power Generation', description: 'Power transformers and distribution transformers used in electrical power systems.', icon: 'transformer', manufacturers: [], modelCount: 0 },
  { name: 'UPS', code: 'UPS', category: 'Electrical Equipment', description: 'Uninterruptible Power Supply', icon: 'ups', manufacturers: ['APC', 'Eaton', 'Vertiv'], modelCount: 5 },
  { name: 'Switchgear', code: 'SWG', category: 'Electrical Equipment', description: 'LV / MV Switchgear', icon: 'switchgear', manufacturers: ['ABB', 'Schneider', 'Siemens'], modelCount: 7 },
  { name: 'Control Panel', code: 'CP', category: 'Electrical Equipment', description: 'Electrical Control Panels', icon: 'panel', manufacturers: ['Rittal', 'Schneider', 'ABB'], modelCount: 5 },
  { name: 'Chiller', code: 'CHL', category: 'HVAC', description: 'Chillers', icon: 'chiller', manufacturers: ['Carrier', 'Trane', 'York'], modelCount: 6 },
  { name: 'AHU', code: 'AHU', category: 'HVAC', description: 'Air Handling Units', icon: 'ahu', manufacturers: ['Trane', 'Daikin'], modelCount: 4 },
  { name: 'Split AC', code: 'SAC', category: 'HVAC', description: 'Split Air Conditioners', icon: 'ac', manufacturers: ['Daikin', 'LG', 'Mitsubishi'], modelCount: 6 },
  { name: 'CCTV Camera', code: 'CCTV', category: 'Security & Safety', description: 'Security Cameras', icon: 'cctv', manufacturers: ['Hikvision', 'Axis'], modelCount: 4 },
  { name: 'Laptop', code: 'LAP', category: 'IT Equipment', description: 'Laptops', icon: 'laptop', manufacturers: ['Dell', 'HP', 'Lenovo', 'Apple'], modelCount: 12 },
  { name: 'Desktop', code: 'DSK', category: 'IT Equipment', description: 'Desktop Computers and Workstations', icon: 'desktop', manufacturers: ['Dell', 'HP', 'Lenovo'], modelCount: 6 },
  { name: 'Server', code: 'SRV', category: 'IT Equipment', description: 'Rack and Tower Servers', icon: 'server', manufacturers: ['Dell', 'HPE'], modelCount: 4 },
  { name: 'Network Switch', code: 'NSW', category: 'IT Equipment', description: 'Managed / Unmanaged Switches', icon: 'network', manufacturers: ['Cisco', 'Aruba'], modelCount: 5 },
  { name: 'Printer', code: 'PRN', category: 'IT Equipment', description: 'Printers, Scanners and MFPs', icon: 'printer', manufacturers: ['HP', 'Canon', 'Ricoh'], modelCount: 3 },
  { name: 'Fire Alarm Panel', code: 'FAP', category: 'Security & Safety', description: 'Fire Alarm Control Panels', icon: 'alarm', manufacturers: ['Honeywell', 'Siemens'], modelCount: 3 },
  { name: 'Access Controller', code: 'ACC', category: 'Security & Safety', description: 'Door Controllers and Card Readers', icon: 'access', manufacturers: ['HID', 'Suprema'], modelCount: 4 },
  { name: 'Fire Pump', code: 'FP', category: 'Security & Safety', description: 'Fire Fighting Pumps', icon: 'fire', manufacturers: ['Grundfos', 'Xylem'], modelCount: 2 },
  { name: 'Forklift', code: 'FLT', category: 'Material Handling', description: 'Electric / Diesel Forklifts', icon: 'forklift', manufacturers: ['Toyota', 'Linde', 'Jungheinrich'], modelCount: 5 },
  { name: 'Conveyor', code: 'CNV', category: 'Material Handling', description: 'Belt and Roller Conveyors', icon: 'conveyor', manufacturers: ['Interroll', 'Dorner'], modelCount: 3 },
  { name: 'Crane', code: 'CRN', category: 'Material Handling', description: 'Overhead and Gantry Cranes', icon: 'crane', manufacturers: ['Konecranes', 'Demag'], modelCount: 2, status: 'Inactive' },
  { name: 'Car', code: 'CAR', category: 'Vehicles & Fleet', description: 'Passenger Cars and SUVs', icon: 'car', manufacturers: ['Toyota', 'Nissan', 'Hyundai'], modelCount: 6 },
  { name: 'Truck', code: 'TRK', category: 'Vehicles & Fleet', description: 'Light and Heavy Trucks', icon: 'truck', manufacturers: ['Isuzu', 'Volvo'], modelCount: 4 },
  { name: 'Power Drill', code: 'PDR', category: 'Tools & Instruments', description: 'Cordless and Corded Drills', icon: 'drill', manufacturers: ['Bosch', 'Makita', 'DeWalt'], modelCount: 5 },
  { name: 'Multimeter', code: 'MLT', category: 'Tools & Instruments', description: 'Digital Multimeters', icon: 'meter', manufacturers: ['Fluke', 'Keysight'], modelCount: 3 },
  { name: 'Filing Cabinet', code: 'CAB', category: 'Furniture & Fixtures', description: 'Filing and Storage Cabinets', icon: 'cabinet', manufacturers: ['Steelcase', 'Herman Miller'], modelCount: 2 },
];

/** Models shown for Transformer in the design mock. */
const TRANSFORMER_MODELS: Omit<AssetModel, 'id'>[] = [
  { name: 'TX-500', code: 'TX500', manufacturer: 'ABB', assetCount: 12, status: 'Active' },
  { name: 'TX-1000', code: 'TX1000', manufacturer: 'Siemens', assetCount: 8, status: 'Active' },
  { name: 'TX-2000', code: 'TX2000', manufacturer: 'Schneider', assetCount: 6, status: 'Active' },
  { name: 'DTR-315', code: 'DTR315', manufacturer: 'Eaton', assetCount: 4, status: 'Active' },
  { name: 'DTR-630', code: 'DTR630', manufacturer: 'GE', assetCount: 3, status: 'Active' },
  { name: 'TX-3000', code: 'TX3000', manufacturer: 'Hitachi', assetCount: 2, status: 'Inactive' },
];

function buildMockTypes(): AssetType[] {
  return TYPE_SEEDS.map((s, ti) => {
    const models: AssetModel[] =
      s.code === 'TRF'
        ? TRANSFORMER_MODELS.map((m) => ({ ...m, id: nextId++ }))
        : Array.from({ length: s.modelCount }, (_, k) => {
            const series = 100 * (k + 1) + ((ti * 17) % 50);
            return {
              id: nextId++,
              name: `${s.code}-${series}`,
              code: `${s.code}${series}`,
              manufacturer: s.manufacturers[k % s.manufacturers.length],
              assetCount: (ti * 7 + k * 5) % 15,
              status: k === s.modelCount - 1 && k > 2 ? 'Inactive' : 'Active',
            };
          });
    return {
      id: nextId++,
      name: s.name,
      code: s.code,
      category: s.category,
      description: s.description,
      status: s.status ?? 'Active',
      icon: s.icon,
      models,
    };
  });
}

const EMPTY_TYPE: TypeForm = { name: '', code: '', category: '', description: '', status: 'Active' };
const EMPTY_MODEL: ModelForm = { name: '', code: '', manufacturer: '', status: 'Active' };
const MAX_IMAGE_BYTES = 2 * 1024 * 1024;

@Component({
  selector: 'app-asset-types-models',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './asset-types-models.html',
  styleUrl: './asset-types-models.css',
})
export class AssetTypesModels {
  readonly icons = {
    Check, ChevronLeft, ChevronRight, EllipsisVertical, ExternalLink, Eye, FileStack, Hexagon, ImageUp, Inbox,
    Layers, Plus, Power, PowerOff, Search, SquarePen, Trash2, X,
  };

  readonly typeIcons = {
    generator: Zap,
    transformer: Cog,
    ups: BatteryCharging,
    switchgear: ToggleRight,
    panel: CircuitBoard,
    chiller: Snowflake,
    ahu: Fan,
    ac: AirVent,
    vrf: Wind,
    cctv: Cctv,
    laptop: Laptop,
    desktop: Monitor,
    server: Server,
    network: Network,
    printer: Printer,
    alarm: BellRing,
    access: KeyRound,
    fire: Flame,
    forklift: Forklift,
    conveyor: Package,
    crane: Container,
    pallet: Boxes,
    car: Car,
    truck: Truck,
    drill: Drill,
    meter: Ruler,
    cabinet: Archive,
    furniture: Armchair,
  };

  readonly pageSizeOptions = [10, 25, 50];

  types: AssetType[] = buildMockTypes();
  selectedTypeId: number;

  // Types table
  typeSearch = '';
  typeCategory = '';
  typePager = new Pager();
  checkedTypes = new Set<number>();

  // Type details (read-only until "Edit")
  editing = false;
  detailForm: TypeForm = { ...EMPTY_TYPE };
  detailSubmitted = false;

  // Models table
  modelSearch = '';
  modelStatus: '' | Status = '';
  modelPager = new Pager();
  checkedModels = new Set<number>();

  // Add-type modal
  typeModalOpen = false;
  typeModal: TypeForm = { ...EMPTY_TYPE };
  typeModalSubmitted = false;
  private typeCodeTouched = false;

  // Add/edit-model modal
  modelModalOpen = false;
  editingModel: AssetModel | null = null;
  modelModal: ModelForm = { ...EMPTY_MODEL };
  modelModalSubmitted = false;

  openMenu: string | null = null;
  deleteTarget: { kind: 'type'; item: AssetType } | { kind: 'model'; item: AssetModel } | null = null;

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  constructor(private readonly router: Router) {
    this.selectedTypeId = this.types.find((t) => t.code === 'TRF')?.id ?? this.types[0].id;
    this.loadDetailForm();
  }

  // ===== Lookups =====

  get categoryOptions(): string[] {
    return [...new Set(this.types.map((t) => t.category))].sort((a, b) => a.localeCompare(b));
  }

  get selectedType(): AssetType | null {
    return this.types.find((t) => t.id === this.selectedTypeId) ?? null;
  }

  get manufacturerOptions(): string[] {
    return [...new Set(this.types.flatMap((t) => t.models.map((m) => m.manufacturer)))].sort((a, b) => a.localeCompare(b));
  }

  typeAssetCount(t: AssetType): number {
    return t.models.reduce((sum, m) => sum + m.assetCount, 0);
  }

  // ===== Types table =====

  get filteredTypes(): AssetType[] {
    const q = this.typeSearch.trim().toLowerCase();
    return this.types.filter(
      (t) =>
        (!q || [t.name, t.code, t.category, t.description].some((v) => v.toLowerCase().includes(q))) &&
        (!this.typeCategory || t.category === this.typeCategory),
    );
  }

  get pageTypes(): AssetType[] {
    return this.typePager.slice(this.filteredTypes);
  }

  onTypeFilterChange(): void {
    this.typePager.page = 1;
  }

  selectType(t: AssetType): void {
    if (t.id === this.selectedTypeId) return;
    if (this.editing && !confirm('Discard unsaved changes to this asset type?')) return;
    this.selectedTypeId = t.id;
    this.editing = false;
    this.modelSearch = '';
    this.modelStatus = '';
    this.modelPager.page = 1;
    this.checkedModels.clear();
    this.loadDetailForm();
  }

  get allTypesChecked(): boolean {
    return this.pageTypes.length > 0 && this.pageTypes.every((t) => this.checkedTypes.has(t.id));
  }

  get someTypesChecked(): boolean {
    return !this.allTypesChecked && this.pageTypes.some((t) => this.checkedTypes.has(t.id));
  }

  toggleAllTypes(): void {
    const on = !this.allTypesChecked;
    this.pageTypes.forEach((t) => (on ? this.checkedTypes.add(t.id) : this.checkedTypes.delete(t.id)));
  }

  toggleChecked(set: Set<number>, id: number): void {
    if (set.has(id)) set.delete(id);
    else set.add(id);
  }

  setTypeStatusForChecked(status: Status): void {
    const rows = this.types.filter((t) => this.checkedTypes.has(t.id));
    rows.forEach((t) => (t.status = status));
    this.checkedTypes.clear();
    this.showToast(`${rows.length} asset type${rows.length === 1 ? '' : 's'} marked ${status.toLowerCase()}.`);
    this.loadDetailForm();
  }

  // ===== Type details =====

  private loadDetailForm(): void {
    const t = this.selectedType;
    this.detailForm = t
      ? { name: t.name, code: t.code, category: t.category, description: t.description, status: t.status }
      : { ...EMPTY_TYPE };
    this.detailSubmitted = false;
  }

  startEdit(t?: AssetType): void {
    this.openMenu = null;
    if (t) this.selectType(t);
    if (this.selectedTypeId === t?.id || !t) this.editing = true;
  }

  cancelEdit(): void {
    this.editing = false;
    this.loadDetailForm();
  }

  saveDetails(): void {
    const t = this.selectedType;
    if (!t) return;
    this.detailSubmitted = true;
    const error = this.validateType(this.detailForm, t.id);
    if (error) {
      this.showToast(error);
      return;
    }
    Object.assign(t, this.cleanType(this.detailForm));
    this.editing = false;
    this.loadDetailForm();
    this.showToast(`${t.name} updated.`);
  }

  private cleanType(f: TypeForm): TypeForm {
    return { ...f, name: f.name.trim(), code: f.code.trim().toUpperCase(), category: f.category.trim(), description: f.description.trim() };
  }

  isTypeCodeTaken(code: string, exceptId: number | null): boolean {
    const c = code.trim().toUpperCase();
    return !!c && this.types.some((t) => t.id !== exceptId && t.code.toUpperCase() === c);
  }

  private validateType(f: TypeForm, exceptId: number | null): string | null {
    if (!f.name.trim()) return 'Type name is required.';
    if (!f.code.trim()) return 'Type code is required.';
    if (!f.category.trim()) return 'Category is required.';
    if (this.isTypeCodeTaken(f.code, exceptId)) return `Code "${f.code.trim().toUpperCase()}" is already used by another type.`;
    return null;
  }

  onImageSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    const target = this.selectedType;
    if (!file || !target) return;
    if (!['image/jpeg', 'image/png'].includes(file.type)) {
      this.showToast('Please choose a JPG or PNG image.');
      return;
    }
    if (file.size > MAX_IMAGE_BYTES) {
      this.showToast('Image must be 2 MB or smaller.');
      return;
    }
    const reader = new FileReader();
    reader.onload = () => {
      target.image = reader.result as string;
      this.showToast('Type image updated.');
    };
    reader.readAsDataURL(file);
  }

  removeImage(): void {
    if (this.selectedType) this.selectedType.image = undefined;
  }

  toggleTypeStatus(t: AssetType): void {
    this.openMenu = null;
    t.status = t.status === 'Active' ? 'Inactive' : 'Active';
    if (t.id === this.selectedTypeId) this.loadDetailForm();
    this.showToast(`${t.name} is now ${t.status.toLowerCase()}.`);
  }

  // ===== Add type modal =====

  openTypeModal(): void {
    this.typeModal = { ...EMPTY_TYPE, category: this.typeCategory };
    this.typeModalSubmitted = false;
    this.typeCodeTouched = false;
    this.typeModalOpen = true;
  }

  /** Suggests a code from the name until the user types their own. */
  onTypeNameChange(): void {
    if (this.typeCodeTouched) return;
    const words = this.typeModal.name.trim().split(/\s+/).filter(Boolean);
    this.typeModal.code = (words.length > 1 ? words.map((w) => w[0]).join('') : (words[0] ?? '').slice(0, 3))
      .toUpperCase()
      .replace(/[^A-Z0-9]/g, '');
  }

  markTypeCodeTouched(): void {
    this.typeCodeTouched = true;
  }

  saveTypeModal(): void {
    this.typeModalSubmitted = true;
    if (this.validateType(this.typeModal, null)) return;
    const created: AssetType = { id: nextId++, ...this.cleanType(this.typeModal), icon: 'pallet', models: [] };
    this.types.push(created);
    this.typeModalOpen = false;
    this.typeSearch = '';
    this.typeCategory = '';
    this.typePager.page = this.typePager.totalPages(this.types.length);
    this.selectType(created);
    this.showToast(`${created.name} added.`);
  }

  // ===== Models table =====

  get filteredModels(): AssetModel[] {
    const t = this.selectedType;
    if (!t) return [];
    const q = this.modelSearch.trim().toLowerCase();
    return t.models.filter(
      (m) =>
        (!q || [m.name, m.code, m.manufacturer].some((v) => v.toLowerCase().includes(q))) &&
        (!this.modelStatus || m.status === this.modelStatus),
    );
  }

  get pageModels(): AssetModel[] {
    return this.modelPager.slice(this.filteredModels);
  }

  onModelFilterChange(): void {
    this.modelPager.page = 1;
  }

  get allModelsChecked(): boolean {
    return this.pageModels.length > 0 && this.pageModels.every((m) => this.checkedModels.has(m.id));
  }

  get someModelsChecked(): boolean {
    return !this.allModelsChecked && this.pageModels.some((m) => this.checkedModels.has(m.id));
  }

  toggleAllModels(): void {
    const on = !this.allModelsChecked;
    this.pageModels.forEach((m) => (on ? this.checkedModels.add(m.id) : this.checkedModels.delete(m.id)));
  }

  setModelStatusForChecked(status: Status): void {
    const rows = (this.selectedType?.models ?? []).filter((m) => this.checkedModels.has(m.id));
    rows.forEach((m) => (m.status = status));
    this.checkedModels.clear();
    this.showToast(`${rows.length} model${rows.length === 1 ? '' : 's'} marked ${status.toLowerCase()}.`);
  }

  toggleModelStatus(m: AssetModel): void {
    this.openMenu = null;
    m.status = m.status === 'Active' ? 'Inactive' : 'Active';
    this.showToast(`${m.name} is now ${m.status.toLowerCase()}.`);
  }

  viewModelAssets(m: AssetModel): void {
    this.openMenu = null;
    this.router.navigate(['/assets/list'], { queryParams: { type: this.selectedType?.code, model: m.code } });
  }

  // ===== Add / edit model modal =====

  openModelModal(m?: AssetModel): void {
    this.openMenu = null;
    this.editingModel = m ?? null;
    this.modelModal = m ? { name: m.name, code: m.code, manufacturer: m.manufacturer, status: m.status } : { ...EMPTY_MODEL };
    this.modelModalSubmitted = false;
    this.modelModalOpen = true;
  }

  isModelCodeTaken(code: string): boolean {
    const c = code.trim().toUpperCase();
    return !!c && (this.selectedType?.models ?? []).some((m) => m !== this.editingModel && m.code.toUpperCase() === c);
  }

  saveModelModal(): void {
    this.modelModalSubmitted = true;
    const f = this.modelModal;
    const t = this.selectedType;
    if (!t || !f.name.trim() || !f.code.trim() || !f.manufacturer.trim() || this.isModelCodeTaken(f.code)) return;

    const values = { name: f.name.trim(), code: f.code.trim().toUpperCase(), manufacturer: f.manufacturer.trim(), status: f.status };
    if (this.editingModel) {
      Object.assign(this.editingModel, values);
      this.showToast(`${values.name} updated.`);
    } else {
      t.models.push({ id: nextId++, assetCount: 0, ...values });
      this.modelPager.page = this.modelPager.totalPages(t.models.length);
      this.showToast(`${values.name} added to ${t.name}.`);
    }
    this.modelModalOpen = false;
  }

  // ===== Delete =====

  askDeleteType(t: AssetType): void {
    this.openMenu = null;
    const assets = this.typeAssetCount(t);
    if (assets > 0) {
      this.showToast(`${t.name} has ${assets} asset${assets === 1 ? '' : 's'} registered. Reassign them before deleting.`);
      return;
    }
    this.deleteTarget = { kind: 'type', item: t };
  }

  askDeleteModel(m: AssetModel): void {
    this.openMenu = null;
    if (m.assetCount > 0) {
      this.showToast(`${m.name} has ${m.assetCount} asset${m.assetCount === 1 ? '' : 's'} registered. Reassign them before deleting.`);
      return;
    }
    this.deleteTarget = { kind: 'model', item: m };
  }

  confirmDelete(): void {
    const target = this.deleteTarget;
    if (!target) return;
    if (target.kind === 'type') {
      this.types = this.types.filter((t) => t !== target.item);
      this.checkedTypes.delete(target.item.id);
      if (target.item.id === this.selectedTypeId) {
        this.selectedTypeId = this.filteredTypes[0]?.id ?? this.types[0]?.id ?? -1;
        this.editing = false;
        this.loadDetailForm();
      }
    } else {
      const models = this.selectedType?.models ?? [];
      models.splice(models.indexOf(target.item), 1);
      this.checkedModels.delete(target.item.id);
    }
    this.showToast(`${target.item.name} deleted.`);
    this.deleteTarget = null;
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
    this.typeModalOpen = false;
    this.modelModalOpen = false;
    this.deleteTarget = null;
  }

  trackById(_: number, row: { id: number }): number {
    return row.id;
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 2800);
  }
}
