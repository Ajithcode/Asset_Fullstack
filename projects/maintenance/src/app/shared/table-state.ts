import { Signal, computed, signal } from '@angular/core';

/** Paging, checkbox selection, row menu and inline delete confirmation for one register table. */
export class TableState<T extends { id: string }> {
  readonly page = signal(1);
  readonly size = signal(10);
  readonly selected = signal<ReadonlySet<string>>(new Set());
  readonly menuId = signal<string | null>(null);
  readonly confirmDeleteId = signal<string | null>(null);

  /** Current page of `rows`, with each row's 1-based position in the full list. */
  readonly rows: Signal<{ row: T; n: number }[]>;
  readonly total: Signal<number>;
  readonly allSelected: Signal<boolean>;
  readonly selectedRows: Signal<T[]>;

  constructor(private readonly source: Signal<T[]>) {
    this.total = computed(() => source().length);
    this.rows = computed(() => {
      const all = source();
      const pages = Math.max(1, Math.ceil(all.length / this.size()));
      const page = Math.min(this.page(), pages);
      const start = (page - 1) * this.size();
      return all.slice(start, start + this.size()).map((row, i) => ({ row, n: start + i + 1 }));
    });
    this.allSelected = computed(() => {
      const ids = this.rows().map((r) => r.row.id);
      return ids.length > 0 && ids.every((id) => this.selected().has(id));
    });
    this.selectedRows = computed(() => source().filter((r) => this.selected().has(r.id)));
  }

  /** Back to page 1 and clear selection (call when filters change). */
  reset(): void {
    this.page.set(1);
    this.selected.set(new Set());
  }

  toggle(id: string): void {
    this.selected.update((s) => {
      const next = new Set(s);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  toggleAll(): void {
    const ids = this.rows().map((r) => r.row.id);
    const all = this.allSelected();
    this.selected.update((s) => {
      const next = new Set(s);
      ids.forEach((id) => (all ? next.delete(id) : next.add(id)));
      return next;
    });
  }

  clearSelection(): void {
    this.selected.set(new Set());
  }

  toggleMenu(id: string, ev: Event): void {
    ev.stopPropagation();
    this.menuId.update((cur) => (cur === id ? null : id));
  }

  askDelete(id: string): void {
    this.menuId.set(null);
    this.confirmDeleteId.set(id);
  }
}

/** Short-lived message shown bottom-right. */
export class Toast {
  readonly message = signal<string | null>(null);
  private timer?: ReturnType<typeof setTimeout>;

  show(message: string): void {
    this.message.set(message);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.message.set(null), 3200);
  }
}
