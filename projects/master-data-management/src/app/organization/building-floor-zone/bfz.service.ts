import { Injectable } from '@angular/core';
import { BFZ_SITES, initialBfz } from './bfz.data';
import { BfzFilters, BfzRecord, BfzSite } from './bfz.model';

/** DEMO MODE: in-memory buildings, floors and zones; replace with API calls when the backend is ready. */
@Injectable({ providedIn: 'root' })
export class BfzService {
  private records: BfzRecord[] = initialBfz();

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

  save(record: BfzRecord, originalCode: string | null): void {
    const index = originalCode ? this.records.findIndex((r) => r.code === originalCode) : -1;
    if (index >= 0) {
      const before = this.records[index];
      this.records[index] = record;
      // Keep children pointing at the renamed code, and on the building's site
      this.records.forEach((r) => {
        if (before.type === 'Building' && r.buildingCode === originalCode) {
          r.buildingCode = record.code;
          r.siteId = record.siteId;
        }
        if (before.type === 'Floor' && r.floorCode === originalCode) r.floorCode = record.code;
      });
    } else {
      // New floors and zones go right after their siblings so the tree reads in order
      const anchor = record.type === 'Building' ? -1 : this.lastIndexUnder(record);
      if (anchor >= 0) {
        this.records.splice(anchor + 1, 0, record);
      } else {
        this.records.push(record);
      }
    }
    this.records = [...this.records];
  }

  remove(codes: string[]): void {
    this.records = this.records.filter((r) => !codes.includes(r.code));
  }

  sites(): BfzSite[] {
    return BFZ_SITES;
  }

  site(siteId: string): BfzSite | undefined {
    return BFZ_SITES.find((s) => s.id === siteId);
  }

  siteName(siteId: string): string {
    return this.site(siteId)?.name ?? siteId;
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
