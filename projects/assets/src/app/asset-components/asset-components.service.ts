import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, forkJoin, map, of, switchMap } from 'rxjs';
import { AuthService } from 'shared-ui';
import { ASSET_API, TENANT, MEDIA_API } from '../services/asset-api';

// ===== API contracts (System.Text.Json, camelCase) =====

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

interface AssetListResponseDto {
  items: AssetListItemDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** The parts of GET /api/asset-registrations/{id} (AssetRegistrationResponse) this page reads. */
interface AssetRegistrationDto {
  assetRegistrationId: string;
  basicInformation: { model: string | null; manufacturer: string | null } | null;
}

/** AssetComponentResponse. */
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
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  clientId: string | null;
  tenantId: string | null;
}

interface AssetComponentListDto {
  items: AssetComponentDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** SaveAssetComponentRequest (create and update). */
export interface SaveAssetComponentRequest {
  parentComponentId: string | null;
  componentName: string;
  componentTag: string;
  status: string;
  model: string | null;
  manufacturer: string | null;
  partNumber: string | null;
  serialNumber: string | null;
  installedDate: string | null;
  expectedReplacementDate: string | null;
  lifecycleUsed: number;
  lifecycleRatedLife: number;
  lifecycleUnit: string;
  description: string | null;
  createdBy: string | null;
  clientId: string;
  tenantId: string;
}

/** AssetDocumentResponse. */
export interface AssetDocumentDto {
  id: string;
  assetRegistrationId: string;
  assetName: string | null;
  assetTag: string | null;
  documentName: string;
  documentType: string;
  lifecycleStage: string;
  version: string;
  expiryDate: string | null;
  description: string | null;
  tags: string[];
  status: string | null;
  fileName: string;
  fileUrl: string;
  contentType: string | null;
  fileSize: number | null;
  uploadedBy: string | null;
  uploadDate: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
}

interface AssetDocumentListDto {
  items: AssetDocumentDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** Details of a component document / attachment; the file comes from an upload. */
export interface ComponentFileDetails {
  documentName: string;
  documentType: string;
  version: string;
  status: 'Active' | 'Inactive';
  description: string;
}

export interface UploadedFile {
  fileName: string;
  fileUrl: string;
  contentType: string | null;
  fileSize: number;
}

/**
 * Component documents and attachments are kept as asset documents (/api/asset-documents) of the component's
 * asset, linked to the component by a tag. Documents and attachments use different tags so each tab only
 * lists its own files.
 */
export type ComponentFileKind = 'document' | 'attachment';

const FILE_TAG_PREFIX: Record<ComponentFileKind, string> = { document: 'component-doc:', attachment: 'component-att:' };
/** Asset documents need a lifecycle stage; component files are filed under this one. */
const COMPONENT_FILE_STAGE = 'Operation';
const MAX_PAGE_SIZE = 200;

export function componentFileTag(kind: ComponentFileKind, componentApiId: string): string {
  return FILE_TAG_PREFIX[kind] + componentApiId;
}

/** "2026-01-15T00:00:00Z" → "2026-01-15"; empty when not set. */
export function isoDate(value: string | null | undefined): string {
  return value ? value.slice(0, 10) : '';
}

/** "2026-01-15T09:30:00Z" → local "2026-01-15T09:30"; empty when not set. */
export function isoLocalDateTime(value: string | null | undefined): string {
  if (!value) return '';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value.slice(0, 16);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

@Injectable({ providedIn: 'root' })
export class AssetComponentsService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  private readonly registrationsUrl = `${ASSET_API}asset-registrations`;
  private readonly documentsUrl = `${ASSET_API}asset-documents`;
  private readonly mediaUrl = `${MEDIA_API}upload`;

  get userEmail(): string | null {
    return this.auth.getUserEmail();
  }

  // ===== Assets =====

  /** Registered assets for the asset picker (first 200; `search` narrows, e.g. by tag). */
  listAssets(search?: string): Observable<AssetListItemDto[]> {
    let params = new HttpParams().set('page', 1).set('pageSize', MAX_PAGE_SIZE).set('sortBy', 'assetName');
    if (search) params = params.set('search', search);
    return this.http.get<AssetListResponseDto>(this.registrationsUrl, { params }).pipe(map((r) => r.items ?? []));
  }

  /** Model and manufacturer of an asset (not part of the list rows). */
  getAssetBasics(assetRegistrationId: string): Observable<{ model: string; manufacturer: string }> {
    return this.http
      .get<AssetRegistrationDto>(`${this.registrationsUrl}/${encodeURIComponent(assetRegistrationId)}`)
      .pipe(map((r) => ({ model: r.basicInformation?.model ?? '', manufacturer: r.basicInformation?.manufacturer ?? '' })));
  }

  // ===== Components =====

  private componentsUrl(assetRegistrationId: string): string {
    return `${this.registrationsUrl}/${encodeURIComponent(assetRegistrationId)}/components`;
  }

  /** Every component of the asset (flat; parents via parentComponentId), all pages. */
  listComponents(assetRegistrationId: string): Observable<AssetComponentDto[]> {
    const page = (p: number) =>
      this.http.get<AssetComponentListDto>(this.componentsUrl(assetRegistrationId), {
        params: new HttpParams().set('page', p).set('pageSize', MAX_PAGE_SIZE),
      });
    return page(1).pipe(
      switchMap((first) => {
        const pages = Math.ceil((first.totalCount ?? 0) / MAX_PAGE_SIZE);
        if (pages <= 1) return of(first.items ?? []);
        const rest = Array.from({ length: pages - 1 }, (_, i) => page(i + 2));
        return forkJoin(rest).pipe(map((more) => [...(first.items ?? []), ...more.flatMap((r) => r.items ?? [])]));
      }),
    );
  }

  createComponent(assetRegistrationId: string, body: SaveAssetComponentRequest): Observable<AssetComponentDto> {
    return this.http.post<AssetComponentDto>(this.componentsUrl(assetRegistrationId), {
      ...body,
      createdBy: this.userEmail ?? body.createdBy,
      ...TENANT,
    });
  }

  updateComponent(assetRegistrationId: string, componentId: string, body: SaveAssetComponentRequest): Observable<AssetComponentDto> {
    return this.http.put<AssetComponentDto>(`${this.componentsUrl(assetRegistrationId)}/${encodeURIComponent(componentId)}`, body);
  }

  deleteComponent(assetRegistrationId: string, componentId: string): Observable<void> {
    return this.http.delete<void>(`${this.componentsUrl(assetRegistrationId)}/${encodeURIComponent(componentId)}`);
  }

  // ===== Component documents / attachments =====

  /** Files of one component, newest first. */
  listComponentFiles(kind: ComponentFileKind, assetRegistrationId: string, componentApiId: string): Observable<AssetDocumentDto[]> {
    const tag = componentFileTag(kind, componentApiId);
    const params = new HttpParams()
      .set('assetRegistrationId', assetRegistrationId)
      .set('search', tag)
      .set('page', 1)
      .set('pageSize', MAX_PAGE_SIZE);
    return this.http
      .get<AssetDocumentListDto>(this.documentsUrl, { params })
      .pipe(map((r) => (r.items ?? []).filter((d) => (d.tags ?? []).includes(tag))));
  }

  /** Every component file of one kind on the asset (all components), for asset-wide summaries. */
  listAssetComponentFiles(kind: ComponentFileKind, assetRegistrationId: string): Observable<AssetDocumentDto[]> {
    const prefix = FILE_TAG_PREFIX[kind];
    const params = new HttpParams()
      .set('assetRegistrationId', assetRegistrationId)
      .set('search', prefix)
      .set('page', 1)
      .set('pageSize', MAX_PAGE_SIZE);
    return this.http
      .get<AssetDocumentListDto>(this.documentsUrl, { params })
      .pipe(map((r) => (r.items ?? []).filter((d) => (d.tags ?? []).some((t) => t.startsWith(prefix)))));
  }

  /** Uploads the file to the Media service; returns its url and details. */
  uploadFile(file: File): Observable<UploadedFile> {
    const form = new FormData();
    form.append('file', file, file.name);
    form.append('category', 'asset-documents');
    return this.http.post<{ url: string }>(this.mediaUrl, form).pipe(
      map((r) => ({ fileName: file.name, fileUrl: r.url, contentType: file.type || null, fileSize: file.size })),
    );
  }

  createComponentFile(
    kind: ComponentFileKind,
    assetRegistrationId: string,
    componentApiId: string,
    details: ComponentFileDetails,
    file: UploadedFile,
  ): Observable<AssetDocumentDto> {
    return this.http
      .post<AssetDocumentDto[]>(this.documentsUrl, {
        assetRegistrationIds: [assetRegistrationId],
        documentName: details.documentName,
        documentType: details.documentType,
        lifecycleStage: COMPONENT_FILE_STAGE,
        version: details.version,
        expiryDate: null,
        description: details.description || null,
        tags: [componentFileTag(kind, componentApiId)],
        status: details.status,
        fileName: file.fileName,
        fileUrl: file.fileUrl,
        contentType: file.contentType,
        fileSize: file.fileSize,
        createdBy: this.userEmail,
        ...TENANT,
      })
      .pipe(map((created) => created[0]));
  }

  /** Updates details; pass `file` to replace the file as well. */
  updateComponentFile(existing: AssetDocumentDto, details: ComponentFileDetails, file?: UploadedFile): Observable<AssetDocumentDto> {
    return this.http.put<AssetDocumentDto>(`${this.documentsUrl}/${encodeURIComponent(existing.id)}`, {
      assetRegistrationId: existing.assetRegistrationId,
      documentName: details.documentName,
      documentType: details.documentType,
      lifecycleStage: existing.lifecycleStage || COMPONENT_FILE_STAGE,
      version: details.version,
      expiryDate: existing.expiryDate,
      description: details.description || null,
      tags: existing.tags,
      status: details.status,
      fileName: file?.fileName ?? null,
      fileUrl: file?.fileUrl ?? null,
      contentType: file?.contentType ?? null,
      fileSize: file?.fileSize ?? null,
    });
  }

  deleteComponentFile(id: string): Observable<void> {
    return this.http.delete<void>(`${this.documentsUrl}/${encodeURIComponent(id)}`);
  }

  deleteComponentFiles(ids: string[]): Observable<unknown> {
    return this.http.post(`${this.documentsUrl}/bulk-delete`, { ids });
  }

  /** Fetches a stored file as a Blob (for downloads). */
  fetchFile(url: string): Observable<Blob> {
    return this.http.get(url, { responseType: 'blob' });
  }
}
