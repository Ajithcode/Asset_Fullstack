import { Component, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  LucideAngularModule,
  Battery,
  Box,
  Cable,
  ChevronDown,
  ArrowLeft,
  ChevronLeft,
  ChevronRight,
  CircleGauge,
  CirclePlus,
  CircuitBoard,
  Cog,
  Component as ComponentIcon,
  Cpu,
  Cylinder,
  Droplets,
  EllipsisVertical,
  Eye,
  Fan,
  FileText,
  Fuel,
  Gauge,
  History,
  Inbox,
  Paperclip,
  Plus,
  Puzzle,
  Search,
  Settings,
  Snowflake,
  SquarePen,
  Thermometer,
  Trash2,
  Waves,
  Wind,
  Wrench,
  X,
  Zap,
} from 'lucide-angular';
import { ComponentMaintenance, type MaintenanceData } from './component-maintenance/component-maintenance';
import { ComponentServiceHistory, type ServiceRecord } from './component-service-history/component-service-history';
import { ComponentDocuments, type ComponentDocument } from './component-documents/component-documents';
import { ComponentAttachments, type ComponentAttachment } from './component-attachments/component-attachments';
import { ComponentOverview } from './component-overview/component-overview';

type ComponentStatus = 'Active' | 'Under Maintenance' | 'Needs Replacement' | 'Inactive';
type LifeUnit = 'hrs' | 'months';
type AssetTab = 'overview' | 'components' | 'maintenance' | 'service' | 'documents' | 'attachments';

interface TabMeta {
  title: string;
  subtitle: string;
  scope: 'asset' | 'component';
}

export interface AssetComponent {
  id: number;
  name: string;
  tag: string;
  model: string;
  manufacturer: string;
  partNumber: string;
  serialNumber: string;
  /** ISO dates (yyyy-mm-dd); empty when unknown. */
  installedDate: string;
  expectedReplacement: string;
  /** Life used so far and the rated life, for the life-cycle bar. 0 total = not tracked. */
  lifeUsed: number;
  lifeTotal: number;
  lifeUnit: LifeUnit;
  status: ComponentStatus;
  description: string;
  icon: keyof AssetComponents['componentIcons'];
  children: AssetComponent[];
  /** Maintenance plan and history, created on first visit to the Maintenance tab. */
  maintenance?: MaintenanceData;
  /** Service log, created on first visit to the Service History tab. */
  serviceHistory?: ServiceRecord[];
  /** Documents, created on first visit to the Documents tab. */
  documents?: ComponentDocument[];
  /** Attachments, created on first visit to the Attachments tab. */
  attachments?: ComponentAttachment[];
}

interface AssetRecord {
  name: string;
  tag: string;
  type: string;
  model: string;
  manufacturer: string;
  location: string;
  area: string;
  custodian: string;
  status: 'Active' | 'Under Maintenance' | 'In Repair';
  icon: keyof AssetComponents['componentIcons'];
  components: AssetComponent[];
}

type ComponentForm = Omit<AssetComponent, 'id' | 'children' | 'icon'> & { parentId: number | null };

let nextId = 1;

function part(
  name: string,
  tag: string,
  icon: AssetComponent['icon'],
  details: Partial<Omit<AssetComponent, 'id' | 'name' | 'tag' | 'icon'>> = {},
): AssetComponent {
  return {
    id: nextId++,
    name,
    tag,
    icon,
    model: details.model ?? '',
    manufacturer: details.manufacturer ?? '',
    partNumber: details.partNumber ?? '',
    serialNumber: details.serialNumber ?? '',
    installedDate: details.installedDate ?? '',
    expectedReplacement: details.expectedReplacement ?? '',
    lifeUsed: details.lifeUsed ?? 0,
    lifeTotal: details.lifeTotal ?? 0,
    lifeUnit: details.lifeUnit ?? 'hrs',
    status: details.status ?? 'Active',
    description: details.description ?? '',
    children: details.children ?? [],
  };
}

function buildMockAssets(): AssetRecord[] {
  return [
    {
      name: 'Generator 250 kVA',
      tag: 'GEN-00045',
      type: 'Generator',
      model: 'G250',
      manufacturer: 'Caterpillar',
      location: 'Head Office - Building A',
      area: 'Plant Room',
      custodian: 'Ahmed Basim',
      status: 'Active',
      icon: 'generator',
      components: [
        part('Engine Assembly', 'ENG-001', 'engine', {
          model: 'C9.3B', manufacturer: 'Caterpillar', partNumber: '490-1022', serialNumber: 'SN-ENG-2026-001',
          installedDate: '2026-01-15', lifeUsed: 4000, lifeTotal: 30000,
          description: 'Diesel engine assembly including the intake and lubrication filters.',
          children: [
            part('Air Filter', 'AF-001', 'filter', {
              model: 'CAT-AF250', manufacturer: 'Caterpillar', partNumber: '1R-1808', serialNumber: 'SN-AF-2026-001',
              installedDate: '2026-01-15', expectedReplacement: '2027-01-15', lifeUsed: 4000, lifeTotal: 10000,
              description: 'Primary air filter for engine air intake system.',
            }),
            part('Fuel Filter', 'FF-001', 'fuel', {
              model: 'CAT-FF250', manufacturer: 'Caterpillar', partNumber: '1R-0751', serialNumber: 'SN-FF-2026-001',
              installedDate: '2026-01-15', expectedReplacement: '2026-11-15', lifeUsed: 4000, lifeTotal: 5000,
              description: 'Secondary fuel filter protecting the injection system.',
            }),
            part('Oil Filter', 'OF-001', 'oil', {
              model: 'CAT-OF250', manufacturer: 'Caterpillar', partNumber: '1R-0739', serialNumber: 'SN-OF-2026-001',
              installedDate: '2026-01-15', expectedReplacement: '2026-09-30', lifeUsed: 4000, lifeTotal: 4000,
              status: 'Under Maintenance', description: 'Full-flow engine oil filter.',
            }),
            part('Turbo Charger', 'TC-001', 'turbo', {
              model: 'CAT-TC250', manufacturer: 'Caterpillar', partNumber: '358-1471', serialNumber: 'SN-TC-2026-001',
              installedDate: '2026-01-15', expectedReplacement: '2030-01-15', lifeUsed: 4000, lifeTotal: 25000,
              description: 'Exhaust-driven turbocharger for the engine air supply.',
            }),
          ],
        }),
        part('Alternator', 'ALT-001', 'alternator', {
          model: 'LC6114', manufacturer: 'Leroy-Somer', partNumber: 'LS-6114-250', serialNumber: 'SN-ALT-2026-001',
          installedDate: '2026-01-15', lifeUsed: 4000, lifeTotal: 40000, description: 'Brushless synchronous alternator.',
          children: [
            part('AVR', 'AVR-001', 'board', {
              model: 'R450', manufacturer: 'Leroy-Somer', partNumber: 'R450-A', serialNumber: 'SN-AVR-2026-001',
              installedDate: '2026-01-15', description: 'Automatic voltage regulator.',
            }),
          ],
        }),
        part('Cooling System', 'CLS-001', 'cooling', {
          manufacturer: 'Caterpillar', installedDate: '2026-01-15', description: 'Engine cooling circuit.',
          children: [
            part('Radiator', 'RAD-001', 'radiator', {
              model: 'CAT-RD250', manufacturer: 'Caterpillar', partNumber: '245-7590', serialNumber: 'SN-RAD-2026-001',
              installedDate: '2026-01-15', lifeUsed: 4000, lifeTotal: 20000, description: 'Engine-mounted radiator core.',
            }),
            part('Cooling Fan', 'FAN-001', 'fan', {
              model: 'CAT-FN250', manufacturer: 'Caterpillar', partNumber: '361-4511', serialNumber: 'SN-FAN-2026-001',
              installedDate: '2026-01-15', lifeUsed: 4000, lifeTotal: 15000, description: 'Pusher fan for the radiator.',
            }),
          ],
        }),
        part('Control System', 'CTL-001', 'control', {
          installedDate: '2026-01-15', description: 'Generator control and protection.',
          children: [
            part('Control Panel', 'CP-001', 'panel', {
              model: 'EMCP 4.2B', manufacturer: 'Caterpillar', partNumber: '533-0371', serialNumber: 'SN-CP-2026-001',
              installedDate: '2026-01-15', description: 'Operator control panel.',
            }),
            part('Controller Module', 'CM-001', 'cpu', {
              model: 'ECM-A4', manufacturer: 'Caterpillar', partNumber: '548-4207', serialNumber: 'SN-CM-2026-001',
              installedDate: '2026-01-15', description: 'Engine control module.',
            }),
            part('Battery', 'BAT-001', 'battery', {
              model: '12V 100Ah', manufacturer: 'Varta', partNumber: '600-123', serialNumber: 'SN-BAT-2026-001',
              installedDate: '2026-01-15', expectedReplacement: '2028-01-15', lifeUsed: 9, lifeTotal: 24, lifeUnit: 'months',
              status: 'Needs Replacement', description: 'Lead-acid starter battery.',
            }),
          ],
        }),
        part('Enclosure', 'ENC-001', 'enclosure', {
          model: 'SA-250', manufacturer: 'Caterpillar', installedDate: '2026-01-15', description: 'Sound-attenuated weatherproof enclosure.',
        }),
      ],
    },
    {
      name: 'HVAC Chiller',
      tag: 'HVAC-00112',
      type: 'Chiller',
      model: 'TR-350',
      manufacturer: 'Trane',
      location: 'Head Office - Building B',
      area: 'Roof',
      custodian: 'Sara Ahmed',
      status: 'Active',
      icon: 'cooling',
      components: [
        part('Compressor', 'CMP-112', 'engine', {
          model: 'CVHF-350', manufacturer: 'Trane', partNumber: 'CMP-350', serialNumber: 'SN-CMP-2025-112',
          installedDate: '2025-06-01', lifeUsed: 9800, lifeTotal: 60000, description: 'Centrifugal compressor.',
        }),
        part('Condenser Fan', 'CFN-112', 'fan', {
          model: 'CF-900', manufacturer: 'Ziehl-Abegg', installedDate: '2025-06-01', lifeUsed: 9800, lifeTotal: 30000,
          description: 'Air-cooled condenser fan.',
        }),
        part('Chilled Water Pump', 'CWP-112', 'oil', {
          model: 'NBG-125', manufacturer: 'Grundfos', installedDate: '2025-06-01', status: 'Under Maintenance',
          description: 'Primary chilled water circulation pump.',
        }),
      ],
    },
    {
      name: 'UPS 100 kVA',
      tag: 'UPS-00078',
      type: 'UPS',
      model: 'Galaxy VS',
      manufacturer: 'Schneider Electric',
      location: 'Data Center - Building A',
      area: 'DC Room',
      custodian: 'Mohan Kumar',
      status: 'Active',
      icon: 'battery',
      components: [
        part('Battery String A', 'BSA-078', 'battery', {
          model: 'VRLA 12V 9Ah', manufacturer: 'CSB', installedDate: '2024-03-10', expectedReplacement: '2028-03-10',
          lifeUsed: 31, lifeTotal: 48, lifeUnit: 'months', description: '40-block VRLA battery string.',
        }),
        part('Power Module', 'PM-078', 'board', {
          model: 'GVSPM50KB', manufacturer: 'Schneider Electric', installedDate: '2024-03-10', description: '50 kW power module.',
        }),
      ],
    },
  ];
}

const EMPTY_FORM: ComponentForm = {
  name: '',
  tag: '',
  model: '',
  manufacturer: '',
  partNumber: '',
  serialNumber: '',
  installedDate: '',
  expectedReplacement: '',
  lifeUsed: 0,
  lifeTotal: 0,
  lifeUnit: 'hrs',
  status: 'Active',
  description: '',
  parentId: null,
};

const NOW = new Date();
const TODAY_ISO = `${NOW.getFullYear()}-${String(NOW.getMonth() + 1).padStart(2, '0')}-${String(NOW.getDate()).padStart(2, '0')}`;

@Component({
  selector: 'app-asset-components',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule, ComponentMaintenance, ComponentServiceHistory, ComponentDocuments, ComponentAttachments, ComponentOverview],
  templateUrl: './asset-components.html',
  styleUrl: './asset-components.css',
})
export class AssetComponents {
  readonly icons = {
    ArrowLeft, ChevronDown, ChevronLeft, ChevronRight, CirclePlus, EllipsisVertical, Eye, FileText, History, Inbox, Paperclip,
    Plus, Puzzle, Search, SquarePen, Trash2, Wrench, X, ComponentIcon,
  };

  readonly componentIcons = {
    generator: Zap,
    engine: Settings,
    filter: Cylinder,
    fuel: Fuel,
    oil: Droplets,
    turbo: Fan,
    alternator: CircleGauge,
    board: CircuitBoard,
    cooling: Snowflake,
    radiator: Waves,
    fan: Wind,
    control: Gauge,
    panel: Thermometer,
    cpu: Cpu,
    battery: Battery,
    enclosure: Box,
    cable: Cable,
    generic: Cog,
  };

  readonly tabs: { key: AssetTab; label: string; icon: typeof CirclePlus }[] = [
    { key: 'overview', label: 'Overview', icon: CirclePlus },
    { key: 'components', label: 'Components', icon: Puzzle },
    { key: 'maintenance', label: 'Maintenance', icon: Wrench },
    { key: 'service', label: 'Service History', icon: History },
    { key: 'documents', label: 'Documents', icon: FileText },
    { key: 'attachments', label: 'Attachments', icon: Paperclip },
  ];

  readonly statusOptions: ComponentStatus[] = ['Active', 'Under Maintenance', 'Needs Replacement', 'Inactive'];
  readonly pageSizeOptions = [10, 25, 50];

  assets: AssetRecord[] = buildMockAssets();
  assetTag: string;
  activeTab: AssetTab = 'components';

  expanded = new Set<number>();
  /** Tree node whose children fill the table; null means the asset itself (top-level components). */
  groupId: number | null = null;
  /** Component shown in the details panel. */
  detailId: number | null = null;
  /** Component the component-level tabs (Maintenance, Service History, Documents, Attachments) are showing. */
  focusId: number | null = null;

  search = '';
  statusFilter: '' | ComponentStatus = '';
  page = 1;
  pageSize = 10;
  checked = new Set<number>();
  openMenu: number | null = null;

  formOpen = false;
  editing: AssetComponent | null = null;
  form: ComponentForm = { ...EMPTY_FORM };
  formSubmitted = false;

  deleteTarget: AssetComponent | null = null;

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  constructor(
    private readonly router: Router,
    route: ActivatedRoute,
  ) {
    const requested = route.snapshot.queryParamMap.get('tag');
    this.assetTag = this.assets.some((a) => a.tag === requested) ? requested! : this.assets[0].tag;
    this.resetSelection();
  }

  // ===== Asset =====

  get asset(): AssetRecord {
    return this.assets.find((a) => a.tag === this.assetTag) ?? this.assets[0];
  }

  onAssetChange(): void {
    this.router.navigate([], { queryParams: { tag: this.assetTag }, replaceUrl: true });
    this.resetSelection();
  }

  /** Opens the first assembly and shows its first part, as in the design. */
  private resetSelection(): void {
    this.expanded.clear();
    this.checked.clear();
    this.search = '';
    this.statusFilter = '';
    this.page = 1;
    const firstGroup = this.asset.components.find((c) => c.children.length);
    this.asset.components.filter((c) => c.children.length).forEach((c) => {
      if (c === firstGroup || c.icon === 'control') this.expanded.add(c.id);
    });
    this.groupId = firstGroup?.id ?? null;
    this.detailId = firstGroup?.children[0]?.id ?? this.asset.components[0]?.id ?? null;
    this.focusId = firstGroup?.id ?? this.detailId;
  }

  // ===== Tabs =====

  /**
   * Header text for every tab except Components. `scope` says whether the tab summarises the whole
   * asset or works on one component at a time (those show the component picker in the summary card).
   */
  private readonly tabMetas: Partial<Record<AssetTab, TabMeta>> = {
    overview: { title: 'Overview', subtitle: 'View component structure, health, and key details for the selected asset', scope: 'asset' },
    maintenance: { title: 'Maintenance', subtitle: 'Manage maintenance activities, schedules and history for asset components', scope: 'component' },
    service: { title: 'Service History', subtitle: 'View and manage service history for asset components', scope: 'component' },
    documents: { title: 'Documents', subtitle: 'Manage documents related to asset components', scope: 'component' },
    attachments: { title: 'Attachments', subtitle: 'Manage files, images and other attachments for asset components', scope: 'component' },
  };

  get tabMeta(): TabMeta | null {
    return this.tabMetas[this.activeTab] ?? null;
  }

  get isComponentScoped(): boolean {
    return this.tabMeta?.scope === 'component';
  }

  openTab(tab: AssetTab, focus?: AssetComponent | null): void {
    if (focus) {
      this.focusId = focus.id;
    } else if (this.tabMetas[tab]?.scope === 'component' && this.tabMetas[this.activeTab]?.scope !== 'component') {
      // Component-level tabs follow whatever component was being looked at on the Components tab.
      this.focusId = this.detailId ?? this.groupId ?? this.asset.components[0]?.id ?? null;
    }
    this.openMenu = null;
    this.activeTab = tab;
  }

  /** From the Overview: show a component on the Components tab. */
  showComponent(c: AssetComponent): void {
    this.selectNode(c);
    this.activeTab = 'components';
  }

  /** Bound for child components, so their trees use the same icons as this page. */
  readonly iconForFn = (c: AssetComponent) => this.iconFor(c);

  get focusComponent(): AssetComponent | null {
    return this.find(this.focusId) ?? this.asset.components[0] ?? null;
  }

  /** Every component on the asset, indented by depth, for the Maintenance tab's component picker. */
  get componentOptions(): { id: number; label: string }[] {
    const out: { id: number; label: string }[] = [];
    const visit = (nodes: AssetComponent[], depth: number) => {
      for (const n of nodes) {
        out.push({ id: n.id, label: `${'   '.repeat(depth)}${n.name} (${n.tag})` });
        visit(n.children, depth + 1);
      }
    };
    visit(this.asset.components, 0);
    return out;
  }

  /** Opens the Components tab with the maintenance component selected. */
  viewComponentDetails(): void {
    const c = this.focusComponent;
    if (c) this.selectNode(c);
    this.activeTab = 'components';
  }

  viewAssetDetails(): void {
    this.router.navigate(['/assets/list'], { queryParams: { tag: this.asset.tag } });
  }

  // ===== Tree helpers =====

  private walk(nodes: AssetComponent[], fn: (n: AssetComponent, parent: AssetComponent | null) => void, parent: AssetComponent | null = null): void {
    for (const n of nodes) {
      fn(n, parent);
      this.walk(n.children, fn, n);
    }
  }

  find(id: number | null): AssetComponent | null {
    if (id === null) return null;
    let found: AssetComponent | null = null;
    this.walk(this.asset.components, (n) => {
      if (n.id === id) found = n;
    });
    return found;
  }

  parentOf(id: number): AssetComponent | null {
    let parent: AssetComponent | null = null;
    this.walk(this.asset.components, (n, p) => {
      if (n.id === id) parent = p;
    });
    return parent;
  }

  private isInSubtree(candidate: AssetComponent, ancestor: AssetComponent): boolean {
    let inside = false;
    this.walk([ancestor], (n) => {
      if (n.id === candidate.id) inside = true;
    });
    return inside;
  }

  get totalComponents(): number {
    let count = 0;
    this.walk(this.asset.components, () => count++);
    return count;
  }

  // ===== Tree interaction =====

  toggleExpand(n: AssetComponent, event?: Event): void {
    event?.stopPropagation();
    if (this.expanded.has(n.id)) this.expanded.delete(n.id);
    else this.expanded.add(n.id);
  }

  /** Assemblies open in the table; single parts are shown in the table of their parent. */
  selectNode(n: AssetComponent | null): void {
    this.openMenu = null;
    this.checked.clear();
    this.search = '';
    this.statusFilter = '';
    this.page = 1;
    if (!n) {
      this.groupId = null;
      this.detailId = null;
      return;
    }
    if (n.children.length) {
      this.groupId = n.id;
      this.expanded.add(n.id);
    } else {
      this.groupId = this.parentOf(n.id)?.id ?? null;
    }
    this.detailId = n.id;
  }

  get group(): AssetComponent | null {
    return this.find(this.groupId);
  }

  get detail(): AssetComponent | null {
    return this.find(this.detailId);
  }

  // ===== Table =====

  get groupChildren(): AssetComponent[] {
    return this.group ? this.group.children : this.asset.components;
  }

  get filtered(): AssetComponent[] {
    const q = this.search.trim().toLowerCase();
    return this.groupChildren.filter(
      (c) =>
        (!q || [c.name, c.tag, c.model, c.manufacturer, c.partNumber, c.serialNumber].some((v) => v.toLowerCase().includes(q))) &&
        (!this.statusFilter || c.status === this.statusFilter),
    );
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.filtered.length / this.pageSize));
  }

  get currentPage(): number {
    return Math.min(this.page, this.totalPages);
  }

  get pageRows(): AssetComponent[] {
    const start = (this.currentPage - 1) * this.pageSize;
    return this.filtered.slice(start, start + this.pageSize);
  }

  get rangeStart(): number {
    return this.filtered.length ? (this.currentPage - 1) * this.pageSize + 1 : 0;
  }

  get rangeEnd(): number {
    return Math.min(this.currentPage * this.pageSize, this.filtered.length);
  }

  get pageList(): number[] {
    return Array.from({ length: this.totalPages }, (_, i) => i + 1);
  }

  goToPage(p: number): void {
    if (p >= 1 && p <= this.totalPages) this.page = p;
  }

  onFilterChange(): void {
    this.page = 1;
  }

  get allChecked(): boolean {
    return this.pageRows.length > 0 && this.pageRows.every((r) => this.checked.has(r.id));
  }

  get someChecked(): boolean {
    return !this.allChecked && this.pageRows.some((r) => this.checked.has(r.id));
  }

  toggleAll(): void {
    const on = !this.allChecked;
    this.pageRows.forEach((r) => (on ? this.checked.add(r.id) : this.checked.delete(r.id)));
  }

  toggleRow(id: number): void {
    if (this.checked.has(id)) this.checked.delete(id);
    else this.checked.add(id);
  }

  setStatusForChecked(status: ComponentStatus): void {
    const rows = this.groupChildren.filter((c) => this.checked.has(c.id));
    rows.forEach((c) => (c.status = status));
    this.checked.clear();
    this.showToast(`${rows.length} component${rows.length === 1 ? '' : 's'} marked ${status.toLowerCase()}.`);
  }

  setStatus(c: AssetComponent, status: ComponentStatus): void {
    this.openMenu = null;
    c.status = status;
    this.showToast(`${c.name} marked ${status.toLowerCase()}.`);
  }

  // ===== Display helpers =====

  iconFor(c: AssetComponent) {
    return this.componentIcons[c.icon];
  }

  statusClass(status: string): string {
    return 'status--' + status.toLowerCase().replace(/\s+/g, '-');
  }

  lifePercent(c: AssetComponent): number {
    return c.lifeTotal ? Math.min(100, Math.round((c.lifeUsed / c.lifeTotal) * 100)) : 0;
  }

  lifeLevel(c: AssetComponent): 'ok' | 'warn' | 'bad' {
    const p = this.lifePercent(c);
    return p >= 90 ? 'bad' : p >= 70 ? 'warn' : 'ok';
  }

  isReplacementDue(c: AssetComponent): boolean {
    return !!c.expectedReplacement && c.expectedReplacement <= TODAY_ISO;
  }

  // ===== Menus =====

  toggleMenu(id: number, event: MouseEvent): void {
    event.stopPropagation();
    this.openMenu = this.openMenu === id ? null : id;
  }

  @HostListener('document:click')
  closeMenu(): void {
    this.openMenu = null;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.openMenu = null;
    this.formOpen = false;
    this.deleteTarget = null;
  }

  // ===== Add / edit =====

  /** Every component as an indented list for the "Parent Component" picker. */
  get parentOptions(): { id: number; label: string }[] {
    const out: { id: number; label: string }[] = [];
    const visit = (nodes: AssetComponent[], depth: number) => {
      for (const n of nodes) {
        if (this.editing && this.isInSubtree(n, this.editing)) continue;
        out.push({ id: n.id, label: `${'   '.repeat(depth)}${n.name}` });
        visit(n.children, depth + 1);
      }
    };
    visit(this.asset.components, 0);
    return out;
  }

  openAdd(): void {
    this.openMenu = null;
    this.editing = null;
    this.form = { ...EMPTY_FORM, parentId: this.groupId, manufacturer: this.asset.manufacturer, installedDate: TODAY_ISO };
    this.formSubmitted = false;
    this.formOpen = true;
  }

  openEdit(c: AssetComponent | null = this.detail): void {
    this.openMenu = null;
    if (!c) return;
    this.editing = c;
    this.form = {
      name: c.name,
      tag: c.tag,
      model: c.model,
      manufacturer: c.manufacturer,
      partNumber: c.partNumber,
      serialNumber: c.serialNumber,
      installedDate: c.installedDate,
      expectedReplacement: c.expectedReplacement,
      lifeUsed: c.lifeUsed,
      lifeTotal: c.lifeTotal,
      lifeUnit: c.lifeUnit,
      status: c.status,
      description: c.description,
      parentId: this.parentOf(c.id)?.id ?? null,
    };
    this.formSubmitted = false;
    this.formOpen = true;
  }

  isTagTaken(tag: string): boolean {
    const t = tag.trim().toUpperCase();
    if (!t) return false;
    let taken = false;
    this.walk(this.asset.components, (n) => {
      if (n !== this.editing && n.tag.toUpperCase() === t) taken = true;
    });
    return taken;
  }

  get lifeInvalid(): boolean {
    return this.form.lifeUsed < 0 || this.form.lifeTotal < 0 || (this.form.lifeTotal > 0 && this.form.lifeUsed > this.form.lifeTotal);
  }

  get datesInvalid(): boolean {
    return !!this.form.installedDate && !!this.form.expectedReplacement && this.form.expectedReplacement < this.form.installedDate;
  }

  saveForm(): void {
    this.formSubmitted = true;
    const f = this.form;
    if (!f.name.trim() || !f.tag.trim() || this.isTagTaken(f.tag) || this.lifeInvalid || this.datesInvalid) return;

    const { parentId, ...rest } = f;
    const values = {
      ...rest,
      name: f.name.trim(),
      tag: f.tag.trim().toUpperCase(),
      model: f.model.trim(),
      manufacturer: f.manufacturer.trim(),
      partNumber: f.partNumber.trim(),
      serialNumber: f.serialNumber.trim(),
      description: f.description.trim(),
      lifeUsed: Number(f.lifeUsed) || 0,
      lifeTotal: Number(f.lifeTotal) || 0,
    };
    const parent = this.find(parentId);
    const siblings = parent ? parent.children : this.asset.components;

    if (this.editing) {
      const c = this.editing;
      Object.assign(c, values);
      const currentParent = this.parentOf(c.id);
      if ((currentParent?.id ?? null) !== (parent?.id ?? null)) {
        const from = currentParent ? currentParent.children : this.asset.components;
        from.splice(from.indexOf(c), 1);
        siblings.push(c);
      }
      this.showToast(`${c.name} updated.`);
    } else {
      const created: AssetComponent = { id: nextId++, ...values, icon: this.guessIcon(values.name), children: [] };
      siblings.push(created);
      this.detailId = created.id;
      this.showToast(`${created.name} added${parent ? ` to ${parent.name}` : ''}.`);
    }
    if (parent) this.expanded.add(parent.id);
    this.groupId = parent?.id ?? null;
    this.formOpen = false;
  }

  private guessIcon(name: string): AssetComponent['icon'] {
    const n = name.toLowerCase();
    if (n.includes('fuel')) return 'fuel';
    if (n.includes('oil') || n.includes('pump')) return 'oil';
    if (n.includes('filter')) return 'filter';
    if (n.includes('battery')) return 'battery';
    if (n.includes('fan')) return 'fan';
    if (n.includes('radiator')) return 'radiator';
    if (n.includes('panel')) return 'panel';
    if (n.includes('module') || n.includes('controller')) return 'cpu';
    if (n.includes('cable') || n.includes('wiring')) return 'cable';
    return 'generic';
  }

  // ===== Remove =====

  askDelete(c: AssetComponent): void {
    this.openMenu = null;
    this.deleteTarget = c;
  }

  confirmDelete(): void {
    const c = this.deleteTarget;
    if (!c) return;
    const parent = this.parentOf(c.id);
    const siblings = parent ? parent.children : this.asset.components;
    siblings.splice(siblings.indexOf(c), 1);
    this.checked.delete(c.id);
    // Fall back to the parent if the removed branch held the current selection.
    if (this.find(this.groupId) === null) this.groupId = parent?.id ?? null;
    if (this.find(this.detailId) === null) this.detailId = null;
    this.deleteTarget = null;
    this.showToast(`${c.name} removed.`);
  }

  countDescendants(c: AssetComponent): number {
    let count = -1;
    this.walk([c], () => count++);
    return count;
  }

  trackById(_: number, c: AssetComponent): number {
    return c.id;
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 2800);
  }
}
