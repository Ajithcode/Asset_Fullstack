import { Component, EventEmitter, Input, OnChanges, Output, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  Activity,
  Box,
  Building2,
  ChevronLeft,
  ChevronRight,
  ClipboardCheck,
  Download,
  FileText,
  Layers,
  LucideAngularModule,
  LucideIconData,
  Package,
  Search,
  ShieldCheck,
  Truck,
  Users,
  Wrench,
  X
} from 'lucide-angular';
import { OrganizationService } from '../organization.service';
import { SummaryColumn, SummaryRow, SummaryStat, SummaryTab } from '../organization.model';
import { downloadCsv } from '../organization-csv';

const STAT_ICONS: Record<string, LucideIconData> = { Activity, Box, Building2, ClipboardCheck, FileText, Layers, Package, ShieldCheck, Truck, Users, Wrench };

/**
 * Read-only summary of one master (Asset, People, Vendor, ...) for the selected organization:
 * stat cards worked out from the rows, search + filters, a paged table and a details drawer.
 */
@Component({
  standalone: true,
  selector: 'app-org-summary-tab',
  imports: [FormsModule, LucideAngularModule],
  templateUrl: './summary-tab.html',
  styleUrls: ['../../shared/admin-workspace.css', '../organization.css']
})
export class SummaryTabView implements OnChanges {
  @Input({ required: true }) tab!: SummaryTab;
  @Output() notify = new EventEmitter<string>();

  private readonly service = inject(OrganizationService);
  readonly icons = { Search, Download, ChevronLeft, ChevronRight, X };
  readonly pageSizes = [10, 20, 50];

  search = '';
  filter = '';
  status = '';
  page = 0;
  pageSize = 10;
  selected: SummaryRow | null = null;

  ngOnChanges(): void {
    this.search = '';
    this.filter = '';
    this.status = '';
    this.page = 0;
    this.selected = null;
  }

  statIcon(s: SummaryStat): LucideIconData {
    return STAT_ICONS[s.icon] ?? Box;
  }

  statValue(s: SummaryStat): number {
    return this.service.statValue(this.tab, s);
  }

  get filterLabel(): string {
    return this.tab.columns.find((c) => c.key === this.tab.filterKey)?.label ?? '';
  }

  get filterOptions(): string[] {
    return [...new Set(this.tab.rows.map((r) => r[this.tab.filterKey]))].sort();
  }

  get rows(): SummaryRow[] {
    const term = this.search.trim().toLowerCase();
    return this.tab.rows.filter(
      (r) =>
        (!this.filter || r[this.tab.filterKey] === this.filter) &&
        (!this.status || r[this.tab.statusKey] === this.status) &&
        (!term || this.tab.columns.some((c) => (r[c.key] ?? '').toLowerCase().includes(term)))
    );
  }

  get pageRows(): SummaryRow[] {
    return this.rows.slice(this.page * this.pageSize, (this.page + 1) * this.pageSize);
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.rows.length / this.pageSize));
  }

  get pages(): number[] {
    return Array.from({ length: this.pageCount }, (_, i) => i);
  }

  get showingFrom(): number {
    return this.rows.length ? this.page * this.pageSize + 1 : 0;
  }

  get showingTo(): number {
    return Math.min((this.page + 1) * this.pageSize, this.rows.length);
  }

  onFilterChange(): void {
    this.page = 0;
  }

  goTo(page: number): void {
    this.page = Math.min(Math.max(0, page), this.pageCount - 1);
  }

  clearFilters(): void {
    this.search = '';
    this.filter = '';
    this.status = '';
    this.page = 0;
  }

  tone(c: SummaryColumn, r: SummaryRow): string {
    return c.tones?.[r[c.key]] ?? 'purple';
  }

  open(r: SummaryRow): void {
    this.selected = this.selected === r ? null : r;
  }

  close(): void {
    this.selected = null;
  }

  rowTitle(r: SummaryRow): string {
    return r[this.tab.columns[1].key];
  }

  export(): void {
    const rows = this.rows;
    downloadCsv(
      `${this.tab.id}-summary.csv`,
      this.tab.columns.map((c) => c.label),
      rows.map((r) => this.tab.columns.map((c) => r[c.key] ?? ''))
    );
    this.notify.emit(`Exported ${rows.length} ${this.tab.title.toLowerCase()}.`);
  }
}
