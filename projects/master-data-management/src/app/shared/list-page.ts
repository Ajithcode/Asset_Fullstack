import { Directive, ElementRef, HostListener, ViewChild } from '@angular/core';

/**
 * List + side drawer behaviour shared by the People masters: filters, sorting, paging, row
 * selection, row menus, the add/edit drawer, delete confirmation and the toast.
 * Subclasses say how to read, filter, sort and remove their records.
 */
@Directive()
export abstract class ListPage<T, F extends object, S extends string = string> {
  @ViewChild('formEl', { read: ElementRef }) formEl?: ElementRef<HTMLElement>;

  readonly pageSizes = [10, 20, 50, 100];

  draft: F;
  applied: F;
  sortKey: S | null = null;
  sortDir: 1 | -1 = 1;
  page = 0;
  pageSize = 10;
  selected = new Set<string>();
  menuFor: string | null = null;

  formOpen = false;
  formMode: 'add' | 'edit' = 'add';
  formRecord: T | null = null;

  pendingDelete: T[] = [];
  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  protected constructor(private readonly emptyFilters: F) {
    this.draft = { ...emptyFilters };
    this.applied = { ...emptyFilters };
  }

  /** Unique key of a record (its ID or code). */
  abstract keyOf(record: T): string;
  /** Display name used in messages. */
  abstract nameOf(record: T): string;
  protected abstract all(): T[];
  protected abstract matches(record: T, filters: F): boolean;
  protected abstract sortValue(record: T, key: S): string | number;
  protected abstract removeRecords(keys: string[]): void;

  get rows(): T[] {
    const rows = this.all().filter((r) => this.matches(r, this.applied));
    const key = this.sortKey;
    if (!key) return rows;
    return [...rows].sort((a, b) => {
      const x = this.sortValue(a, key);
      const y = this.sortValue(b, key);
      const order = typeof x === 'number' && typeof y === 'number' ? x - y : String(x).localeCompare(String(y), undefined, { numeric: true });
      return this.sortDir * order;
    });
  }

  get pageRows(): T[] {
    return this.rows.slice(this.page * this.pageSize, (this.page + 1) * this.pageSize);
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.rows.length / this.pageSize));
  }

  get pageNumbers(): number[] {
    const count = this.pageCount;
    const start = Math.max(0, Math.min(this.page - 2, count - 5));
    return Array.from({ length: Math.min(5, count) }, (_, i) => start + i);
  }

  get showingFrom(): number {
    return this.rows.length ? this.page * this.pageSize + 1 : 0;
  }

  get showingTo(): number {
    return Math.min((this.page + 1) * this.pageSize, this.rows.length);
  }

  get allOnPageSelected(): boolean {
    return this.pageRows.length > 0 && this.pageRows.every((r) => this.selected.has(this.keyOf(r)));
  }

  apply(): void {
    this.applied = { ...this.draft };
    this.page = 0;
  }

  clear(): void {
    this.draft = { ...this.emptyFilters };
    this.apply();
  }

  sortBy(key: S): void {
    if (this.sortKey !== key) {
      this.sortKey = key;
      this.sortDir = 1;
    } else if (this.sortDir === 1) {
      this.sortDir = -1;
    } else {
      this.sortKey = null;
    }
    this.page = 0;
  }

  goTo(page: number): void {
    this.page = Math.min(Math.max(page, 0), this.pageCount - 1);
  }

  toggleRow(key: string): void {
    if (this.selected.has(key)) {
      this.selected.delete(key);
    } else {
      this.selected.add(key);
    }
  }

  togglePage(): void {
    const select = !this.allOnPageSelected;
    this.pageRows.forEach((r) => (select ? this.selected.add(this.keyOf(r)) : this.selected.delete(this.keyOf(r))));
  }

  toggleMenu(key: string): void {
    this.menuFor = this.menuFor === key ? null : key;
  }

  @HostListener('document:click')
  closeMenu(): void {
    this.menuFor = null;
  }

  openAdd(prefill: T | null = null): void {
    this.menuFor = null;
    this.formMode = 'add';
    this.formRecord = prefill;
    this.formOpen = true;
    this.revealForm();
  }

  openEdit(record: T): void {
    this.menuFor = null;
    this.formMode = 'edit';
    this.formRecord = record;
    this.formOpen = true;
    this.revealForm();
  }

  closeForm(): void {
    this.formOpen = false;
    this.formRecord = null;
  }

  // The drawer sits at the top of the page; bring it into view when opened from further down
  protected revealForm(): void {
    setTimeout(() => this.formEl?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' }));
  }

  /** Keeps a renamed record selected after saving. */
  protected carrySelection(fromKey: string | null, toKey: string): void {
    if (fromKey && this.selected.delete(fromKey)) this.selected.add(toKey);
  }

  /** Reason a record can't be deleted, or null when it can. */
  protected deleteBlocker(_record: T): string | null {
    return null;
  }

  askDelete(list: T[]): void {
    this.menuFor = null;
    for (const r of list) {
      const reason = this.deleteBlocker(r);
      if (reason) {
        this.notify(reason);
        return;
      }
    }
    this.pendingDelete = list;
  }

  askDeleteSelected(): void {
    this.askDelete(this.all().filter((r) => this.selected.has(this.keyOf(r))));
  }

  confirmDelete(noun: string): void {
    const keys = this.pendingDelete.map((r) => this.keyOf(r));
    this.removeRecords(keys);
    keys.forEach((k) => this.selected.delete(k));
    if (this.formMode === 'edit' && this.formRecord && keys.includes(this.keyOf(this.formRecord))) this.closeForm();
    this.notify(keys.length === 1 ? `${this.nameOf(this.pendingDelete[0])} deleted.` : `${keys.length} ${noun} deleted.`);
    this.pendingDelete = [];
    this.goTo(this.page);
  }

  cancelDelete(): void {
    this.pendingDelete = [];
  }

  notify(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 4000);
  }
}
