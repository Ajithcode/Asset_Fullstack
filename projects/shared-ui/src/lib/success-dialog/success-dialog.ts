import { AfterViewInit, Component, ElementRef, EventEmitter, HostListener, Input, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';

/** One button of the success dialog; the first `primary` action gets the focus when the dialog opens. */
export interface SuccessDialogAction {
  /** Handed back through (action) when clicked. */
  key: string;
  label: string;
  primary?: boolean;
}

/**
 * Centered success popup (green check, message, action buttons) on a dimmed backdrop. Does not close by itself:
 * the host closes it on (action) or (closed) (X button, Esc or a click on the backdrop).
 * Usage: <app-success-dialog *ngIf="done" title="Asset registered" [message]="msg" [actions]="actions"
 *          (action)="onAction($event)" (closed)="done = false"></app-success-dialog>
 */
@Component({
  selector: 'app-success-dialog',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="sd-overlay" (click)="closed.emit()">
      <div class="sd-modal" role="alertdialog" aria-modal="true" aria-labelledby="sd-title" aria-describedby="sd-message"
           (click)="$event.stopPropagation()">
        <button type="button" class="sd-close" (click)="closed.emit()" aria-label="Close">
          <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true"><path d="M18 6 6 18M6 6l12 12" /></svg>
        </button>
        <div class="sd-icon" aria-hidden="true">
          <svg viewBox="0 0 52 52"><circle cx="26" cy="26" r="24" /><path class="sd-tick" d="M15 27l7.5 7.5L37.5 19" /></svg>
        </div>
        <h2 id="sd-title" class="sd-title">{{ title }}</h2>
        <p id="sd-message" class="sd-message">{{ message }}</p>
        <div class="sd-actions">
          <button *ngFor="let a of actions" type="button" class="sd-btn" [class.sd-primary]="a.primary"
                  [attr.data-action]="a.key" (click)="action.emit(a.key)">{{ a.label }}</button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .sd-overlay { position: fixed; inset: 0; z-index: 10002; display: flex; align-items: center; justify-content: center; padding: 16px;
      background: rgba(30, 16, 64, 0.5); backdrop-filter: blur(2px); animation: sd-fade .18s ease-out; font-family: var(--piq-font, inherit); }
    .sd-modal { position: relative; width: 100%; max-width: 540px; max-height: calc(100vh - 32px); overflow-y: auto; box-sizing: border-box;
      padding: 32px 28px 24px; border-radius: 18px; background: #fff; text-align: center; color: #111827;
      border-top: 4px solid #6d28d9; box-shadow: 0 24px 60px rgba(46, 16, 101, 0.3); animation: sd-pop .26s cubic-bezier(.2, .9, .3, 1.25); }
    .sd-close { position: absolute; top: 12px; right: 12px; display: inline-flex; align-items: center; justify-content: center; width: 32px; height: 32px;
      border: 0; border-radius: 8px; background: none; color: #6b7280; cursor: pointer; }
    .sd-close:hover { background: #f3f0ff; color: #4c1d95; }
    .sd-close svg { fill: none; stroke: currentColor; stroke-width: 2.2; stroke-linecap: round; }
    .sd-icon { width: 72px; height: 72px; margin: 0 auto 14px; border-radius: 50%; background: #dcfce7; padding: 6px; box-sizing: border-box; }
    .sd-icon svg { width: 100%; height: 100%; }
    .sd-icon circle { fill: #16a34a; }
    .sd-tick { fill: none; stroke: #fff; stroke-width: 4.5; stroke-linecap: round; stroke-linejoin: round;
      stroke-dasharray: 36; stroke-dashoffset: 36; animation: sd-tick .35s .18s ease-out forwards; }
    .sd-title { margin: 0 0 6px; font-size: 1.188rem; font-weight: 800; color: #2e1065; }
    .sd-message { margin: 0 0 22px; font-size: 0.938rem; line-height: 1.45; color: #374151; overflow-wrap: anywhere; }
    .sd-actions { display: flex; flex-wrap: wrap; gap: 10px; justify-content: center; }
    .sd-btn { flex: 1 1 auto; min-height: 40px; padding: 0 18px; white-space: nowrap; border: 1px solid #c4b5fd; border-radius: 10px; background: #fff;
      color: #4c1d95; font: inherit; font-size: 0.875rem; font-weight: 700; cursor: pointer; transition: background .15s, box-shadow .15s; }
    .sd-btn:hover { background: #f5f3ff; }
    .sd-btn:focus-visible { outline: none; box-shadow: 0 0 0 3px rgba(109, 40, 217, 0.35); }
    .sd-btn.sd-primary { border-color: transparent; background: var(--piq-brand-gradient, linear-gradient(90deg, #2e1065, #4c1d95 55%, #6d28d9)); color: #fff; }
    .sd-btn.sd-primary:hover { filter: brightness(1.12); }
    @keyframes sd-fade { from { opacity: 0; } to { opacity: 1; } }
    @keyframes sd-pop { from { opacity: 0; transform: scale(.85) translateY(8px); } to { opacity: 1; transform: none; } }
    @keyframes sd-tick { to { stroke-dashoffset: 0; } }
    @media (prefers-reduced-motion: reduce) { .sd-overlay, .sd-modal, .sd-tick { animation: none; } .sd-tick { stroke-dashoffset: 0; } }
  `],
})
export class SuccessDialog implements AfterViewInit {
  @Input() title = 'Success';
  @Input() message = '';
  @Input() actions: SuccessDialogAction[] = [];
  @Output() action = new EventEmitter<string>();
  @Output() closed = new EventEmitter<void>();

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  ngAfterViewInit(): void {
    const el = this.host.nativeElement;
    const button = el.querySelector<HTMLButtonElement>('.sd-btn.sd-primary') ?? el.querySelector<HTMLButtonElement>('.sd-btn');
    setTimeout(() => button?.focus());
  }

  @HostListener('document:keydown.escape', ['$event'])
  onEscape(event: Event): void {
    event.preventDefault();
    this.closed.emit();
  }
}
