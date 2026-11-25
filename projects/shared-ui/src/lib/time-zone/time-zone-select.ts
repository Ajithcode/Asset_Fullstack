import { CommonModule } from '@angular/common';
import { Component, ElementRef, HostListener, Input, forwardRef, inject } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { DEFAULT_TIME_ZONE, TimeZoneOption, allTimeZones, displayTimeZone, toTimeZoneId } from './time-zones';

/**
 * The one time zone picker (searchable, all IANA zones, "(UTC+05:30) Asia/Kolkata").
 *   <app-time-zone-select name="tz" [(ngModel)]="form.timeZone" />
 * The model holds the IANA id. Old saved labels such as "(GMT+04:00) Dubai" are shown as their zone.
 * An empty model stays empty unless [defaultToIndia]="true" (new records), which selects Asia/Kolkata.
 */
@Component({
  standalone: true,
  selector: 'app-time-zone-select',
  imports: [CommonModule],
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => TimeZoneSelect), multi: true }],
  template: `
    <div class="tz">
      <input
        #box
        type="text"
        class="tz-input"
        autocomplete="off"
        role="combobox"
        aria-haspopup="listbox"
        [attr.aria-expanded]="open"
        [attr.id]="inputId || null"
        [placeholder]="open ? 'Search time zones…' : placeholder"
        [disabled]="disabled"
        [value]="open ? query : shown"
        (focus)="show()"
        (click)="show()"
        (input)="onType($any($event.target).value)"
        (keydown)="onKey($event)"
      />
      <svg class="tz-caret" viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m6 9 6 6 6-6" /></svg>
      <ul *ngIf="open" class="tz-list" role="listbox" [style.top.px]="pos.top" [style.left.px]="pos.left" [style.width.px]="pos.width" [style.maxHeight.px]="pos.maxHeight">
        <li *ngIf="!filtered.length" class="tz-empty">No time zone matches "{{ query }}"</li>
        <li
          *ngFor="let z of filtered; let i = index"
          role="option"
          class="tz-opt"
          [class.is-active]="i === active"
          [class.is-selected]="z.value === id"
          [attr.aria-selected]="z.value === id"
          (mousedown)="$event.preventDefault(); pick(z)"
          (mouseenter)="active = i"
        >{{ z.label }}</li>
      </ul>
    </div>
  `,
  styles: `
    :host { display: block; min-width: 0; }
    .tz { position: relative; }
    .tz-input { width: 100%; box-sizing: border-box; height: 100%; min-height: 34px; padding: 0 28px 0 10px; border: 1px solid var(--adm-border, #d6d9e2); border-radius: 8px; background: #fff; font: inherit; color: inherit; text-overflow: ellipsis; }
    .tz-input:focus { outline: 2px solid #c9c3f6; outline-offset: 0; }
    .tz-input:disabled { background: #f3f4f6; }
    .tz-caret { position: absolute; right: 10px; top: 50%; transform: translateY(-50%); pointer-events: none; color: #6b7280; }
    .tz-list { position: fixed; z-index: 5000; margin: 0; padding: 4px; list-style: none; overflow-y: auto; background: #fff; border: 1px solid #d6d9e2; border-radius: 10px; box-shadow: 0 10px 28px rgba(15, 23, 42, .18); }
    .tz-opt { padding: 7px 10px; border-radius: 7px; font-size: 0.844rem; color: #1f2937; cursor: pointer; }
    .tz-opt.is-active { background: #f5f3ff; color: #5b21b6; }
    .tz-opt.is-selected { font-weight: 700; }
    .tz-empty { padding: 10px; font-size: 0.813rem; color: #6b7280; }
  `,
})
export class TimeZoneSelect implements ControlValueAccessor {
  private readonly host = inject(ElementRef<HTMLElement>);
  @Input() placeholder = 'Select Time Zone';
  @Input() inputId = '';
  /** Pick Asia/Kolkata when nothing is selected yet (new records). */
  @Input() defaultToIndia = false;

  readonly zones = allTimeZones();
  id = '';
  query = '';
  open = false;
  active = 0;
  disabled = false;
  filtered: TimeZoneOption[] = this.zones;
  pos = { top: 0, left: 0, width: 280, maxHeight: 260 };
  private onChange: (v: string) => void = () => {};
  private onTouched: () => void = () => {};

  get shown(): string {
    return this.id ? displayTimeZone(this.id) : '';
  }

  writeValue(value: string | null): void {
    const id = toTimeZoneId(value);
    if (!id && this.defaultToIndia) {
      this.id = DEFAULT_TIME_ZONE;
      queueMicrotask(() => this.onChange(this.id));
      return;
    }
    this.id = id;
  }
  registerOnChange(fn: (v: string) => void): void { this.onChange = fn; }
  registerOnTouched(fn: () => void): void { this.onTouched = fn; }
  setDisabledState(d: boolean): void { this.disabled = d; }

  show(): void {
    if (this.open || this.disabled) return;
    this.query = '';
    this.filter();
    this.open = true;
    this.place();
    this.active = Math.max(0, this.filtered.findIndex((z) => z.value === this.id));
    setTimeout(() => this.scrollToActive());
  }

  onType(text: string): void {
    this.query = text;
    this.filter();
    this.active = 0;
  }

  /** Search by id, label, offset ("+5:30", "05:30") and pieces like "kolkata" or "india". */
  private filter(): void {
    const q = this.query.trim().toLowerCase().replace(/_/g, ' ');
    this.filtered = q
      ? this.zones.filter((z) => z.label.toLowerCase().replace(/_/g, ' ').includes(q) || z.label.toLowerCase().replace(/utc([+-])0/, 'utc$1').includes(q))
      : this.zones;
  }

  pick(z: TimeZoneOption): void {
    this.id = z.value;
    this.onChange(z.value);
    this.close();
  }

  onKey(e: KeyboardEvent): void {
    if (!this.open && (e.key === 'ArrowDown' || e.key === 'Enter')) { e.preventDefault(); this.show(); return; }
    if (!this.open) return;
    if (e.key === 'ArrowDown') { e.preventDefault(); this.active = Math.min(this.active + 1, this.filtered.length - 1); this.scrollToActive(); }
    else if (e.key === 'ArrowUp') { e.preventDefault(); this.active = Math.max(this.active - 1, 0); this.scrollToActive(); }
    else if (e.key === 'Enter') { e.preventDefault(); const z = this.filtered[this.active]; if (z) this.pick(z); }
    else if (e.key === 'Escape') { e.stopPropagation(); this.close(); }
    else if (e.key === 'Tab') this.close();
  }

  private close(): void {
    this.open = false;
    this.onTouched();
  }

  private place(): void {
    const r = (this.host.nativeElement as HTMLElement).getBoundingClientRect();
    const below = window.innerHeight - r.bottom - 12;
    const above = r.top - 12;
    const flip = below < 180 && above > below;
    const maxHeight = Math.max(140, Math.min(300, flip ? above : below));
    this.pos = { top: flip ? Math.max(8, r.top - maxHeight - 4) : r.bottom + 4, left: r.left, width: Math.max(r.width, 260), maxHeight };
  }

  private scrollToActive(): void {
    const el = (this.host.nativeElement as HTMLElement).querySelector('.tz-opt.is-active') as HTMLElement | null;
    el?.scrollIntoView({ block: 'nearest' });
  }

  @HostListener('document:mousedown', ['$event'])
  onDocDown(e: MouseEvent): void {
    if (this.open && !(this.host.nativeElement as HTMLElement).contains(e.target as Node)) this.close();
  }

  @HostListener('window:resize')
  @HostListener('window:scroll')
  onViewport(): void {
    if (this.open) this.place();
  }
}
