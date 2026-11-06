import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { LucideAngularModule, CalendarClock, CircleAlert, Eye, Gauge, Inbox, Search, ShieldAlert, TriangleAlert, X } from 'lucide-angular';
import { ASSET_RECORDS, ASSET_TYPE_ICONS, AssetIconKey, TODAY, assetLocation, toIsoDate } from 'shared-ui';
import { loadAssetDetail } from '../asset-details/asset-details';

const LEVEL_ORDER = ['Critical', 'High', 'Medium', 'Low'];

/** One asset's criticality summary, from the register and its detail JSON. */
interface CriticalityRow {
  tag: string;
  name: string;
  category: string;
  location: string;
  icon: AssetIconKey;
  level: string;
  /** 0-100, or null when the asset has no criticality assessment. */
  score: number | null;
  /** Likelihood x consequence (1-25), or null. */
  risk: number | null;
  /** yyyy-mm-dd, or '' */
  assessmentDate: string;
  validUntil: string;
  nextReview: string;
  openMitigations: number;
  downtimeCost: number | null;
  currency: string;
}

/**
 * Menu page (Assets > Asset Criticality): criticality of every asset.
 * One asset's full Criticality tab (with the asset header and tab bar) is /assets/criticality?tag=...
 */
@Component({
  selector: 'app-asset-criticality-overview',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './asset-criticality-overview.html',
  styleUrls: [
    '../asset-status-condition/asset-status-condition.css',
    '../asset-status-condition/asset-status-condition-panels.css',
    '../asset-status-condition/asset-status-overview.css',
  ],
})
export class AssetCriticalityOverview {
  readonly icons = { CalendarClock, CircleAlert, Eye, Gauge, Inbox, Search, ShieldAlert, TriangleAlert, X };
  readonly typeIcons = ASSET_TYPE_ICONS;
  readonly levels = LEVEL_ORDER;
  private readonly today = toIsoDate(TODAY);

  rows: CriticalityRow[] = [];
  loading = true;

  search = '';
  levelFilter = '';

  constructor(private readonly router: Router) {
    this.load();
  }

  private async load(): Promise<void> {
    const rows = await Promise.all(
      ASSET_RECORDS.map(async (asset): Promise<CriticalityRow> => {
        const c = (await loadAssetDetail(asset.tag))?.criticality;
        return {
          tag: asset.tag,
          name: asset.name,
          category: asset.category,
          location: assetLocation(asset),
          icon: asset.icon,
          level: c?.level || asset.criticality,
          score: c?.score ?? null,
          risk: c?.likelihood && c?.consequence ? c.likelihood * c.consequence : null,
          assessmentDate: c?.assessmentDate ?? '',
          validUntil: c?.validUntil ?? '',
          nextReview: c?.nextReview ?? '',
          openMitigations: (c?.mitigations ?? []).filter((m) => m.status !== 'Completed').length,
          downtimeCost: c?.impact?.downtimeCostPerHour ?? null,
          currency: c?.impact?.currency ?? '',
        };
      }),
    );
    // Most critical first, then highest score
    this.rows = rows.sort((a, b) => LEVEL_ORDER.indexOf(a.level) - LEVEL_ORDER.indexOf(b.level) || (b.score ?? -1) - (a.score ?? -1));
    this.loading = false;
  }

  // ===== Filters =====

  get filtered(): CriticalityRow[] {
    const term = this.search.trim().toLowerCase();
    return this.rows.filter(
      (r) =>
        (!term || [r.tag, r.name, r.category, r.location].some((v) => v.toLowerCase().includes(term))) &&
        (!this.levelFilter || r.level === this.levelFilter),
    );
  }

  get hasFilters(): boolean {
    return !!(this.search.trim() || this.levelFilter);
  }

  resetFilters(): void {
    this.search = '';
    this.levelFilter = '';
  }

  // ===== KPIs (over the filtered assets) =====

  countLevel(level: string): number {
    return this.filtered.filter((r) => r.level === level).length;
  }

  get avgScore(): number | null {
    const known = this.filtered.map((r) => r.score).filter((v): v is number => v !== null);
    return known.length ? Math.round(known.reduce((a, b) => a + b, 0) / known.length) : null;
  }

  get overdueReviews(): number {
    return this.filtered.filter((r) => this.isPast(r.nextReview)).length;
  }

  get openMitigations(): number {
    return this.filtered.reduce((sum, r) => sum + r.openMitigations, 0);
  }

  // ===== Display =====

  isPast(date: string): boolean {
    return !!date && date < this.today;
  }

  levelTone(level: string): string {
    return level === 'Critical' ? 'critical' : level === 'High' ? 'serious' : level === 'Medium' ? 'warn' : 'good';
  }

  scoreTone(score: number): string {
    return score >= 75 ? 'critical' : score >= 50 ? 'warn' : 'good';
  }

  /** Opens the asset's Criticality tab (asset header and tab bar). */
  open(row: CriticalityRow): void {
    this.router.navigate(['/assets/criticality'], { queryParams: { tag: row.tag } });
  }

  trackByTag(_: number, row: CriticalityRow): string {
    return row.tag;
  }
}
