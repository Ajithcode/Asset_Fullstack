import { Component, EventEmitter, Input, Output } from '@angular/core';
import { DataJob, ValidationRule } from '../data-import.model';

/** Validation Rules tab: switch individual rules on or off. */
@Component({
  selector: 'app-adm-job-rules',
  styleUrls: ['../../shared/admin-workspace.css', '../data-import-export.css'],
  template: `
    <div class="panel__head">
      <h3>Validation Rules <small class="muted">· {{ enabledCount }} of {{ job.rules.length }} enabled</small></h3>
    </div>
    <ul class="rules">
      @for (r of job.rules; track r.id) {
        <li>
          <div class="rules__body">
            <b>{{ r.name }}</b>
            <span class="muted">{{ r.description }}</span>
            <small>Field: {{ r.field }}</small>
          </div>
          <span class="pill" [attr.data-tone]="r.severity === 'Error' ? 'red' : 'orange'"><i></i>{{ r.severity }}</span>
          <label class="switch">
            <input type="checkbox" [checked]="r.enabled" (change)="toggle(r)" [attr.aria-label]="'Enable ' + r.name" /><i></i>
          </label>
        </li>
      }
    </ul>
    <p class="muted small-note">Rules run when you click Validate and before every {{ job.type === 'Import' ? 'import' : 'export' }}.</p>
  `
})
export class JobRules {
  @Input({ required: true }) job!: DataJob;
  @Output() rulesChange = new EventEmitter<{ rules: ValidationRule[]; rule: ValidationRule }>();

  get enabledCount(): number {
    return this.job.rules.filter((r) => r.enabled).length;
  }

  toggle(rule: ValidationRule): void {
    const updated = { ...rule, enabled: !rule.enabled };
    this.rulesChange.emit({ rules: this.job.rules.map((r) => (r.id === rule.id ? updated : r)), rule: updated });
  }
}
