import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, EMPTY, map, expand, reduce } from 'rxjs';
import { AuthService } from 'shared-ui';
import { ASSET_API, TENANT } from '../services/asset-api';

export type CategoryStatus = 'Active' | 'Inactive';
export type FieldType = 'Text' | 'Number' | 'Date' | 'Dropdown' | 'Yes / No';

/** A custom field defined on a category (UI shape). */
export interface CategoryCustomField {
  id: string;
  name: string;
  type: FieldType;
  required: boolean;
}

/** A technical attribute defined on a category (UI shape). */
export interface CategoryAttribute {
  id: string;
  name: string;
  unit: string;
  example: string;
}

/** A category as returned by the API, flattened (the component builds the tree from parentId). */
export interface AppCategory {
  id: string;
  parentId: string | null;
  name: string;
  code: string;
  description: string;
  status: CategoryStatus;
  imageUrl: string | null;
  /** Only the fields/attributes defined on this category itself (inherited ones come from its ancestors). */
  customFields: CategoryCustomField[];
  attributes: CategoryAttribute[];
}

/** Values collected by the add/edit category forms. */
export interface CategoryFormValue {
  parentId: string | null;
  name: string;
  code: string;
  description: string;
  status: CategoryStatus;
  imageUrl?: string | null;
}

// ===== API contracts (System.Text.Json camelCase) =====

interface CustomFieldDto {
  id: string;
  fieldName: string;
  type: string;
  required: boolean;
  definedOnCategoryId: string;
  definedOnCategoryName: string;
  inherited: boolean;
  createdAt: string | null;
}

interface AttributeDto {
  id: string;
  attribute: string;
  unit: string | null;
  exampleValue: string | null;
  definedOnCategoryId: string;
  definedOnCategoryName: string;
  inherited: boolean;
  createdAt: string | null;
}

interface AssetCategoryDto {
  id: string;
  parentCategoryId: string | null;
  parentCategoryName: string | null;
  name: string;
  code: string;
  description: string | null;
  status: string | null;
  imageUrl: string | null;
  customFields: CustomFieldDto[];
  attributes: AttributeDto[];
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  clientId: string | null;
  tenantId: string | null;
}

interface CreateAssetCategoryRequest {
  parentCategoryId: string | null;
  name: string;
  code: string;
  description: string | null;
  status: string;
  createdBy: string;
  clientId: string;
  tenantId: string;
}

interface UpdateAssetCategoryRequest {
  parentCategoryId: string | null;
  name: string;
  code: string;
  description: string | null;
  status: string;
  imageUrl: string | null;
}

interface AssetListItemDto {
  category: string | null;
}

interface AssetListResponseDto {
  items: AssetListItemDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

// The API calls the yes/no field type "Checkbox".
function toFieldType(apiType: string): FieldType {
  return apiType?.toLowerCase() === 'checkbox' ? 'Yes / No' : ((['Text', 'Number', 'Date', 'Dropdown'].find((t) => t.toLowerCase() === apiType?.toLowerCase()) ?? 'Text') as FieldType);
}

function toApiFieldType(type: FieldType): string {
  return type === 'Yes / No' ? 'Checkbox' : type;
}

export function toStatus(status: string | null | undefined): CategoryStatus {
  return status?.toLowerCase() === 'inactive' ? 'Inactive' : 'Active';
}

function toAppCategory(dto: AssetCategoryDto): AppCategory {
  return {
    id: dto.id,
    parentId: dto.parentCategoryId ?? null,
    name: dto.name,
    code: dto.code,
    description: dto.description ?? '',
    status: toStatus(dto.status),
    imageUrl: dto.imageUrl ?? null,
    customFields: (dto.customFields ?? [])
      .filter((f) => !f.inherited)
      .map((f) => ({ id: f.id, name: f.fieldName, type: toFieldType(f.type), required: f.required })),
    attributes: (dto.attributes ?? [])
      .filter((a) => !a.inherited)
      .map((a) => ({ id: a.id, name: a.attribute, unit: a.unit ?? '', example: a.exampleValue ?? '' })),
  };
}

const ASSET_PAGE_SIZE = 200;

@Injectable({ providedIn: 'root' })
export class AssetCategoriesService {
  private readonly apiUrl = `${ASSET_API}asset-categories`;

  constructor(private readonly http: HttpClient, private readonly auth: AuthService) {}

  /** GET /api/asset-categories — every category (flat; build the tree from parentId). */
  getAll(): Observable<AppCategory[]> {
    return this.http.get<AssetCategoryDto[]>(this.apiUrl).pipe(map((list) => list.map(toAppCategory)));
  }

  /** POST /api/asset-categories */
  create(form: CategoryFormValue): Observable<AppCategory> {
    const body: CreateAssetCategoryRequest = {
      parentCategoryId: form.parentId,
      name: form.name,
      code: form.code,
      description: form.description || null,
      status: form.status,
      createdBy: this.auth.getUserEmail() || 'unknown',
      clientId: TENANT.clientId,
      tenantId: TENANT.tenantId,
    };
    return this.http.post<AssetCategoryDto>(this.apiUrl, body).pipe(map(toAppCategory));
  }

  /** PUT /api/asset-categories/{id} — replaces name, code, parent, description, status and image. */
  update(id: string, form: CategoryFormValue): Observable<AppCategory> {
    const body: UpdateAssetCategoryRequest = {
      parentCategoryId: form.parentId,
      name: form.name,
      code: form.code,
      description: form.description || null,
      status: form.status,
      imageUrl: form.imageUrl ?? null,
    };
    return this.http.put<AssetCategoryDto>(`${this.apiUrl}/${id}`, body).pipe(map(toAppCategory));
  }

  /** DELETE /api/asset-categories/{id} (409 when it has sub-categories, types or custom fields). */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  /** POST /api/asset-categories/{id}/custom-fields */
  addCustomField(categoryId: string, field: { name: string; type: FieldType; required: boolean }): Observable<CategoryCustomField> {
    return this.http
      .post<CustomFieldDto>(`${this.apiUrl}/${categoryId}/custom-fields`, {
        fieldName: field.name,
        type: toApiFieldType(field.type),
        required: field.required,
      })
      .pipe(map((f) => ({ id: f.id, name: f.fieldName, type: toFieldType(f.type), required: f.required })));
  }

  /** DELETE /api/asset-categories/{id}/custom-fields/{fieldId} */
  deleteCustomField(categoryId: string, fieldId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${categoryId}/custom-fields/${fieldId}`);
  }

  /** POST /api/asset-categories/{id}/attributes */
  addAttribute(categoryId: string, attribute: { name: string; unit: string; example: string }): Observable<CategoryAttribute> {
    return this.http
      .post<AttributeDto>(`${this.apiUrl}/${categoryId}/attributes`, {
        attribute: attribute.name,
        unit: attribute.unit || null,
        exampleValue: attribute.example || null,
      })
      .pipe(map((a) => ({ id: a.id, name: a.attribute, unit: a.unit ?? '', example: a.exampleValue ?? '' })));
  }

  /** DELETE /api/asset-categories/{id}/attributes/{attributeId} */
  deleteAttribute(categoryId: string, attributeId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${categoryId}/attributes/${attributeId}`);
  }

  /**
   * Registered assets counted by their classification category, keyed by the lower-cased category value
   * (assets may store a category id, name or code). Reads every page of GET /api/asset-registrations.
   */
  getAssetCountsByCategory(): Observable<Map<string, number>> {
    const url = `${ASSET_API}asset-registrations`;
    const fetchPage = (page: number) =>
      this.http.get<AssetListResponseDto>(url, {
        params: new HttpParams().set('page', page).set('pageSize', ASSET_PAGE_SIZE),
      });

    let page = 1;
    return fetchPage(page).pipe(
      expand((res) => (page * ASSET_PAGE_SIZE < res.totalCount && res.items.length ? fetchPage(++page) : EMPTY)),
      reduce((counts, res) => {
        for (const item of res.items ?? []) {
          const key = item.category?.trim().toLowerCase();
          if (key) counts.set(key, (counts.get(key) ?? 0) + 1);
        }
        return counts;
      }, new Map<string, number>()),
    );
  }
}
