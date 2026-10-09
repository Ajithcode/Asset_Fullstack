import { Component, HostListener, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  LucideAngularModule,
  CalendarDays,
  Check,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  ChevronUp,
  Cog,
  Combine,
  Copy,
  Download,
  EllipsisVertical,
  Eye,
  FolderPlus,
  GripVertical,
  Inbox,
  LayoutList,
  Layers,
  ListTree,
  Plus,
  Power,
  PowerOff,
  Search,
  Settings,
  SquarePen,
  Trash2,
  Upload,
  X,
} from 'lucide-angular';
import {
  CustomField,
  FieldGroup,
  FieldLayout,
  FieldScope,
  FieldStatus,
  FieldType,
  LOOKUP_SOURCES,
  SYSTEM_FIELDS,
  typeClass,
} from './custom-fields.model';
import { CfFieldLayouts } from './field-layouts/field-layouts';

type ListTab = FieldScope | 'compound';
type PageTab = ListTab | 'groups' | 'layouts';

type FieldForm = Omit<CustomField, 'id' | 'order' | 'options' | 'parts'> & { optionsText: string; partsText: string };

interface GroupForm {
  original: FieldGroup | null;
  name: string;
  description: string;
  status: FieldStatus;
  /** Assigned fields in display order; applied to the fields on save. */
  fieldIds: number[];
}

interface TabMeta {
  label: string;
  /** Header title suffix and add-button noun; null keeps the plain "Custom Fields" header. */
  noun: string | null;
  subtitle: string;
}

const FIELD_TYPES: FieldType[] = ['Text', 'Text Area', 'Number', 'Date', 'Dropdown', 'Checkbox', 'Compound', 'Lookup'];
const NAME_PATTERN = /^[a-z][a-z0-9_]*$/;
const DESCRIPTION_MAX = 500;
const TOOLTIP_MAX = 250;
const GROUP_DESCRIPTION_MAX = 250;
const ASSET_CATEGORIES = ['Power Generation', 'Electrical Equipment', 'HVAC', 'IT Equipment', 'Security & Safety', 'Material Handling'];
const ALL_COMPONENTS = 'All Components';
const COMPONENT_TYPES = ['Engine', 'Alternator', 'Control Panel', 'Cooling System', 'Fuel System', 'Battery', 'Exhaust System'];

const G_GENERAL = 'General Information';
const G_PURCHASE = 'Purchase & Warranty';
const G_TECH = 'Technical Specifications';
const G_LOCATION = 'Location & Assignment';
const G_COMPLIANCE = 'Compliance & Certification';
const G_OPERATIONAL = 'Operational Details';
const G_FINANCIAL = 'Financial Information';
const G_MAINTENANCE = 'Maintenance Information';

let nextId = 1;

type Seed = [
  scope: FieldScope,
  name: string,
  label: string,
  type: FieldType,
  group: string,
  required: boolean,
  extra?: Partial<Pick<CustomField, 'appliesTo' | 'options' | 'parts' | 'lookupSource' | 'description' | 'tooltip' | 'showInDetails' | 'searchable' | 'status' | 'defaultValue'>>,
];

const SEEDS: Seed[] = [
  ['asset', 'warranty_expiry', 'Warranty Expiry Date', 'Date', G_PURCHASE, true, { description: 'Warranty expiry date provided by manufacturer', searchable: true }],
  ['asset', 'asset_condition', 'Current Condition', 'Dropdown', G_GENERAL, true, { options: ['New', 'Good', 'Fair', 'Poor'], defaultValue: 'Good', showInDetails: true }],
  ['asset', 'installation_date', 'Installation Date', 'Date', G_GENERAL, false, { showInDetails: true }],
  ['asset', 'power_rating', 'Power Rating (kVA)', 'Number', G_TECH, true, { showInDetails: true, searchable: true }],
  ['asset', 'voltage', 'Voltage (V)', 'Number', G_TECH, false],
  ['asset', 'serial_number', 'Serial Number', 'Text', G_GENERAL, true, { showInDetails: true, searchable: true }],
  ['asset', 'cost_center', 'Cost Center', 'Dropdown', G_FINANCIAL, false, { options: ['CC-100 Operations', 'CC-200 Facilities', 'CC-300 IT'] }],
  ['asset', 'criticality', 'Criticality', 'Dropdown', G_GENERAL, false, { options: ['Critical', 'High', 'Medium', 'Low'], defaultValue: 'Medium', searchable: true }],
  ['asset', 'location_zone', 'Location Zone', 'Text', G_LOCATION, false],
  ['asset', 'last_calibration', 'Last Calibration Date', 'Date', G_COMPLIANCE, false],
  ['asset', 'next_calibration', 'Next Calibration Date', 'Date', G_COMPLIANCE, false],
  ['asset', 'frequency', 'Frequency (Hz)', 'Number', G_TECH, false, { defaultValue: '50' }],
  ['asset', 'manufacturer', 'Manufacturer', 'Text', G_GENERAL, false, { searchable: true, showInDetails: true }],
  ['asset', 'model', 'Model', 'Text', G_GENERAL, false, { searchable: true, showInDetails: true }],
  ['asset', 'engine_type', 'Engine Type', 'Text', G_TECH, false],
  ['asset', 'cooling_system', 'Cooling System', 'Dropdown', G_TECH, false, { options: ['Liquid Cooled', 'Air Cooled', 'Radiator', 'Heat Exchanger'] }],
  ['asset', 'fuel_type', 'Fuel Type', 'Dropdown', G_TECH, false, { options: ['Diesel', 'Natural Gas', 'Petrol', 'LPG', 'Bi-Fuel'], defaultValue: 'Diesel' }],
  ['asset', 'emissions_standard', 'Emissions Standard', 'Text', G_TECH, false],
  ['asset', 'efficiency', 'Efficiency (%)', 'Number', G_TECH, false],
  ['asset', 'purchase_date', 'Purchase Date', 'Date', G_PURCHASE, false, { showInDetails: true }],
  ['asset', 'warranty_period', 'Warranty Period (Months)', 'Number', G_PURCHASE, false, { defaultValue: '12' }],
  ['asset', 'warranty_provider', 'Warranty Provider', 'Text', G_PURCHASE, false],
  ['asset', 'supplier', 'Supplier', 'Lookup', G_PURCHASE, false, { lookupSource: 'Suppliers', searchable: true }],
  ['asset', 'gps_coordinates', 'GPS Coordinates', 'Compound', G_LOCATION, false, { parts: ['Latitude', 'Longitude'] }],
  ['asset', 'assigned_department', 'Assigned Department', 'Dropdown', G_LOCATION, false, { options: ['Operations', 'Facilities', 'IT', 'Logistics', 'Security'] }],
  ['asset', 'custodian', 'Custodian', 'Lookup', G_LOCATION, false, { lookupSource: 'Users' }],
  ['asset', 'building_floor', 'Building / Floor', 'Text', G_LOCATION, false],
  ['asset', 'certification_no', 'Certification No.', 'Text', G_COMPLIANCE, false, { searchable: true }],
  ['asset', 'purchase_order_no', 'Purchase Order No.', 'Text', G_FINANCIAL, false, { searchable: true }],
  ['asset', 'insured', 'Insured', 'Checkbox', G_FINANCIAL, false, { defaultValue: 'false', status: 'Inactive' }],
  ['asset', 'depreciation_method', 'Depreciation Method', 'Dropdown', G_FINANCIAL, false, { options: ['Straight Line', 'Declining Balance', 'Units of Production'] }],
  ['asset', 'purchase_cost', 'Purchase Cost (AED)', 'Number', G_FINANCIAL, false],
  ['asset', 'maintenance_frequency', 'Maintenance Frequency', 'Dropdown', G_MAINTENANCE, false, { options: ['Weekly', 'Monthly', 'Quarterly', 'Half-Yearly', 'Yearly'] }],
  ['asset', 'last_service_date', 'Last Service Date', 'Date', G_MAINTENANCE, false],
  ['asset', 'next_service_date', 'Next Service Date', 'Date', G_MAINTENANCE, false],
  ['asset', 'service_provider', 'Service Provider', 'Lookup', G_MAINTENANCE, false, { lookupSource: 'Suppliers' }],
  ['asset', 'amc_contract_no', 'AMC Contract No.', 'Text', G_MAINTENANCE, false],
  ['asset', 'runtime_hours', 'Runtime Hours', 'Number', G_MAINTENANCE, false],
  ['asset', 'maintenance_notes', 'Maintenance Notes', 'Text Area', G_MAINTENANCE, false],
  // Component fields have their own form and aren't part of the asset field groups.
  ['component', 'serial_number', 'Serial Number', 'Text', '', true, { showInDetails: true, tooltip: 'Enter the manufacturer serial number of the component.' }],
  ['component', 'manufacturing_date', 'Manufacturing Date', 'Date', '', false, { showInDetails: true }],
  ['component', 'warranty_period', 'Warranty Period (Months)', 'Number', '', false, { showInDetails: true, defaultValue: '12' }],
  ['component', 'material', 'Material', 'Dropdown', '', false, { options: ['Cast Iron', 'Steel', 'Stainless Steel', 'Aluminium', 'Copper', 'Composite'] }],
  ['component', 'dimensions', 'Dimensions (L x W x H)', 'Compound', '', false, { parts: ['Length (mm)', 'Width (mm)', 'Height (mm)'], showInDetails: true }],
  ['component', 'weight', 'Weight (kg)', 'Number', '', false, { showInDetails: true }],
  ['component', 'operating_temperature', 'Operating Temperature', 'Text', '', false, { tooltip: 'Range in °C, e.g. -20 to 55' }],
  ['component', 'ip_rating', 'IP Rating', 'Dropdown', '', false, { options: ['IP44', 'IP54', 'IP55', 'IP65', 'IP66', 'IP67'] }],
  ['component', 'supplier', 'Supplier', 'Lookup', '', false, { lookupSource: 'Suppliers', searchable: true }],
  ['component', 'installation_position', 'Installation Position', 'Dropdown', '', false, { options: ['Front', 'Rear', 'Left', 'Right', 'Top', 'Bottom', 'Internal'] }],
  ['category', 'fuel_tank_capacity', 'Fuel Tank Capacity (L)', 'Number', G_OPERATIONAL, true, { appliesTo: 'Power Generation' }],
  ['category', 'rated_output', 'Rated Output', 'Compound', G_OPERATIONAL, true, { appliesTo: 'Power Generation', parts: ['kVA', 'kW', 'Power Factor'] }],
  ['category', 'cooling_capacity_tr', 'Cooling Capacity (TR)', 'Number', G_OPERATIONAL, true, { appliesTo: 'HVAC' }],
  ['category', 'ram_gb', 'RAM (GB)', 'Number', G_OPERATIONAL, false, { appliesTo: 'IT Equipment' }],
  ['category', 'ip_address', 'IP Address', 'Text', G_OPERATIONAL, false, { appliesTo: 'IT Equipment', searchable: true }],
  ['category', 'ip_rating', 'IP Rating', 'Dropdown', G_COMPLIANCE, false, { appliesTo: 'Electrical Equipment', options: ['IP54', 'IP55', 'IP65', 'IP66'] }],
  ['category', 'load_capacity_kg', 'Load Capacity (kg)', 'Number', G_OPERATIONAL, true, { appliesTo: 'Material Handling' }],
];

function buildMockFields(): CustomField[] {
  const counters = new Map<string, number>();
  return SEEDS.map(([scope, name, label, type, group, required, extra = {}]) => {
    const key = `${scope}|${group}`;
    const order = counters.get(key) ?? 0;
    counters.set(key, order + 1);
    return {
      id: nextId++,
      scope,
      appliesTo: extra.appliesTo ?? (scope === 'component' ? ALL_COMPONENTS : ''),
      name,
      label,
      type,
      group,
      required,
      showInDetails: extra.showInDetails ?? false,
      searchable: extra.searchable ?? false,
      defaultValue: extra.defaultValue ?? '',
      options: extra.options ?? [],
      parts: extra.parts ?? [],
      lookupSource: extra.lookupSource ?? '',
      description: extra.description ?? '',
      tooltip: extra.tooltip ?? '',
      status: extra.status ?? 'Active',
      order,
    };
  });
}

const DEFAULT_GROUPS: Omit<FieldGroup, 'order'>[] = [
  { name: G_GENERAL, description: 'Basic asset information fields', status: 'Active' },
  { name: G_PURCHASE, description: 'Purchase, vendor and warranty details', status: 'Active' },
  { name: G_TECH, description: 'Technical and performance details', status: 'Active' },
  { name: G_LOCATION, description: 'Location, site and assignment details', status: 'Active' },
  { name: G_COMPLIANCE, description: 'Compliance and regulatory fields', status: 'Active' },
  { name: G_OPERATIONAL, description: 'Daily operational information', status: 'Inactive' },
  { name: G_FINANCIAL, description: 'Cost, depreciation and financial details', status: 'Active' },
  { name: G_MAINTENANCE, description: 'Maintenance related custom fields', status: 'Active' },
];

type LayoutSeed = [name: string, assetType: string, description: string, status: FieldStatus, sections: [string, string[]][]];

const SYS = ['asset_name', 'asset_tag'];
const LAYOUT_SEEDS: LayoutSeed[] = [
  ['Generator Layout', 'Generator', 'Layout for Generator assets', 'Active', [
    [G_GENERAL, [...SYS, 'manufacturer', 'model', 'serial_number', 'installation_date']],
    [G_TECH, ['power_rating', 'voltage', 'frequency', 'fuel_type', 'cooling_system', 'engine_type']],
    [G_PURCHASE, ['purchase_date', 'warranty_expiry', 'supplier', 'warranty_period']],
    [G_COMPLIANCE, ['last_calibration']],
    [G_MAINTENANCE, ['maintenance_frequency']],
  ]],
  ['Electrical Equipment', 'Electrical', 'Layout for electrical assets', 'Active', [
    [G_GENERAL, [...SYS, 'manufacturer', 'model', 'serial_number']],
    [G_TECH, ['voltage', 'frequency', 'power_rating']],
    [G_COMPLIANCE, ['ip_rating', 'certification_no', 'last_calibration', 'next_calibration']],
  ]],
  ['HVAC Equipment', 'HVAC', 'Layout for HVAC assets', 'Active', [
    [G_GENERAL, [...SYS, 'manufacturer', 'model', 'serial_number', 'installation_date']],
    [G_TECH, ['cooling_system', 'power_rating', 'efficiency']],
    [G_MAINTENANCE, ['maintenance_frequency', 'last_service_date', 'next_service_date', 'service_provider']],
  ]],
  ['IT Equipment', 'IT Asset', 'Layout for IT assets', 'Active', [
    [G_GENERAL, [...SYS, 'manufacturer', 'model', 'serial_number']],
    [G_LOCATION, ['location_zone', 'custodian', 'assigned_department']],
    [G_PURCHASE, ['purchase_date', 'warranty_expiry', 'supplier']],
  ]],
  ['Vehicle Layout', 'Vehicle', 'Layout for vehicle assets', 'Inactive', [
    [G_GENERAL, [...SYS, 'manufacturer', 'model']],
    [G_TECH, ['fuel_type', 'engine_type', 'emissions_standard']],
    [G_MAINTENANCE, ['runtime_hours', 'last_service_date', 'next_service_date']],
  ]],
  ['General Asset Layout', 'All Assets', 'Default layout for general assets', 'Active', [
    [G_GENERAL, [...SYS, 'asset_condition', 'criticality', 'serial_number']],
    [G_LOCATION, ['location_zone', 'gps_coordinates']],
    [G_PURCHASE, ['purchase_date', 'warranty_expiry']],
  ]],
];

function buildMockLayouts(fields: CustomField[]): FieldLayout[] {
  const idOf = (name: string) => [...SYSTEM_FIELDS, ...fields].find((f) => f.name === name && f.scope !== 'component')?.id;
  return LAYOUT_SEEDS.map(([name, assetType, description, status, sections], i) => ({
    id: i + 1,
    name,
    assetType,
    description,
    status,
    sections: sections.map(([group, names]) => ({
      group,
      fieldIds: names.map(idOf).filter((id): id is number => id !== undefined),
    })),
  }));
}

const TAB_META: Record<PageTab, TabMeta> = {
  asset: { label: 'Asset Fields', noun: null, subtitle: 'Create and manage additional fields for asset records to capture industry or organization specific information.' },
  component: { label: 'Component Fields', noun: 'Component', subtitle: 'Create and manage custom fields for asset components to capture component specific information.' },
  category: { label: 'Category Specific Fields', noun: 'Category', subtitle: 'Create fields that only appear on assets of a specific asset category.' },
  compound: { label: 'Compound Fields', noun: 'Compound', subtitle: 'Combine several related values into one field, such as dimensions or coordinates.' },
  groups: { label: 'Field Groups', noun: 'Groups', subtitle: 'Organize custom fields into logical groups for easier data entry and management.' },
  layouts: { label: 'Field Layouts', noun: 'Layouts', subtitle: 'Design and manage field layouts to organize how custom fields are presented for different asset types.' },
};

/** "Warranty Expiry Date" → "warranty_expiry_date". */
function toFieldName(label: string): string {
  return label
    .toLowerCase()
    .replace(/\(.*?\)/g, ' ')
    .replace(/[^a-z0-9]+/g, '_')
    .replace(/^_+|_+$/g, '')
    .replace(/^(\d)/, 'f_$1');
}

function splitLines(text: string): string[] {
  return [...new Set(text.split('\n').map((o) => o.trim()).filter(Boolean))];
}

function downloadJson(data: unknown, fileName: string): void {
  const url = URL.createObjectURL(new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' }));
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}

@Component({
  selector: 'app-asset-custom-fields',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule, CfFieldLayouts],
  templateUrl: './asset-custom-fields.html',
  styleUrl: './asset-custom-fields.css',
})
export class AssetCustomFields {
  readonly icons = {
    CalendarDays, Check, ChevronDown, ChevronLeft, ChevronRight, ChevronUp, Cog, Combine, Copy, Download, EllipsisVertical, Eye,
    FolderPlus, GripVertical, Inbox, LayoutList, Layers, ListTree, Plus, Power, PowerOff, Search, Settings, SquarePen, Trash2, Upload, X,
  };

  @ViewChild(CfFieldLayouts) private layoutsView?: CfFieldLayouts;

  readonly tabs = (Object.keys(TAB_META) as PageTab[]).map((key) => ({ key, label: TAB_META[key].label }));
  readonly fieldTypes = FIELD_TYPES;
  readonly assetCategories = ASSET_CATEGORIES;
  readonly allComponents = ALL_COMPONENTS;
  readonly componentTypes = COMPONENT_TYPES;
  readonly lookupSources = Object.keys(LOOKUP_SOURCES);
  readonly pageSizeOptions = [10, 25, 50];
  readonly descriptionMax = DESCRIPTION_MAX;
  readonly tooltipMax = TOOLTIP_MAX;
  readonly groupDescriptionMax = GROUP_DESCRIPTION_MAX;
  readonly typeClass = typeClass;
  readonly scopeOptions: { value: FieldScope; label: string }[] = [
    { value: 'asset', label: 'Asset' },
    { value: 'component', label: 'Component' },
    { value: 'category', label: 'Asset Category' },
  ];

  fields: CustomField[] = buildMockFields();
  groups: FieldGroup[] = DEFAULT_GROUPS.map((g, order) => ({ ...g, order }));
  layouts: FieldLayout[] = buildMockLayouts(this.fields);
  activeTab: PageTab = 'asset';

  // List filters (shared by the field and group lists)
  search = '';
  typeFilter: '' | FieldType = '';
  statusFilter: '' | FieldStatus = '';
  groupFilter = '';
  /** Asset category (category tab) or component type (component tab). */
  appliesToFilter = '';
  usedInFilter: '' | FieldScope = '';
  page = 1;
  pageSize = 10;
  checked = new Set<number>();
  checkedGroups = new Set<FieldGroup>();
  openMenu: string | null = null;

  // Field side panel (open on load, editing the first field, as in the design)
  panelOpen = true;
  editing: CustomField | null = null;
  form: FieldForm = this.emptyForm('asset');
  formSubmitted = false;
  private nameTouched = false;
  addingGroup = false;
  newGroupName = '';

  // Group side panel
  groupForm: GroupForm | null = null;
  groupSubmitted = false;
  assignSearch = '';
  private dragIndex: number | null = null;
  dropIndex: number | null = null;
  // "Add Fields" picker
  pickerOpen = false;
  pickerSearch = '';
  pickerChecked = new Set<number>();

  deleteTargets: CustomField[] = [];
  groupDeleteTargets: FieldGroup[] = [];

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  constructor() {
    this.openFirstOfTab();
  }

  // ===== Tabs =====

  get meta(): TabMeta {
    return TAB_META[this.activeTab];
  }

  get isFieldTab(): boolean {
    return this.activeTab !== 'groups' && this.activeTab !== 'layouts';
  }

  /** Scope new fields get on this tab (the compound tab defaults to asset). */
  get scope(): FieldScope {
    return this.activeTab === 'component' || this.activeTab === 'category' ? this.activeTab : 'asset';
  }

  get listNoun(): string {
    return this.activeTab === 'asset' ? 'custom' : this.activeTab;
  }

  /** "Fields", "Groups" or "Layouts", for the import/export buttons. */
  get headerNoun(): string {
    return this.isFieldTab ? 'Fields' : this.meta.noun!;
  }

  get addLabel(): string {
    if (this.activeTab === 'groups') return 'Add Field Group';
    if (this.activeTab === 'layouts') return 'Add Field Layout';
    return this.meta.noun ? `Add ${this.meta.noun} Field` : 'Add Custom Field';
  }

  selectTab(tab: PageTab): void {
    if (tab === this.activeTab) return;
    this.activeTab = tab;
    this.search = '';
    this.typeFilter = '';
    this.statusFilter = '';
    this.groupFilter = '';
    this.appliesToFilter = '';
    this.usedInFilter = '';
    this.page = 1;
    this.checked.clear();
    this.checkedGroups.clear();
    this.openMenu = null;
    this.openFirstOfTab();
  }

  /** Each tab opens with a record in its side panel, as in the designs. */
  private openFirstOfTab(): void {
    this.closePanel();
    this.groupForm = null;
    if (this.isFieldTab) {
      const first = this.tabFields[0];
      if (first) this.openEdit(first);
    } else if (this.activeTab === 'groups') {
      const g = this.groups.find((x) => x.name === G_TECH) ?? this.sortedGroups[0];
      if (g) this.openGroupEdit(g);
    }
  }

  onAdd(): void {
    if (this.activeTab === 'groups') this.openGroupEdit();
    else if (this.activeTab === 'layouts') this.layoutsView?.openAdd();
    else this.openAdd();
  }

  onExport(): void {
    if (this.activeTab === 'groups') this.exportGroups();
    else if (this.activeTab === 'layouts') this.layoutsView?.exportLayouts();
    else this.exportFields();
  }

  async onImportPicked(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    let parsed: unknown;
    try {
      parsed = JSON.parse(await file.text());
    } catch {
      this.showToast(`${file.name} isn't valid JSON.`);
      return;
    }
    if (this.activeTab === 'groups') this.importGroups(parsed, file.name);
    else if (this.activeTab === 'layouts') this.layoutsView?.importLayouts(parsed, file.name);
    else this.importFields(parsed, file.name);
  }

  // ===== Field list =====

  get tabFields(): CustomField[] {
    return this.activeTab === 'compound'
      ? this.fields.filter((f) => f.type === 'Compound')
      : this.fields.filter((f) => f.scope === this.activeTab);
  }

  get filtered(): CustomField[] {
    const q = this.search.trim().toLowerCase();
    return this.tabFields.filter(
      (f) =>
        (!q || [f.name, f.label, f.group, f.description, f.appliesTo, ...f.parts].some((v) => v.toLowerCase().includes(q))) &&
        (!this.typeFilter || f.type === this.typeFilter) &&
        (!this.statusFilter || f.status === this.statusFilter) &&
        (!this.groupFilter || f.group === this.groupFilter) &&
        // A field for all components also applies to the filtered component type.
        (!this.appliesToFilter || f.appliesTo === this.appliesToFilter || f.appliesTo === ALL_COMPONENTS) &&
        (!this.usedInFilter || f.scope === this.usedInFilter),
    );
  }

  /** Rows of whichever list the tab shows; drives paging for both fields and groups. */
  private get listLength(): number {
    return this.activeTab === 'groups' ? this.filteredGroups.length : this.filtered.length;
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.listLength / this.pageSize));
  }

  get currentPage(): number {
    return Math.min(this.page, this.totalPages);
  }

  get pageRows(): CustomField[] {
    const start = (this.currentPage - 1) * this.pageSize;
    return this.filtered.slice(start, start + this.pageSize);
  }

  get rangeStart(): number {
    return this.listLength ? (this.currentPage - 1) * this.pageSize + 1 : 0;
  }

  get rangeEnd(): number {
    return Math.min(this.currentPage * this.pageSize, this.listLength);
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
    return this.pageRows.length > 0 && this.pageRows.every((f) => this.checked.has(f.id));
  }

  get someChecked(): boolean {
    return !this.allChecked && this.pageRows.some((f) => this.checked.has(f.id));
  }

  toggleAll(): void {
    const on = !this.allChecked;
    this.pageRows.forEach((f) => (on ? this.checked.add(f.id) : this.checked.delete(f.id)));
  }

  toggleRow(id: number): void {
    if (this.checked.has(id)) this.checked.delete(id);
    else this.checked.add(id);
  }

  get checkedFields(): CustomField[] {
    return this.fields.filter((f) => this.checked.has(f.id));
  }

  setStatusForChecked(status: FieldStatus): void {
    const rows = this.checkedFields;
    rows.forEach((f) => (f.status = status));
    this.checked.clear();
    this.showToast(`${rows.length} field${rows.length === 1 ? '' : 's'} ${status === 'Active' ? 'activated' : 'deactivated'}.`);
  }

  usedIn(f: CustomField): string {
    return f.scope === 'asset' ? 'Asset' : f.scope === 'component' ? `Component · ${f.appliesTo}` : `Category · ${f.appliesTo}`;
  }

  // ===== Menus =====

  toggleMenu(key: string, event: MouseEvent): void {
    event.stopPropagation();
    this.openMenu = this.openMenu === key ? null : key;
  }

  @HostListener('document:click')
  closeMenu(): void {
    this.openMenu = null;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.openMenu = null;
    this.deleteTargets = [];
    this.groupDeleteTargets = [];
    this.pickerOpen = false;
  }

  // ===== Field side panel =====

  private defaultAppliesTo(scope: FieldScope): string {
    if (scope === 'category') return (this.activeTab === 'category' && this.appliesToFilter) || ASSET_CATEGORIES[0];
    if (scope === 'component') return (this.activeTab === 'component' && this.appliesToFilter) || ALL_COMPONENTS;
    return '';
  }

  private emptyForm(scope: FieldScope, type: FieldType = 'Text'): FieldForm {
    return {
      scope,
      appliesTo: this.defaultAppliesTo(scope),
      name: '',
      label: '',
      type,
      group: scope === 'component' ? '' : G_GENERAL,
      required: false,
      showInDetails: true,
      searchable: false,
      defaultValue: '',
      optionsText: '',
      partsText: '',
      lookupSource: type === 'Lookup' ? this.lookupSources[0] : '',
      description: '',
      tooltip: '',
      status: 'Active',
    };
  }

  get panelTitle(): string {
    const noun = this.activeTab === 'compound' ? 'Compound' : this.form.scope === 'asset' ? 'Custom' : this.form.scope === 'component' ? 'Component' : 'Category';
    return `${this.editing ? 'Edit' : 'Add'} ${noun} Field`;
  }

  /** Component field types are fixed once created, as in the design; the compound tab only holds compound fields. */
  get typeLocked(): boolean {
    return this.activeTab === 'compound' || (this.form.scope === 'component' && !!this.editing);
  }

  get isComponentForm(): boolean {
    return this.form.scope === 'component';
  }

  openAdd(): void {
    this.openMenu = null;
    if (!this.isFieldTab) this.selectTab('asset');
    this.editing = null;
    this.form = this.emptyForm(this.scope, this.activeTab === 'compound' ? 'Compound' : 'Text');
    this.nameTouched = false;
    this.formSubmitted = false;
    this.addingGroup = false;
    this.panelOpen = true;
  }

  openEdit(f: CustomField, duplicate = false): void {
    this.openMenu = null;
    this.editing = duplicate ? null : f;
    this.form = {
      scope: f.scope,
      appliesTo: f.appliesTo,
      name: duplicate ? this.uniqueCopyName(f) : f.name,
      label: duplicate ? `${f.label} (Copy)` : f.label,
      type: f.type,
      group: f.group,
      required: f.required,
      showInDetails: f.showInDetails,
      searchable: f.searchable,
      defaultValue: f.defaultValue,
      optionsText: f.options.join('\n'),
      partsText: f.parts.join('\n'),
      lookupSource: f.lookupSource,
      description: f.description,
      tooltip: f.tooltip,
      status: f.status,
    };
    this.nameTouched = true;
    this.formSubmitted = false;
    this.addingGroup = false;
    this.panelOpen = true;
  }

  private uniqueCopyName(f: CustomField): string {
    let n = 2;
    while (this.isNameTaken(`${f.name}_${n}`, f.scope, f.appliesTo, null)) n++;
    return `${f.name}_${n}`;
  }

  closePanel(): void {
    this.panelOpen = false;
    this.editing = null;
  }

  onLabelChange(): void {
    if (!this.nameTouched) this.form.name = toFieldName(this.form.label);
  }

  onNameInput(): void {
    this.nameTouched = true;
  }

  onScopeChange(): void {
    this.form.appliesTo = this.defaultAppliesTo(this.form.scope);
    this.form.group = this.form.scope === 'component' ? '' : this.form.group || G_GENERAL;
  }

  onTypeChange(): void {
    // A default that doesn't fit the new type would be rejected later; clear it.
    this.form.defaultValue = this.form.type === 'Checkbox' ? 'false' : '';
    if (this.form.type === 'Lookup' && !this.form.lookupSource) this.form.lookupSource = this.lookupSources[0];
  }

  get formOptions(): string[] {
    return splitLines(this.form.optionsText);
  }

  get formParts(): string[] {
    return splitLines(this.form.partsText);
  }

  lookupValues(source: string): string[] {
    return LOOKUP_SOURCES[source] ?? [];
  }

  isNameTaken(name: string, scope: FieldScope, appliesTo: string, except: CustomField | null): boolean {
    if (scope !== 'component' && SYSTEM_FIELDS.some((f) => f.name === name)) return true;
    return this.fields.some(
      (f) => f !== except && f.scope === scope && f.name === name && (scope !== 'category' || f.appliesTo === appliesTo),
    );
  }

  get nameError(): string | null {
    const name = this.form.name.trim();
    if (!name) return 'Field name is required.';
    if (!NAME_PATTERN.test(name)) return 'Use lowercase letters, digits and underscores, starting with a letter.';
    if (this.isNameTaken(name, this.form.scope, this.form.appliesTo, this.editing)) return 'Another field already uses this name.';
    return null;
  }

  get defaultError(): string | null {
    const v = this.form.defaultValue.trim();
    if (!v) return null;
    if (this.form.type === 'Number' && !Number.isFinite(Number(v))) return 'Default must be a number.';
    if (this.form.type === 'Dropdown' && !this.formOptions.includes(v)) return 'Default must be one of the options.';
    return null;
  }

  get optionsError(): string | null {
    return this.form.type === 'Dropdown' && this.formOptions.length < 2 ? 'Add at least two options, one per line.' : null;
  }

  get partsError(): string | null {
    return this.form.type === 'Compound' && this.formParts.length < 2 ? 'Add at least two sub-fields, one per line.' : null;
  }

  saveField(): void {
    this.formSubmitted = true;
    const f = this.form;
    if (this.nameError || !f.label.trim() || this.defaultError || this.optionsError || this.partsError) return;
    if (f.description.length > DESCRIPTION_MAX || f.tooltip.length > TOOLTIP_MAX) return;
    if (f.scope !== 'asset' && !f.appliesTo) return;

    const values = {
      scope: f.scope,
      appliesTo: f.scope === 'asset' ? '' : f.appliesTo,
      name: f.name.trim(),
      label: f.label.trim(),
      type: f.type,
      group: f.scope === 'component' ? '' : f.group,
      required: f.required,
      showInDetails: f.showInDetails,
      searchable: f.searchable,
      defaultValue: f.type === 'Compound' ? '' : f.defaultValue.trim(),
      options: f.type === 'Dropdown' ? this.formOptions : [],
      parts: f.type === 'Compound' ? this.formParts : [],
      lookupSource: f.type === 'Lookup' ? f.lookupSource : '',
      description: f.description.trim(),
      tooltip: f.tooltip.trim(),
      status: f.status,
    };

    if (this.editing) {
      const groupChanged = this.editing.group !== values.group;
      Object.assign(this.editing, values);
      if (groupChanged) this.editing.order = this.nextOrder(values.group);
      this.showToast(`${values.label} saved.`);
    } else {
      const created: CustomField = { id: nextId++, ...values, order: this.nextOrder(values.group) };
      this.fields.push(created);
      this.editing = created;
      this.showToast(`${values.label} added.`);
    }
    this.formSubmitted = false;
  }

  private nextOrder(group: string): number {
    const orders = this.fields.filter((f) => f.group === group).map((f) => f.order);
    return orders.length ? Math.max(...orders) + 1 : 0;
  }

  addGroupInline(): void {
    const name = this.newGroupName.trim();
    if (!name) return;
    if (!this.groups.some((g) => g.name.toLowerCase() === name.toLowerCase())) {
      this.groups.push({ name, description: '', status: 'Active', order: this.groups.length });
    }
    this.form.group = this.groups.find((g) => g.name.toLowerCase() === name.toLowerCase())!.name;
    this.newGroupName = '';
    this.addingGroup = false;
  }

  toggleStatus(f: CustomField): void {
    this.openMenu = null;
    f.status = f.status === 'Active' ? 'Inactive' : 'Active';
    if (this.editing === f) this.form.status = f.status;
    this.showToast(`${f.label} ${f.status === 'Active' ? 'activated' : 'deactivated'}.`);
  }

  // ===== Delete fields =====

  askDelete(targets: CustomField[]): void {
    this.openMenu = null;
    this.deleteTargets = targets;
  }

  confirmDelete(): void {
    const targets = this.deleteTargets;
    const ids = new Set(targets.map((f) => f.id));
    this.fields = this.fields.filter((f) => !ids.has(f.id));
    targets.forEach((f) => this.checked.delete(f.id));
    // Deleted fields also leave every layout and the open group draft.
    this.layouts.forEach((l) => l.sections.forEach((s) => (s.fieldIds = s.fieldIds.filter((id) => !ids.has(id)))));
    if (this.groupForm) this.groupForm.fieldIds = this.groupForm.fieldIds.filter((id) => !ids.has(id));
    if (this.editing && ids.has(this.editing.id)) this.closePanel();
    this.deleteTargets = [];
    this.showToast(targets.length === 1 ? `${targets[0].label} deleted.` : `${targets.length} fields deleted.`);
  }

  // ===== Field import / export =====

  private exportFields(): void {
    const rows = this.tabFields.map(({ id, order, ...rest }) => rest);
    downloadJson({ version: 1, fields: rows }, `custom-fields-${this.activeTab}.json`);
    this.showToast(`Exported ${rows.length} field${rows.length === 1 ? '' : 's'}.`);
  }

  private importFields(parsed: unknown, fileName: string): void {
    const list: unknown[] = Array.isArray(parsed) ? parsed : Array.isArray((parsed as { fields?: unknown[] })?.fields) ? (parsed as { fields: unknown[] }).fields : [];
    let added = 0;
    let skipped = 0;
    for (const raw of list) {
      const f = this.toImportedField(raw);
      if (!f || this.isNameTaken(f.name, f.scope, f.appliesTo, null)) {
        skipped++;
        continue;
      }
      if (f.group && !this.groups.some((g) => g.name === f.group)) {
        this.groups.push({ name: f.group, description: '', status: 'Active', order: this.groups.length });
      }
      f.order = this.nextOrder(f.group);
      this.fields.push(f);
      added++;
    }
    this.showToast(
      list.length
        ? `Imported ${added} field${added === 1 ? '' : 's'}${skipped ? `; skipped ${skipped} invalid or duplicate` : ''}.`
        : `No fields found in ${fileName}.`,
    );
  }

  /** Accepts the shape produced by Export; anything missing a valid name, label or type is rejected. */
  private toImportedField(raw: unknown): CustomField | null {
    if (!raw || typeof raw !== 'object') return null;
    const r = raw as Record<string, unknown>;
    const str = (v: unknown) => (typeof v === 'string' ? v.trim() : '');
    const list = (v: unknown) => (Array.isArray(v) ? v.map(str).filter(Boolean) : []);
    const name = str(r['name']);
    const label = str(r['label']);
    const type = str(r['type']) as FieldType;
    const scope = (['asset', 'component', 'category'].includes(str(r['scope'])) ? str(r['scope']) : this.scope) as FieldScope;
    const appliesTo = str(r['appliesTo']) || (scope === 'component' ? ALL_COMPONENTS : '');
    const parts = list(r['parts']);
    const lookupSource = str(r['lookupSource']);
    if (!NAME_PATTERN.test(name) || !label || !FIELD_TYPES.includes(type)) return null;
    if (scope === 'category' && !appliesTo) return null;
    if (type === 'Compound' && parts.length < 2) return null;
    if (type === 'Lookup' && !LOOKUP_SOURCES[lookupSource]) return null;
    return {
      id: nextId++,
      scope,
      appliesTo: scope === 'asset' ? '' : appliesTo,
      name,
      label,
      type,
      group: scope === 'component' ? '' : str(r['group']),
      required: r['required'] === true,
      showInDetails: r['showInDetails'] !== false,
      searchable: r['searchable'] === true,
      defaultValue: type === 'Compound' ? '' : str(r['defaultValue']),
      options: list(r['options']),
      parts: type === 'Compound' ? parts : [],
      lookupSource: type === 'Lookup' ? lookupSource : '',
      description: str(r['description']).slice(0, DESCRIPTION_MAX),
      tooltip: str(r['tooltip']).slice(0, TOOLTIP_MAX),
      status: r['status'] === 'Inactive' ? 'Inactive' : 'Active',
      order: 0,
    };
  }

  // ===== Field groups list =====

  get sortedGroups(): FieldGroup[] {
    return [...this.groups].sort((a, b) => a.order - b.order);
  }

  get filteredGroups(): FieldGroup[] {
    const q = this.search.trim().toLowerCase();
    return this.sortedGroups.filter(
      (g) => (!q || [g.name, g.description].some((v) => v.toLowerCase().includes(q))) && (!this.statusFilter || g.status === this.statusFilter),
    );
  }

  get groupPageRows(): FieldGroup[] {
    const start = (this.currentPage - 1) * this.pageSize;
    return this.filteredGroups.slice(start, start + this.pageSize);
  }

  get allGroupsChecked(): boolean {
    return this.groupPageRows.length > 0 && this.groupPageRows.every((g) => this.checkedGroups.has(g));
  }

  get someGroupsChecked(): boolean {
    return !this.allGroupsChecked && this.groupPageRows.some((g) => this.checkedGroups.has(g));
  }

  toggleAllGroups(): void {
    const on = !this.allGroupsChecked;
    this.groupPageRows.forEach((g) => (on ? this.checkedGroups.add(g) : this.checkedGroups.delete(g)));
  }

  get checkedGroupList(): FieldGroup[] {
    return [...this.checkedGroups];
  }

  toggleGroupRow(g: FieldGroup): void {
    if (this.checkedGroups.has(g)) this.checkedGroups.delete(g);
    else this.checkedGroups.add(g);
  }

  /** Field groups organise the asset form, so component fields aren't counted. */
  groupFields(group: string): CustomField[] {
    return this.fields.filter((f) => f.scope !== 'component' && f.group === group).sort((a, b) => a.order - b.order);
  }

  setGroupStatus(groups: FieldGroup[], status: FieldStatus): void {
    this.openMenu = null;
    groups.forEach((g) => (g.status = status));
    if (this.groupForm?.original && groups.includes(this.groupForm.original)) this.groupForm.status = status;
    this.checkedGroups.clear();
    this.showToast(
      groups.length === 1
        ? `${groups[0].name} ${status === 'Active' ? 'activated' : 'deactivated'}.`
        : `${groups.length} groups ${status === 'Active' ? 'activated' : 'deactivated'}.`,
    );
  }

  // ===== Group side panel =====

  openGroupEdit(g?: FieldGroup): void {
    this.openMenu = null;
    this.groupForm = {
      original: g ?? null,
      name: g?.name ?? '',
      description: g?.description ?? '',
      status: g?.status ?? 'Active',
      fieldIds: g ? this.groupFields(g.name).map((f) => f.id) : [],
    };
    this.groupSubmitted = false;
    this.assignSearch = '';
  }

  closeGroupPanel(): void {
    this.groupForm = null;
  }

  get groupNameError(): string | null {
    const e = this.groupForm;
    if (!e) return null;
    const name = e.name.trim();
    if (!name) return 'Group name is required.';
    if (this.groups.some((g) => g !== e.original && g.name.toLowerCase() === name.toLowerCase())) return 'A group with this name already exists.';
    return null;
  }

  fieldById(id: number): CustomField | undefined {
    return this.fields.find((f) => f.id === id);
  }

  /** Assigned fields in draft order, narrowed by the panel search; `index` is the position in the full draft. */
  get assignedRows(): { field: CustomField; index: number }[] {
    const q = this.assignSearch.trim().toLowerCase();
    return (this.groupForm?.fieldIds ?? [])
      .map((id, index) => ({ field: this.fieldById(id)!, index }))
      .filter((r) => r.field && (!q || [r.field.name, r.field.label, r.field.type].some((v) => v.toLowerCase().includes(q))));
  }

  unassign(index: number): void {
    this.groupForm?.fieldIds.splice(index, 1);
  }

  moveAssigned(from: number, to: number): void {
    const ids = this.groupForm?.fieldIds;
    if (!ids || to < 0 || to >= ids.length || from === to) return;
    const [id] = ids.splice(from, 1);
    ids.splice(to, 0, id);
  }

  onAssignDragStart(event: DragEvent, index: number): void {
    this.dragIndex = index;
    event.dataTransfer?.setData('text/plain', String(index));
    if (event.dataTransfer) event.dataTransfer.effectAllowed = 'move';
  }

  onAssignDragOver(event: DragEvent, index: number): void {
    if (this.dragIndex === null) return;
    event.preventDefault();
    this.dropIndex = index;
  }

  onAssignDrop(event: DragEvent, index: number): void {
    event.preventDefault();
    if (this.dragIndex !== null) this.moveAssigned(this.dragIndex, index);
    this.onAssignDragEnd();
  }

  onAssignDragEnd(): void {
    this.dragIndex = null;
    this.dropIndex = null;
  }

  saveGroup(): void {
    this.groupSubmitted = true;
    const e = this.groupForm;
    if (!e || this.groupNameError || e.description.length > GROUP_DESCRIPTION_MAX) return;
    const name = e.name.trim();
    let group = e.original;
    if (group) {
      const old = group.name;
      if (old !== name) {
        // Everything that refers to the group by name follows the rename.
        this.fields.filter((f) => f.group === old).forEach((f) => (f.group = name));
        this.layouts.forEach((l) => l.sections.filter((s) => s.group === old).forEach((s) => (s.group = name)));
        if (this.form.group === old) this.form.group = name;
        if (this.groupFilter === old) this.groupFilter = name;
      }
      Object.assign(group, { name, description: e.description.trim(), status: e.status });
    } else {
      group = { name, description: e.description.trim(), status: e.status, order: this.groups.length };
      this.groups.push(group);
    }
    // Fields removed from the draft become unassigned; fields added move here from their old group.
    const ids = new Set(e.fieldIds);
    this.groupFields(name).filter((f) => !ids.has(f.id)).forEach((f) => (f.group = ''));
    e.fieldIds.forEach((id, order) => {
      const f = this.fieldById(id);
      if (f) Object.assign(f, { group: name, order });
    });
    if (this.editing && this.editing.scope !== 'component') this.form.group = this.editing.group;
    e.original = group;
    this.groupSubmitted = false;
    this.showToast(`Group "${name}" saved.`);
  }

  // ===== "Add Fields" picker =====

  openPicker(): void {
    this.pickerOpen = true;
    this.pickerSearch = '';
    this.pickerChecked.clear();
  }

  get pickerRows(): CustomField[] {
    const assigned = new Set(this.groupForm?.fieldIds ?? []);
    const q = this.pickerSearch.trim().toLowerCase();
    return this.fields.filter(
      (f) =>
        f.scope !== 'component' &&
        !assigned.has(f.id) &&
        (!q || [f.name, f.label, f.group, f.appliesTo].some((v) => v.toLowerCase().includes(q))),
    );
  }

  togglePick(id: number): void {
    if (this.pickerChecked.has(id)) this.pickerChecked.delete(id);
    else this.pickerChecked.add(id);
  }

  addPicked(): void {
    if (!this.groupForm) return;
    this.groupForm.fieldIds.push(...this.pickerRows.filter((f) => this.pickerChecked.has(f.id)).map((f) => f.id));
    this.pickerOpen = false;
  }

  // ===== Delete groups =====

  askDeleteGroups(groups: FieldGroup[]): void {
    this.openMenu = null;
    this.groupDeleteTargets = groups;
  }

  get groupDeleteFieldCount(): number {
    return this.groupDeleteTargets.reduce((n, g) => n + this.fields.filter((f) => f.group === g.name).length, 0);
  }

  /** Deleting a group unassigns its fields and removes its section from every layout. */
  confirmDeleteGroups(): void {
    const targets = this.groupDeleteTargets;
    const names = new Set(targets.map((g) => g.name));
    const unassigned = this.groupDeleteFieldCount;
    this.fields.filter((f) => names.has(f.group)).forEach((f) => (f.group = ''));
    this.layouts.forEach((l) => (l.sections = l.sections.filter((s) => !names.has(s.group))));
    this.groups = this.groups.filter((g) => !targets.includes(g));
    this.sortedGroups.forEach((g, i) => (g.order = i));
    targets.forEach((g) => this.checkedGroups.delete(g));
    if (names.has(this.form.group)) this.form.group = '';
    if (this.groupForm?.original && targets.includes(this.groupForm.original)) this.groupForm = null;
    this.groupDeleteTargets = [];
    const what = targets.length === 1 ? `Group "${targets[0].name}"` : `${targets.length} groups`;
    this.showToast(`${what} deleted${unassigned ? `; ${unassigned} field${unassigned === 1 ? '' : 's'} unassigned` : ''}.`);
  }

  // ===== Group import / export =====

  private exportGroups(): void {
    const rows = this.sortedGroups.map((g) => ({
      name: g.name,
      description: g.description,
      status: g.status,
      fields: this.groupFields(g.name).map((f) => (f.scope === 'category' ? { name: f.name, appliesTo: f.appliesTo } : { name: f.name })),
    }));
    downloadJson({ version: 1, groups: rows }, 'field-groups.json');
    this.showToast(`Exported ${rows.length} group${rows.length === 1 ? '' : 's'}.`);
  }

  /** Adds groups whose names are new; listed fields that exist are moved into them. */
  private importGroups(parsed: unknown, fileName: string): void {
    const list: unknown[] = Array.isArray(parsed) ? parsed : Array.isArray((parsed as { groups?: unknown[] })?.groups) ? (parsed as { groups: unknown[] }).groups : [];
    const str = (v: unknown) => (typeof v === 'string' ? v.trim() : '');
    let added = 0;
    let skipped = 0;
    for (const raw of list) {
      const r = (raw && typeof raw === 'object' ? raw : {}) as Record<string, unknown>;
      const name = str(r['name']);
      if (!name || this.groups.some((g) => g.name.toLowerCase() === name.toLowerCase())) {
        skipped++;
        continue;
      }
      this.groups.push({
        name,
        description: str(r['description']).slice(0, GROUP_DESCRIPTION_MAX),
        status: r['status'] === 'Inactive' ? 'Inactive' : 'Active',
        order: this.groups.length,
      });
      const refs = Array.isArray(r['fields']) ? (r['fields'] as unknown[]) : [];
      refs.forEach((ref, order) => {
        const x = (ref && typeof ref === 'object' ? ref : {}) as Record<string, unknown>;
        const f = this.fields.find(
          (c) => c.scope !== 'component' && c.name === str(x['name']) && (c.scope === 'asset' || c.appliesTo === str(x['appliesTo'])),
        );
        if (f) Object.assign(f, { group: name, order });
      });
      added++;
    }
    this.showToast(
      list.length
        ? `Imported ${added} group${added === 1 ? '' : 's'}${skipped ? `; skipped ${skipped} invalid or existing` : ''}.`
        : `No groups found in ${fileName}.`,
    );
  }

  trackById(_: number, row: { id: number }): number {
    return row.id;
  }

  showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 3000);
  }
}
