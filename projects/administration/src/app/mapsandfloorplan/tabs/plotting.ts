import { Component, HostListener, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  ChevronDown,
  ChevronUp,
  Cpu,
  Ellipsis,
  Image,
  ListFilter,
  LucideAngularModule,
  MapPin,
  Move,
  MousePointer2,
  Package,
  Plus,
  Search,
  Sparkles,
  Trash2
} from 'lucide-angular';
import { MapCanvas } from '../map-canvas/map-canvas';
import { MapViewer } from '../map-viewer/map-viewer';
import { PlotItem, PlotKind, TONE_HEX, Tone, fmtDate, fmtTime } from '../maps.data';
import { MapsStore } from '../maps.store';

/** Device & Asset Plotting: place devices and assets on the plan, reposition them, and inspect a plot. */
@Component({
  standalone: true,
  selector: 'app-maps-plotting',
  imports: [FormsModule, LucideAngularModule, MapViewer, MapCanvas],
  templateUrl: './plotting.html',
  styleUrls: ['../../shared/admin-workspace.css', '../maps.css'],
  styles: `
    .swatches { display: flex; gap: 6px; }
    .swatches button { width: 22px; height: 22px; border: 2px solid #fff; border-radius: 6px; cursor: pointer; box-shadow: 0 0 0 1px var(--adm-border); }
    .swatches button.on { box-shadow: 0 0 0 2px var(--adm-purple); }
    .unplotted { padding: 0; }
    .unplotted .panel__head { padding: 12px 14px 0; }
    .unplotted .list { padding: 0 10px 10px; margin: 0; }
    .rhead__icon--solid { border-style: solid; }
  `
})
export class PlottingTab {
  readonly store = inject(MapsStore);
  readonly icons = {
    ChevronDown, ChevronUp, Cpu, Ellipsis, Image, ListFilter, MapPin, Move, MousePointer2, Package, Plus, Search, Sparkles, Trash2
  };
  readonly tones: Tone[] = ['purple', 'blue', 'green', 'orange', 'red', 'pink'];

  listTab: PlotKind = 'device';
  rightTab: 'details' | 'settings' = 'details';
  search = '';
  activeOnly = false;
  unplottedOpen = true;
  /** Item waiting for a click on the plan (plot or reposition). */
  placingId: string | null = null;
  plotMenu: PlotKind | null = null;
  menuFor: string | null = null;
  show = { devices: true, assets: true, zones: true, labels: true };
  showFilter: 'all' | 'devices' | 'assets' = 'all';

  settings = { snap: true, grid: 1, labelMode: 'ID', autoZone: true };

  date = fmtDate;
  time = fmtTime;

  get list(): PlotItem[] {
    const q = this.search.trim().toLowerCase();
    return this.store.items.filter(
      (i) =>
        i.kind === this.listTab &&
        (!q || `${i.name} ${i.code} ${i.type}`.toLowerCase().includes(q)) &&
        (!this.activeOnly || i.status === 'Active')
    );
  }

  get selected(): PlotItem | undefined {
    return this.store.selectedItem;
  }

  get selectedZoneLabel(): string {
    const z = this.store.zones.find((x) => x.id === this.selected?.zoneId);
    return z ? `${z.name} (${z.code})` : 'Not in a zone';
  }

  hex(t: Tone): string {
    return TONE_HEX[t];
  }

  /** Plan size in metres (60 m × 32 m when the plan has no dimensions). */
  get planSize(): { w: number; h: number } {
    const p = this.store.currentPlan;
    return { w: p?.widthMeters || 60, h: p?.heightMeters || 32 };
  }

  /** Map coordinates in metres on the current plan. */
  metres(i: PlotItem): { x: string; y: string } {
    const { w, h } = this.planSize;
    return { x: (((i.x ?? 0) / 100) * w).toFixed(2), y: (((i.y ?? 0) / 100) * h).toFixed(2) };
  }

  get emptyText(): string {
    if (this.store.itemsLoading) return `Loading ${this.listTab}s…`;
    return this.count(this.listTab) ? `No ${this.listTab}s match.` : `No ${this.listTab}s registered yet.`;
  }

  count(kind: PlotKind): number {
    return this.store.items.filter((i) => i.kind === kind).length;
  }

  select(i: PlotItem): void {
    this.store.selectedItemId = i.id;
    this.listTab = i.kind;
    this.rightTab = 'details';
  }

  togglePlotMenu(event: MouseEvent, kind: PlotKind): void {
    event.stopPropagation();
    this.plotMenu = this.plotMenu === kind ? null : kind;
    this.menuFor = null;
  }

  toggleMenu(event: MouseEvent, id: string): void {
    event.stopPropagation();
    this.menuFor = this.menuFor === id ? null : id;
    this.plotMenu = null;
  }

  @HostListener('document:click')
  closeMenus(): void {
    this.plotMenu = null;
    this.menuFor = null;
  }

  unplottedOf(kind: PlotKind): PlotItem[] {
    return this.store.unplotted.filter((i) => i.kind === kind);
  }

  /** Arm placement for an item; the next plan click drops it there. */
  startPlacing(i: PlotItem): void {
    if (!this.store.editing) {
      this.store.notify('Switch to Edit to plot items');
      return;
    }
    if (!this.store.currentPlan) {
      this.store.notify('Upload a floor plan for this location before plotting', 'error');
      return;
    }
    this.plotMenu = null;
    this.menuFor = null;
    this.select(i);
    this.placingId = i.id;
  }

  place(pos: { x: number; y: number }): void {
    const id = this.placingId;
    this.placingId = null;
    if (!id) return;
    let { x, y } = pos;
    if (this.settings.snap) {
      // Snap to the grid size in metres
      const { w, h } = this.planSize;
      const gx = (this.settings.grid / w) * 100;
      const gy = (this.settings.grid / h) * 100;
      x = Math.round(x / gx) * gx;
      y = Math.round(y / gy) * gy;
    }
    const item = this.store.items.find((i) => i.id === id);
    if (!item) return;
    this.store.selectedItemId = id;
    const zone = this.store.zoneAt(x, y);
    this.store.plot(id, x, y, `${item.name} (${item.code}) plotted${zone ? ` in ${zone.name} (${zone.code})` : ''}`);
  }

  /** Auto Plot (Bulk): drop every unplotted item inside a zone, or on a free spot (one PUT per item). */
  autoPlot(): void {
    const pending = this.store.unplotted;
    if (!pending.length) {
      this.store.notify('Nothing left to plot');
      return;
    }
    const zone = this.store.zones.find((z) => z.type === 'Production') ?? this.store.zones[0];
    const moves = pending.map((item, n) => ({
      id: item.id,
      x: zone ? zone.x + zone.w * (0.25 + 0.2 * (n % 3)) : 20 + 15 * (n % 5),
      y: zone ? zone.y + zone.h * (0.75 - 0.15 * Math.floor(n / 3)) : 20 + 15 * Math.floor(n / 5)
    }));
    const n = moves.length;
    this.store.plotMany(moves, `${n} item${n === 1 ? '' : 's'} plotted automatically - review their positions`);
  }

  remove(i: PlotItem): void {
    this.store.unplot(i.id, `${i.name} (${i.code}) removed from the map`);
  }

  /** Marker colour (kept on this page only - the plot API has no colour). */
  setTone(i: PlotItem, t: Tone): void {
    this.store.setTone(i, t);
  }

  rotate(i: PlotItem, deg: number): void {
    this.store.rotate(i, deg);
  }

  replaceIcon(): void {
    this.store.notify('Icon library is not connected yet - pick a colour instead');
  }
}
