import { Component, HostListener, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  LucideAngularModule, AlertTriangle, ArrowRight, Download, Eye, GitBranch, LayoutList, Link2, Maximize, Minus, Network,
  Pencil, Plus, Search, Trash2, Unlink, X,
} from 'lucide-angular';
import { Observable, concatMap, forkJoin, of } from 'rxjs';
import { apiErrorMessage } from '../services/asset-api';
import { FROM_RELATIONSHIPS } from './user-relationships';
import { AssetRelationshipDto, AssetRelationshipService, RegisteredAsset, subAssetKey, toSubAssetRef } from './asset-relationship.service';
import {
  CRITICALITY_ORDER, DiagramEdge, DiagramNode, ImpactHit, NODE_H, NODE_W, RelEnd, RelRow, criticalityRank, impactOf,
  isCriticalDependency, layout, toRows,
} from './relationship-graph';

type View = 'list' | 'diagram';

/** The Add / Edit Relationship form. */
interface RelForm {
  /** Row being edited; null when adding. */
  editing: RelRow | null;
  sourceId: string;
  targetId: string;
  type: string;
}

const PAGE_SIZE = 10;

@Component({
  selector: 'app-asset-relationship-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './asset-relationship-list.html',
  // Shares the asset pages' card, KPI, table, pill and modal styles
  styleUrls: [
    '../asset-status-condition/asset-status-condition.css',
    '../asset-status-condition/asset-status-condition-panels.css',
    './asset-relationship-list.css',
    './relationship-diagram.css',
  ],
})
export class AssetRelationshipList implements OnInit, OnDestroy {
  readonly icons = {
    AlertTriangle, ArrowRight, Download, Eye, GitBranch, LayoutList, Link2, Maximize, Minus, Network, Pencil, Plus, Search,
    Trash2, Unlink, X,
  };
  readonly fromRelationships = FROM_RELATIONSHIPS;
  readonly criticalities = CRITICALITY_ORDER;
  readonly nodeW = NODE_W;
  readonly nodeH = NODE_H;
  readonly criticalityRank = criticalityRank;
  readonly trackRow = (_: number, r: RelRow) => r.id;

  // ===== Data (all from the Asset API) =====
  records: AssetRelationshipDto[] = [];
  assets: RegisteredAsset[] = [];
  private assetById = new Map<string, RegisteredAsset>();
  /** Relationship types the API accepts (GET /api/asset-relationships/types). */
  types: string[] = [];
  rows: RelRow[] = [];
  loading = false;
  loadError = '';

  // ===== Filters =====
  search = '';
  typeFilter = '';
  siteFilter = '';
  criticalityFilter = '';
  /** Only relationships of this asset (as source or target), set from the diagram or the details panel. */
  assetFilter = '';

  view: View = 'list';
  page = 1;
  readonly pageSize = PAGE_SIZE;

  /** Relationship open in the details panel. */
  selected: RelRow | null = null;
  /** Asset whose failure impact is shown (diagram selection or details panel). */
  impactKey = '';

  // ===== Add / Edit =====
  form: RelForm | null = null;
  sourceQuery = '';
  targetQuery = '';
  formError = '';
  saving = false;
  /** Relationship waiting for delete confirmation. */
  deleting: RelRow | null = null;

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;
  private readonly api = inject(AssetRelationshipService);

  ngOnInit(): void {
    this.load();
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
  }

  load(): void {
    this.loading = true;
    this.loadError = '';
    forkJoin({ records: this.api.getAllRelationships(), assets: this.api.getAllAssets(), types: this.api.getTypes() }).subscribe({
      next: ({ records, assets, types }) => {
        this.records = records;
        this.assets = assets;
        this.assetById = new Map(assets.map((a) => [a.id, a]));
        this.types = types;
        this.rows = toRows(records, this.assetById);
        this.loading = false;
        // keep the details panel on the same relationship after a reload
        if (this.selected) this.selected = this.rows.find((r) => r.id === this.selected!.id) ?? null;
        this.relayout();
      },
      error: (err) => {
        this.loading = false;
        this.loadError = apiErrorMessage(err, 'Could not load the asset relationships.');
      },
    });
  }

  // ===== KPIs =====

  get totalRelationships(): number {
    return this.rows.length;
  }

  /** Distinct assets and components that take part in at least one relationship. */
  get connectedAssets(): number {
    const keys = new Set<string>();
    this.rows.forEach((r) => keys.add(r.source.key).add(r.target.key));
    return keys.size;
  }

  get criticalDependencies(): number {
    return this.rows.filter(isCriticalDependency).length;
  }

  /** Registered assets that take part in no relationship yet. */
  get unrelatedAssets(): number {
    const keys = new Set<string>();
    this.rows.forEach((r) => keys.add(r.source.key).add(r.target.key));
    return this.assets.filter((a) => !keys.has(a.id)).length;
  }

  // ===== Filtering =====

  get sites(): string[] {
    return [...new Set(this.rows.map((r) => r.site).filter(Boolean))].sort();
  }

  get filtered(): RelRow[] {
    const q = this.search.trim().toLowerCase();
    const has = (e: RelEnd) => [e.name, e.tag, e.assetId ?? ''].some((v) => v.toLowerCase().includes(q));
    return this.rows
      .filter(
        (r) =>
          (!q || has(r.source) || has(r.target) || r.type.toLowerCase().includes(q)) &&
          (!this.typeFilter || r.type === this.typeFilter) &&
          (!this.siteFilter || r.site === this.siteFilter) &&
          (!this.criticalityFilter || r.criticality === this.criticalityFilter) &&
          (!this.assetFilter || r.source.key === this.assetFilter || r.target.key === this.assetFilter),
      )
      .sort((a, b) => b.updatedAt.localeCompare(a.updatedAt));
  }

  get hasFilters(): boolean {
    return !!(this.search || this.typeFilter || this.siteFilter || this.criticalityFilter || this.assetFilter);
  }

  onFilter(): void {
    this.page = 1;
    this.relayout();
  }

  clearFilters(): void {
    this.search = this.typeFilter = this.siteFilter = this.criticalityFilter = this.assetFilter = '';
    this.onFilter();
  }

  /** Name of the asset the list is limited to. */
  get assetFilterName(): string {
    const r = this.rows.find((x) => x.source.key === this.assetFilter) ?? this.rows.find((x) => x.target.key === this.assetFilter);
    return r ? (r.source.key === this.assetFilter ? r.source.name : r.target.name) : this.assetFilter;
  }

  showAsset(key: string): void {
    this.assetFilter = key;
    this.onFilter();
  }

  // ===== Paging =====

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.filtered.length / this.pageSize));
  }

  get pageRows(): RelRow[] {
    const p = Math.min(this.page, this.pageCount);
    return this.filtered.slice((p - 1) * this.pageSize, p * this.pageSize);
  }

  setPage(p: number): void {
    this.page = Math.min(Math.max(1, p), this.pageCount);
  }

  // ===== Details and impact =====

  select(row: RelRow): void {
    this.selected = row;
    this.impactKey = row.source.key;
  }

  closeDetails(): void {
    this.selected = null;
  }

  /** Assets that may be affected if the impact asset fails (downstream through the relationships). */
  get impact(): ImpactHit[] {
    return this.impactKey ? impactOf(this.impactKey, this.rows) : [];
  }

  get impactName(): string {
    const r = this.rows.find((x) => x.source.key === this.impactKey) ?? this.rows.find((x) => x.target.key === this.impactKey);
    return r ? (r.source.key === this.impactKey ? r.source.name : r.target.name) : '';
  }

  criticalityClass(level: string): string {
    const rank = criticalityRank(level);
    return rank >= 3 ? 'pill--critical' : rank === 2 ? 'pill--serious' : rank === 1 ? 'pill--warn' : rank === 0 ? 'pill--good' : '';
  }

  isCritical(row: RelRow): boolean {
    return isCriticalDependency(row);
  }

  label(e: RelEnd): string {
    return e.tag ? `${e.name} (${e.tag})` : e.name;
  }

  // ===== Diagram =====

  nodes: DiagramNode[] = [];
  edges: DiagramEdge[] = [];
  private graphW = 0;
  private graphH = 0;
  zoom = 1;
  panX = 24;
  panY = 24;
  private drag: { x: number; y: number; panX: number; panY: number } | null = null;

  setView(v: View): void {
    this.view = v;
    if (v === 'diagram') this.relayout();
  }

  /** Re-lays out the diagram for the current filters. */
  private relayout(): void {
    const g = layout(this.filtered);
    this.nodes = g.nodes;
    this.edges = g.edges;
    this.graphW = g.width;
    this.graphH = g.height;
    this.fit();
  }

  get viewBox(): string {
    return `0 0 ${Math.max(640, this.graphW + 48)} ${Math.max(360, this.graphH + 48)}`;
  }

  get transform(): string {
    return `translate(${this.panX} ${this.panY}) scale(${this.zoom})`;
  }

  zoomBy(factor: number): void {
    this.zoom = Math.min(2.5, Math.max(0.3, this.zoom * factor));
  }

  fit(): void {
    this.zoom = 1;
    this.panX = 24;
    this.panY = 24;
  }

  onWheel(event: WheelEvent): void {
    event.preventDefault();
    this.zoomBy(event.deltaY < 0 ? 1.1 : 1 / 1.1);
  }

  startPan(event: PointerEvent): void {
    if ((event.target as Element).closest('.rd-node')) return;
    this.drag = { x: event.clientX, y: event.clientY, panX: this.panX, panY: this.panY };
    (event.currentTarget as Element).setPointerCapture?.(event.pointerId);
  }

  movePan(event: PointerEvent): void {
    if (!this.drag) return;
    this.panX = this.drag.panX + (event.clientX - this.drag.x);
    this.panY = this.drag.panY + (event.clientY - this.drag.y);
  }

  endPan(): void {
    this.drag = null;
  }

  /** Clicking an asset in the diagram shows what its failure may affect. */
  pickNode(node: DiagramNode): void {
    this.impactKey = this.impactKey === node.end.key ? '' : node.end.key;
    this.selected = null;
  }

  /** Keys reached from the picked asset, highlighted in the diagram. */
  get impactKeys(): Set<string> {
    return new Set(this.impact.map((h) => h.end.key));
  }

  edgePath(e: DiagramEdge): string {
    const x1 = e.from.x + NODE_W;
    const y1 = e.from.y + NODE_H / 2;
    const x2 = e.to.x;
    const y2 = e.to.y + NODE_H / 2;
    if (x2 <= x1) {
      // same or earlier column (cycle): loop around underneath
      const y = Math.max(e.from.y, e.to.y) + NODE_H + 18;
      return `M ${x1} ${y1} C ${x1 + 60} ${y}, ${x2 - 60} ${y}, ${x2} ${y2}`;
    }
    const mid = (x1 + x2) / 2;
    return `M ${x1} ${y1} C ${mid} ${y1}, ${mid} ${y2}, ${x2} ${y2}`;
  }

  edgeLabel(e: DiagramEdge): { x: number; y: number } {
    return { x: (e.from.x + NODE_W + e.to.x) / 2, y: (e.from.y + e.to.y + NODE_H) / 2 - 6 };
  }

  short(text: string, max = 22): string {
    return text.length > max ? `${text.slice(0, max - 1)}…` : text;
  }

  openDiagramFor(row: RelRow): void {
    this.assetFilter = row.source.key;
    this.impactKey = row.source.key;
    this.page = 1;
    this.setView('diagram');
  }

  // ===== Add / Edit =====

  openAdd(): void {
    this.form = { editing: null, sourceId: '', targetId: '', type: '' };
    this.sourceQuery = this.targetQuery = '';
    this.formError = '';
  }

  openEdit(row: RelRow): void {
    this.form = { editing: row, sourceId: row.source.key, targetId: row.target.key, type: row.type };
    this.sourceQuery = this.label(row.source);
    this.targetQuery = this.label(row.target);
    this.formError = '';
  }

  closeForm(): void {
    this.form = null;
    this.formError = '';
  }

  /** Assets matching what is typed in a picker (first 50). */
  matches(query: string, exclude: string): RegisteredAsset[] {
    const q = query.trim().toLowerCase();
    return this.assets
      .filter((a) => a.id !== exclude && (!q || [a.name, a.assetTag, a.id].some((v) => v.toLowerCase().includes(q))))
      .slice(0, 50);
  }

  /** Which asset picker list is open. */
  pickerOpen: 'source' | 'target' | '' = '';

  pickSource(a: RegisteredAsset): void {
    this.form!.sourceId = a.id;
    this.sourceQuery = a.assetTag ? `${a.name} (${a.assetTag})` : a.name;
    this.pickerOpen = '';
  }

  pickTarget(a: RegisteredAsset): void {
    this.form!.targetId = a.id;
    this.targetQuery = a.assetTag ? `${a.name} (${a.assetTag})` : a.name;
    this.pickerOpen = '';
  }

  /** Typing in a picker clears its choice until an asset is picked again. */
  typed(which: 'source' | 'target'): void {
    if (which === 'source') this.form!.sourceId = '';
    else this.form!.targetId = '';
    this.pickerOpen = which;
  }

  /** The edited relationship's target is a component (only its type can change). */
  get editingComponent(): boolean {
    return !!this.form?.editing?.target.isComponent;
  }

  private validate(f: RelForm): string {
    if (!f.sourceId) return 'Choose the source asset.';
    if (!f.type) return 'Choose the relationship type.';
    if (!f.targetId) return 'Choose the target asset.';
    if (f.sourceId === f.targetId) return 'An asset cannot be related to itself.';
    if (!this.editingComponent && (!this.assetById.has(f.sourceId) || !this.assetById.has(f.targetId))) return 'Choose registered assets from the list.';
    const existing = this.rows.find((r) => r.source.key === f.sourceId && r.target.key === f.targetId && r.id !== f.editing?.id);
    if (existing) return `${existing.source.name} is already related to ${existing.target.name} (${existing.type}). Edit that relationship instead.`;
    return '';
  }

  save(): void {
    const f = this.form;
    if (!f || this.saving) return;
    this.formError = this.validate(f);
    if (this.formError) return;
    const edit = f.editing;
    let request: Observable<unknown>;
    let message: string;
    if (!edit) {
      request = this.api.save([{ mainAssetRegistrationId: f.sourceId, subAssets: [{ assetRegistrationId: f.targetId, relationshipType: f.type }] }]);
      message = 'Relationship added.';
    } else {
      const record = this.records.find((r) => r.id === edit.recordId);
      if (!record) return void (this.formError = 'This relationship no longer exists. Reload the page.');
      const others = record.subAssets.filter((s) => `${record.id}|${subAssetKey(s)}` !== edit.id).map((s) => toSubAssetRef(s));
      if (f.sourceId === edit.source.key) {
        // same source: replace the link in its record (new target and/or type)
        const link = edit.target.isComponent
          ? toSubAssetRef(edit.sub, f.type)
          : { assetRegistrationId: f.targetId, relationshipType: f.type };
        request = this.api.update(record.id, [...others, link]);
      } else {
        // new source: remove the link from the old record, then add it to the new source's record
        request = this.api
          .replaceSubAssets(record.id, others)
          .pipe(concatMap(() => this.api.save([{ mainAssetRegistrationId: f.sourceId, subAssets: [{ assetRegistrationId: f.targetId, relationshipType: f.type }] }])));
      }
      message = 'Relationship updated.';
    }
    this.run(request, message, () => this.closeForm(), (err) => (this.formError = apiErrorMessage(err)));
  }

  // ===== Delete =====

  askDelete(row: RelRow): void {
    this.deleting = row;
    this.formError = '';
  }

  confirmDelete(): void {
    const row = this.deleting;
    const record = row && this.records.find((r) => r.id === row.recordId);
    if (!row || !record || this.saving) return;
    const remaining = record.subAssets.filter((s) => `${record.id}|${subAssetKey(s)}` !== row.id).map((s) => toSubAssetRef(s));
    this.run(
      this.api.replaceSubAssets(record.id, remaining),
      'Relationship deleted.',
      () => {
        if (this.selected?.id === row.id) this.selected = null;
        this.deleting = null;
      },
      (err) => (this.formError = apiErrorMessage(err)),
    );
  }

  private run(request: Observable<unknown>, success: string, done: () => void, fail: (err: unknown) => void): void {
    this.saving = true;
    request.subscribe({
      next: () => {
        this.saving = false;
        done();
        this.showToast(success);
        this.load();
      },
      error: (err) => {
        this.saving = false;
        fail(err);
      },
    });
  }

  // ===== Export =====

  /** Downloads the filtered relationships as CSV. */
  exportCsv(): void {
    const head = ['Relationship ID', 'Source Asset ID', 'Source Asset', 'Relationship Type', 'Target Asset ID', 'Target Asset', 'Site', 'Criticality', 'Created By', 'Last Updated'];
    const cell = (v: string) => `"${(v ?? '').replace(/"/g, '""')}"`;
    const lines = this.filtered.map((r) =>
      [r.id, r.source.assetId ?? '', this.label(r.source), r.type, r.target.assetId ?? r.target.tag, this.label(r.target), r.site, r.criticality, r.createdBy, r.updatedAt]
        .map(cell)
        .join(','),
    );
    const blob = new Blob([[head.map(cell).join(','), ...lines].join('\r\n')], { type: 'text/csv;charset=utf-8' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = 'asset-relationships.csv';
    a.click();
    URL.revokeObjectURL(a.href);
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.pickerOpen) this.pickerOpen = '';
    else if (this.deleting) this.deleting = null;
    else if (this.form) this.closeForm();
    else this.selected = null;
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 3000);
  }
}
