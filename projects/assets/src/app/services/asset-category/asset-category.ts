import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from 'shared-ui';

/**
 * Asset categories API.
 * Swagger: {assetRegistrationApiUrl}swagger/index.html, tag "Asset Categories".
 * Categories nest through parentCategoryId (null = top level). Status is Active or Inactive.
 * Custom fields and attributes defined on a category are inherited by its sub-categories; each
 * category's response already lists the inherited ones (inherited: true) before its own.
 */

export type CategoryStatus = 'Active' | 'Inactive';
export type CategoryFieldType = 'Text' | 'Number' | 'Date' | 'Dropdown' | 'Checkbox';

export const CATEGORY_FIELD_TYPES: CategoryFieldType[] = ['Text', 'Number', 'Date', 'Dropdown', 'Checkbox'];

export interface AssetCategoryCustomField {
  id: string;
  fieldName: string;
  type: CategoryFieldType;
  required: boolean;
  definedOnCategoryId: string;
  definedOnCategoryName: string;
  inherited: boolean;
  createdAt: string | null;
}

export interface AssetCategoryAttribute {
  id: string;
  attribute: string;
  unit: string | null;
  exampleValue: string | null;
  definedOnCategoryId: string;
  definedOnCategoryName: string;
  inherited: boolean;
  createdAt: string | null;
}

/** GET /api/asset-categories, GET /api/asset-categories/{id}, and the POST / PUT result. */
export interface AssetCategory {
  id: string;
  parentCategoryId: string | null;
  parentCategoryName: string | null;
  name: string;
  code: string;
  description: string | null;
  status: string | null;
  imageUrl: string | null;
  customFields: AssetCategoryCustomField[];
  attributes: AssetCategoryAttribute[];
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  clientId: string | null;
  tenantId: string | null;
}

/** Body for POST /api/asset-categories. */
export interface CreateAssetCategoryRequest {
  parentCategoryId: string | null;
  name: string;
  code: string;
  description: string | null;
  status: CategoryStatus;
  createdBy?: string | null;
  clientId?: string | null;
  tenantId?: string | null;
}

/** Body for PUT /api/asset-categories/{id} — full replace, so send every field. */
export interface UpdateAssetCategoryRequest {
  parentCategoryId: string | null;
  name: string;
  code: string;
  description: string | null;
  status: CategoryStatus;
  imageUrl: string | null;
}

export interface CreateCustomFieldRequest {
  fieldName: string;
  type: CategoryFieldType;
  required: boolean;
}

export interface CreateAttributeRequest {
  attribute: string;
  unit: string | null;
  exampleValue: string | null;
}

export interface AssetCategoryQuery {
  search?: string;
  status?: string;
  /** "root" lists top-level categories; an id lists that category's direct children. */
  parentCategoryId?: string;
}

@Injectable({ providedIn: 'root' })
export class AssetCategoryService {
  private readonly apiUrl = `${environment.assetRegistrationApiUrl}api/asset-categories`;

  constructor(private readonly http: HttpClient) {}

  /** GET /api/asset-categories — flat list, ordered by name. */
  getAll(query: AssetCategoryQuery = {}): Observable<AssetCategory[]> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (typeof value === 'string' && value.trim()) params = params.set(key, value.trim());
    }
    return this.http.get<AssetCategory[]>(this.apiUrl, { params });
  }

  /** GET /api/asset-categories/{id} */
  getById(id: string): Observable<AssetCategory> {
    return this.http.get<AssetCategory>(this.url(id));
  }

  /** POST /api/asset-categories */
  create(request: CreateAssetCategoryRequest): Observable<AssetCategory> {
    return this.http.post<AssetCategory>(this.apiUrl, {
      clientId: environment.clientId,
      tenantId: environment.tenantId,
      ...request,
    });
  }

  /** PUT /api/asset-categories/{id} */
  update(id: string, request: UpdateAssetCategoryRequest): Observable<AssetCategory> {
    return this.http.put<AssetCategory>(this.url(id), request);
  }

  /** DELETE /api/asset-categories/{id} — refused (409) while it has sub-categories, asset types or custom fields. */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(this.url(id));
  }

  /** POST /api/asset-categories/{id}/custom-fields */
  addCustomField(id: string, request: CreateCustomFieldRequest): Observable<AssetCategoryCustomField> {
    return this.http.post<AssetCategoryCustomField>(`${this.url(id)}/custom-fields`, request);
  }

  /** DELETE /api/asset-categories/{id}/custom-fields/{fieldId} — only on the category that defined it. */
  deleteCustomField(id: string, fieldId: string): Observable<void> {
    return this.http.delete<void>(`${this.url(id)}/custom-fields/${encodeURIComponent(fieldId)}`);
  }

  /** POST /api/asset-categories/{id}/attributes */
  addAttribute(id: string, request: CreateAttributeRequest): Observable<AssetCategoryAttribute> {
    return this.http.post<AssetCategoryAttribute>(`${this.url(id)}/attributes`, request);
  }

  /** DELETE /api/asset-categories/{id}/attributes/{attributeId} — only on the category that defined it. */
  deleteAttribute(id: string, attributeId: string): Observable<void> {
    return this.http.delete<void>(`${this.url(id)}/attributes/${encodeURIComponent(attributeId)}`);
  }

  /** PUT body that keeps a category as it is; spread over it to change one thing (status, image). */
  toUpdateRequest(category: AssetCategory): UpdateAssetCategoryRequest {
    return {
      parentCategoryId: category.parentCategoryId,
      name: category.name,
      code: category.code,
      description: category.description,
      status: category.status?.toLowerCase() === 'inactive' ? 'Inactive' : 'Active',
      imageUrl: category.imageUrl,
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

  private url(id: string): string {
    return `${this.apiUrl}/${encodeURIComponent(id)}`;
  }
}
