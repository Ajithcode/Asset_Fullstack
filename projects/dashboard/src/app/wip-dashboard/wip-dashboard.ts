import { Component, DestroyRef, Input, OnInit, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, catchError, forkJoin, of } from 'rxjs';
import { CommonModule } from '@angular/common';
import { DragDropModule, CdkDragDrop } from '@angular/cdk/drag-drop';
import { WidgetDragHandle } from '../shared/widget-drag-handle/widget-drag-handle';
import { loadOrder, saveOrder, reorderByKey, moveWithinVisible } from '../shared/dashboard-widgets/widget-order.util';
import { ModuleWidgetsApi, shortDate } from '../services/dashboard/module-widgets';

const STATUS_COLORS: Record<string, string> = {
  'Not Started': '#f0a93b',
  'In Progress': '#4f2bd8',
  'On Hold': '#94a3b8',
  Completed: '#22a06b',
  Cancelled: '#e15252',
};
const STATUS_CLASSES: Record<string, string> = { 'In Progress': 'inprogress', 'Not Started': 'planned', 'On Hold': 'planned', Completed: 'completed' };

interface WipStatCard {
  label: string;
  value: number;
  icon: string;
  trend: 'up' | 'down' | 'neutral';
  delta: string;
}

interface WipStatusDistribution {
  label: string;
  percent: number;
  color: string;
}

interface WipAlert {
  title: string;
  subtitle: string;
  time: string;
  type: 'warning' | 'danger';
}

interface WipActiveJob {
  id: string;
  asset: string;
  status: string;
  statusClass: string;
  progress: number;
  assignedTo: string;
  eta: string;
}

@Component({
  standalone: true,
  selector: 'app-wip-dashboard',
  imports: [CommonModule, DragDropModule, WidgetDragHandle],
  templateUrl: './wip-dashboard.html',
  styleUrls: ['./wip-dashboard.css'],
})
export class WipDashboard implements OnInit {
  /** Widget ids (see WIDGET_CATALOG 'wip') the user chose to show. */
  @Input() selected: string[] = [];

  // ===== Widget drag-and-drop ordering =====
  readonly midOrder: string[] = loadOrder('piq.dashboard.wip.midOrder', ['donut', 'trend']);
  readonly bottomOrder: string[] = loadOrder('piq.dashboard.wip.bottomOrder', ['alerts', 'jobs']);

  trackByWidgetId = (_: number, id: string) => id;

  has(id: string): boolean {
    return this.selected.includes(id);
  }

  visible(order: string[]): string[] {
    return order.filter((id) => this.has(id));
  }

  get visibleStatCards(): WipStatCard[] {
    return this.wipStatCards.filter((c) => this.has('stat:' + c.label));
  }

  onStatCardDrop(event: CdkDragDrop<WipStatCard[]>): void {
    moveWithinVisible(this.wipStatCards, (c) => this.has('stat:' + c.label), event.previousIndex, event.currentIndex);
    saveOrder('piq.dashboard.wip.statOrder', this.wipStatCards.map((c) => c.label));
  }

  onMidDrop(event: CdkDragDrop<string[]>): void {
    moveWithinVisible(this.midOrder, (id) => this.has(id), event.previousIndex, event.currentIndex);
    saveOrder('piq.dashboard.wip.midOrder', this.midOrder);
  }

  onBottomDrop(event: CdkDragDrop<string[]>): void {
    moveWithinVisible(this.bottomOrder, (id) => this.has(id), event.previousIndex, event.currentIndex);
    saveOrder('piq.dashboard.wip.bottomOrder', this.bottomOrder);
  }

  /** Stat card layout (labels, icons); values come from loadData(). */
  wipStatCards: WipStatCard[] = reorderByKey([
    { label: 'Total Jobs', value: 0, icon: 'jobs', trend: 'neutral', delta: '' },
    { label: 'In Progress', value: 0, icon: 'progress', trend: 'neutral', delta: '' },
    { label: 'Completed today', value: 0, icon: 'completed', trend: 'neutral', delta: '' },
    { label: 'SLA Breached', value: 0, icon: 'sla', trend: 'neutral', delta: '' },
    { label: 'Planned', value: 0, icon: 'planned', trend: 'neutral', delta: '' },
  ], 'piq.dashboard.wip.statOrder', (c) => c.label);

  wipTotalJobs = 0;

  private readonly iconClassMap: Record<string, string> = {
    jobs: 'icon-purple',
    progress: 'icon-blue',
    completed: 'icon-green',
    sla: 'icon-red',
    planned: 'icon-orange',
  };

  getIconClass(icon: string): string {
    return this.iconClassMap[icon] || 'icon-purple';
  }

  wipStatusDistribution: WipStatusDistribution[] = [];

  /** Completion trend as SVG points in the 420x180 chart (y = 0 is 100%). */
  wipTrendPoints: { x: number; y: number }[] = [];
  /** Caption under the trend chart. */
  trendLabel = 'Monthly, this year';

  wipAlerts: WipAlert[] = [];

  wipActiveJobs: WipActiveJob[] = [];

  /** Donut segments from the distribution; grey ring when there is no data. */
  get donutGradient(): string {
    let acc = 0;
    const stops = this.wipStatusDistribution.map((s) => {
      const stop = `${s.color} ${acc}% ${acc + s.percent}%`;
      acc += s.percent;
      return stop;
    });
    return acc > 0 ? `conic-gradient(${stops.join(', ')}, #ede9f4 ${acc}% 100%)` : '#ede9f4';
  }

  get trendLine(): string {
    return this.wipTrendPoints.map((p, i) => `${i ? 'L' : 'M'}${p.x},${p.y}`).join(' ');
  }

  get trendArea(): string {
    const pts = this.wipTrendPoints;
    return pts.length ? `${this.trendLine} L${pts[pts.length - 1].x},180 L${pts[0].x},180 Z` : '';
  }

  private readonly api = inject(ModuleWidgetsApi);
  private readonly destroyRef = inject(DestroyRef);

  ngOnInit(): void {
    this.loadData();
  }

  /**
   * Production analytics (Report Analytics service; WIP jobs are its production orders). Each call fails on its own,
   * blanking only its widgets.
   */
  private loadData(): void {
    const safe = <T>(source: Observable<T>) => source.pipe(catchError(() => of(null)));
    forkJoin({
      status: safe(this.api.getProductionStatus()),
      today: safe(this.api.getProductionSummary('Today')),
      delayed: safe(this.api.getProductionOrders(1, 'Delayed')),
      trend: safe(this.api.getProductionOutputTrend()),
      insights: safe(this.api.getProductionInsights()),
      orders: safe(this.api.getProductionOrders(5)),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(({ status, today, delayed, trend, insights, orders }) => {
        const count = (label: string) => status?.data.items.find((i) => i.label === label)?.count ?? 0;
        const output = today?.data.productionOutput;
        const cards: Record<string, Partial<WipStatCard> | null> = {
          'Total Jobs': status ? { value: status.data.total } : null,
          'In Progress': status ? { value: count('In Progress') } : null,
          'Completed today': output
            ? {
                value: output.value ?? 0,
                trend: output.change === null ? 'neutral' : output.change < 0 ? 'down' : 'up',
                delta: output.change === null ? '' : `${Math.abs(output.change)}%`,
              }
            : null,
          'SLA Breached': delayed ? { value: delayed.totalCount } : null,
          Planned: status ? { value: count('Not Started') } : null,
        };
        this.wipStatCards = this.wipStatCards.map((c) => (cards[c.label] ? { ...c, ...cards[c.label] } : c));

        if (status) {
          this.wipTotalJobs = status.data.total;
          this.wipStatusDistribution = status.data.items
            .filter((i) => i.count > 0)
            .map((i) => ({ label: i.label, percent: i.percentage, color: STATUS_COLORS[i.label] ?? '#94a3b8' }));
        }

        // Completion % per month: output completed against output planned (orders due that month)
        const actual = trend?.data.series.find((s) => s.name === 'Actual')?.values ?? [];
        const planned = trend?.data.series.find((s) => s.name === 'Planned')?.values ?? [];
        const months = (trend?.data.categories ?? []).map((_, i) => {
          const a = actual[i] ?? 0;
          const p = planned[i] ?? 0;
          return p > 0 ? Math.min(100, (a / p) * 100) : a > 0 ? 100 : 0;
        }).slice(-7);
        const hasOutput = actual.some((v) => (v ?? 0) > 0) || planned.some((v) => (v ?? 0) > 0);
        this.wipTrendPoints = hasOutput
          ? months.map((pct, i) => ({ x: months.length > 1 ? Math.round(10 + (i * 400) / (months.length - 1)) : 210, y: Math.round(180 - pct * 1.8) }))
          : [];
        if (trend) this.trendLabel = `Monthly, ${trend.period.preset.toLowerCase()}`;

        this.wipAlerts = (insights?.data ?? [])
          .filter((i) => i.severity !== 'success' && i.count > 0)
          .map((i) => ({ title: i.title, subtitle: i.message, time: '', type: i.severity === 'danger' || i.severity === 'critical' ? 'danger' : 'warning' }));

        this.wipActiveJobs = (orders?.items ?? []).map((o) => {
          const late = o.schedule === 'Delayed';
          return {
            id: o.orderNo ?? o.id,
            asset: o.assetId ?? o.workCenter,
            status: late ? 'Delayed' : o.status,
            statusClass: late ? 'delayed' : STATUS_CLASSES[o.status] ?? 'planned',
            progress: Math.round(o.progress),
            assignedTo: '—',
            eta: shortDate(o.dueDate),
          };
        });
      });
  }
}
