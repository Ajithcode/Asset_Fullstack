import { Component, effect, input } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LucideAngularModule, ArrowDown, ArrowUp, Info, Plus, Trash2 } from 'lucide-angular';
import { Inspection } from '../shared/inspection.models';

interface Rule {
  role: string;
  required: boolean;
  reauth: boolean;
}

/** Sign-off chain editor. UI state only: the API has no sign-off rule resource yet. */
@Component({
  selector: 'app-insp-signoff-rules',
  standalone: true,
  imports: [FormsModule, LucideAngularModule],
  template: `
    <div class="card">
      <div class="card-head"><h2 class="grow">Sign-off Rules</h2>
        <button type="button" class="btn btn-outline btn-sm" (click)="add()"><lucide-icon [img]="icons.Plus" [size]="14"></lucide-icon>Add Step</button></div>
      <div class="card-body body">
        <div class="note"><lucide-icon [img]="icons.Info" [size]="16"></lucide-icon>
          <span>Steps are read from the latest inspection's sign-off chain{{ source() ? ' (' + source()!.inspectionCode + ')' : '' }}. Changes here are a preview only and are not persisted yet.</span></div>
        <div class="table-wrap flush">
          <table class="table">
            <thead><tr><th>Order</th><th>Role</th><th>Required</th><th>Re-authenticate</th><th></th></tr></thead>
            <tbody>
              @for (r of rules; track $index; let i = $index, last = $last) {
                <tr>
                  <td><span class="n">{{ i + 1 }}</span></td>
                  <td><input class="control" [(ngModel)]="r.role" [attr.aria-label]="'Role for step ' + (i + 1)" /></td>
                  <td><label class="toggle"><input type="checkbox" [(ngModel)]="r.required" aria-label="Required" /><span></span></label></td>
                  <td><label class="toggle"><input type="checkbox" [(ngModel)]="r.reauth" aria-label="Re-authenticate" /><span></span></label></td>
                  <td class="nowrap">
                    <button type="button" class="icon-btn" aria-label="Move up" [disabled]="i === 0" (click)="move(i, -1)"><lucide-icon [img]="icons.ArrowUp" [size]="15"></lucide-icon></button>
                    <button type="button" class="icon-btn" aria-label="Move down" [disabled]="last" (click)="move(i, 1)"><lucide-icon [img]="icons.ArrowDown" [size]="15"></lucide-icon></button>
                    <button type="button" class="icon-btn" aria-label="Remove step" [disabled]="rules.length === 1" (click)="rules.splice(i, 1)"><lucide-icon [img]="icons.Trash2" [size]="15"></lucide-icon></button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        <p class="muted">Chain: {{ chain() }}</p>
      </div>
    </div>
  `,
  styleUrls: ['../shared/insp-page.css'],
  styles: [
    `.body { display: flex; flex-direction: column; gap: 12px; padding-top: 0; }
     .flush { padding: 0; } .table tbody tr { cursor: default; } .table .control { max-width: 280px; }
     .n { display: inline-flex; align-items: center; justify-content: center; width: 26px; height: 26px; border-radius: 50%; background: var(--ip-accent-soft); color: var(--ip-accent); font-weight: 700; }
     p { margin: 0; }`
  ]
})
export class SignoffRules {
  readonly icons = { ArrowDown, ArrowUp, Info, Plus, Trash2 };
  readonly source = input<Inspection | undefined>();
  rules: Rule[] = [];
  private seeded = false;

  constructor() {
    effect(() => {
      const steps = this.source()?.signOff ?? [];
      if (this.seeded && !steps.length) return;
      const roles = steps.length ? steps.map((s) => s.role) : ['Inspector', 'Supervisor', 'Approver'];
      this.rules = roles.map((role) => ({ role, required: true, reauth: true }));
      this.seeded = true;
    });
  }

  add(): void {
    this.rules.push({ role: 'New role', required: true, reauth: true });
  }

  move(i: number, step: number): void {
    const [r] = this.rules.splice(i, 1);
    this.rules.splice(i + step, 0, r!);
  }

  chain(): string {
    return this.rules.map((r) => r.role || '—').join(' → ');
  }
}
