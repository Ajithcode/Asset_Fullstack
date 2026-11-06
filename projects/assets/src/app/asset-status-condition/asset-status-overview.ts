import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { LucideAngularModule, Activity, CircleAlert, Eye, Gauge, Inbox, Power, Search, Timer, X } from 'lucide-angular';
import { ASSET_RECORDS, ASSET_TYPE_ICONS, AssetIconKey, assetHeader } from 'shared-ui';
import { loadAssetDetail } from '../asset-details/asset-details';

/** One asset's status and condition summary, from the register and its detail JSON. */
interface StatusRow {
  tag: string;
  name: string;
  category: string;
  location: string;
  icon: AssetIconKey;
  status: string;
  /** Latest assessment's overall condition, or the register's condition when there is no detail data. */
  condition: string;
  healthScore: number | null;
  availability: number | null;
  utilization: number | null;
  /** yyyy-mm-dd, or '' */
  lastMaintenance: string;
  openAlerts: number;
  criticality: string;
}

/**
 * Menu page (Assets > Asset Status & Condition): status and condition of every asset.
 * One asset's full Status & Condition tab (with the asset header and tab bar) is /assets/status-condition?tag=...
 */
@Component({
  selector: 'app-asset-status-overview',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './asset-status-overview.html',
  styleUrls: ['./asset-status-condition.css', './asset-status-condition-panels.css', './asset-status-overview.css'],
})
export class AssetStatusOverview {
  readonly icons = { Activity, CircleAlert, Eye, Gauge, Inbox, Power, Search, Timer, X };
  readonly typeIcons = ASSET_TYPE_ICONS;

  rows: StatusRow[] = [];
  loading = true;

  search = '';
  statusFilter = '';
  conditionFilter = '';

  constructor(private readonly router: Router) {
    this.load();
  }

  private async load(): Promise<void> {
    this.rows = await Promise.all(
      ASSET_RECORDS.map(async (asset): Promise<StatusRow> => {
        const header = assetHeader(asset);
        const detail = await loadAssetDetail(asset.tag);
        const s = detail?.status;
        const latest = s?.assessments?.length
          ? [...s.assessments].sort((a, b) => b.date.localeCompare(a.date))[0]
          : null;
        return {
          tag: asset.tag,
          name: asset.name,
          category: asset.category,
          location: s?.operational?.location || header.location,
          icon: asset.icon,
          status: s?.operational?.status || asset.status,
          condition: latest?.overall || header.condition,
          healthScore: detail?.health?.score ?? null,
          availability: s?.availability ?? null,
          utilization: s?.utilization ?? null,
          lastMaintenance: s?.lastMaintenance ?? '',
          openAlerts: (s?.alerts ?? []).filter((a) => a.status !== 'Resolved').length,
          criticality: asset.criticality,
        };
      }),
    );
    this.loading = false;
  }

  // ===== Filters =====

  get statusOptions(): string[] {
    return [...new Set(this.rows.map((r) => r.status))].sort();
  }

  get conditionOptions(): string[] {
    const order = ['Excellent', 'Good', 'Fair', 'Poor', 'Critical'];
    return [...new Set(this.rows.map((r) => r.condition))].sort((a, b) => order.indexOf(a) - order.indexOf(b));
  }

  get filtered(): StatusRow[] {
    const term = this.search.trim().toLowerCase();
    return this.rows.filter(
      (r) =>
        (!term || [r.tag, r.name, r.category, r.location].some((v) => v.toLowerCase().includes(term))) &&
        (!this.statusFilter || r.status === this.statusFilter) &&
        (!this.conditionFilter || r.condition === this.conditionFilter),
    );
  }

  get hasFilters(): boolean {
    return !!(this.search.trim() || this.statusFilter || this.conditionFilter);
  }

  resetFilters(): void {
    this.search = '';
    this.statusFilter = '';
    this.conditionFilter = '';
  }

  // ===== KPIs (over the filtered assets) =====

  get activeCount(): number {
    return this.filtered.filter((r) => r.status === 'Active').length;
  }

  get attentionCount(): number {
    return this.filtered.filter((r) => ['Poor', 'Critical'].includes(r.condition)).length;
  }

  get openAlerts(): number {
    return this.filtered.reduce((sum, r) => sum + r.openAlerts, 0);
  }

  private average(values: (number | null)[]): number | null {
    const known = values.filter((v): v is number => v !== null);
    return known.length ? Math.round(known.reduce((a, b) => a + b, 0) / known.length) : null;
  }

  get avgAvailability(): number | null {
    return this.average(this.filtered.map((r) => r.availability));
  }

  get avgHealth(): number | null {
    return this.average(this.filtered.map((r) => r.healthScore));
  }

  // ===== Display =====

  statusTone(status: string): string {
    return status === 'Active' ? 'good' : status === 'Standby' || status === 'Under Maintenance' ? 'warn' : 'critical';
  }

  conditionTone(condition: string): string {
    return condition === 'Excellent' || condition === 'Good' ? 'good' : condition === 'Fair' ? 'warn' : condition === 'Poor' ? 'serious' : 'critical';
  }

  criticalityTone(criticality: string): string {
    return criticality === 'Critical' ? 'critical' : criticality === 'Low' ? 'good' : 'warn';
  }

  healthTone(score: number): string {
    return score >= 75 ? 'good' : score >= 50 ? 'warn' : 'critical';
  }

  /** Opens the asset's Status & Condition tab (asset header and tab bar). */
  open(row: StatusRow): void {
    this.router.navigate(['/assets/status-condition'], { queryParams: { tag: row.tag } });
  }

  trackByTag(_: number, row: StatusRow): string {
    return row.tag;
  }
}
