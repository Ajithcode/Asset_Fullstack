import { Injectable, inject, signal } from '@angular/core';
import { Observable, finalize, forkJoin, map, of, shareReplay, switchMap, tap } from 'rxjs';
import { MdmApi, MdmAudit, apiErrorMessage, inSequence } from '../../shared/mdm-api';
import { SiteService } from '../site/site.service';
import { StructureAddressDto, StructureReference, fromApiAddress, reference, toLocalStamp } from '../structure-api';
import { blankBfz } from './bfz.data';
import { BfzFilters, BfzRecord, BfzSite, BfzStatus, BfzType } from './bfz.model';

const RESOURCES: Record<BfzType, string> = { Building: 'buildings', Floor: 'floors', Zone: 'zones' };

interface SpaceDto extends MdmAudit {
  name: string;
  code: string;
  site: StructureReference | null;
  description: string | null;
  capacity: number | null;
  area: number | null;
  imageUrl: string | null;
  images: string[] | null;
  allowAssetAssignment: boolean;
  enableTracking: boolean;
  restrictedAccess: boolean;
  showOnMap: boolean;
  latitude: number | null;
  longitude: number | null;
  // Building
  category?: string | null;
  address?: StructureAddressDto | null;
  // Floor and zone
  buildingId?: string;
  // Floor
  levelNumber?: number | null;
  // Zone
  floorId?: string | null;
  color?: string | null;
}

/**
 * Buildings, floors and zones from `/api/master-data/buildings|floors|zones`, kept in one list keyed
 * by code (codes are unique across all three here). Floors and zones point at their building and
 * floor by code; the API ids are kept alongside.
 */
@Injectable({ providedIn: 'root' })
export class BfzService {
  private readonly api = inject(MdmApi);
  private readonly siteService = inject(SiteService);
  private records: BfzRecord[] = [];
  private readonly ids = new Map<string, string>();
  private firstLoad?: Observable<BfzRecord[]>;
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  load(): Observable<BfzRecord[]> {
    this.loading.set(true);
    this.error.set(null);
    return this.siteService.ensureLoaded().pipe(
      switchMap(() =>
        forkJoin([
          this.api.list<SpaceDto>(RESOURCES.Building),
          this.api.list<SpaceDto>(RESOURCES.Floor),
          this.api.list<SpaceDto>(RESOURCES.Zone)
        ])
      ),
      map(([buildings, floors, zones]) => {
        this.ids.clear();
        const codeById = new Map<string, string>();
        const adopt = (type: BfzType) => (dto: SpaceDto) => {
          const record = this.toRecord(dto, type, codeById);
          this.ids.set(record.code, dto.id);
          codeById.set(dto.id, record.code);
          return record;
        };
        // Buildings first so floors and zones can find their building's code, then floors for zones
        const b = buildings.map(adopt('Building'));
        const f = floors.map(adopt('Floor'));
        const z = zones.map(adopt('Zone'));
        this.records = this.ordered([...b, ...f, ...z]);
        return this.records;
      }),
      tap({ error: (err) => this.error.set(apiErrorMessage(err)) }),
      finalize(() => this.loading.set(false))
    );
  }

  /** Loads once and shares the result; used by pages that only need buildings for lookups. */
  ensureLoaded(): Observable<BfzRecord[]> {
    this.firstLoad ??= this.load().pipe(
      tap({ error: () => (this.firstLoad = undefined) }),
      shareReplay(1)
    );
    return this.firstLoad;
  }

  all(): BfzRecord[] {
    return this.records;
  }

  find(code: string | null): BfzRecord | undefined {
    return code ? this.records.find((r) => r.code === code) : undefined;
  }

  idOf(code: string | null | undefined): string | undefined {
    return code ? this.ids.get(code) : undefined;
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

  /** Creates (no `originalCode`) or updates the record, then stores what the server returned. */
  persist(record: BfzRecord, originalCode: string | null): Observable<BfzRecord> {
    const resource = RESOURCES[record.type];
    const id = originalCode ? this.ids.get(originalCode) : undefined;
    const body = this.toRequest(record);
    const call = id ? this.api.update<SpaceDto>(resource, id, body) : this.api.create<SpaceDto>(resource, body);
    return call.pipe(
      map((dto) => {
        const codeById = new Map([...this.ids].map(([code, apiId]) => [apiId, code]));
        const saved = { ...this.toRecord(dto, record.type, codeById), assets: record.assets, people: record.people, departments: record.departments };
        if (originalCode) this.ids.delete(originalCode);
        this.ids.set(saved.code, dto.id);
        this.store(saved, id ? originalCode : null);
        return saved;
      })
    );
  }

  setStatus(record: BfzRecord, status: BfzStatus): Observable<BfzRecord> {
    return this.api.setStatus<SpaceDto>(RESOURCES[record.type], this.ids.get(record.code) ?? '', status).pipe(
      map((dto) => {
        const saved = { ...record, status: (dto.status as BfzStatus) ?? status, updatedOn: toLocalStamp(dto.updatedAt) || record.updatedOn };
        this.store(saved, record.code);
        return saved;
      })
    );
  }

  /** Deletes zones, then floors, then buildings, one at a time; ones deleted before a failure leave the list too. */
  delete(codes: string[]): Observable<unknown> {
    const rank: Record<BfzType, number> = { Zone: 0, Floor: 1, Building: 2 };
    const list = codes
      .map((code) => this.find(code))
      .filter((r): r is BfzRecord => !!r)
      .sort((a, b) => rank[a.type] - rank[b.type]);
    return inSequence(
      list.map((r) => {
        const id = this.ids.get(r.code);
        const call = id ? this.api.remove(RESOURCES[r.type], id) : of(null);
        return call.pipe(
          tap(() => {
            this.ids.delete(r.code);
            this.records = this.records.filter((x) => x.code !== r.code);
          })
        );
      })
    );
  }

  sites(): BfzSite[] {
    return this.siteService.all().map((s) => ({
      id: s.id,
      name: s.name,
      prefix: s.code,
      city: s.address.city,
      state: s.address.state,
      country: s.address.country,
      lat: s.lat,
      lng: s.lng
    }));
  }

  site(siteId: string): BfzSite | undefined {
    return this.sites().find((s) => s.id === siteId);
  }

  siteName(siteId: string): string {
    return this.siteService.find(siteId)?.name ?? siteId;
  }

  private store(record: BfzRecord, originalCode: string | null): void {
    const index = originalCode ? this.records.findIndex((r) => r.code === originalCode) : -1;
    if (index >= 0) {
      const before = this.records[index];
      this.records[index] = record;
      // Keep children pointing at the renamed code, and on the building's site
      this.records = this.records.map((r) => {
        if (before.type === 'Building' && r.buildingCode === originalCode) return { ...r, buildingCode: record.code, siteId: record.siteId };
        if (before.type === 'Floor' && r.floorCode === originalCode) return { ...r, floorCode: record.code };
        return r;
      });
    } else {
      // New floors and zones go right after their siblings so the tree reads in order
      const anchor = record.type === 'Building' ? -1 : this.lastIndexUnder(record);
      const next = [...this.records];
      if (anchor >= 0) next.splice(anchor + 1, 0, record);
      else next.push(record);
      this.records = next;
    }
  }

  /** Each building followed by its floors, each floor by its zones; zones without a floor after the floors. */
  private ordered(records: BfzRecord[]): BfzRecord[] {
    const out: BfzRecord[] = [];
    const placed = new Set<BfzRecord>();
    const put = (r: BfzRecord) => {
      if (!placed.has(r)) {
        placed.add(r);
        out.push(r);
      }
    };
    for (const b of records.filter((r) => r.type === 'Building')) {
      put(b);
      for (const f of records.filter((r) => r.type === 'Floor' && r.buildingCode === b.code)) {
        put(f);
        records.filter((r) => r.type === 'Zone' && r.floorCode === f.code).forEach(put);
      }
      records.filter((r) => r.type === 'Zone' && r.buildingCode === b.code).forEach(put);
    }
    records.forEach(put);
    return out;
  }

  private toRecord(dto: SpaceDto, type: BfzType, codeById: Map<string, string>): BfzRecord {
    const blank = blankBfz();
    const known = this.find(dto.code);
    return {
      ...blank,
      code: dto.code,
      name: dto.name,
      type,
      siteId: dto.site?.id ?? '',
      buildingCode: type === 'Building' ? null : (codeById.get(dto.buildingId ?? '') ?? null),
      floorCode: type === 'Zone' ? (codeById.get(dto.floorId ?? '') ?? null) : null,
      description: dto.description ?? '',
      capacity: dto.capacity ?? null,
      area: dto.area ?? null,
      status: (dto.status as BfzStatus) ?? 'Active',
      image: dto.imageUrl,
      images: dto.images ?? [],
      category: dto.category ?? '',
      levelNumber: type === 'Floor' ? (dto.levelNumber ?? null) : null,
      color: dto.color ?? blank.color,
      address: type === 'Building' ? fromApiAddress(dto.address) : blank.address,
      lat: dto.latitude ?? null,
      lng: dto.longitude ?? null,
      allowAssetAssignment: dto.allowAssetAssignment,
      enableTracking: dto.enableTracking,
      restrictedAccess: dto.restrictedAccess,
      showOnMap: dto.showOnMap,
      // Counts owned by other modules
      assets: known?.assets ?? 0,
      people: known?.people ?? 0,
      departments: known?.departments ?? 0,
      createdOn: toLocalStamp(dto.createdAt),
      updatedOn: toLocalStamp(dto.updatedAt ?? dto.createdAt)
    };
  }

  private toRequest(r: BfzRecord): object {
    const common = {
      name: r.name,
      code: r.code,
      site: reference(r.siteId, this.siteName(r.siteId)),
      description: r.description,
      capacity: r.capacity,
      area: r.area,
      imageUrl: r.image,
      images: r.images,
      allowAssetAssignment: r.allowAssetAssignment,
      enableTracking: r.enableTracking,
      restrictedAccess: r.restrictedAccess,
      showOnMap: r.showOnMap,
      latitude: r.lat,
      longitude: r.lng,
      status: r.status
    };
    if (r.type === 'Building') return { ...common, category: r.category, address: r.address };
    if (r.type === 'Floor') return { ...common, buildingId: this.idOf(r.buildingCode) ?? null, levelNumber: r.levelNumber };
    return {
      ...common,
      buildingId: this.idOf(r.buildingCode) ?? null,
      floorId: this.idOf(r.floorCode) ?? null,
      category: r.category,
      color: r.color
    };
  }

  private lastIndexUnder(record: BfzRecord): number {
    let last = -1;
    this.records.forEach((r, i) => {
      const inside = record.type === 'Floor' ? r.code === record.buildingCode || r.buildingCode === record.buildingCode : r.code === record.floorCode || r.floorCode === record.floorCode;
      if (inside) last = i;
    });
    return last;
  }
}
