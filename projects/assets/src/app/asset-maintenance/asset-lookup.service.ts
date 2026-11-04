import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of, shareReplay, throwError } from 'rxjs';
import { AssetIconKey, AssetRecord, AssetStatus, Criticality } from 'shared-ui';
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

interface AssetListResponseDto {
  items: AssetListItemDto[];
  totalCount: number;
}

/** GET /api/asset-registrations/{id} (AssetRegistrationResponse); only the sections these pages read. */
export interface AssetRegistrationDto {
  id: string;
  assetRegistrationId: string;
  basicInformation: {
    assetName: string | null;
    assetTag: string | null;
    model: string | null;
    manufacturer: string | null;
    serialNumber: string | null;
    status: string | null;
    condition: string | null;
    criticality: string | null;
    images: string[] | null;
  } | null;
  classification: {
    category: string | null;
    assetType: string | null;
    department: string | null;
    criticality: string | null;
    lifecycleStage: string | null;
  } | null;
  locationAssignment: {
    location: { siteName: string | null; buildingName: string | null; floorName: string | null; zoneName: string | null; roomName: string | null; locationPath: string | null } | null;
    assignment: {
      custodianDepartment: string | null;
      assignedToName: string | null;
      responsibleTeam: string | null;
      assignmentStartDate: string | null;
    } | null;
  } | null;
  purchaseWarranty: {
    purchase: {
      purchaseOrderNumber: string | null;
      supplierVendor: string | null;
      manufacturer: string | null;
      purchaseDate: string | null;
      deliveryDate: string | null;
      purchaseCost: number | null;
      totalCost: number | null;
      currency: string | null;
    } | null;
    warranty: {
      warrantyType: string | null;
      warrantyStartDate: string | null;
      warrantyEndDate: string | null;
      amcIncluded: boolean | null;
      serviceProvider: string | null;
    } | null;
  } | null;
  additionalInformation: {
    technical: { expectedLifespan: number | null; expectedLifespanUnit: string | null } | null;
    operational: { lastMaintenanceDate: string | null; nextMaintenanceDate: string | null; utilization: string | null } | null;
  } | null;
  createdBy: string | null;
  createdAt: string | null;
}

const STATUSES: AssetStatus[] = ['Active', 'Under Maintenance', 'In Repair', 'Disposed', 'In Storage', 'Checked Out'];
const CRITICALITIES: Criticality[] = ['Critical', 'High', 'Medium', 'Low'];
const ICON_WORDS: [RegExp, AssetIconKey][] = [
  [/generat|genset|power/i, 'generator'],
  [/chiller|pump|fan/i, 'chiller'],
  [/ups|batter/i, 'ups'],
  [/fork|lift|vehicle|crane/i, 'forklift'],
  [/fire|safety/i, 'fire'],
  [/hvac|air|ac\b|cool/i, 'ac'],
  [/laptop|computer|it\b|desktop|server/i, 'laptop'],
  [/cctv|camera|security/i, 'cctv'],
  [/solar|panel/i, 'solar'],
];

function pick<T extends string>(value: string | null | undefined, options: T[], fallback: T): T {
  return options.find((o) => o.toLowerCase() === (value ?? '').trim().toLowerCase()) ?? fallback;
}

/** yyyy-MM-dd of an API date-time ('' when missing). */
export function isoDay(value: string | null | undefined): string {
  return value ? value.slice(0, 10) : '';
}

/**
 * Asset record shown by the pages for a registered asset (from its registration list row).
 */
export function toAssetRecord(item: AssetListItemDto): AssetRecord {
  const tag = item.assetTag ?? item.assetRegistrationId;
  const text = `${item.category ?? ''} ${item.assetType ?? ''} ${item.assetName ?? ''}`;
  let id = 0;
  for (const ch of item.assetRegistrationId) id = (id * 31 + ch.charCodeAt(0)) % 100_000;
  const area = [item.buildingName, item.floorName, item.zoneName, item.roomName].filter(Boolean).join(' / ');
  return {
    id,
    tag,
    name: item.assetName ?? tag,
    category: item.category ?? '',
    type: item.assetType ?? '',
    site: item.siteName ?? '',
    area: area || item.locationPath || '',
    custodian: item.custodianName ?? '',
    department: item.department ?? '',
    status: pick(item.status, STATUSES, 'Active'),
    criticality: pick(item.criticality, CRITICALITIES, 'Medium'),
    manufacturer: item.manufacturer ?? '',
    model: '',
    nextMaintenance: isoDay(item.nextMaintenanceDate) || null,
    icon: ICON_WORDS.find(([re]) => re.test(text))?.[1] ?? 'transformer',
    image: item.imageUrl ?? '',
  };
}

/** Resolves the asset tag the pages are opened with (?tag=) to its registration in the Asset API. */
@Injectable({ providedIn: 'root' })
export class AssetLookupService {
  private readonly http = inject(HttpClient);
  private readonly byTagCache = new Map<string, Observable<AssetListItemDto | null>>();

  /** The registration whose tag is `tag` (case-insensitive), or null when no registered asset has it. */
  byTag(tag: string): Observable<AssetListItemDto | null> {
    const key = tag.trim().toUpperCase();
    let cached = this.byTagCache.get(key);
    if (!cached) {
      cached = this.http
        .get<AssetListResponseDto>(`${ASSET_API}asset-registrations`, { params: { search: tag.trim(), page: 1, pageSize: 50 } })
        .pipe(
          map((res) => res.items.find((a) => (a.assetTag ?? '').toUpperCase() === key) ?? null),
          // failed lookups are retried next time
          catchError((err) => {
            this.byTagCache.delete(key);
            return throwError(() => err);
          }),
          shareReplay(1),
        );
      this.byTagCache.set(key, cached);
    }
    return cached;
  }

  /** Full registration (READ only). */
  registration(assetRegistrationId: string): Observable<AssetRegistrationDto> {
    return this.http.get<AssetRegistrationDto>(`${ASSET_API}asset-registrations/${encodeURIComponent(assetRegistrationId)}`);
  }

  /** Registered assets for pickers (first 200, newest first). */
  list(search = ''): Observable<AssetListItemDto[]> {
    const params: Record<string, string | number> = { page: 1, pageSize: 200 };
    if (search.trim()) params['search'] = search.trim();
    return this.http.get<AssetListResponseDto>(`${ASSET_API}asset-registrations`, { params }).pipe(
      map((res) => res.items),
      catchError(() => of([])),
    );
  }
}
