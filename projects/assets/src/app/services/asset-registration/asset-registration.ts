import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, expand, map, reduce, EMPTY } from 'rxjs';
import { AuthService, environment } from 'shared-ui';

/** Swagger: {assetRegistrationApiUrl}swagger/index.html, tag "Asset Registration". */

export interface AssetBasicInformation {
  assetName: string;
  assetTag: string;
  barcode: string;
  rfidTagId: string;
  model: string;
  manufacturer: string;
  serialNumber: string;
  status: string;
  condition: string;
  criticality: string;
  description: string;
  images?: string[] | null;
}

export interface AssetClassification {
  category: string;
  subCategory: string;
  assetType: string;
  assetSubType: string;
  functionPurpose: string;
  criticality: string;
  department: string;
  costCenter: string;
  businessUnit: string;
  taggingMethod: string;
  complianceCategory: string;
  lifecycleStage: string;
  keywords: string[];
}

export interface AssetLocation {
  projectId: string;
  countryId: string;
  countryName: string;
  siteId: string;
  siteName: string;
  buildingId: string;
  buildingName: string;
  floorId: string;
  floorName: string;
  zoneId: string;
  zoneName: string;
  roomId: string;
  roomName: string;
  locationPath: string;
}

export interface AssetAssignment {
  custodianDepartment: string;
  assignedToUserId: string;
  assignedToName: string;
  ownershipType: string;
  costCenter: string;
  responsibleTeam: string;
  assignmentStartDate: string | null;
  assignmentEndDate: string | null;
  isCurrent: boolean | null;
}

export interface AssetLocationAssignment {
  location: AssetLocation;
  assignment: AssetAssignment;
}

export interface AssetDocumentRef {
  fileName: string;
  url: string;
  contentType: string;
  fileSize: number | null;
  uploadedAt: string | null;
}

export interface AssetPurchase {
  purchaseOrderNumber: string;
  invoiceNumber: string;
  supplierVendor: string;
  manufacturer: string;
  purchaseDate: string | null;
  deliveryDate: string | null;
  purchaseCost: number | null;
  quantity: number | null;
  quantityUnit: string;
  currency: string;
  taxPercent: number | null;
  totalCost: number | null;
  paymentTerms: string;
  procurementMethod: string;
  projectReference: string;
  remarks: string;
}

export interface AssetWarranty {
  warrantyType: string;
  warrantyStartDate: string | null;
  warrantyEndDate?: string | null;
  /** Whole number (int32 in the API). */
  warrantyPeriod: number | null;
  warrantyPeriodUnit: string;
  amcIncluded: boolean | null;
  supportCoverage: string;
  serviceProvider: string;
  warrantyReference: string;
}

export interface AssetPurchaseWarranty {
  purchase: AssetPurchase;
  documents: AssetDocumentRef[];
  warranty: AssetWarranty;
}

export interface AssetSpecification {
  name: string;
  value: string;
  unit: string;
}

export interface AssetTechnicalDetails {
  specifications: AssetSpecification[];
  dimensionLength: number | null;
  dimensionWidth: number | null;
  dimensionHeight: number | null;
  dimensionUnit: string;
  weight: number | null;
  weightUnit: string;
  /** Whole number (int32 in the API). */
  expectedLifespan: number | null;
  expectedLifespanUnit: string;
  technicalRemarks: string;
}

export interface AssetOperationalDetails {
  operatingHoursPerDay: number | null;
  operatingHoursUnit: string;
  operatingShift: string;
  utilization: string;
  /** Whole number (int32 in the API). */
  maintenanceFrequency: number | null;
  maintenanceFrequencyUnit: string;
  lastMaintenanceDate: string | null;
  nextMaintenanceDate: string | null;
  complianceStandards: string[];
}

export interface AssetCustomField {
  fieldName: string;
  fieldType: string;
  value: string;
}

export interface AssetAdditionalInformation {
  technical: AssetTechnicalDetails;
  operational: AssetOperationalDetails;
  customFields: AssetCustomField[];
  attachments: AssetDocumentRef[];
}

/** Draft step sections, in wizard order; each is the last path segment of its PUT endpoint. */
export type AssetRegistrationSection =
  | 'basic-information'
  | 'classification'
  | 'location-assignment'
  | 'purchase-warranty'
  | 'additional-information';

export interface AssetRegistrationSectionBody {
  'basic-information': AssetBasicInformation;
  classification: AssetClassification;
  'location-assignment': AssetLocationAssignment;
  'purchase-warranty': AssetPurchaseWarranty;
  'additional-information': AssetAdditionalInformation;
}

/** Registration (draft or submitted) as returned by the API. */
export interface AssetRegistrationResult {
  id: string;
  /** Business id such as "AR000003"; used in every draft URL. */
  assetRegistrationId: string;
  basicInformation?: AssetBasicInformation;
  classification?: AssetClassification;
  locationAssignment?: AssetLocationAssignment;
  purchaseWarranty?: AssetPurchaseWarranty;
  additionalInformation?: AssetAdditionalInformation;
  status?: string;
  createdAt?: string;
}

/** Row of GET /api/asset-registrations. */
export interface AssetRegistrationListItem {
  id: string;
  assetRegistrationId: string;
  assetName: string;
  assetTag: string;
  imageUrl: string | null;
  category: string;
  assetType: string;
  siteName: string;
  buildingName: string;
  floorName: string;
  zoneName: string;
  roomName: string | null;
  locationPath: string;
  custodianUserId: string;
  custodianName: string;
  department: string;
  status: string;
  criticality: string;
  manufacturer: string;
  nextMaintenanceDate: string | null;
  createdAt: string;
}

export interface AssetRegistrationListQuery {
  search?: string;
  category?: string;
  assetType?: string;
  status?: string;
  locationId?: string;
  department?: string;
  custodianUserId?: string;
  criticality?: string;
  manufacturer?: string;
  condition?: string;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
  page?: number;
  pageSize?: number;
}

export interface AssetRegistrationListResult {
  items: AssetRegistrationListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  summary: { total: number; active: number; underMaintenance: number; inRepair: number; disposed: number; other: number };
}

/** Largest pageSize the list API honours (larger values fall back to its default of 20). */
const MAX_PAGE_SIZE = 200;

/** GET /api/asset-registrations/filters — values present in registered assets, for the list filters. */
export interface AssetRegistrationFilters {
  categories: string[];
  assetTypes: { assetType: string; category: string }[];
  statuses: string[];
  /** id is what the list's locationId filter takes; path is "UAE > Dubai HQ > Main Building". */
  locations: { id: string; name: string; level: string; path: string }[];
  departments: string[];
  custodians: { userId: string; name: string }[];
  criticalities: string[];
  manufacturers: string[];
  conditions: string[];
}

/** Request body for POST /api/asset-registrations/drafts */
interface CreateAssetRegistrationDraftRequest {
  basicInformation: AssetBasicInformation;
  createdBy: string;
  clientId: string;
  tenantId: string;
}

@Injectable({ providedIn: 'root' })
export class AssetRegistrationService {
  private apiUrl = `${environment.assetRegistrationApiUrl}api/asset-registrations`;

  constructor(private http: HttpClient, private auth: AuthService) {}

  /** GET /api/asset-registrations (paged, filtered list of registered assets) */
  getAll(query: AssetRegistrationListQuery = {}): Observable<AssetRegistrationListResult> {
    const params: Record<string, string | number> = {};
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== '') params[key] = value;
    }
    return this.http.get<AssetRegistrationListResult>(this.apiUrl, { params });
  }

  /** GET /api/asset-registrations/filters */
  getFilters(): Observable<AssetRegistrationFilters> {
    return this.http.get<AssetRegistrationFilters>(`${this.apiUrl}/filters`);
  }

  /**
   * Every asset matching the query, fetched page by page (the API caps pageSize), for exports and for filters the API
   * cannot apply itself.
   */
  getAllMatching(query: Omit<AssetRegistrationListQuery, 'page' | 'pageSize'> = {}): Observable<AssetRegistrationListItem[]> {
    const pageSize = MAX_PAGE_SIZE;
    return this.getAll({ ...query, page: 1, pageSize }).pipe(
      expand((res) => (res.page * pageSize < res.totalCount ? this.getAll({ ...query, page: res.page + 1, pageSize }) : EMPTY)),
      map((res) => res.items),
      reduce((all, items) => all.concat(items), [] as AssetRegistrationListItem[]),
    );
  }

  /** GET /api/asset-registrations/{assetRegistrationId} */
  getById(assetRegistrationId: string): Observable<AssetRegistrationResult> {
    return this.http.get<AssetRegistrationResult>(`${this.apiUrl}/${encodeURIComponent(assetRegistrationId)}`);
  }

  /** GET /api/asset-registrations/drafts */
  getDrafts(): Observable<AssetRegistrationResult[]> {
    return this.http.get<AssetRegistrationResult[]>(`${this.apiUrl}/drafts`);
  }

  /** GET /api/asset-registrations/drafts/{assetRegistrationId} */
  getDraft(assetRegistrationId: string): Observable<AssetRegistrationResult> {
    return this.http.get<AssetRegistrationResult>(`${this.apiUrl}/drafts/${encodeURIComponent(assetRegistrationId)}`);
  }

  /** POST /api/asset-registrations/drafts — starts a draft from the Basic Information step. */
  createDraft(basicInformation: AssetBasicInformation, saveAsDraft = false): Observable<AssetRegistrationResult> {
    const request: CreateAssetRegistrationDraftRequest = {
      basicInformation,
      createdBy: this.auth.getUserEmail() || 'unknown',
      clientId: environment.clientId,
      tenantId: environment.tenantId,
    };
    return this.http.post<AssetRegistrationResult>(`${this.apiUrl}/drafts`, request, { params: { saveAsDraft } });
  }

  /** PUT /api/asset-registrations/drafts/{assetRegistrationId}/{section} */
  saveSection<S extends AssetRegistrationSection>(
    assetRegistrationId: string,
    section: S,
    body: AssetRegistrationSectionBody[S],
    saveAsDraft = false,
  ): Observable<AssetRegistrationResult> {
    return this.http.put<AssetRegistrationResult>(
      `${this.apiUrl}/drafts/${encodeURIComponent(assetRegistrationId)}/${section}`,
      body,
      { params: { saveAsDraft } },
    );
  }

  /** POST /api/asset-registrations/drafts/{assetRegistrationId}/submit — registers the asset. */
  submit(assetRegistrationId: string): Observable<AssetRegistrationResult> {
    return this.http.post<AssetRegistrationResult>(`${this.apiUrl}/drafts/${encodeURIComponent(assetRegistrationId)}/submit`, {});
  }

  /** DELETE /api/asset-registrations/drafts/{assetRegistrationId} */
  deleteDraft(assetRegistrationId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/drafts/${encodeURIComponent(assetRegistrationId)}`);
  }
}
