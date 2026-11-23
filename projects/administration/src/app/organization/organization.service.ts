import { Injectable, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { OrgOption, TabSource, loadOrganizationScope, loadOrganizations } from './organization-scope';
import { LocationType, OrgLocation, OrgTabId, SummaryRow, SummaryStat, SummaryTab } from './organization.model';
import { seedSummaryTabs } from './organization.data';

/** A readable message from a validation problem, a conflict or a network failure. */
export function apiErrorMessage(err: unknown, fallback: string): string {
  if (err instanceof HttpErrorResponse) {
    const body = err.error;
    if (body?.errors) return (Object.values(body.errors) as string[][]).flat().join(' ');
    if (body?.message) return body.message;
    if (err.status === 0) return 'The Administration service could not be reached.';
    return fallback;
  }
  return err instanceof Error ? err.message : fallback;
}

@Injectable({ providedIn: 'root' })
export class OrganizationService {
  private readonly http = inject(HttpClient);

  private readonly locations = signal<OrgLocation[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  // Master data summary tabs (Asset, People, Vendor, ...): layouts; rows are filled by loadFor()
  private readonly tabs: SummaryTab[] = seedSummaryTabs();

  /** Organizations from the Master Data API for the picker. */
  readonly organizations = signal<OrgOption[]>([]);
  readonly organizationsLoaded = signal(false);
  /** Where each tab's data comes from and whether that call worked; set once an organization is loaded. */
  readonly sources = signal<Partial<Record<OrgTabId, TabSource>>>({});

  /** GET /api/master-data/organizations; the error (if any) shows in `error`. */
  loadOrganizations(): Observable<OrgOption[]> {
    this.error.set(null);
    return loadOrganizations(this.http).pipe(
      tap({
        next: (list) => {
          this.organizations.set(list);
          this.organizationsLoaded.set(true);
        },
        error: (err) => {
          this.organizationsLoaded.set(true);
          this.error.set(apiErrorMessage(err, 'Organizations could not be loaded from the Master Data service.'));
        }
      })
    );
  }

  /**
   * Loads everything for one organization: its locations (sites, buildings, floors, zones) and the rows of every
   * summary tab. Each source reports connected / broken in `sources`, so one failing API does not blank the page.
   */
  loadFor(org: OrgOption): void {
    this.loading.set(true);
    this.error.set(null);
    this.locations.set([]);
    for (const tab of this.tabs) tab.rows = [];
    loadOrganizationScope(this.http, org).subscribe({
      next: (scope) => {
        this.locations.set(scope.locations);
        for (const tab of this.tabs) tab.rows = scope.rows[tab.id as Exclude<OrgTabId, 'location'>] ?? [];
        this.sources.set(scope.sources);
        const broken = (Object.keys(scope.sources) as OrgTabId[]).filter((t) => scope.sources[t].state === 'broken');
        if (broken.length) this.error.set(`Could not load: ${broken.join(', ')}.`);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(apiErrorMessage(err, 'The organization data could not be loaded.'));
        this.loading.set(false);
      }
    });
  }

  sourceOf(tab: OrgTabId): TabSource | undefined {
    return this.sources()[tab];
  }

  all(): OrgLocation[] {
    return this.locations();
  }

  find(code: string | null): OrgLocation | undefined {
    return code ? this.locations().find((l) => l.code === code) : undefined;
  }

  ofType(type: LocationType): OrgLocation[] {
    return this.locations().filter((l) => l.type === type);
  }

  childrenOf(code: string | null): OrgLocation[] {
    return this.locations().filter((l) => l.parent === code);
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
