import { Injectable } from '@angular/core';
import { BUILDINGS, FLOORS, SITES, ZONES, findBuilding, initialLocations } from './location.data';
import { Building, Floor, LocationFilters, LocationRecord, LocationStats, Site, Zone } from './location.model';

/** DEMO MODE: in-memory locations; replace these methods with API calls when the backend is ready. */
@Injectable({ providedIn: 'root' })
export class LocationService {
  private records: LocationRecord[] = initialLocations();

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

  save(record: LocationRecord, originalCode: string | null): void {
    const index = originalCode ? this.records.findIndex((r) => r.code === originalCode) : -1;
    if (index >= 0) {
      this.records[index] = record;
      if (originalCode !== record.code) {
        this.records.forEach((r) => {
          if (r.parentCode === originalCode) r.parentCode = record.code;
        });
      }
    } else {
      this.records.unshift(record);
    }
    this.records = [...this.records];
  }

  remove(codes: string[]): void {
    this.records = this.records
      .filter((r) => !codes.includes(r.code))
      .map((r) => (r.parentCode && codes.includes(r.parentCode) ? { ...r, parentCode: null } : r));
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
