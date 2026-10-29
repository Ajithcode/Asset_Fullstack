import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ChevronRight, LucideAngularModule, TriangleAlert, X } from 'lucide-angular';
import { ASSET360_MODULES } from '../../shared/admin-common';
import { NUMBER_LENGTHS, NumberSequence, RECORD_TYPES, RESET_RULES, SCOPES, SEPARATORS, nextNumbers } from '../numbering-sequence.model';

/** Right-hand "Sequence Settings" drawer: the core fields plus a preview of the next five numbers. */
@Component({
  standalone: true,
  selector: 'app-sequence-drawer',
  imports: [FormsModule, LucideAngularModule],
  templateUrl: './sequence-drawer.html',
  styleUrls: ['../../shared/admin-workspace.css', './sequence-drawer.css']
})
export class SequenceDrawer {
  @Input({ required: true }) sequence!: NumberSequence;
  @Input() errors: Record<string, string> = {};
  /** Only show errors after the first save attempt */
  @Input() showErrors = false;
  @Input() nearLimitCount = 0;
  @Input() isNew = false;
  @Output() changed = new EventEmitter<void>();
  @Output() save = new EventEmitter<void>();
  @Output() cancel = new EventEmitter<void>();
  @Output() closed = new EventEmitter<void>();
  @Output() showNearLimit = new EventEmitter<void>();

  readonly icons = { ChevronRight, TriangleAlert, X };
  readonly modules = ASSET360_MODULES;
  readonly recordTypes = RECORD_TYPES;
  readonly separators = SEPARATORS;
  readonly numberLengths = NUMBER_LENGTHS;
  readonly resetRules = RESET_RULES;
  readonly scopes = SCOPES;

  err(field: string): string | null {
    return this.showErrors ? (this.errors[field] ?? null) : null;
  }

  get prefix(): string {
    return this.sequence.tokens.find((t) => t.kind === 'prefix')?.value ?? '';
  }

  set prefix(value: string) {
    const token = this.sequence.tokens.find((t) => t.kind === 'prefix');
    if (token) token.value = value.toUpperCase();
    else this.sequence.tokens.unshift({ kind: 'prefix', value: value.toUpperCase() });
    this.changed.emit();
  }

  get preview(): string[] {
    return nextNumbers(this.sequence, 5);
  }

  touch(): void {
    this.changed.emit();
  }
}
