import { ChangeDetectionStrategy, Component, ElementRef, EventEmitter, HostBinding, Input, Output, ViewEncapsulation, inject } from '@angular/core';

/**
 * The small details popup of a map object (gateway, zone "Total Assets" box, asset marker). Same frame, header, close button,
 * rows and placement as the popup of Administration › Organization & Locations › Maps & Floor Plans › Map Library
 * (asset-live-popup): 300 px card with a caret, above the object (below it near the top edge), kept inside the map.
 * Project the icon as `[mpIcon]`, the rows as the body and the buttons as `[mpActions]`.
 */
@Component({
  selector: 'app-map-popup',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.Default,
  encapsulation: ViewEncapsulation.None,
  template: `
    <span class="mp__caret" [style.left.px]="pos.caretX" aria-hidden="true"></span>
    <section class="mp" role="dialog" [attr.aria-label]="heading" (click)="$event.stopPropagation()" (mousedown)="$event.stopPropagation()" (dblclick)="$event.stopPropagation()" (wheel)="$event.stopPropagation()">
      <header class="mp__head">
        <span class="mp__icon"><ng-content select="[mpIcon]"></ng-content></span>
        <div class="mp__title">
          <strong [attr.title]="heading">{{ heading }}</strong>
          @if (subtitle) { <small [attr.title]="subtitle">{{ subtitle }}</small> }
        </div>
        @if (badge) { <span class="mp__badge" [class]="'mp__badge mp__badge--' + badgeTone"><i></i>{{ badge }}</span> }
        <button type="button" class="mp__close" (click)="close.emit()" aria-label="Close">
          <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><path d="M18 6 6 18M6 6l12 12"/></svg>
        </button>
      </header>
      <div class="mp__body"><ng-content></ng-content></div>
      <div class="mp__actions"><ng-content select="[mpActions]"></ng-content></div>
    </section>
  `,
  styles: `
    app-map-popup { position: absolute; z-index: 1100; width: 300px; max-width: calc(100% - 16px); pointer-events: none; }
    app-map-popup.above { transform: translate(-50%, calc(-100% - 22px)); }
    app-map-popup.below { transform: translate(-50%, 22px); }
    app-map-popup.docked { top: 12px !important; right: 12px; left: auto !important; transform: none; }
    app-map-popup.hidden { display: none; }
    app-map-popup .mp, app-map-popup .mp__caret { pointer-events: auto; }
    .mp__caret { position: absolute; width: 14px; height: 14px; margin-left: -7px; background: #fff; border: 1px solid #ececf3; transform: rotate(45deg); z-index: 1; }
    app-map-popup.above .mp__caret { bottom: -7px; border-top: 0; border-left: 0; }
    app-map-popup.below .mp__caret { top: -7px; border-bottom: 0; border-right: 0; }
    app-map-popup.docked .mp__caret { display: none; }
    .mp { position: relative; background: #fff; border: 1px solid #ececf3; border-radius: 12px; box-shadow: 0 14px 34px -12px rgba(30, 20, 80, 0.34), 0 2px 8px rgba(15, 23, 42, 0.06);
      overflow: hidden; animation: mp-in 0.2s cubic-bezier(0.21, 1.02, 0.73, 1) both; font-size: 0.781rem; color: #111827; text-align: left; line-height: 1.3; }
    .mp__head { display: grid; grid-template-columns: auto 1fr auto auto; align-items: center; gap: 8px; padding: 9px 6px 9px 10px; border-bottom: 1px solid #f1f1f6; }
    .mp__icon { display: grid; place-items: center; width: 30px; height: 30px; border-radius: 9px; background: #ede9fe; color: #5b3fd4; }
    .mp__icon svg { width: 18px; height: 18px; }
    .mp__title { min-width: 0; display: flex; flex-direction: column; }
    .mp__title strong { font-size: 0.844rem; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .mp__title small { color: #6b7280; font-size: 0.719rem; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .mp__title b { color: #111827; font-weight: 600; }
    .mp__badge { display: inline-flex; align-items: center; gap: 5px; padding: 2px 8px; border-radius: 999px; background: #f3f4f6; color: #6b7280; font-size: 0.688rem; font-weight: 600; white-space: nowrap; }
    .mp__badge i { width: 7px; height: 7px; border-radius: 50%; background: #9ca3af; }
    .mp__badge--ok { background: #dcfce7; color: #15803d; }
    .mp__badge--ok i { background: #16a34a; }
    .mp__badge--bad { background: #fee2e2; color: #b91c1c; }
    .mp__badge--bad i { background: #dc2626; }
    .mp__close { display: grid; place-items: center; width: 26px; height: 26px; border: 0; border-radius: 7px; background: transparent; color: #9ca3af; cursor: pointer; }
    .mp__close:hover { background: #f3f4f6; color: #111827; }
    .mp__body { max-height: 230px; overflow-y: auto; padding: 8px 10px 10px; display: flex; flex-direction: column; gap: 8px; }
    .mp__actions { display: flex; gap: 8px; padding: 0 10px 10px; }
    .mp__actions:empty { display: none; }
    .mp__acts { display: contents; }
    .mp__grid { display: grid; grid-template-columns: 1fr 1fr; gap: 7px 12px; margin: 0; padding: 2px 2px 0; }
    .mp__grid dt { font-size: 0.625rem; text-transform: uppercase; letter-spacing: 0.04em; color: #9ca3af; margin-bottom: 2px; }
    .mp__grid dd { margin: 0; display: flex; flex-direction: column; gap: 2px; font-weight: 600; min-width: 0; overflow-wrap: anywhere; }
    .mp__grid small { font-weight: 400; font-size: 0.688rem; }
    .mp__wide { grid-column: 1 / -1; }
    .mp__muted { color: #9ca3af; font-weight: 400; }
    .mp__mono { font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; font-size: 0.719rem; }
    .mp__h { display: flex; align-items: center; gap: 6px; padding: 2px 2px 0; margin: 0; font-size: 0.625rem; text-transform: uppercase; letter-spacing: 0.04em; color: #9ca3af; font-weight: 600; }
    .mp__n { display: inline-grid; place-items: center; min-width: 18px; height: 18px; padding: 0 5px; border-radius: 999px; background: #dc2626; color: #fff; font-size: 0.656rem; letter-spacing: 0; }
    .mp__asset { display: grid; grid-template-columns: auto 1fr; align-items: center; gap: 8px; padding: 7px 9px; border: 1px solid #f0f0f5; border-radius: 10px; background: #fcfcfe; }
    .mp__aic { display: grid; place-items: center; width: 26px; height: 26px; border-radius: 7px; background: #ede9fe; color: #5b3fd4; }
    .mp__ainfo { min-width: 0; display: flex; flex-direction: column; }
    .mp__ainfo strong { font-size: 0.781rem; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .mp__ainfo small { color: #6b7280; font-size: 0.688rem; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .mp__note { margin: 0 2px; color: #9ca3af; font-size: 0.719rem; }
    .mp__btn { flex: 1; display: inline-flex; align-items: center; justify-content: center; gap: 6px; height: 30px; padding: 0 10px; border: 1px solid #ddd6fe; border-radius: 8px;
      background: #fff; color: #5b3fd4; font: inherit; font-weight: 600; cursor: pointer; }
    .mp__btn:hover { background: #f5f3ff; }
    .mp__btn--primary { background: #5b3fd4; border-color: #5b3fd4; color: #fff; }
    .mp__btn--primary:hover { background: #4c35b3; }
    @keyframes mp-in { from { opacity: 0; transform: translateY(-6px) scale(0.98); } to { opacity: 1; transform: none; } }
    @media (prefers-reduced-motion: reduce) { .mp { animation: none; } }
  `,
})
export class MapPopup {
  @Input({ required: true }) heading = '';
  @Input() subtitle = '';
  /** Status pill in the header (e.g. Online / Offline). */
  @Input() badge = '';
  @Input() badgeTone: 'ok' | 'bad' | 'mute' = 'mute';
  /** The object's position in the map's pixels; null docks the popup in the map's top-right corner. */
  @Input() anchor: { x: number; y: number; inView: boolean } | null = null;
  @Output() close = new EventEmitter<void>();

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  /** Placement: centred on the object, kept inside the map's width; below it when there is no room above. */
  get pos(): { left: number; top: number; mode: 'above' | 'below' | 'docked' | 'hidden'; caretX: number } {
    const a = this.anchor;
    if (!a) return { left: 0, top: 0, mode: 'docked', caretX: 0 };
    if (!a.inView) return { left: 0, top: 0, mode: 'hidden', caretX: 0 };
    const el = this.host.nativeElement;
    const width = el.offsetWidth || 300;
    const height = el.offsetHeight || 240;
    const mapWidth = el.parentElement?.clientWidth ?? width;
    const half = width / 2;
    const left = mapWidth < width + 16 ? mapWidth / 2 : Math.min(Math.max(a.x, half + 8), mapWidth - half - 8);
    const caretX = Math.min(Math.max(half + (a.x - left), 16), width - 16);
    return { left, top: a.y, mode: a.y < height + 30 ? 'below' : 'above', caretX };
  }

  @HostBinding('style.left.px') get hostLeft(): number | null {
    return this.pos.mode === 'above' || this.pos.mode === 'below' ? this.pos.left : null;
  }

  @HostBinding('style.top.px') get hostTop(): number | null {
    return this.pos.mode === 'above' || this.pos.mode === 'below' ? this.pos.top : null;
  }

  @HostBinding('class') get hostClass(): string {
    return this.pos.mode;
  }
}
