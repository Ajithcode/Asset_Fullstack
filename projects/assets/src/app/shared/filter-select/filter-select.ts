import {
  ChangeDetectorRef, Component, ElementRef, HostListener, Input, OnChanges, ViewChild, forwardRef, inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, FormsModule, NG_VALUE_ACCESSOR } from '@angular/forms';
import { LucideAngularModule, Check, ChevronDown, Search, X } from 'lucide-angular';

/** A plain string (value = label) or a value/label pair. */
export type FilterSelectOption = string | { value: string; label: string };

interface NormalizedOption {
  value: string;
  label: string;
}

/** Lists longer than this get a search box at the top of the panel. */
const SEARCH_THRESHOLD = 8;
/** Matches the panel-out animation length below. */
const CLOSE_MS = 140;

/**
 * Drop-in replacement for a filter `<select>`: works with [(ngModel)], opens an animated panel with
 * an "all" entry, a checkmark on the current value, a search box for long lists and full keyboard
 * support (arrows, Home/End, Enter, Escape, Tab).
 *
 *   <app-filter-select name="category" [(ngModel)]="draft.category" [options]="categoryOptions" allLabel="All Categories" />
 */
@Component({
  selector: 'app-filter-select',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideAngularModule],
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => FilterSelect), multi: true }],
  template: `
    <button
      #trigger
      type="button"
      class="fs-trigger"
      [class.is-open]="open && !closing"
      [class.has-value]="!!value"
      [class.is-placeholder]="!includeAll && !value"
      [class.is-invalid]="invalid"
      [disabled]="disabled"
      [attr.aria-expanded]="open && !closing"
      aria-haspopup="listbox"
      [attr.aria-label]="ariaLabel || null"
      (click)="toggle()"
      (keydown)="onTriggerKeydown($event)"
    >
      <span class="fs-value">{{ currentLabel }}</span>
      <lucide-icon class="fs-chevron" [img]="icons.ChevronDown" [size]="16"></lucide-icon>
    </button>

    <div *ngIf="open" class="fs-panel" [class.is-closing]="closing" role="presentation" (keydown)="onPanelKeydown($event)">
      <div *ngIf="searchable" class="fs-search">
        <lucide-icon [img]="icons.Search" [size]="14"></lucide-icon>
        <input
          #searchInput
          type="text"
          [(ngModel)]="query"
          (ngModelChange)="onQueryChange()"
          [ngModelOptions]="{ standalone: true }"
          placeholder="Search…"
          aria-label="Search options"
        />
        <button *ngIf="query" type="button" class="fs-clear" (click)="query = ''; onQueryChange(); focusSearch()" aria-label="Clear search">
          <lucide-icon [img]="icons.X" [size]="12"></lucide-icon>
        </button>
      </div>

      <ul #list class="fs-list" role="listbox" [attr.aria-label]="ariaLabel || allLabel">
        <li
          *ngFor="let o of visible; let i = index; trackBy: trackByValue"
          role="option"
          class="fs-option"
          [class.is-active]="i === activeIndex"
          [class.is-selected]="o.value === value"
          [class.is-all]="o.value === ''"
          [attr.aria-selected]="o.value === value"
          [style.animation-delay.ms]="i < 12 ? i * 14 : 0"
          (mouseenter)="activeIndex = i"
          (mousedown)="$event.preventDefault()"
          (click)="choose(o)"
        >
          <span class="fs-label">{{ o.label }}</span>
          <lucide-icon *ngIf="o.value === value" class="fs-check" [img]="icons.Check" [size]="15" [strokeWidth]="2.5"></lucide-icon>
        </li>
        <li *ngIf="!visible.length" class="fs-empty">No matches for "{{ query }}"</li>
      </ul>
    </div>
  `,
  styles: [`
    :host {
      position: relative;
      display: block;
      min-width: 0;
      --fs-accent: #4f2bd8;
      --fs-accent-soft: #f3f0ff;
      --fs-border: #d1d5db;
      --fs-text: #111827;
      --fs-muted: #6b7280;
    }

    .fs-trigger {
      display: flex;
      align-items: center;
      gap: 8px;
      width: 100%;
      height: 38px;
      padding: 0 10px 0 12px;
      border: 1px solid var(--fs-border);
      border-radius: 6px;
      background: #fff;
      font: inherit;
      font-size: 13.5px;
      color: var(--fs-text);
      text-align: left;
      cursor: pointer;
      transition: border-color 0.15s ease, box-shadow 0.15s ease;
    }

    .fs-trigger:hover:not(:disabled) {
      border-color: #a5a9b4;
    }

    .fs-trigger:focus-visible,
    .fs-trigger.is-open {
      outline: none;
      border-color: var(--fs-accent);
      box-shadow: 0 0 0 3px rgba(79, 43, 216, 0.12);
    }

    .fs-trigger.is-placeholder .fs-value {
      color: #9ca3af;
    }

    .fs-trigger.is-invalid {
      border-color: #dc2626;
    }

    .fs-trigger.is-invalid.is-open,
    .fs-trigger.is-invalid:focus-visible {
      box-shadow: 0 0 0 3px rgba(220, 38, 38, 0.12);
    }

    .fs-trigger:disabled {
      background: #f9fafb;
      color: var(--fs-muted);
      cursor: not-allowed;
    }

    .fs-value {
      flex: 1 1 auto;
      min-width: 0;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .fs-chevron {
      flex: 0 0 auto;
      display: inline-flex;
      color: var(--fs-muted);
      transition: transform 0.22s cubic-bezier(0.16, 1, 0.3, 1), color 0.15s ease;
    }

    .fs-trigger.is-open .fs-chevron {
      transform: rotate(180deg);
      color: var(--fs-accent);
    }

    .fs-panel {
      position: absolute;
      top: calc(100% + 6px);
      left: 0;
      z-index: 60;
      width: max(100%, 220px);
      max-width: 340px;
      padding: 6px;
      border: 1px solid #e5e7eb;
      border-radius: 10px;
      background: #fff;
      box-shadow: 0 16px 36px rgba(15, 23, 42, 0.14), 0 2px 6px rgba(15, 23, 42, 0.06);
      transform-origin: top center;
      animation: fs-in 0.2s cubic-bezier(0.16, 1, 0.3, 1) both;
    }

    .fs-panel.is-closing {
      animation: fs-out 0.14s ease-in both;
      pointer-events: none;
    }

    .fs-search {
      display: flex;
      align-items: center;
      gap: 6px;
      height: 32px;
      margin-bottom: 6px;
      padding: 0 8px;
      border: 1px solid #e5e7eb;
      border-radius: 7px;
      background: #f9fafb;
      color: var(--fs-muted);
      transition: border-color 0.15s ease, background 0.15s ease;
    }

    .fs-search:focus-within {
      border-color: var(--fs-accent);
      background: #fff;
    }

    .fs-search input {
      flex: 1 1 auto;
      min-width: 0;
      border: 0;
      background: transparent;
      font: inherit;
      font-size: 13px;
      color: var(--fs-text);
      outline: none;
    }

    .fs-clear {
      display: inline-flex;
      padding: 2px;
      border: 0;
      border-radius: 50%;
      background: #e5e7eb;
      color: #4b5563;
      cursor: pointer;
    }

    .fs-list {
      max-height: 264px;
      margin: 0;
      padding: 0;
      overflow-y: auto;
      list-style: none;
      overscroll-behavior: contain;
    }

    .fs-option {
      display: flex;
      align-items: center;
      gap: 8px;
      min-height: 34px;
      padding: 6px 10px;
      border-radius: 6px;
      font-size: 13.5px;
      color: var(--fs-text);
      cursor: pointer;
      animation: fs-option-in 0.22s ease-out both;
      transition: background 0.12s ease, color 0.12s ease;
    }

    .fs-option.is-all {
      color: var(--fs-muted);
    }

    .fs-option.is-active {
      background: var(--fs-accent-soft);
      color: var(--fs-text);
    }

    .fs-option.is-selected {
      color: var(--fs-accent);
      font-weight: 600;
    }

    .fs-label {
      flex: 1 1 auto;
      min-width: 0;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .fs-check {
      flex: 0 0 auto;
      display: inline-flex;
      color: var(--fs-accent);
      animation: fs-check-in 0.2s cubic-bezier(0.34, 1.56, 0.64, 1) both;
    }

    .fs-empty {
      padding: 14px 10px;
      font-size: 13px;
      color: var(--fs-muted);
      text-align: center;
    }

    @keyframes fs-in {
      from { opacity: 0; transform: translateY(-6px) scale(0.98); }
      to { opacity: 1; transform: none; }
    }

    @keyframes fs-out {
      from { opacity: 1; transform: none; }
      to { opacity: 0; transform: translateY(-4px) scale(0.98); }
    }

    @keyframes fs-option-in {
      from { opacity: 0; transform: translateY(-3px); }
      to { opacity: 1; transform: none; }
    }

    @keyframes fs-check-in {
      from { opacity: 0; transform: scale(0.4); }
      to { opacity: 1; transform: none; }
    }

    @media (prefers-reduced-motion: reduce) {
      .fs-panel,
      .fs-panel.is-closing,
      .fs-option,
      .fs-check {
        animation-duration: 1ms;
        animation-delay: 0ms !important;
      }
    }
  `],
})
export class FilterSelect implements ControlValueAccessor, OnChanges {
  /** Options after the "all" entry. */
  @Input() options: readonly FilterSelectOption[] | null = [];
  /** Label of the empty-value entry and of the trigger when nothing is chosen. */
  @Input() allLabel = 'All';
  @Input() ariaLabel = '';
  /** false for required choices (popups): no "all" entry, `placeholder` shows until something is picked. */
  @Input() includeAll = true;
  @Input() placeholder = 'Select';
  /** Red border, e.g. a required field left empty after submit. */
  @Input() invalid = false;
  /** Force the search box on or off; by default it shows for long lists. */
  @Input() search: boolean | null = null;

  @ViewChild('trigger') private trigger?: ElementRef<HTMLButtonElement>;
  @ViewChild('searchInput') private searchInput?: ElementRef<HTMLInputElement>;
  @ViewChild('list') private list?: ElementRef<HTMLUListElement>;

  readonly icons = { Check, ChevronDown, Search, X };

  value = '';
  disabled = false;
  open = false;
  closing = false;
  query = '';
  activeIndex = -1;

  private all: NormalizedOption[] = [];
  visible: NormalizedOption[] = [];

  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly cdr = inject(ChangeDetectorRef);
  private closeTimer?: ReturnType<typeof setTimeout>;
  private onChange: (value: string) => void = () => {};
  private onTouched: () => void = () => {};

  ngOnChanges(): void {
    const items = (this.options ?? []).map((o) => (typeof o === 'string' ? { value: o, label: o } : o));
    this.all = this.includeAll ? [{ value: '', label: this.allLabel }, ...items] : items;
    this.applyQuery();
  }

  get searchable(): boolean {
    return this.search ?? this.all.length > SEARCH_THRESHOLD;
  }

  get currentLabel(): string {
    const empty = this.includeAll ? this.allLabel : this.placeholder;
    return this.all.find((o) => o.value === this.value)?.label ?? (this.value || empty);
  }

  // ===== ControlValueAccessor =====

  writeValue(value: unknown): void {
    this.value = value == null ? '' : String(value);
    this.cdr.markForCheck();
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(disabled: boolean): void {
    this.disabled = disabled;
    if (disabled) this.close(false);
  }

  // ===== Open / close =====

  toggle(): void {
    if (this.open && !this.closing) this.close();
    else this.show();
  }

  private show(): void {
    if (this.disabled) return;
    clearTimeout(this.closeTimer);
    this.closing = false;
    this.open = true;
    this.query = '';
    this.applyQuery();
    this.activeIndex = this.visible.length ? Math.max(0, this.visible.findIndex((o) => o.value === this.value)) : -1;
    // After render: focus the search box (if any) and bring the current option into view.
    setTimeout(() => {
      if (this.searchable) this.focusSearch();
      this.scrollActiveIntoView();
    });
  }

  close(refocus = true): void {
    if (!this.open || this.closing) return;
    this.closing = true;
    this.onTouched();
    if (refocus) this.trigger?.nativeElement.focus();
    this.closeTimer = setTimeout(() => {
      this.open = false;
      this.closing = false;
      this.cdr.markForCheck();
    }, CLOSE_MS);
  }

  choose(o: NormalizedOption): void {
    if (o.value !== this.value) {
      this.value = o.value;
      this.onChange(o.value);
    }
    this.close();
  }

  focusSearch(): void {
    this.searchInput?.nativeElement.focus();
  }

  @HostListener('document:mousedown', ['$event'])
  onDocumentMouseDown(event: MouseEvent): void {
    if (this.open && !this.host.nativeElement.contains(event.target as Node)) this.close(false);
  }

  // ===== Search =====

  onQueryChange(): void {
    this.applyQuery();
    this.activeIndex = this.visible.length ? 0 : -1;
  }

  private applyQuery(): void {
    const q = this.query.trim().toLowerCase();
    this.visible = q ? this.all.filter((o) => o.value !== '' && o.label.toLowerCase().includes(q)) : this.all;
  }

  // ===== Keyboard =====

  onTriggerKeydown(event: KeyboardEvent): void {
    if (['ArrowDown', 'ArrowUp', 'Enter', ' '].includes(event.key) && !(this.open && !this.closing)) {
      event.preventDefault();
      this.show();
      return;
    }
    if (this.open && !this.closing) this.onPanelKeydown(event);
  }

  onPanelKeydown(event: KeyboardEvent): void {
    const last = this.visible.length - 1;
    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        this.activeIndex = this.activeIndex >= last ? 0 : this.activeIndex + 1;
        this.scrollActiveIntoView();
        break;
      case 'ArrowUp':
        event.preventDefault();
        this.activeIndex = this.activeIndex <= 0 ? last : this.activeIndex - 1;
        this.scrollActiveIntoView();
        break;
      case 'Home':
        event.preventDefault();
        this.activeIndex = 0;
        this.scrollActiveIntoView();
        break;
      case 'End':
        event.preventDefault();
        this.activeIndex = last;
        this.scrollActiveIntoView();
        break;
      case 'Enter': {
        event.preventDefault();
        const o = this.visible[this.activeIndex];
        if (o) this.choose(o);
        break;
      }
      case 'Escape':
        event.preventDefault();
        event.stopPropagation();
        this.close();
        break;
      case 'Tab':
        this.close(false);
        break;
    }
  }

  private scrollActiveIntoView(): void {
    const el = this.list?.nativeElement.children[this.activeIndex] as HTMLElement | undefined;
    el?.scrollIntoView({ block: 'nearest' });
  }

  trackByValue(_: number, o: NormalizedOption): string {
    return o.value;
  }
}
