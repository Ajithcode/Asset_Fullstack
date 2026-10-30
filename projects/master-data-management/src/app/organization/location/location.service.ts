import { Injectable, inject } from '@angular/core';
import { Observable, switchMap } from 'rxjs';
import { MdmAudit } from '../../shared/mdm-api';
import { BfzService } from '../building-floor-zone/bfz.service';
import { BfzRecord } from '../building-floor-zone/bfz.model';
import { SiteService } from '../site/site.service';
import { StructureReference, reference, toLocalStamp } from '../structure-api';
import { StructureStore } from '../structure-store';
import { buildingBounds, center, locationBounds, zoneCells } from './location.geometry';
import { Building, Floor, LocationEnvironment, LocationFilters, LocationRecord, LocationStats, LocationStatus, LocationType, Site, Zone } from './location.model';

interface LocationDto extends MdmAudit {
  locationName: string;
  code: string;
  type: string;
  // Codes of the building, floor and zone records
  building: string | null;
  floor: string | null;
  zone: string | null;
  parentLocationId: string | null;
  environment: string | null;
  site: StructureReference | null;
  area: number | null;
  capacity: number | null;
  description: string | null;
  imageUrl: string | null;
  latitude: number | null;
  longitude: number | null;
  restrictedAccess: boolean;
  temperatureControlled: boolean;
  allowAssetAssignment: boolean;
  enableTracking: boolean;
  geofenceAlerts: boolean;
}

/**
 * Locations from `/api/master-data/locations`, keyed by code. The site / building / floor / zone
 * lookups come from the Site and Building / Floor / Zone masters, which load first.
 */
@Injectable({ providedIn: 'root' })
export class LocationService extends StructureStore<LocationRecord, LocationDto> {
  protected readonly resource = 'locations';
  private readonly siteService = inject(SiteService);
  private readonly bfz = inject(BfzService);

  constructor() {
    super();
  }

  override load(query: Record<string, string | number | boolean | null | undefined> = {}): Observable<LocationRecord[]> {
    return this.bfz.ensureLoaded().pipe(switchMap(() => super.load(query)));
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

  // ---- hierarchy lookups (Site and Building / Floor / Zone masters) ----

  sites(): Site[] {
    return this.siteService.all().map((s) => {
      const first = this.bfz.buildings(s.id).find((b) => b.lat !== null && b.lng !== null);
      return { id: s.id, name: s.name, lat: s.lat ?? first?.lat ?? 0, lng: s.lng ?? first?.lng ?? 0 };
    });
  }

  buildings(siteId = ''): Building[] {
    return this.bfz.buildings(siteId).map((b) => this.toBuilding(b));
  }

  findBuilding(code: string): Building | undefined {
    const b = this.bfz.find(code);
    return b && b.type === 'Building' ? this.toBuilding(b) : undefined;
  }

  floors(buildingCode = ''): Floor[] {
    const floors = buildingCode ? this.bfz.floorsOf(buildingCode) : this.bfz.all().filter((r) => r.type === 'Floor');
    return floors.map((f) => ({ code: f.code, name: f.name }));
  }

  zones(buildingCode = '', floorCode = ''): Zone[] {
    return this.bfz
      .all()
      .filter((z) => z.type === 'Zone' && (!buildingCode || z.buildingCode === buildingCode) && (!floorCode || z.floorCode === floorCode))
      .map((z) => this.toZone(z));
  }

  zonesOnFloor(buildingCode: string, floorCode: string): Zone[] {
    return this.zones(buildingCode, floorCode).filter((z) => z.buildingCode === buildingCode && z.floorCode === floorCode);
  }

  findZone(buildingCode: string, floorCode: string, zoneCode: string): Zone | undefined {
    return this.zonesOnFloor(buildingCode, floorCode).find((z) => z.code === zoneCode);
  }

  /** Map position for a location: inside its zone's cell, or the building centre when it has no zone. */
  positionFor(code: string, buildingCode: string, floorCode: string, zoneCode: string): { lat: number; lng: number } {
    const building = this.findBuilding(buildingCode);
    if (!building) {
      const site = this.sites()[0];
      return { lat: site?.lat ?? 0, lng: site?.lng ?? 0 };
    }
    const cell = zoneCells(building, this.zonesOnFloor(buildingCode, floorCode)).get(zoneCode);
    return center(cell ? locationBounds(cell, code) : buildingBounds(building));
  }

  siteName(siteId: string): string {
    return this.siteService.find(siteId)?.name ?? siteId;
  }

  buildingName(code: string): string {
    return this.bfz.find(code)?.name ?? code;
  }

  floorName(code: string): string {
    return this.bfz.find(code)?.name ?? code;
  }

  zoneName(r: Pick<LocationRecord, 'buildingCode' | 'floorCode' | 'zoneCode'>): string {
    return this.bfz.find(r.zoneCode)?.name ?? r.zoneCode;
  }

  protected toRecord(dto: LocationDto): LocationRecord {
    const buildingCode = dto.building ?? '';
    const floorCode = dto.floor ?? '';
    const zoneCode = dto.zone ?? '';
    const siteId = dto.site?.id ?? this.bfz.find(buildingCode)?.siteId ?? '';
    const position =
      dto.latitude !== null && dto.longitude !== null
        ? { lat: dto.latitude, lng: dto.longitude }
        : this.positionFor(dto.code, buildingCode, floorCode, zoneCode);
    return {
      code: dto.code,
      name: dto.locationName,
      type: dto.type as LocationType,
      environment: (dto.environment as LocationEnvironment) ?? 'Indoor',
      siteId,
      buildingCode,
      floorCode,
      zoneCode,
      // Parent id for now; StructureStore swaps it for the code
      parentCode: dto.parentLocationId ?? null,
      area: dto.area ?? null,
      capacity: dto.capacity ?? null,
      description: dto.description ?? '',
      status: (dto.status as LocationStatus) ?? 'Active',
      image: dto.imageUrl,
      lat: position.lat,
      lng: position.lng,
      restrictedAccess: dto.restrictedAccess,
      temperatureControlled: dto.temperatureControlled,
      allowAssetAssignment: dto.allowAssetAssignment,
      enableTracking: dto.enableTracking,
      geofenceAlerts: dto.geofenceAlerts,
      createdOn: toLocalStamp(dto.createdAt),
      updatedOn: toLocalStamp(dto.updatedAt ?? dto.createdAt)
    };
  }

  protected toRequest(r: LocationRecord): object {
    return {
      locationName: r.name,
      code: r.code,
      type: r.type,
      environment: r.environment,
      site: reference(r.siteId, this.siteName(r.siteId)),
      building: r.buildingCode || null,
      floor: r.floorCode || null,
      zone: r.zoneCode || null,
      parentLocationId: r.parentCode ? (this.idOf(r.parentCode) ?? null) : null,
      area: r.area,
      capacity: r.capacity,
      description: r.description,
      imageUrl: r.image,
      latitude: r.lat,
      longitude: r.lng,
      restrictedAccess: r.restrictedAccess,
      temperatureControlled: r.temperatureControlled,
      allowAssetAssignment: r.allowAssetAssignment,
      enableTracking: r.enableTracking,
      geofenceAlerts: r.geofenceAlerts,
      status: r.status
    };
  }

  private toBuilding(b: BfzRecord): Building {
    const site = this.siteService.find(b.siteId);
    return {
      code: b.code,
      name: b.name,
      siteId: b.siteId,
      floorCodes: this.bfz.floorsOf(b.code).map((f) => f.code),
      lat: b.lat ?? site?.lat ?? 0,
      lng: b.lng ?? site?.lng ?? 0
    };
  }

  private toZone(z: BfzRecord): Zone {
    // The masters don't record indoor / outdoor for zones; locations carry their own environment
    return { code: z.code, name: z.name, buildingCode: z.buildingCode ?? '', floorCode: z.floorCode ?? '', outdoor: false };
  }
}
