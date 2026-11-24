import { Directive, TemplateRef, inject } from '@angular/core';
import { AnalyticsPage, exportTable, isExportAction } from '../../shared/analytics-common';
import {
  AnalyticsApiService, AnalyticsGroup, FilterOptionDto, apiMessage, filterValue, optionLabels, viewDates, viewFilters
} from '../../shared/analytics-api.service';
import { ALL_CATEGORIES, ALL_TYPES, CATEGORY_OPTIONS, RaScope, TYPE_OPTIONS } from './asset-universe';
import { RaKpi } from './ra-cards';
import { RaActionForm, RaActionKind, RaActionRequest, RaDetail, RaRecords } from './ra-dialogs';

export type SortDir = 1 | -1;

export interface SavedView {
  /** Report Analytics view id (saved views only) */
  id?: string;
  name: string;
  filters: Record<string, string>;
}

/** Sorts a copy of `rows` by a string or number field. */
export function sortRows<T>(rows: T[], key: keyof T, dir: SortDir): T[] {
  return [...rows].sort((a, b) => {
    const x = a[key];
    const y = b[key];
    return (typeof x === 'number' && typeof y === 'number' ? x - y : String(x).localeCompare(String(y), 'en', { numeric: true })) * dir;
  });
}

/**
 * Shared behaviour of the asset-scoped analytics pages (Asset Performance, Maintenance, Predictive):
 * the category / asset type filters, Compare Periods, saved views, KPI + widget menus and the dialogs.
 */
@Directive()
export abstract class ScopedAnalyticsPage extends AnalyticsPage {
  categories: string[] = CATEGORY_OPTIONS;
  assetTypes: string[] = TYPE_OPTIONS;
  category = '';
  assetType = '';
  compare = false;

  kpis: RaKpi[] = [];
  views: SavedView[] = [];
  savingView = false;
  records: RaRecords | null = null;
  detail: RaDetail | null = null;
  action: RaActionRequest | null = null;
  expanded: { title: string; tpl: TemplateRef<unknown> } | null = null;
  /** Page-specific follow-up for the detail dialog's primary button */
  protected detailAction: RaActionRequest | null = null;

  /** Rebuilds KPIs, charts, tables and insights from the current filters. */
  abstract recompute(): void;
  /** CSV of one widget's data. */
  abstract exportWidget(id: string): void;
  /** Page section a KPI's "View details" scrolls to. */
  protected abstract kpiTarget(kpiId: string): string;
  /** Short page name used in file names */
  protected abstract readonly fileStem: string;

  get scope(): RaScope {
    return { organization: this.organization, location: this.location, category: this.category, type: this.assetType };
  }

  get viewNames(): string[] {
    return this.views.map((v) => v.name);
  }

  toggleCompare(): void {
    this.compare = !this.compare;
    this.recompute();
  }

  resetFilters(): void {
    this.restoreFilters(this.defaultFilters());
    this.recompute();
  }

  // ----- Saved views -----
  protected snapshot(): Record<string, string> {
    return { period: this.period, organization: this.organization, location: this.location, category: this.category, assetType: this.assetType };
  }

  protected restoreFilters(f: Record<string, string>): void {
    this.period = f['period'] ?? '';
    this.organization = f['organization'] ?? '';
    this.location = f['location'] ?? '';
    this.category = f['category'] ?? '';
    this.assetType = f['assetType'] ?? '';
  }

  private defaultFilters(): Record<string, string> {
    return { period: '', organization: '', location: '', category: '', assetType: '' };
  }

  confirmSaveView(name: string): void {
    this.savingView = false;
    const view: SavedView = { name, filters: this.snapshot() };
    this.views = [...this.views, view];
    this.persistView(view);
  }

  /** Report Analytics endpoint group whose views CRUD stores this page's saved views (null: not stored). */
  protected readonly viewGroup: AnalyticsGroup | null = null;
  protected readonly analyticsApi = inject(AnalyticsApiService);

  /** Loads the built-in view and the user's saved views (GET api/{group}/views). */
  protected loadViews(): void {
    if (!this.viewGroup) return;
    this.analyticsApi.views(this.viewGroup).subscribe({
      next: (list) => (this.views = list.map((v) => ({ id: v.isBuiltIn ? undefined : v.viewId, name: v.viewName, filters: viewFilters(v) }))),
      error: (err) => console.warn('[analytics] views failed', err)
    });
  }

  /** Stores a new saved view (POST api/{group}/views). */
  protected persistView(view: SavedView): void {
    if (!this.viewGroup) {
      this.saveView(view.name);
      return;
    }
    const filters: Record<string, string | null> = {};
    for (const [k, v] of Object.entries(view.filters)) if (k !== 'period') filters[k] = filterValue(v) ?? null;
    this.analyticsApi.createView(this.viewGroup, view.name, { ...filters, ...viewDates(view.filters['period'] ?? '') }).subscribe({
      next: (saved) => {
        view.id = saved.viewId;
        this.saveView(view.name);
      },
      error: (err) => {
        this.views = this.views.filter((v) => v !== view);
        this.viewSaveFailed(view);
        this.notify(apiMessage(err, 'Could not save the view.'));
      }
    });
  }

  /** Called when saving a view fails, after it is taken off the list. */
  protected viewSaveFailed(_view: SavedView): void {}

  /** Fills the filter dropdowns from GET api/{group}/filters. */
  protected applyFilterOptions(f: { organizations?: FilterOptionDto[]; locations?: FilterOptionDto[]; assetCategories?: FilterOptionDto[]; assetTypes?: FilterOptionDto[] }): void {
    this.organizations = optionLabels('All organizations', f.organizations);
    this.locations = optionLabels('All locations', f.locations);
    this.categories = optionLabels(ALL_CATEGORIES, f.assetCategories);
    this.assetTypes = optionLabels(ALL_TYPES, f.assetTypes);
  }

  applyView(name: string): void {
    const view = this.views.find((v) => v.name === name);
    this.restoreFilters(view ? view.filters : this.defaultFilters());
    this.recompute();
  }

  // ----- Menus -----
  kpiAction(item: string, kpi: RaKpi): void {
    if (isExportAction(item)) {
      this.exportKpis();
      return;
    }
    this.scrollTo(this.kpiTarget(kpi.id));
  }

  widgetAction(item: string, id: string, title: string, tpl: TemplateRef<unknown>): void {
    if (item === 'Expand') this.expanded = { title, tpl };
    else this.exportWidget(id);
  }

  exportKpis(): void {
    exportTable(`${this.fileStem}-kpis.csv`, ['KPI', 'Value', 'Change vs previous period (%)', 'Period'], this.kpis.map((k) => [k.label, k.value, k.delta, this.periodLabel]));
    this.notify('KPI summary exported.');
  }

  scrollTo(id: string): void {
    const el = document.getElementById(id);
    if (!el) return;
    el.scrollIntoView({ behavior: 'smooth', block: 'center' });
    el.classList.add('ra-flash');
    setTimeout(() => el.classList.remove('ra-flash'), 1400);
  }

  // ----- Dialogs -----
  openAction(kind: RaActionKind, title: string, context: string, priority?: string): void {
    this.detail = null;
    this.action = { kind, title, context, priority };
  }

  openDetail(detail: RaDetail, follow: RaActionRequest | null = null): void {
    this.detail = detail;
    this.detailAction = follow;
  }

  detailPrimary(): void {
    const follow = this.detailAction;
    this.detail = null;
    if (follow) this.action = follow;
  }

  submitAction(form: RaActionForm): void {
    const kind = this.action?.kind ?? 'Work order';
    this.action = null;
    // DEMO MODE: the Maintenance module API isn't connected yet
    this.notify(`DEMO MODE: ${kind} “${form.title}” created for ${form.owner}, due ${form.due} (${form.priority}).`);
  }
}
