import { Injectable } from '@angular/core';
import { LocationType, OrgLocation, SummaryRow, SummaryStat, SummaryTab } from './organization.model';
import { seedLocations, seedSummaryTabs } from './organization.data';

// DEMO MODE: the location hierarchy and the master data summaries live in memory until the APIs exist
@Injectable({ providedIn: 'root' })
export class OrganizationService {
  private locations: OrgLocation[] = seedLocations();
  private readonly tabs: SummaryTab[] = seedSummaryTabs();

  all(): OrgLocation[] {
    return this.locations;
  }

  find(code: string | null): OrgLocation | undefined {
    return code ? this.locations.find((l) => l.code === code) : undefined;
  }

  ofType(type: LocationType): OrgLocation[] {
    return this.locations.filter((l) => l.type === type);
  }

  childrenOf(code: string | null): OrgLocation[] {
    return this.locations.filter((l) => l.parent === code);
  }

  /** Every code below the given node. */
  descendantsOf(code: string): string[] {
    const out: string[] = [];
    const walk = (c: string) => {
      for (const child of this.childrenOf(c)) {
        out.push(child.code);
        walk(child.code);
      }
    };
    walk(code);
    return out;
  }

  /** Ancestors from the top-level location down to the direct parent. */
  ancestorsOf(code: string): OrgLocation[] {
    const out: OrgLocation[] = [];
    let current = this.find(this.find(code)?.parent ?? null);
    while (current) {
      out.unshift(current);
      current = this.find(current.parent);
    }
    return out;
  }

  codeTaken(code: string, except: string | null): boolean {
    const c = code.trim().toUpperCase();
    return this.locations.some((l) => l.code.toUpperCase() === c && l.code !== except);
  }

  /** Two siblings can't share a name. */
  nameTaken(name: string, parent: string | null, except: string | null): boolean {
    const n = name.trim().toLowerCase();
    return this.locations.some((l) => l.parent === parent && l.name.toLowerCase() === n && l.code !== except);
  }

  nextCode(): string {
    const max = this.locations.reduce((m, l) => Math.max(m, Number(l.code.replace(/\D/g, '')) || 0), 0);
    return `LOC-${String(max + 1).padStart(3, '0')}`;
  }

  /** Adds or replaces a record; a changed code is carried over to its children. */
  save(record: OrgLocation, originalCode: string | null): void {
    if (originalCode === null) {
      this.locations = [...this.locations, record];
      return;
    }
    this.locations = this.locations.map((l) => {
      if (l.code === originalCode) return record;
      if (l.parent === originalCode) return { ...l, parent: record.code };
      return l;
    });
  }

  remove(codes: string[]): void {
    const set = new Set(codes);
    this.locations = this.locations.filter((l) => !set.has(l.code));
  }

  /* ===== Summary tabs ===== */

  tab(id: string): SummaryTab | undefined {
    return this.tabs.find((t) => t.id === id);
  }

  statValue(tab: SummaryTab, stat: SummaryStat): number {
    const rows: SummaryRow[] = tab.rows;
    const col = stat.column ?? '';
    switch (stat.kind) {
      case 'total':
        return rows.length;
      case 'match':
        return rows.filter((r) => (stat.values ?? []).includes(r[col])).length;
      case 'distinct':
        return new Set(rows.map((r) => r[col])).size;
      case 'sum':
        return rows.reduce((s, r) => s + (Number(r[col]) || 0), 0);
    }
  }
}
