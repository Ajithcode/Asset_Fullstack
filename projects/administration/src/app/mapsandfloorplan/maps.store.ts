import { Injectable } from '@angular/core';
import {
  FLOOR_PLANS,
  FloorPlan,
  LAYERS,
  LAYER_SETS,
  MapLayer,
  MapVersion,
  MapZone,
  PLOT_ITEMS,
  PlotItem,
  SITE_TREE,
  SiteNode,
  VERSIONS,
  ZONES,
  ZONE_TYPES,
  ZoneType
} from './maps.data';

/**
 * State shared by the Maps & Floor Plans tabs. Provided by the page, so switching tabs keeps the selected floor,
 * zone or plot. DEMO MODE: everything lives in memory.
 */
@Injectable()
export class MapsStore {
  readonly tree = SITE_TREE;
  floorPlans: FloorPlan[] = FLOOR_PLANS.map((p) => ({ ...p }));
  versions: MapVersion[] = VERSIONS.map((v) => ({ ...v, changes: [...v.changes] }));
  zones: MapZone[] = ZONES.map((z) => ({ ...z }));
  items: PlotItem[] = PLOT_ITEMS.map((i) => ({ ...i }));
  layers: MapLayer[] = LAYERS.map((l) => ({ ...l, categories: [...l.categories] }));
  layerSets = [...LAYER_SETS];
  activeLayerSet = LAYER_SETS[0];

  selectedFloorId = 'ho-a-f1';
  selectedPlanId = 'fp-1';
  selectedZoneId: string | null = 'z-pz1';
  selectedItemId: string | null = 'a-ch01';

  /** View / Edit switch in the map header: drawing and plotting tools need Edit. */
  editing = true;

  toast: string | null = null;
  private toastTimer: ReturnType<typeof setTimeout> | null = null;

  /* ===== Undo / redo of map edits (zones + plots) ===== */

  private undoStack: string[] = [];
  private redoStack: string[] = [];

  private snapshotOf(): string {
    return JSON.stringify({ zones: this.zones, items: this.items });
  }

  private restore(snap: string): void {
    const { zones, items } = JSON.parse(snap) as { zones: MapZone[]; items: PlotItem[] };
    // Icons do not survive JSON; take them back from the live records
    const icons = new Map(this.items.map((i) => [i.id, i.icon]));
    this.zones = zones;
    this.items = items.map((i) => ({ ...i, icon: icons.get(i.id) ?? i.icon }));
  }

  /** Call before every map edit so it can be undone. */
  checkpoint(): void {
    this.undoStack.push(this.snapshotOf());
    if (this.undoStack.length > 50) this.undoStack.shift();
    this.redoStack = [];
  }

  get canUndo(): boolean {
    return this.undoStack.length > 0;
  }

  get canRedo(): boolean {
    return this.redoStack.length > 0;
  }

  undo(): void {
    const snap = this.undoStack.pop();
    if (!snap) return;
    this.redoStack.push(this.snapshotOf());
    this.restore(snap);
  }

  redo(): void {
    const snap = this.redoStack.pop();
    if (!snap) return;
    this.undoStack.push(this.snapshotOf());
    this.restore(snap);
  }

  notify(message: string): void {
    this.toast = message;
    if (this.toastTimer) clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = null), 3200);
  }

  /* ===== Location ===== */

  /** Path from the organization down to a node, e.g. Head Office > Building A > Floor 1 (organization left out). */
  pathOf(id: string): SiteNode[] {
    const walk = (node: SiteNode, trail: SiteNode[]): SiteNode[] | null => {
      const next = [...trail, node];
      if (node.id === id) return next;
      for (const c of node.children ?? []) {
        const hit = walk(c, next);
        if (hit) return hit;
      }
      return null;
    };
    return (walk(this.tree, []) ?? []).slice(1);
  }

  locationLabel(id = this.selectedFloorId): string {
    return this.pathOf(id)
      .map((n) => n.label.replace(/\s*\(.*\)$/, ''))
      .join(' > ');
  }

  /** "Building A – Floor 1" */
  floorTitle(id = this.selectedFloorId): string {
    const path = this.pathOf(id);
    return path.slice(-2).map((n) => n.label.replace(/\s*\(.*\)$/, '')).join(' – ');
  }

  /** Every floor as a picker option. */
  get floors(): { id: string; label: string }[] {
    const out: { id: string; label: string }[] = [];
    const walk = (n: SiteNode) => {
      if (n.kind === 'floor') out.push({ id: n.id, label: this.locationLabel(n.id) });
      n.children?.forEach(walk);
    };
    walk(this.tree);
    return out;
  }

  selectFloor(id: string): void {
    this.selectedFloorId = id;
    const plans = this.plansOf(id);
    this.selectedPlanId = (plans.find((p) => p.status === 'Published') ?? plans[0])?.id ?? '';
  }

  /* ===== Floor plans ===== */

  plansOf(floorId: string): FloorPlan[] {
    return this.floorPlans.filter((p) => p.floorId === floorId);
  }

  get currentPlan(): FloorPlan | undefined {
    return this.floorPlans.find((p) => p.id === this.selectedPlanId) ?? this.plansOf(this.selectedFloorId)[0];
  }

  addPlan(name: string, format: FloorPlan['format']): FloorPlan {
    const plan: FloorPlan = {
      id: `fp-${Date.now()}`,
      floorId: this.selectedFloorId,
      name,
      subtitle: 'Uploaded just now',
      type: 'Floor Plan',
      format,
      sizeMb: 1.5,
      version: 'v1.0',
      status: 'Draft',
      updated: new Date().toISOString(),
      updatedBy: 'Sarah Ahmed',
      dimensions: '60.00 m × 32.00 m',
      scale: '1 : 100',
      fileName: `${name.replace(/\s+/g, '_')}_v1.0.${format.toLowerCase()}`
    };
    this.floorPlans = [...this.floorPlans, plan];
    this.selectedPlanId = plan.id;
    this.bumpCount(this.selectedFloorId, 1);
    return plan;
  }

  removePlan(id: string): void {
    const plan = this.floorPlans.find((p) => p.id === id);
    if (!plan) return;
    this.floorPlans = this.floorPlans.filter((p) => p.id !== id);
    this.bumpCount(plan.floorId, -1);
    if (this.selectedPlanId === id) this.selectedPlanId = this.plansOf(this.selectedFloorId)[0]?.id ?? '';
  }

  private bumpCount(floorId: string, by: number): void {
    const node = this.pathOf(floorId).at(-1);
    if (node) node.count = Math.max(0, (node.count ?? 0) + by);
  }

  /* ===== Zones ===== */

  get selectedZone(): MapZone | undefined {
    return this.zones.find((z) => z.id === this.selectedZoneId);
  }

  addZone(shape: MapZone['shape']): MapZone {
    this.checkpoint();
    const n = this.zones.length + 1;
    const zone: MapZone = {
      id: `z-${Date.now()}`,
      code: `ZN-${String(n).padStart(2, '0')}`,
      name: `New ${shape} Zone`,
      type: 'Operational',
      tone: 'grey',
      shape,
      x: 40,
      y: 40,
      w: shape === 'Circle' ? 12 : 18,
      h: shape === 'Circle' ? 22 : 18,
      areaM2: 96,
      description: '',
      accessRule: 'All Staff',
      active: true,
      entryAlert: false,
      exitAlert: false,
      dwellAlert: false,
      dwellMinutes: 30,
      geofence: false
    };
    this.zones = [...this.zones, zone];
    this.selectedZoneId = zone.id;
    return zone;
  }

  saveZone(draft: MapZone): void {
    this.checkpoint();
    draft.tone = ZONE_TYPES.find((t) => t.type === draft.type)?.tone ?? draft.tone;
    this.zones = this.zones.map((z) => (z.id === draft.id ? { ...draft } : z));
  }

  removeZone(id: string): void {
    this.checkpoint();
    this.zones = this.zones.filter((z) => z.id !== id);
    this.items.forEach((i) => i.zoneId === id && (i.zoneId = undefined));
    if (this.selectedZoneId === id) this.selectedZoneId = this.zones[0]?.id ?? null;
  }

  zoneCountOf(type: ZoneType): number {
    return this.zones.filter((z) => z.type === type).length;
  }

  /* ===== Plots ===== */

  get selectedItem(): PlotItem | undefined {
    return this.items.find((i) => i.id === this.selectedItemId);
  }

  get devices(): PlotItem[] {
    return this.items.filter((i) => i.kind === 'device');
  }

  get assets(): PlotItem[] {
    return this.items.filter((i) => i.kind === 'asset');
  }

  get plotted(): PlotItem[] {
    return this.items.filter((i) => i.x !== null);
  }

  get unplotted(): PlotItem[] {
    return this.items.filter((i) => i.x === null);
  }

  /** The zone whose box contains a point on the plan. */
  zoneAt(x: number, y: number): MapZone | undefined {
    return this.zones.find((z) => x >= z.x && x <= z.x + z.w && y >= z.y && y <= z.y + z.h);
  }

  plot(id: string, x: number, y: number): void {
    const item = this.items.find((i) => i.id === id);
    if (!item) return;
    this.checkpoint();
    item.x = Math.round(x * 10) / 10;
    item.y = Math.round(y * 10) / 10;
    item.zoneId = this.zoneAt(item.x, item.y)?.id;
    item.lastUpdated = new Date().toISOString();
  }

  unplot(id: string): void {
    const item = this.items.find((i) => i.id === id);
    if (!item) return;
    this.checkpoint();
    item.x = null;
    item.y = null;
    item.zoneId = undefined;
  }

  /* ===== Layers ===== */

  layer(id: string): MapLayer | undefined {
    return this.layers.find((l) => l.id === id);
  }

  isOn(id: string): boolean {
    const l = this.layer(id);
    return !!l && l.enabled && l.visible;
  }

  /* ===== Versions ===== */

  get currentVersion(): MapVersion | undefined {
    return this.versions.find((v) => v.current);
  }

  versionCount(status: MapVersion['status']): number {
    return this.versions.filter((v) => v.status === status).length;
  }

  publish(version: string): void {
    this.versions = this.versions.map((v) =>
      v.version === version
        ? { ...v, status: 'Published', current: true, uploadedOn: new Date().toISOString() }
        : { ...v, current: false, status: v.status === 'Published' ? 'Archived' : v.status }
    );
    const plan = this.currentPlan;
    if (plan) {
      plan.version = version;
      plan.status = 'Published';
    }
  }

  setVersionStatus(version: string, status: MapVersion['status']): void {
    this.versions = this.versions.map((v) => (v.version === version ? { ...v, status, current: status === 'Published' && v.current } : v));
  }

  addVersion(description: string): MapVersion {
    const latest = this.versions[0]?.version ?? 'v0.0';
    const [major, minor] = latest.slice(1).split('.').map(Number);
    const next: MapVersion = {
      version: `v${major}.${minor + 1}`,
      current: false,
      status: 'Draft',
      changes: [description || 'New upload'],
      uploadedBy: 'Sarah Ahmed',
      uploadedOn: new Date().toISOString(),
      fileName: `BuildingA_Floor1_v${major}.${minor + 1}.png`,
      sizeMb: 2.5,
      description: description || 'New upload'
    };
    this.versions = [next, ...this.versions];
    return next;
  }
}
