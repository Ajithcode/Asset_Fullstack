import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { AuthService } from 'shared-ui';
import { ASSET_API, TENANT } from '../services/asset-api';

export type Status = 'Active' | 'Inactive';

/** Asset type (UI shape) from /api/asset-category-types. */
export interface AppAssetType {
  id: string;
  categoryId: string;
  categoryName: string;
  name: string;
  code: string;
  description: string;
  status: Status;
  imageUrl: string | null;
  modelCount: number;
}

/** Asset model (UI shape) from /api/asset-models. */
export interface AppAssetModel {
  id: string;
  assetTypeId: string;
  name: string;
  code: string;
  manufacturerId: string | null;
  manufacturer: string;
  status: Status;
}

/** Category option for the type's category picker (from /api/asset-categories). */
export interface CategoryOption {
  id: string;
  name: string;
  parentId: string | null;
}

export interface AssetTypeFormValue {
  categoryId: string;
  name: string;
  code: string;
  description: string;
  status: Status;
  imageUrl?: string | null;
}

export interface AssetModelFormValue {
  assetTypeId: string;
  name: string;
  code: string;
  manufacturerId?: string | null;
  manufacturer: string;
  status: Status;
}

// ===== API contracts (System.Text.Json camelCase) =====

interface AssetCategoryTypeDto {
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

interface CreateAssetCategoryTypeRequest {
  categoryId: string;
  typeName: string;
  typeCode: string;
  description: string | null;
  status: string;
  createdBy: string;
  clientId: string;
  tenantId: string;
}

interface UpdateAssetCategoryTypeRequest {
  categoryId: string;
  typeName: string;
  typeCode: string;
  description: string | null;
  status: string;
  imageUrl: string | null;
}

interface AssetModelDto {
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

/** Body for both POST and PUT /api/asset-models. */
interface SaveAssetModelRequest {
  assetTypeId: string;
  modelName: string;
  modelCode: string;
  manufacturerId: string | null;
  manufacturerName: string;
  status: string;
  createdBy: string | null;
  clientId: string | null;
  tenantId: string | null;
}

interface AssetCategoryDto {
  id: string;
  parentCategoryId: string | null;
  name: string;
}

function toStatus(status: string | null | undefined): Status {
  return status?.toLowerCase() === 'inactive' ? 'Inactive' : 'Active';
}

function toAppType(dto: AssetCategoryTypeDto): AppAssetType {
  return {
    id: dto.id,
    categoryId: dto.categoryId,
    categoryName: dto.categoryName ?? '',
    name: dto.typeName,
    code: dto.typeCode,
    description: dto.description ?? '',
    status: toStatus(dto.status),
    imageUrl: dto.imageUrl ?? null,
    modelCount: dto.modelCount ?? 0,
  };
}

function toAppModel(dto: AssetModelDto): AppAssetModel {
  return {
    id: dto.id,
    assetTypeId: dto.assetTypeId,
    name: dto.modelName,
    code: dto.modelCode,
    manufacturerId: dto.manufacturerId ?? null,
    manufacturer: dto.manufacturerName ?? '',
    status: toStatus(dto.status),
  };
}

@Injectable({ providedIn: 'root' })
export class AssetTypesModelsService {
  private readonly typesUrl = `${ASSET_API}asset-category-types`;
  private readonly modelsUrl = `${ASSET_API}asset-models`;
  private readonly categoriesUrl = `${ASSET_API}asset-categories`;

  constructor(private readonly http: HttpClient, private readonly auth: AuthService) {}

  private get user(): string {
    return this.auth.getUserEmail() || 'unknown';
  }

  // ===== Categories (picker only) =====

  /** GET /api/asset-categories */
  getCategories(): Observable<CategoryOption[]> {
    return this.http
      .get<AssetCategoryDto[]>(this.categoriesUrl)
      .pipe(map((list) => list.map((c) => ({ id: c.id, name: c.name, parentId: c.parentCategoryId ?? null }))));
  }

  // ===== Asset types =====

  /** GET /api/asset-category-types */
  getTypes(): Observable<AppAssetType[]> {
    return this.http.get<AssetCategoryTypeDto[]>(this.typesUrl).pipe(map((list) => list.map(toAppType)));
  }

  /** POST /api/asset-category-types */
  createType(form: AssetTypeFormValue): Observable<AppAssetType> {
    const body: CreateAssetCategoryTypeRequest = {
      categoryId: form.categoryId,
      typeName: form.name,
      typeCode: form.code,
      description: form.description || null,
      status: form.status,
      createdBy: this.user,
      clientId: TENANT.clientId,
      tenantId: TENANT.tenantId,
    };
    return this.http.post<AssetCategoryTypeDto>(this.typesUrl, body).pipe(map(toAppType));
  }

  /** PUT /api/asset-category-types/{id} */
  updateType(id: string, form: AssetTypeFormValue): Observable<AppAssetType> {
    const body: UpdateAssetCategoryTypeRequest = {
      categoryId: form.categoryId,
      typeName: form.name,
      typeCode: form.code,
      description: form.description || null,
      status: form.status,
      imageUrl: form.imageUrl ?? null,
    };
    return this.http.put<AssetCategoryTypeDto>(`${this.typesUrl}/${id}`, body).pipe(map(toAppType));
  }

  /** DELETE /api/asset-category-types/{id} (409 while it has models or field layouts). */
  deleteType(id: string): Observable<void> {
    return this.http.delete<void>(`${this.typesUrl}/${id}`);
  }

  // ===== Asset models =====

  /** GET /api/asset-models */
  getModels(): Observable<AppAssetModel[]> {
    return this.http.get<AssetModelDto[]>(this.modelsUrl).pipe(map((list) => list.map(toAppModel)));
  }

  /** POST /api/asset-models */
  createModel(form: AssetModelFormValue): Observable<AppAssetModel> {
    return this.http
      .post<AssetModelDto>(this.modelsUrl, this.modelBody(form, true))
      .pipe(map(toAppModel));
  }

  /** PUT /api/asset-models/{id} */
  updateModel(id: string, form: AssetModelFormValue): Observable<AppAssetModel> {
    return this.http
      .put<AssetModelDto>(`${this.modelsUrl}/${id}`, this.modelBody(form, false))
      .pipe(map(toAppModel));
  }

  /** DELETE /api/asset-models/{id} */
  deleteModel(id: string): Observable<void> {
    return this.http.delete<void>(`${this.modelsUrl}/${id}`);
  }

  private modelBody(form: AssetModelFormValue, isCreate: boolean): SaveAssetModelRequest {
    return {
      assetTypeId: form.assetTypeId,
      modelName: form.name,
      modelCode: form.code,
      manufacturerId: form.manufacturerId ?? null,
      manufacturerName: form.manufacturer,
      status: form.status,
      createdBy: isCreate ? this.user : null,
      clientId: isCreate ? TENANT.clientId : null,
      tenantId: isCreate ? TENANT.tenantId : null,
    };
  }
}
