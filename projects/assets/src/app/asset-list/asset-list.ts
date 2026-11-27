import { Component, DestroyRef, HostListener, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import {
  LucideAngularModule,
  AirVent,
  BatteryCharging,
  Box,
  CalendarDays,
  Cctv,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  ChevronUp,
  ChevronsUpDown,
  Cog,
  Download,
  EllipsisVertical,
  Eye,
  Fan,
  Flame,
  Forklift,
  Funnel,
  GitBranch,
  History,
  Inbox,
  Laptop,
  Lightbulb,
  LocateFixed,
  LayoutGrid,
  List,
  Pencil,
  Plus,
  Printer,
  Search,
  SolarPanel,
  Trash2,
  UserRound,
  Wrench,
  X,
  Zap,
} from 'lucide-angular';
import { Observable, Subject, catchError, debounceTime, map, of, switchMap } from 'rxjs';
import { TODAY, toIsoDate } from 'shared-ui';
import { FilterSelect, FilterSelectOption } from '../shared/filter-select/filter-select';
import { apiErrorMessage } from '../services/asset-api';
import {
  AssetRegistrationFilters, AssetRegistrationListItem, AssetRegistrationResult, AssetRegistrationListQuery, AssetRegistrationListResult, AssetRegistrationService,
} from '../services/asset-registration/asset-registration';
import { AssetHierarchyItemDto, AssetHierarchyService } from '../asset-hierarchy/asset-hierarchy.service';
import { TagLinkService } from '../services/tag-link.service';

type SortKey = 'name' | 'tag' | 'category' | 'location' | 'custodian' | 'status' | 'criticality' | 'nextMaintenance';
type MaintenanceDue = '' | 'overdue' | '30' | '90' | 'none';
type AssetIconKey = 'generator' | 'chiller' | 'ups' | 'forklift' | 'fire' | 'ac' | 'laptop' | 'cctv' | 'transformer' | 'solar' | 'box';

/** One row of GET /api/asset-registrations, shaped for the table and grid. */
interface AssetRow {
  /** Record id (Mongo); assetRegistrationId is the business id such as "AR000003". */
  id: string;
  registrationId: string;
  name: string;
  tag: string;
  imageUrl: string | null;
  category: string;
  type: string;
  site: string;
  /** Location below the site, e.g. "Street One > Third Right > Second Colony". */
  area: string;
  locationPath: string;
  custodian: string;
  department: string;
  status: string;
  criticality: string;
  manufacturer: string;
  /** yyyy-mm-dd, or null when not scheduled. */
  nextMaintenance: string | null;
  icon: AssetIconKey;
}

/** A child of an asset in Asset Hierarchy, shown under that asset's name. */
interface SubItem {
  id: string;
  name: string;
  tag: string;
  /** "Asset", "System", "Floor" … (the hierarchy item kind). */
  kind: string;
  type: string;
  status: string;
  /** Items under this child in the hierarchy. */
  childCount: number;
}

interface SelectOption {
  value: string;
  label: string;
}

/** API sortBy names for each sortable column. */
const SORT_FIELDS: Record<SortKey, string> = {
  name: 'assetName',
  tag: 'assetTag',
  category: 'category',
  location: 'location',
  custodian: 'custodian',
  status: 'status',
  criticality: 'criticality',
  nextMaintenance: 'nextMaintenanceDate',
};

/** Picks a row icon from the asset type / category name. */
const ICON_KEYWORDS: [RegExp, AssetIconKey][] = [
  [/generator/i, 'generator'],
  [/transformer|switchgear|panel/i, 'transformer'],
  [/chiller|cooling|hvac|ahu|air handling/i, 'chiller'],
  [/\bups\b|battery/i, 'ups'],
  [/forklift|truck|vehicle/i, 'forklift'],
  [/fire/i, 'fire'],
  [/air ?con|\bac\b/i, 'ac'],
  [/laptop|computer|server|it /i, 'laptop'],
  [/cctv|camera/i, 'cctv'],
  [/solar/i, 'solar'],
];

function toRow(item: AssetRegistrationListItem): AssetRow {
  const below = [item.buildingName, item.floorName, item.zoneName, item.roomName].filter(Boolean).join(' > ');
  const kind = `${item.assetType ?? ''} ${item.category ?? ''}`;
  return {
    id: item.id,
    registrationId: item.assetRegistrationId,
    name: item.assetName ?? '',
    tag: item.assetTag ?? '',
    imageUrl: item.imageUrl,
    category: item.category ?? '',
    type: item.assetType ?? '',
    site: item.siteName ?? '',
    area: below,
    locationPath: item.locationPath ?? '',
    custodian: item.custodianName ?? '',
    department: item.department ?? '',
    status: item.status ?? '',
    criticality: item.criticality ?? '',
    manufacturer: item.manufacturer ?? '',
    nextMaintenance: item.nextMaintenanceDate ? item.nextMaintenanceDate.slice(0, 10) : null,
    icon: ICON_KEYWORDS.find(([re]) => re.test(kind))?.[1] ?? 'box',
  };
}

interface Filters {
  category: string;
  type: string;
  status: string;
  location: string;
  department: string;
  custodian: string;
  criticality: string;
  manufacturer: string;
  maintenanceDue: MaintenanceDue;
}

interface StatusCard {
  label: string;
  /** Status filter value the card applies; 'Other' covers every status without its own card. */
  value: string;
  dot: string;
  count: number;
}

const EMPTY_FILTERS: Filters = {
  category: '',
  type: '',
  status: '',
  location: '',
  department: '',
  custodian: '',
  criticality: '',
  manufacturer: '',
  maintenanceDue: '',
};

const MAIN_STATUSES = ['Active', 'Under Maintenance', 'In Repair', 'Disposed'];
const CRITICALITY_RANK: Record<string, number> = { Critical: 0, High: 1, Medium: 2, Low: 3 };
const AVATAR_COLORS = ['#6d4aff', '#2563eb', '#0891b2', '#db2777', '#7c3aed', '#059669', '#ea580c', '#4f46e5'];

@Component({
  selector: 'app-asset-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule, FilterSelect],
  templateUrl: './asset-list.html',
  styleUrls: ['./asset-list.css', './asset-list-drafts.css'],
})
export class AssetList {
  readonly icons = {
    Box, CalendarDays, ChevronDown, ChevronLeft, ChevronRight, ChevronUp, ChevronsUpDown,
    Download, EllipsisVertical, Eye, Funnel, GitBranch, History, Inbox, LayoutGrid, Lightbulb, List, LocateFixed, Pencil, Plus,
    Printer, Search, Trash2, UserRound, Wrench, X,
  };

  readonly assetIcons = {
    generator: Zap,
    chiller: Fan,
    ups: BatteryCharging,
    forklift: Forklift,
    fire: Flame,
    ac: AirVent,
    laptop: Laptop,
    cctv: Cctv,
    transformer: Cog,
    solar: SolarPanel,
    box: Box,
  };

  private readonly api = inject(AssetRegistrationService);
  private readonly hierarchyApi = inject(AssetHierarchyService);
  private readonly tagLinks = inject(TagLinkService);
  private readonly destroyRef = inject(DestroyRef);

  // ===== Filter options (GET /api/asset-registrations/filters) =====

  private filterData?: AssetRegistrationFilters;
  categoryOptions: string[] = [];
  statusOptions: string[] = [];
  /** value is the locationId filter, label the full path. */
  locationOptions: SelectOption[] = [];
  departmentOptions: string[] = [];
  /** value is custodianUserId. */
  custodianOptions: SelectOption[] = [];
  criticalityOptions: string[] = [];
  manufacturerOptions: string[] = [];
  readonly pageSizeOptions = [5, 10, 20];
  /** Status filter choices: the API's statuses plus "Other". */
  get statusFilterOptions(): string[] {
    return this.statusFilterCache.source === this.statusOptions
      ? this.statusFilterCache.list
      : (this.statusFilterCache = { source: this.statusOptions, list: [...this.statusOptions, 'Other'] }).list;
  }
  private statusFilterCache: { source: readonly string[] | null; list: string[] } = { source: null, list: [] };

  readonly maintenanceOptions: FilterSelectOption[] = [
    { value: 'overdue', label: 'Overdue' },
    { value: '30', label: 'Due in next 30 days' },
    { value: '90', label: 'Due in next 90 days' },
    { value: 'none', label: 'Not scheduled' },
  ];

  search = '';
  /** Values bound to the filter controls; they take effect on "Apply Filters". */
  draft: Filters = { ...EMPTY_FILTERS };
  applied: Filters = { ...EMPTY_FILTERS };
  showMoreFilters = false;

  sortKey: SortKey | null = null;
  sortDir: 'asc' | 'desc' = 'asc';

  view: 'list' | 'grid' = 'list';
  page = 1;
  pageSize = 10;

  /** Selected rows by id; kept across pages so "Export selected" has their data. */
  readonly selected = new Map<string, AssetRow>();
  openMenuId: string | null = null;

  statusCards: StatusCard[] = this.buildStatusCards();
  /** Assets matching every filter except status; shown as "Total Assets". */
  totalAssets = 0;
  /** Assets matching every filter, across all pages. */
  totalCount = 0;
  pageRows: AssetRow[] = [];
  loading = false;

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  private readonly reload$ = new Subject<void>();
  private readonly searchInput$ = new Subject<void>();

  constructor(private readonly router: Router) {
    this.reload$
      .pipe(
        switchMap(() => this.fetchPage()),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result) => this.showPage(result));
    this.searchInput$.pipe(debounceTime(300), takeUntilDestroyed(this.destroyRef)).subscribe(() => this.refresh());
    this.loadFilterOptions();
    this.loadDrafts();
    this.loadSubAssets();
    this.refresh();
  }

  // ===== Drafts (GET /api/asset-registrations/drafts: the signed-in user's saved registration drafts) =====

  drafts: AssetRegistrationResult[] = [];
  draftsOpen = false;
  deletingDraftId: string | null = null;

  private loadDrafts(): void {
    this.api.getDrafts().subscribe({
      next: (drafts) => (this.drafts = drafts ?? []),
      error: () => (this.drafts = []),
    });
  }

  toggleDrafts(): void {
    this.draftsOpen = !this.draftsOpen;
    if (this.draftsOpen) this.loadDrafts();
  }

  /** Opens the registration wizard on this draft (/assets/registration?draft=AR000012). */
  continueDraft(draft: AssetRegistrationResult): void {
    this.router.navigate(['/assets/registration'], { queryParams: { draft: draft.assetRegistrationId } });
  }

  /** DELETE /api/asset-registrations/drafts/{id}, after confirmation. */
  deleteDraft(draft: AssetRegistrationResult): void {
    const name = draft.basicInformation?.assetName || draft.assetRegistrationId;
    if (!confirm(`Delete draft ${draft.assetRegistrationId} (${name})? This cannot be undone.`)) return;
    this.deletingDraftId = draft.assetRegistrationId;
    this.api.deleteDraft(draft.assetRegistrationId).subscribe({
      next: () => {
        this.deletingDraftId = null;
        this.drafts = this.drafts.filter((d) => d.assetRegistrationId !== draft.assetRegistrationId);
      },
      error: () => {
        this.deletingDraftId = null;
        this.loadDrafts();
      },
    });
  }

  private loadFilterOptions(): void {
    this.api.getFilters().subscribe({
      next: (f) => {
        this.filterData = f;
        const sorted = (values: string[] = []) => [...new Set(values)].sort((a, b) => a.localeCompare(b));
        this.categoryOptions = sorted(f.categories);
        this.statusOptions = sorted(f.statuses);
        this.locationOptions = (f.locations ?? []).map((l) => ({ value: l.id, label: l.path || l.name }));
        this.departmentOptions = sorted(f.departments);
        const custodians = new Map<string, string>();
        for (const c of f.custodians ?? []) if (c.userId) custodians.set(c.userId, c.name || c.userId);
        this.custodianOptions = [...custodians].map(([value, label]) => ({ value, label })).sort((a, b) => a.label.localeCompare(b.label));
        this.criticalityOptions = [...new Set(f.criticalities ?? [])].sort((a, b) => (CRITICALITY_RANK[a] ?? 9) - (CRITICALITY_RANK[b] ?? 9));
        this.manufacturerOptions = sorted(f.manufacturers);
      },
      error: () => this.showToast('Could not load the filter options.'),
    });
  }

  // ===== Filters =====

  /** Types offered depend on the chosen category. */
  get typeOptions(): string[] {
    const types = (this.filterData?.assetTypes ?? []).filter((t) => !this.draft.category || t.category === this.draft.category);
    return [...new Set(types.map((t) => t.assetType))].sort((a, b) => a.localeCompare(b));
  }

  onCategoryChange(): void {
    if (this.draft.type && !this.typeOptions.includes(this.draft.type)) this.draft.type = '';
  }

  get hasPendingChanges(): boolean {
    return (Object.keys(this.draft) as (keyof Filters)[]).some((k) => this.draft[k] !== this.applied[k]);
  }

  get activeFilterCount(): number {
    return (Object.keys(this.applied) as (keyof Filters)[]).filter((k) => this.applied[k] !== '').length;
  }

  get moreFiltersCount(): number {
    return this.applied.maintenanceDue ? 1 : 0;
  }

  applyFilters(): void {
    this.applied = { ...this.draft };
    this.page = 1;
    this.refresh();
  }

  resetFilters(): void {
    this.search = '';
    this.draft = { ...EMPTY_FILTERS };
    this.applied = { ...EMPTY_FILTERS };
    this.page = 1;
    this.refresh();
  }

  /** Search runs on the server, 300 ms after typing stops. */
  onSearchChange(): void {
    this.page = 1;
    this.searchInput$.next();
  }

  clearSearch(): void {
    this.search = '';
    this.page = 1;
    this.refresh();
  }

  /** Status cards act as a quick filter; clicking the active card clears it. */
  filterByStatusCard(value: string): void {
    const next = this.applied.status === value ? '' : value;
    this.draft.status = next;
    this.applied = { ...this.applied, status: next };
    this.page = 1;
    this.refresh();
  }

  private matchesStatus(asset: AssetRow, status: string): boolean {
    if (!status) return true;
    if (status === 'Other') return !MAIN_STATUSES.includes(asset.status);
    return asset.status === status;
  }

  private matchesMaintenanceDue(asset: AssetRow, due: MaintenanceDue): boolean {
    if (!due) return true;
    if (due === 'none') return !asset.nextMaintenance;
    if (!asset.nextMaintenance) return false;
    const days = this.daysUntil(asset.nextMaintenance);
    if (due === 'overdue') return days < 0;
    return days >= 0 && days <= Number(due);
  }

  private buildStatusCards(summary?: AssetRegistrationListResult['summary']): StatusCard[] {
    return [
      { label: 'Active', value: 'Active', dot: '#22a95b', count: summary?.active ?? 0 },
      { label: 'Under Maintenance', value: 'Under Maintenance', dot: '#f59e0b', count: summary?.underMaintenance ?? 0 },
      { label: 'In Repair', value: 'In Repair', dot: '#e11d48', count: summary?.inRepair ?? 0 },
      { label: 'Disposed', value: 'Disposed', dot: '#94a3b8', count: summary?.disposed ?? 0 },
      { label: 'Other', value: 'Other', dot: '#4f2bd8', count: summary?.other ?? 0 },
    ];
  }

  // ===== Sorting (server side) =====

  sortBy(key: SortKey): void {
    if (this.sortKey === key) {
      if (this.sortDir === 'asc') {
        this.sortDir = 'desc';
      } else {
        this.sortKey = null;
      }
    } else {
      this.sortKey = key;
      this.sortDir = 'asc';
    }
    this.page = 1;
    this.refresh();
  }

  sortIcon(key: SortKey) {
    if (this.sortKey !== key) return this.icons.ChevronsUpDown;
    return this.sortDir === 'asc' ? this.icons.ChevronUp : this.icons.ChevronDown;
  }

  // ===== Loading (GET /api/asset-registrations) =====

  private refresh(): void {
    this.reload$.next();
  }

  /** Search, filters and sort as API query parameters (status and maintenance due are added by the caller). */
  private baseQuery(): AssetRegistrationListQuery {
    const f = this.applied;
    return {
      search: this.search.trim() || undefined,
      category: f.category || undefined,
      assetType: f.type || undefined,
      locationId: f.location || undefined,
      department: f.department || undefined,
      custodianUserId: f.custodian || undefined,
      criticality: f.criticality || undefined,
      manufacturer: f.manufacturer || undefined,
      sortBy: this.sortKey ? SORT_FIELDS[this.sortKey] : undefined,
      sortDirection: this.sortKey ? this.sortDir : undefined,
    };
  }

  /**
   * The API has no "Other" status or maintenance-due filter. Those load every matching asset (page by page) and
   * filter and page here; everything else is filtered, sorted and paged by the API.
   */
  private get filtersLocally(): boolean {
    return this.applied.status === 'Other' || !!this.applied.maintenanceDue;
  }

  private fetchPage(): Observable<{ rows: AssetRow[]; total: number; all: number; summary?: AssetRegistrationListResult['summary'] } | null> {
    this.loading = true;
    const query = this.baseQuery();
    const request$ = this.filtersLocally
      ? this.api.getAllMatching(query).pipe(
          map((items) => {
            const all = items.map(toRow);
            const rows = all.filter((a) => this.matchesStatus(a, this.applied.status) && this.matchesMaintenanceDue(a, this.applied.maintenanceDue));
            const count = (fn: (a: AssetRow) => boolean) => all.filter(fn).length;
            const summary = {
              total: all.length,
              active: count((a) => a.status === 'Active'),
              underMaintenance: count((a) => a.status === 'Under Maintenance'),
              inRepair: count((a) => a.status === 'In Repair'),
              disposed: count((a) => a.status === 'Disposed'),
              other: count((a) => !MAIN_STATUSES.includes(a.status)),
            };
            const start = (this.page - 1) * this.pageSize;
            return { rows: rows.slice(start, start + this.pageSize), total: rows.length, all: all.length, summary };
          }),
        )
      : this.api
          .getAll({ ...query, status: this.applied.status || undefined, page: this.page, pageSize: this.pageSize })
          .pipe(map((res) => ({ rows: res.items.map(toRow), total: res.totalCount, all: res.summary?.total ?? res.totalCount, summary: res.summary })));
    return request$.pipe(
      catchError(() => {
        this.showToast('Could not load assets. Please try again.');
        return of(null);
      }),
    );
  }

  private showPage(result: { rows: AssetRow[]; total: number; all: number; summary?: AssetRegistrationListResult['summary'] } | null): void {
    this.loading = false;
    if (!result) return;
    this.pageRows = result.rows;
    this.totalCount = result.total;
    this.totalAssets = result.all;
    this.statusCards = this.buildStatusCards(result.summary);
    // A filter shrank the result below the current page: go to the last page that exists
    if (this.page > this.totalPages) {
      this.page = this.totalPages;
      this.refresh();
    }
  }

  // ===== Pagination =====

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  get rangeStart(): number {
    return this.totalCount ? (this.page - 1) * this.pageSize + 1 : 0;
  }

  get rangeEnd(): number {
    return Math.min(this.page * this.pageSize, this.totalCount);
  }

  /** Page numbers to render, with null standing in for an ellipsis. */
  get pageList(): (number | null)[] {
    const total = this.totalPages;
    if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);
    if (this.page <= 4) return [1, 2, 3, 4, 5, null, total];
    if (this.page >= total - 3) return [1, null, total - 4, total - 3, total - 2, total - 1, total];
    return [1, null, this.page - 1, this.page, this.page + 1, null, total];
  }

  goToPage(p: number): void {
    if (p < 1 || p > this.totalPages || p === this.page) return;
    this.page = p;
    this.refresh();
  }

  onPageSizeChange(): void {
    this.page = 1;
    this.refresh();
  }

  // ===== Selection =====

  get allOnPageSelected(): boolean {
    return this.pageRows.length > 0 && this.pageRows.every((r) => this.selected.has(r.id));
  }

  get someOnPageSelected(): boolean {
    return !this.allOnPageSelected && this.pageRows.some((r) => this.selected.has(r.id));
  }

  toggleAllOnPage(): void {
    const selectAll = !this.allOnPageSelected;
    for (const r of this.pageRows) {
      if (selectAll) this.selected.set(r.id, r);
      else this.selected.delete(r.id);
    }
  }

  toggleRow(row: AssetRow): void {
    if (this.selected.has(row.id)) this.selected.delete(row.id);
    else this.selected.set(row.id, row);
  }

  clearSelection(): void {
    this.selected.clear();
  }

  // ===== Row actions =====

  toggleMenu(id: string, event: MouseEvent): void {
    event.stopPropagation();
    this.openMenuId = this.openMenuId === id ? null : id;
  }

  @HostListener('document:click')
  closeMenu(): void {
    this.openMenuId = null;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.openMenuId = null;
  }

  registerAsset(): void {
    this.router.navigateByUrl('/assets/registration');
  }

  openAssetModule(path: string, asset: AssetRow): void {
    this.openMenuId = null;
    this.router.navigate([path], { queryParams: { tag: asset.tag } });
  }

  /** Opens Asset Registration in edit mode for this asset (GET / PUT /api/asset-registrations/{id}). */
  editAsset(asset: AssetRow): void {
    this.openMenuId = null;
    this.router.navigate(['/assets/registration'], { queryParams: { id: asset.registrationId, tag: asset.tag } });
  }

  /** Registration id of the asset being deleted. */
  deletingId: string | null = null;

  /** DELETE /api/asset-registrations/{id} (soft delete), after confirmation; then reloads the list and filters. */
  deleteAsset(asset: AssetRow): void {
    this.openMenuId = null;
    if (!window.confirm(`Delete ${asset.name} (${asset.tag})? It will be removed from the asset list.`)) return;
    this.deletingId = asset.registrationId;
    this.api.delete(asset.registrationId).subscribe({
      next: () => {
        this.deletingId = null;
        this.selected.delete(asset.id);
        this.showToast(`${asset.name} deleted.`);
        this.loadFilterOptions();
        this.loadSubAssets();
        this.refresh();
      },
      error: (err) => {
        this.deletingId = null;
        this.showToast(apiErrorMessage(err, 'Could not delete the asset. Please try again.'));
      },
    });
  }

  /** Opens the asset's detail page (Overview tab) for this row. */
  viewAsset(asset: AssetRow): void {
    this.openMenuId = null;
    // id: the overview reads GET /api/asset-registrations/{id}; tag: the other detail tabs find the asset by tag
    this.router.navigate(['/assets/overview'], { queryParams: { id: asset.registrationId, tag: asset.tag } });
  }

  // ===== Sub assets (GET /api/asset-hierarchy: the items created under an asset in Asset Hierarchy) =====

  /** Children per asset tag (upper case): the hierarchy items directly under the hierarchy asset with that tag. */
  private subAssetsByTag = new Map<string, SubItem[]>();
  /** Parent asset name per child asset tag (upper case), for the "Sub asset of" note. */
  private parentByTag = new Map<string, string>();
  /** Rows whose sub assets are shown (closed by default). */
  readonly expanded = new Set<string>();
  /** Search text per expanded row. */
  subSearch: Record<string, string> = {};

  private loadSubAssets(): void {
    this.hierarchyApi.getItems().subscribe({
      next: (items) => {
        const byId = new Map(items.map((i) => [i.id, i]));
        const byTag = new Map<string, SubItem[]>();
        const parents = new Map<string, string>();
        const kindLabel = (k: string | null) => (k ? k.charAt(0).toUpperCase() + k.slice(1).toLowerCase() : 'Item');
        const isAsset = (i: AssetHierarchyItemDto) => (i.itemKind ?? '').toLowerCase() === 'asset';
        for (const child of items) {
          const parent = child.parentId ? byId.get(child.parentId) : undefined;
          const parentTag = parent && isAsset(parent) ? (parent.assetTag ?? '').trim().toUpperCase() : '';
          if (!parent || !parentTag) continue;
          byTag.set(parentTag, [
            ...(byTag.get(parentTag) ?? []),
            {
              id: child.id,
              name: child.name,
              tag: child.assetTag ?? '',
              kind: kindLabel(child.itemKind),
              type: child.assetTypeName ?? '',
              status: child.status ?? '',
              childCount: child.childCount ?? 0,
            },
          ]);
          const childTag = (child.assetTag ?? '').trim().toUpperCase();
          if (childTag && isAsset(child)) parents.set(childTag, parent.name);
        }
        byTag.forEach((list) => list.sort((x, y) => x.name.localeCompare(y.name)));
        this.subAssetsByTag = byTag;
        this.parentByTag = parents;
      },
      // the list still works without them; the dropdowns just don't appear
      error: () => {
        this.subAssetsByTag = new Map();
        this.parentByTag = new Map();
      },
    });
  }

  subAssetsOf(row: AssetRow): SubItem[] {
    return this.subAssetsByTag.get(row.tag.trim().toUpperCase()) ?? [];
  }

  /** The asset this row sits under in Asset Hierarchy, e.g. "laptop". */
  mainAssetsOf(row: AssetRow): string {
    return this.parentByTag.get(row.tag.trim().toUpperCase()) ?? '';
  }

  toggleSubAssets(row: AssetRow): void {
    if (this.expanded.has(row.id)) this.expanded.delete(row.id);
    else this.expanded.add(row.id);
  }

  /** The row's sub assets matching its search (name, tag, kind, type or status). */
  filteredSubAssets(row: AssetRow): SubItem[] {
    const subs = this.subAssetsOf(row);
    const q = (this.subSearch[row.id] ?? '').trim().toLowerCase();
    if (!q) return subs;
    return subs.filter((s) => [s.name, s.tag, s.kind, s.type, s.status].some((v) => v.toLowerCase().includes(q)));
  }

  /** Sub assets live in Asset Hierarchy: open it there. */
  openSubAsset(): void {
    this.router.navigateByUrl('/assets/hierarchy');
  }

  trackSub(_: number, sub: SubItem): string {
    return sub.id;
  }

  // ===== Locate a sub asset (same two actions as Real-Time Location › Locate asset) =====

  /** Sub asset whose tag link is being checked. */
  checkingSubId: string | null = null;
  /** Asset with no tag linked (and, from a sub asset, which one asked): shown in the "not linked" popup. */
  noTagFor: { name: string; tag: string; sub: string } | null = null;
  /** Row whose tag link is being checked from its Actions menu. */
  checkingRowId: string | null = null;

  /**
   * Bulb (blink LED / buzzer) or location (live tracking) button on a sub asset. A sub asset has no tag of its own: it
   * moves with its parent, so this locates the PARENT asset (the row it is listed under, e.g. laptop for mouse wire).
   * Checks Tag Management for a tag linked to the parent first; with one, opens Real-Time Location on the parent and
   * action, otherwise explains that the parent has no tag.
   */
  locateSub(sub: SubItem, main: AssetRow, action: 'signal' | 'live'): void {
    if (this.checkingSubId || this.checkingRowId) return;
    this.checkingSubId = sub.id;
    this.locateAsset(main, action, sub.name, () => (this.checkingSubId = null));
  }

  /** Actions menu › Blink LED / Buzzer or Live Tracking on the asset itself. */
  locateRow(a: AssetRow, action: 'signal' | 'live'): void {
    this.openMenuId = null;
    if (this.checkingSubId || this.checkingRowId) return;
    this.checkingRowId = a.id;
    this.locateAsset(a, action, '', () => (this.checkingRowId = null));
  }

  /**
   * Checks Tag Management for a tag linked to the asset; with one, opens Real-Time Location on it (bulb: the LED /
   * buzzer form, live: live tracking), otherwise shows the "No tag linked" popup.
   */
  private locateAsset(asset: AssetRow, action: 'signal' | 'live', fromSub: string, done: () => void): void {
    this.tagLinks.linkedTags().subscribe({
      next: (tags) => {
        done();
        const link =
          tags.find((t) => t.assetRegistrationId === asset.registrationId) ?? TagLinkService.findFor(tags, { tag: asset.tag, name: asset.name });
        if (!link?.assetRegistrationId) {
          this.noTagFor = { name: asset.name, tag: asset.tag, sub: fromSub };
          return;
        }
        this.router.navigate(['/tracking-iot/real-time-location'], {
          queryParams: { asset: link.assetRegistrationId, tag: link.tagId, action },
        });
      },
      error: () => {
        done();
        this.showToast('Could not check the tag link. Please try again.');
      },
    });
  }

  openTagManagement(): void {
    this.noTagFor = null;
    this.router.navigateByUrl('/tracking-iot/tag-ids');
  }

  // ===== Export / print =====

  /** Exports the selected rows, or else every asset matching the search and filters (all pages). */
  exportCsv(): void {
    if (this.selected.size) {
      this.downloadCsv([...this.selected.values()]);
      return;
    }
    this.api.getAllMatching({ ...this.baseQuery(), status: this.applied.status && this.applied.status !== 'Other' ? this.applied.status : undefined }).subscribe({
      next: (items) =>
        this.downloadCsv(
          items.map(toRow).filter((a) => this.matchesStatus(a, this.applied.status) && this.matchesMaintenanceDue(a, this.applied.maintenanceDue)),
        ),
      error: () => this.showToast('Could not export assets. Please try again.'),
    });
  }

  private downloadCsv(rows: AssetRow[]): void {
    const header = ['Asset Name', 'Asset Tag', 'Category', 'Type', 'Site', 'Area', 'Custodian', 'Department', 'Status', 'Criticality', 'Manufacturer', 'Next Maintenance'];
    const escape = (v: string) => (/[",\n]/.test(v) ? `"${v.replace(/"/g, '""')}"` : v);
    const lines = [header, ...rows.map((a) => [a.name, a.tag, a.category, a.type, a.site, a.area, a.custodian, a.department, a.status, a.criticality, a.manufacturer, a.nextMaintenance ?? ''])]
      .map((cols) => cols.map(escape).join(','));

    const blob = new Blob([lines.join('\n')], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `asset-list-${toIsoDate(new Date())}.csv`;
    link.click();
    URL.revokeObjectURL(url);
    this.showToast(`Exported ${rows.length} asset${rows.length === 1 ? '' : 's'}.`);
  }

  print(): void {
    window.print();
  }

  // ===== Display helpers =====

  initials(name: string): string {
    return name
      .split(/\s+/)
      .slice(0, 2)
      .map((p) => p[0]?.toUpperCase() ?? '')
      .join('');
  }

  avatarColor(name: string): string {
    let h = 0;
    for (const ch of name) h = (h * 31 + ch.charCodeAt(0)) >>> 0;
    return AVATAR_COLORS[h % AVATAR_COLORS.length];
  }

  statusClass(status: string): string {
    return 'status--' + status.toLowerCase().replace(/\s+/g, '-');
  }

  criticalityClass(c: string): string {
    return 'crit--' + c.toLowerCase();
  }

  daysUntil(iso: string): number {
    const [y, m, d] = iso.split('-').map(Number);
    return Math.round((new Date(y, m - 1, d).getTime() - TODAY.getTime()) / 86_400_000);
  }

  isOverdue(asset: AssetRow): boolean {
    return !!asset.nextMaintenance && this.daysUntil(asset.nextMaintenance) < 0;
  }

  isDueSoon(asset: AssetRow): boolean {
    if (!asset.nextMaintenance) return false;
    const days = this.daysUntil(asset.nextMaintenance);
    return days >= 0 && days <= 14;
  }

  trackById(_: number, row: AssetRow): string {
    return row.id;
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 2600);
  }
}
