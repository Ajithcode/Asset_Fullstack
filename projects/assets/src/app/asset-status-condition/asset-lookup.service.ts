import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { AssetCondition, AssetHeader, AssetIconKey, AssetStatus, Criticality } from 'shared-ui';
import { ASSET_API } from '../services/asset-api';

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

interface AssetRegistrationListDto {
  items: AssetListItemDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** A registered asset, resolved from the tag in the page's ?tag= query string. */
export interface ResolvedAsset {
  /** Key used by every Asset API endpoint (assetRegistrationId). */
  id: string;
  header: AssetHeader;
  item: AssetListItemDto;
}

/** Shown until the asset named by ?tag= has been found in the register. */
export const EMPTY_HEADER: AssetHeader = {
  tag: '', name: '', category: '-', model: '-', location: '-', status: 'Active', condition: 'Good', criticality: 'Low', image: null, icon: 'generator',
};

/** Icon for an asset without a photo, guessed from its category / type. */
export function iconFor(category: string | null, assetType: string | null): AssetIconKey {
  const text = `${assetType ?? ''} ${category ?? ''}`.toLowerCase();
  const rules: [RegExp, AssetIconKey][] = [
    [/generator|power gen/, 'generator'],
    [/ups|battery/, 'ups'],
    [/transformer|electrical/, 'transformer'],
    [/forklift|pallet|material handling/, 'forklift'],
    [/fire|safety/, 'fire'],
    [/cctv|camera|security/, 'cctv'],
    [/laptop|computer|it equipment|\bit\b/, 'laptop'],
    [/solar|renewable/, 'solar'],
    [/air condition|\bac\b|split/, 'ac'],
    [/hvac|chiller|ahu|air handling/, 'chiller'],
  ];
  return rules.find(([re]) => re.test(text))?.[1] ?? 'generator';
}

/** The header has no condition field in the register, so it follows the status (as before). */
function conditionFor(status: string): AssetCondition {
  if (status === 'In Repair' || status === 'Disposed') return 'Poor';
  if (status === 'Under Maintenance') return 'Fair';
  return 'Good';
}

export function headerFor(item: AssetListItemDto): AssetHeader {
  const tag = item.assetTag || item.assetRegistrationId;
  const status = (item.status || 'Active') as AssetStatus;
  return {
    tag,
    name: item.assetName ?? '',
    category: item.category ?? '-',
    model: [item.manufacturer, item.assetType].filter(Boolean).join(' ') || '-',
    location: item.locationPath || [item.siteName, item.buildingName, item.floorName, item.roomName].filter(Boolean).join(' - ') || '-',
    status,
    condition: conditionFor(status),
    criticality: (item.criticality || 'Low') as Criticality,
    image: item.imageUrl || null,
    icon: iconFor(item.category, item.assetType),
  };
}

/** Finds registered assets by tag (read-only GET /api/asset-registrations). */
@Injectable({ providedIn: 'root' })
export class AssetLookupService {
  private readonly apiUrl = `${ASSET_API}asset-registrations`;

  constructor(private http: HttpClient) {}

  /**
   * The asset with this tag (or asset registration id), or null when none is registered.
   * Without a tag (page opened from the menu) the first registered asset is returned.
   */
  resolve(tag: string | null): Observable<ResolvedAsset | null> {
    const wanted = tag?.trim() ?? '';
    let params = new HttpParams().set('page', 1).set('pageSize', wanted ? 50 : 1);
    if (wanted) params = params.set('search', wanted);
    return this.http.get<AssetRegistrationListDto>(`${this.apiUrl}/`, { params }).pipe(
      map((res) => {
        const items = res.items ?? [];
        const lower = wanted.toLowerCase();
        const item = wanted
          ? items.find((i) => (i.assetTag ?? '').toLowerCase() === lower) ??
            items.find((i) => i.assetRegistrationId.toLowerCase() === lower)
          : items[0];
        return item ? { id: item.assetRegistrationId, header: headerFor(item), item } : null;
      }),
    );
  }
}
