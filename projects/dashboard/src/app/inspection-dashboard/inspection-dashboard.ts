import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DragDropModule, CdkDragDrop } from '@angular/cdk/drag-drop';
import { WidgetDragHandle } from '../shared/widget-drag-handle/widget-drag-handle';
import { loadOrder, saveOrder, reorderByKey, moveWithinVisible } from '../shared/dashboard-widgets/widget-order.util';

export type WorkOrderFilter = 'all' | 'inprogress' | 'pendingapproval' | 'completed' | 'overdue';

interface InspStatCard {
  key: WorkOrderFilter;
  label: string;
  value: number;
  icon: string;
}

interface InspWorkOrder {
  id: string;
  title: string;
  assetsCount: number;
  assignees: string[];
  status: string;
  statusClass: WorkOrderFilter;
  dueDate: string;
}

interface MyInspectionRow {
  key: string;
  label: string;
  count: number;
  icon: string;
  statusClass: string;
}

interface RecentInspectionRow {
  id: string;
  title: string;
  asset: string;
  inspectedBy: string;
  status: string;
  statusClass: string;
  time: string;
}

interface InspectionSummarySegment {
  label: string;
  value: number;
  percent: number;
  color: string;
}

interface ApprovalSummaryRow {
  label: string;
  pending: number;
  icon: string;
}

interface QuickAction {
  key: string;
  label: string;
  icon: string;
}

interface PopupColumn {
  key: string;
  label: string;
}

interface PopupRow {
  [key: string]: string | number;
}

interface InspPopup {
  title: string;
  summary: string;
  icon: string;
  iconClass: string;
  columns: PopupColumn[];
  rows: PopupRow[];
}

@Component({
  standalone: true,
  selector: 'app-inspection-dashboard',
  imports: [CommonModule, DragDropModule, WidgetDragHandle],
  templateUrl: './inspection-dashboard.html',
  styleUrls: ['./inspection-dashboard.css'],
})
export class InspectionDashboard {
  /** Widget ids (see WIDGET_CATALOG 'inspection') the user chose to show. */
  @Input() selected: string[] = [];

  // ===== Widget drag-and-drop ordering =====
  readonly rowAOrder: string[] = loadOrder('piq.dashboard.inspection.rowAOrder', ['workOrders', 'myInspections']);
  readonly rowBOrder: string[] = loadOrder('piq.dashboard.inspection.rowBOrder', ['recent', 'summary', 'approval']);

  trackByWidgetId = (_: number, id: string) => id;

  has(id: string): boolean {
    return this.selected.includes(id);
  }

  visible(order: string[]): string[] {
    return order.filter((id) => this.has(id));
  }

  get visibleStatCards(): InspStatCard[] {
    return this.statCards.filter((c) => this.has('stat:' + c.key));
  }

  onStatCardDrop(event: CdkDragDrop<InspStatCard[]>): void {
    moveWithinVisible(this.statCards, (c) => this.has('stat:' + c.key), event.previousIndex, event.currentIndex);
    saveOrder('piq.dashboard.inspection.statOrder', this.statCards.map((c) => c.key));
  }

  onRowADrop(event: CdkDragDrop<string[]>): void {
    moveWithinVisible(this.rowAOrder, (id) => this.has(id), event.previousIndex, event.currentIndex);
    saveOrder('piq.dashboard.inspection.rowAOrder', this.rowAOrder);
  }

  onRowBDrop(event: CdkDragDrop<string[]>): void {
    moveWithinVisible(this.rowBOrder, (id) => this.has(id), event.previousIndex, event.currentIndex);
    saveOrder('piq.dashboard.inspection.rowBOrder', this.rowBOrder);
  }

  // ===== Summary cards =====
  statCards: InspStatCard[] = reorderByKey<InspStatCard>([
    { key: 'all', label: 'Total Work Orders', value: 128, icon: 'clipboard' },
    { key: 'inprogress', label: 'In Progress', value: 45, icon: 'progress' },
    { key: 'pendingapproval', label: 'Pending Approval', value: 23, icon: 'clock' },
    { key: 'completed', label: 'Completed', value: 60, icon: 'completed' },
    { key: 'overdue', label: 'Overdue', value: 12, icon: 'overdue' },
  ], 'piq.dashboard.inspection.statOrder', (c) => c.key);

  private readonly iconClassMap: Record<string, string> = {
    clipboard: 'icon-blue',
    progress: 'icon-orange',
    clock: 'icon-purple',
    completed: 'icon-green',
    overdue: 'icon-red',
  };

  getIconClass(icon: string): string {
    return this.iconClassMap[icon] || 'icon-purple';
  }

  // ===== Work orders table =====
  workOrderFilter: WorkOrderFilter = 'all';

  readonly workOrderFilters: { key: WorkOrderFilter; label: string }[] = [
    { key: 'all', label: 'All' },
    { key: 'inprogress', label: 'In Progress' },
    { key: 'pendingapproval', label: 'Pending Approval' },
    { key: 'completed', label: 'Completed' },
    { key: 'overdue', label: 'Overdue' },
  ];

  setWorkOrderFilter(filter: WorkOrderFilter): void {
    this.workOrderFilter = filter;
  }

  workOrders: InspWorkOrder[] = [
    { id: 'WO-2025-00128', title: 'Boiler Inspection', assetsCount: 3, assignees: ['Michael Brown', 'David Smith', 'Sarah Wilson', 'Anita Rao'], status: 'In Progress', statusClass: 'inprogress', dueDate: '20 May 2025' },
    { id: 'WO-2025-00127', title: 'Generator Inspection', assetsCount: 2, assignees: ['David Smith', 'Sarah Wilson', 'Anita Rao'], status: 'Pending Approval', statusClass: 'pendingapproval', dueDate: '18 May 2025' },
    { id: 'WO-2025-00126', title: 'HVAC System Check', assetsCount: 5, assignees: ['Sarah Wilson', 'Michael Brown'], status: 'Completed', statusClass: 'completed', dueDate: '15 May 2025' },
    { id: 'WO-2025-00125', title: 'Annual Safety Inspection', assetsCount: 10, assignees: ['Michael Brown', 'David Smith', 'Sarah Wilson', 'Anita Rao', 'Chen Wei'], status: 'In Progress', statusClass: 'inprogress', dueDate: '25 May 2025' },
    { id: 'WO-2025-00124', title: 'Fire Extinguisher Check', assetsCount: 7, assignees: ['David Smith', 'Anita Rao'], status: 'Overdue', statusClass: 'overdue', dueDate: '10 May 2025' },
  ];

  get filteredWorkOrders(): InspWorkOrder[] {
    if (this.workOrderFilter === 'all') return this.workOrders;
    return this.workOrders.filter((wo) => wo.statusClass === this.workOrderFilter);
  }

  private readonly avatarPalette = ['#4f2bd8', '#2563eb', '#22a06b', '#f0a93b', '#e15252', '#0891b2'];

  avatarColor(index: number): string {
    return this.avatarPalette[index % this.avatarPalette.length];
  }

  initials(name: string): string {
    return name.split(' ').map((part) => part[0]).slice(0, 2).join('').toUpperCase();
  }

  // ===== My inspections =====
  myInspections: MyInspectionRow[] = [
    { key: 'notstarted', label: 'Not Started', count: 12, icon: 'notstarted', statusClass: 'notstarted' },
    { key: 'inprogress', label: 'In Progress', count: 8, icon: 'progress', statusClass: 'inprogress' },
    { key: 'pendingapproval', label: 'Pending Approval', count: 5, icon: 'clock', statusClass: 'pendingapproval' },
    { key: 'completed', label: 'Completed', count: 20, icon: 'completed', statusClass: 'completed' },
  ];

  // ===== Recent inspections =====
  recentInspections: RecentInspectionRow[] = [
    { id: 'WO-2025-00127', title: 'Generator Inspection', asset: 'Generator G-01', inspectedBy: 'Michael Brown', status: 'Pending Approval', statusClass: 'pendingapproval', time: '2 hours ago' },
    { id: 'WO-2025-00128', title: 'Boiler Inspection', asset: 'Boiler B-02', inspectedBy: 'David Smith', status: 'In Progress', statusClass: 'inprogress', time: '5 hours ago' },
    { id: 'WO-2025-00126', title: 'HVAC System Check', asset: 'HVAC Unit 3A', inspectedBy: 'Sarah Wilson', status: 'Completed', statusClass: 'completed', time: '1 day ago' },
  ];

  // ===== Inspection summary donut =====
  inspectionSummaryTotal = 128;

  inspectionSummary: InspectionSummarySegment[] = [
    { label: 'Passed', value: 68, percent: 53, color: '#22a06b' },
    { label: 'Failed', value: 25, percent: 20, color: '#e15252' },
    { label: 'In Progress', value: 23, percent: 18, color: '#f0a93b' },
    { label: 'Pending Approval', value: 12, percent: 9, color: '#4f2bd8' },
  ];

  get donutGradient(): string {
    let cursor = 0;
    const stops = this.inspectionSummary.map((seg) => {
      const start = cursor;
      cursor += seg.percent;
      return `${seg.color} ${start}% ${cursor}%`;
    });
    return `conic-gradient(${stops.join(', ')})`;
  }

  // ===== Approval summary =====
  approvalSummary: ApprovalSummaryRow[] = [
    { label: 'Level 1 Approval', pending: 18, icon: 'user' },
    { label: 'Level 2 Approval', pending: 7, icon: 'user' },
    { label: 'Level 3 Approval', pending: 3, icon: 'user' },
    { label: 'Final Approval', pending: 2, icon: 'user' },
  ];

  // ===== Quick actions =====
  quickActions: QuickAction[] = [
    { key: 'add-asset', label: 'Add New Asset', icon: 'asset' },
    { key: 'create-work-order', label: 'Create Work Order', icon: 'work-order' },
    { key: 'create-task', label: 'Create Inspection Task', icon: 'task' },
    { key: 'upload-signature', label: 'Upload Signature/Stamp', icon: 'signature' },
    { key: 'generate-report', label: 'Generate Report', icon: 'report' },
  ];

  onQuickAction(action: QuickAction): void {
    // TODO: wire each action up to its real destination once those pages exist
    console.log('Quick action:', action.key);
  }

  // ===== "View all" popups =====
  popup: InspPopup | null = null;

  private readonly workOrderColumns: PopupColumn[] = [
    { key: 'id', label: 'WO No.' },
    { key: 'title', label: 'Title' },
    { key: 'assetsCount', label: 'Assets' },
    { key: 'assignedTo', label: 'Assigned To' },
    { key: 'status', label: 'Status' },
    { key: 'dueDate', label: 'Due Date' },
  ];

  private workOrderRows(list: InspWorkOrder[]): PopupRow[] {
    return list.map((wo) => ({
      id: wo.id,
      title: wo.title,
      assetsCount: wo.assetsCount,
      assignedTo: wo.assignees.join(', '),
      status: wo.status,
      dueDate: wo.dueDate,
    }));
  }

  openStatPopup(card: InspStatCard): void {
    const list = card.key === 'all' ? this.workOrders : this.workOrders.filter((wo) => wo.statusClass === card.key);
    this.popup = {
      title: card.label,
      summary: `${list.length} work order${list.length === 1 ? '' : 's'}`,
      icon: card.icon,
      iconClass: this.getIconClass(card.icon),
      columns: this.workOrderColumns,
      rows: this.workOrderRows(list),
    };
  }

  openWorkOrdersPopup(): void {
    this.popup = {
      title: 'All Work Orders',
      summary: `${this.workOrders.length} work orders`,
      icon: 'clipboard',
      iconClass: this.getIconClass('clipboard'),
      columns: this.workOrderColumns,
      rows: this.workOrderRows(this.workOrders),
    };
  }

  private readonly myInspectionDetails: Record<string, { workOrder: string; asset: string; dueDate: string }[]> = {
    notstarted: [
      { workOrder: 'WO-2025-00131', asset: 'Chiller Pump 2', dueDate: '28 May 2025' },
      { workOrder: 'WO-2025-00132', asset: 'Water Tank 1', dueDate: '29 May 2025' },
      { workOrder: 'WO-2025-00133', asset: 'Loading Dock Lift', dueDate: '30 May 2025' },
    ],
    inprogress: [
      { workOrder: 'WO-2025-00128', asset: 'Boiler B-02', dueDate: '20 May 2025' },
      { workOrder: 'WO-2025-00125', asset: 'Site-wide Safety', dueDate: '25 May 2025' },
    ],
    pendingapproval: [
      { workOrder: 'WO-2025-00127', asset: 'Generator G-01', dueDate: '18 May 2025' },
    ],
    completed: [
      { workOrder: 'WO-2025-00126', asset: 'HVAC Unit 3A', dueDate: '15 May 2025' },
      { workOrder: 'WO-2025-00120', asset: 'Fire Panel A', dueDate: '10 May 2025' },
    ],
  };

  private readonly myInspectionColumns: PopupColumn[] = [
    { key: 'workOrder', label: 'Work Order' },
    { key: 'asset', label: 'Asset' },
    { key: 'dueDate', label: 'Due Date' },
  ];

  openMyInspectionRowPopup(row: MyInspectionRow): void {
    const rows = (this.myInspectionDetails[row.key] || []) as unknown as PopupRow[];
    this.popup = {
      title: row.label,
      summary: `${row.count} inspection${row.count === 1 ? '' : 's'}`,
      icon: row.icon,
      iconClass: `insp-status-icon-${row.statusClass}`,
      columns: this.myInspectionColumns,
      rows,
    };
  }

  openMyInspectionsAllPopup(): void {
    const columns: PopupColumn[] = [{ key: 'status', label: 'Status' }, ...this.myInspectionColumns];
    const rows: PopupRow[] = this.myInspections.flatMap((row) =>
      (this.myInspectionDetails[row.key] || []).map((detail) => ({ status: row.label, ...detail }))
    );
    this.popup = {
      title: 'My Inspections',
      summary: `${rows.length} inspections`,
      icon: 'clipboard',
      iconClass: this.getIconClass('clipboard'),
      columns,
      rows,
    };
  }

  openRecentInspectionsPopup(): void {
    this.popup = {
      title: 'Recent Inspections',
      summary: `${this.recentInspections.length} inspections`,
      icon: 'clock',
      iconClass: this.getIconClass('clock'),
      columns: [
        { key: 'id', label: 'WO No.' },
        { key: 'title', label: 'Title' },
        { key: 'asset', label: 'Asset' },
        { key: 'inspectedBy', label: 'Inspected By' },
        { key: 'status', label: 'Status' },
        { key: 'time', label: 'Updated' },
      ],
      rows: this.recentInspections.map((insp) => ({ ...insp })),
    };
  }

  private readonly approvalDetails: Record<string, { workOrder: string; title: string; submittedBy: string; submittedDate: string }[]> = {
    'Level 1 Approval': [
      { workOrder: 'WO-2025-00127', title: 'Generator Inspection', submittedBy: 'Michael Brown', submittedDate: '19 May 2025' },
      { workOrder: 'WO-2025-00129', title: 'Elevator Inspection', submittedBy: 'David Smith', submittedDate: '19 May 2025' },
    ],
    'Level 2 Approval': [
      { workOrder: 'WO-2025-00121', title: 'Fire Panel Check', submittedBy: 'Sarah Wilson', submittedDate: '17 May 2025' },
    ],
    'Level 3 Approval': [
      { workOrder: 'WO-2025-00118', title: 'Structural Audit', submittedBy: 'Anita Rao', submittedDate: '14 May 2025' },
    ],
    'Final Approval': [
      { workOrder: 'WO-2025-00115', title: 'Annual Compliance Review', submittedBy: 'Chen Wei', submittedDate: '10 May 2025' },
    ],
  };

  private readonly approvalColumns: PopupColumn[] = [
    { key: 'workOrder', label: 'Work Order' },
    { key: 'title', label: 'Title' },
    { key: 'submittedBy', label: 'Submitted By' },
    { key: 'submittedDate', label: 'Submitted' },
  ];

  openApprovalRowPopup(row: ApprovalSummaryRow): void {
    const rows = (this.approvalDetails[row.label] || []) as unknown as PopupRow[];
    this.popup = {
      title: row.label,
      summary: `${row.pending} pending`,
      icon: 'user',
      iconClass: 'icon-purple',
      columns: this.approvalColumns,
      rows,
    };
  }

  openApprovalAllPopup(): void {
    const columns: PopupColumn[] = [{ key: 'level', label: 'Level' }, ...this.approvalColumns];
    const rows: PopupRow[] = this.approvalSummary.flatMap((row) =>
      (this.approvalDetails[row.label] || []).map((detail) => ({ level: row.label, ...detail }))
    );
    this.popup = {
      title: 'Approval Summary',
      summary: `${rows.length} pending approvals`,
      icon: 'user',
      iconClass: 'icon-purple',
      columns,
      rows,
    };
  }

  closePopup(): void {
    this.popup = null;
  }
}
