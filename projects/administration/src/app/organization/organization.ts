import { Component, HostListener, ViewChild, effect, inject } from '@angular/core';
import { ExportMenu, ExportRequest } from 'shared-ui';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  ArrowUpDown,
  Box,
  Building2,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  ChevronUp,
  Download,
  Ellipsis,
  Grid3x3,
  Layers,
  ListFilter,
  LucideAngularModule,
  LucideIconData,
  MapPin,
  Network,
  Package,
  Search,
  ShieldCheck,
  Truck,
  Users,
  Wrench
} from 'lucide-angular';
import { AdminPage } from '../shared/admin-common';
import { LOCATION_TYPES, TYPE_TONES } from './organization.data';
import { LocationType, OrgLocation, OrgTabId, RecordStatus } from './organization.model';
import { OrganizationService } from './organization.service';
import { LocationTree } from './location-tree/location-tree';
import { SummaryTabView } from './summary-tab/summary-tab';

type SortKey = 'code' | 'name' | 'type' | 'parent' | 'contact' | 'status';

/** Administration > Organization: location hierarchy plus a summary of each master for the organization. */
@Component({
  standalone: true,
  selector: 'app-admin-organization',
  imports: [FormsModule, RouterLink, LucideAngularModule, LocationTree, SummaryTabView, ExportMenu],
  templateUrl: './organization.html',
  styleUrls: ['../shared/admin-workspace.css', './organization.css']
})
export class OrganizationPage extends AdminPage {
  @ViewChild(LocationTree) tree?: LocationTree;

  readonly service = inject(OrganizationService);
  readonly icons = {
    ArrowUpDown, Building2, ChevronDown, ChevronLeft, ChevronRight, ChevronUp, Download, Ellipsis, Grid3x3, Layers,
    ListFilter, MapPin, Network, Search
  };
  readonly tabs: { id: OrgTabId; label: string; icon: LucideIconData }[] = [
    { id: 'location', label: 'Location', icon: MapPin },
    { id: 'asset', label: 'Asset', icon: Box },
    { id: 'people', label: 'People', icon: Users },
    { id: 'vendor', label: 'Vendor', icon: Truck },
    { id: 'maintenance', label: 'Maintenance', icon: Wrench },
    { id: 'inventory', label: 'Inventory', icon: Package },
    { id: 'compliance', label: 'Compliance', icon: ShieldCheck }
  ];
  readonly types = LOCATION_TYPES;
  readonly typeTones = TYPE_TONES;
  readonly pageSizes = [10, 20, 50];
  readonly typeCards: { type: LocationType; label: string; icon: LucideIconData; tone: string }[] = [
    { type: 'Location', label: 'Locations', icon: MapPin, tone: 'purple' },
    { type: 'Building', label: 'Buildings', icon: Building2, tone: 'blue' },
    { type: 'Floor', label: 'Floors', icon: Layers, tone: 'pink' },
    { type: 'Zone', label: 'Zones', icon: Grid3x3, tone: 'green' }
  ];

  activeTab: OrgTabId = 'location';

  selectedNode: string | null = null;
  search = '';
  filterOpen = false;
  typeFilter: '' | LocationType = '';
  statusFilter: '' | RecordStatus = '';
  sortKey: SortKey | null = null;
  sortDir: 1 | -1 = 1;
  page = 0;
  pageSize = 10;
  listCollapsed = false;
  menuFor: string | null = null;

  /** Selected organization (Master Data id); `organization` (from AdminPage) holds its name for the page. */
  orgId = '';
  /** Locations are read-only here; they are managed in Master Data > Organization > Location. */

  constructor() {
    super();
    // Load failures show up as a toast
    effect(() => {
      const error = this.service.error();
      if (error) this.notify(error, 'error');
    });
    // Organizations come from the Master Data API; the first one is selected and its data loaded into every tab
    this.service.loadOrganizations().subscribe({
      next: (list) => {
        if (list.length) this.selectOrganization(list[0].id);
      },
      error: () => {}
    });
  }

  /** Organization picker: load the chosen organization's data into all tabs. */
  selectOrganization(id: string): void {
    const org = this.service.organizations().find((o) => o.id === id);
    if (!org) return;
    this.orgId = org.id;
    this.organization = org.name;
    this.selectedNode = null;
    this.page = 0;
    this.service.loadFor(org);
  }

  /* ===== Tabs ===== */

  setTab(id: OrgTabId): void {
    this.activeTab = id;
    this.menuFor = null;
  }

  get summaryTab() {
    return this.service.tab(this.activeTab);
  }

  /* ===== Stats ===== */

  countOf(type: LocationType): number {
    return this.service.ofType(type).length;
  }

  // A stat card narrows the table to its type across the whole organization
  showType(type: LocationType): void {
    this.selectedNode = null;
    this.typeFilter = this.typeFilter === type ? '' : type;
    this.filterOpen = this.filterOpen || !!this.typeFilter;
    this.page = 0;
  }

  /* ===== Hierarchy ===== */

  get selectedLocation(): OrgLocation | undefined {
    return this.service.find(this.selectedNode);
  }

  selectNode(code: string | null): void {
    this.menuFor = null;
    this.selectedNode = code;
    this.page = 0;
  }

  /** The selected node with the branch above it and everything below it. */
  private scope(): OrgLocation[] {
    const node = this.selectedLocation;
    if (!node) return this.service.all();
    const codes = new Set([...this.service.ancestorsOf(node.code).map((a) => a.code), node.code, ...this.service.descendantsOf(node.code)]);
    return this.service.all().filter((l) => codes.has(l.code));
  }

  /* ===== Table ===== */

  get rows(): OrgLocation[] {
    const term = this.search.trim().toLowerCase();
    const rows = this.scope().filter(
      (l) =>
        (!this.typeFilter || l.type === this.typeFilter) &&
        (!this.statusFilter || l.status === this.statusFilter) &&
        (!term || [l.code, l.name, l.type, this.parentName(l), l.contact, l.city].some((v) => v.toLowerCase().includes(term)))
    );
    const key = this.sortKey;
    if (!key) return rows;
    return [...rows].sort((a, b) => this.sortDir * this.sortValue(a, key).localeCompare(this.sortValue(b, key), undefined, { numeric: true }));
  }

  private sortValue(l: OrgLocation, key: SortKey): string {
    if (key === 'parent') return this.parentName(l);
    if (key === 'type') return String(LOCATION_TYPES.indexOf(l.type));
    return l[key];
  }

  get pageRows(): OrgLocation[] {
    return this.rows.slice(this.page * this.pageSize, (this.page + 1) * this.pageSize);
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.rows.length / this.pageSize));
  }

  /** Up to five page buttons around the current page. */
  get pageNumbers(): number[] {
    const count = this.pageCount;
    const start = Math.max(0, Math.min(this.page - 2, count - 5));
    return Array.from({ length: Math.min(5, count) }, (_, i) => start + i);
  }

  get showingFrom(): number {
    return this.rows.length ? this.page * this.pageSize + 1 : 0;
  }

  get showingTo(): number {
    return Math.min((this.page + 1) * this.pageSize, this.rows.length);
  }

  goTo(page: number): void {
    this.page = Math.min(Math.max(0, page), this.pageCount - 1);
  }

  onFilterChange(): void {
    this.page = 0;
  }

  clearFilters(): void {
    this.search = '';
    this.typeFilter = '';
    this.statusFilter = '';
    this.page = 0;
  }

  get filterCount(): number {
    return (this.typeFilter ? 1 : 0) + (this.statusFilter ? 1 : 0);
  }

  sortBy(key: SortKey): void {
    if (this.sortKey === key) this.sortDir = this.sortDir === 1 ? -1 : 1;
    else {
      this.sortKey = key;
      this.sortDir = 1;
    }
  }

  ariaSort(key: SortKey): string {
    return this.sortKey !== key ? 'none' : this.sortDir === 1 ? 'ascending' : 'descending';
  }

  parentName(l: OrgLocation): string {
    return this.service.find(l.parent)?.name ?? '';
  }

  toggleList(): void {
    this.listCollapsed = !this.listCollapsed;
  }

  toggleFilters(): void {
    this.filterOpen = !this.filterOpen;
  }

  /* ===== Row menu ===== */

  toggleMenu(l: OrgLocation, event: Event): void {
    event.stopPropagation();
    this.menuFor = this.menuFor === l.code ? null : l.code;
  }

  toggleToolbarMenu(event: Event): void {
    event.stopPropagation();
    this.menuFor = this.menuFor === '__toolbar' ? null : '__toolbar';
  }

  @HostListener('document:click')
  closeMenu(): void {
    this.menuFor = null;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.menuFor = null;
  }

  /* ===== Import / Export ===== */

  /** Export menu (CSV / PDF): every location in the selected branch matching search, type and status, in table order. */
  readonly exportRequest = (): ExportRequest<OrgLocation> => ({
    title: 'Organization Locations',
    fileBase: 'locations',
    columns: [
      { label: 'Location Code', key: 'code' }, { label: 'Location Name', key: 'name' }, { label: 'Type', key: 'type' },
      { label: 'Parent Code', value: (l) => l.parent ?? '' }, { label: 'Address', key: 'address' }, { label: 'Country', key: 'country' },
      { label: 'City', key: 'city' }, { label: 'Time Zone', key: 'timeZone' }, { label: 'Manager', key: 'contact' }, { label: 'Status', key: 'status' }
    ],
    filters: { Search: this.search, Type: this.typeFilter, Status: this.statusFilter, Branch: this.selectedNode },
    rows: () => this.rows
  });
}
