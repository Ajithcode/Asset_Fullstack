import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Info, LucideAngularModule, Upload } from 'lucide-angular';
import { ConfigGroup, ConfigSetting, ConfigValue } from './system-configuration.data';
import { configIcon } from './config-icons';

const MAX_LOGO_BYTES = 2 * 1024 * 1024;

/** One card of settings ("Platform Identity", "Regional Defaults", ...) rendered from its metadata. */
@Component({
  standalone: true,
  selector: 'app-config-group-card',
  imports: [FormsModule, LucideAngularModule],
  styleUrls: ['../shared/admin-workspace.css', './system-configuration.css'],
  template: `
    <section class="sub-panel cfg-card">
      <header class="cfg-card__head">
        <span class="cfg-card__icon"><lucide-icon [img]="icon()" [size]="18"></lucide-icon></span>
        <div>
          <h3>{{ group.title }}</h3>
          <p>{{ caption || group.subtitle }}</p>
        </div>
      </header>

      <div class="cfg-grid">
        @for (s of settings; track s.key) {
          <div class="field cfg-field" [class.invalid]="!!errors[s.key]" [class.selected]="s.key === selectedKey">
            <span class="cfg-label">
              <button type="button" class="cfg-label__btn" (click)="select(s)">
                {{ s.label }}
                @if (s.required) {
                  <span class="req">*</span>
                }
              </button>
              <button type="button" class="cfg-info" [attr.aria-label]="'About ' + s.label" (click)="select(s)">
                <lucide-icon [img]="icons.Info" [size]="13"></lucide-icon>
              </button>
              @if (isDirty(s)) {
                <span class="cfg-dirty" title="Unsaved change">Modified</span>
              }
            </span>

            @switch (s.type) {
              @case ('select') {
                <select [attr.aria-label]="s.label" [ngModel]="values[s.key]" (ngModelChange)="change(s, $event)" (focus)="select(s)">
                  @for (o of s.options; track o) {
                    <option [value]="o">{{ o }}</option>
                  }
                </select>
              }
              @case ('toggle') {
                <label class="switch cfg-switch">
                  <input type="checkbox" [ngModel]="values[s.key]" (ngModelChange)="change(s, $event)" (focus)="select(s)" />
                  <i></i>
                  <span class="muted">{{ values[s.key] ? 'On' : 'Off' }}</span>
                </label>
              }
              @case ('number') {
                <div class="cfg-unit">
                  <input
                    type="number"
                    [attr.aria-label]="s.label"
                    [attr.min]="s.min"
                    [attr.max]="s.max"
                    [ngModel]="values[s.key]"
                    (ngModelChange)="change(s, $event)"
                    (focus)="select(s)"
                  />
                  @if (s.unit) {
                    <span>{{ s.unit }}</span>
                  }
                </div>
              }
              @case ('logo') {
                <div class="cfg-logo">
                  @if (values[s.key]) {
                    <img [src]="values[s.key]" alt="Organization logo" />
                  } @else {
                    <span class="cfg-logo__default">A360</span>
                  }
                  <button type="button" class="btn btn--sm" (click)="logoInput.click(); select(s)">
                    <lucide-icon [img]="icons.Upload" [size]="14"></lucide-icon> Change Logo
                  </button>
                  <input #logoInput type="file" hidden accept="image/png,image/jpeg" (change)="pickLogo(s, logoInput)" />
                </div>
              }
              @default {
                <input
                  [type]="inputType(s)"
                  [attr.aria-label]="s.label"
                  [ngModel]="values[s.key]"
                  (ngModelChange)="change(s, $event)"
                  (focus)="select(s)"
                />
              }
            }
            <small>{{ errors[s.key] || hint(s) }}</small>
          </div>
        }
      </div>
    </section>
  `
})
export class ConfigGroupCard {
  @Input({ required: true }) group!: ConfigGroup;
  /** The settings to show (already filtered by the settings search). */
  @Input({ required: true }) settings: ConfigSetting[] = [];
  /** Working (unsaved) values. */
  @Input({ required: true }) values: Record<string, ConfigValue> = {};
  @Input() errors: Record<string, string> = {};
  @Input() selectedKey: string | null = null;
  /** Replaces the group subtitle, e.g. with the section name in search results. */
  @Input() caption = '';

  @Output() selectSetting = new EventEmitter<string>();
  @Output() valueChange = new EventEmitter<{ key: string; value: ConfigValue }>();
  @Output() notice = new EventEmitter<string>();

  readonly icons = { Info, Upload };

  icon() {
    return configIcon(this.group.icon);
  }

  select(s: ConfigSetting): void {
    this.selectSetting.emit(s.key);
  }

  isDirty(s: ConfigSetting): boolean {
    return this.values[s.key] !== s.value;
  }

  change(s: ConfigSetting, value: ConfigValue | null): void {
    this.valueChange.emit({ key: s.key, value: value ?? '' });
  }

  hint(s: ConfigSetting): string {
    const v = String(this.values[s.key]);
    return s.hintByValue?.[v] ?? s.hint;
  }

  inputType(s: ConfigSetting): string {
    return s.type === 'email' ? 'email' : s.type === 'url' ? 'url' : 'text';
  }

  /** Reads a PNG / JPG up to 2 MB as a data URL for the preview. */
  pickLogo(s: ConfigSetting, input: HTMLInputElement): void {
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    if (!['image/png', 'image/jpeg'].includes(file.type)) {
      this.notice.emit('Logo must be a PNG or JPG image.');
      return;
    }
    if (file.size > MAX_LOGO_BYTES) {
      this.notice.emit('Logo must be 2MB or smaller.');
      return;
    }
    const reader = new FileReader();
    reader.onload = () => this.valueChange.emit({ key: s.key, value: String(reader.result) });
    reader.readAsDataURL(file);
  }
}
