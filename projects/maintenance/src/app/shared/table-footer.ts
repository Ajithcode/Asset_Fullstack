import { Component, computed, input, model } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LucideAngularModule, ChevronLeft, ChevronRight } from 'lucide-angular';

/** "Show [10] records per page · Showing 1 to 10 of 124 records · ‹ 1 2 3 … 13 ›" */
@Component({
  selector: 'app-mnt-table-footer',
  imports: [FormsModule, LucideAngularModule],
  template: `
    <div class="tf">
      <label class="tf-size">Show
        <select [ngModel]="size()" (ngModelChange)="setSize($event)" aria-label="Records per page">
          @for (s of sizes; track s) { <option [ngValue]="s">{{ s }}</option> }
        </select>
        records per page
      </label>
      <span class="tf-info">{{ info() }}</span>
      <nav class="tf-pages" aria-label="Pagination">
        <button type="button" class="tf-nav" aria-label="Previous page" [disabled]="page() <= 1" (click)="go(page() - 1)"><lucide-icon [img]="icons.ChevronLeft" [size]="17"></lucide-icon></button>
        @for (p of pages(); track $index) {
          @if (p === 0) { <span class="tf-gap">…</span> }
          @else { <button type="button" class="tf-p" [class.is-active]="p === page()" (click)="go(p)">{{ p }}</button> }
        }
        <button type="button" class="tf-nav" aria-label="Next page" [disabled]="page() >= pageCount()" (click)="go(page() + 1)"><lucide-icon [img]="icons.ChevronRight" [size]="17"></lucide-icon></button>
      </nav>
    </div>
  `,
  styles: `
    .tf { display: flex; align-items: center; gap: 16px; flex-wrap: wrap; padding: 16px 18px; font-size: 13.5px; color: #2c2a3d; }
    .tf-size { display: inline-flex; align-items: center; gap: 10px; }
    .tf-size select { height: 38px; min-width: 72px; padding: 0 8px; border: 1px solid #d6d3e0; border-radius: 8px; font: inherit; font-weight: 600; background: #fff; }
    .tf-info { flex: 1; text-align: center; }
    .tf-pages { display: inline-flex; align-items: center; gap: 4px; padding: 3px; border: 1px solid #e8e6f0; border-radius: 8px; background: #fff; }
    .tf-p, .tf-nav { min-width: 34px; height: 34px; border: 0; border-radius: 6px; background: none; font: inherit; color: #1c1442; cursor: pointer; display: inline-flex; align-items: center; justify-content: center; }
    .tf-p:hover, .tf-nav:hover:not(:disabled) { background: #f1ecff; }
    .tf-p.is-active { background: #4318ff; color: #fff; font-weight: 700; }
    .tf-nav:disabled { opacity: 0.4; cursor: default; }
    .tf-gap { min-width: 24px; text-align: center; color: #6b6880; }
    @media (max-width: 760px) { .tf-info { flex-basis: 100%; text-align: left; order: 3; } }
  `
})
export class TableFooter {
  readonly total = input.required<number>();
  readonly page = model(1);
  readonly size = model(10);
  readonly sizes = [10, 25, 50, 100];
  readonly icons = { ChevronLeft, ChevronRight };

  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.total() / this.size())));

  readonly info = computed(() => {
    const total = this.total();
    if (!total) return 'No records';
    const from = (this.page() - 1) * this.size() + 1;
    return `Showing ${from} to ${Math.min(this.page() * this.size(), total)} of ${total} records`;
  });

  /** Page numbers with 0 marking a gap: 1 2 3 4 5 … 13 */
  readonly pages = computed(() => {
    const n = this.pageCount();
    const cur = this.page();
    if (n <= 7) return Array.from({ length: n }, (_, i) => i + 1);
    if (cur <= 4) return [1, 2, 3, 4, 5, 0, n];
    if (cur >= n - 3) return [1, 0, n - 4, n - 3, n - 2, n - 1, n];
    return [1, 0, cur - 1, cur, cur + 1, 0, n];
  });

  go(p: number): void {
    this.page.set(Math.min(Math.max(1, p), this.pageCount()));
  }

  setSize(s: number): void {
    this.size.set(s);
    this.page.set(1);
  }
}
