import { Component, EventEmitter, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Box, Building2, ChevronDown, ChevronUp, LucideAngularModule, Search, User, Wrench } from 'lucide-angular';
import { VARIABLE_GROUPS } from '../notification-templates.data';
import { VariableGroup } from '../notification-templates.model';
import { token } from '../template-render';

/** "Available Variables": grouped, searchable; clicking a variable asks the editor to insert it. */
@Component({
  standalone: true,
  selector: 'app-variable-panel',
  imports: [FormsModule, LucideAngularModule],
  template: `
    <h3 class="vp-title">Available Variables</h3>
    <label class="vp-search">
      <lucide-icon [img]="icons.Search" [size]="14"></lucide-icon>
      <input [(ngModel)]="search" placeholder="Search variables..." aria-label="Search variables" />
    </label>
    @for (g of groups(); track g.name) {
      <div class="vp-group">
        <button type="button" class="vp-group__head" (click)="toggle(g.name)" [attr.aria-expanded]="isOpen(g.name)">
          <lucide-icon [img]="groupIcon(g)" [size]="15"></lucide-icon>
          <span>{{ g.name }}</span>
          <lucide-icon [img]="isOpen(g.name) ? icons.ChevronUp : icons.ChevronDown" [size]="14"></lucide-icon>
        </button>
        @if (isOpen(g.name)) {
          <div class="vp-vars">
            @for (v of g.variables; track v) {
              <button type="button" class="vp-var" (click)="pick.emit(v)" [title]="'Insert ' + label(v)">{{ label(v) }}</button>
            }
          </div>
        }
      </div>
    } @empty {
      <p class="vp-empty">No variables match.</p>
    }
  `,
  styleUrl: './variable-panel.css'
})
export class VariablePanel {
  @Output() pick = new EventEmitter<string>();

  readonly icons = { Search, ChevronDown, ChevronUp, Wrench, Box, User, Building2 };
  search = '';
  private open = new Set<string>(['Work Order']);

  /** Groups filtered by the search; a search opens every matching group. */
  groups(): VariableGroup[] {
    const term = this.search.trim().toLowerCase();
    if (!term) return VARIABLE_GROUPS;
    return VARIABLE_GROUPS.map((g) => ({ ...g, variables: g.variables.filter((v) => v.includes(term.replace(/\s+/g, '_'))) })).filter((g) => g.variables.length);
  }

  isOpen(name: string): boolean {
    return !!this.search.trim() || this.open.has(name);
  }

  toggle(name: string): void {
    if (this.open.has(name)) this.open.delete(name);
    else this.open.add(name);
  }

  label(v: string): string {
    return token(v);
  }

  groupIcon(g: VariableGroup) {
    switch (g.icon) {
      case 'wo': return this.icons.Wrench;
      case 'asset': return this.icons.Box;
      case 'user': return this.icons.User;
      default: return this.icons.Building2;
    }
  }
}
