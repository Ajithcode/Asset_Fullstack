import { Component, EventEmitter, HostListener, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import * as XLSX from 'xlsx';
import { LucideAngularModule, ArrowDown, ArrowUp, ArrowUpDown, ChevronLeft, ChevronRight, Download, Search, X } from 'lucide-angular';
import { Drill, DrillColumn, DrillRow } from './drill';

/**
 * Popup listing the records behind a clicked widget element. Rows that can be opened drill into the
 * record; Back returns to the list.
 */
@Component({
  selector: 'app-drill-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideAngularModule],
  templateUrl: './drill-dialog.html',
  styleUrls: ['./drill-dialog.css'],
})
export class DrillDialog {
  readonly icons = { ArrowDown, ArrowUp, ArrowUpDown, ChevronLeft, ChevronRight, Download, Search, X };
  readonly pageSize = 10;

  @Output() closed = new EventEmitter<void>();

  /** Opened drills, the current one last. */
  stack: Drill[] = [];
  search = '';
  sortKey = '';
  sortDesc = false;
  page = 0;

  @Input() set drill(d: Drill | null) {
    this.stack = d ? [d] : [];
    this.reset();
  }

  get current(): Drill | null {
    return this.stack[this.stack.length - 1] ?? null;
  }

  get rows(): DrillRow[] {
    const d = this.current;
    if (!d?.rows) return [];
    const q = this.search.trim().toLowerCase();
    const list = q ? d.rows.filter((r) => Object.values(r.cells).some((v) => String(v).toLowerCase().includes(q))) : [...d.rows];
    const col = d.columns?.find((c) => c.key === this.sortKey);
    if (col) {
      const dir = this.sortDesc ? -1 : 1;
      list.sort((a, b) => {
        const x = a.cells[col.key];
        const y = b.cells[col.key];
        return dir * (col.num ? Number(x) - Number(y) : String(x).localeCompare(String(y), undefined, { numeric: true }));
      });
    }
    return list;
  }

  get pageRows(): DrillRow[] {
    return this.rows.slice(this.page * this.pageSize, (this.page + 1) * this.pageSize);
  }

  get pages(): number {
    return Math.max(1, Math.ceil(this.rows.length / this.pageSize));
  }

  sort(c: DrillColumn): void {
    if (this.sortKey === c.key) this.sortDesc = !this.sortDesc;
    else {
      this.sortKey = c.key;
      this.sortDesc = !!c.num;
    }
    this.page = 0;
  }

  openRow(r: DrillRow): void {
    if (!r.open) return;
    this.stack = [...this.stack, r.open()];
    this.reset();
  }

  back(): void {
    this.stack = this.stack.slice(0, -1);
    this.reset();
  }

  run(a: { run: () => void }): void {
    a.run();
    this.close();
  }

  exportRows(): void {
    const d = this.current;
    if (!d?.columns) return;
    const sheet = XLSX.utils.json_to_sheet(this.rows.map((r) => Object.fromEntries(d.columns!.map((c) => [c.label, r.cells[c.key] ?? '']))));
    const book = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(book, sheet, 'Data');
    XLSX.writeFile(book, `${d.title.replace(/[^\w]+/g, '_')}.xlsx`);
  }

  @HostListener('document:keydown.escape')
  close(): void {
    if (!this.stack.length) return;
    this.stack = [];
    this.closed.emit();
  }

  private reset(): void {
    this.search = '';
    this.sortKey = '';
    this.sortDesc = false;
    this.page = 0;
  }
}
