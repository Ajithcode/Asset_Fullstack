import { HttpClient } from '@angular/common/http';
import { Injectable, Injector, inject } from '@angular/core';
import { Observable, forkJoin, map, tap } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { apiErrorMessage } from '../../shared/api-error';
import { SiteService } from '../site/site.service';
import { blankBfz } from './bfz.data';
import { BfzFilters, BfzRecord, BfzSite, BfzType } from './bfz.model';

interface ReferenceDto {
  id: string | null;
  name: string;
}

/** Fields shared by the buildings, floors and zones APIs ({projectApiUrl}api/master-data/...). */
interface SpaceDto {
  id: string;
  name: string;
  code: string;
  site: ReferenceDto | null;
  parentLevel: string | null;
  description: string | null;
  capacity: number | null;
  area: number | null;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  status: string | null;
}

interface BuildingDto extends SpaceDto {
  category: string | null;
  latitude: number | null;
  longitude: number | null;
}

interface FloorDto extends SpaceDto {
  buildingId: string;
  buildingName: string;
  levelNumber: number | null;
}

interface ZoneDto extends SpaceDto {
  buildingId: string;
  buildingName: string;
  floorId: string | null;
  floorName: string | null;
  category: string | null;
  color: string | null;
}

const ENDPOINT: Record<BfzType, string> = { Building: 'buildings', Floor: 'floors', Zone: 'zones' };

/**
 * Buildings, floors and zones from the Master Data API, kept as one list for the tree. Rows are
 * cached after load() so the page can keep reading them synchronously; writes go to the API and
 * update the cache when they succeed. Address, images, settings and counts are UI-only for now.
 */
@Injectable({ providedIn: 'root' })
export class BfzService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  // SiteService depends on this service for building counts, so look it up lazily
  private readonly injector = inject(Injector);
  private readonly apiUrl = `${environment.projectApiUrl}api/master-data/`;

  private records: BfzRecord[] = [];
  loading = false;
  loadError = '';

  constructor() {
    this.load();
  }

  /** GET buildings, floors and zones together (skipped while a load is already running) */
  load(): void {
    if (this.loading) return;
    this.loading = true;
    this.loadError = '';
    const params = { clientId: environment.clientId };
    forkJoin({
      buildings: this.http.get<BuildingDto[]>(this.apiUrl + ENDPOINT.Building, { params }),
      floors: this.http.get<FloorDto[]>(this.apiUrl + ENDPOINT.Floor, { params }),
      zones: this.http.get<ZoneDto[]>(this.apiUrl + ENDPOINT.Zone, { params })
    }).subscribe({
      next: ({ buildings, floors, zones }) => {
        const codeById = new Map([...buildings, ...floors].map((d) => [d.id, d.code]));
        // Keep the tree order: each building, then its floors (by level), each followed by its zones
        const rows: BfzRecord[] = [];
        for (const b of buildings) {
          rows.push(this.fromBuilding(b));
          const ownFloors = floors.filter((f) => f.buildingId === b.id).sort((x, y) => (x.levelNumber ?? 0) - (y.levelNumber ?? 0));
          for (const f of ownFloors) {
            rows.push(this.fromFloor(f, codeById));
            zones.filter((z) => z.floorId === f.id).forEach((z) => rows.push(this.fromZone(z, codeById)));
          }
          // Zones that cover the whole building rather than one floor
          zones.filter((z) => z.buildingId === b.id && !z.floorId).forEach((z) => rows.push(this.fromZone(z, codeById)));
        }
        this.records = rows;
        this.loading = false;
      },
      error: (err) => {
        this.loadError = apiErrorMessage(err);
        this.loading = false;
      }
    });
  }

  all(): BfzRecord[] {
    return this.records;
  }

  find(code: string | null): BfzRecord | undefined {
    return code ? this.records.find((r) => r.code === code) : undefined;
  }

  buildings(siteId = ''): BfzRecord[] {
    return this.records.filter((r) => r.type === 'Building' && (!siteId || r.siteId === siteId));
  }

  floorsOf(buildingCode: string | null): BfzRecord[] {
    return this.records
      .filter((r) => r.type === 'Floor' && r.buildingCode === buildingCode)
      .sort((a, b) => (a.levelNumber ?? 0) - (b.levelNumber ?? 0));
  }

  zonesOfFloor(floorCode: string | null): BfzRecord[] {
    return this.records.filter((r) => r.type === 'Zone' && r.floorCode === floorCode);
  }

  zonesOfBuilding(buildingCode: string): BfzRecord[] {
    return this.records.filter((r) => r.type === 'Zone' && r.buildingCode === buildingCode);
  }

  /** Floors of a building, or zones of a floor. Zones have no children. */
  children(r: BfzRecord): BfzRecord[] {
    return r.type === 'Building' ? this.floorsOf(r.code) : r.type === 'Floor' ? this.zonesOfFloor(r.code) : [];
  }

  /** The building a record belongs to (itself for a building). */
  buildingOf(r: BfzRecord): BfzRecord | undefined {
    return r.type === 'Building' ? r : this.find(r.buildingCode);
  }

  parent(r: BfzRecord): BfzRecord | undefined {
    return r.type === 'Zone' ? this.find(r.floorCode) : r.type === 'Floor' ? this.find(r.buildingCode) : undefined;
  }

  /** 'Dubai Factory (B01)', 'Ground Floor (GF)'; zones keep their plain name. */
  label(r: BfzRecord): string {
    if (r.type === 'Zone') return r.name;
    return `${r.name} (${r.code.split('-').pop()})`;
  }

  filter(f: BfzFilters): BfzRecord[] {
    const term = f.search.trim().toLowerCase();
    return this.records.filter(
      (r) =>
        (!f.siteId || r.siteId === f.siteId) &&
        (!f.type || r.type === f.type) &&
        (!f.status || r.status === f.status) &&
        (!term || [r.name, r.code, r.type, this.siteName(r.siteId), r.description].some((v) => v.toLowerCase().includes(term)))
    );
  }

  stats() {
    return {
      buildings: this.records.filter((r) => r.type === 'Building').length,
      floors: this.records.filter((r) => r.type === 'Floor').length,
      zones: this.records.filter((r) => r.type === 'Zone').length
    };
  }

  isCodeTaken(code: string, exceptCode: string | null): boolean {
    const wanted = code.trim().toLowerCase();
    return this.records.some((r) => r.code.toLowerCase() === wanted && r.code !== exceptCode);
  }

  /** POST for a new record, PUT /{id} for an existing one, to the endpoint for its type. */
  save(record: BfzRecord, originalCode: string | null): Observable<BfzRecord> {
    const user = this.auth.getUserEmail() || 'unknown';
    const url = this.apiUrl + ENDPOINT[record.type];
    const body = this.toRequest(record);
    const request$ = record.id
      ? this.http.put<BuildingDto & FloorDto & ZoneDto>(`${url}/${record.id}`, { ...body, updatedBy: user })
      : this.http.post<BuildingDto & FloorDto & ZoneDto>(url, {
          ...body,
          createdBy: user,
          clientId: environment.clientId,
          tenantId: environment.tenantId
        });

    return request$.pipe(
      map((dto) => {
        const codeById = new Map(this.records.map((r) => [r.id, r.code]));
        const saved = record.type === 'Building' ? this.fromBuilding(dto) : record.type === 'Floor' ? this.fromFloor(dto, codeById) : this.fromZone(dto, codeById);
        // Keep the fields the API doesn't store yet
        return { ...record, ...saved, address: record.address, images: record.images, image: record.image };
      }),
      tap((saved) => this.store(saved, originalCode))
    );
  }

  /** DELETE {type}/{id}; the API refuses a building with floors or zones, and a floor with zones. */
  remove(record: BfzRecord): Observable<void> {
    return this.http
      .delete<void>(`${this.apiUrl}${ENDPOINT[record.type]}/${record.id}`)
      .pipe(tap(() => (this.records = this.records.filter((r) => r.id !== record.id))));
  }

  /** Sites from the Site master, with the extra details the building pages show. */
  sites(): BfzSite[] {
    return this.injector.get(SiteService).all().map((s) => ({
      id: s.id,
      name: s.name,
      // Building codes start with the site's prefix, e.g. DXB from DXB-01
      prefix: s.code.split('-')[0],
      city: s.address.city,
      state: s.address.state,
      country: s.address.country,
      lat: s.lat ?? 0,
      lng: s.lng ?? 0
    }));
  }

  site(siteId: string): BfzSite | undefined {
    return this.sites().find((s) => s.id === siteId);
  }

  siteName(siteId: string): string {
    return this.site(siteId)?.name ?? siteId;
  }

  /** Puts a saved row into the cache, keeping the tree order and children's links. */
  private store(record: BfzRecord, originalCode: string | null): void {
    const rows = [...this.records];
    const index = rows.findIndex((r) => r.id === record.id);
    if (index >= 0) {
      const before = rows[index];
      rows[index] = record;
      // Keep children pointing at the renamed code, and on the building's site (the API moves them too)
      rows.forEach((r, i) => {
        if (before.type === 'Building' && r.buildingCode === originalCode) rows[i] = { ...r, buildingCode: record.code, siteId: record.siteId };
        if (before.type === 'Floor' && r.floorCode === originalCode) rows[i] = { ...rows[i], floorCode: record.code };
      });
    } else {
      // New floors and zones go right after their siblings so the tree reads in order
      const anchor = record.type === 'Building' ? -1 : this.lastIndexUnder(record, rows);
      if (anchor >= 0) {
        rows.splice(anchor + 1, 0, record);
      } else {
        rows.push(record);
      }
    }
    this.records = rows;
  }

  private fromSpace(dto: SpaceDto, type: BfzType): BfzRecord {
    // DEMO MODE: the fields the API doesn't store yet keep what this session has
    const cached = this.records.find((r) => r.id === dto.id) ?? blankBfz();
    return {
      ...cached,
      id: dto.id,
      code: dto.code,
      name: dto.name,
      type,
      siteId: dto.site?.id ?? '',
      description: dto.description ?? '',
      capacity: dto.capacity,
      area: dto.area,
      status: dto.status === 'Inactive' ? 'Inactive' : 'Active',
      createdOn: dto.createdAt?.slice(0, 16) ?? '',
      updatedOn: (dto.updatedAt ?? dto.createdAt)?.slice(0, 16) ?? ''
    };
  }

  private fromBuilding(dto: BuildingDto): BfzRecord {
    return { ...this.fromSpace(dto, 'Building'), category: dto.category ?? '', lat: dto.latitude, lng: dto.longitude };
  }

  private fromFloor(dto: FloorDto, codeById: Map<string, string>): BfzRecord {
    return { ...this.fromSpace(dto, 'Floor'), buildingCode: codeById.get(dto.buildingId) ?? null, levelNumber: dto.levelNumber };
  }

  private fromZone(dto: ZoneDto, codeById: Map<string, string>): BfzRecord {
    const record = this.fromSpace(dto, 'Zone');
    return {
      ...record,
      buildingCode: codeById.get(dto.buildingId) ?? null,
      floorCode: dto.floorId ? (codeById.get(dto.floorId) ?? null) : null,
      category: dto.category ?? '',
      color: dto.color ?? record.color
    };
  }

  private toRequest(r: BfzRecord) {
    const site = this.site(r.siteId);
    const common = {
      name: r.name,
      code: r.code,
      site: site ? { id: site.id, name: site.name } : null,
      description: r.description,
      capacity: r.capacity,
      area: r.area,
      status: r.status
    };
    const buildingId = this.find(r.buildingCode)?.id ?? null;
    switch (r.type) {
      case 'Building':
        return { ...common, category: r.category, latitude: r.lat, longitude: r.lng };
      case 'Floor':
        return { ...common, buildingId, levelNumber: r.levelNumber };
      case 'Zone':
        return { ...common, buildingId, floorId: this.find(r.floorCode)?.id ?? null, category: r.category, color: r.color };
    }
  }

  private lastIndexUnder(record: BfzRecord, rows = this.records): number {
    let last = -1;
    rows.forEach((r, i) => {
      const inside = record.type === 'Floor' ? r.code === record.buildingCode || r.buildingCode === record.buildingCode : r.code === record.floorCode || r.floorCode === record.floorCode;
      if (inside) last = i;
    });
    return last;
  }
}
