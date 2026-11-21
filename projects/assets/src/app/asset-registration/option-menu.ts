import { Component, ElementRef, EventEmitter, HostListener, Input, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LucideAngularModule, EllipsisVertical, Pencil, Plus, Trash2, X } from 'lucide-angular';

/** A dropdown option; lists may carry extra fields (colour, initials...) that new options get from `defaults`. */
export interface MenuOption {
  label: string;
  value: string;
}

type Mode = 'add' | 'edit' | 'delete';

/** "Pumps & Compressors" -> "pumps-compressors" */
function slug(label: string): string {
  return label.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '') || 'option';
}

/**
 * The ⋮ button next to a dropdown: add a new option, rename the selected one or delete it.
 * DEMO MODE: changes edit the option list in memory (the page's own array) until the master data API is wired up.
 */
@Component({
  selector: 'app-option-menu',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideAngularModule],
  template: `
    <button type="button" class="om-btn" [disabled]="disabled" (click)="toggle($event)" aria-haspopup="menu" [attr.aria-expanded]="open"
      [attr.aria-label]="'Manage ' + label + ' options'" [title]="disabled ? disabledHint : 'Add, edit or delete ' + label + ' options'">
      <lucide-icon [img]="icons.EllipsisVertical" [size]="17"></lucide-icon>
    </button>

    <div *ngIf="open" class="om-pop" (click)="onPopClick($event)" (keydown.escape)="close()">
      <ng-container *ngIf="!mode">
        <!-- Icons only; the tooltip and screen-reader label say what each does -->
        <div class="om-menu" role="menu" [attr.aria-label]="label + ' options'">
          <button type="button" role="menuitem" (click)="start('add')" [title]="'Add ' + label" [attr.aria-label]="'Add ' + label">
            <lucide-icon [img]="icons.Plus" [size]="17"></lucide-icon>
          </button>
          <button type="button" role="menuitem" [disabled]="!selected" (click)="start('edit')"
            [title]="selected ? 'Edit &quot;' + selected.label + '&quot;' : 'Select an option first'" [attr.aria-label]="selected ? 'Edit ' + selected.label : 'Edit (select an option first)'">
            <lucide-icon [img]="icons.Pencil" [size]="16"></lucide-icon>
          </button>
          <button type="button" role="menuitem" class="om-danger" [disabled]="!selected" (click)="start('delete')"
            [title]="selected ? 'Delete &quot;' + selected.label + '&quot;' : 'Select an option first'" [attr.aria-label]="selected ? 'Delete ' + selected.label : 'Delete (select an option first)'">
            <lucide-icon [img]="icons.Trash2" [size]="16"></lucide-icon>
          </button>
        </div>
      </ng-container>

      <form *ngIf="mode === 'add' || mode === 'edit'" class="om-form" (ngSubmit)="save()">
        <span class="om-title">{{ mode === 'add' ? 'Add ' + label : 'Edit ' + label }}</span>
        <input #nameInput class="om-input" [(ngModel)]="text" name="text" maxlength="80" [attr.aria-label]="label + ' name'"
          [placeholder]="'Enter ' + label.toLowerCase()" autocomplete="off" />
        <span *ngIf="error" class="om-error" role="alert">{{ error }}</span>
        <span class="om-actions">
          <button type="button" class="om-link" (click)="mode = null">Back</button>
          <button type="submit" class="om-save">{{ mode === 'add' ? 'Add' : 'Save' }}</button>
        </span>
      </form>

      <div *ngIf="mode === 'delete' && selected" class="om-form">
        <span class="om-title">Delete "{{ selected.label }}"?</span>
        <span class="om-note">It is removed from the {{ label }} list{{ value === selected.value ? ' and cleared from this field' : '' }}.</span>
        <span class="om-actions">
          <button type="button" class="om-link" (click)="mode = null">Back</button>
          <button type="button" class="om-save om-save--danger" (click)="remove()">Delete</button>
        </span>
      </div>
    </div>
  `,
  styles: [`
    :host { position: relative; display: inline-flex; flex: 0 0 auto; }
    .om-btn {
      display: inline-flex; align-items: center; justify-content: center; width: 38px; height: 100%; min-height: 38px;
      border: 1px solid #d1d5db; border-radius: 6px; background: #fff; color: #4f2bd8; cursor: pointer;
    }
    .om-btn:hover:not(:disabled), .om-btn[aria-expanded='true'] { border-color: #c9bdf7; background: #f3f0ff; }
    .om-btn:disabled { color: #9ca3af; cursor: not-allowed; }
    .om-pop {
      position: absolute; top: calc(100% + 4px); right: 0; z-index: 40; min-width: 240px; max-width: 300px; padding: 6px;
      border: 1px solid #e5e7eb; border-radius: 8px; background: #fff; box-shadow: 0 10px 30px rgba(15, 23, 42, 0.14);
      font-size: 13px; text-align: left;
    }
    .om-pop:has(.om-menu) { min-width: 0; padding: 4px; }
    .om-menu { display: flex; gap: 2px; }
    .om-menu button {
      display: inline-flex; align-items: center; justify-content: center; width: 34px; height: 34px; border: 0; border-radius: 6px;
      background: none; color: #374151; cursor: pointer;
    }
    .om-menu button:hover:not(:disabled) { background: #f3f0ff; color: #4f2bd8; }
    .om-menu button:disabled { color: #b5b9c2; cursor: default; }
    .om-menu .om-danger:not(:disabled) { color: #dc2626; }
    .om-menu .om-danger:hover:not(:disabled) { background: #fdecec; color: #dc2626; }
    .om-form { display: flex; flex-direction: column; gap: 8px; padding: 6px; }
    .om-title { font-weight: 700; color: #111827; }
    .om-note { color: #4b5563; }
    .om-input {
      height: 34px; padding: 0 10px; border: 1px solid #d1d5db; border-radius: 6px; font: inherit; outline: none;
    }
    .om-input:focus { border-color: #4f2bd8; box-shadow: 0 0 0 3px rgba(79, 43, 216, 0.12); }
    .om-error { color: #dc2626; font-size: 12px; }
    .om-actions { display: flex; justify-content: flex-end; gap: 10px; }
    .om-link { padding: 0 4px; border: 0; background: none; font: inherit; color: #4b5563; cursor: pointer; }
    .om-save {
      height: 30px; padding: 0 14px; border: 0; border-radius: 6px; background: #4f2bd8; font: inherit; font-weight: 600; color: #fff; cursor: pointer;
    }
    .om-save--danger { background: #dc2626; }
  `],
})
export class OptionMenu {
  readonly icons = { EllipsisVertical, Pencil, Plus, Trash2, X };

  /** The page's option array; it is edited in place. */
  @Input({ required: true }) options!: MenuOption[];
  /** Field name used in the menu, e.g. "Manufacturer". */
  @Input() label = 'Option';
  /** The field's current value. */
  @Input() value = '';
  @Input() disabled = false;
  @Input() disabledHint = '';
  /** Fields given to new options, e.g. a colour for status lists. */
  @Input() defaults: Record<string, unknown> = {};
  @Output() valueChange = new EventEmitter<string>();

  open = false;
  mode: Mode | null = null;
  text = '';
  error = '';

  private readonly host = inject(ElementRef<HTMLElement>);

  get selected(): MenuOption | undefined {
    return this.options.find((o) => o.value === this.value);
  }

  toggle(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    this.open = !this.open;
    this.mode = null;
  }

  /** Most dropdowns sit inside a <label>; a click on the menu's text must not open the dropdown. */
  onPopClick(event: Event): void {
    event.stopPropagation();
    if (!(event.target as HTMLElement).closest('button, input')) event.preventDefault();
  }

  close(): void {
    this.open = false;
    this.mode = null;
  }

  start(mode: Mode): void {
    this.mode = mode;
    this.error = '';
    this.text = mode === 'edit' ? this.selected?.label ?? '' : '';
    setTimeout(() => (this.host.nativeElement.querySelector('.om-input') as HTMLInputElement | null)?.focus());
  }

  private isDuplicate(label: string, except?: MenuOption): boolean {
    const l = label.toLowerCase();
    return this.options.some((o) => o !== except && o.label.toLowerCase() === l);
  }

  save(): void {
    const label = this.text.trim();
    if (!label) {
      this.error = `Enter the ${this.label.toLowerCase()} name.`;
      return;
    }
    if (this.mode === 'edit') {
      const target = this.selected;
      if (!target) return this.close();
      if (this.isDuplicate(label, target)) {
        this.error = `"${label}" is already in the list.`;
        return;
      }
      target.label = label;
      return this.close();
    }
    if (this.isDuplicate(label)) {
      this.error = `"${label}" is already in the list.`;
      return;
    }
    let value = slug(label);
    for (let n = 2; this.options.some((o) => o.value === value); n++) value = `${slug(label)}-${n}`;
    const option: Record<string, unknown> & MenuOption = { ...this.defaults, label, value };
    // Lists of people show initials
    if (this.options.some((o) => 'initials' in o)) {
      option['initials'] = label.split(/\s+/).map((w) => w[0]).join('').slice(0, 2).toUpperCase();
      option['department'] ??= '';
    }
    this.options.push(option);
    this.valueChange.emit(value);
    this.close();
  }

  remove(): void {
    const target = this.selected;
    if (!target) return this.close();
    this.options.splice(this.options.indexOf(target), 1);
    this.valueChange.emit('');
    this.close();
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: Event): void {
    if (this.open && !this.host.nativeElement.contains(event.target as Node)) this.close();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.close();
  }
}
