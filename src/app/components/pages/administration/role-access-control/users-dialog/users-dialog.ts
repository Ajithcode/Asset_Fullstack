import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LucideAngularModule, Search, X } from 'lucide-angular';
import { avatarTone, initials } from '../../shared/admin-common';

/** "View all users" modal: searchable list of the users holding a role or policy. */
@Component({
  selector: 'app-users-dialog',
  imports: [FormsModule, LucideAngularModule],
  template: `
    <div class="overlay" (click)="closed.emit()">
      <div class="confirm ud" role="dialog" aria-modal="true" [attr.aria-label]="title" (click)="$event.stopPropagation()">
        <div class="drawer__head">
          <h2>{{ title }} ({{ users.length }})</h2>
          <button type="button" class="icon-btn icon-btn--bare" aria-label="Close" (click)="closed.emit()">
            <lucide-icon [img]="icons.X" [size]="18"></lucide-icon>
          </button>
        </div>
        <label class="search">
          <lucide-icon [img]="icons.Search" [size]="16"></lucide-icon>
          <input type="search" placeholder="Search users..." [(ngModel)]="term" />
        </label>
        <ul class="ud__list">
          @for (u of filtered; track $index) {
            <li>
              <span class="avatar" [style.background]="tone(u).bg" [style.color]="tone(u).ink">{{ initialsOf(u) }}</span>
              <span>{{ u }}</span>
            </li>
          } @empty {
            <li class="muted">No users match.</li>
          }
        </ul>
      </div>
    </div>
  `,
  styleUrls: ['../../shared/admin-workspace.css'],
  styles: [
    `.ud { width: min(440px, calc(100vw - 32px)); }
     .ud .search { margin-bottom: 10px; }
     .ud__list { list-style: none; margin: 0; padding: 0; max-height: 360px; overflow-y: auto; }
     .ud__list li { display: flex; align-items: center; gap: 10px; padding: 7px 4px; border-bottom: 1px solid #f1f2f6; }`
  ]
})
export class UsersDialog {
  @Input() title = 'Assigned Users';
  @Input() users: string[] = [];
  @Output() closed = new EventEmitter<void>();

  readonly icons = { X, Search };
  term = '';

  get filtered(): string[] {
    const t = this.term.trim().toLowerCase();
    return t ? this.users.filter((u) => u.toLowerCase().includes(t)) : this.users;
  }

  tone(name: string): { bg: string; ink: string } {
    return avatarTone(name);
  }

  initialsOf(name: string): string {
    return initials(name);
  }
}
