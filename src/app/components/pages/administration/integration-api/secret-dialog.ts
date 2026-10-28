import { Component, EventEmitter, Input, Output } from '@angular/core';
import { Copy, LucideAngularModule, TriangleAlert } from 'lucide-angular';

/** Shows a newly issued secret exactly once, with a copy button. The parent drops the value on close. */
@Component({
  standalone: true,
  selector: 'app-secret-dialog',
  imports: [LucideAngularModule],
  styleUrls: ['../shared/admin-workspace.css', './integration-api.css'],
  template: `
    <div class="overlay">
      <div class="confirm int-dialog" role="dialog" aria-modal="true" aria-labelledby="int-secret-title">
        <h2 id="int-secret-title">{{ title }}</h2>
        <p class="note note--warn">
          <lucide-icon [img]="icons.TriangleAlert" [size]="16"></lucide-icon>
          Copy this value now. For your security it won't be shown again.
        </p>
        <div class="int-secret-box">
          <code>{{ value }}</code>
          <button type="button" class="btn btn--sm btn--outline" (click)="copy()">
            <lucide-icon [img]="icons.Copy" [size]="14"></lucide-icon> {{ copied ? 'Copied' : 'Copy' }}
          </button>
        </div>
        <p class="muted int-small">DEMO MODE: this value is generated in your browser and is not valid against any real system.</p>
        <div class="confirm__actions">
          <button type="button" class="btn btn--primary" (click)="closed.emit()">I've stored it safely</button>
        </div>
      </div>
    </div>
  `
})
export class SecretDialog {
  @Input({ required: true }) title = '';
  @Input({ required: true }) value = '';
  @Output() closed = new EventEmitter<void>();

  readonly icons = { Copy, TriangleAlert };
  copied = false;

  copy(): void {
    navigator.clipboard?.writeText(this.value).then(
      () => (this.copied = true),
      () => (this.copied = false)
    );
  }
}
