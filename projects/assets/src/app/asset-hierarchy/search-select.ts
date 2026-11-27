import { Component, ElementRef, EventEmitter, HostListener, Input, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface SearchSelectOption {
  id: string;
  /** "Name (AssetID)". */
  label: string;
  /** Second line: category, type... */
  sub?: string;
  /** Not selectable; `hint` says why. */
  disabled?: boolean;
  hint?: string;
}

/** Local searchable select (type-ahead): single choice, or several with chips (`multiple`). */
@Component({
  selector: 'app-search-select',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="ss" [class.is-open]="open">
      <div class="ss-box" (click)="focus()">
        <span *ngFor="let o of chosen" class="ss-chip">
          {{ o.label }}
          <button type="button" (click)="remove(o.id, $event)" [attr.aria-label]="'Remove ' + o.label">&times;</button>
        </span>
        <input
          #q
          class="ss-input"
          type="text"
          autocomplete="off"
          [placeholder]="chosen.length && multiple ? '' : single ? '' : placeholder"
          [ngModel]="query"
          (ngModelChange)="query = $event; open = true"
          (focus)="open = true"
          [attr.aria-label]="placeholder"
          [ngModelOptions]="{ standalone: true }"
        />
        <span *ngIf="!multiple && single && !query" class="ss-single" (click)="focus()">{{ single.label }}</span>
        <button *ngIf="!multiple && single" type="button" class="ss-clear" (click)="clear($event)" aria-label="Clear">&times;</button>
      </div>
      <ul *ngIf="open" class="ss-list" role="listbox" [attr.aria-multiselectable]="multiple">
        <li
          *ngFor="let o of filtered"
          role="option"
          class="ss-opt"
          [class.is-disabled]="o.disabled"
          [class.is-on]="isOn(o.id)"
          [attr.aria-selected]="isOn(o.id)"
          [attr.aria-disabled]="o.disabled"
          (mousedown)="$event.preventDefault()"
          (click)="pick(o)"
        >
          <span *ngIf="multiple" class="ss-check">{{ isOn(o.id) ? '☑' : '☐' }}</span>
          <span class="ss-main">
            <strong>{{ o.label }}</strong>
            <small *ngIf="o.sub || (o.disabled && o.hint)">{{ o.disabled && o.hint ? o.hint : o.sub }}</small>
          </span>
        </li>
        <li *ngIf="!filtered.length" class="ss-empty">No matches.</li>
      </ul>
    </div>
  `,
  styles: [
    `
      :host { display: block; }
      .ss { position: relative; }
      .ss-box { display: flex; flex-wrap: wrap; align-items: center; gap: 4px; min-height: 38px; padding: 4px 8px; border: 1px solid var(--ah-control-border, #d1d5db); border-radius: 8px; background: #fff; cursor: text; position: relative; }
      .ss.is-open .ss-box { border-color: var(--ah-accent, #4f2bd8); box-shadow: 0 0 0 3px var(--ah-accent-soft, #f3f0ff); }
      .ss-input { flex: 1; min-width: 80px; border: 0; outline: 0; background: transparent; font: inherit; padding: 4px 0; }
      .ss-single { position: absolute; left: 10px; right: 30px; top: 50%; transform: translateY(-50%); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; pointer-events: none; }
      .ss-clear { border: 0; background: transparent; cursor: pointer; font-size: 18px; line-height: 1; color: var(--ah-text-muted, #6b7280); }
      .ss-chip { display: inline-flex; align-items: center; gap: 4px; padding: 2px 6px 2px 8px; border-radius: 999px; background: var(--ah-accent-soft, #f3f0ff); border: 1px solid var(--ah-accent-border, #c9bdf7); font-size: 12px; }
      .ss-chip button { border: 0; background: transparent; cursor: pointer; font-size: 14px; line-height: 1; padding: 0 2px; }
      .ss-list { position: absolute; z-index: 30; left: 0; right: 0; top: calc(100% + 4px); max-height: 240px; overflow: auto; margin: 0; padding: 4px; list-style: none; background: #fff; border: 1px solid var(--ah-border, #e5e7eb); border-radius: 8px; box-shadow: 0 8px 24px rgba(17, 24, 39, 0.14); }
      .ss-opt { display: flex; gap: 8px; align-items: flex-start; padding: 6px 8px; border-radius: 6px; cursor: pointer; }
      .ss-opt:hover { background: var(--ah-accent-soft, #f3f0ff); }
      .ss-opt.is-on { background: var(--ah-accent-soft, #f3f0ff); }
      .ss-opt.is-disabled { opacity: 0.55; cursor: not-allowed; }
      .ss-opt.is-disabled:hover { background: transparent; }
      .ss-main { display: flex; flex-direction: column; min-width: 0; }
      .ss-main small { color: var(--ah-text-muted, #6b7280); }
      .ss-empty { padding: 8px; color: var(--ah-text-muted, #6b7280); }
    `,
  ],
})
export class SearchSelect {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  @Input() options: SearchSelectOption[] = [];
  @Input() multiple = false;
  @Input() placeholder = 'Search...';
  /** Selected ids (one in single mode). */
  @Input() selected: string[] = [];
  @Output() selectedChange = new EventEmitter<string[]>();

  query = '';
  open = false;

  get chosen(): SearchSelectOption[] {
    return this.multiple ? this.selected.map((id) => this.options.find((o) => o.id === id)).filter((o): o is SearchSelectOption => !!o) : [];
  }

  get single(): SearchSelectOption | undefined {
    return this.multiple ? undefined : this.options.find((o) => o.id === this.selected[0]);
  }

  get filtered(): SearchSelectOption[] {
    const q = this.query.trim().toLowerCase();
    return this.options.filter((o) => !q || `${o.label} ${o.sub ?? ''}`.toLowerCase().includes(q));
  }

  isOn(id: string): boolean {
    return this.selected.includes(id);
  }

  focus(): void {
    this.open = true;
    this.host.nativeElement.querySelector('input')?.focus();
  }

  pick(o: SearchSelectOption): void {
    if (o.disabled) return;
    if (this.multiple) {
      this.selectedChange.emit(this.isOn(o.id) ? this.selected.filter((x) => x !== o.id) : [...this.selected, o.id]);
      return;
    }
    this.selectedChange.emit([o.id]);
    this.query = '';
    this.open = false;
  }

  remove(id: string, e: Event): void {
    e.stopPropagation();
    this.selectedChange.emit(this.selected.filter((x) => x !== id));
  }

  clear(e: Event): void {
    e.stopPropagation();
    this.selectedChange.emit([]);
  }

  @HostListener('document:mousedown', ['$event'])
  onOutside(e: Event): void {
    if (this.open && !this.host.nativeElement.contains(e.target as Node)) {
      this.open = false;
      this.query = '';
    }
  }
}
