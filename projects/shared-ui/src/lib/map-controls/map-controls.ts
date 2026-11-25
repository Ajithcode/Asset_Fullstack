import { ChangeDetectionStrategy, ChangeDetectorRef, Component, ElementRef, EventEmitter, HostBinding, HostListener, Input, OnDestroy, OnInit, Output, ViewEncapsulation, inject } from '@angular/core';

/**
 * Shared map control stack (+ / - zoom, Fit to screen, Full screen) for every map (IoT & Tracking: Real-Time Location, Geofence & Alerts,
 * Tracking & Telemetry, floor plan views). The map component places it inside its own box and handles `fit`;
 * Full screen is done here: it puts the map's card (`fullscreenHost`, else the nearest `.map-card`, else the parent)
 * into the browser's fullscreen mode, so the overlays on the map (floor picker, live panel...) stay with it.
 */
@Component({
  selector: 'app-map-controls',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
  template: `<div class="mc" [class.mc--inline]="inline" role="group" aria-label="Map view" [style.top.px]="inline ? null : top" [style.left.px]="inline ? null : left">
    @if (zoom) {
      <button type="button" class="mc-btn mc-btn--zoom" (click)="zoomIn.emit()" [disabled]="!canZoomIn" title="Zoom in" aria-label="Zoom in">
        <svg viewBox="0 0 24 24" width="17" height="17" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><path d="M12 5v14M5 12h14"/></svg>
      </button>
      <button type="button" class="mc-btn mc-btn--zoom" (click)="zoomOut.emit()" [disabled]="!canZoomOut" title="Zoom out" aria-label="Zoom out">
        <svg viewBox="0 0 24 24" width="17" height="17" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><path d="M5 12h14"/></svg>
      </button>
    }
    <button type="button" class="mc-btn" (click)="fit.emit()" title="Fit to screen" aria-label="Fit to screen">
      <svg viewBox="0 0 24 24" width="17" height="17" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 7V5a2 2 0 0 1 2-2h2"/><path d="M17 3h2a2 2 0 0 1 2 2v2"/><path d="M21 17v2a2 2 0 0 1-2 2h-2"/><path d="M7 21H5a2 2 0 0 1-2-2v-2"/><rect x="8" y="8" width="8" height="8" rx="1"/></svg>
    </button>
    <button type="button" class="mc-btn" (click)="toggleFullscreen()" [title]="full ? 'Exit full screen' : 'Full screen'" [attr.aria-label]="full ? 'Exit full screen' : 'Full screen'" [attr.aria-pressed]="full">
      @if (full) {
        <svg viewBox="0 0 24 24" width="17" height="17" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M8 3v3a2 2 0 0 1-2 2H3"/><path d="M21 8h-3a2 2 0 0 1-2-2V3"/><path d="M3 16h3a2 2 0 0 1 2 2v3"/><path d="M16 21v-3a2 2 0 0 1 2-2h3"/></svg>
      } @else {
        <svg viewBox="0 0 24 24" width="17" height="17" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M8 3H5a2 2 0 0 0-2 2v3"/><path d="M21 8V5a2 2 0 0 0-2-2h-3"/><path d="M3 16v3a2 2 0 0 0 2 2h3"/><path d="M16 21h3a2 2 0 0 0 2-2v-3"/></svg>
      }
    </button>
  </div>`,
  styles: `
    /* one vertical stack in the map's top-left corner: + / - / Fit to screen / Full screen (same 32 px buttons) */
    .mc:not(.mc--inline) { position: absolute; z-index: 1100; display: flex; flex-direction: column; overflow: hidden; border-radius: 8px; background: #fff;
      border: 1px solid #e2e8f0; box-shadow: 0 2px 8px rgba(15, 23, 42, .18); }
    .mc--inline { display: contents; }
    .mc:not(.mc--inline) .mc-btn { display: grid; place-items: center; width: 32px; height: 32px; padding: 0; border: 0; border-bottom: 1px solid #eef2f7; background: none; color: #475569; cursor: pointer; }
    .mc:not(.mc--inline) .mc-btn:last-child { border-bottom: 0; }
    .mc:not(.mc--inline) .mc-btn:hover:not(:disabled) { background: #f5f3ff; color: #6d28d9; }
    .mc:not(.mc--inline) .mc-btn:disabled { opacity: .35; cursor: default; }
    .mc .mc-btn:focus-visible { outline: 2px solid #6d28d9; outline-offset: -2px; }
    /* the map card fills the screen in full screen mode */
    .mc-fs { background: #fff; width: 100% !important; height: 100% !important; max-height: none !important; border-radius: 0 !important; }
    .mc-fs .map-wrap { height: 100% !important; border-radius: 0 !important; }
  `,
})
export class MapControls implements OnInit, OnDestroy {
  /** Element put into fullscreen; default: the nearest `.map-card`, else the parent element. */
  @Input() fullscreenHost: HTMLElement | null = null;
  /** Distance from the top-left corner of the map, in px. */
  @Input() top = 10;
  @Input() left = 10;
  /** Show the + / - zoom buttons at the top of the stack (the map handles `zoomIn` / `zoomOut`). */
  @Input() zoom = false;
  @Input() canZoomIn = true;
  @Input() canZoomOut = true;
  /** Only the Fit / Full screen buttons, no frame or position: they join a stack the page already draws (same button style as its own). */
  @Input() inline = false;
  @HostBinding('style.display') get hostDisplay(): string | null {
    return this.inline ? 'contents' : null;
  }
  @Output() zoomIn = new EventEmitter<void>();
  @Output() zoomOut = new EventEmitter<void>();
  /** Fit the map to its plan / content (the map decides what that is). */
  @Output() fit = new EventEmitter<void>();
  /** Emits after the fullscreen state changed, so the map can recalculate its size. */
  @Output() fullscreenChange = new EventEmitter<boolean>();

  full = false;
  private readonly el = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly cdr = inject(ChangeDetectorRef);
  private target: HTMLElement | null = null;

  ngOnInit(): void {
    this.sync();
  }

  ngOnDestroy(): void {
    if (this.full && typeof document !== 'undefined' && document.fullscreenElement === this.target) void document.exitFullscreen?.().catch(() => undefined);
  }

  private host(): HTMLElement | null {
    const own = this.el.nativeElement;
    return this.fullscreenHost ?? (own.closest('.map-card') as HTMLElement | null) ?? own.parentElement;
  }

  toggleFullscreen(): void {
    if (typeof document === 'undefined') return;
    if (document.fullscreenElement) {
      void document.exitFullscreen().catch(() => undefined);
      return;
    }
    const t = this.host();
    if (!t?.requestFullscreen) return;
    this.target = t;
    t.classList.add('mc-fs');
    t.requestFullscreen().catch(() => t.classList.remove('mc-fs'));
  }

  @HostListener('document:fullscreenchange')
  sync(): void {
    if (typeof document === 'undefined') return;
    const on = !!this.target && document.fullscreenElement === this.target;
    if (!on) this.target?.classList.remove('mc-fs');
    if (on !== this.full) {
      this.full = on;
      this.cdr.markForCheck();
      // the browser resizes the card after this event: let the map follow it
      setTimeout(() => this.fullscreenChange.emit(on), 60);
    }
  }
}
