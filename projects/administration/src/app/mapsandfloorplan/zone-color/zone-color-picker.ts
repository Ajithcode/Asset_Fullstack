import { Component, EventEmitter, Input, Output } from '@angular/core';

/** Brand-friendly zone colours offered by default (the API may send its own list). */
export const ZONE_PALETTE = ['#5B2AA8', '#7C3AED', '#2563EB', '#0EA5E9', '#10B981', '#84CC16', '#F59E0B', '#F97316', '#EF4444', '#EC4899'];

const HEX = /^#[0-9a-fA-F]{6}$/;

export function isHex(v: string | null | undefined): v is string {
  return !!v && HEX.test(v);
}

/** Palette swatches + a custom hex picker, with a preview of how the zone will look (transparent fill, solid border). */
@Component({
  standalone: true,
  selector: 'app-zone-color-picker',
  template: `
    <div class="zcp">
      <div class="zcp__swatches" role="radiogroup" aria-label="Zone colour">
        @for (c of palette; track c) {
          <button type="button" class="zcp__sw" role="radio" [attr.aria-checked]="same(c)" [attr.aria-label]="c" [class.on]="same(c)" [style.background]="c" [disabled]="disabled" (click)="pick(c)"></button>
        }
      </div>
      <div class="zcp__custom">
        <input type="color" class="zcp__native" aria-label="Custom colour" [value]="valid ? value : '#5B2AA8'" [disabled]="disabled" (input)="pick($any($event.target).value)" />
        <input type="text" class="zcp__hex" maxlength="7" aria-label="Hex colour" placeholder="#RRGGBB" [value]="text" [disabled]="disabled" (input)="typed($any($event.target).value)" />
        <span class="zcp__prev" [style.border-color]="valid ? value : '#94a3b8'" [style.background]="fill">Preview</span>
      </div>
      @if (text && !valid) { <small class="zcp__err">Use a hex colour like #5B2AA8</small> }
    </div>
  `,
  styles: `
    .zcp__swatches { display: flex; flex-wrap: wrap; gap: 8px; }
    .zcp__sw { width: 28px; height: 28px; border-radius: 50%; border: 2px solid #fff; box-shadow: 0 0 0 1px #cbd5e1; cursor: pointer; padding: 0; }
    .zcp__sw.on { box-shadow: 0 0 0 2px #0f172a; }
    .zcp__custom { display: flex; align-items: center; gap: 8px; margin-top: 10px; }
    .zcp__native { width: 34px; height: 30px; padding: 0; border: 1px solid #cbd5e1; border-radius: 6px; background: #fff; cursor: pointer; }
    .zcp__hex { width: 96px; padding: 6px 8px; border: 1px solid #cbd5e1; border-radius: 6px; font: inherit; text-transform: uppercase; }
    .zcp__prev { flex: 1; text-align: center; padding: 6px 8px; border: 2px solid; border-radius: 6px; font-size: 0.75rem; color: #334155; }
    .zcp__err { display: block; margin-top: 4px; color: #dc2626; }
  `
})
export class ZoneColorPicker {
  @Input() palette: string[] = ZONE_PALETTE;
  @Input() value = ZONE_PALETTE[0];
  @Input() disabled = false;
  @Output() valueChange = new EventEmitter<string>();

  text = '';

  get valid(): boolean {
    return isHex(this.value);
  }

  get fill(): string {
    return this.valid ? this.value + '1F' : 'transparent';
  }

  same(c: string): boolean {
    return this.valid && this.value.toLowerCase() === c.toLowerCase();
  }

  ngOnChanges(): void {
    this.text = this.value ?? '';
  }

  pick(c: string): void {
    this.text = c.toUpperCase();
    this.value = c.toUpperCase();
    this.valueChange.emit(this.value);
  }

  typed(v: string): void {
    let t = v.trim();
    if (t && !t.startsWith('#')) t = '#' + t;
    this.text = t.toUpperCase();
    if (isHex(t)) this.pick(t);
  }
}
