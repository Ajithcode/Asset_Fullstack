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

@Injectable({ providedIn: 'root' })
export class AssetDetailsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${ASSET_API}asset-registrations`;
  private readonly byTag = new Map<string, Observable<AssetRegistrationDto | null>>();

  /**
   * GET api/asset-dashboard/assets/{assetRegistrationId}/counts: the asset's summary counts (components, documents, …).
   * Keys are normalised to lower case without a "count"/"total" prefix or suffix, e.g. { components: 3, workorders: 2 }.
   */
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
