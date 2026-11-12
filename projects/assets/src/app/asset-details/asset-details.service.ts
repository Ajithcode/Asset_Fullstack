import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, catchError, map, of, shareReplay, switchMap, throwError } from 'rxjs';
import { AssetIconKey, AssetRecord, AssetStatus, Criticality, environment } from 'shared-ui';
import { ASSET_API } from '../services/asset-api';

// ===== DTOs (Asset API, System.Text.Json camelCase) =====

/** One row of GET /api/asset-registrations (AssetListItemResponse). */
export interface AssetListItemDto {
  id: string;
  assetRegistrationId: string;
  assetName: string | null;
  assetTag: string | null;
  imageUrl: string | null;
  category: string | null;
  assetType: string | null;
  siteName: string | null;
  buildingName: string | null;
  floorName: string | null;
  zoneName: string | null;
  roomName: string | null;
  locationPath: string | null;
  custodianUserId: string | null;
  custodianName: string | null;
  department: string | null;
  status: string | null;
  criticality: string | null;
  manufacturer: string | null;
  nextMaintenanceDate: string | null;
  createdAt: string | null;
}

interface AssetRegistrationListDto {
  items: AssetListItemDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AssetBasicInformationDto {
  assetName: string | null;
  assetTag: string | null;
  barcode: string | null;
  rfidTagId: string | null;
  model: string | null;
  manufacturer: string | null;
  serialNumber: string | null;
  status: string | null;
  condition: string | null;
  criticality: string | null;
  description: string | null;
  images: string[] | null;
}

export interface AssetClassificationDto {
  category: string | null;
  subCategory: string | null;
  assetType: string | null;
  assetSubType: string | null;
  functionPurpose: string | null;
  criticality: string | null;
  department: string | null;
  costCenter: string | null;
  businessUnit: string | null;
  taggingMethod: string | null;
  complianceCategory: string | null;
  lifecycleStage: string | null;
  keywords: string[] | null;
}

export interface AssetLocationDto {
  projectId: string | null;
  countryId: string | null;
  countryName: string | null;
  siteId: string | null;
  siteName: string | null;
  buildingId: string | null;
  buildingName: string | null;
  floorId: string | null;
  floorName: string | null;
  zoneId: string | null;
  zoneName: string | null;
  roomId: string | null;
  roomName: string | null;
  locationPath: string | null;
}

export interface AssetAssignmentDto {
  custodianDepartment: string | null;
  assignedToUserId: string | null;
  assignedToName: string | null;
  ownershipType: string | null;
  costCenter: string | null;
  responsibleTeam: string | null;
  assignmentStartDate: string | null;
  assignmentEndDate: string | null;
  isCurrent: boolean | null;
}

export interface AssetFileDto {
  fileName: string | null;
  url: string | null;
  contentType: string | null;
  fileSize: number | null;
  uploadedAt: string | null;
}

export interface AssetPurchaseDto {
  purchaseOrderNumber: string | null;
  invoiceNumber: string | null;
  supplierVendor: string | null;
  manufacturer: string | null;
  purchaseDate: string | null;
  deliveryDate: string | null;
  purchaseCost: number | null;
  quantity: number | null;
  quantityUnit: string | null;
  currency: string | null;
  taxPercent: number | null;
  totalCost: number | null;
  paymentTerms: string | null;
  procurementMethod: string | null;
  projectReference: string | null;
  remarks: string | null;
}

export interface AssetWarrantyDto {
  warrantyType: string | null;
  warrantyStartDate: string | null;
  warrantyEndDate: string | null;
  warrantyPeriod: number | null;
  warrantyPeriodUnit: string | null;
  amcIncluded: boolean | null;
  supportCoverage: string | null;
  serviceProvider: string | null;
  warrantyReference: string | null;
}

export interface AssetSpecificationDto {
  name: string | null;
  value: string | null;
  unit: string | null;
}

export interface AssetCustomFieldValueDto {
  fieldName: string | null;
  fieldType: string | null;
  value: string | null;
}

export interface AssetTechnicalDetailsDto {
  specifications: AssetSpecificationDto[] | null;
  dimensionLength: number | null;
  dimensionWidth: number | null;
  dimensionHeight: number | null;
  dimensionUnit: string | null;
  weight: number | null;
  weightUnit: string | null;
  expectedLifespan: number | null;
  expectedLifespanUnit: string | null;
  technicalRemarks: string | null;
}

export interface AssetOperationalDetailsDto {
  operatingHoursPerDay: number | null;
  operatingHoursUnit: string | null;
  operatingShift: string | null;
  utilization: string | null;
  maintenanceFrequency: number | null;
  maintenanceFrequencyUnit: string | null;
  lastMaintenanceDate: string | null;
  nextMaintenanceDate: string | null;
  complianceStandards: string[] | null;
}

/** GET /api/asset-registrations/{assetRegistrationId} (AssetRegistrationResponse). */
export interface AssetRegistrationDto {
  id: string;
  assetRegistrationId: string;
  basicInformation: AssetBasicInformationDto | null;
  classification: AssetClassificationDto | null;
  locationAssignment: { location: AssetLocationDto | null; assignment: AssetAssignmentDto | null } | null;
  purchaseWarranty: { purchase: AssetPurchaseDto | null; documents: AssetFileDto[] | null; warranty: AssetWarrantyDto | null } | null;
  additionalInformation: {
    technical: AssetTechnicalDetailsDto | null;
    operational: AssetOperationalDetailsDto | null;
    customFields: AssetCustomFieldValueDto[] | null;
    attachments: AssetFileDto[] | null;
  } | null;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  clientId: string | null;
  tenantId: string | null;
}

/** GET /api/asset-registrations/{id}/components (AssetComponentResponse). */
export interface AssetComponentDto {
  id: string;
  assetRegistrationId: string;
  parentComponentId: string | null;
  parentComponentName: string | null;
  componentName: string;
  componentTag: string;
  status: string | null;
  model: string | null;
  manufacturer: string | null;
  partNumber: string | null;
  serialNumber: string | null;
  installedDate: string | null;
  expectedReplacementDate: string | null;
  lifecycleUsed: number;
  lifecycleRatedLife: number;
  lifecycleUnit: string | null;
  lifecyclePercent: number | null;
  description: string | null;
  childCount: number;
}

interface AssetComponentListDto {
  items: AssetComponentDto[];
  totalCount: number;
}

/** GET /api/asset-custom-fields (AssetCustomFieldResponse). */
export interface AssetCustomFieldDefinitionDto {
  id: string;
  scope: string;
  fieldName: string;
  displayLabel: string;
  fieldType: string;
  groupId: string | null;
  groupName: string | null;
  displayOrder: number;
  appliesToCategoryId: string | null;
  appliesToCategoryName: string | null;
  componentType: string | null;
  required: boolean;
  showInDetails: boolean;
  includeInSearch: boolean;
  status: string | null;
  defaultValue: string | null;
  description: string | null;
  helpText: string | null;
  options: string[];
  compoundParts: { label?: string; name?: string }[];
  lookupSource: string | null;
}

interface AssetCustomFieldListDto {
  items: AssetCustomFieldDefinitionDto[];
  totalCount: number;
}

/** An asset loaded from the API: the full registration plus the register-row shape the detail pages use. */
export interface ApiAssetDetails {
  registration: AssetRegistrationDto;
  record: AssetRecord;
}

// ===== Mapping =====

/** yyyy-MM-dd of an API date, or ''. */
export function isoDay(value: string | null | undefined): string {
  return value ? value.slice(0, 10) : '';
}

/** Absolute URL for a file the API returns as a relative path. */
export function fileUrl(url: string | null | undefined): string {
  if (!url) return '';
  if (/^(https?:|blob:|data:)/i.test(url)) return url;
  return environment.assetApiUrl.replace(/\/$/, '') + (url.startsWith('/') ? url : '/' + url);
}

const ICON_RULES: [RegExp, AssetIconKey][] = [
  [/generator|genset|dg\b/i, 'generator'],
  [/chiller|hvac|ahu|fan|pump/i, 'chiller'],
  [/ups|battery/i, 'ups'],
  [/forklift|fleet|vehicle|truck|car\b/i, 'forklift'],
  [/fire|sprinkler|extinguisher/i, 'fire'],
  [/\bac\b|air ?con|split/i, 'ac'],
  [/laptop|computer|it\b|server|desktop|printer/i, 'laptop'],
  [/cctv|camera|security/i, 'cctv'],
  [/transformer|switchgear|electrical|panel/i, 'transformer'],
  [/solar|pv\b/i, 'solar'],
];

/** Header icon for an asset, guessed from its category, type and name. */
export function iconForAsset(...text: (string | null | undefined)[]): AssetIconKey {
  const s = text.filter(Boolean).join(' ');
  return ICON_RULES.find(([re]) => re.test(s))?.[1] ?? 'generator';
}

/**
 * The register-row shape (AssetRecord) of an API asset. `fallback` is the demo record with the same tag,
 * used only for values the API does not hold (numeric id, icon).
 */
export function toAssetRecord(dto: AssetRegistrationDto, fallback?: AssetRecord): AssetRecord {
  const basic = dto.basicInformation;
  const cls = dto.classification;
  const loc = dto.locationAssignment?.location;
  const assign = dto.locationAssignment?.assignment;
  const purchase = dto.purchaseWarranty?.purchase;
  const area = [loc?.buildingName, loc?.floorName, loc?.zoneName, loc?.roomName].filter(Boolean).join(', ');
  return {
    id: fallback?.id ?? 0,
    name: basic?.assetName ?? fallback?.name ?? '',
    tag: basic?.assetTag ?? fallback?.tag ?? '',
    category: cls?.category ?? fallback?.category ?? '',
    type: cls?.assetType ?? fallback?.type ?? '',
    site: loc?.siteName ?? loc?.locationPath ?? fallback?.site ?? '',
    area: area || (fallback?.area ?? ''),
    custodian: assign?.assignedToName ?? fallback?.custodian ?? '',
    department: assign?.custodianDepartment ?? cls?.department ?? fallback?.department ?? '',
    status: ((basic?.status ?? fallback?.status ?? 'Active') as AssetStatus),
    criticality: ((basic?.criticality ?? cls?.criticality ?? fallback?.criticality ?? 'Medium') as Criticality),
    manufacturer: basic?.manufacturer ?? purchase?.manufacturer ?? fallback?.manufacturer ?? '',
    model: basic?.model ?? fallback?.model ?? '',
    nextMaintenance: isoDay(dto.additionalInformation?.operational?.nextMaintenanceDate) || fallback?.nextMaintenance || null,
    icon: fallback?.icon ?? iconForAsset(cls?.category, cls?.assetType, basic?.assetName),
    image: fileUrl(basic?.images?.[0]) || fallback?.image || '',
    serialNumber: basic?.serialNumber ?? fallback?.serialNumber,
    purchaseDate: isoDay(purchase?.purchaseDate) || fallback?.purchaseDate,
  };
}

/** Value of a custom field the asset carries in its registration (matched by field name, case-insensitive). */
export function customFieldValue(dto: AssetRegistrationDto | null | undefined, fieldName: string): string | undefined {
  const name = fieldName.toLowerCase();
  const hit = dto?.additionalInformation?.customFields?.find((f) => (f.fieldName ?? '').toLowerCase() === name);
  return hit?.value ?? undefined;
}

/**
 * Asset details read from the Asset API (read-only): the registration found by tag, its components and the
 * custom field definitions. The demo JSON files in this folder remain the source for status, health, criticality
 * and the other per-asset sections the API does not hold.
 */
/**
 * Flattens a counts response to { key: number }. Accepts a flat object, one wrapped in data / counts / summary,
 * nested objects ({ workOrders: { total: 4, open: 1 } } gives workorders 4 and workordersopen 1) or a list of
 * { key|name|label, count|value|total } items.
 */
export function normaliseCounts(res: unknown): Record<string, number> {
  const out: Record<string, number> = {};
  const clean = (k: string) => k.toLowerCase().replace(/[^a-z]/g, '').replace(/^(total|count)|(count|total)$/g, '');
  const walk = (value: unknown, prefix: string): void => {
    if (typeof value === 'number' && Number.isFinite(value)) {
      if (prefix && !(prefix in out)) out[prefix] = value;
      return;
    }
    if (Array.isArray(value)) {
      for (const item of value) {
        if (!item || typeof item !== 'object') continue;
        const r = item as Record<string, unknown>;
        const key = r['key'] ?? r['name'] ?? r['label'] ?? r['type'];
        const n = r['count'] ?? r['value'] ?? r['total'];
        if (typeof key === 'string' && typeof n === 'number') walk(n, prefix + clean(key));
      }
      return;
    }
    if (!value || typeof value !== 'object') return;
    for (const [k, v] of Object.entries(value as Record<string, unknown>)) {
      // Wrappers and an inner "total" / "count" belong to the parent key
      const wrapper = /^(data|counts?|summary|results?|totals?)$/i.test(k);
      walk(v, wrapper ? prefix : prefix + clean(k));
    }
  };
  walk(res, '');
  return out;
}

/** GET api/asset-registrations/{id}/health-summary response. */
interface HealthSummaryDto {
  assetRegistrationId?: string;
  condition?: {
    value?: string | null;
    healthScore?: number | null;
    source?: string | null;
    assessedAt?: string | null;
    assessedBy?: string | null;
  } | null;
  criticality?: {
    level?: string | null;
    score?: number | null;
    maxScore?: number | null;
    source?: string | null;
    assessedAt?: string | null;
    nextReviewDate?: string | null;
    assessedBy?: string | null;
  } | null;
  availability?: number | null;
  utilization?: number | null;
  nextMaintenanceDate?: string | null;
  lastMaintenanceDate?: string | null;
  openWorkOrders?: number | null;
}

/** The Overview cards' figures from the health summary; null where the API has no value. */
export interface AssetHealthSummary {
  healthScore: number | null;
  condition: string | null;
  conditionAssessedAt: string | null;
  criticality: string | null;
  criticalityScore: number | null;
  criticalityMaxScore: number;
  availability: number | null;
  utilization: number | null;
  nextMaintenance: string | null;
  lastMaintenance: string | null;
  openWorkOrders: number | null;
}

export function normaliseHealthSummary(res: HealthSummaryDto | null): AssetHealthSummary {
  const num = (v: unknown): number | null => (typeof v === 'number' && Number.isFinite(v) ? v : null);
  const text = (v: unknown): string | null => (typeof v === 'string' && v.trim() ? v : null);
  const c = res?.condition;
  const k = res?.criticality;
  return {
    healthScore: num(c?.healthScore),
    condition: text(c?.value),
    conditionAssessedAt: text(c?.assessedAt),
    criticality: text(k?.level),
    criticalityScore: num(k?.score),
    criticalityMaxScore: num(k?.maxScore) ?? 100,
    availability: num(res?.availability),
    utilization: num(res?.utilization),
    nextMaintenance: text(res?.nextMaintenanceDate),
    lastMaintenance: text(res?.lastMaintenanceDate),
    openWorkOrders: num(res?.openWorkOrders),
  };
}

/** One row of GET api/asset-registrations/{id}/activity. */
export interface AssetActivityItem {
  /** ISO date-time */
  at: string;
  type: string;
  title: string;
  detail: string;
  by: string;
}

/**
 * Reads the activity response: a list, or a page object with items / data / results. Field names are matched loosely
 * (e.g. occurredAt / timestamp / createdAt for the time) until the API's exact shape is confirmed.
 */
/** First field whose name looks like a time (…At, …On, …Date, …Time) and whose value parses as a date; '' when none. */
function dateField(r: Record<string, unknown>): string {
  for (const [k, v] of Object.entries(r)) {
    if (!/(at|on|date|time|timestamp)$/i.test(k) || typeof v !== 'string') continue;
    if (/^\d{4}-\d{2}-\d{2}/.test(v) && !Number.isNaN(Date.parse(v))) return v;
  }
  return '';
}

export function normaliseActivity(res: unknown): AssetActivityItem[] {
  const page = res as Record<string, unknown> | null;
  const list = Array.isArray(res) ? res : [page?.['items'], page?.['data'], page?.['results'], page?.['activities']].find(Array.isArray) ?? [];
  const str = (r: Record<string, unknown>, ...keys: string[]): string => {
    for (const k of keys) {
      const v = r[k];
      if (typeof v === 'string' && v.trim()) return v.trim();
      if (v && typeof v === 'object') {
        const o = v as Record<string, unknown>;
        const n = o['name'] ?? o['fullName'] ?? o['userName'];
        if (typeof n === 'string' && n.trim()) return n.trim();
      }
    }
    return '';
  };
  return (list as unknown[])
    .filter((x): x is Record<string, unknown> => !!x && typeof x === 'object')
    .map((r) => {
      const title = str(r, 'title', 'action', 'activity', 'event', 'eventName', 'summary', 'message', 'description');
      const detail = str(r, 'description', 'details', 'detail', 'remarks', 'notes', 'changes');
      return {
        at: str(r, 'occurredAt', 'timestamp', 'activityDate', 'performedAt', 'createdAt', 'date', 'eventDate') || dateField(r),
        type: str(r, 'type', 'activityType', 'category', 'module', 'entityType', 'source'),
        title,
        detail: detail === title ? '' : detail,
        by: str(r, 'performedBy', 'performedByName', 'userName', 'user', 'createdBy', 'actor', 'by'),
      };
    })
    .filter((a) => a.at || a.title);
}

/** One custom field of an asset with its value, from GET api/asset-registrations/{id}/custom-fields. */
export interface AssetCustomFieldValue {
  id: string;
  name: string;
  label: string;
  type: string;
  group: string;
  required: boolean;
  order: number;
  /** '' when not set; Checkbox values are 'true' / 'false'. */
  value: string;
}

/** A field value as text: lists and compound parts joined, booleans as 'true' / 'false'. */
function fieldValueText(v: unknown): string {
  if (v === null || v === undefined) return '';
  if (typeof v === 'string') return v.trim();
  if (typeof v === 'number' || typeof v === 'boolean') return String(v);
  if (Array.isArray(v)) return v.map(fieldValueText).filter(Boolean).join(', ');
  if (typeof v === 'object') {
    const o = v as Record<string, unknown>;
    if ('value' in o && Object.keys(o).length <= 3) return fieldValueText(o['value']);
    return Object.entries(o).map(([k, x]) => (fieldValueText(x) ? `${k}: ${fieldValueText(x)}` : '')).filter(Boolean).join(', ');
  }
  return '';
}

/**
 * Reads the custom-fields response: a list of fields, a { items / fields / data } wrapper, or groups
 * ({ groups: [{ groupName, fields: [...] }] }). Field names are matched loosely until the exact shape is confirmed.
 */
export function normaliseAssetCustomFields(res: unknown): AssetCustomFieldValue[] {
  const isObj = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v);
  const s = (r: Record<string, unknown>, ...keys: string[]): string => {
    for (const k of keys) if (typeof r[k] === 'string' && (r[k] as string).trim()) return (r[k] as string).trim();
    return '';
  };
  const out: AssetCustomFieldValue[] = [];
  const addField = (r: Record<string, unknown>, group: string): void => {
    const def = isObj(r['field']) ? (r['field'] as Record<string, unknown>) : isObj(r['definition']) ? (r['definition'] as Record<string, unknown>) : r;
    const label = s(def, 'label', 'fieldLabel', 'displayName', 'fieldName', 'name');
    if (!label || r['isActive'] === false) return;
    const valueKey = ['value', 'fieldValue', 'displayValue', 'values'].find((k) => k in r);
    const order = Number(def['order'] ?? def['displayOrder'] ?? def['sortOrder'] ?? out.length);
    out.push({
      id: s(def, 'id', 'fieldId', 'customFieldId') || label,
      name: s(def, 'name', 'fieldName', 'key') || label,
      label,
      type: s(def, 'type', 'fieldType', 'dataType') || 'Text',
      group: s(r, 'group', 'groupName', 'fieldGroup', 'category') || s(def, 'group', 'groupName', 'fieldGroup') || group || 'Other',
      required: def['required'] === true || def['isRequired'] === true || r['required'] === true || r['isRequired'] === true,
      order: Number.isFinite(order) ? order : out.length,
      value: valueKey ? fieldValueText(r[valueKey]) : '',
    });
  };
  const walk = (list: unknown[], group: string): void => {
    for (const item of list) {
      if (!isObj(item)) continue;
      const inner = ['fields', 'items', 'customFields'].map((k) => item[k]).find(Array.isArray) as unknown[] | undefined;
      if (inner) walk(inner, s(item, 'groupName', 'name', 'group', 'title') || group);
      else addField(item, group);
    }
  };
  if (Array.isArray(res)) walk(res, '');
  else if (isObj(res)) {
    const list = ['groups', 'fields', 'items', 'customFields', 'data'].map((k) => res[k]).find(Array.isArray) as unknown[] | undefined;
    if (list) walk(list, '');
  }
  return out;
}

@Injectable({ providedIn: 'root' })
export class AssetDetailsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${ASSET_API}asset-registrations`;
  private readonly byTag = new Map<string, Observable<AssetRegistrationDto | null>>();

  /**
   * GET api/asset-dashboard/assets/{assetRegistrationId}/counts: the asset's summary counts (components, documents, …).
   * Keys are normalised to lower case without a "count"/"total" prefix or suffix, e.g. { components: 3, workorders: 2 }.
   */
  /** GET api/asset-registrations/{assetRegistrationId}/health-summary: figures for the Overview cards. */
  getHealthSummary(assetRegistrationId: string): Observable<AssetHealthSummary> {
    return this.http.get<HealthSummaryDto | null>(`${this.base}/${encodeURIComponent(assetRegistrationId)}/health-summary`).pipe(map(normaliseHealthSummary));
  }

  /** GET api/asset-registrations/{assetRegistrationId}/activity?page=&pageSize=: the asset's recent activity, newest first. */
  getActivity(assetRegistrationId: string, page = 1, pageSize = 10): Observable<AssetActivityItem[]> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http
      .get<unknown>(`${this.base}/${encodeURIComponent(assetRegistrationId)}/activity`, { params })
      .pipe(map(normaliseActivity));
  }

  /**
   * GET api/asset-registrations/{assetRegistrationId}/components/summary: component totals, status counts and health.
   * Keys are normalised like the counts API, e.g. { components: 5, active: 3, undermaintenance: 1, averagehealth: 72 }.
   */
  getComponentSummary(assetRegistrationId: string): Observable<Record<string, number>> {
    return this.http
      .get<unknown>(`${this.base}/${encodeURIComponent(assetRegistrationId)}/components/summary`)
      .pipe(map(normaliseCounts));
  }

  /** GET api/asset-registrations/{assetRegistrationId}/custom-fields?emptyOnly=: the asset's custom fields and values. */
  getAssetCustomFields(assetRegistrationId: string, emptyOnly = false): Observable<AssetCustomFieldValue[]> {
    const params = new HttpParams().set('emptyOnly', emptyOnly);
    return this.http
      .get<unknown>(`${this.base}/${encodeURIComponent(assetRegistrationId)}/custom-fields`, { params })
      .pipe(map(normaliseAssetCustomFields));
  }

  getDashboardCounts(assetRegistrationId: string): Observable<Record<string, number>> {
    const url = `${environment.assetDashboardApiUrl}api/asset-dashboard/assets/${encodeURIComponent(assetRegistrationId)}/counts`;
    return this.http.get<unknown>(url).pipe(map(normaliseCounts));
  }

  /** Asset by its tag: the list is searched for the tag, then the full record is read. Null when no asset has the tag. */
  getRegistrationByTag(tag: string): Observable<AssetRegistrationDto | null> {
    const key = tag.trim().toLowerCase();
    let cached = this.byTag.get(key);
    if (!cached) {
      const params = new HttpParams().set('search', tag.trim()).set('pageSize', 50);
      cached = this.http.get<AssetRegistrationListDto>(this.base, { params }).pipe(
        map((res) => (res.items ?? []).find((i) => (i.assetTag ?? '').toLowerCase() === key) ?? null),
        switchMap((row) => (row ? this.getRegistration(row.assetRegistrationId) : of(null))),
        catchError((err) => {
          this.byTag.delete(key); // let a later visit retry
          return throwError(() => err);
        }),
        shareReplay({ bufferSize: 1, refCount: false }),
      );
      this.byTag.set(key, cached);
    }
    return cached;
  }

  getRegistration(assetRegistrationId: string): Observable<AssetRegistrationDto> {
    return this.http.get<AssetRegistrationDto>(`${this.base}/${encodeURIComponent(assetRegistrationId)}`);
  }

  /** Asset by its registration id (GET /api/asset-registrations/{id}) mapped to the detail page shape; null when not found. */
  getById(assetRegistrationId: string, fallback?: AssetRecord): Observable<ApiAssetDetails | null> {
    return this.getRegistration(assetRegistrationId).pipe(
      map((registration) => (registration ? { registration, record: toAssetRecord(registration, fallback) } : null)),
      catchError((err) => (err?.status === 404 ? of(null) : throwError(() => err))),
    );
  }

  /** Asset by tag mapped to the shape the detail pages use; null when not registered. */
  getByTag(tag: string, fallback?: AssetRecord): Observable<ApiAssetDetails | null> {
    return this.getRegistrationByTag(tag).pipe(map((registration) => (registration ? { registration, record: toAssetRecord(registration, fallback) } : null)));
  }

  /** Every component of the asset (flat; parentComponentId links them into a tree). */
  getComponents(assetRegistrationId: string): Observable<AssetComponentDto[]> {
    const params = new HttpParams().set('pageSize', 200);
    return this.http
      .get<AssetComponentListDto>(`${this.base}/${encodeURIComponent(assetRegistrationId)}/components`, { params })
      .pipe(map((res) => res.items ?? []));
  }

  /** Active custom field definitions for assets (asset and category scope). */
  getCustomFieldDefinitions(): Observable<AssetCustomFieldDefinitionDto[]> {
    const params = new HttpParams().set('pageSize', 200);
    return this.http.get<AssetCustomFieldListDto>(`${ASSET_API}asset-custom-fields`, { params }).pipe(map((res) => res.items ?? []));
  }
}
