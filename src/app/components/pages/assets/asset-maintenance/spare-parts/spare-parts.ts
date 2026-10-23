import { Component, EventEmitter, HostListener, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import * as XLSX from 'xlsx';
import {
  LucideAngularModule,
  ArrowDown,
  ArrowUp,
  Box,
  CalendarDays,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  ClipboardList,
  Coins,
  EllipsisVertical,
  Plus,
  Search,
  TriangleAlert,
  Upload,
  X,
  Package,
} from 'lucide-angular';
import { AssetRecord, TODAY, toIsoDate } from '../../asset-data';
import { PartReservation, PartUsage, SparePart, StockStatus, isStocked, lineCost, reservedQty, stockStatus } from '../maintenance-parts';

/** The work-order fields this tab reads and updates (issuing parts adds to a work order's parts cost). */
export interface PartsOrder {
  id: string;
  date: string;
  description: string;
  status: string;
  parts: number;
}

interface UsageRow {
  part: SparePart;
  qty: number;
  total: number;
  avgCost: number;
  lastUsed: string;
  lastWo: string;
  lines: PartUsage[];
}

type SubTab = 'usage' | 'stock' | 'reserved' | 'bom';

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
/** Top-consumed bars: one series, one hue per part rank kept stable by part, not by rank. */
const BAR_COLORS = ['#7c5cff', '#2563eb', '#16a34a', '#eaa200', '#f97316'];

@Component({
  selector: 'app-spare-parts',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideAngularModule],
  templateUrl: './spare-parts.html',
  // shares the page's base styles (cards, tables, buttons, dialogs) with the parent
  styleUrls: ['../asset-maintenance.css', '../asset-maintenance-charts.css', './spare-parts.css'],
})
export class SpareParts {
  @Input({ required: true }) asset!: AssetRecord;
  /** Owned by the parent page; issuing / requesting parts edits them in place. */
  @Input({ required: true }) parts!: SparePart[];
  @Input({ required: true }) usage!: PartUsage[];
  @Input({ required: true }) reserved!: PartReservation[];
  @Input({ required: true }) orders!: PartsOrder[];
  @Input() lastMonth: { critical: number; lowStock: number } = { critical: 0, lowStock: 0 };
  @Input() year = TODAY.getFullYear();
  @Output() activity = new EventEmitter<string>();
  @Output() notify = new EventEmitter<string>();

  readonly icons = { ArrowDown, ArrowUp, Box, CalendarDays, ChevronDown, ChevronLeft, ChevronRight, ClipboardList, Coins, EllipsisVertical, Package, Plus, Search, TriangleAlert, Upload, X };
  readonly pageSizes = [10, 25, 50];
  readonly Math = Math;
  private readonly today = toIsoDate(TODAY);

  sub: SubTab = 'usage';
  search = '';
  categoryFilter = '';
  statusFilter: StockStatus | '' = '';
  from = `${TODAY.getFullYear() - 1}-01-01`;
  to = `${TODAY.getFullYear()}-12-31`;
  rangeOpen = false;
  sortByQty = false;
  pageSize = 10;
  page = 0;
  readonly checked = new Set<string>();
  selectedNo: string | null = null;
  menuOpen = '';
  linesOf: UsageRow | null = null;
  issueForm: { partNo: string; qty: number; wo: string } | null = null;
  requestForm: { partNo: string; qty: number; neededBy: string; note: string } | null = null;
  formError = '';
  private requestSeq = 0;

  // ===== Lookups =====

  part(no: string): SparePart | undefined {
    return this.parts.find((p) => p.partNo === no);
  }

  status(p: SparePart): StockStatus {
    return stockStatus(p);
  }

  statusClass(s: StockStatus): string {
    return s === 'In Stock' ? 'sp-st--ok' : s === 'Low Stock' ? 'sp-st--low' : 'sp-st--out';
  }

  reservedOf(p: SparePart): number {
    return reservedQty(p.partNo, this.reserved);
  }

  available(p: SparePart): number {
    return Math.max(0, p.onHand - this.reservedOf(p));
  }

  get stocked(): SparePart[] {
    return this.parts.filter(isStocked);
  }

  get categories(): string[] {
    return [...new Set(this.stocked.map((p) => p.category))].sort();
  }

  fmtDate(iso: string): string {
    return iso ? `${iso.slice(8, 10)} ${MONTHS[Number(iso.slice(5, 7)) - 1]} ${iso.slice(0, 4)}` : '—';
  }

  // ===== KPIs =====

  private yearLines(year: number): PartUsage[] {
    return this.usage.filter((u) => u.date.startsWith(String(year)));
  }

  get kpis(): { used: number; usedChange: number; cost: number; costChange: number; critical: number; criticalDiff: number; low: number; lowDiff: number } {
    const stockedQty = (lines: PartUsage[]) => lines.filter((u) => { const p = this.part(u.partNo); return !p || isStocked(p); }).reduce((t, u) => t + u.qty, 0);
    const cost = (lines: PartUsage[]) => lines.reduce((t, u) => t + lineCost(u), 0);
    const now = this.yearLines(this.year);
    const last = this.yearLines(this.year - 1);
    const pct = (a: number, b: number) => (b ? Math.round(((a - b) / b) * 100) : 0);
    const critical = this.stocked.filter((p) => p.critical).length;
    const low = this.stocked.filter((p) => this.status(p) !== 'In Stock').length;
    return {
      used: stockedQty(now), usedChange: pct(stockedQty(now), stockedQty(last)),
      cost: cost(now), costChange: pct(cost(now), cost(last)),
      critical, criticalDiff: critical - this.lastMonth.critical,
      low, lowDiff: low - this.lastMonth.lowStock,
    };
  }

  // ===== Parts usage =====

  get rangeLabel(): string {
    return `${this.fmtDate(this.from)} - ${this.fmtDate(this.to)}`;
  }

  /** Usage in the date range, one row per part. */
  get usageRows(): UsageRow[] {
    const map = new Map<string, UsageRow>();
    for (const u of this.usage) {
      if (u.date < this.from || u.date > this.to) continue;
      const p = this.part(u.partNo);
      if (!p) continue;
      let row = map.get(u.partNo);
      if (!row) map.set(u.partNo, (row = { part: p, qty: 0, total: 0, avgCost: 0, lastUsed: '', lastWo: '', lines: [] }));
      row.qty += u.qty;
      row.total += lineCost(u);
      row.lines.push(u);
      if (u.date >= row.lastUsed) {
        row.lastUsed = u.date;
        row.lastWo = u.wo;
      }
    }
    const q = this.search.trim().toLowerCase();
    return [...map.values()]
      .map((r) => ({ ...r, avgCost: Math.round(r.total / r.qty) }))
      .filter((r) => (!this.categoryFilter || r.part.category === this.categoryFilter) && (!this.statusFilter || (isStocked(r.part) && this.status(r.part) === this.statusFilter)))
      .filter((r) => !q || `${r.part.partNo} ${r.part.name} ${r.part.category} ${r.lastWo}`.toLowerCase().includes(q))
      .sort((a, b) => (this.sortByQty ? b.qty - a.qty : b.lastUsed.localeCompare(a.lastUsed)));
  }

  get pageRows(): UsageRow[] {
    return this.usageRows.slice(this.page * this.pageSize, (this.page + 1) * this.pageSize);
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.usageRows.length / this.pageSize));
  }

  get pages(): number[] {
    return Array.from({ length: this.pageCount }, (_, i) => i);
  }

  get showing(): string {
    const n = this.usageRows.length;
    return n ? `Showing ${this.page * this.pageSize + 1} to ${Math.min(n, (this.page + 1) * this.pageSize)} of ${n} records` : 'No records';
  }

  onFilter(): void {
    this.page = 0;
  }

  get allChecked(): boolean {
    return this.pageRows.length > 0 && this.pageRows.every((r) => this.checked.has(r.part.partNo));
  }

  toggleAll(on: boolean): void {
    for (const r of this.pageRows) on ? this.checked.add(r.part.partNo) : this.checked.delete(r.part.partNo);
  }

  toggleCheck(no: string, on: boolean): void {
    on ? this.checked.add(no) : this.checked.delete(no);
  }

  // ===== Other sub-tabs =====

  get stockRows(): SparePart[] {
    const q = this.search.trim().toLowerCase();
    return this.stocked
      .filter((p) => (!this.categoryFilter || p.category === this.categoryFilter) && (!this.statusFilter || this.status(p) === this.statusFilter))
      .filter((p) => !q || `${p.partNo} ${p.name} ${p.category}`.toLowerCase().includes(q));
  }

  get reservedRows(): { r: PartReservation; part: SparePart; covered: boolean }[] {
    return this.reserved
      .map((r) => ({ r, part: this.part(r.partNo)! }))
      .filter((x) => !!x.part)
      .map((x) => ({ ...x, covered: x.part.onHand >= this.reservedOf(x.part) }));
  }

  get bomRows(): { part: SparePart; shortfall: number }[] {
    return this.stocked.filter((p) => p.bomQty > 0).map((part) => ({ part, shortfall: Math.max(0, part.bomQty - part.onHand - part.onOrder) }));
  }

  get bomShortfalls(): number {
    return this.bomRows.filter((r) => r.shortfall > 0).length;
  }

  // ===== Side cards =====

  /** Stocked parts used most this year (by quantity). */
  get topConsumed(): { part: SparePart; qty: number; color: string; width: number }[] {
    const qty = new Map<string, number>();
    for (const u of this.yearLines(this.year)) qty.set(u.partNo, (qty.get(u.partNo) ?? 0) + u.qty);
    const rows = [...qty.entries()]
      .map(([no, n]) => ({ part: this.part(no)!, qty: n }))
      .filter((r) => r.part && isStocked(r.part))
      .sort((a, b) => b.qty - a.qty || a.part.name.localeCompare(b.part.name))
      .slice(0, 5);
    const max = Math.max(1, ...rows.map((r) => r.qty));
    return rows.map((r, i) => ({ ...r, color: BAR_COLORS[i], width: (r.qty / max) * 100 }));
  }

  get lowStock(): SparePart[] {
    return this.stocked.filter((p) => this.status(p) !== 'In Stock').sort((a, b) => a.onHand - b.onHand);
  }

  get selected(): SparePart | null {
    return (this.selectedNo && this.part(this.selectedNo)) || this.usageRows[0]?.part || this.stocked[0] || null;
  }

  select(p: SparePart): void {
    this.selectedNo = p.partNo;
    this.menuOpen = '';
  }

  lastUsedOf(p: SparePart): string {
    return this.usage.filter((u) => u.partNo === p.partNo).reduce((m, u) => (u.date > m ? u.date : m), '');
  }

  /** Estimated next replacement from the last use and the part's interval. */
  nextReplacement(p: SparePart): string {
    const last = this.lastUsedOf(p);
    if (!p.replaceEveryDays) return 'On condition';
    if (!last) return '—';
    const d = new Date(last + 'T00:00');
    d.setDate(d.getDate() + p.replaceEveryDays);
    return `${this.fmtDate(toIsoDate(d))} (est.)`;
  }

  showAllConsumed(): void {
    this.sub = 'usage';
    this.sortByQty = true;
    this.from = `${this.year}-01-01`;
    this.to = `${this.year}-12-31`;
    this.onFilter();
  }

  showLowStock(): void {
    this.sub = 'stock';
    this.statusFilter = '';
    this.search = '';
    this.categoryFilter = '';
  }

  // ===== Issue / request =====

  get openOrders(): PartsOrder[] {
    return this.orders.filter((o) => o.status !== 'Completed');
  }

  openIssue(p?: SparePart): void {
    this.menuOpen = '';
    this.formError = '';
    const part = p && p.onHand > 0 ? p : this.stocked.find((x) => x.onHand > 0);
    this.issueForm = { partNo: part?.partNo ?? '', qty: 1, wo: this.openOrders[0]?.id ?? '' };
  }

  saveIssue(): void {
    const f = this.issueForm;
    const p = f && this.part(f.partNo);
    if (!f || !p) return void (this.formError = 'Choose a part.');
    const qty = Math.floor(Number(f.qty));
    if (!(qty > 0)) return void (this.formError = 'Enter a quantity of at least 1.');
    if (qty > p.onHand) return void (this.formError = `Only ${p.onHand} in stock.`);
    const wo = this.orders.find((o) => o.id === f.wo);
    if (!wo) return void (this.formError = 'Choose the work order the parts are issued to.');
    p.onHand -= qty;
    this.usage.push({ wo: wo.id, date: this.today, partNo: p.partNo, qty, unitCost: p.unitCost });
    wo.parts += qty * p.unitCost;
    this.selectedNo = p.partNo;
    this.issueForm = null;
    this.activity.emit(`Spare part issued (${p.name})`);
    this.notify.emit(`${qty} × ${p.name} issued to ${wo.id} (AED ${(qty * p.unitCost).toLocaleString()}).`);
  }

  openRequest(p?: SparePart): void {
    this.menuOpen = '';
    this.formError = '';
    const part = p ?? this.lowStock[0] ?? this.stocked[0];
    const d = new Date(TODAY);
    d.setDate(d.getDate() + (part?.leadTimeDays ?? 7));
    this.requestForm = { partNo: part?.partNo ?? '', qty: part ? Math.max(1, part.reorderLevel * 2 - part.onHand - part.onOrder) : 1, neededBy: toIsoDate(d), note: '' };
  }

  saveRequest(): void {
    const f = this.requestForm;
    const p = f && this.part(f.partNo);
    if (!f || !p) return void (this.formError = 'Choose a part.');
    const qty = Math.floor(Number(f.qty));
    if (!(qty > 0)) return void (this.formError = 'Enter a quantity of at least 1.');
    if (!f.neededBy) return void (this.formError = 'Enter the date the parts are needed by.');
    p.onOrder += qty;
    this.requestSeq++;
    const ref = `PR-${this.year}-${String(140 + this.requestSeq).padStart(4, '0')}`;
    this.requestForm = null;
    this.activity.emit(`Parts requested (${qty} × ${p.name})`);
    this.notify.emit(`${ref} raised: ${qty} × ${p.name}, needed by ${this.fmtDate(f.neededBy)}.`);
  }

  /** Raise requests for every BOM shortfall at once. */
  requestShortfalls(): void {
    const short = this.bomRows.filter((r) => r.shortfall > 0);
    for (const r of short) r.part.onOrder += r.shortfall;
    this.requestSeq++;
    if (short.length) this.notify.emit(`PR-${this.year}-${String(140 + this.requestSeq).padStart(4, '0')} raised for ${short.length} BOM shortfall${short.length > 1 ? 's' : ''}.`);
  }

  releaseReservation(r: PartReservation): void {
    this.menuOpen = '';
    const i = this.reserved.indexOf(r);
    if (i >= 0) this.reserved.splice(i, 1);
    this.notify.emit(`Reservation of ${r.qty} × ${this.part(r.partNo)?.name ?? r.partNo} released.`);
  }

  // ===== Export & menus =====

  export(kind: 'xlsx' | 'csv' = 'xlsx'): void {
    this.menuOpen = '';
    const rows = this.usageRows
      .filter((r) => !this.checked.size || this.checked.has(r.part.partNo))
      .map((r, i) => ({
        '#': i + 1, 'Part Number': r.part.partNo, 'Part Name': r.part.name, Category: r.part.category, 'Used Qty': r.qty, 'Unit Cost (AED)': r.avgCost,
        'Total Cost (AED)': r.total, 'Last Used Date': r.lastUsed, 'Related WO': r.lastWo, Stock: isStocked(r.part) ? this.status(r.part) : 'Non-stock',
      }));
    const book = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet(rows), 'Parts Usage');
    XLSX.writeFile(book, `${this.asset.tag}_Parts_Usage.${kind}`, { bookType: kind });
  }

  print(): void {
    this.menuOpen = '';
    window.print();
  }

  toggleMenu(name: string, event: Event): void {
    event.stopPropagation();
    this.menuOpen = this.menuOpen === name ? '' : name;
  }

  @HostListener('document:click')
  closeMenus(): void {
    this.menuOpen = '';
    this.rangeOpen = false;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.menuOpen = '';
    this.rangeOpen = false;
    this.issueForm = null;
    this.requestForm = null;
    this.linesOf = null;
  }
}
