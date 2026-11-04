import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from 'shared-ui';

/**
 * Asset types and models API.
 * Swagger: {assetRegistrationApiUrl}swagger/index.html, tags "Asset Types" and "Asset Models".
 * A type belongs to an asset category (api/asset-categories); a model belongs to a type.
 * The manufacturer id comes from Master Data (api/master-data/manufacturers). Status is Active or Inactive.
 */

export type TypeModelStatus = 'Active' | 'Inactive';

/** GET /api/asset-category-types, GET /api/asset-category-types/{id}, and the POST / PUT result. */
export interface AssetTypeRecord {
  id: string;
  categoryId: string;
  categoryName: string | null;
  typeName: string;
  typeCode: string;
  description: string | null;
  status: string | null;
  imageUrl: string | null;
  modelCount: number;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  clientId: string | null;
  tenantId: string | null;
}

/** Body for POST /api/asset-category-types. */
export interface CreateAssetTypeRequest {
  categoryId: string;
  typeName: string;
  typeCode: string;
  description: string | null;
  status: TypeModelStatus;
  createdBy?: string | null;
  clientId?: string | null;
  tenantId?: string | null;
}

/** Body for PUT /api/asset-category-types/{id} — full replace, so send every field. */
export interface UpdateAssetTypeRequest {
  categoryId: string;
  typeName: string;
  typeCode: string;
  description: string | null;
  status: TypeModelStatus;
  imageUrl: string | null;
}

/** GET /api/asset-models, GET /api/asset-models/{id}, and the POST / PUT result. */
export interface AssetModelRecord {
  id: string;
  assetTypeId: string;
  assetTypeName: string | null;
  modelName: string;
  modelCode: string;
  manufacturerId: string | null;
  manufacturerName: string;
  status: string | null;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  clientId: string | null;
  tenantId: string | null;
}

/** Body for POST /api/asset-models and PUT /api/asset-models/{id}. */
export interface SaveAssetModelRequest {
  assetTypeId: string;
  modelName: string;
  modelCode: string;
  manufacturerId: string | null;
  manufacturerName: string;
  status: TypeModelStatus;
  createdBy?: string | null;
  clientId?: string | null;
  tenantId?: string | null;
}

export interface AssetTypeQuery {
  search?: string;
  status?: string;
  categoryId?: string;
}

export interface AssetModelQuery {
  search?: string;
  status?: string;
  assetTypeId?: string;
  /** Manufacturer id or name. */
  manufacturer?: string;
}

@Injectable({
  providedIn: 'root',
})
export class AssetTypes {
  private readonly typesUrl = `${environment.assetRegistrationApiUrl}api/asset-category-types`;
  private readonly modelsUrl = `${environment.assetRegistrationApiUrl}api/asset-models`;

  constructor(private readonly http: HttpClient) {}

  // ===== Asset types =====

  /** GET /api/asset-category-types */
  getTypes(query: AssetTypeQuery = {}): Observable<AssetTypeRecord[]> {
    return this.http.get<AssetTypeRecord[]>(this.typesUrl, { params: this.params({ ...query }) });
  }

  /** GET /api/asset-category-types/{id} */
  getType(id: string): Observable<AssetTypeRecord> {
    return this.http.get<AssetTypeRecord>(`${this.typesUrl}/${encodeURIComponent(id)}`);
  }

  /** POST /api/asset-category-types */
  createType(request: CreateAssetTypeRequest): Observable<AssetTypeRecord> {
    return this.http.post<AssetTypeRecord>(this.typesUrl, { clientId: environment.clientId, tenantId: environment.tenantId, ...request });
  }

  /** PUT /api/asset-category-types/{id} */
  updateType(id: string, request: UpdateAssetTypeRequest): Observable<AssetTypeRecord> {
    return this.http.put<AssetTypeRecord>(`${this.typesUrl}/${encodeURIComponent(id)}`, request);
  }

  /** DELETE /api/asset-category-types/{id} — refused (409) while the type has models or field layouts. */
  deleteType(id: string): Observable<void> {
    return this.http.delete<void>(`${this.typesUrl}/${encodeURIComponent(id)}`);
  }

  /** PUT body that keeps a type as it is; spread over it to change one thing (e.g. status). */
  toTypeRequest(t: AssetTypeRecord): UpdateAssetTypeRequest {
    return {
      categoryId: t.categoryId,
      typeName: t.typeName,
      typeCode: t.typeCode,
      description: t.description,
      status: toStatus(t.status),
      imageUrl: t.imageUrl,
    };
  }

  // ===== Asset models =====

  /** GET /api/asset-models */
  getModels(query: AssetModelQuery = {}): Observable<AssetModelRecord[]> {
    return this.http.get<AssetModelRecord[]>(this.modelsUrl, { params: this.params({ ...query }) });
  }

  /** GET /api/asset-models/{id} */
  getModel(id: string): Observable<AssetModelRecord> {
    return this.http.get<AssetModelRecord>(`${this.modelsUrl}/${encodeURIComponent(id)}`);
  }

  /** POST /api/asset-models */
  createModel(request: SaveAssetModelRequest): Observable<AssetModelRecord> {
    return this.http.post<AssetModelRecord>(this.modelsUrl, { clientId: environment.clientId, tenantId: environment.tenantId, ...request });
  }

  /** PUT /api/asset-models/{id} */
  updateModel(id: string, request: SaveAssetModelRequest): Observable<AssetModelRecord> {
    return this.http.put<AssetModelRecord>(`${this.modelsUrl}/${encodeURIComponent(id)}`, request);
  }

  /** DELETE /api/asset-models/{id} */
  deleteModel(id: string): Observable<void> {
    return this.http.delete<void>(`${this.modelsUrl}/${encodeURIComponent(id)}`);
  }

  /** PUT body that keeps a model as it is; spread over it to change one thing (e.g. status). */
  toModelRequest(m: AssetModelRecord): SaveAssetModelRequest {
    return {
      assetTypeId: m.assetTypeId,
      modelName: m.modelName,
      modelCode: m.modelCode,
      manufacturerId: m.manufacturerId,
      manufacturerName: m.manufacturerName,
      status: toStatus(m.status),
    };
  }

  /** Readable message from an API error: conflict message, first validation error, or a fallback. */
  errorMessage(error: unknown, fallback: string): string {
    const body = (error as { error?: unknown })?.error as
      | { message?: string; errors?: Record<string, string[]>; title?: string }
      | undefined;
    if (body?.message) return body.message;
    const first = body?.errors ? Object.values(body.errors).flat()[0] : undefined;
    return first ?? body?.title ?? fallback;
  }

  private params(values: Record<string, string | undefined>): HttpParams {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(values)) {
      if (value?.trim()) params = params.set(key, value.trim());
    }
    return params;
  }
}

export function toStatus(status: string | null | undefined): TypeModelStatus {
  return status?.trim().toLowerCase() === 'inactive' ? 'Inactive' : 'Active';
}
