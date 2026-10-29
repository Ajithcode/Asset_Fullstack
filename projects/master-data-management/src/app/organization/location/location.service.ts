import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, tap } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { apiErrorMessage } from '../../shared/api-error';
import { BUILDINGS, FLOORS, SITES, ZONES, blankLocation, findBuilding } from './location.data';
import { Building, Floor, LocationFilters, LocationRecord, LocationStats, LocationType, Site, Zone } from './location.model';

/** Raw shape returned by GET/POST/PUT {projectApiUrl}api/master-data/locations */
interface LocationDto {
  id: string;
  locationName: string;
  code: string;
  type: string;
  environment: string | null;
  description: string | null;
  latitude: number | null;
  longitude: number | null;
  site: string | null;
  building: string | null;
  floor: string | null;
  zone: string | null;
  parentLocationId: string | null;
  parentLocationName: string | null;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  clientId: string | null;
  tenantId: string | null;
  status: string | null;
  isDeleted: boolean;
}

/** Fields the API stores; area, capacity, image and the settings switches are UI-only for now. */
interface LocationRequest {
  locationName: string;
  code: string;
  type: string;
  environment: string;
  description: string;
  latitude: number;
  longitude: number;
  site: string;
  building: string;
  floor: string;
  zone: string;
  parentLocationId: string | null;
  status: string;
}

/**
 * Locations from the Master Data API. Rows are cached after load() so the page can keep reading
 * them synchronously; writes go to the API and update the cache when they succeed. The site /
 * building / floor / zone pickers are still DEMO MODE data until those masters are connected.
 */
@Injectable({ providedIn: 'root' })
export class LocationService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly apiUrl = `${environment.projectApiUrl}api/master-data/locations`;

  private records: LocationRecord[] = [];
  loading = false;
  loadError = '';

  /** GET /api/master-data/locations (skipped while a load is already running) */
  load(): void {
    if (this.loading) return;
    this.loading = true;
    this.loadError = '';
    this.http.get<LocationDto[]>(this.apiUrl, { params: { clientId: environment.clientId } }).subscribe({
      next: (list) => {
        const codeById = new Map(list.map((d) => [d.id, d.code]));
        this.records = list.map((d) => this.toRecord(d, codeById));
        this.loading = false;
      },
      error: (err) => {
        this.loadError = apiErrorMessage(err);
        this.loading = false;
      }
    });
  }

  all(): LocationRecord[] {
    return this.records;
  }

  find(code: string | null): LocationRecord | undefined {
    return code ? this.records.find((r) => r.code === code) : undefined;
  }

  children(code: string): LocationRecord[] {
    return this.records.filter((r) => r.parentCode === code);
  }

  inZone(buildingCode: string, floorCode: string, zoneCode: string): LocationRecord[] {
    return this.records.filter(
      (r) => r.buildingCode === buildingCode && r.floorCode === floorCode && r.zoneCode === zoneCode && r.parentCode === null
    );
  }

  filter(f: LocationFilters): LocationRecord[] {
    const term = f.search.trim().toLowerCase();
    return this.records.filter(
      (r) =>
        (!f.siteId || r.siteId === f.siteId) &&
        (!f.buildingCode || r.buildingCode === f.buildingCode) &&
        (!f.floorCode || r.floorCode === f.floorCode) &&
        (!f.zoneCode || r.zoneCode === f.zoneCode) &&
        (!term ||
          [r.name, r.code, r.type, r.buildingCode, this.buildingName(r.buildingCode), r.floorCode, r.zoneCode, this.zoneName(r)]
            .some((v) => v.toLowerCase().includes(term)))
    );
  }

  stats(): LocationStats {
    const active = this.records.filter((r) => r.status === 'Active').length;
    const outdoor = this.records.filter((r) => r.environment === 'Outdoor').length;
    return {
      total: this.records.length,
      active,
      inactive: this.records.length - active,
      indoor: this.records.length - outdoor,
      outdoor
    };
  }

  isCodeTaken(code: string, exceptCode: string | null): boolean {
    const wanted = code.trim().toLowerCase();
    return this.records.some((r) => r.code.toLowerCase() === wanted && r.code !== exceptCode);
  }

  /** POST for a new record, PUT /{id} for an existing one. */
  save(record: LocationRecord, originalCode: string | null): Observable<LocationRecord> {
    const user = this.auth.getUserEmail() || 'unknown';
    const body = this.toRequest(record);
    const request$ = record.id
      ? this.http.put<LocationDto>(`${this.apiUrl}/${record.id}`, { ...body, updatedBy: user })
      : this.http.post<LocationDto>(this.apiUrl, {
          ...body,
          createdBy: user,
          clientId: environment.clientId,
          tenantId: environment.tenantId
        });

    return request$.pipe(
      // Keep the fields the API doesn't store yet
      map((dto) => ({ ...record, ...this.toRecord(dto) })),
      tap((saved) => {
        const rows = [...this.records];
        const index = rows.findIndex((r) => r.id === saved.id);
        if (index >= 0) {
          rows[index] = saved;
        } else {
          rows.unshift(saved);
        }
        // Children point at their parent by code, so follow a code change
        if (originalCode && originalCode !== saved.code) {
          rows.forEach((r, i) => {
            if (r.parentCode === originalCode) rows[i] = { ...r, parentCode: saved.code };
          });
        }
        this.records = rows;
      })
    );
  }

  /** DELETE /api/master-data/locations/{id}; the API refuses a location that still has children. */
  remove(record: LocationRecord): Observable<void> {
    return this.http
      .delete<void>(`${this.apiUrl}/${record.id}`)
      .pipe(tap(() => (this.records = this.records.filter((r) => r.id !== record.id))));
  }

  private toRecord(dto: LocationDto, codeById?: Map<string, string>): LocationRecord {
    const parentId = dto.parentLocationId;
    // DEMO MODE: the fields the API doesn't store yet keep what this session has
    const cached = this.records.find((r) => r.id === dto.id) ?? blankLocation();
    return {
      ...cached,
      id: dto.id,
      code: dto.code,
      name: dto.locationName,
      type: dto.type as LocationType,
      environment: dto.environment === 'Outdoor' ? 'Outdoor' : 'Indoor',
      siteId: dto.site ?? '',
      buildingCode: dto.building ?? '',
      floorCode: dto.floor ?? '',
      zoneCode: dto.zone ?? '',
      parentCode: parentId ? (codeById?.get(parentId) ?? this.records.find((r) => r.id === parentId)?.code ?? null) : null,
      description: dto.description ?? '',
      status: dto.status === 'Inactive' ? 'Inactive' : 'Active',
      lat: dto.latitude ?? 0,
      lng: dto.longitude ?? 0,
      createdOn: dto.createdAt?.slice(0, 16) ?? '',
      updatedOn: (dto.updatedAt ?? dto.createdAt)?.slice(0, 16) ?? ''
    };
  }

  private toRequest(r: LocationRecord): LocationRequest {
    return {
      locationName: r.name,
      code: r.code,
      type: r.type,
      environment: r.environment,
      description: r.description,
      latitude: r.lat,
      longitude: r.lng,
      site: r.siteId,
      building: r.buildingCode,
      floor: r.floorCode,
      zone: r.zoneCode,
      parentLocationId: this.find(r.parentCode)?.id ?? null,
      status: r.status
    };
  }

  // ---- hierarchy lookups ----

  sites(): Site[] {
    return SITES;
  }

  buildings(siteId = ''): Building[] {
    return BUILDINGS.filter((b) => !siteId || b.siteId === siteId);
  }

  floors(buildingCode = ''): Floor[] {
    const building = findBuilding(buildingCode);
    return building ? FLOORS.filter((f) => building.floorCodes.includes(f.code)) : FLOORS;
  }

  zones(buildingCode = '', floorCode = ''): Zone[] {
    const seen = new Set<string>();
    return ZONES.filter((z) => {
      if ((buildingCode && z.buildingCode !== buildingCode) || (floorCode && z.floorCode !== floorCode)) return false;
      // The same zone code can exist in several buildings; list it once when unfiltered
      if (seen.has(z.code)) return false;
      seen.add(z.code);
      return true;
    });
  }

  siteName(siteId: string): string {
    return SITES.find((s) => s.id === siteId)?.name ?? siteId;
  }

  buildingName(code: string): string {
    return findBuilding(code)?.name ?? code;
  }

  floorName(code: string): string {
    return FLOORS.find((f) => f.code === code)?.name ?? code;
  }

  zoneName(r: Pick<LocationRecord, 'buildingCode' | 'floorCode' | 'zoneCode'>): string {
    return (
      ZONES.find((z) => z.code === r.zoneCode && z.buildingCode === r.buildingCode && z.floorCode === r.floorCode)?.name ??
      ZONES.find((z) => z.code === r.zoneCode)?.name ??
      r.zoneCode
    );
  }
}
