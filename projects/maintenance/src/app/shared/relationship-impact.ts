import { Component, DestroyRef, effect, inject, input, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, catchError, forkJoin, map, of, switchMap, throwError } from 'rxjs';
import { LucideAngularModule, AlertTriangle } from 'lucide-angular';
import { environment } from 'shared-ui';

const ASSET_API = `${environment.assetApiUrl}api/`;
const PAGE = 200;

interface SubAssetDto { assetRegistrationId: string | null; componentId: string | null; name: string; tag: string | null; relationshipType: string }
interface RelationshipDto { id: string; mainAssetRegistrationId: string; mainAssetName: string | null; mainAssetTag: string | null; subAssets: SubAssetDto[] }
interface RegistrationDto { assetRegistrationId: string; basicInformation?: { assetName?: string | null; assetTag?: string | null } | null }

/** One asset the work order's asset may affect. */
export interface AffectedAsset {
  name: string;
  tag: string;
  /** "Supporting Asset of Generator" or "via UPS → Server". */
  reason: string;
  depth: number;
}

/**
 * Maintenance impact analysis for a work order: assets that may be affected when the work order's asset is shut down,
 * found by following Asset Relationships downstream (main asset -> its linked assets, transitively, each asset once).
 * Uses the Asset service only (GET /api/asset-registrations/{tag}, GET /api/asset-relationships).
 */
@Component({
  selector: 'app-mnt-relationship-impact',
  imports: [LucideAngularModule],
  template: `
    @if (assetTag().trim()) {
      <section class="ri" aria-live="polite">
        <header class="ri-head"><lucide-icon [img]="icon" [size]="16"></lucide-icon><strong>Potentially affected assets</strong></header>
        @if (loading()) {
          <p class="ri-note">Checking asset relationships…</p>
        } @else if (error()) {
          <p class="ri-note">{{ error() }}</p>
        } @else if (affected().length) {
          <ul class="ri-list">
            @for (a of affected(); track a.tag + a.name) {
              <li><span class="ri-asset">{{ a.tag || a.name }}</span><span class="ri-why">{{ a.tag ? a.name + ' · ' : '' }}{{ a.reason }}</span></li>
            }
          </ul>
          <p class="ri-note">From the saved Asset Relationships. Actual impact depends on redundancy and current operating conditions.</p>
        } @else {
          <p class="ri-note">{{ notFound() ? 'This asset tag is not in the asset register.' : 'No other assets depend on this asset through the saved relationships.' }}</p>
        }
      </section>
    }
  `,
  styles: `
    .ri { grid-column: 1 / -1; display: flex; flex-direction: column; gap: 6px; padding: 10px 12px; border: 1px solid #fde68a; border-radius: 8px; background: #fffbeb; }
    .ri-head { display: flex; align-items: center; gap: 6px; color: #92400e; font-size: 0.844rem; }
    .ri-list { display: flex; flex-direction: column; gap: 4px; margin: 0; padding: 0; list-style: none; }
    .ri-list li { display: flex; flex-wrap: wrap; gap: 4px 10px; font-size: 0.813rem; }
    .ri-asset { font-weight: 700; color: #111827; }
    .ri-why { color: #6b7280; }
    .ri-note { margin: 0; font-size: 0.75rem; color: #6b7280; }
  `,
})
export class RelationshipImpact {
  /** The work order's asset tag (or Asset ID). */
  readonly assetTag = input<string>('');
  readonly icon = AlertTriangle;

  readonly loading = signal(false);
  readonly error = signal('');
  readonly notFound = signal(false);
  readonly affected = signal<AffectedAsset[]>([]);

  private readonly http = inject(HttpClient);
  private timer?: ReturnType<typeof setTimeout>;
  private request?: { unsubscribe(): void };

  constructor() {
    // Re-check shortly after the tag stops changing (it can be typed)
    effect(() => {
      const tag = this.assetTag().trim();
      clearTimeout(this.timer);
      this.request?.unsubscribe();
      if (!tag) return this.affected.set([]);
      this.timer = setTimeout(() => this.check(tag), 400);
    });
    inject(DestroyRef).onDestroy(() => {
      clearTimeout(this.timer);
      this.request?.unsubscribe();
    });
  }

  private check(tag: string): void {
    this.loading.set(true);
    this.error.set('');
    this.notFound.set(false);
    this.request = forkJoin({ id: this.resolveId(tag), records: this.allRelationships() }).subscribe({
      next: ({ id, records }) => {
        this.loading.set(false);
        if (!id) {
          this.notFound.set(true);
          this.affected.set([]);
          return;
        }
        this.affected.set(downstream(id, records));
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Could not check the asset relationships.');
      },
    });
  }

  /** Asset ID for a tag or ID: GET /api/asset-registrations/{key}; null when the register has no such asset. */
  private resolveId(key: string): Observable<string | null> {
    return this.http.get<RegistrationDto>(`${ASSET_API}asset-registrations/${encodeURIComponent(key)}`).pipe(
      map((dto) => dto.assetRegistrationId),
      catchError((err: unknown) =>
        err instanceof HttpErrorResponse && err.status === 404
          ? this.http
              .get<{ items: { assetRegistrationId: string; assetTag: string | null }[] }>(`${ASSET_API}asset-registrations`, {
                params: new HttpParams().set('search', key).set('page', 1).set('pageSize', 50),
              })
              .pipe(map((res) => (res.items ?? []).find((a) => (a.assetTag ?? '').toLowerCase() === key.toLowerCase())?.assetRegistrationId ?? null))
          : throwError(() => err),
      ),
    );
  }

  /** Every relationship record, all pages. */
  private allRelationships(): Observable<RelationshipDto[]> {
    const page = (n: number) =>
      this.http.get<{ items: RelationshipDto[]; totalCount: number }>(`${ASSET_API}asset-relationships`, {
        params: new HttpParams().set('page', n).set('pageSize', PAGE),
      });
    return page(1).pipe(
      switchMap((first) => {
        const pages = Math.ceil((first.totalCount ?? 0) / PAGE);
        if (pages <= 1) return of(first.items ?? []);
        return forkJoin(Array.from({ length: pages - 1 }, (_, i) => page(i + 2).pipe(map((r) => r.items ?? [])))).pipe(
          map((more) => [...(first.items ?? []), ...more.flat()]),
        );
      }),
    );
  }
}

/** Breadth-first walk from the asset through its linked assets (cycle-safe). */
export function downstream(startId: string, records: RelationshipDto[]): AffectedAsset[] {
  const byMain = new Map(records.map((r) => [r.mainAssetRegistrationId, r]));
  const names = new Map<string, string>();
  records.forEach((r) => names.set(r.mainAssetRegistrationId, r.mainAssetName || r.mainAssetTag || r.mainAssetRegistrationId));
  const seen = new Set<string>([startId]);
  const out: AffectedAsset[] = [];
  let frontier: { id: string; path: string[] }[] = [{ id: startId, path: [names.get(startId) ?? startId] }];
  for (let depth = 1; frontier.length; depth++) {
    const next: { id: string; path: string[] }[] = [];
    for (const f of frontier) {
      for (const s of byMain.get(f.id)?.subAssets ?? []) {
        const key = s.assetRegistrationId ?? `component:${s.componentId}`;
        if (seen.has(key)) continue;
        seen.add(key);
        const name = s.name || s.tag || key;
        const path = [...f.path, name];
        out.push({
          name,
          tag: s.tag ?? '',
          depth,
          reason: depth === 1 ? `${s.relationshipType} of ${f.path[f.path.length - 1]}` : `via ${path.slice(1, -1).join(' → ')}`,
        });
        if (s.assetRegistrationId) next.push({ id: s.assetRegistrationId, path });
      }
    }
    frontier = next;
  }
  return out;
}
