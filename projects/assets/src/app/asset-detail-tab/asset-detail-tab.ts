import { Component, DestroyRef, HostListener, OnDestroy, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  LucideAngularModule,
  Activity,
  CalendarClock,
  ChevronDown,
  ClipboardList,
  Download,
  ExternalLink,
  FileText,
  HeartPulse,
  History,
  Images,
  Info,
  LayoutGrid,
  MessageSquarePlus,
  Network,
  Power,
  Printer,
  Puzzle,
  ShieldAlert,
  Timer,
  TriangleAlert,
  Wrench,
  X,
} from 'lucide-angular';
import { Subject, catchError, forkJoin, of, switchMap, tap } from 'rxjs';
import { ASSET_TYPE_ICONS, AssetCondition, AssetHeader, AssetRecord, AssetTabLink, TODAY, assetHeader, assetLocation, assetTabs, toIsoDate } from 'shared-ui';
import { blankAssetRecord } from '../shared/blank-asset';
import { AssetDetail, Assessment, loadAssetDetail } from '../asset-details/asset-details';
import {
  AssetActivityItem,
  AssetCustomFieldValue,
  AssetDetailsService,
  AssetHealthSummary,
  AssetRegistrationDto,
  customFieldValue,
  isoDay,
} from '../asset-details/asset-details.service';
import { ServiceRequestApiService, ServiceRequestListItemDto, localStamp } from '../service-request/service-request-api.service';
import { AssetComponentsService } from '../asset-components/asset-components.service';
import { MaintenanceData, dataFor } from '../asset-maintenance/asset-maintenance';
import { isOpen } from '../asset-maintenance/maintenance-sr';
import { AssetComponent, COMPONENT_ICONS, componentTreeOf, componentsOf, loadComponentTree } from '../asset-components/asset-components';
import { ComponentOverview } from '../asset-components/component-overview/component-overview';
import { CustomField } from '../asset-custom-fields/custom-fields.model';
import { CustomFieldDto, toCustomField } from '../asset-custom-fields/custom-fields.service';

/** Which asset detail tab this page is: route data { view }. */
type View = 'overview' | 'components' | 'fields' | 'history';

type HistoryKind = 'Status' | 'Condition' | 'Health' | 'Criticality' | 'Maintenance' | 'Service Request' | 'Document' | 'Image' | 'Relationship';

/** One entry of the asset's history, with the detail tab it belongs to. */
interface HistoryEvent {
  /** yyyy-MM-dd or yyyy-MM-ddTHH:mm */
  at: string;
  kind: HistoryKind;
  title: string;
  detail: string;
  by: string;
  path: string;
}

/** One label / value line of a registration section on the Overview. */
interface InfoRow {
  label: string;
  value: string;
  /** Spans the whole row (long text). */
  wide?: boolean;
}

/** A panel of the Overview built from GET /api/asset-registrations/{id}. */
interface InfoSection {
  title: string;
  rows: InfoRow[];
}

interface FieldRow {
  field: CustomField;
  value: string;
}

const VIEW_TABS: Record<View, string> = { overview: 'Overview', components: 'Components', fields: 'Custom Fields', history: 'History' };
const KIND_TONES: Record<HistoryKind, string> = {
  Status: 'good', Condition: 'warn', Health: 'good', Criticality: 'serious', Maintenance: 'warn', 'Service Request': 'serious', Document: 'good', Image: 'good', Relationship: 'warn',
};
const HISTORY_KINDS = Object.keys(KIND_TONES) as HistoryKind[];
/** Detail tab each history kind opens. */
const KIND_PATHS: Record<HistoryKind, string> = {
  Status: '/assets/status-condition', Condition: '/assets/status-condition', Health: '/assets/health-score',
  Criticality: '/assets/criticality', Maintenance: '/assets/maintenance-details', 'Service Request': '/assets/service-history',
  Document: '/assets/documents', Image: '/assets/images', Relationship: '/assets/relationships',
};

/** A Recent Activity row: a history event, or an activity API row whose type may not be a known kind. */
type ActivityRow = Omit<HistoryEvent, 'kind'> & { kind: string };

/** "25.2048, 55.2708" from a location with coordinates; undefined without them. */
function gps(loc: { latitude?: number | null; longitude?: number | null } | null | undefined): string | undefined {
  return loc?.latitude != null && loc?.longitude != null ? `${loc.latitude}, ${loc.longitude}` : undefined;
}

/** Label / name as a match key: lowercase letters and digits only. */
function fieldKey(s: string): string {
  return s.toLowerCase().replace(/[^a-z0-9]/g, '');
}

/** Same, without a trailing unit in brackets: "Power Rating (kVA)" -> "powerrating". */
function fieldBaseKey(s: string): string {
  return fieldKey(s.replace(/\([^)]*\)\s*$/, ''));
}

type StdField = [name: string, label: string, type?: CustomField['type'], required?: boolean];

/** Field groups the Custom Fields tab always shows, in this order; values come from the Asset API. */
const STANDARD_FIELD_GROUPS: { name: string; fields: StdField[] }[] = [
  {
    name: 'Purchase & Warranty',
    fields: [
      ['warranty_expiry', 'Warranty Expiry Date', 'Date', true],
      ['purchase_date', 'Purchase Date', 'Date'],
      ['warranty_period', 'Warranty Period (Months)', 'Number'],
      ['warranty_provider', 'Warranty Provider'],
      ['supplier', 'Supplier'],
    ],
  },
  {
    name: 'General Information',
    fields: [
      ['asset_condition', 'Current Condition', 'Text', true],
      ['installation_date', 'Installation Date', 'Date'],
      ['serial_number', 'Serial Number', 'Text', true],
      ['criticality', 'Criticality'],
      ['manufacturer', 'Manufacturer'],
      ['model', 'Model'],
    ],
  },
  {
    name: 'Technical Specifications',
    fields: [
      ['power_rating', 'Power Rating (kVA)', 'Number', true],
      ['voltage', 'Voltage (V)', 'Number'],
      ['frequency', 'Frequency (Hz)', 'Number'],
      ['engine_type', 'Engine Type'],
      ['cooling_system', 'Cooling System'],
      ['fuel_type', 'Fuel Type'],
      ['emissions_standard', 'Emissions Standard'],
      ['efficiency', 'Efficiency (%)', 'Number'],
    ],
  },
  {
    name: 'Financial Information',
    fields: [
      ['cost_center', 'Cost Center'],
      ['purchase_order', 'Purchase Order No.'],
      ['depreciation_method', 'Depreciation Method'],
      ['purchase_cost', 'Purchase Cost (AED)', 'Number'],
    ],
  },
  {
    name: 'Location & Assignment',
    fields: [
      ['location_zone', 'Location Zone'],
      ['gps_coordinates', 'GPS Coordinates'],
      ['assigned_department', 'Assigned Department'],
      ['custodian', 'Custodian'],
      ['building_floor', 'Building / Floor'],
    ],
  },
  {
    name: 'Compliance & Certification',
    fields: [
      ['last_calibration_date', 'Last Calibration Date', 'Date'],
      ['next_calibration_date', 'Next Calibration Date', 'Date'],
      ['certification_no', 'Certification No.'],
    ],
  },
  {
    name: 'Maintenance Information',
    fields: [
      ['maintenance_frequency', 'Maintenance Frequency'],
      ['last_service_date', 'Last Service Date', 'Date'],
      ['next_service_date', 'Next Service Date', 'Date'],
      ['service_provider', 'Service Provider'],
      ['amc_contract_no', 'AMC Contract No.'],
      ['runtime_hours', 'Runtime Hours', 'Number'],
      ['maintenance_notes', 'Maintenance Notes', 'Text Area'],
    ],
  },
  {
    name: 'Operational Details',
    fields: [
      ['fuel_tank_capacity', 'Fuel Tank Capacity (L)', 'Number', true],
      ['rated_output', 'Rated Output', 'Text', true],
    ],
  },
];

function standardField(group: string, [name, label, type = 'Text', required = false]: StdField, order: number): CustomField {
  return {
    id: `std:${name}`, scope: 'asset', appliesTo: '', appliesToId: '', name, label, type, group, groupId: '', required,
    showInDetails: true, searchable: false, defaultValue: '', options: [], parts: [], partDefs: [], lookupSource: '',
    description: '', tooltip: '', status: 'Active', order,
  };
}

/** A field from the asset custom-fields API as the tab's field definition (only what the tab reads is filled). */
function apiCustomField(f: AssetCustomFieldValue): CustomField {
  const types = ['Text', 'Text Area', 'Number', 'Date', 'Dropdown', 'Checkbox', 'Compound', 'Lookup'];
  const key = f.type.toLowerCase().replace(/[\s_-]/g, '');
  const aliases: Record<string, string> = { select: 'Dropdown', multiselect: 'Dropdown', boolean: 'Checkbox', bool: 'Checkbox', textarea: 'Text Area', datetime: 'Date' };
  const type = aliases[key] ?? types.find((t) => t.toLowerCase().replace(/\s/g, '') === key) ?? 'Text';
  return {
    id: f.id, scope: 'asset', appliesTo: '', appliesToId: '', name: f.name, label: f.label, type: type as CustomField['type'],
    group: f.group, groupId: '', required: f.required, showInDetails: true, searchable: false, defaultValue: '', options: [],
    parts: [], partDefs: [], lookupSource: '', description: '', tooltip: '', status: 'Active', order: f.order,
  };
}

/** API fields grouped by field group, each group in field order. */
function groupFieldValues(fields: AssetCustomFieldValue[]): { name: string; rows: FieldRow[] }[] {
  const groups = new Map<string, FieldRow[]>();
  for (const f of fields) {
    if (!groups.has(f.group)) groups.set(f.group, []);
    const value = f.type.toLowerCase() === 'checkbox' && /^(yes|no)$/i.test(f.value) ? String(/^yes$/i.test(f.value)) : f.value;
    groups.get(f.group)!.push({ field: apiCustomField(f), value });
  }
  return [...groups].map(([name, rows]) => ({ name, rows: rows.sort((a, b) => a.field.order - b.field.order) }));
}

/** History kind for an activity API type, e.g. "ServiceRequest" or "service_request" is "Service Request". */
function kindOf(type: string): HistoryKind | null {
  const t = type.toLowerCase().replace(/[^a-z]/g, '');
  return HISTORY_KINDS.find((k) => t.includes(k.toLowerCase().replace(/[^a-z]/g, ''))) ?? (t.includes('workorder') ? 'Maintenance' : null);
}
const HISTORY_PAGE = 15;


/**
 * Overview, Components, Custom Fields and History tabs of the asset detail page. Every figure comes from the one asset
 * named by ?tag=: its register record, detail file, maintenance data and components.
 */
@Component({
  selector: 'app-asset-detail-tab',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule, ComponentOverview],
  templateUrl: './asset-detail-tab.html',
  // Shares the Status & Condition page's header, tab, KPI, table and pill styles
  styleUrls: [
    '../asset-status-condition/asset-status-condition.css',
    '../asset-status-condition/asset-status-condition-panels.css',
    '../asset-maintenance-tab/asset-maintenance-tab.css',
    './asset-detail-tab.css',
  ],
})
export class AssetDetailTab implements OnDestroy {
  readonly icons = {
    Activity, CalendarClock, ChevronDown, ClipboardList, Download, ExternalLink, FileText, HeartPulse, History, Images, Info, LayoutGrid, MessageSquarePlus, Network,
    Power, Printer, Puzzle, ShieldAlert, Timer, TriangleAlert, Wrench, X,
  };
  readonly typeIcons = ASSET_TYPE_ICONS;
  readonly kinds = HISTORY_KINDS;
  readonly today = toIsoDate(TODAY);
  readonly iconFor = (c: AssetComponent) => COMPONENT_ICONS[c.icon];

  view: View = 'overview';
  tabs: AssetTabLink[] = assetTabs('Overview');

  /** The asset named by ?tag=; when it is missing or unknown the page shows a "not found" message. */
  asset: AssetHeader = assetHeader(blankAssetRecord());
  record: AssetRecord = blankAssetRecord();
  notFound = false;
  requestedTag = '';
  loading = false;
  detail: AssetDetail | null = null;
  maintenance: MaintenanceData = dataFor(blankAssetRecord());
  tree: AssetComponent[] = [];
  components: AssetComponent[] = [];
  selectedComponent: AssetComponent | null = null;

  // History filters
  history: HistoryEvent[] = [];
  kindFilter: HistoryKind | '' = '';
  search = '';
  historyLimit = HISTORY_PAGE;

  /** The asset's registration from the Asset API; null when not registered. */
  registration: AssetRegistrationDto | null = null;
  /** Summary counts from GET api/asset-dashboard/assets/{id}/counts; null until loaded or on error (local counts are shown). */
  private apiCounts: Record<string, number> | null = null;
  /** Overview card figures from GET api/asset-registrations/{id}/health-summary; null until loaded or on error. */
  health: AssetHealthSummary | null = null;
  /** Recent Activity rows from GET api/asset-registrations/{id}/activity; null until loaded or on error (local history is shown). */
  private apiActivity: ActivityRow[] | null = null;
  /** Components tab figures from GET api/asset-registrations/{id}/components/summary; null until loaded or on error. */
  componentSummary: Record<string, number> | null = null;
  /** The asset's service requests from the API; null until loaded. */
  private apiRequests: ServiceRequestListItemDto[] | null = null;
  /** Field definitions from the API (empty until loaded). */
  private fields: CustomField[] = [];

  // Custom fields
  fieldGroups: { name: string; rows: FieldRow[] }[] = [];
  /**
   * Field groups from GET api/asset-registrations/{id}/custom-fields; null until loaded or on error. Merged into the
   * defined field groups: API values fill matching fields, API fields with no definition are added to their own group.
   */
  private apiFieldGroups: { name: string; rows: FieldRow[] }[] | null = null;
  emptyOnly = false;

  actionsOpen = false;
  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;
  private readonly router = inject(Router);
  private readonly assetsApi = inject(AssetDetailsService);
  private readonly componentsApi = inject(AssetComponentsService);
  private readonly requestsApi = inject(ServiceRequestApiService);
  private readonly load$ = new Subject<{ tag: string | null; id: string | null }>();

  constructor() {
    const route = inject(ActivatedRoute);
    const v = route.snapshot.data['view'] as View;
    this.view = v in VIEW_TABS ? v : 'overview';
    this.tabs = assetTabs(VIEW_TABS[this.view]);
    const destroyRef = inject(DestroyRef);
    this.assetsApi.getCustomFieldDefinitions().pipe(takeUntilDestroyed(destroyRef)).subscribe({
      next: (defs) => {
        if (!defs.length) return;
        this.fields = defs.map((d) => toCustomField(d as unknown as CustomFieldDto));
        this.fieldGroups = this.buildFields();
      },
      error: () => undefined, // no definitions: the Custom Fields tab stays empty
    });
    this.load$.pipe(switchMap(({ tag, id }) => this.loadApi(tag, id)), takeUntilDestroyed(destroyRef)).subscribe();
    route.queryParamMap.pipe(takeUntilDestroyed(destroyRef)).subscribe((p) => this.load(p.get('tag'), p.get('id')));
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
  }

  // ===== Loading =====

  /** ?id= (asset registration id) is read directly; without it the asset is found by ?tag=. */
  private load(tag: string | null, id: string | null = null): void {
    this.requestedTag = tag ?? id ?? '';
    this.registration = null;
    this.apiRequests = null;
    this.apiCounts = null;
    this.health = null;
    this.apiActivity = null;
    this.componentSummary = null;
    this.apiFieldGroups = null;
    this.notFound = !tag && !id; // opened without an asset
    this.loading = !this.notFound;
    this.load$.next({ tag, id }); // the Asset API record is shown when it arrives
  }

  /** Shows a register record: header, maintenance data, components and the detail data. */
  private setRecord(record: AssetRecord): void {
    this.record = record;
    this.asset = assetHeader(record);
    this.maintenance = dataFor(record);
    this.tree = componentTreeOf(record.tag);
    this.components = componentsOf(record.tag);
    if (this.registration) this.applyCondition();
    this.selectedComponent = null;
    this.kindFilter = '';
    this.search = '';
    this.historyLimit = HISTORY_PAGE;
    this.closeMenus();
    this.detail = null;
    this.rebuild();
    this.loading = true;
    loadAssetDetail(record.tag).then((d) => {
      if (this.record !== record) return; // the user moved on to another asset
      this.loading = false;
      this.detail = d;
      this.rebuild();
    });
  }

  /**
   * Reads the asset from the Asset API (registration, components, service requests, counts): by registration id
   * when given (GET /api/asset-registrations/{id}), else by tag. An asset the API does not have shows "not found".
   */
  private loadApi(tagParam: string | null, id: string | null) {
    const key = tagParam ?? id;
    if (!key) return of(null);
    const asset$ = id ? this.assetsApi.getById(id) : this.assetsApi.getByTag(key);
    // Components are looked up by tag: the registration's own tag once loaded
    let tag = key;
    return asset$.pipe(
      catchError(() => of(null)),
      tap((a) => {
        if (this.requestedTag !== key) return;
        if (a?.record.tag) tag = a.record.tag;
        if (!a) {
          this.loading = false;
          this.notFound = true;
          return;
        }
        this.registration = a.registration;
        this.setRecord(a.record);
      }),
      switchMap((a) => {
        if (!a || this.requestedTag !== key) return of(null);
        return forkJoin({
          tree: loadComponentTree(this.componentsApi, tag).pipe(catchError(() => of(null))),
          requests: this.requestsApi.list({ assetRegistrationId: a.registration.assetRegistrationId, pageSize: 200 }).pipe(catchError(() => of(null))),
          counts: this.assetsApi.getDashboardCounts(a.registration.assetRegistrationId).pipe(catchError(() => of(null))),
          health: this.assetsApi.getHealthSummary(a.registration.assetRegistrationId).pipe(catchError(() => of(null))),
          activity: this.assetsApi.getActivity(a.registration.assetRegistrationId, 1, 10).pipe(catchError(() => of(null))),
          customFields:
            this.view === 'fields'
              ? this.assetsApi.getAssetCustomFields(a.registration.assetRegistrationId).pipe(catchError(() => of(null)))
              : of(null),
          componentSummary:
            this.view === 'components'
              ? this.assetsApi.getComponentSummary(a.registration.assetRegistrationId).pipe(catchError(() => of(null)))
              : of(null),
        }).pipe(
          tap((r) => {
            if (this.requestedTag !== key) return;
            if (r.tree) {
              this.tree = r.tree;
              this.components = componentsOf(tag);
              this.selectedComponent = null;
            }
            if (r.counts && Object.keys(r.counts).length) this.apiCounts = r.counts;
            this.health = r.health;
            if (r.activity) this.apiActivity = r.activity.map((x) => this.activityRow(x));
            if (r.componentSummary && Object.keys(r.componentSummary).length) this.componentSummary = r.componentSummary;
            if (r.customFields) {
              this.apiFieldGroups = groupFieldValues(r.customFields);
              this.rebuild();
            }
            if (r.requests) {
              this.apiRequests = r.requests.items.filter((x) => x.status !== 'Draft');
              this.rebuild();
            }
          }),
        );
      }),
    );
  }

  /** The registration's condition, when it is one the header knows. */
  private applyCondition(): void {
    const c = this.registration?.basicInformation?.condition;
    if (c === 'Good' || c === 'Fair' || c === 'Poor') this.asset = { ...this.asset, condition: c as AssetCondition };
  }

  private rebuild(): void {
    this.history = this.buildHistory();
    this.fieldGroups = this.buildFields();
  }

  get title(): string {
    return VIEW_TABS[this.view];
  }

  // ===== Tones =====

  statusTone(status: string): string {
    return status === 'Active' || status === 'Checked Out' ? 'good' : status === 'Disposed' ? 'critical' : 'warn';
  }

  conditionTone(condition: string | undefined): string {
    return condition === 'Excellent' || condition === 'Good' ? 'good' : condition === 'Fair' ? 'warn' : 'critical';
  }

  criticalityTone(level: string): string {
    return level === 'Critical' ? 'critical' : level === 'High' ? 'serious' : level === 'Medium' ? 'warn' : 'good';
  }

  healthTone(status: string | undefined): string {
    return status === 'Healthy' || status === 'Good' ? 'good' : status === 'Fair' ? 'warn' : status === 'Poor' ? 'serious' : 'critical';
  }

  kindTone(k: string): string {
    return KIND_TONES[k as HistoryKind] ?? 'warn';
  }

  componentTone(status: string): string {
    return status === 'Active' ? 'good' : status === 'Under Maintenance' ? 'warn' : status === 'Needs Replacement' ? 'critical' : 'serious';
  }

  // ===== Overview =====

  get location(): string {
    return assetLocation(this.record);
  }

  /** Most recent condition assessment. */
  get latestAssessment(): Assessment | null {
    return [...(this.detail?.status.assessments ?? [])].sort((a, b) => b.date.localeCompare(a.date))[0] ?? null;
  }

  // Overview cards: the health-summary API first, else the asset's own data.

  get healthScore(): number | null {
    return this.health?.healthScore ?? this.detail?.health.score ?? null;
  }

  get healthStatus(): string | null {
    // The API gives a score only; the status label still comes from the detail data when it has one
    return this.detail?.health.status ?? null;
  }

  get criticalityMaxScore(): number {
    return this.health?.criticalityMaxScore ?? 100;
  }

  /** When the condition was assessed: the API's assessment date, else the latest local assessment. */
  get conditionAssessedAt(): string | null {
    return this.health?.conditionAssessedAt ?? this.latestAssessment?.date ?? null;
  }

  get condition(): string {
    return this.health?.condition ?? this.latestAssessment?.overall ?? this.asset.condition;
  }

  get criticality(): string {
    return this.health?.criticality ?? this.detail?.criticality?.level ?? this.asset.criticality;
  }

  get criticalityScore(): number | null {
    return this.health?.criticalityScore ?? this.detail?.criticality?.score ?? null;
  }

  get availability(): number | null {
    return this.health?.availability ?? this.detail?.status.availability ?? null;
  }

  get utilization(): number | null {
    return this.health?.utilization ?? this.detail?.status.utilization ?? null;
  }

  get lastMaintenance(): string | null {
    return this.health?.lastMaintenance ?? this.detail?.status?.lastMaintenance ?? null;
  }

  get openWorkOrders(): number {
    if (this.health?.openWorkOrders != null) return this.health.openWorkOrders;
    return this.maintenance.workOrders.filter((w) => w.status !== 'Completed').length;
  }

  get openRequests(): number {
    if (this.apiRequests) return this.apiRequests.filter((r) => !/closed|cancelled/i.test(r.status ?? '')).length;
    return this.maintenance.serviceRequests.filter(isOpen).length;
  }

  get openAlerts(): number {
    return (this.detail?.status.alerts ?? []).filter((a) => a.status !== 'Resolved').length;
  }

  get nextMaintenance(): string {
    return this.health?.nextMaintenance || this.detail?.status.lifecycle.nextMaintenance || this.record.nextMaintenance || '';
  }

  /** A count from the counts API under the first of its key spellings that it has; undefined when it has none. */
  private apiCount(...keys: string[]): number | undefined {
    const c = this.apiCounts;
    return c ? keys.map((k) => c[k]).find((n) => n !== undefined) : undefined;
  }

  /**
   * Overview panels from the asset's registration (GET /api/asset-registrations/{id}). Empty values show "-";
   * a section with no value at all is left out.
   */
  get registrationSections(): InfoSection[] {
    const reg = this.registration;
    if (!reg) return [];
    const b = reg.basicInformation;
    const c = reg.classification;
    const loc = reg.locationAssignment?.location;
    const as = reg.locationAssignment?.assignment;
    const p = reg.purchaseWarranty?.purchase;
    const w = reg.purchaseWarranty?.warranty;
    const t = reg.additionalInformation?.technical;
    const op = reg.additionalInformation?.operational;
    const day = (v: string | null | undefined) => {
      const d = isoDay(v);
      return d ? new Date(d + 'T00:00:00').toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }) : '';
    };
    const join = (...v: (string | number | null | undefined)[]) => v.filter((x) => x !== null && x !== undefined && x !== '').join(' ');
    const money = (n: number | null | undefined) => (n === null || n === undefined ? '' : join(p?.currency, n.toLocaleString()));
    const dims = [t?.dimensionLength, t?.dimensionWidth, t?.dimensionHeight].some((n) => n != null)
      ? join([t?.dimensionLength, t?.dimensionWidth, t?.dimensionHeight].map((n) => n ?? '-').join(' x '), t?.dimensionUnit)
      : '';
    const row = (label: string, value: string | number | boolean | null | undefined, wide = false): InfoRow => ({
      label,
      value: value === null || value === undefined ? '' : typeof value === 'boolean' ? (value ? 'Yes' : 'No') : String(value),
      wide,
    });
    const sections: InfoSection[] = [
      {
        title: 'Basic Information',
        rows: [
          row('Asset Name', b?.assetName), row('Asset Tag', b?.assetTag), row('Registration ID', reg.assetRegistrationId),
          row('Barcode', b?.barcode), row('RFID Tag', b?.rfidTagId), row('Serial Number', b?.serialNumber),
          row('Manufacturer', b?.manufacturer ?? p?.manufacturer), row('Model', b?.model), row('Status', b?.status),
          row('Condition', b?.condition), row('Criticality', b?.criticality ?? c?.criticality), row('Description', b?.description, true),
        ],
      },
      {
        title: 'Classification',
        rows: [
          row('Category', c?.category), row('Sub-category', c?.subCategory), row('Asset Type', c?.assetType),
          row('Asset Sub-type', c?.assetSubType), row('Function / Purpose', c?.functionPurpose), row('Department', c?.department),
          row('Cost Center', c?.costCenter), row('Business Unit', c?.businessUnit), row('Tagging Method', c?.taggingMethod),
          row('Compliance Category', c?.complianceCategory), row('Lifecycle Stage', c?.lifecycleStage), row('Keywords', c?.keywords?.join(', '), true),
        ],
      },
      {
        title: 'Location & Assignment',
        rows: [
          row('Country', loc?.countryName), row('Site', loc?.siteName), row('Building', loc?.buildingName),
          row('Floor', loc?.floorName), row('Zone', loc?.zoneName), row('Room', loc?.roomName),
          row('Custodian Department', as?.custodianDepartment), row('Assigned To', as?.assignedToName), row('Ownership', as?.ownershipType),
          row('Responsible Team', as?.responsibleTeam), row('Assignment Start', day(as?.assignmentStartDate)), row('Assignment End', day(as?.assignmentEndDate)),
          row('Location Path', loc?.locationPath, true),
        ],
      },
      {
        title: 'Purchase & Warranty',
        rows: [
          row('Purchase Order', p?.purchaseOrderNumber), row('Invoice Number', p?.invoiceNumber), row('Supplier / Vendor', p?.supplierVendor),
          row('Purchase Date', day(p?.purchaseDate)), row('Delivery Date', day(p?.deliveryDate)), row('Purchase Cost', money(p?.purchaseCost)),
          row('Quantity', join(p?.quantity, p?.quantityUnit)), row('Tax', p?.taxPercent != null ? p.taxPercent + '%' : ''), row('Total Cost', money(p?.totalCost)),
          row('Payment Terms', p?.paymentTerms), row('Procurement Method', p?.procurementMethod), row('Project Reference', p?.projectReference),
          row('Warranty Type', w?.warrantyType), row('Warranty Start', day(w?.warrantyStartDate)), row('Warranty End', day(w?.warrantyEndDate)),
          row('Warranty Period', join(w?.warrantyPeriod, w?.warrantyPeriodUnit)), row('AMC Included', w?.amcIncluded), row('Service Provider', w?.serviceProvider),
          row('Support Coverage', w?.supportCoverage), row('Warranty Reference', w?.warrantyReference), row('Remarks', p?.remarks, true),
        ],
      },
      {
        title: 'Technical & Operational',
        rows: [
          ...(t?.specifications ?? []).filter((s) => s.name).map((s) => row(s.name!, join(s.value, s.unit))),
          row('Dimensions (L x W x H)', dims), row('Weight', join(t?.weight, t?.weightUnit)), row('Expected Lifespan', join(t?.expectedLifespan, t?.expectedLifespanUnit)),
          row('Operating Hours', join(op?.operatingHoursPerDay, op?.operatingHoursUnit)), row('Operating Shift', op?.operatingShift), row('Utilization', op?.utilization),
          row('Maintenance Frequency', join(op?.maintenanceFrequency, op?.maintenanceFrequencyUnit)), row('Last Maintenance', day(op?.lastMaintenanceDate)),
          row('Next Maintenance', day(op?.nextMaintenanceDate)), row('Compliance Standards', op?.complianceStandards?.join(', '), true),
          row('Technical Remarks', t?.technicalRemarks, true),
        ],
      },
    ];
    return sections.filter((s) => s.rows.some((r) => r.value));
  }

  /** Operational Details card rows from the registration, used when the asset has no status detail data. */
  get operationalRows(): InfoRow[] {
    const reg = this.registration;
    if (!reg) return [];
    const b = reg.basicInformation;
    const c = reg.classification;
    const t = reg.additionalInformation?.technical;
    const op = reg.additionalInformation?.operational;
    const day = (v: string | null | undefined) => {
      const d = isoDay(v);
      return d ? new Date(d + 'T00:00:00').toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }) : '';
    };
    const join = (...v: (string | number | null | undefined)[]) => v.filter((x) => x !== null && x !== undefined && x !== '').join(' ');
    const commissioned = customFieldValue(reg, 'Commissioning Date') ?? customFieldValue(reg, 'Installation Date');
    const rows: InfoRow[] = [
      { label: 'Operational Status', value: join(b?.status) },
      { label: 'Operating Shift', value: join(op?.operatingShift) },
      { label: 'Utilization', value: join(op?.utilization) },
      { label: 'Operating Hours', value: join(op?.operatingHoursPerDay, op?.operatingHoursUnit ? `${op.operatingHoursUnit} / day` : '') },
      { label: 'Maintenance Frequency', value: join(op?.maintenanceFrequency, op?.maintenanceFrequencyUnit) },
      { label: 'Last Maintenance', value: day(op?.lastMaintenanceDate) },
      { label: 'Next Maintenance', value: day(op?.nextMaintenanceDate) },
      { label: 'Lifecycle Stage', value: join(c?.lifecycleStage) },
      { label: 'Commissioned On', value: commissioned ? day(String(commissioned)) : '' },
      { label: 'Expected Life', value: join(t?.expectedLifespan, t?.expectedLifespanUnit) },
      { label: 'Compliance Standards', value: op?.complianceStandards?.join(', ') ?? '' },
    ];
    return rows.some((r) => r.value) ? rows : [];
  }

  /** Counts that link to the other detail tabs: from the counts API when it has the figure, else counted locally. */
  get glance(): { label: string; value: number; icon: typeof Info; path: string }[] {
    const d = this.detail;
    return [
      { label: 'Components', value: this.apiCount('components', 'component', 'assetcomponents') ?? this.components.length, icon: Puzzle, path: '/assets/component-details' },
      { label: 'Relationships', value: this.apiCount('relationships', 'relationship', 'assetrelationships') ?? d?.relationships.links.length ?? 0, icon: Network, path: '/assets/relationships' },
      { label: 'Documents', value: this.apiCount('documents', 'document', 'assetdocuments') ?? d?.documents.length ?? 0, icon: FileText, path: '/assets/documents' },
      { label: 'Images', value: this.apiCount('images', 'image', 'assetimages') ?? d?.images.length ?? 0, icon: Images, path: '/assets/images' },
      { label: 'Work Orders', value: this.apiCount('workorders', 'workorder') ?? this.maintenance.workOrders.length, icon: Wrench, path: '/assets/maintenance-details' },
      { label: 'Service Requests', value: this.apiCount('servicerequests', 'servicerequest') ?? this.apiRequests?.length ?? this.maintenance.serviceRequests.length, icon: MessageSquarePlus, path: '/assets/service-history' },
      { label: 'Open Alerts', value: this.apiCount('openalerts', 'alertsopen', 'activealerts', 'alerts', 'alert') ?? this.openAlerts, icon: TriangleAlert, path: '/assets/status-condition' },
    ];
  }

  /** Recent Activity: the activity API's rows when it answered, else the newest local history events. */
  get recentHistory(): ActivityRow[] {
    return this.apiActivity ?? this.history.slice(0, 6);
  }

  private activityRow(a: AssetActivityItem): ActivityRow {
    const kind = kindOf(a.type);
    return {
      at: a.at,
      kind: kind ?? (a.type || 'Activity'),
      title: a.title || a.type || 'Activity',
      detail: a.detail,
      by: a.by,
      path: kind ? KIND_PATHS[kind] : '/assets/history',
    };
  }

  // ===== Components =====

  pickComponent(c: AssetComponent): void {
    this.selectedComponent = this.selectedComponent === c ? null : c;
  }

  onComponentTab(e: { tab: 'service' | 'documents'; component: AssetComponent | null }): void {
    if (e.tab === 'service') this.go('/assets/service-history');
    else this.router.navigate(['/assets/components'], { queryParams: { tag: this.asset.tag } });
  }

  parentOf(c: AssetComponent): AssetComponent | null {
    return this.components.find((p) => p.children.includes(c)) ?? null;
  }

  lifePct(c: AssetComponent): number {
    return c.lifeTotal ? Math.min(100, Math.round((c.lifeUsed / c.lifeTotal) * 100)) : 0;
  }

  // ===== Custom fields =====

  /** The asset's value for a field, from its record and detail data; '' when it has none. */
  private valueOf(f: CustomField): string {
    const fromApi =
      customFieldValue(this.registration, f.name) ??
      customFieldValue(this.registration, f.label) ??
      this.registrationValue(f.name) ??
      this.specificationValue(f.label);
    if (fromApi !== undefined && fromApi !== null && fromApi !== '') return String(fromApi);
    const r = this.record;
    const d = this.detail;
    const op = d?.status.operational;
    const life = d?.status.lifecycle;
    const m = this.maintenance.overview;
    const purchase = r.purchaseDate ?? '';
    const warrantyEnd = purchase && Number.isFinite(m.warrantyYears) ? `${Number(purchase.slice(0, 4)) + m.warrantyYears}${purchase.slice(4)}` : '';
    const values: Record<string, string | number | undefined> = {
      warranty_expiry: warrantyEnd,
      asset_condition: this.latestAssessment?.overall,
      installation_date: life?.commissionedOn,
      power_rating: op && /kVA/i.test(op.ratedUnit) ? op.ratedCapacity : undefined,
      serial_number: m.serialNumber,
      criticality: d?.criticality.level ?? r.criticality,
      location_zone: r.site,
      manufacturer: r.manufacturer,
      model: r.model,
      purchase_date: purchase,
      warranty_period: m.warrantyYears * 12,
      warranty_provider: m.amcVendor,
      assigned_department: r.department,
      custodian: r.custodian,
      building_floor: r.area,
      last_service_date: d?.status.lastMaintenance,
      next_service_date: life?.nextMaintenance,
      service_provider: m.amcVendor,
      runtime_hours: op?.runningHours,
      maintenance_frequency: d?.health.maintenancePlan[0]?.frequency,
      rated_output: op && /kVA|kW/i.test(op.ratedUnit) ? `${op.ratedCapacity} ${op.ratedUnit}` : undefined,
      cooling_capacity_tr: op && /TR/.test(op.ratedUnit) ? op.ratedCapacity : undefined,
    };
    const v = values[f.name];
    return v === undefined || v === null || v === '' ? f.defaultValue : String(v);
  }

  /** Values the registration holds for the standard field names. */
  private registrationValue(name: string): string | number | undefined {
    const reg = this.registration;
    if (!reg) return undefined;
    const b = reg.basicInformation;
    const w = reg.purchaseWarranty?.warranty;
    const p = reg.purchaseWarranty?.purchase;
    const op = reg.additionalInformation?.operational;
    const loc = reg.locationAssignment?.location;
    const values: Record<string, string | number | null | undefined> = {
      serial_number: b?.serialNumber,
      manufacturer: b?.manufacturer ?? p?.manufacturer,
      model: b?.model,
      asset_condition: b?.condition,
      criticality: b?.criticality ?? reg.classification?.criticality,
      purchase_date: isoDay(p?.purchaseDate),
      warranty_expiry: isoDay(w?.warrantyEndDate),
      warranty_period: w?.warrantyPeriod != null ? (/year/i.test(w.warrantyPeriodUnit ?? '') ? w.warrantyPeriod * 12 : w.warrantyPeriod) : undefined,
      warranty_provider: w?.serviceProvider,
      service_provider: w?.serviceProvider,
      assigned_department: reg.locationAssignment?.assignment?.custodianDepartment ?? reg.classification?.department,
      custodian: reg.locationAssignment?.assignment?.assignedToName,
      location_zone: loc?.siteName,
      building_floor: [loc?.buildingName, loc?.floorName].filter(Boolean).join(', '),
      last_service_date: isoDay(op?.lastMaintenanceDate),
      next_service_date: isoDay(op?.nextMaintenanceDate),
      maintenance_frequency: op?.maintenanceFrequency != null ? `${op.maintenanceFrequency} ${op.maintenanceFrequencyUnit ?? ''}`.trim() : undefined,
      supplier: p?.supplierVendor,
      purchase_order: p?.purchaseOrderNumber,
      purchase_cost: p?.purchaseCost ?? undefined,
      cost_center: reg.classification?.costCenter ?? reg.locationAssignment?.assignment?.costCenter,
      gps_coordinates: gps(loc as { latitude?: number | null; longitude?: number | null } | null | undefined),
      maintenance_notes: reg.additionalInformation?.technical?.technicalRemarks,
    };
    const v = values[name];
    return v === null || v === '' || (typeof v === 'number' && !Number.isFinite(v)) ? undefined : v;
  }

  /** A technical specification of the registration whose name matches the label, e.g. "Voltage" for "Voltage (V)". */
  private specificationValue(label: string): string | undefined {
    const key = fieldBaseKey(label);
    const spec = this.registration?.additionalInformation?.technical?.specifications?.find((s) => fieldBaseKey(s.name ?? '') === key);
    if (!spec?.value) return undefined;
    // The label already names the unit for unit fields, e.g. "Voltage (V)"
    return /\)\s*$/.test(label) || !spec.unit ? spec.value : `${spec.value} ${spec.unit}`;
  }

  private buildFields(): { name: string; rows: FieldRow[] }[] {
    const groups = new Map<string, FieldRow[]>();
    const byKey = new Map<string, FieldRow>();
    const find = (f: CustomField): FieldRow | undefined =>
      byKey.get(fieldKey(f.label)) ?? byKey.get(fieldKey(f.name)) ?? byKey.get(fieldBaseKey(f.label));
    const add = (group: string, row: FieldRow): void => {
      if (!groups.has(group)) groups.set(group, []);
      groups.get(group)!.push(row);
      for (const k of [fieldKey(row.field.label), fieldKey(row.field.name), fieldBaseKey(row.field.label)]) if (k && !byKey.has(k)) byKey.set(k, row);
    };
    // 1. The standard groups, always shown
    for (const g of STANDARD_FIELD_GROUPS)
      g.fields.forEach((s, i) => {
        const f = standardField(g.name, s, i);
        add(g.name, { field: f, value: this.valueOf(f) });
      });
    // 2. Defined asset fields and this category's fields that are not standard ones
    const defined = this.fields.filter(
      (f) => f.status === 'Active' && (f.scope === 'asset' || (f.scope === 'category' && f.appliesTo === this.record.category)),
    );
    for (const f of defined) {
      const row = find(f);
      if (row) {
        if (!row.value) row.value = this.valueOf(f);
      } else add(f.group || 'Other', { field: f, value: this.valueOf(f) });
    }
    // 3. Values from the custom-fields API fill matching fields; the rest go in their own group
    for (const g of this.apiFieldGroups ?? [])
      for (const r of g.rows) {
        const row = find(r.field);
        if (row) {
          if (r.value) row.value = r.value;
        } else add(g.name, r);
      }
    return [...groups].map(([name, rows]) => ({ name, rows: rows.sort((a, b) => a.field.order - b.field.order) }));
  }

  get fieldCount(): number {
    return this.fieldGroups.reduce((s, g) => s + g.rows.length, 0);
  }

  get filledCount(): number {
    return this.fieldGroups.reduce((s, g) => s + g.rows.filter((r) => r.value).length, 0);
  }

  get missingRequired(): number {
    return this.fieldGroups.reduce((s, g) => s + g.rows.filter((r) => r.field.required && !r.value).length, 0);
  }

  get shownGroups(): { name: string; rows: FieldRow[] }[] {
    if (!this.emptyOnly) return this.fieldGroups;
    return this.fieldGroups.map((g) => ({ name: g.name, rows: g.rows.filter((r) => !r.value) })).filter((g) => g.rows.length);
  }

  displayValue(r: FieldRow): string {
    if (!r.value) return '';
    if (r.field.type === 'Checkbox') return r.value === 'true' ? 'Yes' : 'No';
    if (r.field.type === 'Date') {
      // Shown as "07 Nov 2026"; a value that is not a date is shown as it is (the date pipe would throw).
      const d = /^\d{4}-\d{2}-\d{2}/.test(r.value) ? new Date(r.value.slice(0, 10) + 'T00:00:00') : new Date(NaN);
      if (!Number.isNaN(d.getTime())) return d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
    }
    return r.value;
  }

  // ===== History =====

  private buildHistory(): HistoryEvent[] {
    const d = this.detail;
    const out: HistoryEvent[] = [];
    const add = (at: string, kind: HistoryKind, title: string, detail: string, by: string, path: string) => {
      if (at && at.slice(0, 10) <= this.today) out.push({ at, kind, title, detail, by, path });
    };
    if (d) {
      for (const e of d.status.timeline) add(e.date, 'Status', e.title, [e.status, e.condition].filter(Boolean).join(' · '), e.by ?? '', '/assets/status-condition');
      for (const a of d.status.assessments) add(a.date, 'Condition', `Condition assessed: ${a.overall}`, `Health ${a.healthScore}%${a.remarks ? ' · ' + a.remarks : ''}`, a.assessedBy, '/assets/status-condition');
      for (const h of d.health.history) add(h.date, 'Health', `Health score ${h.score} (${h.status})`, `${h.trigger}${h.comments ? ' · ' + h.comments : ''}`, h.assessedBy, '/assets/health-score');
      for (const c of d.criticality.history) add(c.date, 'Criticality', `Criticality reviewed: ${c.level} (${c.score})`, c.remarks, c.assessedBy, '/assets/criticality');
      for (const doc of d.documents) {
        for (const v of doc.versions) add(v.uploadedAt, 'Document', `${doc.name} uploaded (v${v.version})`, v.note || doc.type, v.uploadedBy, '/assets/documents');
      }
      for (const img of d.images) {
        for (const h of img.history) add(h.at, 'Image', `${img.title}: ${h.action}`, img.type, h.by, '/assets/images');
      }
      for (const l of d.relationships.links) add(l.since, 'Relationship', `${l.type} linked: ${l.tag}`, l.description, '', '/assets/relationships');
    }
    for (const w of this.maintenance.workOrders) {
      add(w.date, 'Maintenance', `${w.id} · ${w.type} ${w.status.toLowerCase()}`, w.description, w.technician, '/assets/maintenance-details');
    }
    if (this.apiRequests) {
      for (const r of this.apiRequests) {
        add(localStamp(r.requestedDate), 'Service Request', `${r.requestNumber} raised`, `${r.title}${r.status ? ' · ' + r.status : ''}`, r.requestedBy ?? '', '/assets/service-history');
      }
    } else {
      for (const r of this.maintenance.serviceRequests) {
        add(r.createdAt, 'Service Request', `${r.id} raised`, r.title, r.requestedBy, '/assets/service-history');
        add(r.resolvedAt, 'Service Request', `${r.id} resolved`, r.title, r.assignedTo, '/assets/service-history');
      }
    }
    return out.sort((a, b) => b.at.localeCompare(a.at));
  }

  get filteredHistory(): HistoryEvent[] {
    const q = this.search.trim().toLowerCase();
    return this.history.filter(
      (e) => (!this.kindFilter || e.kind === this.kindFilter) && (!q || [e.title, e.detail, e.by].some((v) => v.toLowerCase().includes(q))),
    );
  }

  get shownHistory(): HistoryEvent[] {
    return this.filteredHistory.slice(0, this.historyLimit);
  }

  kindCount(k: HistoryKind): number {
    return this.history.filter((e) => e.kind === k).length;
  }

  // ===== Actions =====

  go(path: string): void {
    this.router.navigate([path], { queryParams: { tag: this.asset.tag } });
  }

  exportCsv(): void {
    this.closeMenus();
    const lines =
      this.view === 'fields'
        ? [['Group', 'Field', 'Value', 'Required'], ...this.fieldGroups.flatMap((g) => g.rows.map((r) => [g.name, r.field.label, this.displayValue(r), r.field.required ? 'Yes' : 'No']))]
        : this.view === 'components'
          ? [['Tag', 'Component', 'Status', 'Manufacturer', 'Model', 'Serial Number', 'Installed'], ...this.components.map((c) => [c.tag, c.name, c.status, c.manufacturer, c.model, c.serialNumber, c.installedDate])]
          : [['Date', 'Type', 'Event', 'Detail', 'By'], ...this.filteredHistory.map((e) => [e.at.replace('T', ' '), e.kind, e.title, e.detail, e.by])];
    const csv = lines.map((l) => l.map((v) => `"${String(v ?? '').replace(/"/g, '""')}"`).join(',')).join('\r\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }));
    const a = document.createElement('a');
    a.href = url;
    a.download = `${this.asset.tag}_${VIEW_TABS[this.view].toLowerCase().replace(/ /g, '_')}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }

  print(): void {
    this.closeMenus();
    window.print();
  }

  // ===== Menus =====

  toggleActions(event: Event): void {
    event.stopPropagation();
    this.actionsOpen = !this.actionsOpen;
  }

  @HostListener('document:click')
  closeMenus(): void {
    this.actionsOpen = false;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeMenus();
    this.selectedComponent = null;
  }

  tabClick(tab: AssetTabLink): void {
    if (!tab.path && !tab.active) this.showToast(`${tab.label} is not available in this demo.`);
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 3000);
  }
}
