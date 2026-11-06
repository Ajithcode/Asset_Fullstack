import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { LucideAngularModule, Activity, CalendarClock, Eye, HeartPulse, Inbox, Search, TriangleAlert, Wrench, X } from 'lucide-angular';
import { ASSET_RECORDS, ASSET_TYPE_ICONS, AssetIconKey, TODAY, assetLocation, toIsoDate } from 'shared-ui';
import { loadAssetDetail } from '../asset-details/asset-details';

const STATUS_ORDER = ['Healthy', 'Good', 'Fair', 'Poor', 'Critical'];

/** One asset's health summary, from the register and its detail JSON. */
interface HealthRow {
  tag: string;
  name: string;
  category: string;
  location: string;
  icon: AssetIconKey;
  /** 0-100, or null when the asset has no health assessment. */
  score: number | null;
  /** Healthy / Good / Fair / Poor / Critical, or '' */
  status: string;
  grade: string;
  /** yyyy-mm-dd, or '' */
  assessmentDate: string;
  nextAssessment: string;
  /** Earliest next-due date in the asset's maintenance plan, or ''. */
  nextMaintenance: string;
  assessedBy: string;
}

/**
 * Menu page (Assets > Asset Health Score): health score of every asset.
 * One asset's full Health Score tab (with the asset header and tab bar) is /assets/health-score?tag=...
 */
@Component({
  selector: 'app-asset-health-overview',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './asset-health-overview.html',
  styleUrls: [
    '../asset-status-condition/asset-status-condition.css',
    '../asset-status-condition/asset-status-condition-panels.css',
    '../asset-status-condition/asset-status-overview.css',
  ],
})
export class AssetHealthOverview {
  readonly icons = { Activity, CalendarClock, Eye, HeartPulse, Inbox, Search, TriangleAlert, Wrench, X };
  readonly typeIcons = ASSET_TYPE_ICONS;
  readonly statuses = STATUS_ORDER;
  private readonly today = toIsoDate(TODAY);

  rows: HealthRow[] = [];
  loading = true;

  search = '';
  statusFilter = '';

  constructor(private readonly router: Router) {
    this.load();
  }

  private async load(): Promise<void> {
    const rows = await Promise.all(
      ASSET_RECORDS.map(async (asset): Promise<HealthRow> => {
        const h = (await loadAssetDetail(asset.tag))?.health;
        const nextDue = (h?.maintenancePlan ?? []).map((t) => t.nextDue).filter(Boolean).sort();
        return {
          tag: asset.tag,
          name: asset.name,
          category: asset.category,
          location: assetLocation(asset),
          icon: asset.icon,
          score: h?.score ?? null,
          status: h?.status ?? '',
          grade: h?.grade ?? '',
          assessmentDate: h?.assessmentDate ?? '',
          nextAssessment: h?.nextAssessment ?? '',
          nextMaintenance: nextDue[0] ?? '',
          assessedBy: h?.assessedBy ?? '',
        };
      }),
    );
    // Lowest health first, so the assets that need attention are on top; assets without a score last
    this.rows = rows.sort((a, b) => (a.score ?? 101) - (b.score ?? 101));
    this.loading = false;
  }

  // ===== Filters =====

  get filtered(): HealthRow[] {
    const term = this.search.trim().toLowerCase();
    return this.rows.filter(
      (r) =>
        (!term || [r.tag, r.name, r.category, r.location].some((v) => v.toLowerCase().includes(term))) &&
        (!this.statusFilter || r.status === this.statusFilter),
    );
  }

  get hasFilters(): boolean {
    return !!(this.search.trim() || this.statusFilter);
  }

  resetFilters(): void {
    this.search = '';
    this.statusFilter = '';
  }

  // ===== KPIs (over the filtered assets) =====

  get avgScore(): number | null {
    const known = this.filtered.map((r) => r.score).filter((v): v is number => v !== null);
    return known.length ? Math.round(known.reduce((a, b) => a + b, 0) / known.length) : null;
  }

  countStatus(...statuses: string[]): number {
    return this.filtered.filter((r) => statuses.includes(r.status)).length;
  }

  get overdueAssessments(): number {
    return this.filtered.filter((r) => this.isPast(r.nextAssessment)).length;
  }

  // ===== Display =====

  isPast(date: string): boolean {
    return !!date && date < this.today;
  }

  statusTone(status: string): string {
    return status === 'Healthy' || status === 'Good' ? 'good' : status === 'Fair' ? 'warn' : status === 'Poor' ? 'serious' : 'critical';
  }

  scoreTone(score: number): string {
    return score >= 75 ? 'good' : score >= 50 ? 'warn' : 'critical';
  }

  /** Opens the asset's Health Score tab (asset header and tab bar). */
  open(row: HealthRow): void {
    this.router.navigate(['/assets/health-score'], { queryParams: { tag: row.tag } });
  }

  trackByTag(_: number, row: HealthRow): string {
    return row.tag;
  }
}
