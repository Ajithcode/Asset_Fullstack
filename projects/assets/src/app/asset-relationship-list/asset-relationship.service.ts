import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, catchError, map, of, throwError } from 'rxjs';
import { AuthService } from 'shared-ui';
import { ASSET_API, TENANT } from '../services/asset-api';

/** "Connected Asset", "Supporting Asset", "Component" or "Sub Asset" (GET /api/asset-relationships/types). */
export type ApiRelationshipType = 'Connected Asset' | 'Supporting Asset' | 'Component' | 'Sub Asset';

export const API_RELATIONSHIP_TYPES: ApiRelationshipType[] = ['Connected Asset', 'Supporting Asset', 'Component', 'Sub Asset'];

/** One linked sub asset (AssetRelationshipSubAssetResponse): a registered asset or a component of the main asset. */
export interface SubAssetDto {
  assetRegistrationId: string | null;
  componentId: string | null;
  name: string;
  tag: string | null;
  relationshipType: string;
  category: string | null;
  location: string | null;
}

/** A main asset and its sub assets (AssetRelationshipResponse). */
export interface AssetRelationshipDto {
  id: string;
  mainAssetRegistrationId: string;
  mainAssetName: string | null;
  mainAssetTag: string | null;
  mainCategory: string | null;
  mainLocation: string | null;
  subAssetCount: number;
  subAssets: SubAssetDto[];
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  lastUpdated: string | null;
  clientId: string | null;
  tenantId: string | null;
}

interface AssetRelationshipListDto {
  items: AssetRelationshipDto[];
  totalCount: number;
  totalSubAssetLinks: number;
  page: number;
  pageSize: number;
}

/** One section of the Related Assets popup (AssetRelatedAssetsResponse). */
export interface RelatedAssetsDto {
  relationshipId: string | null;
  mainAssetRegistrationId: string;
  mainAssetName: string | null;
  mainAssetTag: string | null;
  mainCategory: string | null;
  mainLocation: string | null;
  status: string | null;
  subAssetCount: number;
  subAssets: SubAssetDto[];
}

/** A sub asset to link: send assetRegistrationId for a registered asset or componentId for a component. */
export interface SubAssetRef {
  assetRegistrationId?: string | null;
  componentId?: string | null;
  relationshipType?: string | null;
}

/** One row of GET /api/asset-registrations, reduced to what the relationship screens show. */
export interface RegisteredAsset {
  /** Asset ID (assetRegistrationId), used as the asset's key on these screens. */
  id: string;
  name: string;
  assetTag: string;
  category: string;
  assetType: string;
  location: string;
  status: string;
  criticality: string;
  manufacturer: string;
  imageUrl: string | null;
}

interface AssetListItemDto {
  id: string;
  assetRegistrationId: string;
  assetName: string | null;
  assetTag: string | null;
  imageUrl: string | null;
  category: string | null;
  assetType: string | null;
  locationPath: string | null;
  status: string | null;
  criticality: string | null;
  manufacturer: string | null;
}

interface AssetRegistrationListDto {
  items: AssetListItemDto[];
  totalCount: number;
}

/** The parts of GET /api/asset-registrations/{id} the Relationships tab header uses. */
export interface RegisteredAssetDetail extends RegisteredAsset {
  model: string;
  condition: string;
}

interface AssetRegistrationDetailDto {
  assetRegistrationId: string;
  basicInformation: {
    assetName: string | null;
    assetTag: string | null;
    model: string | null;
    manufacturer: string | null;
    status: string | null;
    condition: string | null;
    criticality: string | null;
    images: string[] | null;
  } | null;
  classification: { category: string | null; assetType: string | null; criticality: string | null } | null;
  locationAssignment: { location: { locationPath: string | null } | null } | null;
}

/** Largest page the relationship and asset lists accept. */
const MAX_PAGE_SIZE = 200;

function toRegisteredAsset(dto: AssetListItemDto): RegisteredAsset {
  return {
    id: dto.assetRegistrationId,
    name: dto.assetName || dto.assetRegistrationId,
    assetTag: dto.assetTag ?? '',
    category: dto.category ?? '',
    assetType: dto.assetType ?? '',
    location: dto.locationPath ?? '',
    status: dto.status ?? '',
    criticality: dto.criticality ?? '',
    manufacturer: dto.manufacturer ?? '',
    imageUrl: dto.imageUrl,
  };
}

/** Key that identifies a sub asset within one main asset (matches the API's own key). */
export function subAssetKey(sub: { assetRegistrationId?: string | null; componentId?: string | null }): string {
  return sub.componentId ? `component:${sub.componentId}` : `asset:${sub.assetRegistrationId ?? ''}`;
}

/** A saved sub asset as a request entry, optionally with a new relationship type. */
export function toSubAssetRef(sub: SubAssetDto, relationshipType: string = sub.relationshipType): SubAssetRef {
  return sub.componentId
    ? { componentId: sub.componentId, relationshipType }
    : { assetRegistrationId: sub.assetRegistrationId, relationshipType };
}

/** GET /api/asset-relationships/assets/{id} (AssetRelationshipDetailsResponse), the parts the Relationships tab reads. */
interface AssetRelationshipDetailsDto {
  asset: { assetRegistrationId: string; assetName: string | null; assetTag: string | null } | null;
  location: { locationPath: string | null } | null;
  components: {
    items: { id: string; componentName: string | null; componentTag: string | null; parentComponentId: string | null }[] | null;
  } | null;
  relationshipId: string | null;
  /** The sub assets linked to this asset. */
  linkedAssets: Record<string, unknown>[] | null;
  /** The main assets this asset is linked under. */
  parentAssets: Record<string, unknown>[] | null;
}

/** The Relationships tab's data from GET /api/asset-relationships/assets/{id}. */
export interface AssetRelationshipDetails {
  /** The saved relationship with this asset as the main asset; null when it has none. */
  saved: AssetRelationshipDto | null;
  /** The asset's components as sub-asset entries (componentId set), for the "add relationship" catalog. */
  components: SubAssetDto[];
}

/** A linked asset entry as a SubAssetDto: accepts the sub-asset fields or the asset list's own names. */
function toSubAsset(r: Record<string, unknown>): SubAssetDto {
  const str = (...keys: string[]): string | null => {
    for (const k of keys) if (typeof r[k] === 'string' && (r[k] as string).trim()) return r[k] as string;
    return null;
  };
  return {
    assetRegistrationId: str('assetRegistrationId', 'subAssetRegistrationId'),
    componentId: str('componentId'),
    name: str('name', 'assetName', 'componentName') ?? '',
    tag: str('tag', 'assetTag', 'componentTag'),
    relationshipType: str('relationshipType', 'type') ?? 'Connected Asset',
    category: str('category'),
    location: str('location', 'locationPath'),
  };
}

function toRelationshipDetails(dto: AssetRelationshipDetailsDto, assetRegistrationId: string): AssetRelationshipDetails {
  const linked = (dto.linkedAssets ?? []).map(toSubAsset);
  const saved: AssetRelationshipDto | null =
    dto.relationshipId || linked.length
      ? {
          id: dto.relationshipId ?? '',
          mainAssetRegistrationId: dto.asset?.assetRegistrationId || assetRegistrationId,
          mainAssetName: dto.asset?.assetName ?? null,
          mainAssetTag: dto.asset?.assetTag ?? null,
          mainCategory: null,
          mainLocation: dto.location?.locationPath ?? null,
          subAssetCount: linked.length,
          subAssets: linked,
          createdBy: null,
          createdAt: null,
          updatedBy: null,
          updatedAt: null,
          lastUpdated: null,
          clientId: null,
          tenantId: null,
        }
      : null;
  const components: SubAssetDto[] = (dto.components?.items ?? []).map((c) => ({
    assetRegistrationId: dto.asset?.assetRegistrationId || assetRegistrationId,
    componentId: c.id,
    name: c.componentName ?? c.componentTag ?? c.id,
    tag: c.componentTag,
    relationshipType: 'Component',
    category: null,
    location: dto.location?.locationPath ?? null,
  }));
  return { saved, components };
}

@Injectable({ providedIn: 'root' })
export class AssetRelationshipService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly url = `${ASSET_API}asset-relationships`;

  /** Every saved relationship (one per main asset), newest change first. */
  getRelationships(assetRegistrationId?: string): Observable<AssetRelationshipDto[]> {
    let params = new HttpParams().set('page', 1).set('pageSize', MAX_PAGE_SIZE);
    if (assetRegistrationId) params = params.set('assetRegistrationId', assetRegistrationId);
    return this.http.get<AssetRelationshipListDto>(this.url, { params }).pipe(map((res) => res.items ?? []));
  }

  /** The saved relationship whose main asset is this one, or null when it has none. */
  getByMainAsset(assetRegistrationId: string): Observable<AssetRelationshipDto | null> {
    return this.getRelationships(assetRegistrationId).pipe(
      map((items) => items.find((r) => r.mainAssetRegistrationId === assetRegistrationId) ?? null),
    );
  }

  /**
   * GET /api/asset-relationships/assets/{assetRegistrationId}: the asset's saved relationship (relationshipId and
   * linkedAssets) and its components, in one call for the Relationships tab.
   */
  getAssetDetails(assetRegistrationId: string): Observable<AssetRelationshipDetails> {
    return this.http
      .get<AssetRelationshipDetailsDto>(`${this.url}/assets/${encodeURIComponent(assetRegistrationId)}`)
      .pipe(map((dto) => toRelationshipDetails(dto, assetRegistrationId)));
  }

  /** Related Assets popup: saved sub assets plus not-yet-linked components, per asset. */
  getRelated(assetRegistrationIds: string[]): Observable<RelatedAssetsDto[]> {
    const params = new HttpParams().set('assetRegistrationIds', assetRegistrationIds.join(','));
    return this.http.get<RelatedAssetsDto[]>(`${this.url}/related`, { params });
  }

  /** Links sub assets to main assets; an asset that already has a record gets them merged in. */
  save(relationships: { mainAssetRegistrationId: string; subAssets: SubAssetRef[] }[]): Observable<AssetRelationshipDto[]> {
    return this.http.post<AssetRelationshipDto[]>(this.url, {
      relationships,
      createdBy: this.auth.getUserEmail(),
      ...TENANT,
    });
  }

  /** Replaces the sub assets of one main asset (must keep at least one). */
  update(id: string, subAssets: SubAssetRef[]): Observable<AssetRelationshipDto> {
    return this.http.put<AssetRelationshipDto>(`${this.url}/${encodeURIComponent(id)}`, { subAssets });
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${encodeURIComponent(id)}`);
  }

  deleteMany(ids: string[]): Observable<{ deletedCount: number }> {
    return this.http.post<{ deletedCount: number }>(`${this.url}/bulk-delete`, { ids });
  }

  /**
   * Saves the full sub asset list of a main asset: PUT when it has a record, DELETE when the list is now empty.
   * Returns the saved record, or null when it was deleted.
   */
  replaceSubAssets(relationshipId: string, subAssets: SubAssetRef[]): Observable<AssetRelationshipDto | null> {
    return subAssets.length
      ? this.update(relationshipId, subAssets)
      : this.delete(relationshipId).pipe(map(() => null));
  }

  // ===== Asset register (read only) =====

  /** Registered assets for the pickers (first 200, optionally searched by name, tag or ID). */
  getAssets(search = ''): Observable<RegisteredAsset[]> {
    let params = new HttpParams().set('page', 1).set('pageSize', MAX_PAGE_SIZE);
    if (search.trim()) params = params.set('search', search.trim());
    return this.http
      .get<AssetRegistrationListDto>(`${ASSET_API}asset-registrations`, { params })
      .pipe(map((res) => (res.items ?? []).map(toRegisteredAsset)));
  }

  /**
   * Finds an asset by its Asset ID, falling back to its asset tag. Emits null when the register has no such asset.
   */
  findAsset(idOrTag: string): Observable<RegisteredAssetDetail | null> {
    return this.http.get<AssetRegistrationDetailDto>(`${ASSET_API}asset-registrations/${encodeURIComponent(idOrTag)}`).pipe(
      map((dto) => this.toDetail(dto)),
      catchError((err: unknown) => {
        if (!(err instanceof HttpErrorResponse) || err.status !== 404) return throwError(() => err);
        return this.getAssets(idOrTag).pipe(
          map((list) => list.find((a) => a.assetTag.toLowerCase() === idOrTag.toLowerCase() || a.id === idOrTag) ?? null),
          map((a): RegisteredAssetDetail | null => (a ? { ...a, model: '', condition: '' } : null)),
        );
      }),
    );
  }

  /** Emits the asset for each key, or null for keys the register does not know. Errors are reported as null. */
  findAssetOrNull(idOrTag: string): Observable<RegisteredAssetDetail | null> {
    return this.findAsset(idOrTag).pipe(catchError(() => of(null)));
  }

  private toDetail(dto: AssetRegistrationDetailDto): RegisteredAssetDetail {
    const basic = dto.basicInformation;
    return {
      id: dto.assetRegistrationId,
      name: basic?.assetName || dto.assetRegistrationId,
      assetTag: basic?.assetTag ?? '',
      category: dto.classification?.category ?? '',
      assetType: dto.classification?.assetType ?? '',
      location: dto.locationAssignment?.location?.locationPath ?? '',
      status: basic?.status ?? '',
      criticality: basic?.criticality || dto.classification?.criticality || '',
      manufacturer: basic?.manufacturer ?? '',
      imageUrl: basic?.images?.[0] ?? null,
      model: basic?.model ?? '',
      condition: basic?.condition ?? '',
    };
  }
}
