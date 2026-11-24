import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, forkJoin, from, map, mergeMap, of, switchMap, toArray } from 'rxjs';
import { ASSET_API } from '../../services/asset-api';
import { StatusConditionAsset, StatusConditionService } from '../../asset-status-condition-overview/status-condition.service';
import { AssetDisposalService, DisposalListItemDto } from '../../asset-disposal/asset-disposal.service';
import { LifecycleRecordDto } from '../asset-lifecycle.service';

/** Lifecycle phases shown on the landing page; each groups one or more lifecycle stage keys of the API. */
export const PHASES = ['Planning', 'Acquisition', 'Commissioning', 'In Service', 'Retired', 'Disposal'] as const;
export type Phase = (typeof PHASES)[number] | 'Not tracked';

/** API stage key (AssetLifecycleOptions.StageKeys) -> landing-page phase. */
const PHASE_OF_STAGE: Record<string, Phase> = {
  planning: 'Planning',
  procurement: 'Acquisition',
  received: 'Acquisition',
  installation: 'Commissioning',
  commissioning: 'Commissioning',
  operation: 'In Service',
  maintenance: 'In Service',
  upgrade: 'In Service',
  decommissioning: 'Retired',
  disposal: 'Disposal',
};

/** Disposal state of an asset, from the disposal requests that include it. */
export type DisposalState = 'Not Started' | 'Draft' | 'Pending Approval' | 'Approved' | 'Completed';

export interface LifecycleEvent {
  assetRegistrationId: string;
  assetTag: string;
  assetName: string;
  date: string;
  title: string;
  kind: string;
  by: string;
}

/** One asset on the Asset Lifecycle landing page. */
export interface LifecycleRow {
  asset: StatusConditionAsset;
  /** Current lifecycle stage key (the stage marked Current), '' when none is recorded. */
  stageKey: string;
  phase: Phase;
  acquisitionDate: string | null;
  commissioningDate: string | null;
  /** Reference date for age and life used: commissioning, else acquisition. */
  referenceDate: string | null;
  ageYears: number | null;
  usefulLifeYears: number | null;
  /** Elapsed share of the planned service life, 0-100 (capped); null when life or dates are unknown. */
  lifeUsedPct: number | null;
  plannedReplacement: string | null;
  warrantyEnd: string | null;
  purchaseCost: number | null;
  currency: string;
  disposal: DisposalState;
  disposalRequest: { id: string; number: string } | null;
  events: LifecycleEvent[];
}

export interface LifecycleData {
  rows: LifecycleRow[];
  /** Every lifecycle event and stage change across assets, newest first. */
  events: LifecycleEvent[];
}

/** The parts of GET /api/asset-registrations/{id} the landing page uses. */
interface RegistrationDto {
  assetRegistrationId: string;
  purchaseWarranty?: {
    purchase?: { purchaseDate?: string | null; deliveryDate?: string | null; purchaseCost?: number | null; totalCost?: number | null; currency?: string | null } | null;
    warranty?: { warrantyEndDate?: string | null } | null;
  } | null;
  additionalInformation?: { technical?: { expectedLifespan?: number | null; expectedLifespanUnit?: string | null } | null } | null;
}

const YEAR_MS = 365.25 * 86_400_000;
/** Assets loaded in parallel for their registration and lifecycle record. */
const CONCURRENCY = 6;

/** Expected lifespan in years; null for units that are not a length of time (e.g. operating hours). */
export function lifespanYears(value: number | null | undefined, unit: string | null | undefined): number | null {
  if (value == null || !(value > 0)) return null;
  const u = (unit || 'years').trim().toLowerCase();
  if (u.startsWith('year') || u === 'yr' || u === 'yrs') return value;
  if (u.startsWith('month')) return value / 12;
  if (u.startsWith('week')) return value / 52.18;
  if (u.startsWith('day')) return value / 365.25;
  return null;
}

function addYears(iso: string, years: number): string {
  return new Date(new Date(iso).getTime() + years * YEAR_MS).toISOString();
}

const ACTIVE_DISPOSAL = ['Draft', 'Pending Approval', 'Approved'];

@Injectable({ providedIn: 'root' })
export class LifecycleOverviewService {
  private readonly http = inject(HttpClient);
  private readonly assets = inject(StatusConditionService);
  private readonly disposals = inject(AssetDisposalService);

  /**
   * Loads the landing page: every asset (Asset Status & Condition list), each asset's registration (dates, cost,
   * expected lifespan, warranty) and lifecycle record (stages, events), and the disposal requests with their assets.
   */
  load(): Observable<LifecycleData> {
    return forkJoin({ assets: this.assets.allAssets(), disposal: this.disposalByAsset() }).pipe(
      switchMap(({ assets, disposal }) =>
        from(assets).pipe(
          mergeMap(
            (a) =>
              forkJoin({
                reg: this.http.get<RegistrationDto>(`${ASSET_API}asset-registrations/${encodeURIComponent(a.assetRegistrationId)}`).pipe(catchError(() => of(null))),
                lc: this.http.get<LifecycleRecordDto>(`${ASSET_API}asset-lifecycle/${encodeURIComponent(a.assetRegistrationId)}`).pipe(catchError(() => of(null))),
              }).pipe(map(({ reg, lc }) => toRow(a, reg, lc, disposal.get(a.assetRegistrationId)))),
            CONCURRENCY,
          ),
          toArray(),
        ),
      ),
      map((rows) => ({
        rows: rows.sort((x, y) => (x.asset.assetTag ?? '').localeCompare(y.asset.assetTag ?? '')),
        events: rows.flatMap((r) => r.events).sort((x, y) => y.date.localeCompare(x.date)),
      })),
    );
  }

  /** Disposal state per asset: every request (all pages), opened for its assets; completed beats active beats ended. */
  private disposalByAsset(): Observable<Map<string, { state: DisposalState; id: string; number: string }>> {
    const page = (n: number) => this.disposals.list({ page: n, pageSize: 100 });
    return page(1).pipe(
      switchMap((first) => {
        const pages = Math.ceil((first.totalCount ?? 0) / 100);
        const rest = pages > 1 ? forkJoin(Array.from({ length: pages - 1 }, (_, i) => page(i + 2).pipe(map((r) => r.items)))) : of([] as DisposalListItemDto[][]);
        return rest.pipe(map((more) => [...first.items, ...more.flat()]));
      }),
      switchMap((items) => {
        // Rejected / cancelled requests leave the asset's disposal state unchanged
        const relevant = items.filter((i) => i.status === 'Completed' || ACTIVE_DISPOSAL.includes(i.status ?? ''));
        if (!relevant.length) return of([]);
        return from(relevant).pipe(
          mergeMap((i) => this.disposals.get(i.id).pipe(catchError(() => of(null))), CONCURRENCY),
          toArray(),
        );
      }),
      map((requests) => {
        const rank = (s: DisposalState) => (s === 'Completed' ? 3 : s === 'Approved' ? 2 : s === 'Pending Approval' ? 1 : 0);
        const out = new Map<string, { state: DisposalState; id: string; number: string }>();
        for (const r of requests) {
          if (!r) continue;
          const state = (r.status ?? 'Draft') as DisposalState;
          for (const a of r.assets ?? []) {
            const prev = out.get(a.assetRegistrationId);
            if (!prev || rank(state) > rank(prev.state)) out.set(a.assetRegistrationId, { state, id: r.id, number: r.requestNumber });
          }
        }
        return out;
      }),
      catchError(() => of(new Map())),
    );
  }
}

function toRow(
  a: StatusConditionAsset,
  reg: RegistrationDto | null,
  lc: LifecycleRecordDto | null,
  disposal: { state: DisposalState; id: string; number: string } | undefined,
): LifecycleRow {
  const purchase = reg?.purchaseWarranty?.purchase;
  const stages = lc?.stages ?? [];
  const current = stages.find((s) => s.status === 'Current');
  const stageDate = (...keys: string[]) =>
    stages.find((s) => keys.includes(s.key) && (s.status === 'Completed' || s.status === 'Current') && s.date)?.date ?? null;
  const acquisitionDate = purchase?.purchaseDate ?? stageDate('received', 'procurement') ?? purchase?.deliveryDate ?? null;
  const commissioningDate = stageDate('commissioning', 'operation');
  const referenceDate = commissioningDate ?? acquisitionDate;
  const tech = reg?.additionalInformation?.technical;
  const usefulLifeYears = lifespanYears(tech?.expectedLifespan, tech?.expectedLifespanUnit);
  const ageYears = referenceDate ? Math.max(0, (Date.now() - new Date(referenceDate).getTime()) / YEAR_MS) : null;
  const lifeUsedPct = usefulLifeYears && ageYears != null ? Math.min(100, Math.round((ageYears / usefulLifeYears) * 100)) : null;
  const tag = a.assetTag || a.assetRegistrationId;
  const events: LifecycleEvent[] = [
    ...(lc?.events ?? []).map((e) => ({ assetRegistrationId: a.assetRegistrationId, assetTag: tag, assetName: a.assetName ?? '', date: e.date, title: e.title, kind: e.kind, by: e.by ?? '' })),
    ...stages
      .filter((s) => s.date && (s.status === 'Completed' || s.status === 'Current'))
      .map((s) => ({
        assetRegistrationId: a.assetRegistrationId, assetTag: tag, assetName: a.assetName ?? '', date: s.date!,
        title: `${stageLabel(s.key)} ${s.status === 'Current' ? 'started' : 'completed'}${s.notes ? ' – ' + s.notes : ''}`,
        kind: 'Stage', by: s.owner ?? '',
      })),
  ];
  return {
    asset: a,
    stageKey: current?.key ?? '',
    phase: current ? (PHASE_OF_STAGE[current.key] ?? 'Not tracked') : 'Not tracked',
    acquisitionDate,
    commissioningDate,
    referenceDate,
    ageYears,
    usefulLifeYears,
    lifeUsedPct,
    plannedReplacement: referenceDate && usefulLifeYears ? addYears(referenceDate, usefulLifeYears) : null,
    warrantyEnd: reg?.purchaseWarranty?.warranty?.warrantyEndDate ?? null,
    purchaseCost: purchase?.totalCost ?? purchase?.purchaseCost ?? null,
    currency: purchase?.currency ?? '',
    disposal: disposal?.state ?? 'Not Started',
    disposalRequest: disposal ? { id: disposal.id, number: disposal.number } : null,
    events,
  };
}

/** "Commissioning" for "commissioning", "Decommissioning" for "decommissioning", etc. */
export function stageLabel(key: string): string {
  return key ? key.charAt(0).toUpperCase() + key.slice(1) : '-';
}
