import { Component, ElementRef, HostListener, ViewChild, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { Coords } from 'shared-ui';
import {
  AssetAdditionalInformation, AssetBasicInformation, AssetClassification, AssetLocationAssignment, AssetPurchaseWarranty, AssetRegistrationSection,
  AssetRegistrationSectionBody, AssetRegistrationService,
} from '../services/asset-registration/asset-registration';
import {
  AssetDropdownService, AssetRegistrationOptions, DropdownOption, LocationHierarchy, textOptions,
} from '../services/asset-dropdowns/asset-dropdown';
import { Observable, concat, defer, forkJoin, last, map, of, shareReplay, switchMap, tap } from 'rxjs';
import {
  LucideAngularModule,
  ArrowLeft,
  ArrowRight,
  Box,
  Building,
  Calendar,
  Camera,
  Check,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  ClipboardList,
  CloudUpload,
  Cog,
  Cpu,
  Download,
  EllipsisVertical,
  FileCog,
  FileImage,
  FileSpreadsheet,
  FileText,
  Flag,
  Folder,
  House,
  IdCard,
  Images,
  Info,
  LayoutGrid,
  ListChecks,
  Map as MapIcon,
  MapPin,
  Maximize,
  Minimize,
  Minus,
  Paperclip,
  Pencil,
  Plus,
  Printer,
  QrCode,
  RotateCcw,
  ScanBarcode,
  ScanLine,
  Search,
  Settings,
  ShieldCheck,
  ShoppingCart,
  Square,
  SquareCheck,
  Tag,
  Trash2,
  Upload,
  User,
  Users,
  UsersRound,
  Wifi,
  X,
} from 'lucide-angular';
import { OptionMenu } from './option-menu';
import { ScanPrintDialog, ScanPrintResult } from './scan-print-dialog';

interface Step {
  title: string;
  subtitle: string;
}

interface Option {
  label: string;
  value: string;
}

interface StatusOption extends Option {
  color: string;
}

interface SubTypeOption extends Option {
  image?: string;
}

interface Person extends Option {
  initials: string;
  department: string;
}

/** A team; parentId is its department's id when the list comes from master data. */
interface TeamOption extends Option {
  parentId?: string;
}

/** Default badge colors by label, for statuses, conditions and criticalities that have no color of their own. */
const BADGE_COLORS: Record<string, string> = {
  active: '#16a34a',
  inactive: '#94a3b8',
  'in storage': '#3b82f6',
  'under maintenance': '#f59e0b',
  'in repair': '#f97316',
  'out of service': '#dc2626',
  disposed: '#64748b',
  retired: '#64748b',
  excellent: '#15803d',
  good: '#16a34a',
  fair: '#f59e0b',
  poor: '#dc2626',
  critical: '#dc2626',
  high: '#f97316',
  medium: '#f59e0b',
  low: '#16a34a',
};
const DEFAULT_BADGE_COLOR = '#94a3b8';

/** Supplier / Vendor and Manufacturer choices used while Master Data Management has none (the asset saves the label). */
const DEFAULT_SUPPLIERS: DropdownOption[] = [
  { label: 'Global Supplies LLC', value: 'Global Supplies LLC' },
  { label: 'Prime Industrial Traders', value: 'Prime Industrial Traders' },
  { label: 'TechSource Distributors', value: 'TechSource Distributors' },
];
const DEFAULT_MANUFACTURERS: DropdownOption[] = [
  { label: 'Caterpillar', value: 'Caterpillar' },
  { label: 'Siemens', value: 'Siemens' },
  { label: 'Schneider Electric', value: 'Schneider Electric' },
];

/** Criticality choices used while Master Data Management has none of its own (the asset saves the label). */
const DEFAULT_CRITICALITIES: DropdownOption[] = [
  { label: 'High', value: 'High' },
  { label: 'Medium', value: 'Medium', isDefault: true },
  { label: 'Low', value: 'Low' },
];

/** Form fields that hold a location, top to bottom. */
type LocationLevelKey = 'country' | 'site' | 'building' | 'floor' | 'zone' | 'room';

/** Tree levels as the Locating module shows them; the backend's Outer Zone level is skipped. */
type LocationKind = 'project' | 'country' | 'site' | 'building' | 'outdoorZone' | 'floor' | 'zone' | 'subZone';

interface LocationNode {
  /** Master-data record id; countries, which have no record, use their name. */
  id: string;
  label: string;
  kind: LocationKind;
  children: LocationNode[];
  coords: Coords;
  /** Uploaded map image of a floor, zone or sub zone. */
  mapImage?: string;
}

interface LocationModel {
  tree: LocationNode[];
  index: Record<string, LocationNode>;
  parentOf: Record<string, string>;
}

interface LocationLevel {
  key: LocationLevelKey;
  label: string;
  placeholder: string;
  required: boolean;
}

/** A child location drawn on the map, in percent of the image. */
interface PlanPin {
  node: LocationNode;
  x: number;
  y: number;
}

interface AssetImage {
  src: string;
  alt: string;
}

/** A row of the Custom Fields table; organisation fields are fixed, user-added ones can be renamed and removed. */
interface CustomField {
  label: string;
  type: 'text' | 'select' | 'date' | 'textarea';
  value: string;
  options?: Option[];
  maxLength?: number;
  userAdded?: boolean;
}

/** An uploaded file: purchase documents (step 4) or attachments (step 5). */
interface AssetDocument {
  name: string;
  /** Bytes. */
  size: number;
  /** ISO date (yyyy-mm-dd). */
  uploadedAt: string;
  /** Present for files picked in this session. */
  file?: File;
}

type DocumentList = 'purchaseDocuments' | 'attachments';

/** Step 5 technical specifications; units sit next to their value. */
interface TechnicalSpecs {
  powerRating: string;
  powerUnit: string;
  voltage: string;
  voltageUnit: string;
  frequency: string;
  frequencyUnit: string;
  phase: string;
  fuelType: string;
  fuelTankCapacity: string;
  fuelTankUnit: string;
  length: string;
  width: string;
  height: string;
  dimensionUnit: string;
  weight: string;
  weightUnit: string;
  temperatureRange: string;
  temperatureUnit: string;
  ipRating: string;
  noiseLevel: string;
  noiseUnit: string;
  lifespan: string;
  lifespanUnit: string;
  remarks: string;
}

/** One cell of the technical specifications grid. */
interface SpecField {
  label: string;
  kind: 'unit' | 'select' | 'dimensions';
  key: keyof TechnicalSpecs;
  inputType?: 'number' | 'text';
  unitKey?: keyof TechnicalSpecs;
  units?: Option[];
  options?: Option[];
}

interface OperationalInfo {
  operatingHours: string;
  operatingHoursUnit: string;
  operatingShift: string;
  utilization: string;
  maintenanceFrequency: string;
  maintenanceFrequencyUnit: string;
  lastMaintenance: string;
  nextMaintenance: string;
  complianceStandards: string[];
}

type InfoTab = 'technical' | 'operational' | 'custom' | 'attachments';

type DocumentType = 'pdf' | 'sheet' | 'image' | 'doc';

/** All registration fields; the form starts blank. */
interface AssetForm {
  // Step 1: Basic Information
  assetName: string;
  assetTag: string;
  barcode: string;
  rfidTag: string;
  category: string;
  subCategory: string;
  assetType: string;
  model: string;
  manufacturer: string;
  serialNumber: string;
  status: string;
  condition: string;
  criticality: string;
  description: string;
  // Step 2: Classification
  assetSubType: string;
  functionPurpose: string;
  costCenter: string;
  businessUnit: string;
  taggingMethod: string;
  complianceCategory: string;
  lifecycleStage: string;
  keywords: string[];
  // Step 3: Location & Assignment (location fields hold LocationNode ids)
  country: string;
  site: string;
  building: string;
  floor: string;
  zone: string;
  room: string;
  department: string;
  assignedTo: string;
  ownershipType: string;
  responsibleTeam: string;
  assignmentStart: string;
  assignmentEnd: string;
  setAsCurrent: boolean;
  // Step 4: Purchase & Warranty (manufacturer is shared with step 1)
  purchaseOrder: string;
  invoiceNumber: string;
  vendor: string;
  purchaseDate: string;
  deliveryDate: string;
  /** Plain number as typed, e.g. "250000"; shown formatted. */
  purchaseCost: string;
  quantity: string;
  unit: string;
  currency: string;
  taxRate: string;
  paymentTerms: string;
  procurementMethod: string;
  projectReference: string;
  remarks: string;
  warrantyType: string;
  warrantyStart: string;
  warrantyPeriod: string;
  warrantyPeriodUnit: string;
  amcIncluded: boolean;
  supportCoverage: string;
  serviceProvider: string;
  warrantyReference: string;
  // Step 5: Additional Information
  specs: TechnicalSpecs;
  operations: OperationalInfo;
}

const DESCRIPTION_MAX = 500;
const REMARKS_MAX = 500;
const NOTES_MAX = 200;
const DOCUMENT_MAX_BYTES = 10 * 1024 * 1024;
const DOCUMENT_EXTENSIONS = ['pdf', 'jpg', 'jpeg', 'png', 'doc', 'docx', 'xls', 'xlsx'];
const QR_SIZE = 25;
const PLAN_ZOOM_MIN = 1;
const PLAN_ZOOM_MAX = 3;
const PLAN_ZOOM_STEP = 0.5;

/** Options whose label and value are the same, e.g. units('kVA', 'kW'). */
function units(...labels: string[]): Option[] {
  return labels.map((label) => ({ label, value: label }));
}

const LOCATION_LEVELS: LocationLevelKey[] = ['country', 'site', 'building', 'floor', 'zone', 'room'];

/** Which form field a tree level fills; projects fill none. */
const LEVEL_OF_KIND: Record<LocationKind, LocationLevelKey | null> = {
  project: null,
  country: 'country',
  site: 'site',
  building: 'building',
  outdoorZone: 'zone',
  floor: 'floor',
  zone: 'zone',
  subZone: 'room',
};

const LOCATION_KIND_LABELS: Record<LocationKind, string> = {
  project: 'Project',
  country: 'Country',
  site: 'Site',
  building: 'Building',
  outdoorZone: 'Outdoor Zone',
  floor: 'Floor',
  zone: 'Zone',
  subZone: 'Room',
};

/** Levels that can carry their own map image. */
const MAP_LEVEL_KINDS: LocationKind[] = ['floor', 'zone', 'outdoorZone', 'subZone'];

/** Floor plan the Locating module shows when a level has no uploaded map. */
const DEFAULT_MAP_IMAGE = 'map1.png';

/** Master data has no map coordinates yet; the floor plan lays pins with equal coordinates out on a grid. */
const NO_COORDS: Coords = { lat: 0, lng: 0, zoom: 0 };

const sameName = (a?: string, b?: string) => !!a && !!b && a.trim().toLowerCase() === b.trim().toLowerCase();

/**
 * Builds the step 3 tree from the master-data location APIs: Country -> Site -> Building -> Floor -> Zone -> Room.
 * Countries come from each site's country name. A zone without a floor hangs off its building, or off its site as an
 * outdoor zone. Rooms (master "locations") name their zone / floor / building instead of linking by id.
 */
function buildMasterLocationTree(h: LocationHierarchy): LocationNode[] {
  const node = (id: string, label: string, kind: LocationKind): LocationNode => ({ id, label, kind, children: [], coords: NO_COORDS });
  const byId: Record<string, LocationNode> = {};
  const add = (parent: LocationNode | undefined, child: LocationNode) => {
    byId[child.id] = child;
    parent?.children.push(child);
  };

  const countries: LocationNode[] = [];
  for (const site of h.sites) {
    const countryName = site.country?.trim() || 'Unspecified';
    let country = countries.find((c) => sameName(c.label, countryName));
    if (!country) countries.push((country = node(countryName, countryName, 'country')));
    add(country, node(site.value, site.label, 'site'));
  }
  for (const b of h.buildings) if (b.parentId && byId[b.parentId]) add(byId[b.parentId], node(b.value, b.label, 'building'));
  for (const f of h.floors) if (f.parentId && byId[f.parentId]) add(byId[f.parentId], node(f.value, f.label, 'floor'));
  for (const z of h.zones) {
    const floor = z.parentId ? byId[z.parentId] : undefined;
    const building = z.buildingId ? byId[z.buildingId] : undefined;
    const site = z.siteId ? byId[z.siteId] : undefined;
    if (floor) add(floor, node(z.value, z.label, 'zone'));
    else if (building) add(building, node(z.value, z.label, 'zone'));
    else if (site) add(site, node(z.value, z.label, 'outdoorZone'));
  }

  // Rooms: find the parent by name, narrowing by building and floor when given
  const all = Object.values(byId);
  const named = (kind: LocationKind[], name?: string) => all.filter((n) => kind.includes(n.kind) && sameName(n.label, name));
  const under = (n: LocationNode, ancestor?: LocationNode[]) => !ancestor?.length || ancestor.some((a) => contains(a, n));
  const contains = (a: LocationNode, n: LocationNode): boolean => a === n || a.children.some((c) => contains(c, n));
  for (const room of h.rooms) {
    const buildings = room.buildingName ? named(['building'], room.buildingName) : undefined;
    const floors = room.floorName ? named(['floor'], room.floorName).filter((f) => under(f, buildings)) : undefined;
    const parent =
      named(['zone', 'outdoorZone'], room.zoneName).find((z) => under(z, floors) && under(z, buildings)) ?? floors?.[0] ?? buildings?.[0];
    if (parent) add(parent, node(room.value, room.label, 'subZone'));
  }
  return countries;
}

function buildLocationModel(tree: LocationNode[]): LocationModel {
  const index: Record<string, LocationNode> = {};
  const parentOf: Record<string, string> = {};
  const walk = (nodes: LocationNode[], parentId?: string) =>
    nodes.forEach((node) => {
      index[node.id] = node;
      if (parentId) parentOf[node.id] = parentId;
      walk(node.children, node.id);
    });
  walk(tree);
  return { tree, index, parentOf };
}

@Component({
  standalone: true,
  selector: 'app-asset-registration',
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule, OptionMenu, ScanPrintDialog],
  templateUrl: './asset-registration.html',
  // Split by step, in cascade order, to keep each stylesheet under the anyComponentStyle budget (20 kB)
  styleUrls: [
    './asset-registration.css',
    './asset-registration-classification-location.css',
    './asset-registration-purchase-additional.css',
  ],
})
export class AssetRegistration {
  readonly icons = {
    ArrowLeft, ArrowRight, Box, Building, Calendar, Camera, Check, ChevronDown, ChevronLeft, ChevronRight, ClipboardList,
    CloudUpload, Cog, Cpu, Download, EllipsisVertical, FileCog, FileImage, FileSpreadsheet, FileText, Flag, Folder, House,
    IdCard, Images, Info, LayoutGrid, ListChecks, MapIcon, MapPin, Maximize, Minimize, Minus, Paperclip, Pencil, Plus,
    Printer, QrCode, RotateCcw,
    ScanBarcode, ScanLine, Search, Settings, ShieldCheck, ShoppingCart, Square, SquareCheck, Tag, Trash2, Upload, User,
    Users, UsersRound, Wifi, X,
  };

  readonly descriptionMax = DESCRIPTION_MAX;

  readonly steps: Step[] = [
    { title: 'Basic Information', subtitle: 'Asset details and identification' },
    { title: 'Classification', subtitle: 'Category and type' },
    { title: 'Location & Assignment', subtitle: 'Where and with whom' },
    { title: 'Purchase & Warranty', subtitle: 'Cost and warranty details' },
    { title: 'Additional Information', subtitle: 'Custom fields and attachments' },
  ];

  currentStep = 0;
  /** Set once the user tries to leave a step with missing fields, so required-field errors only show after that. */
  submitted = false;

  form: AssetForm = {
    assetName: '',
    assetTag: '',
    barcode: '',
    rfidTag: '',
    category: '',
    subCategory: '',
    assetType: '',
    model: '',
    manufacturer: '',
    serialNumber: '',
    status: '',
    condition: '',
    criticality: '',
    description: '',
    assetSubType: '',
    functionPurpose: '',
    costCenter: '',
    businessUnit: '',
    taggingMethod: '',
    complianceCategory: '',
    lifecycleStage: '',
    keywords: [],
    country: '',
    site: '',
    building: '',
    floor: '',
    zone: '',
    room: '',
    department: '',
    assignedTo: '',
    ownershipType: '',
    responsibleTeam: '',
    assignmentStart: '',
    assignmentEnd: '',
    setAsCurrent: true,
    purchaseOrder: '',
    invoiceNumber: '',
    vendor: '',
    purchaseDate: '',
    deliveryDate: '',
    purchaseCost: '',
    quantity: '1',
    unit: '',
    currency: '',
    taxRate: '',
    paymentTerms: '',
    procurementMethod: '',
    projectReference: '',
    remarks: '',
    warrantyType: '',
    warrantyStart: '',
    warrantyPeriod: '',
    warrantyPeriodUnit: '',
    amcIncluded: false,
    supportCoverage: '',
    serviceProvider: '',
    warrantyReference: '',
    specs: {
      powerRating: '',
      powerUnit: '',
      voltage: '',
      voltageUnit: '',
      frequency: '',
      frequencyUnit: '',
      phase: '',
      fuelType: '',
      fuelTankCapacity: '',
      fuelTankUnit: '',
      length: '',
      width: '',
      height: '',
      dimensionUnit: '',
      weight: '',
      weightUnit: '',
      temperatureRange: '',
      temperatureUnit: '',
      ipRating: '',
      noiseLevel: '',
      noiseUnit: '',
      lifespan: '',
      lifespanUnit: '',
      remarks: '',
    },
    operations: {
      operatingHours: '',
      operatingHoursUnit: '',
      operatingShift: '',
      utilization: '',
      maintenanceFrequency: '',
      maintenanceFrequencyUnit: '',
      lastMaintenance: '',
      nextMaintenance: '',
      complianceStandards: [],
    },
  };

  readonly categories: Option[] = [];

  readonly subCategoriesByCategory: Record<string, Option[]> = {};

  readonly assetTypesBySubCategory: Record<string, Option[]> = {};

  /** Built-in (no API list yet), keyed by asset type name in lower case. */
  readonly assetSubTypesByType: Record<string, SubTypeOption[]> = {
    generator: [
      { label: 'Diesel Generator', value: 'diesel', image: 'subtype-diesel-generator.svg' },
      { label: 'Gas Generator', value: 'gas', image: 'subtype-gas-generator.svg' },
      { label: 'UPS Generator', value: 'ups', image: 'subtype-ups-generator.svg' },
      { label: 'Solar Generator', value: 'solar', image: 'subtype-solar-generator.svg' },
    ],
    transformer: [
      { label: 'Step-Up Transformer', value: 'step-up' },
      { label: 'Step-Down Transformer', value: 'step-down' },
      { label: 'Isolation Transformer', value: 'isolation' },
    ],
    ups: [
      { label: 'Online UPS', value: 'online' },
      { label: 'Line-Interactive UPS', value: 'line-interactive' },
    ],
  };

  /** From GET master-data/asset-models (parentId = manufacturer id), loaded when the page opens. The asset saves the model name. */
  private assetModels: DropdownOption[] = [];

  /** From GET master-data/manufacturers (value = record id), loaded when the page opens. */
  readonly manufacturers: Option[] = DEFAULT_MANUFACTURERS.map(({ label, value }) => ({ label, value }));

  readonly statuses: StatusOption[] = [
    { label: 'Active', value: 'active', color: '#16a34a' },
    { label: 'Inactive', value: 'inactive', color: '#94a3b8' },
    { label: 'Under Maintenance', value: 'maintenance', color: '#f59e0b' },
    { label: 'Retired', value: 'retired', color: '#64748b' },
  ];

  readonly conditions: StatusOption[] = [
    { label: 'Excellent', value: 'excellent', color: '#15803d' },
    { label: 'Good', value: 'good', color: '#16a34a' },
    { label: 'Fair', value: 'fair', color: '#f59e0b' },
    { label: 'Poor', value: 'poor', color: '#dc2626' },
  ];

  /** High / Medium / Low from the start, so the required field always has choices; Master Data's own replace them when it has any. */
  readonly criticalities: StatusOption[] = DEFAULT_CRITICALITIES.map((o) => ({ label: o.label, value: o.value, color: BADGE_COLORS[o.label.toLowerCase()] ?? DEFAULT_BADGE_COLOR }));
  private criticalitiesRequested = false;

  readonly functionPurposes: Option[] = [];

  /** No cost-center master data API yet; options can be added through the option menu. */
  readonly costCenters: Option[] = [];

  readonly businessUnits: Option[] = [];

  readonly taggingMethods: Option[] = [];

  readonly complianceCategories: Option[] = [];

  readonly lifecycleStages: Option[] = [];

  /** Text typed into the keywords input before it becomes a chip. */
  keywordDraft = '';

  // ===== Step 3: Location & Assignment =====

  private readonly registrationApi = inject(AssetRegistrationService);

  /** Master-data locations, shaped for the tree, the dropdowns and the map; filled when step 3 first opens. */
  private readonly locationModel = signal<LocationModel>(buildLocationModel([]));

  readonly locationLevels: LocationLevel[] = [
    { key: 'country', label: 'Country', placeholder: 'Select country', required: true },
    { key: 'site', label: 'Site', placeholder: 'Select site', required: true },
    { key: 'building', label: 'Building', placeholder: 'Select building', required: true },
    { key: 'floor', label: 'Floor', placeholder: 'Select floor', required: true },
    { key: 'zone', label: 'Zone / Area', placeholder: 'Select zone / area', required: false },
    { key: 'room', label: 'Room / Location', placeholder: 'Select room', required: false },
  ];

  /** Ids of tree nodes the user has expanded. */
  expandedLocations = new Set<string>();
  locationQuery = '';
  locationView: 'hierarchy' | 'map' = 'hierarchy';
  planZoom = PLAN_ZOOM_MIN;
  isPlanFullscreen = false;

  readonly departments: Option[] = [];
  readonly people: Person[] = [];

  readonly ownershipTypes: Option[] = [];

  readonly teams: TeamOption[] = [];


  // ===== Step 4: Purchase & Warranty =====

  readonly remarksMax = REMARKS_MAX;

  /** From GET master-data/suppliers; three sample suppliers until Master Data has its own. */
  readonly vendors: Option[] = DEFAULT_SUPPLIERS.map(({ label, value }) => ({ label, value }));

  readonly units: Option[] = [];

  readonly currencies: Option[] = [];

  /** Standard VAT per currency's country, used by the tax reset button. */
  private readonly defaultVatByCurrency: Record<string, string> = { AED: '5', SAR: '15', USD: '0', EUR: '0' };

  readonly paymentTermsOptions: Option[] = [];

  readonly procurementMethods: Option[] = [];

  readonly warrantyTypes: Option[] = [];

  readonly warrantyPeriodUnits: Option[] = [];

  readonly supportCoverages: Option[] = [];

  readonly serviceProviders: Option[] = [];

  /** True while the cost box has focus, so it shows the raw number instead of "250,000.00". */
  costEditing = false;

  purchaseDocuments: AssetDocument[] = [];
  /** The document list a file is being dragged over. */
  draggingList: DocumentList | null = null;
  /** "list:index" of the document whose "more" menu is open. */
  docMenuKey: string | null = null;

  // ===== Step 5: Additional Information =====

  readonly infoTabs: { key: InfoTab; label: string; icon: typeof Cog }[] = [
    { key: 'technical', label: 'Technical Details', icon: Cog },
    { key: 'operational', label: 'Operational Details', icon: ClipboardList },
    { key: 'custom', label: 'Custom Fields', icon: LayoutGrid },
    { key: 'attachments', label: 'Attachments', icon: Paperclip },
  ];
  /** The right column shows Operational + Custom Fields, except whichever one is open in a tab on the left. */
  activeInfoTab: InfoTab = 'technical';

  readonly technicalRemarksMax = REMARKS_MAX;
  readonly notesMax = NOTES_MAX;

  readonly specFields: SpecField[] = [
    { label: 'Power Rating', kind: 'unit', key: 'powerRating', unitKey: 'powerUnit', units: [] },
    { label: 'Voltage', kind: 'unit', key: 'voltage', unitKey: 'voltageUnit', units: [] },
    { label: 'Frequency', kind: 'unit', key: 'frequency', unitKey: 'frequencyUnit', units: [] },
    {
      label: 'Phase', kind: 'select', key: 'phase',
      options: [],
    },
    {
      label: 'Fuel Type', kind: 'select', key: 'fuelType',
      options: [],
    },
    {
      label: 'Fuel Tank Capacity', kind: 'unit', key: 'fuelTankCapacity', unitKey: 'fuelTankUnit',
      units: [],
    },
    { label: 'Dimensions (L × W × H)', kind: 'dimensions', key: 'length', unitKey: 'dimensionUnit', units: [] },
    { label: 'Weight', kind: 'unit', key: 'weight', unitKey: 'weightUnit', units: [] },
    {
      label: 'Operating Temperature Range', kind: 'unit', key: 'temperatureRange', inputType: 'text', unitKey: 'temperatureUnit',
      units: [],
    },
    {
      label: 'IP Rating', kind: 'select', key: 'ipRating',
      options: [],
    },
    { label: 'Noise Level', kind: 'unit', key: 'noiseLevel', unitKey: 'noiseUnit', units: [] },
    {
      label: 'Expected Lifespan', kind: 'unit', key: 'lifespan', unitKey: 'lifespanUnit',
      units: [],
    },
  ];

  readonly hourUnits: Option[] = [];
  readonly maintenanceUnits: Option[] = [];
  readonly operatingShifts: Option[] = [];
  readonly utilizations: Option[] = [];
  readonly complianceOptions: Option[] = [];

  customFields: CustomField[] = [
    {
      label: 'Asset Criticality Level', type: 'select', value: '',
      options: [
        { label: 'Critical', value: 'critical' },
        { label: 'High', value: 'high' },
        { label: 'Medium', value: 'medium' },
        { label: 'Low', value: 'low' },
      ],
    },
    {
      label: 'Environmental Impact', type: 'select', value: '',
      options: [{ label: 'High', value: 'high' }, { label: 'Medium', value: 'medium' }, { label: 'Low', value: 'low' }],
    },
    { label: 'Installation Date', type: 'date', value: '' },
    { label: 'Commissioning Date', type: 'date', value: '' },
    {
      label: 'Asset Color', type: 'select', value: '',
      options: units('Yellow', 'White', 'Grey', 'Black', 'Green').map((o) => ({ ...o, value: o.value.toLowerCase() })),
    },
    { label: 'Notes', type: 'textarea', value: '', maxLength: NOTES_MAX },
  ];

  attachments: AssetDocument[] = [];

  images: AssetImage[] = [];
  activeImageIndex = 0;

  @ViewChild('imageInput') imageInput?: ElementRef<HTMLInputElement>;

  /** Dark-module path of the preview QR pattern, regenerated from the asset tag. */
  qrPath = '';
  readonly qrSize = QR_SIZE;

  toastMessage = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  constructor(private readonly router: Router) {
    this.generateQr();
    this.ensureLocationSelection();
    this.loadStepDropdowns(0);
  }

  // ===== Dropdown data =====

  private readonly dropdownApi = inject(AssetDropdownService);

  /** Parent ids whose dependent list has already been fetched, so switching back does not refetch. */
  private readonly loadedSubCategories = new Set<string>();
  private readonly loadedAssetTypes = new Set<string>();
  /** Parent ids with a request in flight, so the same list is not requested twice at once. */
  private readonly pendingLists = new Set<string>();

  /** Steps whose dropdown APIs have been called, so revisiting a step does not call them again. */
  private readonly loadedSteps = new Set<number>();
  /** GET /options serves the fixed lists of every step; requested once, on the first step that needs it. */
  private registrationOptions$?: Observable<AssetRegistrationOptions>;
  private departmentsLoaded = false;

  private get options$(): Observable<AssetRegistrationOptions> {
    return (this.registrationOptions$ ??= this.dropdownApi.getRegistrationOptions().pipe(
      // A failed load comes back empty: forget it so the next step asks again instead of keeping blank lists
      tap((o) => {
        if (!Object.keys(o ?? {}).length) this.registrationOptions$ = undefined;
      }),
      shareReplay(1),
    ));
  }

  /**
   * Calls a step's APIs the first time that step is shown: only Criticality when the page opens, the rest as
   * the user moves through the steps. Sub Category, Asset Type, Assigned To and Responsible Team load when their parent
   * is chosen. A value already in the form is kept when the loaded list has it (by value or label), otherwise cleared.
   */
  private loadStepDropdowns(step: number): void {
    if (this.loadedSteps.has(step)) return;
    const loaders = [
      () => this.loadBasicInformationDropdowns(),
      () => this.loadClassificationDropdowns(),
      () => this.loadLocationAssignmentDropdowns(),
      () => this.loadPurchaseWarrantyDropdowns(),
      () => this.loadAdditionalInformationDropdowns(),
    ];
    if (!loaders[step]) return;
    this.loadedSteps.add(step);
    loaders[step]();
  }

  /** Step 1: Criticality and Manufacturer from master data (Manufacturer is also shown on step 4). Status and Condition are built-in. */
  private loadBasicInformationDropdowns(): void {
    const f = this.form;
    // No manufacturers in Master Data yet: keep the sample ones so the required field can be filled
    this.dropdownApi.getManufacturers().subscribe((list) =>
      this.swapOptions(this.manufacturers, list.length ? list : DEFAULT_MANUFACTURERS, (re) => (f.manufacturer = re(f.manufacturer))),
    );
    this.dropdownApi.getAssetModels().subscribe((list) => (this.assetModels = list));
    this.loadCriticalities();
  }

  /**
   * Model choices: the models of the chosen manufacturer (all of them when none is chosen),
   * plus the current value so a draft keeps a model that is no longer listed.
   */
  get modelOptions(): string[] {
    const maker = this.manufacturers.find((m) => m.value === this.form.manufacturer);
    const names = this.assetModels
      .filter((m) => !maker || m.parentId === maker.value)
      .map((m) => m.label);
    return [...new Set([...names, this.form.model].filter(Boolean))].sort((a, b) => a.localeCompare(b));
  }

  /** Picking a model fills its manufacturer when the manufacturer is listed. */
  onModelChange(): void {
    const model = this.assetModels.find((m) => m.label === this.form.model);
    if (model?.parentId && this.manufacturers.some((o) => o.value === model.parentId)) this.form.manufacturer = model.parentId;
  }

  /** Criticality is on steps 1 and 2: loaded once by whichever is shown first (a draft can open on step 2). */
  private loadCriticalities(): void {
    if (this.criticalitiesRequested) return;
    this.criticalitiesRequested = true;
    const f = this.form;
    // Nothing chosen yet: preselect Medium until Master Data answers
    f.criticality ||= DEFAULT_CRITICALITIES.find((c) => c.isDefault)?.value ?? '';
    this.dropdownApi.getCriticalities().subscribe((loaded) => {
      // No criticalities in Master Data yet: keep High / Medium / Low so the required field can be filled
      const list = loaded.length ? loaded : DEFAULT_CRITICALITIES;
      this.swapOptions(this.criticalities, this.withColors(list), (re) => (f.criticality = re(f.criticality)));
      // Nothing chosen (or the choice is gone): preselect the record marked defaultCriticality
      f.criticality ||= list.find((c) => c.isDefault)?.value ?? '';
    });
  }

  /** Step 2: Category, Business Unit, Department (master data) and the classification lists from /options. */
  private loadClassificationDropdowns(): void {
    const f = this.form;
    this.loadCriticalities();
    // Only top-level categories here; sub-categories load when a category is chosen
    this.dropdownApi.getAssetCategories().subscribe((list) => {
      this.swapOptions(this.categories, list.filter((c) => !c.parentId), (re) => (f.category = re(f.category)));
      this.loadSubCategories();
    });
    this.dropdownApi.getBusinessUnits().subscribe((list) =>
      this.swapOptions(this.businessUnits, list, (re) => (f.businessUnit = re(f.businessUnit))),
    );
    this.loadDepartments();
    this.options$.subscribe(({ classification: c = {} }) => {
      this.swapOptions(this.functionPurposes, textOptions(c.functionPurposes), (re) => (f.functionPurpose = re(f.functionPurpose)));
      this.swapOptions(this.taggingMethods, textOptions(c.taggingMethods), (re) => (f.taggingMethod = re(f.taggingMethod)));
      this.swapOptions(this.complianceCategories, textOptions(c.complianceCategories), (re) => (f.complianceCategory = re(f.complianceCategory)));
      this.swapOptions(this.lifecycleStages, textOptions(c.lifecycleStages), (re) => (f.lifecycleStage = re(f.lifecycleStage)));
    });
  }

  /** Step 3: location tree (country ... room), Department (people and teams follow it) and Ownership Type from /options. */
  private loadLocationAssignmentDropdowns(): void {
    const f = this.form;
    this.loadLocations();
    this.loadDepartments();
    this.options$.subscribe(({ locationAssignment: l = {} }) =>
      this.swapOptions(this.ownershipTypes, textOptions(l.ownershipTypes), (re) => (f.ownershipType = re(f.ownershipType))),
    );
  }

  /** Step 4: Vendor, Service Provider (master data) and the purchase / warranty lists from /options. */
  private loadPurchaseWarrantyDropdowns(): void {
    const f = this.form;
    // No suppliers in Master Data yet: keep the sample ones so the required field can be filled
    this.dropdownApi.getSuppliers().subscribe((list) =>
      this.swapOptions(this.vendors, list.length ? list : DEFAULT_SUPPLIERS, (re) => (f.vendor = re(f.vendor))),
    );
    this.dropdownApi.getServiceProviders().subscribe((list) =>
      this.swapOptions(this.serviceProviders, list, (re) => (f.serviceProvider = re(f.serviceProvider))),
    );
    this.options$.subscribe(({ purchaseWarranty: p = {} }) => {
      this.swapOptions(this.units, textOptions(p.quantityUnits), (re) => (f.unit = re(f.unit)));
      // "AED - UAE Dirham" keeps the currency code as its value; VAT defaults and amounts are keyed by it
      const currencies = (p.currencies ?? []).map((label) => ({ label, value: label.split(' - ')[0].trim() }));
      this.swapOptions(this.currencies, currencies, (re) => (f.currency = re(f.currency)));
      this.swapOptions(this.paymentTermsOptions, textOptions(p.paymentTerms), (re) => (f.paymentTerms = re(f.paymentTerms)));
      this.swapOptions(this.procurementMethods, textOptions(p.procurementMethods), (re) => (f.procurementMethod = re(f.procurementMethod)));
      this.swapOptions(this.warrantyTypes, textOptions(p.warrantyTypes), (re) => (f.warrantyType = re(f.warrantyType)));
      this.swapOptions(this.warrantyPeriodUnits, textOptions(p.warrantyPeriodUnits), (re) => (f.warrantyPeriodUnit = re(f.warrantyPeriodUnit)));
      this.swapOptions(this.supportCoverages, textOptions(p.supportCoverages), (re) => (f.supportCoverage = re(f.supportCoverage)));
    });
  }

  /** Step 5: spec units and selects, operational lists and compliance standards, all from /options. */
  private loadAdditionalInformationDropdowns(): void {
    const f = this.form;
    this.options$.subscribe(({ additionalInformation: a = {} }) => {
      const specLists: Partial<Record<keyof TechnicalSpecs, { units?: string[]; options?: string[] }>> = {
        powerRating: { units: a.powerRatingUnits },
        voltage: { units: a.voltageUnits },
        frequency: { units: a.frequencyUnits },
        phase: { options: a.phases },
        fuelType: { options: a.fuelTypes },
        fuelTankCapacity: { units: a.capacityUnits },
        length: { units: a.dimensionUnits },
        weight: { units: a.weightUnits },
        temperatureRange: { units: a.temperatureUnits },
        ipRating: { options: a.ipRatings },
        noiseLevel: { units: a.noiseLevelUnits },
        lifespan: { units: a.lifespanUnits },
      };
      for (const spec of this.specFields) {
        const lists = specLists[spec.key];
        const unitKey = spec.unitKey;
        if (lists?.units && unitKey) this.swapOptions((spec.units ??= []), textOptions(lists.units), (re) => (f.specs[unitKey] = re(f.specs[unitKey])));
        if (lists?.options) this.swapOptions((spec.options ??= []), textOptions(lists.options), (re) => (f.specs[spec.key] = re(f.specs[spec.key])));
      }
      const o = f.operations;
      this.swapOptions(this.hourUnits, textOptions(a.operatingHoursUnits), (re) => (o.operatingHoursUnit = re(o.operatingHoursUnit)));
      this.swapOptions(this.operatingShifts, textOptions(a.operatingShifts), (re) => (o.operatingShift = re(o.operatingShift)));
      this.swapOptions(this.utilizations, textOptions(a.utilizations), (re) => (o.utilization = re(o.utilization)));
      this.swapOptions(this.maintenanceUnits, textOptions(a.maintenanceFrequencyUnits), (re) => (o.maintenanceFrequencyUnit = re(o.maintenanceFrequencyUnit)));
      this.swapOptions(this.complianceOptions, textOptions(a.complianceStandards), (re) => {
        o.complianceStandards = o.complianceStandards.map(re).filter(Boolean);
      });
    });
  }

  /** Departments are shown on steps 2 and 3; whichever opens first loads them, then people and teams follow. */
  private loadDepartments(): void {
    if (this.departmentsLoaded) return;
    this.departmentsLoaded = true;
    const f = this.form;
    this.dropdownApi.getDepartments().subscribe((list) => {
      this.swapOptions(this.departments, list, (re) => (f.department = re(f.department)));
      this.loadDepartmentMembers();
    });
  }

  // ===== Dependent dropdowns (fetched when the parent changes) =====

  /** Category chosen: GET asset-categories/{id}/children, then keep the sub-category (matched by name) or pick the first. */
  private loadSubCategories(): void {
    const categoryId = this.form.category;
    if (!categoryId) return;
    if (this.loadedSubCategories.has(categoryId)) return this.pickSubCategory(categoryId, []);
    if (this.pendingLists.has(`category:${categoryId}`)) return;
    this.pendingLists.add(`category:${categoryId}`);
    const previous = Object.values(this.subCategoriesByCategory).flat();
    this.dropdownApi.getSubCategories(categoryId).subscribe((list) => {
      this.pendingLists.delete(`category:${categoryId}`);
      this.loadedSubCategories.add(categoryId);
      this.subCategoriesByCategory[categoryId] = list.map(({ label, value }) => ({ label, value }));
      this.pickSubCategory(categoryId, previous);
    });
  }

  private pickSubCategory(categoryId: string, previous: Option[]): void {
    if (this.form.category !== categoryId) return; // user moved on while the request was running
    const list = this.subCategories;
    this.form.subCategory = this.relabel(previous, list, this.form.subCategory) || (list[0]?.value ?? '');
    this.loadAssetTypes();
  }

  /** Sub category chosen: GET asset-types?categoryId={id}, then keep the type (matched by name) or pick the first. */
  private loadAssetTypes(): void {
    const subCategoryId = this.form.subCategory;
    if (!subCategoryId) return;
    if (this.loadedAssetTypes.has(subCategoryId)) return this.pickAssetType(subCategoryId, []);
    if (this.pendingLists.has(`subCategory:${subCategoryId}`)) return;
    this.pendingLists.add(`subCategory:${subCategoryId}`);
    const previous = Object.values(this.assetTypesBySubCategory).flat();
    const categoryId = this.form.category;
    // Types linked to the sub-category; when it has none, those linked to its parent category
    this.dropdownApi
      .getAssetTypes(subCategoryId)
      .pipe(switchMap((list) => (list.length || !categoryId ? of(list) : this.dropdownApi.getAssetTypes(categoryId))))
      .subscribe((list) => {
        this.pendingLists.delete(`subCategory:${subCategoryId}`);
        this.loadedAssetTypes.add(subCategoryId);
        this.assetTypesBySubCategory[subCategoryId] = list.map(({ label, value }) => ({ label, value }));
        this.pickAssetType(subCategoryId, previous);
      });
  }

  private pickAssetType(subCategoryId: string, previous: Option[]): void {
    if (this.form.subCategory !== subCategoryId) return;
    const list = this.assetTypes;
    this.form.assetType = this.relabel(previous, list, this.form.assetType) || (list[0]?.value ?? '');
    this.onAssetTypeChange();
  }

  /** Department chosen: GET employees?departmentId= and teams?departmentId=, then keep the person/team if still listed. */
  private loadDepartmentMembers(): void {
    const departmentId = this.form.department;
    if (!departmentId) return;
    forkJoin({
      employees: this.dropdownApi.getEmployees(departmentId),
      teams: this.dropdownApi.getTeams(departmentId),
    }).subscribe(({ employees, teams }) => {
      if (this.form.department !== departmentId) return;
      const f = this.form;
      const people: Person[] = employees.map((e) => ({ label: e.label, value: e.value, initials: this.initialsOf(e.label), department: departmentId }));
      this.people.splice(0, this.people.length, ...people);
      if (!people.some((p) => p.value === f.assignedTo)) f.assignedTo = '';
      this.teams.splice(0, this.teams.length, ...teams);
      if (!teams.some((t) => t.value === f.responsibleTeam)) f.responsibleTeam = teams[0]?.value ?? '';
    });
  }

  onDepartmentChange(): void {
    this.loadDepartmentMembers();
  }

  /** Swaps a list's entries in place (so template bindings keep working), then lets the caller remap form values. */
  private swapOptions<T extends Option>(list: T[], next: T[], remap: (re: (value: string) => string) => void): void {
    const old = [...list];
    list.splice(0, list.length, ...next);
    remap((value) => this.relabel(old, list, value));
  }

  /** The value in `next` for `value`: itself if listed, else the entry with the same label as in `old`, else ''. */
  private relabel(old: Option[], next: Option[], value: string): string {
    if (!value || next.some((o) => o.value === value)) return value;
    const label = old.find((o) => o.value === value)?.label ?? value;
    return next.find((o) => this.sameLabel(o.label, label))?.value ?? '';
  }

  private sameLabel(a: string, b: string): boolean {
    return a.trim().toLowerCase() === b.trim().toLowerCase();
  }

  /** Gives each option a badge color: its own from the API, else a default for its label. */
  private withColors(next: DropdownOption[]): StatusOption[] {
    return next.map((o) => ({
      label: o.label,
      value: o.value,
      color: o.color || BADGE_COLORS[o.label.trim().toLowerCase()] || DEFAULT_BADGE_COLOR,
    }));
  }

  private initialsOf(name: string): string {
    return name.split(/\s+/).filter(Boolean).slice(0, 2).map((w) => w[0].toUpperCase()).join('');
  }

  // ===== Lookups for the template =====

  // Dependent lists: the list for the chosen parent is created on first use, so options added from the ⋮ menu are kept
  get subCategories(): Option[] {
    return this.form.category ? (this.subCategoriesByCategory[this.form.category] ??= []) : [];
  }

  get assetTypes(): Option[] {
    return this.form.subCategory ? (this.assetTypesBySubCategory[this.form.subCategory] ??= []) : [];
  }

  get assetSubTypes(): SubTypeOption[] {
    const typeName = this.assetTypes.find((t) => t.value === this.form.assetType)?.label.trim().toLowerCase();
    return typeName ? (this.assetSubTypesByType[typeName] ??= []) : [];
  }

  /** Sub types that have a picture, shown as tiles in the Category Reference card. */
  get assetSubTypeTiles(): SubTypeOption[] {
    return this.assetSubTypes.filter((o) => o.image);
  }

  labelOf(options: Option[], value: string): string {
    return options.find((o) => o.value === value)?.label ?? '—';
  }

  colorOf(options: StatusOption[], value: string): string {
    return options.find((o) => o.value === value)?.color ?? '#cbd5e1';
  }

  get activeImage(): AssetImage | undefined {
    return this.images[this.activeImageIndex];
  }

  /** Thumbnails beside the main image: the next two images after the active one. */
  get thumbnails(): { image: AssetImage; index: number }[] {
    return this.images
      .map((image, index) => ({ image, index }))
      .filter(({ index }) => index !== this.activeImageIndex)
      .slice(0, 2);
  }

  onCategoryChange(): void {
    this.form.subCategory = this.subCategories[0]?.value ?? '';
    this.onSubCategoryChange();
    this.loadSubCategories();
  }

  onSubCategoryChange(): void {
    this.form.assetType = this.assetTypes[0]?.value ?? '';
    this.onAssetTypeChange();
    this.loadAssetTypes();
  }

  onAssetTypeChange(): void {
    this.form.assetSubType = this.assetSubTypes[0]?.value ?? '';
  }

  selectAssetType(value: string): void {
    if (this.form.assetType === value) return;
    this.form.assetType = value;
    this.onAssetTypeChange();
  }

  /** Scrolls a form control into view and focuses it (used by "Change Category" / "Edit" buttons). */
  focusField(id: string): void {
    const control = document.getElementById(id);
    control?.scrollIntoView({ block: 'center', behavior: 'smooth' });
    control?.focus({ preventScroll: true });
  }

  // ===== Location =====

  get locationTree(): LocationNode[] {
    return this.locationModel().tree;
  }

  /** GET master-data sites, buildings, floors, zones and locations (rooms), then builds the tree and dropdowns. */
  private loadLocations(): void {
    this.dropdownApi.getLocationHierarchy().subscribe((hierarchy) => {
      const tree = buildMasterLocationTree(hierarchy);
      this.locationModel.set(buildLocationModel(tree));
      tree.forEach((country) => this.expandedLocations.add(country.id));
      if (!tree.length) this.showToast('No locations found in master data.');
      this.ensureLocationSelection();
    });
  }

  /** Keeps the chosen location if it still exists after a reload, otherwise picks the first zone of the first floor. */
  private ensureLocationSelection(): void {
    const { index } = this.locationModel();
    const allKnown = LOCATION_LEVELS.every((level) => !this.form[level] || index[this.form[level]]);
    if (this.selectedLocation && allKnown) {
      this.selectLocation(this.selectedLocation);
      return;
    }
    const floor = this.findFirst(this.locationTree, 'floor');
    this.selectLocation(floor?.children[0] ?? floor ?? this.findFirst(this.locationTree, 'country'));
  }

  private findFirst(nodes: LocationNode[], kind: LocationKind): LocationNode | undefined {
    for (const node of nodes) {
      const found = node.kind === kind ? node : this.findFirst(node.children, kind);
      if (found) return found;
    }
    return undefined;
  }

  /** Root-to-node chain, including the node. */
  private ancestorsOf(node: LocationNode): LocationNode[] {
    const { index, parentOf } = this.locationModel();
    const chain = [node];
    for (let id = parentOf[node.id]; id; id = parentOf[id]) chain.unshift(index[id]);
    return chain;
  }

  /** Options for a location dropdown: the matching children of the nearest filled level above it. */
  locationOptions(levelIndex: number): LocationNode[] {
    const { index, tree } = this.locationModel();
    let parent: LocationNode | undefined;
    for (let i = levelIndex - 1; i >= 0 && !parent; i--) parent = index[this.form[LOCATION_LEVELS[i]]];
    const pool = parent ? parent.children : tree.flatMap((root) => (root.kind === 'project' ? root.children : [root]));
    return pool.filter((node) => LEVEL_OF_KIND[node.kind] === LOCATION_LEVELS[levelIndex]);
  }

  /** A dropdown changed: clear the levels below it and reveal the choice in the tree. */
  onLocationLevelChange(levelIndex: number): void {
    const node = this.locationModel().index[this.form[LOCATION_LEVELS[levelIndex]]];
    if (node) this.selectLocation(node);
  }

  /** Projects only group countries, so clicking one just expands or collapses it. */
  onLocationClick(node: LocationNode): void {
    if (node.kind === 'project') this.toggleLocation(node);
    else this.selectLocation(node);
  }

  /** Selects a node and fills every level down to it; deeper levels are cleared. */
  selectLocation(node: LocationNode | undefined): void {
    const previousMap = this.mapLevel?.id;
    LOCATION_LEVELS.forEach((level) => (this.form[level] = ''));
    if (!node) return;
    for (const n of this.ancestorsOf(node)) {
      const level = LEVEL_OF_KIND[n.kind];
      if (level) this.form[level] = n.id;
      this.expandedLocations.add(n.id);
    }
    if (this.mapLevel?.id !== previousMap) this.planZoom = PLAN_ZOOM_MIN;
  }

  toggleLocation(node: LocationNode): void {
    if (this.expandedLocations.has(node.id)) this.expandedLocations.delete(node.id);
    else this.expandedLocations.add(node.id);
  }

  isLocationExpanded(node: LocationNode): boolean {
    return !!this.locationQuery.trim() || this.expandedLocations.has(node.id);
  }

  /** While searching, only nodes that match (or have a matching descendant) are shown. */
  visibleLocations(nodes: LocationNode[]): LocationNode[] {
    const query = this.locationQuery.trim().toLowerCase();
    if (!query) return nodes;
    const matches = (node: LocationNode): boolean =>
      node.label.toLowerCase().includes(query) || node.children.some(matches);
    return nodes.filter(matches);
  }

  /** Same level icons as the Locating tree; floors and zones keep the check box style. */
  locationIcon(node: LocationNode) {
    switch (node.kind) {
      case 'project':
        return this.icons.Folder;
      case 'country':
        return this.icons.Flag;
      case 'site':
        return this.icons.MapIcon;
      case 'building':
        return this.icons.Building;
      default:
        return this.isCheckedLocation(node) ? this.icons.SquareCheck : this.icons.Square;
    }
  }

  /** Floors and zones show a filled check box when they are the selected location. */
  isCheckedLocation(node: LocationNode): boolean {
    return MAP_LEVEL_KINDS.includes(node.kind) && node.id === this.selectedLocation?.id;
  }

  /** Outdoor zones hang off an Area directly, so they have no building or floor. */
  get isOutdoorLocation(): boolean {
    return this.locationModel().index[this.form.zone]?.kind === 'outdoorZone';
  }

  isLevelRequired(level: LocationLevel): boolean {
    return level.required && !(this.isOutdoorLocation && (level.key === 'building' || level.key === 'floor'));
  }

  /** Nodes for the filled levels, top to bottom. */
  get locationPath(): LocationNode[] {
    const { index } = this.locationModel();
    return LOCATION_LEVELS.map((level) => index[this.form[level]]).filter((n): n is LocationNode => !!n);
  }

  get locationPathText(): string {
    return this.locationPath.map((n) => n.label).join(' > ');
  }

  /** Deepest selected node. */
  get selectedLocation(): LocationNode | undefined {
    return this.locationPath[this.locationPath.length - 1];
  }

  /**
   * The level whose map is shown: the nearest selected floor / zone / sub zone with an uploaded map,
   * otherwise the selected floor (or outdoor zone) on the default plan.
   */
  get mapLevel(): LocationNode | undefined {
    const selected = this.selectedLocation;
    if (!selected) return undefined;
    const chain = this.ancestorsOf(selected);
    return (
      [...chain].reverse().find((n) => MAP_LEVEL_KINDS.includes(n.kind) && n.mapImage) ??
      chain.find((n) => n.kind === 'floor') ??
      chain.find((n) => n.kind === 'outdoorZone')
    );
  }

  get mapImageSrc(): string {
    return this.mapLevel?.mapImage || DEFAULT_MAP_IMAGE;
  }

  /** e.g. "Street One - Third Right" for a floor, "Third Right - Second Colony" for a zone map. */
  get planTitle(): string {
    const level = this.mapLevel;
    if (!level) return 'Floor Plan';
    const parent = this.locationModel().index[this.locationModel().parentOf[level.id]];
    return parent ? `${parent.label} - ${level.label}` : level.label;
  }

  /**
   * Children of the shown map, placed the way the Locating floor plan places zone pins: their coordinates
   * normalised into the 10-90% box. Backend zones inherit their parent's coordinates, so when they all
   * coincide they are laid out on an even grid instead of stacking on one spot.
   */
  get planPins(): PlanPin[] {
    const children = this.mapLevel?.children ?? [];
    if (!children.length) return [];
    const lats = children.map((c) => c.coords.lat);
    const lngs = children.map((c) => c.coords.lng);
    const minLat = Math.min(...lats);
    const maxLat = Math.max(...lats);
    const minLng = Math.min(...lngs);
    const latRange = maxLat - minLat;
    const lngRange = Math.max(...lngs) - minLng;
    if (latRange || lngRange) {
      return children.map((node) => ({
        node,
        x: 10 + ((node.coords.lng - minLng) / (lngRange || 1)) * 80,
        y: 10 + ((maxLat - node.coords.lat) / (latRange || 1)) * 80,
      }));
    }
    const cols = Math.ceil(Math.sqrt(children.length));
    const rows = Math.ceil(children.length / cols);
    return children.map((node, i) => ({
      node,
      x: 10 + (((i % cols) + 0.5) / cols) * 80,
      y: 10 + ((Math.floor(i / cols) + 0.5) / rows) * 80,
    }));
  }

  /** The pin on the shown map that is, or contains, the selected location. */
  get selectedPin(): PlanPin | undefined {
    const selected = this.selectedLocation;
    if (!selected) return undefined;
    const chainIds = new Set(this.ancestorsOf(selected).map((n) => n.id));
    return this.planPins.find((pin) => chainIds.has(pin.node.id));
  }

  /** Callout subtitle: the selected location inside the pin, or the pin's level name. */
  pinSubtitle(pin: PlanPin): string {
    const selected = this.selectedLocation;
    return selected && selected.id !== pin.node.id ? selected.label : LOCATION_KIND_LABELS[pin.node.kind];
  }

  /** Keeps the callout inside the map near its edges. */
  popupPlacement(pin: { x: number; y: number } | undefined): Record<string, boolean> {
    return {
      'is-left': !!pin && pin.x > 65,
      'is-right': !!pin && pin.x < 30,
      'is-above': !!pin && pin.y > 60,
    };
  }

  zoomPlan(direction: 1 | -1): void {
    this.planZoom = Math.min(PLAN_ZOOM_MAX, Math.max(PLAN_ZOOM_MIN, this.planZoom + direction * PLAN_ZOOM_STEP));
  }

  get canZoomIn(): boolean {
    return this.planZoom < PLAN_ZOOM_MAX;
  }

  get canZoomOut(): boolean {
    return this.planZoom > PLAN_ZOOM_MIN;
  }

  togglePlanFullscreen(panel: HTMLElement): void {
    if (document.fullscreenElement) document.exitFullscreen();
    else panel.requestFullscreen?.().catch(() => this.showToast('Full screen is not available in this browser.'));
  }

  @HostListener('document:fullscreenchange')
  onFullscreenChange(): void {
    this.isPlanFullscreen = !!document.fullscreenElement;
  }

  // ===== Assignment =====

  get assignedPerson(): Person | undefined {
    return this.people.find((p) => p.value === this.form.assignedTo);
  }

  resetResponsibleTeam(): void {
    const department = this.form.department;
    this.form.responsibleTeam =
      this.teams.find((t) => t.parentId === department)?.value ?? this.teams[0]?.value ?? '';
    const team = this.labelOf(this.teams, this.form.responsibleTeam);
    this.showToast(this.form.responsibleTeam ? `Responsible team reset to ${team}.` : 'No default team for this department.');
  }

  /** Opens the browser date picker for the hidden native input behind a formatted date control. */
  openDatePicker(input: HTMLInputElement): void {
    try {
      input.showPicker();
    } catch {
      input.focus();
    }
  }

  // ===== Purchase =====

  /** "250000" -> "250,000.00"; empty or invalid input stays empty. */
  formatAmount(value: string | number): string {
    const amount = typeof value === 'number' ? value : parseFloat(value);
    if (value === '' || isNaN(amount)) return '';
    return amount.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  onCostInput(event: Event): void {
    this.form.purchaseCost = (event.target as HTMLInputElement).value.replace(/[^0-9.]/g, '');
  }

  /** Cost x quantity, plus VAT. */
  get totalCost(): number | '' {
    const cost = parseFloat(this.form.purchaseCost);
    if (isNaN(cost)) return '';
    const quantity = parseFloat(this.form.quantity) || 0;
    const tax = parseFloat(this.form.taxRate) || 0;
    return cost * quantity * (1 + tax / 100);
  }

  resetTaxRate(): void {
    this.form.taxRate = this.defaultVatByCurrency[this.form.currency] ?? '0';
    this.showToast(`VAT reset to ${this.form.taxRate}% for ${this.form.currency}.`);
  }

  // ===== Warranty =====

  /** Start date plus the warranty period, minus one day (5 years from 28 Jun 2026 ends 27 Jun 2031). */
  get warrantyEnd(): string {
    const period = parseInt(this.form.warrantyPeriod, 10);
    const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(this.form.warrantyStart);
    const type = this.optionLabel(this.warrantyTypes, this.form.warrantyType).toLowerCase();
    if (!match || !(period > 0) || type === 'none' || type === 'no warranty') return '';
    const [year, month, day] = match.slice(1).map(Number);
    // Unit is matched by label so both the built-in values ("years") and the API's ("Year(s)") work
    const unit = this.optionLabel(this.warrantyPeriodUnits, this.form.warrantyPeriodUnit).toLowerCase();
    const months = unit.startsWith('year') ? period * 12 : unit.startsWith('month') ? period : 0;
    const days = unit.startsWith('day') ? period : 0;
    const end = new Date(year, month - 1 + months, day - 1 + days);
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${end.getFullYear()}-${pad(end.getMonth() + 1)}-${pad(end.getDate())}`;
  }

  // ===== Purchase documents =====

  docType(doc: AssetDocument): DocumentType {
    const ext = doc.name.split('.').pop()?.toLowerCase() ?? '';
    if (ext === 'pdf') return 'pdf';
    if (['xls', 'xlsx', 'csv'].includes(ext)) return 'sheet';
    if (['jpg', 'jpeg', 'png'].includes(ext)) return 'image';
    return 'doc';
  }

  docIcon(doc: AssetDocument) {
    const icons = { pdf: this.icons.FileText, sheet: this.icons.FileSpreadsheet, image: this.icons.FileImage, doc: this.icons.FileText };
    return icons[this.docType(doc)];
  }

  formatFileSize(bytes: number): string {
    return bytes >= 1024 * 1024 ? `${(bytes / (1024 * 1024)).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
  }

  documentsOf(list: DocumentList): AssetDocument[] {
    return this[list];
  }

  openDocumentPicker(list: DocumentList): void {
    document.getElementById(`doc-input-${list}`)?.click();
  }

  onDocumentsSelected(event: Event, list: DocumentList): void {
    const input = event.target as HTMLInputElement;
    this.addDocuments(list, Array.from(input.files ?? []));
    input.value = '';
  }

  onDocDragOver(event: DragEvent, list: DocumentList): void {
    event.preventDefault();
    this.draggingList = list;
  }

  onDocDrop(event: DragEvent, list: DocumentList): void {
    event.preventDefault();
    this.draggingList = null;
    this.addDocuments(list, Array.from(event.dataTransfer?.files ?? []));
  }

  /** Adds allowed files (type + 10 MB limit) and reports the rest. */
  private addDocuments(list: DocumentList, files: File[]): void {
    const today = new Date();
    const uploadedAt = new Date(today.getTime() - today.getTimezoneOffset() * 60000).toISOString().slice(0, 10);
    const accepted: AssetDocument[] = [];
    const rejected: string[] = [];
    for (const file of files) {
      const ext = file.name.split('.').pop()?.toLowerCase() ?? '';
      if (DOCUMENT_EXTENSIONS.includes(ext) && file.size <= DOCUMENT_MAX_BYTES) {
        accepted.push({ name: file.name, size: file.size, uploadedAt, file });
      } else {
        rejected.push(file.name);
      }
    }
    this[list] = [...this[list], ...accepted];
    if (rejected.length) {
      this.showToast(`Not added (only PDF, JPG, PNG, DOC, XLS up to 10 MB): ${rejected.join(', ')}`);
    } else if (accepted.length) {
      this.showToast(`${accepted.length} file${accepted.length > 1 ? 's' : ''} added.`);
    }
  }

  downloadDocument(doc: AssetDocument): void {
    this.docMenuKey = null;
    if (!doc.file) {
      this.showToast(`${doc.name} has no file to download.`);
      return;
    }
    const url = URL.createObjectURL(doc.file);
    const link = document.createElement('a');
    link.href = url;
    link.download = doc.name;
    link.click();
    setTimeout(() => URL.revokeObjectURL(url));
  }

  removeDocument(list: DocumentList, index: number): void {
    this.docMenuKey = null;
    this[list] = this[list].filter((_, i) => i !== index);
  }

  toggleDocMenu(key: string): void {
    this.docMenuKey = this.docMenuKey === key ? null : key;
  }

  /** Closes the document menu on any click outside it. */
  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.docMenuKey !== null && !(event.target as HTMLElement).closest('.doc-menu-wrap')) {
      this.docMenuKey = null;
    }
  }

  // ===== Keywords / tags =====

  addKeyword(event?: Event): void {
    event?.preventDefault();
    const keyword = this.keywordDraft.replace(/,/g, ' ').trim();
    if (keyword && !this.form.keywords.some((k) => k.toLowerCase() === keyword.toLowerCase())) {
      this.form.keywords = [...this.form.keywords, keyword];
    }
    this.keywordDraft = '';
  }

  removeKeyword(index: number): void {
    this.form.keywords = this.form.keywords.filter((_, i) => i !== index);
  }

  onKeywordBackspace(): void {
    if (!this.keywordDraft && this.form.keywords.length) this.removeKeyword(this.form.keywords.length - 1);
  }

  // ===== Step navigation =====

  /** Required fields per step (marked with a red asterisk); steps without an entry have none. */
  private readonly requiredByStep: (keyof AssetForm)[][] = [
    ['assetName', 'assetTag', 'status'],
    ['category', 'subCategory', 'assetType', 'criticality', 'department', 'taggingMethod', 'lifecycleStage'],
    ['country', 'site', 'building', 'floor', 'department'],
    ['purchaseOrder', 'vendor', 'manufacturer', 'purchaseDate', 'purchaseCost', 'quantity'],
  ];

  isInvalid(field: keyof AssetForm): boolean {
    return (
      this.submitted &&
      this.requiredByStep.some((fields) => fields.includes(field)) &&
      this.isMissing(field)
    );
  }

  private isMissing(field: keyof AssetForm): boolean {
    if ((field === 'building' || field === 'floor') && this.isOutdoorLocation) return false;
    return !String(this.form[field]).trim();
  }

  private stepValid(step: number): boolean {
    return (this.requiredByStep[step] ?? []).every((field) => !this.isMissing(field));
  }

  /** First step before `index` with a missing required field, or -1. */
  private firstInvalidStepBefore(index: number): number {
    for (let step = 0; step < index; step++) {
      if (!this.stepValid(step)) return step;
    }
    return -1;
  }

  get isLastStep(): boolean {
    return this.currentStep === this.steps.length - 1;
  }

  goToStep(index: number): void {
    const invalidStep = this.firstInvalidStepBefore(index);
    if (invalidStep !== -1) {
      this.submitted = true;
      this.currentStep = invalidStep;
      this.showToast('Fill in the required fields first.');
      return;
    }
    this.currentStep = index;
    this.loadStepDropdowns(index);
  }

  /** Next saves the step to the draft before moving on; the last step's Submit registers the asset. */
  next(): void {
    if (this.isLastStep) {
      this.register();
      return;
    }
    if (this.registering) return;
    const step = this.currentStep;
    if (!this.stepValid(step)) {
      this.goToStep(step + 1);
      return;
    }
    this.registering = true;
    this.saveStep(step).subscribe({
      next: () => {
        this.registering = false;
        this.goToStep(step + 1);
      },
      error: (err) => {
        this.registering = false;
        this.showApiError('This step could not be saved', err);
      },
    });
  }

  previous(): void {
    if (this.currentStep > 0) this.currentStep--;
  }

  private register(): void {
    if (this.firstInvalidStepBefore(this.steps.length) !== -1) {
      this.goToStep(this.steps.length);
      return;
    }
    if (this.registering) return;
    this.registering = true;
    // Steps already saved by Next are skipped unless they changed since; a retry after a failure resumes where it stopped
    concat(...this.steps.map((_, step) => this.saveStep(step)))
      .pipe(
        last(),
        switchMap(() => this.registrationApi.submit(this.draftId)),
      )
      .subscribe({
        next: (r) => {
          this.registering = false;
          this.draftId = '';
          this.savedSteps = {};
          this.showToast(`${this.form.assetName} (${this.form.assetTag}) registered as ${r.assetRegistrationId}.`);
        },
        error: (err) => {
          this.registering = false;
          this.showApiError('The asset could not be registered', err);
        },
      });
  }

  /** Draft holding the steps saved so far; created by the first step's Next (its asset tag is then reserved). */
  private draftId = '';
  /** Body last saved for each step, so unchanged steps are not sent again. */
  private savedSteps: Partial<Record<number, string>> = {};

  private readonly stepSections: readonly AssetRegistrationSection[] = ['basic-information', 'classification', 'location-assignment', 'purchase-warranty', 'additional-information'];

  private stepBody(step: number): AssetRegistrationSectionBody[AssetRegistrationSection] {
    return [
      () => this.toBasicInformation(),
      () => this.toClassification(),
      () => this.toLocationAssignment(),
      () => this.toPurchaseWarranty(),
      () => this.toAdditionalInformation(),
    ][step]();
  }

  /** Saves one step to the draft (creating the draft on first save); emits once, without a request when nothing changed. */
  private saveStep(step: number, saveAsDraft = false): Observable<void> {
    return defer(() => {
      if (!this.draftId && step !== 0) return this.saveStep(0, true).pipe(switchMap(() => this.saveStep(step, saveAsDraft)));
      const body = this.stepBody(step);
      const json = JSON.stringify(body);
      if (this.draftId && this.savedSteps[step] === json) return of(undefined);
      const request$ = this.draftId
        ? this.registrationApi.saveSection(this.draftId, this.stepSections[step], body, saveAsDraft)
        : this.registrationApi.createDraft(body as AssetBasicInformation, saveAsDraft);
      return request$.pipe(
        map((r) => {
          this.draftId ||= r.assetRegistrationId;
          this.savedSteps[step] = json;
        }),
      );
    });
  }

  private showApiError(stage: string, err: { error?: { errors?: Record<string, string[]>; message?: string; title?: string } }): void {
    const fieldErrors = Object.entries(err?.error?.errors ?? {}).map(([field, messages]) => `${field}: ${messages.join(' ')}`);
    this.showToast(`${stage}. ${fieldErrors.join(' ') || err?.error?.message || err?.error?.title || 'Please try again.'}`);
  }

  /** Label of an option, or the raw value when it is not listed; '' stays ''. */
  private optionLabel(options: Option[], value: string): string {
    return value ? options.find((o) => o.value === value)?.label ?? value : '';
  }

  /**
   * A form value as a number, or null when blank / not a number. `type="number"` inputs hand ngModel a number once
   * edited (a string before), so both are accepted; calling `.trim()` on the number used to throw and fail the save.
   */
  private numOrNull(value: string | number | null | undefined): number | null {
    if (value === null || value === undefined) return null;
    const text = String(value).trim();
    if (text === '') return null;
    const n = Number(text);
    return Number.isFinite(n) ? n : null;
  }

  private toBasicInformation(): AssetBasicInformation {
    const f = this.form;
    return {
      assetName: f.assetName, assetTag: f.assetTag, barcode: f.barcode, rfidTagId: f.rfidTag, model: f.model,
      manufacturer: this.optionLabel(this.manufacturers, f.manufacturer), serialNumber: f.serialNumber,
      status: this.optionLabel(this.statuses, f.status), condition: this.optionLabel(this.conditions, f.condition),
      criticality: this.optionLabel(this.criticalities, f.criticality), description: f.description,
    };
  }

  private toClassification(): AssetClassification {
    const f = this.form;
    return {
      category: this.optionLabel(this.categories, f.category), subCategory: this.optionLabel(this.subCategories, f.subCategory),
      assetType: this.optionLabel(this.assetTypes, f.assetType), assetSubType: this.optionLabel(this.assetSubTypes, f.assetSubType),
      functionPurpose: this.optionLabel(this.functionPurposes, f.functionPurpose), criticality: this.optionLabel(this.criticalities, f.criticality),
      department: this.optionLabel(this.departments, f.department), costCenter: this.optionLabel(this.costCenters, f.costCenter),
      businessUnit: this.optionLabel(this.businessUnits, f.businessUnit), taggingMethod: this.optionLabel(this.taggingMethods, f.taggingMethod),
      complianceCategory: this.optionLabel(this.complianceCategories, f.complianceCategory), lifecycleStage: this.optionLabel(this.lifecycleStages, f.lifecycleStage),
      keywords: [...f.keywords],
    };
  }

  private toLocationAssignment(): AssetLocationAssignment {
    const f = this.form;
    const { index } = this.locationModel();
    const name = (id: string) => index[id]?.label ?? '';
    const deepest = [...LOCATION_LEVELS].reverse().map((level) => index[f[level]]).find(Boolean);
    const chain = deepest ? this.ancestorsOf(deepest) : [];
    const person = this.assignedPerson;
    return {
      location: {
        projectId: chain[0]?.kind === 'project' ? chain[0].id : '',
        countryId: f.country, countryName: name(f.country), siteId: f.site, siteName: name(f.site), buildingId: f.building, buildingName: name(f.building),
        floorId: f.floor, floorName: name(f.floor), zoneId: f.zone, zoneName: name(f.zone), roomId: f.room, roomName: name(f.room),
        locationPath: chain.map((n) => n.label).join(' > '),
      },
      assignment: {
        custodianDepartment: this.optionLabel(this.departments, f.department), assignedToUserId: person?.value ?? '', assignedToName: person?.label ?? '',
        ownershipType: this.optionLabel(this.ownershipTypes, f.ownershipType), costCenter: this.optionLabel(this.costCenters, f.costCenter),
        responsibleTeam: this.optionLabel(this.teams, f.responsibleTeam), assignmentStartDate: this.isoOrNull(f.assignmentStart),
        assignmentEndDate: this.isoOrNull(f.assignmentEnd), isCurrent: f.setAsCurrent,
      },
    };
  }

  // Documents need the url returned by the media upload, which this page does not call yet, so none are sent
  private toPurchaseWarranty(): AssetPurchaseWarranty {
    const f = this.form;
    const total = this.totalCost;
    return {
      purchase: {
        purchaseOrderNumber: f.purchaseOrder, invoiceNumber: f.invoiceNumber, supplierVendor: this.optionLabel(this.vendors, f.vendor),
        manufacturer: this.optionLabel(this.manufacturers, f.manufacturer), purchaseDate: this.isoOrNull(f.purchaseDate), deliveryDate: this.isoOrNull(f.deliveryDate),
        purchaseCost: this.numOrNull(f.purchaseCost), quantity: this.numOrNull(f.quantity), quantityUnit: this.optionLabel(this.units, f.unit), currency: f.currency,
        taxPercent: this.numOrNull(f.taxRate), totalCost: total === '' ? null : total, paymentTerms: this.optionLabel(this.paymentTermsOptions, f.paymentTerms),
        procurementMethod: this.optionLabel(this.procurementMethods, f.procurementMethod), projectReference: f.projectReference, remarks: f.remarks,
      },
      documents: [],
      warranty: {
        warrantyType: this.optionLabel(this.warrantyTypes, f.warrantyType), warrantyStartDate: this.isoOrNull(f.warrantyStart),
        warrantyPeriod: this.numOrNull(f.warrantyPeriod), warrantyPeriodUnit: f.warrantyPeriod ? this.optionLabel(this.warrantyPeriodUnits, f.warrantyPeriodUnit) : '',
        amcIncluded: f.amcIncluded, supportCoverage: this.optionLabel(this.supportCoverages, f.supportCoverage),
        serviceProvider: this.optionLabel(this.serviceProviders, f.serviceProvider), warrantyReference: f.warrantyReference,
      },
    };
  }

  private toAdditionalInformation(): AssetAdditionalInformation {
    const { specs, operations: o } = this.form;
    const lifespanUnits = this.specFields.find((s) => s.key === 'lifespan')?.units ?? [];
    // Dimensions, weight and lifespan have their own fields; the rest go in the specifications list
    const specifications = this.specFields
      .filter((s) => s.kind !== 'dimensions' && s.key !== 'weight' && s.key !== 'lifespan' && String(specs[s.key]).trim())
      .map((s) => ({
        name: s.label,
        value: s.options ? this.optionLabel(s.options, String(specs[s.key])) : String(specs[s.key]),
        unit: s.unitKey ? this.optionLabel(s.units ?? [], String(specs[s.unitKey])) : '',
      }));
    return {
      technical: {
        specifications, dimensionLength: this.numOrNull(specs.length), dimensionWidth: this.numOrNull(specs.width), dimensionHeight: this.numOrNull(specs.height),
        dimensionUnit: specs.dimensionUnit, weight: this.numOrNull(specs.weight), weightUnit: specs.weightUnit,
        expectedLifespan: this.numOrNull(specs.lifespan), expectedLifespanUnit: this.optionLabel(lifespanUnits, specs.lifespanUnit), technicalRemarks: specs.remarks,
      },
      operational: {
        operatingHoursPerDay: this.numOrNull(o.operatingHours), operatingHoursUnit: this.optionLabel(this.hourUnits, o.operatingHoursUnit),
        operatingShift: this.optionLabel(this.operatingShifts, o.operatingShift), utilization: this.optionLabel(this.utilizations, o.utilization),
        maintenanceFrequency: this.numOrNull(o.maintenanceFrequency), maintenanceFrequencyUnit: this.optionLabel(this.maintenanceUnits, o.maintenanceFrequencyUnit),
        lastMaintenanceDate: this.isoOrNull(o.lastMaintenance), nextMaintenanceDate: this.isoOrNull(o.nextMaintenance),
        complianceStandards: o.complianceStandards.map((c) => this.optionLabel(this.complianceOptions, c)),
      },
      customFields: this.customFields
        .filter((c) => c.label.trim())
        .map((c) => ({ fieldName: c.label.trim(), fieldType: c.type, value: c.options ? this.optionLabel(c.options, c.value) : c.value })),
      attachments: [],
    };
  }

  /** True while a Next, Save as Draft or Submit request is in flight. */
  registering = false;

  /** "yyyy-mm-dd" as an ISO timestamp, or null when empty. */
  private isoOrNull(d: string): string | null {
    return d ? new Date(`${d}T00:00:00`).toISOString() : null;
  }

  /** Saves the current step as a draft; required fields are not enforced. */
  saveDraft(): void {
    if (this.registering) return;
    this.registering = true;
    this.saveStep(this.currentStep, true).subscribe({
      next: () => {
        this.registering = false;
        this.showToast(`Draft saved${this.draftId ? ` (${this.draftId})` : ''}.`);
      },
      error: (err) => {
        this.registering = false;
        this.showApiError('The draft could not be saved', err);
      },
    });
  }

  backToList(): void {
    this.router.navigateByUrl('/assets/list');
  }

  // ===== Images =====

  openImagePicker(): void {
    this.imageInput?.nativeElement.click();
  }

  onImagesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    Array.from(input.files ?? [])
      .filter((file) => file.type.startsWith('image/'))
      .forEach((file) => {
        const reader = new FileReader();
        reader.onload = () => {
          this.images = [...this.images, { src: String(reader.result), alt: file.name }];
          this.activeImageIndex = this.images.length - 1;
        };
        reader.readAsDataURL(file);
      });
    input.value = '';
  }

  // ===== Identification =====

  /** Builds a QR-style preview pattern from the asset tag (visual placeholder, not a scannable code). */
  generateQr(): void {
    const size = QR_SIZE;
    let seed = 0;
    for (const ch of this.form.assetTag + this.form.barcode) seed = (seed * 31 + ch.charCodeAt(0)) >>> 0;
    const random = () => {
      seed = (seed * 1664525 + 1013904223) >>> 0;
      return seed / 0x100000000;
    };

    const inFinder = (x: number, y: number) =>
      (x < 8 && y < 8) || (x >= size - 8 && y < 8) || (x < 8 && y >= size - 8);
    const finderDark = (x: number, y: number) => {
      const fx = x >= size - 8 ? x - (size - 7) : x;
      const fy = y >= size - 8 ? y - (size - 7) : y;
      if (fx < 0 || fy < 0 || fx > 6 || fy > 6) return false; // separator
      const ring = Math.max(Math.abs(fx - 3), Math.abs(fy - 3));
      return ring !== 2;
    };

    let path = '';
    for (let y = 0; y < size; y++) {
      for (let x = 0; x < size; x++) {
        const dark = inFinder(x, y) ? finderDark(x, y) : random() > 0.52;
        if (dark) path += `M${x} ${y}h1v1h-1z`;
      }
    }
    this.qrPath = path;
  }

  onGenerateQr(): void {
    this.generateQr();
    this.showToast('QR code generated for ' + this.form.assetTag + '.');
  }

  scanBarcode(): void {
    this.showToast('Scanner not connected. Demo barcode kept: ' + this.form.barcode);
  }

  readRfid(): void {
    this.showToast('RFID reader not connected. Demo tag kept: ' + this.form.rfidTag);
  }

  /** Quick Actions → Submit dialog. */
  scanPrintOpen = false;

  /** Keeps what was scanned or typed in the dialog and reports the print. */
  onScanPrint(r: ScanPrintResult): void {
    this.scanPrintOpen = false;
    const changed = r.assetTag !== this.form.assetTag || r.barcode !== this.form.barcode || r.rfidTag !== this.form.rfidTag;
    this.form.assetTag = r.assetTag;
    this.form.barcode = r.barcode;
    this.form.rfidTag = r.rfidTag;
    if (changed) this.generateQr();
    this.showToast(`${r.copies} label${r.copies > 1 ? 's' : ''} for ${r.assetTag || 'the asset'} sent to ${r.printer}${changed ? '; identifiers updated on the form' : ''}.`);
  }

  printLabel(): void {
    this.showToast('Label for ' + this.form.assetTag + ' sent to printer (demo).');
  }

  // ===== Step 5 =====

  /** Compliance standards not yet chosen, for the "add" dropdown. */
  get availableCompliance(): Option[] {
    return this.complianceOptions.filter((o) => !this.form.operations.complianceStandards.includes(o.value));
  }

  addCompliance(event: Event): void {
    const select = event.target as HTMLSelectElement;
    if (select.value) {
      this.form.operations.complianceStandards = [...this.form.operations.complianceStandards, select.value];
    }
    select.value = '';
  }

  removeCompliance(value: string): void {
    this.form.operations.complianceStandards = this.form.operations.complianceStandards.filter((v) => v !== value);
  }

  addCustomField(): void {
    this.customFields = [...this.customFields, { label: '', type: 'text', value: '', userAdded: true }];
    const index = this.customFields.length - 1;
    // Wait for the new row to render, then put the cursor in its name box
    setTimeout(() => document.getElementById(`cf-name-${index}`)?.focus());
  }

  removeCustomField(index: number): void {
    this.customFields = this.customFields.filter((_, i) => i !== index);
  }

  private showToast(message: string): void {
    this.toastMessage = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toastMessage = ''), 3000);
  }
}
