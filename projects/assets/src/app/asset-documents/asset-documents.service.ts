import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { AuthService } from 'shared-ui';
import { ASSET_API, TENANT } from '../services/asset-api';
import { AssetDocument, DocVersion } from '../asset-details/asset-details';
import { AssetLookupService, toLocalDateTime } from './asset-lookup.service';

/** Raw shape returned by /api/asset-documents (AssetDocumentResponse). */
interface AssetDocumentDto {
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
  tags: string[] | null;
  status: string | null;
  fileName: string;
  fileUrl: string;
  contentType: string | null;
  fileSize: number | null;
  uploadedBy: string | null;
  uploadDate: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  clientId: string | null;
  tenantId: string | null;
}

/** Document details shared by create and update. */
interface DocumentBody {
  documentName: string;
  documentType: string;
  lifecycleStage: string;
  version: string;
  expiryDate: string | null;
  description: string | null;
  tags: string[];
  /** "Active" or "Inactive" (the UI's Draft). */
  status: 'Active' | 'Inactive';
}

/** Stored file sent with a create, or with an update that replaces the file. */
interface FileBody {
  fileName: string | null;
  fileUrl: string | null;
  contentType: string | null;
  fileSize: number | null;
}

/** Body of POST /api/asset-documents (one document is created per asset). */
type CreateAssetDocumentRequest = DocumentBody & FileBody & {
  assetRegistrationIds: string[];
  createdBy: string | null;
  clientId: string;
  tenantId: string;
};

/** Body of PUT /api/asset-documents/{id}; an empty fileUrl keeps the current file. */
type UpdateAssetDocumentRequest = DocumentBody & FileBody & { assetRegistrationId: string };

/** A file version as shown in the UI, with the stored file's URL. */
export interface ApiDocVersion extends DocVersion {
  url: string;
  fileName: string;
  contentType: string | null;
}

/** A document of the UI backed by an API record. `id` is a local number; `apiId` is the record id. */
export interface ApiDocument extends AssetDocument {
  apiId: string;
  assetRegistrationId: string;
  assetName: string;
  versions: ApiDocVersion[];
}

/** Details collected by the upload / edit dialogs. */
export interface DocumentFormValue {
  name: string;
  type: string;
  stage: string;
  version: string;
  /** yyyy-MM-dd or ''. */
  expiryDate: string;
  draft: boolean;
  description: string;
  tags: string[];
}

/** A file already stored in the Media service. */
export interface StoredFile {
  url: string;
  name: string;
  contentType: string | null;
  size: number;
}

/** Local ids are unique across pages so documents can be listed together. */
let nextDocId = 1;

function toVersion(dto: AssetDocumentDto, note: string): ApiDocVersion {
  return {
    version: dto.version,
    uploadedAt: toLocalDateTime(dto.uploadDate),
    uploadedBy: dto.uploadedBy ?? '',
    size: dto.fileSize ?? 0,
    note,
    url: dto.fileUrl,
    fileName: dto.fileName,
    contentType: dto.contentType,
  };
}

export function toApiDocument(dto: AssetDocumentDto, id = nextDocId++): ApiDocument {
  return {
    id,
    apiId: dto.id,
    assetRegistrationId: dto.assetRegistrationId,
    assetTag: dto.assetTag?.trim() || dto.assetRegistrationId,
    assetName: dto.assetName?.trim() || '',
    name: dto.documentName,
    type: dto.documentType,
    stage: dto.lifecycleStage,
    description: dto.description ?? '',
    tags: dto.tags ?? [],
    expiryDate: dto.expiryDate ? dto.expiryDate.slice(0, 10) : '',
    draft: dto.status === 'Inactive',
    versions: dto.fileUrl ? [toVersion(dto, 'Current file')] : [],
    pages: 1,
    issuer: '',
    coverTitle: dto.documentName,
    coverSubtitle: '',
    showAssetImage: false,
  };
}

function toBody(v: DocumentFormValue): DocumentBody {
  return {
    documentName: v.name.trim(),
    documentType: v.type,
    lifecycleStage: v.stage,
    version: v.version.trim(),
    expiryDate: v.expiryDate || null,
    description: v.description.trim() || null,
    tags: v.tags,
    status: v.draft ? 'Inactive' : 'Active',
  };
}

function toFileBody(file: StoredFile | null): FileBody {
  return file
    ? { fileName: file.name, fileUrl: file.url, contentType: file.contentType, fileSize: file.size }
    : { fileName: null, fileUrl: null, contentType: null, fileSize: null };
}

@Injectable({ providedIn: 'root' })
export class AssetDocumentsService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly lookup = inject(AssetLookupService);
  private readonly apiUrl = `${ASSET_API}asset-documents`;

  /** GET /api/asset-documents (every page), optionally for one asset. */
  list(assetRegistrationId?: string): Observable<ApiDocument[]> {
    return this.lookup
      .fetchAll<AssetDocumentDto>(this.apiUrl, assetRegistrationId ? { assetRegistrationId } : {})
      .pipe(map((items) => items.map((d) => toApiDocument(d))));
  }

  /** POST /api/media/upload with category "asset-documents". */
  uploadFile(file: File): Observable<StoredFile> {
    return this.lookup
      .uploadFile(file, 'asset-documents')
      .pipe(map((url) => ({ url, name: file.name, contentType: file.type || null, size: file.size })));
  }

  /** POST /api/asset-documents: one document per asset, sharing the stored file. */
  create(assetRegistrationIds: string[], value: DocumentFormValue, file: StoredFile): Observable<ApiDocument[]> {
    const body: CreateAssetDocumentRequest = {
      assetRegistrationIds,
      ...toBody(value),
      ...toFileBody(file),
      createdBy: this.auth.getUserEmail(),
      clientId: TENANT.clientId,
      tenantId: TENANT.tenantId,
    };
    return this.http.post<AssetDocumentDto[]>(this.apiUrl, body).pipe(map((items) => items.map((d) => toApiDocument(d))));
  }

  /**
   * PUT /api/asset-documents/{id}. Pass a file to replace the stored file (new version).
   * The returned document keeps the local id of `doc`.
   */
  update(doc: ApiDocument, value: DocumentFormValue, file: StoredFile | null = null): Observable<ApiDocument> {
    const body: UpdateAssetDocumentRequest = {
      assetRegistrationId: doc.assetRegistrationId,
      ...toBody(value),
      ...toFileBody(file),
    };
    return this.http.put<AssetDocumentDto>(`${this.apiUrl}/${doc.apiId}`, body).pipe(map((d) => toApiDocument(d, doc.id)));
  }

  /** DELETE /api/asset-documents/{id} */
  delete(apiId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${apiId}`);
  }

  /** POST /api/asset-documents/bulk-delete */
  deleteMany(apiIds: string[]): Observable<number> {
    return this.http
      .post<{ deletedCount: number }>(`${this.apiUrl}/bulk-delete`, { ids: apiIds })
      .pipe(map((res) => res?.deletedCount ?? 0));
  }
}
