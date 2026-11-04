import { Component, EventEmitter, HostListener, Output, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  Building2,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  Download,
  Ellipsis,
  Eye,
  Info,
  LayoutGrid,
  Layers,
  List,
  LucideAngularModule,
  Maximize2,
  Package,
  Pencil,
  Plus,
  Search,
  Wifi
} from 'lucide-angular';
import { MapCanvas } from '../map-canvas/map-canvas';
import { FloorPlan, MapStatus, STATUS_TONE, fmtDate, fmtTime } from '../maps.data';
import { MapsStore } from '../maps.store';
import { SiteTree } from '../site-tree/site-tree';
import { UploadDialog } from '../upload-dialog/upload-dialog';

type SortKey = 'updated' | 'name' | 'version';

/** Floor Plans: list of plans for the selected floor with filters, and details of the selected plan. */
@Component({
  standalone: true,
  selector: 'app-maps-floor-plans',
  imports: [FormsModule, LucideAngularModule, SiteTree, MapCanvas, UploadDialog],
  templateUrl: './floor-plans.html',
  styleUrls: ['../../shared/admin-workspace.css', '../maps.css']
})
export class FloorPlansTab {
  readonly store = inject(MapsStore);
  readonly icons = {
    Building2, ChevronDown, ChevronLeft, ChevronRight, Download, Ellipsis, Eye, Info, LayoutGrid, Layers, List, Maximize2, Package,
    Pencil, Plus, Search, Wifi
  };
  readonly pageSizes = [5, 10, 20];

  @Output() goTo = new EventEmitter<string>();

  search = '';
  statusFilter: '' | MapStatus = '';
  typeFilter: '' | FloorPlan['type'] = '';
  sort: SortKey = 'updated';
  view: 'list' | 'grid' = 'list';
  page = 0;
  pageSize = 10;
  checked = new Set<string>();
  menuFor: string | null = null;
  headMenuOpen = false;
  uploadOpen = false;
  editing: FloorPlan | null = null;

  date = fmtDate;
  time = fmtTime;

  tone(s: MapStatus) {
    return STATUS_TONE[s];
  }

  get devicesPlotted(): number {
    return this.store.plotted.filter((i) => i.kind === 'device').length;
  }

  get assetsPlotted(): number {
    return this.store.plotted.filter((i) => i.kind === 'asset').length;
  }

  get all(): FloorPlan[] {
    return this.store.plansOf(this.store.selectedFloorId);
  }

  get rows(): FloorPlan[] {
    const q = this.search.trim().toLowerCase();
    const list = this.all.filter(
      (p) =>
        (!q || `${p.name} ${p.subtitle} ${p.fileName}`.toLowerCase().includes(q)) &&
        (!this.statusFilter || p.status === this.statusFilter) &&
        (!this.typeFilter || p.type === this.typeFilter)
    );
    const by: Record<SortKey, (a: FloorPlan, b: FloorPlan) => number> = {
      updated: (a, b) => b.updated.localeCompare(a.updated),
      name: (a, b) => a.name.localeCompare(b.name),
      version: (a, b) => b.version.localeCompare(a.version, undefined, { numeric: true })
    };
    return [...list].sort(by[this.sort]);
  }

  get paged(): FloorPlan[] {
    return this.rows.slice(this.page * this.pageSize, (this.page + 1) * this.pageSize);
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.rows.length / this.pageSize));
  }

  get pages(): number[] {
    return Array.from({ length: this.pageCount }, (_, i) => i);
  }

  get selected(): FloorPlan | undefined {
    return this.all.find((p) => p.id === this.store.selectedPlanId) ?? this.all[0];
  }

  get allChecked(): boolean {
    return this.paged.length > 0 && this.paged.every((p) => this.checked.has(p.id));
  }

  toggleAll(): void {
    const on = !this.allChecked;
    this.paged.forEach((p) => (on ? this.checked.add(p.id) : this.checked.delete(p.id)));
  }

  toggle(id: string): void {
    if (this.checked.has(id)) this.checked.delete(id);
    else this.checked.add(id);
  }

  onFilter(): void {
    this.page = 0;
  }

  onFloor(): void {
    this.page = 0;
    this.checked.clear();
    this.editing = null;
  }

  select(p: FloorPlan): void {
    this.store.selectedPlanId = p.id;
    this.editing = null;
  }

  toggleMenu(event: MouseEvent, id: string): void {
    event.stopPropagation();
    this.menuFor = this.menuFor === id ? null : id;
  }

  toggleHeadMenu(event: MouseEvent): void {
    event.stopPropagation();
    this.headMenuOpen = !this.headMenuOpen;
  }

  @HostListener('document:click')
  closeMenus(): void {
    this.menuFor = null;
    this.headMenuOpen = false;
  }

  publish(p: FloorPlan): void {
    p.status = 'Published';
    p.updated = new Date().toISOString();
    this.store.notify(`${p.name} published`);
  }

  archive(p: FloorPlan): void {
    p.status = 'Archived';
    this.store.notify(`${p.name} archived`);
  }

  remove(p: FloorPlan): void {
    this.store.removePlan(p.id);
    this.checked.delete(p.id);
    this.store.notify(`${p.name} deleted`);
  }

  removeChecked(): void {
    const n = this.checked.size;
    [...this.checked].forEach((id) => this.store.removePlan(id));
    this.checked.clear();
    this.store.notify(`${n} floor plan${n === 1 ? '' : 's'} deleted`);
  }

  download(p: FloorPlan): void {
    this.store.notify(`Downloading ${p.fileName}`);
  }

  edit(p: FloorPlan): void {
    this.editing = { ...p };
  }

  saveEdit(): void {
    if (!this.editing || !this.editing.name.trim()) return;
    const target = this.store.floorPlans.find((p) => p.id === this.editing!.id);
    if (target) Object.assign(target, this.editing, { name: this.editing.name.trim(), updated: new Date().toISOString() });
    this.store.notify('Floor plan details saved');
    this.editing = null;
  }

  open(p: FloorPlan, tab: string): void {
    this.store.selectedPlanId = p.id;
    this.goTo.emit(tab);
  }

  uploaded(name: string): void {
    this.uploadOpen = false;
    this.store.notify(`${name} uploaded as a draft`);
  }
}
