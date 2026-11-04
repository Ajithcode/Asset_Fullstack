import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map, of, switchMap } from 'rxjs';
import { environment } from 'shared-ui';
import { ASSET_API, TENANT } from '../services/asset-api';

// ===== DTOs (Asset API /api/service-requests, System.Text.Json camelCase) =====

export interface SrReferenceDto {
  id: string | null;
  name: string;
}

export interface SrPersonDto {
  userId: string | null;
  name: string;
  role: string | null;
}

export interface SrAssetDto {
  assetRegistrationId: string;
  assetName: string | null;
  assetTag: string | null;
  category: string | null;
  location: string | null;
  department: string | null;
  status: string | null;
  imageUrl: string | null;
}

export interface SrAttachmentDto {
  attachmentId: string;
  fileName: string;
  fileUrl: string;
  fileType: string | null;
  contentType: string | null;
  fileSize: number | null;
  thumbnailUrl: string | null;
  description: string | null;
  uploadedBy: string | null;
  uploadedAt: string | null;
}

export interface SrUpdateDto {
  updateId: string;
  /** "Created", "Comment", "Status Change" or "Work Order". */
  updateType: string;
  action: string;
  message: string | null;
  status: string | null;
  recommendedAction: string | null;
  attachments: SrAttachmentDto[];
  userId: string | null;
  userName: string | null;
  createdAt: string;
}

export interface SrWorkOrderDto {
  workOrderId: string;
  title: string | null;
  workType: string | null;
  status: string | null;
  priority: string | null;
  plannedStart: string | null;
  plannedEnd: string | null;
  assignedTo: string | null;
  linkedBy: string | null;
  linkedAt: string | null;
}

export interface SrRelatedAssetDto {
  type: string;
  referenceId: string;
  number: string | null;
  name: string | null;
  relationship: string | null;
}

export interface SrSparePartDto {
  partId: string;
  partName: string | null;
  quantityUsed: number;
  usedDate: string | null;
  workOrderId: string | null;
}

export interface SrDocumentDto {
  id: string;
  documentName: string | null;
  fileType: string | null;
  fileUrl: string | null;
  uploadedAt: string | null;
  uploadedBy: string | null;
  /** "Service Request" or "Asset". */
  relatedTo: string;
}

export interface SrCountsDto {
  updatesCount: number;
  attachmentsCount: number;
  relatedCount: number;
  workOrdersCount: number;
  sparePartsCount: number;
  documentsCount: number;
}

/** GET /api/service-requests/{id} (AssetServiceRequestResponse). */
export interface ServiceRequestDto {
  id: string;
  requestNumber: string;
  status: string | null;
  asset: SrAssetDto;
  title: string;
  requestType: string | null;
  priority: string | null;
  requestedDate: string | null;
  targetDate: string | null;
  description: string | null;
  location: SrReferenceDto | null;
  department: SrReferenceDto | null;
  requestedBy: SrPersonDto | null;
  assignedTo: SrPersonDto | null;
  estimatedDowntimeHours: number | null;
  plannedStart: string | null;
  plannedEnd: string | null;
  actualStart: string | null;
  actualEnd: string | null;
  workOrder: SrWorkOrderDto | null;
  relatedServiceRequest: string | null;
  tags: string[];
  referenceNumber: string | null;
  attachments: SrAttachmentDto[];
  counts: SrCountsDto;
  submittedAt: string | null;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  clientId: string | null;
  tenantId: string | null;
}

/** One row of GET /api/service-requests (AssetServiceRequestListItemResponse). */
export interface ServiceRequestListItemDto {
  id: string;
  requestNumber: string;
  title: string;
  status: string | null;
  assetRegistrationId: string;
  assetTag: string | null;
  assetName: string | null;
  requestType: string | null;
  priority: string | null;
  requestedDate: string | null;
  targetDate: string | null;
  location: string | null;
  requestedBy: string | null;
  assignedTo: string | null;
  workOrderId: string | null;
  attachmentsCount: number;
}

export interface ServiceRequestListDto {
  items: ServiceRequestListItemDto[];
  totalCount: number;
  statusCounts: Record<string, number>;
  page: number;
  pageSize: number;
}

export interface ServiceRequestOptionsDto {
  requestTypes: string[];
  priorities: string[];
  statuses: string[];
  updateTypes: string[];
  fileTypes: string[];
  fileExtensions: string[];
  relatedAssetTypes: string[];
}

export interface ServiceRequestRelatedDto {
  workOrders: SrWorkOrderDto[];
  relatedAssets: SrRelatedAssetDto[];
  spareParts: SrSparePartDto[];
  documents: SrDocumentDto[];
  counts: SrCountsDto;
}

export interface ServiceRequestListQuery {
  search?: string;
  assetRegistrationId?: string;
  status?: string;
  priority?: string;
  requestType?: string;
  page?: number;
  pageSize?: number;
}

// ===== Requests =====

export interface SrAttachmentRequest {
  fileName: string;
  fileUrl: string;
  contentType?: string | null;
  fileSize?: number | null;
  thumbnailUrl?: string | null;
  description?: string | null;
}

/** POST / PUT body (SaveAssetServiceRequestRequest). */
export interface SaveServiceRequestBody {
  assetRegistrationId: string | null;
  title: string | null;
  requestType: string | null;
  priority: string | null;
  requestedDate: string | null;
  targetDate: string | null;
  description: string | null;
  location: { id: string | null; name: string | null } | null;
  department: { id: string | null; name: string | null } | null;
  requestedBy: { userId: string | null; name: string | null; role: string | null } | null;
  assignedTo: { userId: string | null; name: string | null; role: string | null } | null;
  estimatedDowntimeHours: number | null;
  plannedStart: string | null;
  plannedEnd: string | null;
  actualStart: string | null;
  actualEnd: string | null;
  relatedWorkOrderId: string | null;
  relatedServiceRequest: string | null;
  /** On edit, null keeps the current attachments. */
  attachments: SrAttachmentRequest[] | null;
  tags: string[] | null;
  referenceNumber: string | null;
  createdBy?: string | null;
  clientId?: string | null;
  tenantId?: string | null;
}

export interface AddUpdateBody {
  message?: string | null;
  /** New status; leave empty for a comment only. */
  status?: string | null;
  recommendedAction?: string | null;
  attachments?: SrAttachmentRequest[] | null;
}

/** Statuses the API knows (AssetServiceRequestRepository). */
export const SR_API_STATUSES = ['Draft', 'Open', 'In Progress', 'On Hold', 'Approved', 'Closed', 'Cancelled'] as const;
export const SR_API_REQUEST_TYPES = ['Corrective', 'Preventive', 'Inspection', 'Breakdown', 'Calibration', 'Installation', 'Other'];
export const SR_API_PRIORITIES = ['Low', 'Medium', 'High', 'Critical'];

/** A work order of the Maintenance service (GET /api/work-orders/asset/{assetId}). */
export interface MaintenanceWorkOrderDto {
  id: string;
  workOrderId: string;
  assetId: string;
  assetName: string;
  workType: string;
  title: string;
  description: string | null;
  priority: string;
  scheduledDate: string | null;
  assignedTechnician: string | null;
  status: string | null;
}

export interface CreateWorkOrderBody {
  workOrderId: string;
  assetId: string;
  assetName: string | null;
  workType: string;
  title: string;
  description: string | null;
  priority: string;
  scheduledDate: string | null;
  assignedTechnician: string | null;
  createdBy: string | null;
  status: string;
}

const MAINTENANCE_API = `${environment.maintenanceApiUrl}api/`;
const MONGO_ID = /^[0-9a-f]{24}$/i;

/** PUT body that keeps every value of the request (the API replaces the whole form on edit). */
export function toSaveBody(r: ServiceRequestDto): SaveServiceRequestBody {
  return {
    assetRegistrationId: r.asset.assetRegistrationId || null,
    title: r.title,
    requestType: r.requestType,
    priority: r.priority,
    requestedDate: r.requestedDate,
    targetDate: r.targetDate,
    description: r.description,
    location: r.location ? { id: r.location.id, name: r.location.name } : null,
    department: r.department ? { id: r.department.id, name: r.department.name } : null,
    requestedBy: r.requestedBy ? { userId: r.requestedBy.userId, name: r.requestedBy.name, role: r.requestedBy.role } : null,
    assignedTo: r.assignedTo ? { userId: r.assignedTo.userId, name: r.assignedTo.name, role: r.assignedTo.role } : null,
    estimatedDowntimeHours: r.estimatedDowntimeHours,
    plannedStart: r.plannedStart,
    plannedEnd: r.plannedEnd,
    actualStart: r.actualStart,
    actualEnd: r.actualEnd,
    relatedWorkOrderId: null,
    relatedServiceRequest: r.relatedServiceRequest,
    attachments: null,
    tags: r.tags ?? [],
    referenceNumber: r.referenceNumber,
  };
}

/** Service requests raised on assets: the request page and the New Service Request form. */
@Injectable({ providedIn: 'root' })
export class ServiceRequestApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${ASSET_API}service-requests`;

  list(query: ServiceRequestListQuery = {}): Observable<ServiceRequestListDto> {
    let params = new HttpParams();
    for (const [k, v] of Object.entries(query)) {
      if (v !== undefined && v !== null && v !== '') params = params.set(k, String(v));
    }
    return this.http.get<ServiceRequestListDto>(this.base, { params });
  }

  options(): Observable<ServiceRequestOptionsDto> {
    return this.http.get<ServiceRequestOptionsDto>(`${this.base}/options`);
  }

  get(id: string): Observable<ServiceRequestDto> {
    return this.http.get<ServiceRequestDto>(`${this.base}/${encodeURIComponent(id)}`);
  }

  /**
   * A request by its id or its number (e.g. "SR-2025-028"); null when there is none.
   * Numbers are looked up through the list search, optionally within one asset.
   */
  find(idOrNumber: string, assetRegistrationId?: string): Observable<ServiceRequestDto | null> {
    if (MONGO_ID.test(idOrNumber)) return this.get(idOrNumber);
    return this.list({ search: idOrNumber, assetRegistrationId, pageSize: 50 }).pipe(
      map((res) => res.items.find((i) => i.requestNumber.toLowerCase() === idOrNumber.toLowerCase()) ?? null),
      switchMap((row) => (row ? this.get(row.id) : of(null))),
    );
  }

  /** Submit Service Request (status Open), or Save as Draft. */
  create(body: SaveServiceRequestBody, saveAsDraft = false): Observable<ServiceRequestDto> {
    const params = saveAsDraft ? new HttpParams().set('saveAsDraft', true) : undefined;
    return this.http.post<ServiceRequestDto>(this.base, { ...TENANT, ...body }, { params });
  }

  update(id: string, body: SaveServiceRequestBody, saveAsDraft = false): Observable<ServiceRequestDto> {
    const params = saveAsDraft ? new HttpParams().set('saveAsDraft', true) : undefined;
    return this.http.put<ServiceRequestDto>(`${this.base}/${encodeURIComponent(id)}`, body, { params });
  }

  submit(id: string): Observable<ServiceRequestDto> {
    return this.http.post<ServiceRequestDto>(`${this.base}/${encodeURIComponent(id)}/submit`, {});
  }

  /** Deletes a draft (a submitted request is cancelled with a status update instead). */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${encodeURIComponent(id)}`);
  }

  getUpdates(id: string, type?: string): Observable<SrUpdateDto[]> {
    const params = type ? new HttpParams().set('type', type) : undefined;
    return this.http.get<{ items: SrUpdateDto[] }>(`${this.base}/${encodeURIComponent(id)}/updates`, { params }).pipe(map((r) => r.items ?? []));
  }

  /** A comment and/or a status change. */
  addUpdate(id: string, body: AddUpdateBody): Observable<ServiceRequestDto> {
    return this.http.post<ServiceRequestDto>(`${this.base}/${encodeURIComponent(id)}/updates`, body);
  }

  addAttachments(id: string, attachments: SrAttachmentRequest[]): Observable<ServiceRequestDto> {
    return this.http.post<ServiceRequestDto>(`${this.base}/${encodeURIComponent(id)}/attachments`, { attachments });
  }

  deleteAttachments(id: string, attachmentIds: string[]): Observable<ServiceRequestDto> {
    return attachmentIds.length === 1
      ? this.http.delete<ServiceRequestDto>(`${this.base}/${encodeURIComponent(id)}/attachments/${encodeURIComponent(attachmentIds[0])}`)
      : this.http.post<ServiceRequestDto>(`${this.base}/${encodeURIComponent(id)}/attachments/bulk-delete`, { attachmentIds });
  }

  getRelated(id: string): Observable<ServiceRequestRelatedDto> {
    return this.http.get<ServiceRequestRelatedDto>(`${this.base}/${encodeURIComponent(id)}/related`);
  }

  /** Links a work order that exists in the Maintenance service. */
  linkWorkOrder(id: string, workOrderId: string): Observable<ServiceRequestDto> {
    return this.http.post<ServiceRequestDto>(`${this.base}/${encodeURIComponent(id)}/work-orders`, { workOrderId });
  }

  unlinkWorkOrder(id: string, workOrderId: string): Observable<ServiceRequestDto> {
    return this.http.delete<ServiceRequestDto>(`${this.base}/${encodeURIComponent(id)}/work-orders/${encodeURIComponent(workOrderId)}`);
  }

  /** Links a work order already created in the Maintenance service as the request's work order. */
  convertToWorkOrder(id: string, workOrderId: string, closeRequest = false): Observable<ServiceRequestDto> {
    return this.http.post<ServiceRequestDto>(`${this.base}/${encodeURIComponent(id)}/convert-to-work-order`, { workOrderId, closeRequest });
  }

  /** Work orders of an asset in the Maintenance service (assetId is the AssetRegistrationId). */
  workOrdersOfAsset(assetRegistrationId: string): Observable<MaintenanceWorkOrderDto[]> {
    return this.http.get<MaintenanceWorkOrderDto[]>(`${MAINTENANCE_API}work-orders/asset/${encodeURIComponent(assetRegistrationId)}`);
  }

  /** Creates a work order in the Maintenance service (Convert to Work Order, step 1). */
  createWorkOrder(body: CreateWorkOrderBody): Observable<MaintenanceWorkOrderDto> {
    return this.http.post<MaintenanceWorkOrderDto>(`${MAINTENANCE_API}work-orders`, { ...TENANT, ...body });
  }

  /** Uploads a file through the Media service (jpg, jpeg, png, webp, pdf) and returns it as an attachment. */
  upload(file: File, category = 'service-requests'): Observable<SrAttachmentRequest> {
    const form = new FormData();
    form.append('file', file);
    form.append('category', category);
    return this.http.post<{ url: string }>(`${ASSET_API}media/upload`, form).pipe(
      map((res) => ({ fileName: file.name, fileUrl: res.url, contentType: file.type || null, fileSize: file.size })),
    );
  }
}

// ===== Shared helpers for the request pages =====

/** Local 'yyyy-MM-ddTHH:mm' of an API date-time, or ''. */
export function localStamp(value: string | null | undefined): string {
  if (!value) return '';
  const d = new Date(value);
  if (isNaN(d.getTime())) return value.slice(0, 16);
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
}

/** ISO (UTC) of a local 'yyyy-MM-ddTHH:mm' input value, or null. */
export function localToIso(value: string | null | undefined): string | null {
  if (!value) return null;
  const d = new Date(value);
  return isNaN(d.getTime()) ? null : d.toISOString();
}

export function fmtFileSize(bytes: number | null | undefined): string {
  if (!bytes) return '-';
  return bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
}
