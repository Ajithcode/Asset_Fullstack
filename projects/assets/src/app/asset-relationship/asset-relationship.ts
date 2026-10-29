import { Component, DestroyRef, HostListener, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  LucideAngularModule,
  LucideIconData,
  AirVent,
  BatteryCharging,
  Box,
  Building2,
  Cctv,
  ChevronDown,
  Cog,
  Cpu,
  Download,
  EllipsisVertical,
  Eye,
  Fan,
  Flame,
  Forklift,
  Fuel,
  Laptop,
  LayoutGrid,
  MapPin,
  Tag,
  List,
  Maximize2,
  Minimize2,
  Network,
  Pencil,
  Plus,
  Printer,
  Search,
  Server,
  SolarPanel,
  ToggleRight,
  Trash2,
  Workflow,
  X,
  Zap,
} from 'lucide-angular';
import {
  ASSET_RECORDS,
  AssetHeader,
  AssetIconKey,
  AssetTabLink,
  assetCondition,
  assetHeader,
  assetLocation,
  assetTabs,
  conditionPill,
  criticalityPill,
  findAsset,
  statusPill,
} from 'shared-ui';
import { AssetDetail, cloneDetail, loadAssetDetail } from '../asset-details/asset-details';
import { FROM_RELATIONSHIPS, nowLocal, readUserRelationships, writeUserRelationships } from '../asset-relationship-list/user-relationships';

type RelType = 'Parent Asset' | 'Connected Asset' | 'Supporting Asset' | 'Component';
type Direction = 'Parent' | 'Child' | 'Sibling';
type RelStatus = 'Active' | 'Inactive';
type Condition = 'Good' | 'Fair' | 'Poor' | '';
type Layout = 'hierarchical' | 'horizontal';
type Side = 'top' | 'bottom' | 'left' | 'right';
type IconKey = keyof AssetRelationship['assetIcons'];

/** An asset that can be linked to the current one (static demo catalogue). */
interface CatalogAsset {
  tag: string;
  name: string;
  category: string;
  location: string;
  condition: Condition;
  icon: IconKey;
}

interface Relationship {
  id: number;
  /** Tag of the related asset in the catalogue. */
  tag: string;
  type: RelType;
  direction: Direction;
  /** ISO dates (yyyy-mm-dd); `to` is empty while the link is open. */
  since: string;
  to: string;
  status: RelStatus;
  description: string;
  reference: string;
  remarks: string;
}

type RelationshipForm = Omit<Relationship, 'id'>;

interface DiagramNode {
  rel: Relationship;
  asset: CatalogAsset;
  x: number;
  y: number;
  side: Side;
}

interface DiagramEdge {
  id: number;
  d: string;
  color: string;
}

/** Relationships of one asset, cached per tag so edits survive moving between tabs (in-memory demo). */
interface AssetGraph {
  catalog: CatalogAsset[];
  relationships: Relationship[];
  nextId: number;
}

// Nodes show name, category and location as well as the relationship type
const NODE_W = 250;
const NODE_H = 104;
const CENTER_W = 270;
const CENTER_H = 104;
const GAP_X = 24;
const GAP_Y = 22;
const ROW_GAP = 56;
const MIN_W = 880;

const TYPE_COLORS: Record<RelType, string> = {
  'Parent Asset': '#2563eb',
  'Connected Asset': '#16a34a',
  'Supporting Asset': '#f59e0b',
  Component: '#ea580c',
};

/** Register icon -> relationship node icon. */
const ICON_FOR: Record<AssetIconKey, IconKey> = {
  generator: 'Zap', chiller: 'Fan', ups: 'BatteryCharging', forklift: 'Forklift', fire: 'Flame',
  ac: 'AirVent', laptop: 'Laptop', cctv: 'Cctv', transformer: 'Cog', solar: 'SolarPanel',
};

const GRAPHS = new Map<string, AssetGraph>();

/**
 * Builds an asset's relationship graph from its data file. Anything in the register can be linked,
 * so the other register assets are added to the catalogue too.
 */
function buildGraph(tag: string, detail: AssetDetail | null): AssetGraph {
  const catalog: CatalogAsset[] = (detail?.relationships.parts ?? []).map((p) => ({ ...p, icon: p.icon as IconKey }));
  for (const other of ASSET_RECORDS) {
    if (other.tag === tag || catalog.some((c) => c.tag === other.tag)) continue;
    catalog.push({ tag: other.tag, name: other.name, category: other.category, location: assetLocation(other), condition: assetCondition(other), icon: ICON_FOR[other.icon] });
  }
  const relationships: Relationship[] = cloneDetail(detail?.relationships.links ?? []);
  return { catalog, relationships, nextId: Math.max(0, ...relationships.map((r) => r.id)) + 1 };
}

/**
 * Opened from the Asset Relationship page: the asset's sub assets created there, as Child relationships.
 * Only register assets can be linked, so the catalogue holds just those.
 */
function buildUserGraph(tag: string): AssetGraph {
  const graph = buildGraph(tag, null);
  graph.relationships = readUserRelationships()
    .filter((u) => u.mainTag === tag)
    .map((u) => ({
      id: u.id, tag: u.subTag, type: 'Connected Asset' as RelType, direction: 'Child' as Direction, since: u.createdAt.slice(0, 10), to: '',
      status: 'Active' as RelStatus, description: `Sub asset of ${tag}.`, reference: 'Asset Relationship', remarks: `Linked by ${u.createdBy}`,
    }));
  graph.nextId = Math.max(0, ...readUserRelationships().map((u) => u.id)) + 1;
  return graph;
}

/**
 * Opened with several assets from the Asset Relationship page: the asset's own relationships plus the sub assets
 * linked on that page (the same data its Related Assets popup lists). Built fresh for each visit.
 */
function buildCombinedGraph(tag: string, detail: AssetDetail | null): AssetGraph {
  const graph = buildGraph(tag, detail);
  const users = readUserRelationships();
  let id = Math.max(graph.nextId - 1, ...users.map((u) => u.id)) + 1;
  for (const u of users) {
    if (u.mainTag !== tag || graph.relationships.some((r) => r.tag === u.subTag)) continue;
    graph.relationships.push({
      id: id++, tag: u.subTag, type: 'Connected Asset', direction: 'Child', since: u.createdAt.slice(0, 10), to: '',
      status: 'Active', description: `Sub asset of ${tag}.`, reference: 'Asset Relationship', remarks: `Linked by ${u.createdBy}`,
    });
  }
  graph.nextId = id;
  return graph;
}

type Diagram = { width: number; height: number; center: { x: number; y: number }; nodes: DiagramNode[]; edges: DiagramEdge[] };

/** One of the assets opened together; `graph` is null while its data loads. */
interface DiagramSet {
  tag: string;
  asset: AssetHeader;
  graph: AssetGraph | null;
}

/** DEMO MODE: all data is static and kept in memory. */
@Component({
  selector: 'app-asset-relationship',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './asset-relationship.html',
  styleUrls: ['./asset-relationship.css'],
})
export class AssetRelationship {
  readonly icons = {
    ChevronDown, Download, EllipsisVertical, Eye, LayoutGrid, List, MapPin, Maximize2, Minimize2,
    Network, Pencil, Plus, Printer, Search, Tag, Trash2, Workflow, X,
  };

  /** Location nodes (the asset's site / room) rather than equipment. */
  isPlace(a: CatalogAsset): boolean {
    return a.icon === 'Building2' || a.tag.startsWith('LOC-');
  }
  readonly assetIcons = {
    Building2, ToggleRight, BatteryCharging, Server, Fuel, Cog, Cpu, Zap, AirVent, Box, Cctv, Fan, Flame, Forklift, Laptop, SolarPanel,
  };
  readonly iconFor = ICON_FOR;

  /** Icon for an asset without a photo (any opened asset, in its diagram centre). */
  nodeIcon(a: CatalogAsset): LucideIconData {
    return this.assetIcons[a.icon];
  }

  centerIconOf(a: AssetHeader): LucideIconData {
    return this.assetIcons[ICON_FOR[a.icon]];
  }
  readonly statusPill = statusPill;
  readonly conditionPill = conditionPill;
  readonly criticalityPill = criticalityPill;

  /** The asset named by ?tag=; when it is missing or unknown the page shows a "not found" message. */
  asset: AssetHeader = { tag: '', name: '', category: '', model: '', location: '', status: 'Active', condition: 'Good', criticality: 'Low', image: null, icon: 'generator' };
  notFound = false;
  requestedTag = '';
  tabs: AssetTabLink[] = assetTabs('Relationships');

  readonly relTypes: RelType[] = ['Parent Asset', 'Connected Asset', 'Supporting Asset', 'Component'];
  readonly directions: Direction[] = ['Parent', 'Child', 'Sibling'];
  readonly statuses: RelStatus[] = ['Active', 'Inactive'];

  private graph: AssetGraph = { catalog: [], relationships: [], nextId: 1 };
  /** True while the asset's relationships are being fetched. */
  loading = false;

  get catalog(): CatalogAsset[] {
    return this.graph.catalog;
  }

  get relationships(): Relationship[] {
    return this.graph.relationships;
  }

  set relationships(value: Relationship[]) {
    this.graph.relationships = value;
  }

  private get nextId(): number {
    return this.graph.nextId;
  }

  private set nextId(value: number) {
    this.graph.nextId = value;
  }

  // Toolbar
  viewFilter: RelType | 'all' = 'all';
  layout: Layout = 'hierarchical';
  fullScreen = false;
  actionsOpen = false;

  // Diagram + details
  selectedId: number | null = null;

  // Related assets table
  search = '';
  typeFilter: RelType | 'all' = 'all';
  statusFilter: RelStatus | 'all' = 'all';
  tableView: 'list' | 'grid' = 'list';
  checked = new Set<number>();
  rowMenuId: number | null = null;

  // Modals
  formOpen = false;
  editingId: number | null = null;
  form: RelationshipForm = this.emptyForm();
  formError = '';
  removeIds: number[] | null = null;
  /** Opened from the Asset Relationship page (?from=relationships) rather than the Asset List. */
  fromRelationships = false;
  /** Assets opened together (?tags=), each with its own diagram; the page header and table follow the active one (?tag=). */
  sets: DiagramSet[] = [];
  private setsKey = '';
  private setGraphs = new Map<string, Promise<AssetGraph>>();
  /** Relationship to select once the next asset is shown (a node clicked in another asset's diagram). */
  private pendingSelect: number | null = null;
  private readonly router = inject(Router);
  private diagramCache = new WeakMap<AssetGraph, { key: string; value: Diagram }>();

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  constructor() {
    inject(ActivatedRoute)
      .queryParamMap.pipe(takeUntilDestroyed(inject(DestroyRef)))
      .subscribe((params) => {
        const tags = [...new Set((params.get('tags') ?? '').split(',').map((t) => t.trim()).filter((t) => findAsset(t)))];
        this.fromRelationships = params.get('from') === FROM_RELATIONSHIPS || tags.length > 0;
        this.loadSets(tags);
        this.load(params.get('tag') ?? tags[tags.length - 1] ?? null);
      });
  }

  /** Shows the asset named in the URL, building (once) and reusing its relationship graph. */
  private load(tag: string | null): void {
    tag ??= ASSET_RECORDS[0].tag; // opened from the menu without an asset: show the first one
    const record = findAsset(tag);
    this.requestedTag = tag ?? '';
    this.notFound = !record;
    if (!record) return;
    this.asset = assetHeader(record);
    // Several assets opened together: share the graph shown in this asset's own diagram
    const fromSet = this.setGraphs.get(record.tag);
    if (fromSet) {
      this.show({ catalog: [], relationships: [], nextId: 1 });
      this.loading = true;
      fromSet.then((graph) => {
        if (this.requestedTag === record.tag) this.show(graph);
      });
      return;
    }
    // From the Asset Relationship page: show the sub assets linked there (read fresh, they change on that page)
    if (this.fromRelationships) {
      this.show(buildUserGraph(record.tag));
      return;
    }
    const cached = GRAPHS.get(record.tag);
    if (cached) {
      this.show(cached);
      return;
    }
    this.show({ catalog: [], relationships: [], nextId: 1 });
    this.loading = true;
    loadAssetDetail(record.tag).then((detail) => {
      if (this.requestedTag !== record.tag) return; // the user moved on to another asset
      // A failed download is not cached, so a later visit tries again
      const graph = buildGraph(record.tag, detail);
      if (detail) GRAPHS.set(record.tag, graph);
      this.show(graph);
    });
  }

  /** Starts loading every asset opened together (kept while switching between them). */
  private loadSets(tags: string[]): void {
    const key = tags.join(',');
    if (key === this.setsKey) return;
    this.setsKey = key;
    this.setGraphs.clear();
    this.sets = tags.map((tag) => ({ tag, asset: assetHeader(findAsset(tag)!), graph: null }));
    for (const set of this.sets) {
      const promise = loadAssetDetail(set.tag).then((detail) => buildCombinedGraph(set.tag, detail));
      this.setGraphs.set(set.tag, promise);
      promise.then((graph) => {
        if (this.sets.includes(set)) set.graph = graph;
      });
    }
  }

  trackSet(_: number, set: DiagramSet): string {
    return set.tag;
  }

  /** Makes another opened asset the active one (header, details and table), optionally selecting a relationship. */
  activate(set: DiagramSet, relId: number | null = null): void {
    if (set.tag === this.asset.tag) {
      if (relId !== null) this.selectedId = relId;
      return;
    }
    this.pendingSelect = relId;
    this.router.navigate([], { queryParams: { tag: set.tag }, queryParamsHandling: 'merge' });
  }

  private show(graph: AssetGraph): void {
    this.loading = false;
    this.graph = graph;
    // Reset per-asset view state
    const pending = this.pendingSelect;
    this.pendingSelect = null;
    this.selectedId = graph.relationships.some((r) => r.id === pending)
      ? pending
      : graph.relationships.find((r) => r.type !== 'Parent Asset')?.id ?? graph.relationships[0]?.id ?? null;
    this.checked.clear();
    this.search = '';
    this.typeFilter = 'all';
    this.statusFilter = 'all';
    this.viewFilter = 'all';
    this.closeMenus();
    this.formOpen = false;
    this.removeIds = null;
  }

  // ===== Lookups =====
  assetFor(tag: string, graph: AssetGraph = this.graph): CatalogAsset {
    return graph.catalog.find((a) => a.tag === tag) ?? { tag, name: tag, category: '-', location: '-', condition: '', icon: 'Cog' };
  }

  typeClass(type: RelType): string {
    return 'type--' + type.split(' ')[0].toLowerCase();
  }

  formatDate(iso: string): string {
    if (!iso) return '-';
    const d = new Date(iso + 'T00:00:00');
    return isNaN(d.getTime()) ? iso : d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
  }

  get selected(): Relationship | null {
    return this.relationships.find((r) => r.id === this.selectedId) ?? null;
  }

  select(rel: Relationship): void {
    this.selectedId = rel.id;
  }

  // ===== Diagram =====
  get diagram(): Diagram {
    return this.diagramOf(this.graph);
  }

  /** A graph's diagram, recomputed only when its relationships, the view filter or the layout change. */
  diagramOf(graph: AssetGraph): Diagram {
    const key = `${this.layout}|${this.viewFilter}|${graph.catalog.length}|${graph.relationships.map((r) => `${r.id}:${r.tag}:${r.type}:${r.status}`).join(',')}`;
    const hit = this.diagramCache.get(graph);
    if (hit && hit.key === key) return hit.value;
    const value = this.layoutDiagram(graph);
    this.diagramCache.set(graph, { key, value });
    return value;
  }

  /** Places nodes around the asset: parents above, components below, the rest split left/right. */
  private layoutDiagram(graph: AssetGraph): Diagram {
    const rels = this.viewFilter === 'all' ? graph.relationships : graph.relationships.filter((r) => r.type === this.viewFilter);
    const sides: Record<Side, Relationship[]> = { top: [], bottom: [], left: [], right: [] };
    if (this.layout === 'horizontal') {
      for (const r of rels) (r.type === 'Parent Asset' ? sides.left : sides.right).push(r);
    } else {
      let flip = 0;
      for (const r of rels) {
        if (r.type === 'Parent Asset') sides.top.push(r);
        else if (r.type === 'Component') sides.bottom.push(r);
        else (flip++ % 2 === 0 ? sides.left : sides.right).push(r);
      }
    }

    const rowWidth = (n: number) => n * NODE_W + Math.max(0, n - 1) * GAP_X;
    const width = Math.max(MIN_W, rowWidth(sides.top.length), rowWidth(sides.bottom.length), 2 * NODE_W + CENTER_W + 160);
    const stackHeight = (n: number) => n * NODE_H + Math.max(0, n - 1) * GAP_Y;
    const midTop = sides.top.length ? NODE_H + ROW_GAP : 0;
    const midHeight = Math.max(CENTER_H, stackHeight(sides.left.length), stackHeight(sides.right.length));
    const bottomTop = midTop + midHeight + ROW_GAP;
    const height = sides.bottom.length ? bottomTop + NODE_H : midTop + midHeight;

    const center = { x: (width - CENTER_W) / 2, y: midTop + (midHeight - CENTER_H) / 2 };
    const cx = width / 2;
    const cy = center.y + CENTER_H / 2;

    const nodes: DiagramNode[] = [];
    const place = (list: Relationship[], side: Side) => {
      list.forEach((rel, i) => {
        let x = 0;
        let y = 0;
        if (side === 'top' || side === 'bottom') {
          x = (width - rowWidth(list.length)) / 2 + i * (NODE_W + GAP_X);
          y = side === 'top' ? 0 : bottomTop;
        } else {
          x = side === 'left' ? 0 : width - NODE_W;
          y = midTop + (midHeight - stackHeight(list.length)) / 2 + i * (NODE_H + GAP_Y);
        }
        nodes.push({ rel, asset: this.assetFor(rel.tag, graph), x, y, side });
      });
    };
    (Object.keys(sides) as Side[]).forEach((s) => place(sides[s], s));

    const edges: DiagramEdge[] = nodes.map((n) => {
      const nx = n.x + NODE_W / 2;
      const ny = n.y + NODE_H / 2;
      let d: string;
      if (n.side === 'top') {
        const mid = (n.y + NODE_H + center.y) / 2;
        d = `M ${cx} ${center.y} V ${mid} H ${nx} V ${n.y + NODE_H + 2}`;
      } else if (n.side === 'bottom') {
        const mid = (center.y + CENTER_H + n.y) / 2;
        d = `M ${cx} ${center.y + CENTER_H} V ${mid} H ${nx} V ${n.y - 2}`;
      } else if (n.side === 'left') {
        const edge = n.x + NODE_W;
        const mid = (edge + center.x) / 2;
        d = `M ${center.x} ${cy} H ${mid} V ${ny} H ${edge + 2}`;
      } else {
        const mid = (center.x + CENTER_W + n.x) / 2;
        d = `M ${center.x + CENTER_W} ${cy} H ${mid} V ${ny} H ${n.x - 2}`;
      }
      return { id: n.rel.id, d, color: TYPE_COLORS[n.rel.type] };
    });

    return { width, height, center, nodes, edges };
  }

  readonly typeColors = TYPE_COLORS;
  readonly nodeW = NODE_W;
  readonly nodeH = NODE_H;
  readonly centerW = CENTER_W;
  readonly centerH = CENTER_H;

  markerId(color: string, tag = ''): string {
    return 'rel-arrow-' + color.slice(1) + (tag ? '-' + tag : '');
  }

  get markerColors(): string[] {
    return [...new Set(Object.values(TYPE_COLORS))];
  }

  toggleFullScreen(): void {
    this.fullScreen = !this.fullScreen;
  }

  // ===== Table =====
  get filteredRels(): Relationship[] {
    const q = this.search.trim().toLowerCase();
    return this.relationships.filter((r) => {
      if (this.typeFilter !== 'all' && r.type !== this.typeFilter) return false;
      if (this.statusFilter !== 'all' && r.status !== this.statusFilter) return false;
      if (!q) return true;
      const a = this.assetFor(r.tag);
      return [r.tag, a.name, a.category, a.location, r.type, r.direction].some((v) => v.toLowerCase().includes(q));
    });
  }

  get allChecked(): boolean {
    const rows = this.filteredRels;
    return rows.length > 0 && rows.every((r) => this.checked.has(r.id));
  }

  get checkedIds(): number[] {
    return [...this.checked];
  }

  toggleAll(on: boolean): void {
    for (const r of this.filteredRels) on ? this.checked.add(r.id) : this.checked.delete(r.id);
  }

  toggleCheck(id: number, on: boolean): void {
    on ? this.checked.add(id) : this.checked.delete(id);
  }

  toggleRowMenu(id: number, event: Event): void {
    event.stopPropagation();
    this.rowMenuId = this.rowMenuId === id ? null : id;
  }

  toggleActions(event: Event): void {
    event.stopPropagation();
    this.actionsOpen = !this.actionsOpen;
  }

  @HostListener('document:click')
  closeMenus(): void {
    this.rowMenuId = null;
    this.actionsOpen = false;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.formOpen) this.formOpen = false;
    else if (this.removeIds) this.removeIds = null;
    else this.fullScreen = false;
  }

  // ===== Add / edit =====
  private emptyForm(): RelationshipForm {
    return { tag: '', type: 'Connected Asset', direction: 'Child', since: new Date().toISOString().slice(0, 10), to: '', status: 'Active', description: '', reference: '', remarks: '' };
  }

  /** Catalogue assets not yet linked (plus the one being edited). */
  get assetOptions(): CatalogAsset[] {
    const editingTag = this.relationships.find((r) => r.id === this.editingId)?.tag;
    const used = new Set(this.relationships.map((r) => r.tag).filter((t) => t !== editingTag));
    return this.catalog.filter((a) => !used.has(a.tag));
  }

  openAdd(): void {
    this.actionsOpen = false;
    this.editingId = null;
    this.form = this.emptyForm();
    this.formError = '';
    this.formOpen = true;
  }

  openEdit(rel: Relationship): void {
    this.rowMenuId = null;
    this.editingId = rel.id;
    const { id, ...rest } = rel;
    this.form = { ...rest };
    this.formError = '';
    this.formOpen = true;
  }

  saveForm(): void {
    const f = this.form;
    if (!f.tag || !f.since) {
      this.formError = 'Select the related asset and the start date.';
      return;
    }
    if (f.to && f.to < f.since) {
      this.formError = 'The end date cannot be before the start date.';
      return;
    }
    const data: RelationshipForm = { ...f, description: f.description.trim(), reference: f.reference.trim(), remarks: f.remarks.trim() };
    if (this.editingId !== null) {
      this.relationships = this.relationships.map((r) => (r.id === this.editingId ? { id: r.id, ...data } : r));
      this.showToast('Relationship updated.');
    } else {
      const rel: Relationship = { id: this.nextId++, ...data };
      this.relationships = [...this.relationships, rel];
      if (this.fromRelationships) {
        const links = readUserRelationships();
        if (!links.some((u) => u.mainTag === this.asset.tag && u.subTag === rel.tag)) {
          writeUserRelationships([{ id: rel.id, mainTag: this.asset.tag, subTag: rel.tag, createdBy: 'Rex Kumar', createdAt: nowLocal() }, ...links]);
        }
      }
      this.selectedId = rel.id;
      this.showToast(`${rel.tag} linked to ${this.asset.tag}.`);
    }
    this.formOpen = false;
  }

  // ===== Remove =====
  askRemove(ids: number[]): void {
    this.rowMenuId = null;
    if (ids.length) this.removeIds = ids;
  }

  get removeLabel(): string {
    const ids = this.removeIds ?? [];
    return ids.length === 1 ? this.relationships.find((r) => r.id === ids[0])?.tag ?? '' : `${ids.length} relationships`;
  }

  confirmRemove(): void {
    const ids = new Set(this.removeIds ?? []);
    const removedTags = new Set(this.relationships.filter((r) => ids.has(r.id)).map((r) => r.tag));
    this.relationships = this.relationships.filter((r) => !ids.has(r.id));
    if (this.fromRelationships) {
      writeUserRelationships(readUserRelationships().filter((u) => !(u.mainTag === this.asset.tag && removedTags.has(u.subTag))));
    }
    ids.forEach((id) => this.checked.delete(id));
    if (this.selectedId !== null && ids.has(this.selectedId)) this.selectedId = this.relationships[0]?.id ?? null;
    this.showToast(ids.size === 1 ? 'Relationship removed.' : `${ids.size} relationships removed.`);
    this.removeIds = null;
  }

  // ===== Misc =====
  tabClick(tab: AssetTabLink): void {
    if (!tab.path && !tab.active) this.showToast(`${tab.label} is not available in this demo.`);
  }

  exportCsv(): void {
    this.actionsOpen = false;
    const head = ['Related Asset ID', 'Asset Name', 'Relationship Type', 'Direction', 'Category', 'Location', 'Status', 'Condition', 'From Date', 'To Date', 'Description'];
    const rows = this.relationships.map((r) => {
      const a = this.assetFor(r.tag);
      return [r.tag, a.name, r.type, r.direction, a.category, a.location, r.status, a.condition || '-', r.since, r.to, r.description];
    });
    const csv = [head, ...rows].map((row) => row.map((v) => `"${String(v).replace(/"/g, '""')}"`).join(',')).join('\r\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = `${this.asset.tag}-relationships.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }

  print(): void {
    this.actionsOpen = false;
    window.print();
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 2600);
  }
}
