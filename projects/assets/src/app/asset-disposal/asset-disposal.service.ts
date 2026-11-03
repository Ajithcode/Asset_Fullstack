import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { AuthService } from 'shared-ui';
import { ASSET_API, TENANT } from '../services/asset-api';

// ===== DTOs (System.Text.Json camelCase of Contracts/AssetDisposalContracts.cs) =====

/** Server statuses; the UI shows "Pending Approval" as "Pending". */
export type DisposalApiStatus = 'Draft' | 'Pending Approval' | 'Approved' | 'Rejected' | 'Completed' | 'Cancelled';

export interface DisposalPersonDto {
  userId?: string | null;
  name: string;
  roleDepartment?: string | null;
}

export interface DisposalAssetDto {
  assetRegistrationId: string;
  assetName?: string | null;
  assetTag?: string | null;
  category?: string | null;
  model?: string | null;
  serialNumber?: string | null;
  manufacturer?: string | null;
  location?: string | null;
  department?: string | null;
  condition?: string | null;
  status?: string | null;
  imageUrl?: string | null;
  estimatedValue: number;
}

export interface DisposalVendorDto {
  vendorId?: string | null;
  name: string;
  address?: string | null;
  tradeLicense?: string | null;
}

export interface DisposalMethodDto {
  method?: string | null;
  expectedDisposalDate?: string | null;
  vendor?: DisposalVendorDto | null;
  contactPerson?: string | null;
  contactNumber?: string | null;
  instructions?: string | null;
}

export interface DisposalApproverDto {
  level: number;
  approvalLevel: string;
  approver: DisposalPersonDto;
  approvalType: string;
  status: string;
  comments?: string | null;
  actedBy?: string | null;
  actedAt?: string | null;
}

export interface DisposalAttachmentDto {
  fileName: string;
  fileUrl: string;
  fileType?: string | null;
  fileSize?: number | null;
  description?: string | null;
  uploadedBy?: string | null;
  uploadedAt?: string | null;
}

export interface DisposalCompletionDto {
  actualDisposalDate?: string | null;
  recoveredValue?: number | null;
  certificateNumber?: string | null;
  certificate?: DisposalAttachmentDto | null;
  remarks?: string | null;
  completedBy?: string | null;
  completedAt?: string | null;
}

export interface DisposalMethodOptionDto {
  method: string;
  description: string;
  vendorRequired: boolean;
}

export interface DisposalOptionsDto {
  reasons: string[];
  priorities: string[];
  methods: DisposalMethodOptionDto[];
  approvalTypes: string[];
  approvalLevels: string[];
  statuses: string[];
  defaultCurrency: string;
}

export interface DisposalRequestDto {
  id: string;
  requestNumber: string;
  status?: DisposalApiStatus | null;
  currentStep: number;
  requestTitle: string;
  requestDate?: string | null;
  requestedBy?: DisposalPersonDto | null;
  reason?: string | null;
  disposalCategory?: string | null;
  priority?: string | null;
  justification?: string | null;
  currency: string;
  assetCount: number;
  totalEstimatedValue: number;
  assets: DisposalAssetDto[];
  method: DisposalMethodDto;
  methodDetails?: DisposalMethodOptionDto | null;
  approvers: DisposalApproverDto[];
  currentApprover?: DisposalApproverDto | null;
  notifyApprovers: boolean;
  attachments: DisposalAttachmentDto[];
  remarks?: string | null;
  submittedBy?: string | null;
  submittedAt?: string | null;
  completion?: DisposalCompletionDto | null;
  cancelledReason?: string | null;
  createdBy?: string | null;
  createdAt?: string | null;
  updatedBy?: string | null;
  updatedAt?: string | null;
}

export interface DisposalListItemDto {
  id: string;
  requestNumber: string;
  requestTitle: string;
  requestDate?: string | null;
  requestedBy?: string | null;
  reason?: string | null;
  priority?: string | null;
  disposalMethod?: string | null;
  vendor?: string | null;
  expectedDisposalDate?: string | null;
  assetCount: number;
  totalEstimatedValue: number;
  currency: string;
  status?: DisposalApiStatus | null;
  pendingWith?: string | null;
  disposalCategory?: string | null;
  firstAsset?: DisposalAssetDto | null;
  createdAt?: string | null;
}

export interface DisposalListResponse {
  items: DisposalListItemDto[];
  totalCount: number;
  statusCounts: Record<string, number>;
  page: number;
  pageSize: number;
}

export interface DisposalAddAssetsResult {
  request: DisposalRequestDto;
  addedCount: number;
  notFound: string[];
  rejected: Record<string, string>;
}

export interface DisposalListQuery {
  search?: string;
  status?: string;
  method?: string;
  reason?: string;
  fromDate?: string;
  toDate?: string;
  page?: number;
  pageSize?: number;
}

// ----- Request bodies -----

export interface DisposalDetailsBody {
  requestTitle: string;
  requestDate: string | null;
  requestedBy: DisposalPersonDto | null;
  reason: string | null;
  disposalCategory: string | null;
  priority: string | null;
  justification: string | null;
  currency?: string | null;
}

export interface DisposalAssetSelection {
  assetRegistrationId: string;
  estimatedValue: number;
}

export interface DisposalMethodBody {
  method: string | null;
  expectedDisposalDate: string | null;
  vendor: DisposalVendorDto | null;
  contactPerson: string | null;
  contactNumber: string | null;
  instructions: string | null;
}

export interface DisposalApproverBody {
  approvalLevel: string;
  approver: DisposalPersonDto;
  approvalType: string;
}

export interface DisposalApprovalBody {
  approvers: DisposalApproverBody[];
  notifyApprovers: boolean;
  attachments: DisposalAttachmentDto[];
  remarks: string | null;
}

export interface CompleteDisposalBody {
  actualDisposalDate: string;
  recoveredValue?: number | null;
  certificateNumber?: string | null;
  remarks?: string | null;
}

// ----- Asset picker (READ-only GET /api/asset-registrations) -----

export interface AssetRegistrationListItemDto {
  id: string;
  assetRegistrationId: string;
  assetName?: string | null;
  assetTag?: string | null;
  imageUrl?: string | null;
  category?: string | null;
  assetType?: string | null;
  siteName?: string | null;
  buildingName?: string | null;
  floorName?: string | null;
  zoneName?: string | null;
  roomName?: string | null;
  locationPath?: string | null;
  department?: string | null;
  status?: string | null;
  manufacturer?: string | null;
}

interface AssetRegistrationListResponse {
  items: AssetRegistrationListItemDto[];
  totalCount: number;
}

export interface AssetRegistrationQuery {
  search?: string;
  category?: string;
  status?: string;
  condition?: string;
  page?: number;
  pageSize?: number;
}

// ===== Status mapping =====

export function toUiStatus(status: string | null | undefined): 'Draft' | 'Pending' | 'Approved' | 'Completed' | 'Rejected' | 'Cancelled' {
  switch (status) {
    case 'Pending Approval': return 'Pending';
    case 'Approved':
    case 'Completed':
    case 'Rejected':
    case 'Cancelled':
      return status;
    default:
      return 'Draft';
  }
}

export function toApiStatus(status: string): string {
  return status === 'Pending' ? 'Pending Approval' : status;
}

/** "YYYY-MM-DD" from an API date-time, or '' when missing. */
export function dateOnly(value: string | null | undefined): string {
  return value ? value.slice(0, 10) : '';
}

function params(query: object): HttpParams {
  let p = new HttpParams();
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null && value !== '') p = p.set(key, String(value));
  }
  return p;
}

@Injectable({ providedIn: 'root' })
export class AssetDisposalService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly base = `${ASSET_API}asset-disposals`;

  currentUser(): string {
    return this.auth.getUserEmail() ?? '';
  }

  list(query: DisposalListQuery = {}): Observable<DisposalListResponse> {
    return this.http.get<DisposalListResponse>(this.base, { params: params({ pageSize: 200, ...query }) });
  }

  options(): Observable<DisposalOptionsDto> {
    return this.http.get<DisposalOptionsDto>(`${this.base}/options`);
  }

  get(id: string): Observable<DisposalRequestDto> {
    return this.http.get<DisposalRequestDto>(`${this.base}/${encodeURIComponent(id)}`);
  }

  create(body: DisposalDetailsBody, saveAsDraft = false): Observable<DisposalRequestDto> {
    return this.http.post<DisposalRequestDto>(
      this.base,
      { ...body, currency: body.currency ?? 'AED', createdBy: this.currentUser(), ...TENANT },
      { params: params({ saveAsDraft }) },
    );
  }

  updateDetails(id: string, body: DisposalDetailsBody, saveAsDraft = false): Observable<DisposalRequestDto> {
    return this.put(id, 'details', { ...body, currency: body.currency ?? 'AED', ...TENANT }, saveAsDraft);
  }

  updateAssets(id: string, assets: DisposalAssetSelection[], saveAsDraft = false): Observable<DisposalRequestDto> {
    return this.put(id, 'assets', { assets }, saveAsDraft);
  }

  addAssetsByTag(id: string, tags: string[]): Observable<DisposalAddAssetsResult> {
    return this.http.post<DisposalAddAssetsResult>(`${this.base}/${encodeURIComponent(id)}/assets/by-tag`, { tags });
  }

  importAssets(id: string, file: File): Observable<DisposalAddAssetsResult> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<DisposalAddAssetsResult>(`${this.base}/${encodeURIComponent(id)}/assets/import`, form);
  }

  updateMethod(id: string, body: DisposalMethodBody, saveAsDraft = false): Observable<DisposalRequestDto> {
    return this.put(id, 'method', body, saveAsDraft);
  }

  updateApproval(id: string, body: DisposalApprovalBody, saveAsDraft = false): Observable<DisposalRequestDto> {
    return this.put(id, 'approval', body, saveAsDraft);
  }

  submit(id: string): Observable<DisposalRequestDto> {
    return this.action(id, 'submit', {});
  }

  approve(id: string, comments?: string): Observable<DisposalRequestDto> {
    return this.action(id, 'approve', { comments: comments || null });
  }

  reject(id: string, comments: string): Observable<DisposalRequestDto> {
    return this.action(id, 'reject', { comments });
  }

  skip(id: string, comments?: string): Observable<DisposalRequestDto> {
    return this.action(id, 'skip', { comments: comments || null });
  }

  complete(id: string, body: CompleteDisposalBody): Observable<DisposalRequestDto> {
    return this.action(id, 'complete', body);
  }

  cancel(id: string, reason?: string): Observable<DisposalRequestDto> {
    return this.action(id, 'cancel', { reason: reason || null });
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${encodeURIComponent(id)}`);
  }

  /** Asset picker source: the registered assets (read only). */
  listAssets(query: AssetRegistrationQuery = {}): Observable<AssetRegistrationListItemDto[]> {
    return this.http
      .get<AssetRegistrationListResponse>(`${ASSET_API}asset-registrations`, { params: params({ pageSize: 200, ...query }) })
      .pipe(map((res) => res?.items ?? []));
  }

  private put(id: string, step: string, body: unknown, saveAsDraft: boolean): Observable<DisposalRequestDto> {
    return this.http.put<DisposalRequestDto>(`${this.base}/${encodeURIComponent(id)}/${step}`, body, { params: params({ saveAsDraft }) });
  }

  private action(id: string, name: string, body: unknown): Observable<DisposalRequestDto> {
    return this.http.post<DisposalRequestDto>(`${this.base}/${encodeURIComponent(id)}/${name}`, body);
  }
}
