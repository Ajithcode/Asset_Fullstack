import { Component, HostListener, OnInit, inject } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { Subscription } from 'rxjs';
import {
  Calendar, ChartColumn, Check, ChevronDown, CirclePlus, Clock, Download, Ellipsis, FileText, GitCompareArrows, GripVertical, Info,
  LucideAngularModule, Pencil, RefreshCw, Share2, Star, User, Users, X
} from 'lucide-angular';
import { AnalyticsPage, CURRENT_USER, downloadCsv } from '../shared/analytics-common';
import { DashboardStore } from './dashboard-store.service';
import { DashboardWidget } from './dashboard-widget/dashboard-widget';
import { AddWidgetDialog, NewWidget } from './dialogs/add-widget-dialog';
import { CreateDashboardDialog, NewDashboard } from './dialogs/create-dashboard-dialog';
import { ShareDashboardDialog, ShareResult } from './dialogs/share-dashboard-dialog';
import {
  AttentionAsset, DashFilters, Dashboard, WidgetConfig, WidgetData, WidgetSpan, WidgetType, widgetCsv
} from './interactive-dashboards.data';
import { LIBRARY_DRAG_TYPE, WidgetLibrary } from './widget-library/widget-library';

type MenuKey = 'picker' | 'mine' | 'shared' | 'favorites' | 'more' | 'info';

const SPAN_STEPS: WidgetSpan[] = [3, 4, 5, 6, 7, 8, 12];
const EMPTY: WidgetData = { empty: true };
const LOADING: WidgetData = { empty: true, message: 'Loading…' };

/** Placeholder shown while the user has no dashboards (never saved). */
function noDashboard(): Dashboard {
  return {
    id: '', name: 'No dashboards', description: '', owner: CURRENT_USER.name, sharing: 'private', sharedWith: [], groups: [],
    role: 'Owner', favorite: false, updatedAt: '—', widgets: [], widgetCount: 0, canShare: false, defaultFilters: null
  };
}

function apiMessage(err: HttpErrorResponse, fallback: string): string {
  if (err.status === 0) return `${fallback} The Report Analytics service is not reachable.`;
  if (err.status === 403) return 'You do not have permission for this dashboard.';
  const body = err.error as { title?: string; errors?: Record<string, string[]> } | null;
  const first = body && typeof body === 'object' && body.errors ? Object.values(body.errors).flat()[0] : '';
  return first || (body && typeof body === 'object' && body.title) || fallback;
}

/** Reports & Analytics > Interactive Dashboards (Report Analytics API: /api/interactive-dashboards) */
@Component({
  selector: 'app-ra-interactive-dashboards',
  standalone: true,
  imports: [FormsModule, RouterLink, NgTemplateOutlet, LucideAngularModule, DashboardWidget, WidgetLibrary, AddWidgetDialog, CreateDashboardDialog, ShareDashboardDialog],
  templateUrl: './interactive-dashboards.html',
  styleUrls: ['../shared/analytics-page.css', './interactive-dashboards.css']
})
export class InteractiveDashboardsPage extends AnalyticsPage implements OnInit {
  readonly store = inject(DashboardStore);
  private readonly route = inject(ActivatedRoute);

  readonly icons = {
    Calendar, ChartColumn, Check, ChevronDown, CirclePlus, Clock, Download, Ellipsis, FileText, GitCompareArrows, GripVertical, Info,
    Pencil, RefreshCw, Share2, Star, User, Users, X
  };

  /** Filter bar values: GET /options date presets and GET /filters */
  datePresets: string[] = [];
  orgOptions: { value: string; label: string }[] = [];
  locationOptions: { value: string; label: string }[] = [];
  categoryOptions: { value: string; label: string }[] = [];
  private defaultPreset = '';

  category = '';
  compare = false;
  filters: DashFilters = this.buildFilters();
  current: Dashboard = noDashboard();
  /** Widget figures by widget id (GET /{id}/data) */
  widgetData: Record<string, WidgetData> = {};
  loadingData = false;

  // Edit mode: drag to reorder and resize (saved with PUT /layout); adds and removes are saved straight away.
  // "Discard changes" puts the snapshot back with PUT /{id}.
  editMode = false;
  private snapshot: WidgetConfig[] | null = null;
  dragId: string | null = null;
  overIndex: number | null = null;

  menu: MenuKey | null = null;
  addDialog: { type: WidgetType | ''; index: number } | null = null;
  createOpen = false;
  shareOpen = false;
  expanded: WidgetConfig | null = null;
  detail: AttentionAsset | null = null;
  saveViewOpen = false;
  viewName = '';
  viewTried = false;

  private dataSub: Subscription | null = null;

  ngOnInit(): void {
    this.store.loadOptions().subscribe({
      next: (o) => {
        this.datePresets = o.datePresets;
        this.defaultPreset = o.defaultDatePreset;
        this.orgOptions = o.organizations;
        this.locationOptions = o.locations;
        this.categoryOptions = o.categories;
      },
      error: (err: HttpErrorResponse) => this.notify(apiMessage(err, 'Could not load the dashboard options.'))
    });
    this.store.loadList().subscribe({
      next: (list) => {
        const requested = this.route.snapshot.queryParamMap.get('dashboard');
        const pick = (requested && list.find((d) => d.id === requested)) || list[0];
        if (pick) this.openById(pick.id);
      },
      error: (err: HttpErrorResponse) => this.notify(apiMessage(err, 'Could not load dashboards.'))
    });
  }

  override ngOnDestroy(): void {
    super.ngOnDestroy();
    this.dataSub?.unsubscribe();
  }

  /** Period caption: the selected preset or the API default. */
  override get periodLabel(): string {
    return this.period || this.defaultPreset || 'This month';
  }

  override get organizationLabel(): string {
    return this.orgOptions.find((o) => o.value === this.organization)?.label || 'All organizations';
  }

  override get locationLabel(): string {
    return this.locationOptions.find((o) => o.value === this.location)?.label || 'All locations';
  }

  get categoryLabel(): string {
    return this.categoryOptions.find((o) => o.value === this.category)?.label || 'All categories';
  }

  /** False while the placeholder is shown (no saved dashboards yet) */
  get hasDashboard(): boolean {
    return !!this.current.id;
  }

  get canEdit(): boolean {
    return this.hasDashboard && this.current.role !== 'Viewer';
  }

  get myDashboards(): Dashboard[] {
    return this.store.dashboards.filter((d) => d.role === 'Owner');
  }

  get sharedWithMe(): Dashboard[] {
    return this.store.dashboards.filter((d) => d.role !== 'Owner');
  }

  get favorites(): Dashboard[] {
    return this.store.dashboards.filter((d) => d.favorite);
  }

  get sharedText(): string {
    const d = this.current;
    if (d.sharing === 'organization') return 'Everyone in the organization';
    const users = d.sharedWith.length;
    const groups = d.groups.length;
    if (!users && !groups) return 'Only you';
    const parts = [];
    if (users) parts.push(`${users} ${users === 1 ? 'user' : 'users'}`);
    if (groups) parts.push(`${groups} ${groups === 1 ? 'group' : 'groups'}`);
    return parts.join(' and ');
  }

  get shareLink(): string {
    const origin = typeof location !== 'undefined' ? location.origin : '';
    return `${origin}${this.basePath}/interactive-dashboards?dashboard=${encodeURIComponent(this.current.id)}`;
  }

  dataFor(w: WidgetConfig): WidgetData {
    return this.widgetData[w.id] ?? (this.loadingData ? LOADING : EMPTY);
  }

  // ===== Filters / data =====

  private buildFilters(): DashFilters {
    return { period: this.period, organization: this.organization, location: this.location, category: this.category, compare: this.compare };
  }

  /** GET /{id}/data for the filter bar. */
  private loadData(): void {
    this.dataSub?.unsubscribe();
    if (!this.hasDashboard || !this.current.widgets.length) {
      this.widgetData = {};
      return;
    }
    this.loadingData = true;
    this.dataSub = this.store.data(this.current, this.filters).subscribe({
      next: (data) => {
        this.loadingData = false;
        this.widgetData = data;
        this.updatedMinutes = 0;
      },
      error: (err: HttpErrorResponse) => {
        this.loadingData = false;
        this.widgetData = {};
        this.notify(apiMessage(err, 'Could not load the dashboard data.'));
      }
    });
  }

  onFilterChange(): void {
    const compareOnly = this.filters.compare !== this.compare && this.filters.period === this.period
      && this.filters.organization === this.organization && this.filters.location === this.location && this.filters.category === this.category;
    this.filters = this.buildFilters();
    if (!compareOnly) this.loadData();
  }

  toggleCompare(): void {
    this.compare = !this.compare;
    this.onFilterChange();
  }

  override refresh(): void {
    super.refresh();
    this.loadData();
  }

  // ===== Dashboards =====

  toggleMenu(key: MenuKey, event: Event): void {
    event.stopPropagation();
    this.menu = this.menu === key ? null : key;
  }

  @HostListener('document:click')
  closeMenus(): void {
    this.menu = null;
  }

  @HostListener('document:keydown.escape')
  closeDialogs(): void {
    this.menu = null;
    this.addDialog = null;
    this.createOpen = false;
    this.shareOpen = false;
    this.expanded = null;
    this.detail = null;
    this.saveViewOpen = false;
  }

  open(d: Dashboard): void {
    this.menu = null;
    if (this.editMode) this.finishEditing();
    this.openById(d.id);
  }

  /** GET /{id}, then its data. */
  private openById(id: string, then?: (d: Dashboard) => void): void {
    this.store.load(id).subscribe({
      next: (d) => {
        this.current = d;
        this.widgetData = {};
        this.loadData();
        then?.(d);
      },
      error: (err: HttpErrorResponse) => this.notify(apiMessage(err, 'Could not open the dashboard.'))
    });
  }

  /** PUT /{id}/favorite. */
  toggleFavorite(d: Dashboard, event: Event): void {
    event.stopPropagation();
    const next = !d.favorite;
    this.store.setFavorite(d, next).subscribe({
      next: () => {
        if (this.current.id === d.id) this.current.favorite = next;
      },
      error: (err: HttpErrorResponse) => this.notify(apiMessage(err, 'Could not update favorites.'))
    });
  }

  /** POST /api/interactive-dashboards (optionally with the current widgets). */
  onCreated(n: NewDashboard): void {
    this.createOpen = false;
    this.store.create(n.name, n.description, n.sharing, n.copyCurrent ? this.current.widgets : []).subscribe({
      next: (d) => {
        if (this.editMode) this.finishEditing();
        this.current = d;
        this.loadData();
        this.startEditing();
        this.notify(`Dashboard “${d.name}” created. Add widgets from the Widget Library.`);
      },
      error: (err: HttpErrorResponse) => this.notify(apiMessage(err, 'Could not create the dashboard.'))
    });
  }

  /** POST /{id}/duplicate. */
  duplicateDashboard(): void {
    this.menu = null;
    if (!this.hasDashboard) return;
    this.store.duplicate(this.current).subscribe({
      next: (d) => {
        this.current = d;
        this.loadData();
        this.notify(`Copied as “${d.name}”.`);
      },
      error: (err: HttpErrorResponse) => this.notify(apiMessage(err, 'Could not copy the dashboard.'))
    });
  }

  /** DELETE /{id} (owner only). */
  deleteDashboard(): void {
    this.menu = null;
    const d = this.current;
    if (!this.hasDashboard || !d.canShare || !confirm(`Delete “${d.name}”? This cannot be undone.`)) return;
    this.store.remove(d).subscribe({
      next: () => {
        if (this.editMode) this.finishEditing();
        const next = this.store.dashboards[0];
        if (next) this.openById(next.id);
        else {
          this.current = noDashboard();
          this.widgetData = {};
        }
        this.notify(`Dashboard “${d.name}” deleted.`);
      },
      error: (err: HttpErrorResponse) => this.notify(apiMessage(err, 'Could not delete the dashboard.'))
    });
  }

  openShare(): void {
    if (!this.hasDashboard) {
      this.notify('Create a dashboard first.');
      return;
    }
    if (!this.current.canShare) {
      this.notify(`Only the owner (${this.current.owner}) can change sharing. Use Copy link from the ⋯ menu instead.`);
      return;
    }
    this.shareOpen = true;
  }

  /** PUT /{id}/share. */
  onShared(r: ShareResult): void {
    this.shareOpen = false;
    this.store.share(this.current, r.sharing, r.sharedWith, r.groups).subscribe({
      next: () => {
        this.current.sharing = r.sharing;
        this.current.sharedWith = r.sharing === 'people' ? r.sharedWith : [];
        this.current.groups = r.sharing === 'people' ? r.groups : [];
        this.notify(`Sharing updated: ${this.sharedText}.`);
      },
      error: (err: HttpErrorResponse) => this.notify(apiMessage(err, 'Could not update sharing.'))
    });
  }

  async copyLink(): Promise<void> {
    this.menu = null;
    try {
      await navigator.clipboard.writeText(this.shareLink);
      this.notify('Dashboard link copied.');
    } catch {
      this.notify(`Copy the link: ${this.shareLink}`);
    }
  }

  // ===== Edit mode =====

  startEditing(): void {
    if (!this.canEdit) return;
    this.snapshot = this.current.widgets.map((w) => ({ ...w }));
    this.editMode = true;
  }

  finishEditing(): void {
    this.editMode = false;
    this.snapshot = null;
  }

  /** PUT /{id} with the widgets as they were when editing started. */
  discardChanges(): void {
    const snapshot = this.snapshot;
    this.finishEditing();
    if (!snapshot) return;
    this.store.replace(this.current, snapshot).subscribe({
      next: (d) => {
        this.current = d;
        this.loadData();
        this.notify('Layout changes discarded.');
      },
      error: (err: HttpErrorResponse) => this.notify(apiMessage(err, 'Could not discard the changes.'))
    });
  }

  /** PUT /{id}/layout with the order and sizes on screen. */
  saveLayout(): void {
    this.store.saveLayout(this.current, this.current.widgets).subscribe({
      next: () => {
        this.finishEditing();
        this.notify(`Layout of “${this.current.name}” saved.`);
      },
      error: (err: HttpErrorResponse) => this.notify(apiMessage(err, 'Could not save the layout.'))
    });
  }

  /** DELETE /{id}/widgets/{widgetId}. */
  removeWidget(w: WidgetConfig): void {
    this.store.removeWidget(this.current, w).subscribe({
      next: () => {
        this.current.widgets = this.current.widgets.filter((x) => x.id !== w.id);
        this.notify(`“${w.title}” removed.`);
      },
      error: (err: HttpErrorResponse) => this.notify(apiMessage(err, 'Could not remove the widget.'))
    });
  }

  resizeWidget(w: WidgetConfig, step: number): void {
    const i = SPAN_STEPS.indexOf(w.span);
    const span = SPAN_STEPS[Math.min(SPAN_STEPS.length - 1, Math.max(0, i + step))];
    // Replace the object so the widget re-renders for its new size (saved with the layout)
    this.current.widgets = this.current.widgets.map((x) => (x.id === w.id ? { ...x, span } : x));
  }

  // ===== Drag and drop (native HTML5) =====

  onDragStart(event: DragEvent, w: WidgetConfig): void {
    if (!this.editMode) {
      event.preventDefault();
      return;
    }
    this.dragId = w.id;
    event.dataTransfer?.setData('text/plain', `move:${w.id}`);
    if (event.dataTransfer) event.dataTransfer.effectAllowed = 'move';
  }

  private isLibraryDrag(event: DragEvent): boolean {
    return !!event.dataTransfer && Array.from(event.dataTransfer.types).includes(LIBRARY_DRAG_TYPE);
  }

  onDragOver(event: DragEvent, index: number): void {
    if (!this.dragId && !(this.isLibraryDrag(event) && this.canEdit)) return;
    event.preventDefault();
    event.stopPropagation();
    if (event.dataTransfer) event.dataTransfer.dropEffect = this.dragId ? 'move' : 'copy';
    this.overIndex = index;
  }

  onDrop(event: DragEvent, index: number): void {
    event.preventDefault();
    event.stopPropagation();
    const libType = event.dataTransfer?.getData(LIBRARY_DRAG_TYPE) as WidgetType | undefined;
    if (libType) {
      this.openAdd(libType, index);
    } else if (this.dragId) {
      const list = [...this.current.widgets];
      const from = list.findIndex((w) => w.id === this.dragId);
      if (from >= 0 && from !== index) {
        const [moved] = list.splice(from, 1);
        list.splice(Math.min(index, list.length), 0, moved);
        this.current.widgets = list;
      }
    }
    this.onDragEnd();
  }

  onDragEnd(): void {
    this.dragId = null;
    this.overIndex = null;
  }

  // ===== Adding widgets =====

  openAdd(type: WidgetType | null, index = this.current.widgets.length): void {
    if (!this.hasDashboard) {
      this.notify('Create a dashboard first, then add widgets to it.');
      return;
    }
    if (!this.canEdit) {
      this.notify(`You have view access to “${this.current.name}”. Copy it with Create Dashboard to customise it.`);
      return;
    }
    this.addDialog = { type: type ?? '', index };
  }

  /** POST /{id}/widgets, then (outside edit mode) PUT /layout for its slot and size, then its data. */
  onWidgetAdded(n: NewWidget): void {
    const at = this.addDialog?.index ?? this.current.widgets.length;
    this.addDialog = null;
    const d = this.current;
    this.store.addWidget(d, { type: n.type, metric: n.metric, title: n.title, span: n.span }).subscribe({
      next: (w) => {
        const list = [...d.widgets];
        list.splice(at, 0, w);
        d.widgets = list;
        if (!this.editMode) this.store.saveLayout(d, list).subscribe({ error: (err: HttpErrorResponse) => console.warn('[dashboards] layout', err) });
        this.store.widgetData(d, w, this.filters).subscribe({
          next: (data) => (this.widgetData = { ...this.widgetData, ...data }),
          error: (err: HttpErrorResponse) => console.warn('[dashboards] widget data', err)
        });
        this.notify(`“${n.title}” added to ${d.name}.`);
      },
      error: (err: HttpErrorResponse) => this.notify(apiMessage(err, 'Could not add the widget.'))
    });
  }

  // ===== Export / views =====

  exportWidget(w: WidgetConfig): void {
    const csv = widgetCsv(w, this.dataFor(w));
    downloadCsv(`${w.title.toLowerCase().replace(/[^a-z0-9]+/g, '-')}.csv`, csv.header, csv.rows);
  }

  /** GET /{id}/export: every widget as an xlsx workbook. */
  exportDashboard(): void {
    if (!this.hasDashboard) {
      this.notify('Create a dashboard first.');
      return;
    }
    const d = this.current;
    this.store.export(d, this.filters).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${d.id}.xlsx`;
        a.click();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
        this.notify(`Exported ${d.widgets.length} widgets from “${d.name}”.`);
      },
      error: (err: HttpErrorResponse) => this.notify(apiMessage(err, 'Could not export the dashboard.'))
    });
  }

  openSaveView(): void {
    this.menu = null;
    this.viewName = '';
    this.viewTried = false;
    this.saveViewOpen = true;
  }

  confirmSaveView(): void {
    this.viewTried = true;
    if (!this.viewName.trim()) return;
    this.saveViewOpen = false;
    this.saveView(this.viewName.trim());
  }

  onNotice(message: string): void {
    this.notify(message);
  }

  scoreTone(score: number): string {
    return score < 50 ? 'red' : score < 65 ? 'orange' : 'green';
  }

  nameTaken = (name: string): boolean => this.store.nameTaken(name);
}
