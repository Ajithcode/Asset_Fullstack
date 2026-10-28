import { Component, ElementRef, EventEmitter, HostListener, Input, Output, inject } from '@angular/core';
import { ChevronDown, LucideAngularModule } from 'lucide-angular';

/**
 * Multi-select shown as removable chips with a checkbox dropdown
 * (Locations / Departments / Assigned Roles in the Administration drawers).
 */
@Component({
  selector: 'app-chip-select',
  imports: [LucideAngularModule],
  template: `
    <div class="cs" [class.invalid]="invalid" [class.disabled]="disabled">
      <div class="cs__box" role="button" tabindex="0" (click)="toggleOpen()" (keydown.enter)="toggleOpen()">
        @for (v of value; track v) {
          <span class="chip">{{ v }}
            @if (!disabled) {
              <button type="button" [attr.aria-label]="'Remove ' + v" (click)="remove(v, $event)">×</button>
            }
          </span>
        } @empty {
          <span class="cs__placeholder">{{ placeholder }}</span>
        }
        <lucide-icon class="cs__caret" [img]="icons.ChevronDown" [size]="16"></lucide-icon>
      </div>
      @if (open) {
        <div class="cs__menu">
          @for (o of options; track o) {
            <label><input type="checkbox" [checked]="has(o)" (change)="toggle(o)" /> {{ o }}</label>
          }
        </div>
      }
    </div>
  `,
  styleUrls: ['../../shared/admin-workspace.css', './chip-select.css']
})
export class ChipSelect {
  @Input() options: string[] = [];
  @Input() value: string[] = [];
  @Input() placeholder = 'Select…';
  @Input() invalid = false;
  @Input() disabled = false;
  @Output() valueChange = new EventEmitter<string[]>();

  readonly icons = { ChevronDown };
  open = false;
  private readonly host = inject(ElementRef<HTMLElement>);

  @HostListener('document:click', ['$event'])
  onDocClick(e: MouseEvent): void {
    if (this.open && !this.host.nativeElement.contains(e.target as Node)) this.open = false;
  }

  toggleOpen(): void {
    if (!this.disabled) this.open = !this.open;
  }

  has(o: string): boolean {
    return this.value.includes(o);
  }

  toggle(o: string): void {
    // Keep the options' order so chips don't jump around
    const next = this.has(o) ? this.value.filter((v) => v !== o) : this.options.filter((x) => x === o || this.value.includes(x));
    this.valueChange.emit(next);
  }

  remove(o: string, e: Event): void {
    e.stopPropagation();
    this.valueChange.emit(this.value.filter((v) => v !== o));
  }
}
