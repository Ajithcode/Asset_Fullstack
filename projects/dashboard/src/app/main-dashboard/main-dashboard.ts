import { Component, OnInit, ChangeDetectorRef, Input, DestroyRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { DragDropModule, CdkDragDrop } from '@angular/cdk/drag-drop';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, catchError, forkJoin, of } from 'rxjs';
import { WidgetDragHandle } from '../shared/widget-drag-handle/widget-drag-handle';
import { loadOrder, saveOrder, reorderByKey, moveWithinVisible } from '../shared/dashboard-widgets/widget-order.util';
import { Dashboard as DashboardApi } from '../services/dashboard/dashboard';
import { ModuleWidgetsApi, timeAgo } from '../services/dashboard/module-widgets';

/** Donut ring segment (circle r=55 in the 140x140 SVG). */
interface DonutSegment {
  color: string;
  dash: string;
  offset: number;
}

const DONUT_CIRCUMFERENCE = 2 * Math.PI * 55;
const PALETTE = ['#3b82f6', '#22c55e', '#8b5cf6', '#f97316', '#94a3b8', '#eab308', '#ef4444', '#0891b2'];
const STATUS_COLORS: Record<string, string> = {
  Active: '#22c55e',
  Inactive: '#94a3b8',
  'In Storage': '#3b82f6',
  'Under Maintenance': '#eab308',
  'In Repair': '#f97316',
  'Out of Service': '#ef4444',
  Disposed: '#64748b',
};
const SEVERITY_COLORS: Record<string, string> = { critical: 'red', high: 'orange', medium: 'yellow', low: 'purple' };
/** Alerts / location updates window of the Asset tab. */
const ALERT_DAYS = 30;

/** Donut legend + ring segments from label/count rows; rows with no assets are left out. */
function buildDonut(rows: { label: string; count: number; color: string }[], total: number): { legend: DonutLegendItem[]; segments: DonutSegment[] } {
  const shown = rows.filter((r) => r.count > 0);
  let acc = 0;
  const segments = shown.map((r) => {
    const len = total ? (r.count / total) * DONUT_CIRCUMFERENCE : 0;
    const seg = { color: r.color, dash: `${len} ${DONUT_CIRCUMFERENCE - len}`, offset: -acc };
    acc += len;
    return seg;
  });
  const legend = shown.map((r) => ({
    label: r.label,
    value: r.count.toLocaleString(),
    percentage: `${total ? Math.round((r.count / total) * 1000) / 10 : 0}%`,
    color: r.color,
  }));
  return { legend, segments };
}

interface StatCard {
  label: string;
  value: string;
  iconColor: string;
  change?: string;
  changeDirection?: 'up' | 'down';
  subLabel?: string;
  badge?: string;
  live?: boolean;
}

interface DonutLegendItem {
  label: string;
  value: string;
  percentage: string;
  color: string;
}

interface AlertItem {
  type: string;
  asset: string;
  time: string;
  severity: string;
}

interface GeofenceItem {
  name: string;
  assetsInside: number;
  alerts: number;
}

interface ActivityItem {
  assetId: string;
  assetName: string;
  route: string;
  status: string;
  location: string;
  speed: string;
  lastUpdate: string;
  live: boolean;
  thumbColor: string;
}

interface ActiveAsset {
  assetId: string;
  route: string;
  speed: string;
  speedPercent: number;
  thumbColor: string;
}

interface DistanceAsset {
  rank: number;
  assetId: string;
  name: string;
  distance: string;
}

interface DashboardData {
  statCards: StatCard[];
  assetStatusDonut: { total: string; totalLabel: string; legend: DonutLegendItem[]; segments: DonutSegment[] };
  assetsByTypeDonut: { total: string; totalLabel: string; legend: DonutLegendItem[]; segments: DonutSegment[] };
  recentAlerts: AlertItem[];
  utilizationChart: { yAxisLabels: string[]; xAxisLabels: string[]; peak: { value: string; x: number; y: number }; linePoints: string };
  geofenceSummary: GeofenceItem[];
  recentActivity: ActivityItem[];
  topActiveAssets: ActiveAsset[];
  topAssetsByDistance: DistanceAsset[];
}

/** Stat card layout (labels, colours, badges); values stay 0 until a data source is wired. */
const STAT_CARD_SHELLS: StatCard[] = [
  { label: 'Total Assets', value: '0', iconColor: '#2563eb' },
  { label: 'Online / Moving', value: '0', iconColor: '#22c55e' },
  { label: 'Idle Assets', value: '0', iconColor: '#8b5cf6' },
  { label: 'Offline', value: '0', iconColor: '#f97316' },
  { label: 'Alerts', value: '0', iconColor: '#ef4444', badge: 'Critical / High' },
];

@Component({
  standalone: true,
  selector: 'app-main-dashboard',
  imports: [CommonModule, DragDropModule,  WidgetDragHandle],
  templateUrl: './main-dashboard.html',
  styleUrls: ['./main-dashboard.css'],
})
export class MainDashboard implements OnInit {
  /** Widget ids (see WIDGET_CATALOG 'asset') the user chose to show. */
  @Input() selected: string[] = [];

  statCards: StatCard[] = [];
  // Position of each card in the source data, so icons/popups stay tied to the card, not its slot
  private statDataIndex = new Map<string, number>();

  // ===== Widget drag-and-drop ordering =====
  readonly row2Order: string[] = loadOrder('piq.dashboard.main.row2Order', ['donutStatus', 'donutType']);
  readonly alertsRowOrder: string[] = loadOrder('piq.dashboard.main.alertsRowOrder', ['alerts', 'topActive']);
  readonly row3Order: string[] = loadOrder('piq.dashboard.main.row3Order', ['utilization', 'geofence']);
  readonly row4Order: string[] = loadOrder('piq.dashboard.main.row4Order', ['activityTable', 'topDistance']);

  trackByWidgetId = (_: number, id: string) => id;

  has(id: string): boolean {
    return this.selected.includes(id);
  }

  visible(order: string[]): string[] {
    return order.filter((id) => this.has(id));
  }

  get visibleStatCards(): StatCard[] {
    return this.statCards.filter((c) => this.has('stat:' + c.label));
  }

  statIndex(card: StatCard): number {
    return this.statDataIndex.get(card.label) ?? 0;
  }

  private persistDrop(event: CdkDragDrop<string[]>, order: string[], storageKey: string): void {
    moveWithinVisible(order, (id) => this.has(id), event.previousIndex, event.currentIndex);
    saveOrder(storageKey, order);
  }

  onStatCardDrop(event: CdkDragDrop<StatCard[]>): void {
    moveWithinVisible(this.statCards, (c) => this.has('stat:' + c.label), event.previousIndex, event.currentIndex);
    saveOrder('piq.dashboard.main.statOrder', this.statCards.map((c) => c.label));
  }

  onRow2Drop(event: CdkDragDrop<string[]>): void {
    this.persistDrop(event, this.row2Order, 'piq.dashboard.main.row2Order');
  }

  onAlertsRowDrop(event: CdkDragDrop<string[]>): void {
    this.persistDrop(event, this.alertsRowOrder, 'piq.dashboard.main.alertsRowOrder');
  }

  onRow3Drop(event: CdkDragDrop<string[]>): void {
    this.persistDrop(event, this.row3Order, 'piq.dashboard.main.row3Order');
  }

  onRow4Drop(event: CdkDragDrop<string[]>): void {
    this.persistDrop(event, this.row4Order, 'piq.dashboard.main.row4Order');
  }
  assetStatusDonut: DashboardData['assetStatusDonut'] = { total: '0', totalLabel: 'Total', legend: [], segments: [] };
  assetsByTypeDonut: DashboardData['assetsByTypeDonut'] = { total: '0', totalLabel: 'Total', legend: [], segments: [] };
  recentAlerts: AlertItem[] = [];
  utilizationData: DashboardData['utilizationChart'] = {
    yAxisLabels: ['100%', '75%', '50%', '25%', '0%'],
    xAxisLabels: ['00:00', '04:00', '08:00', '12:00', '16:00', '20:00', '24:00'],
    peak: { value: '', x: 0, y: 0 },
    linePoints: '',
  };
  geofenceSummary: GeofenceItem[] = [];
  recentActivity: ActivityItem[] = [];
  topActiveAssets: ActiveAsset[] = [];
  topAssetsByDistance: DistanceAsset[] = [];

  // Popup (click on stat cards)
  isStatPopupOpen = false;
  statPopupTitle = '';
  statPopupRows: Array<Record<string, string | number>> = [];

  constructor(
    private cd: ChangeDetectorRef,
    private router: Router,
  ) {}
  ngOnInit(): void {
    // Stat cards keep their labels with zero values until the API answers; a failed call leaves its widget empty
    this.statDataIndex = new Map(STAT_CARD_SHELLS.map((c, i) => [c.label, i]));
    this.statCards = reorderByKey(STAT_CARD_SHELLS.map((c) => ({ ...c })), 'piq.dashboard.main.statOrder', (c) => c.label);
    this.cd.detectChanges();
    this.loadData();
  }

  private readonly api = inject(DashboardApi);
  private readonly widgetsApi = inject(ModuleWidgetsApi);
  private readonly destroyRef = inject(DestroyRef);

  /** Each widget's source; one failing call (null) blanks only its own widget. */
  private loadData(): void {
    const safe = <T>(source: Observable<T>) => source.pipe(catchError(() => of(null)));
    forkJoin({
      kpis: safe(this.api.getKpis(ALERT_DAYS)),
      locations: safe(this.api.getLocations(0)),
      statuses: safe(this.api.getStatusCounts(undefined, false)),
      categories: safe(this.api.getCategories()),
      alerts: safe(this.api.getAlerts(ALERT_DAYS, 5)),
      updates: safe(this.api.getLocationUpdates(1)),
      geofences: safe(this.widgetsApi.getGeofenceSummary()),
      realTime: safe(this.widgetsApi.getRealTimeOverview()),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(({ kpis, locations, statuses, categories, alerts, updates, geofences, realTime }) => {
        // Stat cards: total and alerts from the KPIs, network status per asset from the map legend
        const total = kpis?.totalAssets ?? statuses?.totalAssets ?? 0;
        const legend = locations?.legend ?? {};
        const share = (n: number) => (total ? `${Math.round((n / total) * 1000) / 10}% of total` : undefined);
        const values: Record<string, { value: number; subLabel?: string; badge?: string } | null> = {
          'Total Assets': kpis || statuses ? { value: total } : null,
          'Online / Moving': locations ? { value: legend['Online'] ?? 0, subLabel: share(legend['Online'] ?? 0) } : null,
          'Idle Assets': locations ? { value: legend['Idle'] ?? 0, subLabel: share(legend['Idle'] ?? 0) } : null,
          Offline: locations ? { value: legend['Offline'] ?? 0, subLabel: share(legend['Offline'] ?? 0) } : null,
          Alerts: kpis ? { value: kpis.alerts.value, badge: kpis.alerts.note } : null,
        };
        this.statCards = this.statCards.map((card) => {
          const v = values[card.label];
          return v ? { ...card, value: v.value.toLocaleString(), subLabel: v.subLabel, badge: v.badge ?? card.badge } : card;
        });

        if (statuses) {
          const donut = buildDonut(
            statuses.statuses.map((s, i) => ({ label: s.status, count: s.count, color: STATUS_COLORS[s.status] ?? PALETTE[i % PALETTE.length] })),
            statuses.totalAssets,
          );
          this.assetStatusDonut = { total: statuses.totalAssets.toLocaleString(), totalLabel: 'Total', ...donut };
        }
        if (categories) {
          const donut = buildDonut(
            categories.categories.map((c, i) => ({ label: c.category, count: c.count, color: PALETTE[i % PALETTE.length] })),
            categories.totalAssets,
          );
          this.assetsByTypeDonut = { total: categories.totalAssets.toLocaleString(), totalLabel: 'Total', ...donut };
        }

        this.recentAlerts = (alerts ?? []).map((a) => ({
          type: a.eventType || 'Alert',
          asset: [a.assetName || a.deviceId, a.siteName].filter(Boolean).join(' | '),
          time: timeAgo(a.raisedAt),
          severity: SEVERITY_COLORS[(a.severity ?? '').toLowerCase()] ?? 'gray',
        }));

        // Top Active Assets: tags that moved between gateways most often today
        const tags = [...(updates?.tags ?? [])].sort((a, b) => b.count - a.count).slice(0, 5);
        const maxMoves = tags[0]?.count || 1;
        this.topActiveAssets = tags.map((t, i) => ({
          assetId: t.tagId,
          route: [t.lastFromGatewayId, t.lastToGatewayId].filter(Boolean).join(' → '),
          speed: `${t.count} ${t.count === 1 ? 'move' : 'moves'}`,
          speedPercent: Math.round((t.count / maxMoves) * 100),
          thumbColor: PALETTE[i % PALETTE.length],
        }));

        this.geofenceSummary = [...(geofences?.targets ?? [])]
          .sort((a, b) => b.activeAlerts - a.activeAlerts || b.insideNow - a.insideNow)
          .slice(0, 5)
          .map((t) => ({ name: t.name, assetsInside: t.insideNow, alerts: t.activeAlerts }));

        const lastSeen = (a: { lastValidLocationAt: string | null; location: { at: string | null } | null }) =>
          Date.parse(a.lastValidLocationAt ?? a.location?.at ?? '') || 0;
        this.recentActivity = [...(realTime?.assets ?? [])]
          .sort((a, b) => lastSeen(b) - lastSeen(a))
          .slice(0, 5)
          .map((a) => {
            const status = a.movement === 'Moving' ? 'Moving' : a.movement === 'Stationary' ? 'Stopped' : a.freshness;
            return {
              assetId: a.assetTag || a.assetId,
              assetName: a.name,
              route: a.location?.locationPath ?? '',
              status,
              location: a.location?.zoneName ?? a.location?.floorName ?? a.location?.siteName ?? '—',
              speed: '—',
              lastUpdate: timeAgo(a.lastValidLocationAt ?? a.location?.at),
              live: a.freshness === 'Live',
              thumbColor: status === 'Moving' ? '#22c55e' : status === 'Stopped' ? '#3b82f6' : '#94a3b8',
            };
          });
        this.cd.detectChanges();
      });
  }

  getStatIconClass(index: number): string {
    const classes = ['icon-purple', 'icon-green', 'icon-orange', 'icon-red', 'icon-blue'];
    return classes[index] || 'icon-blue';
  }

  getAlertBgClass(severity: string): string {
    const map: Record<string, string> = {
      red: 'red-bg',
      orange: 'orange-bg',
      purple: 'purple-bg',
      yellow: 'yellow-bg',
      gray: 'gray-bg',
    };
    return map[severity] || 'gray-bg';
  }

  getAlertTextClass(severity: string): string {
    const map: Record<string, string> = {
      red: 'red-text',
      orange: 'orange-text',
      purple: 'purple-text',
      yellow: 'yellow-text',
      gray: 'gray-text',
    };
    return map[severity] || 'gray-text';
  }

  getRankClass(rank: number): string {
    const map: Record<number, string> = {
      1: 'gold',
      2: 'silver',
      3: 'bronze',
    };
    return map[rank] || '';
  }

  // Map stat card click -> popup rows (table)
  onStatCardClick(card: StatCard, index: number): void {
    this.isStatPopupOpen = true;
    this.statPopupTitle = card.label;

    switch (index) {
      case 0: // Total Assets
        this.statPopupRows = this.assetStatusDonut.legend.map((x) => ({
          Status: x.label,
          Value: x.value,
          Percentage: x.percentage,
        }));
        return;
      case 1: // Online / Moving
        this.statPopupRows = this.assetsByTypeDonut.legend.map((x) => ({
          Type: x.label,
          Value: x.value,
          Percentage: x.percentage,
        }));
        return;
      case 2: // Idle Assets
        this.statPopupRows = this.geofenceSummary.map((x) => ({
          Zone: x.name,
          AssetsInside: x.assetsInside,
          Alerts: x.alerts,
        }));
        return;
      case 3: // Offline
        this.statPopupRows = this.recentAlerts.map((a) => ({
          AlertType: a.type,
          Asset: a.asset,
          Time: a.time,
          Severity: a.severity,
        }));
        return;
      case 4: // Alerts
      default:
        this.statPopupRows = this.recentActivity.map((a) => ({
          AssetId: a.assetId,
          AssetName: a.assetName,
          Status: a.status,
          Location: a.location,
          Speed: a.speed,
          LastUpdate: a.lastUpdate,
          Live: a.live ? 'Yes' : 'No',
        }));
        return;
    }
  }

  closeStatPopup(): void {
    this.isStatPopupOpen = false;
    this.statPopupTitle = '';
    this.statPopupRows = [];
  }

  getPopupTableKeys(): string[] {
    const first = this.statPopupRows[0];
    if (!first) return [];
    return Object.keys(first);
  }

  /** Rows carrying a "Zone" column can be located on the live map; other stat breakdowns
   *  (e.g. donut legends, alert types) don't correspond to a real map zone. */
  rowZoneName(row: Record<string, string | number>): string | null {
    const value = row['Zone'];
    return typeof value === 'string' ? value : null;
  }

  hasLocatableRows(): boolean {
    return this.statPopupRows.some((row) => this.rowZoneName(row) !== null);
  }

  locate(zoneName: string): void {
    this.router.navigate(['/tracking-iot/real-time-location'], { queryParams: { zone: zoneName } });
  }
}
