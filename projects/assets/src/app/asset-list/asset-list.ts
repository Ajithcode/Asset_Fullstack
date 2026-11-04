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
  History,
  Inbox,
  Laptop,
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
import {
  AssetRegistrationFilters, AssetRegistrationListItem, AssetRegistrationListQuery, AssetRegistrationListResult, AssetRegistrationService,
} from '../services/asset-registration/asset-registration';

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
  styleUrl: './asset-list.css',
})
export class AssetList {
  readonly icons = {
    Box, CalendarDays, ChevronDown, ChevronLeft, ChevronRight, ChevronUp, ChevronsUpDown,
    Download, EllipsisVertical, Eye, Funnel, History, Inbox, LayoutGrid, List, Pencil, Plus,
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
    this.refresh();
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

  /** Opens the asset's detail page (Overview tab) for this row. */
  viewAsset(asset: AssetRow): void {
    this.openMenuId = null;
    this.router.navigate(['/assets/overview'], { queryParams: { tag: asset.tag } });
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
