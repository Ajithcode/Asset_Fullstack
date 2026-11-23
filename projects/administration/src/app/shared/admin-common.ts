import { Directive, Injectable, OnDestroy, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, Subscription, catchError, forkJoin, map, of, shareReplay } from 'rxjs';
import { AccessService, environment } from 'shared-ui';

// Tenant lookups (organizations, people, locations, departments, asset categories) are filled in place by
// AdminLookupService from the APIs, so pages that hold these arrays see the values once they arrive; empty until loaded.
export const ORGANIZATIONS: string[] = [];

/** The signed-in administrator shown as owner / "updated by" on new records (filled from GET /api/users/me). */
export const CURRENT_USER = { id: '', name: '', role: '', email: '' };

/** Shared people pool for owners, approvers and audit actors across the Administration pages. */
export const ADMIN_PEOPLE: string[] = [];

export const ADMIN_LOCATIONS: string[] = [];

export const ADMIN_DEPARTMENTS: string[] = [];

/** Asset categories (Asset service), used by the role data scope. */
export const ASSET_CATEGORIES: string[] = [];

/** The Asset360 modules, used by access policies, roles and numbering. */
export const ASSET360_MODULES = [
  'Dashboard', 'Assets', 'Tracking & IoT', 'Movement & Custody', 'Maintenance', 'Inspection', 'Inventory',
  'Audit & Compliance', 'Contracts & Warranty', 'Financial', 'Reports & Analytics', 'Administration', 'Master Data Management'
];

/** Distinct, non-empty, sorted names. */
function names(values: (string | null | undefined)[]): string[] {
  return [...new Set(values.map((v) => (v ?? '').trim()).filter(Boolean))].sort((a, b) => a.localeCompare(b));
}

/** Replaces the contents of a shared lookup array without changing its reference. */
function fill(target: string[], values: string[]): void {
  target.splice(0, target.length, ...values);
}

/** A list GET that yields [] when the service is down or the caller may not read it. */
function list<T>(request: Observable<T[]>): Observable<T[]> {
  return request.pipe(
    map((rows) => (Array.isArray(rows) ? rows : [])),
    catchError(() => of([] as T[]))
  );
}

/**
 * Fills the shared Administration lookups once per session:
 * - ORGANIZATIONS: System Configuration organization name, then Master Data organizations
 * - ADMIN_LOCATIONS: Administration organization locations (the hidden organization root left out)
 * - ADMIN_DEPARTMENTS: Master Data departments
 * - ADMIN_PEOPLE: Administration users
 * - ASSET_CATEGORIES: Asset service categories
 * - CURRENT_USER: GET /api/users/me
 * A source that fails leaves its list empty, so the screens show their empty states.
 */
@Injectable({ providedIn: 'root' })
export class AdminLookupService {
  private readonly http = inject(HttpClient);
  private readonly access = inject(AccessService);
  private request: Observable<void> | null = null;

  ensureLoaded(): Observable<void> {
    return this.request ?? this.reload();
  }

  /** Reads every source again (e.g. after locations or users were added). */
  reload(): Observable<void> {
    const admin = environment.administrationApiUrl;
    const scope = { tenantId: environment.tenantId, clientId: environment.clientId };
    this.request = forkJoin({
      config: this.http
        .get<{ platformIdentity?: { organizationName?: string } }>(`${admin}api/administration/system-configuration`, { params: scope })
        .pipe(catchError(() => of(null))),
      organizations: list(this.http.get<{ organizationName: string }[]>(`${environment.masterDataApiUrl}api/master-data/organizations`)),
      locations: list(
        this.http.get<{ locationName: string; locationType: string }[]>(`${admin}api/organization/locations`, { params: scope })
      ),
      departments: list(this.http.get<{ departmentName: string }[]>(`${environment.masterDataApiUrl}api/master-data/departments`)),
      users: list(this.http.get<{ name: string }[]>(`${admin}api/users`, { params: scope })),
      categories: list(this.http.get<{ name: string }[]>(`${environment.assetApiUrl}api/asset-categories`)),
      me: this.access.ensureLoaded().pipe(catchError(() => of(null)))
    }).pipe(
      map(({ config, organizations, locations, departments, users, categories, me }) => {
        const configured = config?.platformIdentity?.organizationName?.trim();
        const orgs = names(organizations.map((o) => o.organizationName)).filter((n) => n !== configured);
        fill(ORGANIZATIONS, configured ? [configured, ...orgs] : orgs);
        fill(ADMIN_LOCATIONS, names(locations.filter((l) => l.locationType !== 'Organization').map((l) => l.locationName)));
        fill(ADMIN_DEPARTMENTS, names(departments.map((d) => d.departmentName)));
        fill(ADMIN_PEOPLE, names(users.map((u) => u.name)));
        fill(ASSET_CATEGORIES, names(categories.map((c) => c.name)));
        if (me) {
          const user = (me as { user?: { userId?: string } | null }).user;
          CURRENT_USER.id = user?.userId ?? '';
          CURRENT_USER.name = me.displayName || me.email;
          CURRENT_USER.role = me.roleName;
          CURRENT_USER.email = me.email;
        }
      }),
      shareReplay(1)
    );
    return this.request;
  }

  /** Name of the signed-in tenant's organization (for the hidden organization root location). */
  organizationName(): Observable<string> {
    return this.ensureLoaded().pipe(map(() => ORGANIZATIONS[0] || CURRENT_USER.name || 'Organization'));
  }
}

export function initials(name: string): string {
  return name.split(/\s+/).filter(Boolean).slice(0, 2).map((p) => p[0].toUpperCase()).join('');
}

const AVATAR_TONES = ['#ede7fb', '#e6effe', '#fde8f2', '#e4f7ef', '#fef1dc'];
const AVATAR_INKS = ['#5b2aa8', '#2563eb', '#db2777', '#059669', '#d97706'];

/** Stable soft background / ink pair for an avatar, picked from the name. */
export function avatarTone(name: string): { bg: string; ink: string } {
  let h = 0;
  for (const c of name) h = (h * 31 + c.charCodeAt(0)) >>> 0;
  const i = h % AVATAR_TONES.length;
  return { bg: AVATAR_TONES[i], ink: AVATAR_INKS[i] };
}

/** "12 Mar 2026" for an ISO date. */
export function formatDate(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
}

export function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}

export type ToastTone = 'info' | 'success' | 'error';

/** Base for the Administration pages: organization picker + toast. Loads the shared lookups on first use. */
@Directive()
export abstract class AdminPage implements OnDestroy {
  readonly organizations = ORGANIZATIONS;
  organization = ORGANIZATIONS[0] ?? '';
  toast: string | null = null;
  /** Colour of the toast: info (dark), success (green) or error (red). */
  toastTone: ToastTone = 'info';
  private toastTimer: ReturnType<typeof setTimeout> | null = null;
  private readonly lookupSub: Subscription = inject(AdminLookupService)
    .ensureLoaded()
    .subscribe(() => {
      if (!this.organization) this.organization = ORGANIZATIONS[0] ?? '';
    });

  notify(message: string, tone: ToastTone = 'info'): void {
    this.toast = message;
    this.toastTone = tone;
    if (this.toastTimer) clearTimeout(this.toastTimer);
    // errors stay a little longer so the reason can be read
    this.toastTimer = setTimeout(() => (this.toast = null), tone === 'error' ? 5000 : 3200);
  }

  ngOnDestroy(): void {
    if (this.toastTimer) clearTimeout(this.toastTimer);
    this.lookupSub.unsubscribe();
  }
}
