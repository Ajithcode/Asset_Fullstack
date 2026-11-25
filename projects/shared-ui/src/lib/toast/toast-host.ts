import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, NgZone, OnDestroy, OnInit, inject } from '@angular/core';
import { TOAST_CONFIG, ToastItem, toastStore } from './toast';

interface Timer { handle?: ReturnType<typeof setTimeout>; remaining: number; startedAt: number }

/**
 * Bottom-right toasts. Mount once (shell layout). A toast closes by itself after TOAST_CONFIG.durationMs for its kind,
 * waits while the mouse is over it (or a keyboard focus is inside) and always has a close (×) button.
 */
@Component({
  standalone: true,
  selector: 'app-toast-host',
  imports: [CommonModule],
  template: `
    <div class="th" aria-live="polite" aria-atomic="false">
      <div
        *ngFor="let t of items; trackBy: track"
        class="th-toast"
        [class.th-toast--success]="t.kind === 'success'"
        [class.th-toast--info]="t.kind === 'info'"
        [class.th-toast--warning]="t.kind === 'warning'"
        [class.th-toast--error]="t.kind === 'error'"
        [attr.role]="t.kind === 'error' || t.kind === 'warning' ? 'alert' : 'status'"
        (mouseenter)="pause(t)"
        (mouseleave)="resume(t)"
        (focusin)="pause(t)"
        (focusout)="resume(t)"
      >
        <span class="th-msg">{{ t.message }}</span>
        <button type="button" class="th-x" aria-label="Close notification" (click)="close(t)">×</button>
      </div>
    </div>
  `,
  styles: `
    .th { position: fixed; right: 20px; bottom: 24px; z-index: 20000; display: flex; flex-direction: column; gap: 10px; width: min(420px, calc(100vw - 32px)); pointer-events: none; }
    .th-toast { pointer-events: auto; display: flex; align-items: flex-start; gap: 12px; padding: 12px 10px 12px 16px; border-radius: 10px; background: #1f2937; color: #fff; font-size: 0.875rem; line-height: 1.4; border-left: 5px solid #22c55e; box-shadow: 0 10px 28px rgba(15, 23, 42, .28); animation: th-in .18s ease-out; }
    .th-toast--info { border-left-color: #3b82f6; }
    .th-toast--warning { border-left-color: #f59e0b; }
    .th-toast--error { border-left-color: #ef4444; background: #7f1d1d; }
    .th-msg { flex: 1 1 auto; min-width: 0; overflow-wrap: anywhere; }
    .th-x { flex: 0 0 auto; width: 26px; height: 26px; margin-top: -3px; border: 0; border-radius: 6px; background: transparent; color: inherit; font-size: 1.25rem; line-height: 1; cursor: pointer; opacity: .75; }
    .th-x:hover, .th-x:focus-visible { opacity: 1; background: rgba(255, 255, 255, .16); }
    @keyframes th-in { from { opacity: 0; transform: translateY(8px); } to { opacity: 1; transform: none; } }
    @media (prefers-reduced-motion: reduce) { .th-toast { animation: none; } }
  `,
})
export class ToastHost implements OnInit, OnDestroy {
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly zone = inject(NgZone);
  private readonly timers = new Map<number, Timer>();
  private unsubscribe?: () => void;
  items: ToastItem[] = [];

  ngOnInit(): void {
    const store = toastStore();
    const sync = () => {
      this.items = store.items;
      for (const t of this.items) if (!this.timers.has(t.id)) this.start(t, TOAST_CONFIG.durationMs[t.kind]);
      for (const id of [...this.timers.keys()]) if (!this.items.some((t) => t.id === id)) this.stop(id);
      this.cdr.detectChanges();
    };
    this.unsubscribe = store.subscribe(() => this.zone.run(sync));
    sync();
  }

  ngOnDestroy(): void {
    this.unsubscribe?.();
    for (const id of [...this.timers.keys()]) this.stop(id);
  }

  track = (_: number, t: ToastItem) => t.id;

  private start(t: ToastItem, ms: number): void {
    const timer: Timer = { remaining: ms, startedAt: Date.now() };
    timer.handle = setTimeout(() => toastStore().dismiss(t.id), ms);
    this.timers.set(t.id, timer);
  }

  private stop(id: number): void {
    const timer = this.timers.get(id);
    if (timer?.handle) clearTimeout(timer.handle);
    this.timers.delete(id);
  }

  pause(t: ToastItem): void {
    const timer = this.timers.get(t.id);
    if (!timer?.handle) return;
    clearTimeout(timer.handle);
    timer.handle = undefined;
    timer.remaining = Math.max(1500, timer.remaining - (Date.now() - timer.startedAt));
  }

  resume(t: ToastItem): void {
    const timer = this.timers.get(t.id);
    if (!timer || timer.handle) return;
    timer.startedAt = Date.now();
    timer.handle = setTimeout(() => toastStore().dismiss(t.id), timer.remaining);
  }

  close(t: ToastItem): void {
    toastStore().dismiss(t.id);
  }
}
