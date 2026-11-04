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
import { Observable, catchError, firstValueFrom, forkJoin, of } from 'rxjs';
import {
  ASSET_RECORDS,
  AssetCondition,
  AssetHeader,
  AssetIconKey,
  AssetStatus,
  AssetTabLink,
  Criticality,
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
import { FROM_RELATIONSHIPS } from '../asset-relationship-list/user-relationships';
import {
  API_RELATIONSHIP_TYPES,
  AssetRelationshipDto,
  AssetRelationshipService,
  RegisteredAsset,
  RegisteredAssetDetail,
  RelatedAssetsDto,
  SubAssetDto,
  SubAssetRef,
  subAssetKey,
  toSubAssetRef,
} from '../asset-relationship-list/asset-relationship.service';
import { apiErrorMessage } from '../services/asset-api';

type RelType = 'Parent Asset' | 'Connected Asset' | 'Supporting Asset' | 'Component' | 'Sub Asset';
type Direction = 'Parent' | 'Child' | 'Sibling';
type RelStatus = 'Active' | 'Inactive';
type Condition = 'Good' | 'Fair' | 'Poor' | '';
type Layout = 'hierarchical' | 'horizontal';
type Side = 'top' | 'bottom' | 'left' | 'right';
type IconKey = keyof AssetRelationship['assetIcons'];

/** An asset that can be linked to the current one. */
interface CatalogAsset {
  tag: string;
  name: string;
  category: string;
  location: string;
  condition: Condition;
  icon: IconKey;
  /** What to send to the API to link it (registered asset or component of the main asset). */
  ref?: SubAssetRef;
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
  /** The saved sub asset this row shows (API-backed assets only). */
  sub?: SubAssetDto;
}

type RelationshipForm = Omit<Relationship, 'id' | 'sub'>;

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

/** The asset's saved relationship record in the Asset API. */
interface ApiLink {
  mainAssetRegistrationId: string;
  /** Null until the asset has a saved relationship. */
  relationshipId: string | null;
  subAssets: SubAssetDto[];
  createdBy: string | null;
  linkedAt: string | null;
}

/**
 * Relationships of one asset. For an asset in the Asset API, `api` is set and every change is saved there;
 * otherwise (sample assets only) the graph is the static demo data, cached per tag in memory.
 */
interface AssetGraph {
  catalog: CatalogAsset[];
  relationships: Relationship[];
  nextId: number;
  api?: ApiLink;
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
  'Sub Asset': '#0891b2',
};

/** Relationship types of the sample (demo) data. */
const DEMO_REL_TYPES: RelType[] = ['Parent Asset', 'Connected Asset', 'Supporting Asset', 'Component'];

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

/** Catalogue key of a saved sub asset: its Asset ID, or the component tag for a component. */
function subTag(s: SubAssetDto): string {
  return s.assetRegistrationId ?? (s.tag || s.componentId || '');
}

function guessIcon(...values: string[]): AssetIconKey {
  const text = values.join(' ').toLowerCase();
  const keys: [string, AssetIconKey][] = [
    ['generator', 'generator'], ['chiller', 'chiller'], ['ups', 'ups'], ['forklift', 'forklift'], ['fire', 'fire'],
    ['air', 'ac'], ['hvac', 'ac'], ['laptop', 'laptop'], ['computer', 'laptop'], ['cctv', 'cctv'], ['camera', 'cctv'],
    ['transformer', 'transformer'], ['solar', 'solar'],
  ];
  return keys.find(([word]) => text.includes(word))?.[1] ?? 'transformer';
}

function catalogFromAsset(a: RegisteredAsset): CatalogAsset {
  return {
    tag: a.id, name: a.name, category: a.category || '-', location: a.location || '-', condition: '',
    icon: ICON_FOR[guessIcon(a.assetType, a.category, a.name)], ref: { assetRegistrationId: a.id },
  };
}

function catalogFromSub(s: SubAssetDto): CatalogAsset {
  return {
    tag: subTag(s), name: s.name, category: s.category || '-', location: s.location || '-', condition: '',
    icon: s.componentId ? 'Cpu' : 'Box',
    ref: s.componentId ? { componentId: s.componentId } : { assetRegistrationId: s.assetRegistrationId },
  };
}

function toDate(iso: string | null | undefined): string {
  return iso ? iso.slice(0, 10) : '';
}

/** Shows a saved relationship record (or none) as the graph's rows; the catalogue keeps every linked sub asset. */
function applyRelationship(graph: AssetGraph, mainId: string, dto: AssetRelationshipDto | null): void {
  const subs = dto?.subAssets ?? [];
  graph.api = {
    mainAssetRegistrationId: mainId,
    relationshipId: dto?.id ?? null,
    subAssets: subs,
    createdBy: dto?.createdBy ?? null,
    linkedAt: dto?.lastUpdated ?? dto?.createdAt ?? null,
  };
  for (const s of subs) {
    if (!graph.catalog.some((c) => c.tag === subTag(s))) graph.catalog.push(catalogFromSub(s));
  }
  graph.relationships = subs.map((s, i) => ({
    id: i + 1,
    tag: subTag(s),
    type: ((TYPE_COLORS as Record<string, string>)[s.relationshipType] ? s.relationshipType : 'Sub Asset') as RelType,
    direction: 'Child',
    since: toDate(dto?.createdAt),
    to: '',
    status: 'Active',
    description: `${s.componentId ? 'Component' : 'Sub asset'} of ${mainId}.`,
    reference: 'Asset Relationship',
    remarks: dto?.createdBy ? `Linked by ${dto.createdBy}` : '',
    sub: s,
  }));
  graph.nextId = subs.length + 1;
}

/** An asset from the Asset API: its saved sub assets, with the register and its components as the catalogue. */
function buildApiGraph(
  asset: RegisteredAssetDetail,
  saved: AssetRelationshipDto | null,
  related: RelatedAssetsDto[],
  register: RegisteredAsset[],
): AssetGraph {
  const graph: AssetGraph = { catalog: [], relationships: [], nextId: 1 };
  for (const s of related.find((r) => r.mainAssetRegistrationId === asset.id)?.subAssets ?? []) {
    if (s.componentId) graph.catalog.push(catalogFromSub(s));
  }
  for (const a of register) {
    if (a.id !== asset.id && !graph.catalog.some((c) => c.tag === a.id)) graph.catalog.push(catalogFromAsset(a));
  }
  applyRelationship(graph, asset.id, saved);
  return graph;
}

function apiHeader(a: RegisteredAssetDetail): AssetHeader {
  return {
    tag: a.id,
    name: a.name,
    category: a.category || '-',
    model: [a.manufacturer, a.model || a.assetType].filter(Boolean).join(' ') || '-',
    location: a.location || '-',
    status: (a.status || '-') as AssetStatus,
    condition: (a.condition || '-') as AssetCondition,
    criticality: (a.criticality || '-') as Criticality,
    image: a.imageUrl,
    icon: guessIcon(a.assetType, a.category, a.name),
  };
}

function placeholderHeader(tag: string): AssetHeader {
  return { tag, name: '', category: '', model: '', location: '', status: 'Active', condition: 'Good', criticality: 'Low', image: null, icon: 'transformer' };
}

/** An asset resolved for this page: its header and relationship graph. */
interface LoadedAsset {
  header: AssetHeader;
  graph: AssetGraph;
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
  // Values from the Asset API may be empty or outside the sample lists: show them without a colour.
  readonly statusPill = (v: AssetStatus): string => (v && v !== ('-' as AssetStatus) ? statusPill(v) : '');
  readonly conditionPill = (v: AssetCondition): string => (['Good', 'Fair', 'Poor'].includes(v) ? conditionPill(v) : '');
  readonly criticalityPill = (v: Criticality): string => (['Critical', 'High', 'Medium', 'Low'].includes(v) ? criticalityPill(v) : '');

  /** The asset named by ?tag=; when it is missing or unknown the page shows a "not found" message. */
  asset: AssetHeader = { tag: '', name: '', category: '', model: '', location: '', status: 'Active', condition: 'Good', criticality: 'Low', image: null, icon: 'generator' };
  notFound = false;
  requestedTag = '';
  tabs: AssetTabLink[] = assetTabs('Relationships');

  /** Relationship types offered: the Asset API's for a registered asset, the sample ones otherwise. */
  relTypes: RelType[] = DEMO_REL_TYPES;
  readonly directions: Direction[] = ['Parent', 'Child', 'Sibling'];
  readonly statuses: RelStatus[] = ['Active', 'Inactive'];

  private graph: AssetGraph = { catalog: [], relationships: [], nextId: 1 };
  /** True while the asset's relationships are being fetched. */
  loading = false;
  /** True while a change is being saved to the Asset API. */
  saving = false;

  /** The shown asset is in the Asset API, so changes are saved there. */
  get apiMode(): boolean {
    return !!this.graph.api;
  }

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
  private setGraphs = new Map<string, Promise<LoadedAsset | null>>();
  /** Relationship to select once the next asset is shown (a node clicked in another asset's diagram). */
  private pendingSelect: number | null = null;
  private readonly router = inject(Router);
  private readonly api = inject(AssetRelationshipService);
  private diagramCache = new WeakMap<AssetGraph, { key: string; value: Diagram }>();

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  constructor() {
    inject(ActivatedRoute)
      .queryParamMap.pipe(takeUntilDestroyed(inject(DestroyRef)))
      .subscribe((params) => {
        const tags = [...new Set((params.get('tags') ?? '').split(',').map((t) => t.trim()).filter((t) => !!t))];
        this.fromRelationships = params.get('from') === FROM_RELATIONSHIPS || tags.length > 0;
        this.loadSets(tags);
        this.load(params.get('tag') ?? tags[tags.length - 1] ?? null);
      });
  }

  /** Shows the asset named in the URL: from the Asset API when it is registered there, else the sample data. */
  private load(tag: string | null): void {
    this.requestedTag = tag ?? '';
    this.notFound = false;
    // Keep a relationship picked in another asset's diagram for when this one is shown
    const pendingSelect = this.pendingSelect;
    this.show({ catalog: [], relationships: [], nextId: 1 });
    this.pendingSelect = pendingSelect;
    this.loading = true;
    // Several assets opened together: share the graph shown in this asset's own diagram
    const pending = (tag && this.setGraphs.get(tag)) || this.resolve(tag);
    pending.then((loaded) => {
      if (this.requestedTag !== (tag ?? '')) return; // the user moved on to another asset
      if (!loaded) {
        this.loading = false;
        this.notFound = true;
        return;
      }
      this.asset = loaded.header;
      this.show(loaded.graph);
    });
  }

  /** Loads an asset's header and relationships; null when neither the Asset API nor the sample data has it. */
  private async resolve(tag: string | null): Promise<LoadedAsset | null> {
    if (!tag) {
      // Opened from the menu without an asset: show the first registered one (or the first sample asset).
      const first = await firstValueFrom(this.api.getAssets().pipe(catchError(() => of([] as RegisteredAsset[]))));
      tag = first[0]?.id ?? ASSET_RECORDS[0]?.tag ?? '';
      if (!tag) return null;
    }
    const registered = await firstValueFrom(this.api.findAssetOrNull(tag));
    if (registered) {
      try {
        const [saved, related, register] = await firstValueFrom(
          forkJoin([
            this.api.getByMainAsset(registered.id),
            this.api.getRelated([registered.id]).pipe(catchError(() => of([] as RelatedAssetsDto[]))),
            this.api.getAssets().pipe(catchError(() => of([] as RegisteredAsset[]))),
          ]),
        );
        return { header: apiHeader(registered), graph: buildApiGraph(registered, saved, related, register) };
      } catch (err) {
        this.showToast(apiErrorMessage(err, 'Could not load the relationships of this asset.'));
        const graph: AssetGraph = { catalog: [], relationships: [], nextId: 1 };
        applyRelationship(graph, registered.id, null);
        return { header: apiHeader(registered), graph };
      }
    }
    // Sample asset (not in the Asset API): its static demo relationships, kept in memory
    const record = findAsset(tag);
    if (!record) return null;
    const cached = GRAPHS.get(record.tag);
    if (cached) return { header: assetHeader(record), graph: cached };
    const detail = await loadAssetDetail(record.tag);
    const graph = buildGraph(record.tag, detail);
    // A failed download is not cached, so a later visit tries again
    if (detail) GRAPHS.set(record.tag, graph);
    return { header: assetHeader(record), graph };
  }

  /** Starts loading every asset opened together (kept while switching between them). */
  private loadSets(tags: string[]): void {
    const key = tags.join(',');
    if (key === this.setsKey) return;
    this.setsKey = key;
    this.setGraphs.clear();
    this.sets = tags.map((tag) => ({ tag, asset: placeholderHeader(tag), graph: null }));
    for (const set of this.sets) {
      const promise = this.resolve(set.tag);
      this.setGraphs.set(set.tag, promise);
      promise.then((loaded) => {
        if (!this.sets.includes(set)) return;
        if (loaded) set.asset = loaded.header;
        set.graph = loaded?.graph ?? { catalog: [], relationships: [], nextId: 1 };
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
    this.relTypes = graph.api ? API_RELATIONSHIP_TYPES : DEMO_REL_TYPES;
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
    if (this.graph.api) {
      this.saveToApi(this.graph, this.graph.api);
      return;
    }
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
      this.selectedId = rel.id;
      this.showToast(`${rel.tag} linked to ${this.asset.tag}.`);
    }
    this.formOpen = false;
  }

  /** Add / edit for a registered asset: the related asset and type are saved to the Asset API. */
  private saveToApi(graph: AssetGraph, link: ApiLink): void {
    const f = this.form;
    if (!f.tag) {
      this.formError = 'Select the related asset.';
      return;
    }
    const target = this.catalog.find((a) => a.tag === f.tag);
    if (!target?.ref || this.saving) return;
    const ref: SubAssetRef = { ...target.ref, relationshipType: f.type };
    const editing = this.relationships.find((r) => r.id === this.editingId)?.sub;

    let request: Observable<AssetRelationshipDto | null>;
    if (editing && link.relationshipId) {
      // Replace the edited row (it may now point at another asset) and keep the rest
      const subs = link.subAssets.map((s) => (subAssetKey(s) === subAssetKey(editing) ? ref : toSubAssetRef(s)));
      request = this.api.update(link.relationshipId, subs);
    } else {
      request = new Observable<AssetRelationshipDto | null>((sub) =>
        this.api
          .save([{ mainAssetRegistrationId: link.mainAssetRegistrationId, subAssets: [ref] }])
          .subscribe({ next: (saved) => (sub.next(saved[0] ?? null), sub.complete()), error: (e) => sub.error(e) }),
      );
    }

    this.saving = true;
    this.formError = '';
    request.subscribe({
      next: (dto) => {
        this.saving = false;
        applyRelationship(graph, link.mainAssetRegistrationId, dto);
        const key = subAssetKey(ref);
        this.selectedId = this.relationships.find((r) => r.sub && subAssetKey(r.sub) === key)?.id ?? this.selectedId;
        this.formOpen = false;
        this.showToast(editing ? 'Relationship updated.' : `${f.tag} linked to ${this.asset.tag}.`);
      },
      error: (err) => {
        this.saving = false;
        this.formError = apiErrorMessage(err);
      },
    });
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
    const graph = this.graph;
    const link = graph.api;
    if (link) {
      if (!link.relationshipId || this.saving) return;
      const removed = new Set(this.relationships.filter((r) => ids.has(r.id) && r.sub).map((r) => subAssetKey(r.sub!)));
      const remaining = link.subAssets.filter((s) => !removed.has(subAssetKey(s))).map((s) => toSubAssetRef(s));
      this.saving = true;
      this.api.replaceSubAssets(link.relationshipId, remaining).subscribe({
        next: (dto) => {
          this.saving = false;
          applyRelationship(graph, link.mainAssetRegistrationId, dto);
          this.checked.clear();
          if (!this.relationships.some((r) => r.id === this.selectedId)) this.selectedId = this.relationships[0]?.id ?? null;
          this.showToast(ids.size === 1 ? 'Relationship removed.' : `${ids.size} relationships removed.`);
          this.removeIds = null;
        },
        error: (err) => {
          this.saving = false;
          this.removeIds = null;
          this.showToast(apiErrorMessage(err));
        },
      });
      return;
    }
    this.relationships = this.relationships.filter((r) => !ids.has(r.id));
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
