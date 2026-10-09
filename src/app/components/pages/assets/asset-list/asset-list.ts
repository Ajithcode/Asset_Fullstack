import { Component, HostListener } from '@angular/core';
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

type AssetStatus = 'Active' | 'Under Maintenance' | 'In Repair' | 'Disposed' | 'In Storage' | 'Checked Out';
type Criticality = 'Critical' | 'High' | 'Medium' | 'Low';
type SortKey = 'name' | 'tag' | 'category' | 'location' | 'custodian' | 'status' | 'criticality' | 'nextMaintenance';
type MaintenanceDue = '' | 'overdue' | '30' | '90' | 'none';

interface AssetRow {
  id: number;
  name: string;
  tag: string;
  category: string;
  type: string;
  site: string;
  area: string;
  custodian: string;
  department: string;
  status: AssetStatus;
  criticality: Criticality;
  manufacturer: string;
  /** ISO date (yyyy-mm-dd), or null when no maintenance is scheduled. */
  nextMaintenance: string | null;
  icon: keyof AssetList['assetIcons'];
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

interface AssetTemplate {
  name: string;
  tagPrefix: string;
  category: string;
  type: string;
  manufacturer: string;
  icon: AssetRow['icon'];
  criticality: Criticality;
}

const NOW = new Date();
const TODAY = new Date(NOW.getFullYear(), NOW.getMonth(), NOW.getDate());

const TEMPLATES: AssetTemplate[] = [
  { name: 'Diesel Generator 250 kVA', tagPrefix: 'GEN', category: 'Power Generation', type: 'Generator', manufacturer: 'Caterpillar', icon: 'generator', criticality: 'Critical' },
  { name: 'HVAC Chiller', tagPrefix: 'HVAC', category: 'HVAC', type: 'Chiller', manufacturer: 'Carrier', icon: 'chiller', criticality: 'High' },
  { name: 'UPS 100 kVA', tagPrefix: 'UPS', category: 'Electrical', type: 'UPS', manufacturer: 'Schneider Electric', icon: 'ups', criticality: 'Critical' },
  { name: 'Forklift - Electric', tagPrefix: 'FLT', category: 'Material Handling', type: 'Forklift', manufacturer: 'Toyota', icon: 'forklift', criticality: 'High' },
  { name: 'Fire Pump', tagPrefix: 'FP', category: 'Safety & Security', type: 'Fire System', manufacturer: 'Grundfos', icon: 'fire', criticality: 'Critical' },
  { name: 'Split AC - 5 Ton', tagPrefix: 'AC', category: 'HVAC', type: 'Air Conditioner', manufacturer: 'Daikin', icon: 'ac', criticality: 'Medium' },
  { name: 'Laptop - Dell Latitude', tagPrefix: 'IT', category: 'IT Equipment', type: 'Laptop', manufacturer: 'Dell', icon: 'laptop', criticality: 'Medium' },
  { name: 'CCTV Camera - Dome', tagPrefix: 'CCTV', category: 'Security', type: 'CCTV', manufacturer: 'Hikvision', icon: 'cctv', criticality: 'Medium' },
  { name: 'Transformer 1 MVA', tagPrefix: 'TRF', category: 'Electrical', type: 'Transformer', manufacturer: 'ABB', icon: 'transformer', criticality: 'Critical' },
  { name: 'Solar Panel - 550W', tagPrefix: 'SOL', category: 'Renewable Energy', type: 'Solar Panel', manufacturer: 'Jinko Solar', icon: 'solar', criticality: 'Low' },
  { name: 'Gas Generator 500 kVA', tagPrefix: 'GEN', category: 'Power Generation', type: 'Generator', manufacturer: 'Cummins', icon: 'generator', criticality: 'High' },
  { name: 'Air Handling Unit', tagPrefix: 'HVAC', category: 'HVAC', type: 'AHU', manufacturer: 'Trane', icon: 'chiller', criticality: 'High' },
  { name: 'Laptop - HP EliteBook', tagPrefix: 'IT', category: 'IT Equipment', type: 'Laptop', manufacturer: 'HP', icon: 'laptop', criticality: 'Low' },
  { name: 'CCTV Camera - Bullet', tagPrefix: 'CCTV', category: 'Security', type: 'CCTV', manufacturer: 'Axis', icon: 'cctv', criticality: 'Medium' },
  { name: 'Pallet Jack - Electric', tagPrefix: 'FLT', category: 'Material Handling', type: 'Pallet Jack', manufacturer: 'Jungheinrich', icon: 'forklift', criticality: 'Low' },
  { name: 'Fire Alarm Panel', tagPrefix: 'FP', category: 'Safety & Security', type: 'Fire System', manufacturer: 'Honeywell', icon: 'fire', criticality: 'High' },
];

const LOCATIONS: [string, string][] = [
  ['Head Office', 'Building A - ER-101'],
  ['Head Office', 'Building B - Roof'],
  ['Data Center', 'Building A - DC Room'],
  ['Warehouse', 'Main Warehouse'],
  ['Head Office', 'Building A - Basement'],
  ['Building C', '2nd Floor'],
  ['Head Office', 'IT Department'],
  ['Building A', 'Ground Floor'],
  ['Main Substation', 'Outdoor'],
  ['Building B', 'Roof'],
];

const CUSTODIANS: [string, string][] = [
  ['Ahmed Basim', 'Facilities'],
  ['Sara Ahmed', 'Facilities'],
  ['Mohan Kumar', 'IT'],
  ['Rashid Ali', 'Logistics'],
  ['Noura Saeed', 'Safety'],
  ['Faisal Ahmed', 'Facilities'],
  ['Sarah James', 'IT'],
  ['Ibrahim Khan', 'Security'],
  ['Hamad Lateef', 'Engineering'],
  ['Ramesh Kumar', 'Engineering'],
];

const STATUS_CYCLE: AssetStatus[] = [
  'Active', 'Active', 'Active', 'Active', 'Under Maintenance', 'Active', 'Active',
  'In Repair', 'Active', 'In Storage', 'Active', 'Disposed', 'Active', 'Checked Out',
];

/** The first ten rows match the design mock; the rest are generated deterministically from the templates. */
function buildMockAssets(): AssetRow[] {
  const seed: Omit<AssetRow, 'id' | 'name' | 'category' | 'type' | 'manufacturer' | 'icon' | 'site' | 'area' | 'custodian' | 'department'>[] = [
    { tag: 'GEN-00045', status: 'Active', criticality: 'Critical', nextMaintenance: '2027-03-01' },
    { tag: 'HVAC-00112', status: 'Active', criticality: 'High', nextMaintenance: '2026-11-15' },
    { tag: 'UPS-00078', status: 'Active', criticality: 'Critical', nextMaintenance: '2026-12-10' },
    { tag: 'FLT-00021', status: 'Active', criticality: 'High', nextMaintenance: '2026-10-22' },
    { tag: 'FP-00056', status: 'Under Maintenance', criticality: 'Critical', nextMaintenance: '2026-10-05' },
    { tag: 'AC-00311', status: 'Active', criticality: 'Medium', nextMaintenance: '2027-01-18' },
    { tag: 'IT-00189', status: 'Active', criticality: 'Medium', nextMaintenance: null },
    { tag: 'CCTV-00234', status: 'Active', criticality: 'Medium', nextMaintenance: '2027-02-12' },
    { tag: 'TRF-00067', status: 'In Repair', criticality: 'Critical', nextMaintenance: '2026-10-20' },
    { tag: 'SOL-00456', status: 'Active', criticality: 'Low', nextMaintenance: '2027-04-01' },
  ];

  const rows: AssetRow[] = [];
  const total = 64;
  for (let i = 0; i < total; i++) {
    const t = TEMPLATES[i < 10 ? i : (i * 7) % TEMPLATES.length];
    const [site, area] = LOCATIONS[i < 10 ? i : (i * 3) % LOCATIONS.length];
    const [custodian, department] = CUSTODIANS[i < 10 ? i : (i * 7 + 3) % CUSTODIANS.length];
    const s = seed[i];

    let nextMaintenance: string | null = null;
    if (!s) {
      // Spread due dates from a few weeks overdue to ~8 months out; every 9th asset has none scheduled.
      if (i % 9 !== 4) {
        const d = new Date(TODAY);
        d.setDate(d.getDate() + ((i * 37) % 260) - 25);
        nextMaintenance = toIsoDate(d);
      }
    }

    rows.push({
      id: i + 1,
      name: t.name,
      tag: s?.tag ?? `${t.tagPrefix}-${String(100 + ((i * 53) % 900)).padStart(5, '0')}`,
      category: t.category,
      type: t.type,
      site,
      area,
      custodian,
      department,
      status: s?.status ?? STATUS_CYCLE[i % STATUS_CYCLE.length],
      criticality: s?.criticality ?? t.criticality,
      manufacturer: t.manufacturer,
      nextMaintenance: s ? s.nextMaintenance : nextMaintenance,
      icon: t.icon,
    });
  }
  return rows;
}

function toIsoDate(d: Date): string {
  const mm = String(d.getMonth() + 1).padStart(2, '0');
  const dd = String(d.getDate()).padStart(2, '0');
  return `${d.getFullYear()}-${mm}-${dd}`;
}

function uniqueSorted(values: string[]): string[] {
  return [...new Set(values)].sort((a, b) => a.localeCompare(b));
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

const MAIN_STATUSES: AssetStatus[] = ['Active', 'Under Maintenance', 'In Repair', 'Disposed'];
const CRITICALITY_RANK: Record<Criticality, number> = { Critical: 0, High: 1, Medium: 2, Low: 3 };
const AVATAR_COLORS = ['#6d4aff', '#2563eb', '#0891b2', '#db2777', '#7c3aed', '#059669', '#ea580c', '#4f46e5'];

@Component({
  selector: 'app-asset-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
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
  };

  readonly assets: AssetRow[] = buildMockAssets();

  readonly categoryOptions = uniqueSorted(this.assets.map((a) => a.category));
  readonly statusOptions: AssetStatus[] = ['Active', 'Under Maintenance', 'In Repair', 'Disposed', 'In Storage', 'Checked Out'];
  readonly locationOptions = uniqueSorted(this.assets.map((a) => a.site));
  readonly departmentOptions = uniqueSorted(this.assets.map((a) => a.department));
  readonly custodianOptions = uniqueSorted(this.assets.map((a) => a.custodian));
  readonly criticalityOptions: Criticality[] = ['Critical', 'High', 'Medium', 'Low'];
  readonly manufacturerOptions = uniqueSorted(this.assets.map((a) => a.manufacturer));
  readonly pageSizeOptions = [10, 25, 50, 100];

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

  selected = new Set<number>();
  openMenuId: number | null = null;

  statusCards: StatusCard[] = [];
  filtered: AssetRow[] = [];
  pageRows: AssetRow[] = [];

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  constructor(private readonly router: Router) {
    this.statusCards = this.buildStatusCards();
    this.refresh();
  }

  // ===== Filters =====

  /** Types offered depend on the chosen category. */
  get typeOptions(): string[] {
    const source = this.draft.category ? this.assets.filter((a) => a.category === this.draft.category) : this.assets;
    return uniqueSorted(source.map((a) => a.type));
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

  onSearchChange(): void {
    this.page = 1;
    this.refresh();
  }

  clearSearch(): void {
    this.search = '';
    this.onSearchChange();
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

  private buildStatusCards(): StatusCard[] {
    const count = (fn: (a: AssetRow) => boolean) => this.assets.filter(fn).length;
    return [
      { label: 'Active', value: 'Active', dot: '#22a95b', count: count((a) => a.status === 'Active') },
      { label: 'Under Maintenance', value: 'Under Maintenance', dot: '#f59e0b', count: count((a) => a.status === 'Under Maintenance') },
      { label: 'In Repair', value: 'In Repair', dot: '#e11d48', count: count((a) => a.status === 'In Repair') },
      { label: 'Disposed', value: 'Disposed', dot: '#94a3b8', count: count((a) => a.status === 'Disposed') },
      { label: 'Other', value: 'Other', dot: '#4f2bd8', count: count((a) => !MAIN_STATUSES.includes(a.status)) },
    ];
  }

  // ===== Sorting =====

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
    this.refresh();
  }

  sortIcon(key: SortKey) {
    if (this.sortKey !== key) return this.icons.ChevronsUpDown;
    return this.sortDir === 'asc' ? this.icons.ChevronUp : this.icons.ChevronDown;
  }

  private sortValue(a: AssetRow, key: SortKey): string | number {
    switch (key) {
      case 'location':
        return `${a.site} ${a.area}`;
      case 'category':
        return `${a.category} ${a.type}`;
      case 'criticality':
        return CRITICALITY_RANK[a.criticality];
      case 'nextMaintenance':
        // Unscheduled assets sort last in ascending order.
        return a.nextMaintenance ?? '9999-12-31';
      default:
        return a[key];
    }
  }

  // ===== Derived rows =====

  private refresh(): void {
    const q = this.search.trim().toLowerCase();
    const f = this.applied;

    let rows = this.assets.filter(
      (a) =>
        (!q ||
          [a.name, a.tag, a.category, a.type, a.site, a.area, a.custodian, a.manufacturer].some((v) =>
            v.toLowerCase().includes(q),
          )) &&
        (!f.category || a.category === f.category) &&
        (!f.type || a.type === f.type) &&
        this.matchesStatus(a, f.status) &&
        (!f.location || a.site === f.location) &&
        (!f.department || a.department === f.department) &&
        (!f.custodian || a.custodian === f.custodian) &&
        (!f.criticality || a.criticality === f.criticality) &&
        (!f.manufacturer || a.manufacturer === f.manufacturer) &&
        this.matchesMaintenanceDue(a, f.maintenanceDue),
    );

    if (this.sortKey) {
      const key = this.sortKey;
      const dir = this.sortDir === 'asc' ? 1 : -1;
      rows = [...rows].sort((x, y) => {
        const vx = this.sortValue(x, key);
        const vy = this.sortValue(y, key);
        const cmp = typeof vx === 'number' && typeof vy === 'number' ? vx - vy : String(vx).localeCompare(String(vy));
        return cmp * dir;
      });
    }

    this.filtered = rows;
    this.page = Math.min(this.page, this.totalPages);
    const start = (this.page - 1) * this.pageSize;
    this.pageRows = rows.slice(start, start + this.pageSize);
  }

  // ===== Pagination =====

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.filtered.length / this.pageSize));
  }

  get rangeStart(): number {
    return this.filtered.length ? (this.page - 1) * this.pageSize + 1 : 0;
  }

  get rangeEnd(): number {
    return Math.min(this.page * this.pageSize, this.filtered.length);
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
      if (selectAll) this.selected.add(r.id);
      else this.selected.delete(r.id);
    }
  }

  toggleRow(id: number): void {
    if (this.selected.has(id)) this.selected.delete(id);
    else this.selected.add(id);
  }

  clearSelection(): void {
    this.selected.clear();
  }

  // ===== Row actions =====

  toggleMenu(id: number, event: MouseEvent): void {
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

  viewAsset(asset: AssetRow): void {
    this.openMenuId = null;
    this.showToast(`${asset.name} (${asset.tag}) details coming soon.`);
  }

  // ===== Export / print =====

  exportCsv(): void {
    const rows = this.selected.size ? this.filtered.filter((a) => this.selected.has(a.id)) : this.filtered;
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

  statusClass(status: AssetStatus): string {
    return 'status--' + status.toLowerCase().replace(/\s+/g, '-');
  }

  criticalityClass(c: Criticality): string {
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

  trackById(_: number, row: AssetRow): number {
    return row.id;
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 2600);
  }
}
