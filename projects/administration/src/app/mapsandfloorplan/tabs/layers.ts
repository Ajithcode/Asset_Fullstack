import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CircleHelp, Eye, EyeOff, GripVertical, Lock, LucideAngularModule, Pencil, Plus, Trash2, X } from 'lucide-angular';
import { MapCanvas } from '../map-canvas/map-canvas';
import { MapViewer } from '../map-viewer/map-viewer';
import { MapLayer } from '../maps.data';
import { MapsStore } from '../maps.store';

const CATEGORIES = ['HVAC', 'Electrical', 'Mechanical', 'Plumbing', 'Fire Safety', 'IT'];
const LAYER_TYPES = ['Zone (Area)', 'Geofence (Alerting)', 'Asset (Plotted / Tracked)', 'Device (IoT)', 'Annotation (Text)', 'Room (Space)', 'Heatmap', 'Base Map'];

/** Saved layer visibility for each layer set (view). */
type LayerSetState = Record<string, boolean>;

/** Layers: which layers draw on the map, their order and style, and saved layer sets (views). */
@Component({
  standalone: true,
  selector: 'app-maps-layers',
  imports: [FormsModule, LucideAngularModule, MapViewer, MapCanvas],
  templateUrl: './layers.html',
  styleUrls: ['../../shared/admin-workspace.css', '../maps.css'],
  styles: `
    .layer-row { display: flex; align-items: center; gap: 8px; padding: 7px 6px; border: 1px solid transparent; border-bottom-color: #f1f2f6; border-radius: 8px; cursor: pointer; }
    .layer-row:hover { background: #faf9fe; }
    .layer-row.active { background: var(--adm-purple-soft); border-color: #c9b8ef; }
    .layer-row.drag-over { box-shadow: inset 0 2px 0 var(--adm-purple); }
    .layer-row__name { flex: 1; min-width: 0; display: flex; align-items: center; gap: 8px; font-weight: 600; font-size: 12.5px; }
    .layer-row__name lucide-icon { color: var(--adm-purple); }
    .grip { cursor: grab; color: #94a3b8; }
    .sets { display: flex; flex-direction: column; gap: 2px; }
    .set-row { display: flex; align-items: center; gap: 8px; padding: 6px; border-radius: 8px; font-size: 12.5px; }
    .set-row.active { background: var(--adm-purple-soft); }
    .set-row label { flex: 1; display: flex; align-items: center; gap: 8px; cursor: pointer; }
    .set-row input[type='text'] { flex: 1; height: 28px; border: 1px solid var(--adm-border); border-radius: 6px; padding: 0 6px; font: inherit; }
    .chips { display: flex; flex-wrap: wrap; gap: 6px; padding: 6px; border: 1px solid var(--adm-border); border-radius: 10px; }
    .chip { display: inline-flex; align-items: center; gap: 4px; padding: 2px 8px; border-radius: 999px; background: var(--adm-purple-soft); color: var(--adm-purple); font-size: 12px; font-weight: 600; }
    .chip button { display: grid; place-items: center; padding: 0; border: 0; background: none; color: inherit; cursor: pointer; }
    .chips select { flex: 1; min-width: 90px; border: 0; outline: 0; font: inherit; background: transparent; }
    .range { display: flex; align-items: center; gap: 10px; }
    .range input { flex: 1; }
    .show-row { display: flex; align-items: center; gap: 12px; font-size: 12.5px; }
    .show-row label { display: inline-flex; align-items: center; gap: 5px; cursor: pointer; }
  `
})
export class LayersTab {
  readonly store = inject(MapsStore);
  readonly icons = { CircleHelp, Eye, EyeOff, GripVertical, Lock, Pencil, Plus, Trash2, X };
  readonly categories = CATEGORIES;
  readonly layerTypes = LAYER_TYPES;
  readonly labelPositions = ['Bottom Center', 'Top Center', 'Right', 'Left', 'Hidden'];
  readonly statuses = ['All Statuses', 'Active', 'Inactive', 'Under Maintenance'];

  rightTab: 'details' | 'style' = 'details';
  selectedId = 'assets';
  draft: MapLayer | null = null;
  dragId: string | null = null;
  overId: string | null = null;
  renaming: string | null = null;
  renameText = '';

  /** Visibility each layer set restores. */
  private setStates: Record<string, LayerSetState> = {
    'Operations View': this.capture(),
    'Maintenance View': { ...this.capture(), devices: false, geofences: false, heatmap: false },
    'Security View': { ...this.capture(), assets: false, rooms: false, geofences: true },
    'Audit View': { ...this.capture(), heatmap: true, annotations: true }
  };

  constructor() {
    this.pick(this.selectedId);
  }

  get enabledCount(): number {
    return this.store.layers.length;
  }

  private capture(): LayerSetState {
    return Object.fromEntries(this.store.layers.map((l) => [l.id, l.enabled]));
  }

  pick(id: string): void {
    this.selectedId = id;
    const l = this.store.layer(id);
    this.draft = l ? { ...l, categories: [...l.categories] } : null;
  }

  toggleEye(event: MouseEvent, l: MapLayer): void {
    event.stopPropagation();
    if (l.locked) return;
    l.visible = !l.visible;
    if (this.draft?.id === l.id) this.draft.visible = l.visible;
  }

  /* ===== Drag to reorder ===== */

  dragStart(id: string): void {
    this.dragId = id;
  }

  dragOver(event: DragEvent, id: string): void {
    event.preventDefault();
    this.overId = id;
  }

  drop(id: string): void {
    const from = this.store.layers.findIndex((l) => l.id === this.dragId);
    const to = this.store.layers.findIndex((l) => l.id === id);
    if (from >= 0 && to >= 0 && from !== to) {
      const list = [...this.store.layers];
      const [moved] = list.splice(from, 1);
      list.splice(to, 0, moved);
      this.store.layers = list;
    }
    this.dragId = null;
    this.overId = null;
  }

  /* ===== Layer sets ===== */

  applySet(name: string): void {
    this.setStates[this.store.activeLayerSet] = this.capture();
    this.store.activeLayerSet = name;
    const state = this.setStates[name] ?? {};
    this.store.layers.forEach((l) => (l.enabled = state[l.id] ?? l.enabled));
    this.pick(this.selectedId);
  }

  addSet(): void {
    let n = this.store.layerSets.length + 1;
    while (this.store.layerSets.includes(`Custom View ${n}`)) n++;
    const name = `Custom View ${n}`;
    this.store.layerSets = [...this.store.layerSets, name];
    this.setStates[name] = this.capture();
    this.store.activeLayerSet = name;
    this.startRename(name);
  }

  startRename(name: string): void {
    this.renaming = name;
    this.renameText = name;
  }

  commitRename(): void {
    const from = this.renaming;
    const to = this.renameText.trim();
    this.renaming = null;
    if (!from || !to || to === from || this.store.layerSets.includes(to)) return;
    this.store.layerSets = this.store.layerSets.map((s) => (s === from ? to : s));
    this.setStates[to] = this.setStates[from];
    delete this.setStates[from];
    if (this.store.activeLayerSet === from) this.store.activeLayerSet = to;
  }

  removeSet(name: string): void {
    if (this.store.layerSets.length <= 1) return;
    this.store.layerSets = this.store.layerSets.filter((s) => s !== name);
    delete this.setStates[name];
    if (this.store.activeLayerSet === name) this.applySet(this.store.layerSets[0]);
  }

  /* ===== Add / edit layer ===== */

  addLayer(): void {
    const id = `layer-${Date.now()}`;
    const base = this.store.layer('annotations')!;
    this.store.layers = [
      ...this.store.layers,
      { ...base, id, name: 'New Layer', description: '', enabled: true, visible: true, locked: false, categories: [] }
    ];
    this.pick(id);
    this.rightTab = 'details';
  }

  addCategory(c: string): void {
    if (this.draft && c && !this.draft.categories.includes(c)) this.draft.categories = [...this.draft.categories, c];
  }

  removeCategory(c: string): void {
    if (this.draft) this.draft.categories = this.draft.categories.filter((x) => x !== c);
  }

  reset(): void {
    this.pick(this.selectedId);
  }

  save(): void {
    // Enter on the Details tab moves on to Style instead of saving
    if (this.rightTab === 'details') {
      this.rightTab = 'style';
      return;
    }
    if (!this.draft || !this.draft.name.trim()) return;
    this.store.layers = this.store.layers.map((l) => (l.id === this.draft!.id ? { ...this.draft!, name: this.draft!.name.trim() } : l));
    this.store.notify(`Layer ${this.draft.name} saved`);
  }

  removeLayer(): void {
    if (!this.draft || this.draft.locked) return;
    const name = this.draft.name;
    this.store.layers = this.store.layers.filter((l) => l.id !== this.draft!.id);
    this.pick(this.store.layers[0]?.id ?? '');
    this.store.notify(`Layer ${name} removed`);
  }

  /* ===== Quick "Show" toggles above the map ===== */

  isShown(id: string): boolean {
    return !!this.store.layer(id)?.enabled;
  }

  setShown(id: string, on: boolean): void {
    const l = this.store.layer(id);
    if (l) l.enabled = on;
    if (this.draft?.id === id) this.draft.enabled = on;
  }
}
