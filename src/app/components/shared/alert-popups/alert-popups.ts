import { Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { AlertFeedService, DesktopAlertMessage, DesktopAlertsService } from 'shared-ui';

interface Popup {
  alert: DesktopAlertMessage;
  receivedAt: Date;
  busy: '' | 'acknowledge' | 'remind';
  error: string;
}

/**
 * In-app pop-ups for live IoT alerts (pushed by the Tracking service, forwarded by the service worker): one card per
 * alert, top right, with Acknowledge and Remind me. A reminder of the same alert updates its card instead of adding one.
 */
@Component({
  selector: 'app-alert-popups',
  standalone: true,
  template: `
    <div class="ap-stack" aria-live="assertive">
      @for (p of popups; track p.alert.alertId) {
        <section class="ap-card" role="alertdialog" [attr.aria-label]="p.alert.title" [attr.data-sev]="sevClass(p.alert.severity)">
          <header class="ap-head">
            <span class="ap-icon" aria-hidden="true">
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3"/><path d="M12 9v4"/><path d="M12 17h.01"/></svg>
            </span>
            <div class="ap-titles">
              <strong>{{ p.alert.title }}</strong>
              <small>{{ p.alert.alertId }} · {{ p.alert.severity || 'Alert' }} · {{ time(p) }}@if (p.alert.number > 1) { · reminder {{ p.alert.number - 1 }} }</small>
            </div>
            <button type="button" class="ap-x" (click)="close(p)" aria-label="Close">×</button>
          </header>
          <p class="ap-body">{{ p.alert.body }}</p>
          @if (p.error) { <p class="ap-error" role="alert">{{ p.error }}</p> }
          <footer class="ap-actions">
            <button type="button" class="ap-link" (click)="open(p)">View</button>
            <span class="ap-grow"></span>
            @if (p.alert.actionToken) { <button type="button" class="ap-btn ap-btn--ghost" [disabled]="!!p.busy" (click)="act(p, 'remind')">{{ p.busy === 'remind' ? 'Saving…' : 'Remind me' }}</button> }
            <button type="button" class="ap-btn" [disabled]="!!p.busy" (click)="act(p, 'acknowledge')">{{ p.busy === 'acknowledge' ? 'Saving…' : 'Acknowledge' }}</button>
          </footer>
        </section>
      }
    </div>
  `,
  styles: `
    .ap-stack { position: fixed; top: 92px; right: 20px; z-index: 3000; display: flex; flex-direction: column; gap: 12px; width: min(380px, calc(100vw - 32px)); pointer-events: none; }
    .ap-card { pointer-events: auto; position: relative; overflow: hidden; padding: 14px 16px 12px 18px; border-radius: 14px; background: #fff;
      box-shadow: 0 18px 40px rgba(30, 16, 70, .22), 0 2px 6px rgba(15, 23, 42, .08); border: 1px solid #ede9fe; animation: ap-in .28s cubic-bezier(.2, .9, .3, 1.2); }
    .ap-card::before { content: ''; position: absolute; inset: 0 auto 0 0; width: 5px; background: var(--c, #6d28d9); }
    .ap-card[data-sev='critical'] { --c: #dc2626; --bg: #fef2f2; }
    .ap-card[data-sev='warning'] { --c: #d97706; --bg: #fffbeb; }
    .ap-card[data-sev='info'] { --c: #2563eb; --bg: #eff6ff; }
    .ap-head { display: flex; align-items: flex-start; gap: 10px; }
    .ap-icon { display: grid; place-items: center; flex: 0 0 34px; height: 34px; border-radius: 10px; color: var(--c, #6d28d9); background: var(--bg, #f5f3ff); }
    .ap-titles { flex: 1; min-width: 0; }
    .ap-titles strong { display: block; font-size: 0.875rem; color: #0f172a; line-height: 1.3; }
    .ap-titles small { display: block; margin-top: 2px; font-size: 0.719rem; color: #64748b; }
    .ap-x { border: 0; background: none; font-size: 1.25rem; line-height: 1; color: #94a3b8; cursor: pointer; padding: 0 2px; }
    .ap-x:hover { color: #0f172a; }
    .ap-body { margin: 8px 0 0 44px; font-size: 0.781rem; line-height: 1.45; color: #334155; }
    .ap-error { margin: 6px 0 0 44px; font-size: 0.75rem; color: #b91c1c; }
    .ap-actions { display: flex; align-items: center; gap: 8px; margin: 12px 0 0 44px; }
    .ap-grow { flex: 1; }
    .ap-link { border: 0; background: none; padding: 0; font: inherit; font-size: 0.781rem; font-weight: 600; color: #6d28d9; cursor: pointer; }
    .ap-btn { padding: 7px 14px; border: 1px solid #5b21b6; border-radius: 9px; background: #5b21b6; color: #fff; font: inherit; font-size: 0.781rem; font-weight: 600; cursor: pointer; }
    .ap-btn:hover:not(:disabled) { background: #4c1d95; }
    .ap-btn--ghost { background: #fff; color: #5b21b6; border-color: #ddd6fe; }
    .ap-btn--ghost:hover:not(:disabled) { background: #f5f3ff; }
    .ap-btn:disabled { opacity: .6; cursor: default; }
    @keyframes ap-in { from { opacity: 0; transform: translateX(24px) scale(.97); } to { opacity: 1; transform: none; } }
    @media (max-width: 640px) { .ap-stack { top: 76px; right: 16px; } }
  `,
})
export class AlertPopups {
  private readonly alerts = inject(DesktopAlertsService);
  private readonly feed = inject(AlertFeedService);
  private readonly router = inject(Router);
  popups: Popup[] = [];

  constructor() {
    this.alerts.alerts$.pipe(takeUntilDestroyed(inject(DestroyRef))).subscribe((alert) => {
      const card: Popup = { alert, receivedAt: new Date(), busy: '', error: '' };
      // newest on top; a reminder replaces the earlier card of the same alert
      this.popups = [card, ...this.popups.filter((p) => p.alert.alertId !== alert.alertId)].slice(0, 4);
    });
  }

  async act(p: Popup, action: 'acknowledge' | 'remind'): Promise<void> {
    p.busy = action;
    p.error = '';
    // alerts from the 10 s feed have no push token: acknowledge them through the alert API
    const ok = p.alert.actionToken ? await this.alerts.act(p.alert, action) : await this.feed.acknowledge({ alertId: p.alert.alertId } as never);
    p.busy = '';
    if (ok) this.close(p);
    else p.error = action === 'acknowledge' ? 'Could not acknowledge the alert. Try again.' : 'Could not set the reminder. Try again.';
  }

  open(p: Popup): void {
    this.close(p);
    void this.router.navigateByUrl('/tracking-iot/geofence-alerts');
  }

  close(p: Popup): void {
    this.popups = this.popups.filter((x) => x !== p);
  }

  time(p: Popup): string {
    const d = p.alert.raisedAt ? new Date(p.alert.raisedAt) : p.receivedAt;
    return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }

  sevClass(s?: string): string {
    return /critical|high/i.test(s ?? '') ? 'critical' : /medium|warn/i.test(s ?? '') ? 'warning' : 'info';
  }
}
