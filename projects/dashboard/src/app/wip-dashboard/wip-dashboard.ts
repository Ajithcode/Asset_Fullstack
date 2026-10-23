import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DragDropModule, CdkDragDrop } from '@angular/cdk/drag-drop';
import { WidgetDragHandle } from '../shared/widget-drag-handle/widget-drag-handle';
import { loadOrder, saveOrder, reorderByKey, moveWithinVisible } from '../shared/dashboard-widgets/widget-order.util';

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
export class WipDashboard {
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

  wipStatCards: WipStatCard[] = reorderByKey([
    {
      label: 'Total Jobs',
      value: 182,
      icon: 'jobs',
      trend: 'up',
      delta: '+22',
    },
    {
      label: 'In Progress',
      value: 63,
      icon: 'progress',
      trend: 'up',
      delta: '+11',
    },
    {
      label: 'Completed today',
      value: 18,
      icon: 'completed',
      trend: 'up',
      delta: '+2',
    },
    {
      label: 'SLA Breached',
      value: 25,
      icon: 'sla',
      trend: 'down',
      delta: '+7',
    },
    {
      label: 'Planned',
      value: 36,
      icon: 'planned',
      trend: 'up',
      delta: '+4',
    },
  ], 'piq.dashboard.wip.statOrder', (c) => c.label);

  wipTotalJobs = 182;

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

  wipStatusDistribution: WipStatusDistribution[] = [
    { label: 'In Progress', percent: 35, color: '#e391c9' },
    { label: 'Planned',     percent: 20, color: '#4f2bd8' },
    { label: 'Completed',   percent: 10, color: '#a8d5a2' },
    { label: 'Delayed',     percent: 35, color: '#f08080' },
  ];

  wipAlerts: WipAlert[] = [
    {
      title: 'Production delay on WIP-1001',
      subtitle: 'Paint task',
      time: '10 mins ago',
      type: 'warning',
    },
    {
      title: 'Critical situation on WIP-1003',
      subtitle: '',
      time: '15 mins ago',
      type: 'danger',
    },
    {
      title: 'SLA Breach on WIP-1002',
      subtitle: 'calibration match',
      time: '10 mins ago',
      type: 'warning',
    },
  ];

  wipActiveJobs: WipActiveJob[] = [
    {
      id: 'WIP-1001',
      asset: 'Employee',
      status: 'In Progress',
      statusClass: 'inprogress',
      progress: 35,
      assignedTo: 'emp01@fmail.com',
      eta: 'Apr 25, 12:00',
    },
    {
      id: 'WIP-1002',
      asset: 'Contractor',
      status: 'Planned',
      statusClass: 'planned',
      progress: 20,
      assignedTo: 'emp02@fmail.com',
      eta: 'Apr 25, 12:00',
    },
    {
      id: 'WIP-1003',
      asset: 'Visitor',
      status: 'Completed',
      statusClass: 'completed',
      progress: 10,
      assignedTo: 'emp03@fmail.com',
      eta: 'Apr 25, 12:00',
    },
    {
      id: 'WIP-1004',
      asset: 'Other',
      status: 'Delayed',
      statusClass: 'delayed',
      progress: 35,
      assignedTo: 'emp04@fmail.com',
      eta: 'Apr 25, 12:00',
    },
  ];
}