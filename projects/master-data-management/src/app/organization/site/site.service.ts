import { Injectable, inject } from '@angular/core';
import { BfzService } from '../building-floor-zone/bfz.service';
import { OrganizationService } from '../organization/organization.service';
import { initialSites } from './site.data';
import { SiteFilters, SiteRecord } from './site.model';

/** DEMO MODE: in-memory sites; replace these methods with API calls when the backend is ready. */
@Injectable({ providedIn: 'root' })
export class SiteService {
  private readonly bfz = inject(BfzService);
  private readonly organizations = inject(OrganizationService);
  private records: SiteRecord[] = initialSites();

  all(): SiteRecord[] {
    return this.records;
  }

  find(id: string | null): SiteRecord | undefined {
    return id ? this.records.find((r) => r.id === id) : undefined;
  }

  /** `id` and every site under it, so none can become its own parent. */
  descendantIds(id: string): Set<string> {
    const found = new Set([id]);
    for (let added = true; added; ) {
      added = false;
      for (const r of this.records) {
        if (r.parentId && found.has(r.parentId) && !found.has(r.id)) {
          found.add(r.id);
          added = true;
        }
      }
    }
    return found;
  }

  /** Sites refer to business units by name. */
  ofBusinessUnit(name: string): SiteRecord[] {
    return this.records.filter((r) => r.businessUnit === name);
  }

  renameBusinessUnit(oldName: string, newName: string): void {
    if (oldName === newName) return;
    this.records = this.records.map((r) => (r.businessUnit === oldName ? { ...r, businessUnit: newName } : r));
  }

  /** Sites refer to departments by name. */
  renameDepartment(oldName: string, newName: string): void {
    if (oldName === newName) return;
    this.records = this.records.map((r) => (r.department === oldName ? { ...r, department: newName } : r));
  }

  childSites(id: string): SiteRecord[] {
    return this.records.filter((r) => r.parentId === id);
  }

  filter(f: SiteFilters): SiteRecord[] {
    const term = f.search.trim().toLowerCase();
    return this.records.filter(
      (r) =>
        (!f.status || r.status === f.status) &&
        (!f.siteType || r.siteType === f.siteType) &&
        (!f.businessUnit || r.businessUnit === f.businessUnit) &&
        (!term ||
          [r.name, r.code, r.address.line1, r.address.line2, r.address.city, r.address.country].some((v) => v.toLowerCase().includes(term)))
    );
  }

  stats() {
    const active = this.records.filter((r) => r.status === 'Active').length;
    return {
      total: this.records.length,
      active,
      inactive: this.records.length - active,
      buildings: this.bfz.buildings().length,
      plants: this.records.filter((r) => r.siteType === 'Plant').length,
      warehouses: this.records.filter((r) => r.siteType === 'Warehouse').length
    };
  }

  buildingsOf(id: string) {
    return this.bfz.buildings(id);
  }

  organizationName(code: string): string {
    return this.organizations.find(code)?.name ?? code;
  }

  isCodeTaken(code: string, exceptId: string | null): boolean {
    const wanted = code.trim().toLowerCase();
    return this.records.some((r) => r.code.toLowerCase() === wanted && r.id !== exceptId);
  }

  nextId(): string {
    const highest = this.records.reduce((max, r) => Math.max(max, Number(r.id.slice(1)) || 0), 0);
    return `S${String(highest + 1).padStart(2, '0')}`;
  }

  save(record: SiteRecord): void {
    const index = this.records.findIndex((r) => r.id === record.id);
    if (index >= 0) {
      this.records[index] = record;
      this.records = [...this.records];
    } else {
      this.records = [...this.records, record];
    }
  }

  remove(ids: string[]): void {
    this.records = this.records
      .filter((r) => !ids.includes(r.id))
      .map((r) => (r.parentId && ids.includes(r.parentId) ? { ...r, parentId: null } : r));
  }
}
