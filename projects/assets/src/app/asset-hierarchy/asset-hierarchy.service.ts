import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { AuthService } from 'shared-ui';
import { ASSET_API, TENANT } from '../services/asset-api';

/** Raw item from GET /api/asset-hierarchy (AssetHierarchyItemResponse). */
export interface AssetHierarchyItemDto {
  id: string;
  itemKind: string;
  parentId: string | null;
  parentName: string | null;
  name: string;
  assetTag: string | null;
  assetTypeId: string | null;
  assetTypeName: string | null;
  modelId: string | null;
  modelName: string | null;
  manufacturerId: string | null;
  manufacturerName: string | null;
  status: string | null;
  childCount: number;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  clientId: string | null;
  tenantId: string | null;
}

/** Fields the hierarchy screen sends for create and edit (SaveAssetHierarchyItemRequest without audit fields). */
export interface HierarchyItemInput {
  itemKind: string;
  parentId: string | null;
  name: string;
  assetTag: string | null;
  assetTypeId: string | null;
  modelId: string | null;
  manufacturerId: string | null;
  manufacturerName: string | null;
  status: string;
}

/** Body of POST /api/asset-hierarchy and PUT /api/asset-hierarchy/{id}. */
interface SaveAssetHierarchyItemRequest extends HierarchyItemInput {
  createdBy: string | null;
  clientId: string;
  tenantId: string;
}

/** Asset type option, from GET /api/asset-category-types. */
export interface HierarchyTypeOption {
  id: string;
  name: string;
}

/** Model option, from GET /api/asset-models. */
export interface HierarchyModelOption {
  id: string;
  name: string;
  assetTypeId: string;
  manufacturerId: string | null;
  manufacturerName: string;
}

interface AssetCategoryTypeDto {
  id: string;
  typeName: string;
  status: string | null;
}

interface AssetModelDto {
  id: string;
  assetTypeId: string;
  modelName: string;
  manufacturerId: string | null;
  manufacturerName: string;
  status: string | null;
}

/** The fields of an item that a PUT must send back unchanged, so a move does not clear them. */
export function toInput(dto: AssetHierarchyItemDto): HierarchyItemInput {
  return {
    itemKind: dto.itemKind,
    parentId: dto.parentId,
    name: dto.name,
    assetTag: dto.assetTag,
    assetTypeId: dto.assetTypeId,
    modelId: dto.modelId,
    manufacturerId: dto.manufacturerId,
    manufacturerName: dto.manufacturerName,
    status: dto.status ?? 'Active',
  };
}

@Injectable({ providedIn: 'root' })
export class AssetHierarchyService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly url = `${ASSET_API}asset-hierarchy`;

  /** Every item, flat; the screen builds the tree from parentId. */
  getItems(): Observable<AssetHierarchyItemDto[]> {
    return this.http.get<AssetHierarchyItemDto[]>(this.url);
  }

  create(input: HierarchyItemInput): Observable<AssetHierarchyItemDto> {
    return this.http.post<AssetHierarchyItemDto>(this.url, this.body(input));
  }

  update(id: string, input: HierarchyItemInput): Observable<AssetHierarchyItemDto> {
    return this.http.put<AssetHierarchyItemDto>(`${this.url}/${encodeURIComponent(id)}`, this.body(input));
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${encodeURIComponent(id)}`);
  }

  getAssetTypes(): Observable<HierarchyTypeOption[]> {
    return this.http
      .get<AssetCategoryTypeDto[]>(`${ASSET_API}asset-category-types`)
      .pipe(map((list) => list.map((t) => ({ id: t.id, name: t.typeName })).sort((a, b) => a.name.localeCompare(b.name))));
  }

  getModels(): Observable<HierarchyModelOption[]> {
    return this.http.get<AssetModelDto[]>(`${ASSET_API}asset-models`, { params: new HttpParams() }).pipe(
      map((list) =>
        list
          .map((m) => ({ id: m.id, name: m.modelName, assetTypeId: m.assetTypeId, manufacturerId: m.manufacturerId, manufacturerName: m.manufacturerName }))
          .sort((a, b) => a.name.localeCompare(b.name)),
      ),
    );
  }

  private body(input: HierarchyItemInput): SaveAssetHierarchyItemRequest {
    return { ...input, createdBy: this.auth.getUserEmail(), ...TENANT };
  }
}
