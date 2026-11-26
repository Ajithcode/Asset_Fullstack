import { AfterViewInit, Component, ElementRef, EventEmitter, HostListener, Input, NgZone, Output, OnChanges, OnDestroy, SimpleChanges, ViewChild, ViewEncapsulation, inject } from '@angular/core';
import type * as Leaflet from 'leaflet';
import { MapControls, MapPopup, MapViewPrefsService } from 'shared-ui';
import { GatewayFeed, PlanPlot, PlanZone } from './floor-map-api.service';

type L = typeof Leaflet;

/** The asset followed live (Real-Time Location › Locate asset): an estimated point on the plan, in % of the plan. */
export interface LivePoint {
  x: number;
  y: number;
  label: string;
  /** Live, Stale or Offline: Stale / Offline are drawn greyed out. */
  state: string;
  /** The asset's id: when its marker is drawn in a zone, the point sits on that marker (same in-zone grid spot) instead of on x / y. */
  assetId?: string;
}

/** Zoom request: the map flies to the point whenever `key` changes. */
export interface MapFocus {
  key: string;
  x: number;
  y: number;
}

/** Inputs that only move the live marker: they never redraw the plan. */
const LIVE_INPUTS = new Set(['live', 'liveTrail', 'focus', 'signal', 'assetMarkers', 'assetFocus']);

/** An asset drawn inside its current zone (Real-Time Location). BLE gives the zone only, so the spot in the zone is a stable layout. */
export interface PlanAsset {
  id: string;
  name: string;
  code: string;
  zoneId: string;
  zoneName: string;
  /** Moving (changed zone in the last minute), Stationary or Unknown (no live location). */
  state: 'Moving' | 'Stationary' | 'Unknown';
  /** Tag id and last valid location time, shown in the click popup. */
  tag?: string;
  lastSeenAt?: string | null;
}

/** Highlight these assets (ripple + bounce for ~3 s, then a steady ring) and fit the view to their zones; new `key` = new request. */
export interface AssetFocus {
  key: string;
  ids: string[];
}

interface PopupTarget {
  kind: 'gateway' | 'zone' | 'asset' | 'plot';
  id: string;
}

interface PopupView {
  kind: 'gateway' | 'zone' | 'asset' | 'plot';
  title: string;
  subtitle: string;
  badge: string;
  tone: 'ok' | 'bad' | 'mute';
  rows: [string, string][];
  /** Zone popup: the first five asset names, the total and how many are not listed. */
  names: string[];
  total: number;
  more: number;
  /** Asset popup: show Locate / LED / Buzzer. */
  actions: boolean;
}

/** The zone colour ~30% darker (borders, label chips); anything that is not a hex colour is returned unchanged. */
const darken = (c: string, f = 0.7): string => {
  const m = /^#?([0-9a-f]{3}|[0-9a-f]{6})$/i.exec(c.trim());
  if (!m) return c;
  const h = m[1].length === 3 ? m[1].split('').map((x) => x + x).join('') : m[1];
  const ch = [0, 2, 4].map((i) => Math.round(parseInt(h.slice(i, i + 2), 16) * f).toString(16).padStart(2, '0'));
  return '#' + ch.join('');
};
/** Glide of an asset marker between zones (ease-in-out). */
const GLIDE_MS = 1800;
/** Asset icon size on screen, and the zoom below which every zone with 2+ assets shows one badge. */
const ICON_PX = 30;
/** Zoom below which the zone name chips are shortened. */
const SHORT_NAME_ZOOM = -1;
const CLUSTER_ZOOM = -1.25;
const reducedMotion = (): boolean => typeof window !== 'undefined' && !!window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
const STATE_COLOR = { Moving: '#16a34a', Stationary: '#6d28d9', Unknown: '#94a3b8' } as const;

/** Plan width in map units; the height follows the image's aspect ratio. */
const W = 1000;

/** Same items in the same order (case-insensitive). */
const sameList = (a: string[] | null | undefined, b: string[] | null | undefined) =>
  (a ?? []).length === (b ?? []).length && (a ?? []).every((x, i) => x.toLowerCase() === (b ?? [])[i]?.toLowerCase());
const esc = (s: string) => s.replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]!);

/** Trolley: handle, basket and two wheels (inline, filled with the state colour --c). */
const TROLLEY_SVG =
  '<svg viewBox="0 0 24 24" aria-hidden="true"><path class="tr-halo" d="M2.4 4.6h2.5l1.4 3"/><path class="tr-handle" d="M2.4 4.6h2.5l1.4 3"/>' +
  '<path class="tr-fill" d="M5.6 7.4h15.2l-1.9 6.5a1.7 1.7 0 0 1-1.6 1.2H9.4a1.7 1.7 0 0 1-1.6-1.3L5.6 7.4Z"/>' +
  '<circle class="tr-fill" cx="9.6" cy="19" r="1.8"/><circle class="tr-fill" cx="17" cy="19" r="1.8"/></svg>';

const DEVICE_SVG =
  '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M4.9 19.1a10 10 0 0 1 0-14.2"/><path d="M7.8 16.2a6 6 0 0 1 0-8.4"/><circle cx="12" cy="12" r="2"/><path d="M16.2 7.8a6 6 0 0 1 0 8.4"/><path d="M19.1 4.9a10 10 0 0 1 0 14.2"/></svg>';
const ASSET_SVG =
  '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 8a2 2 0 0 0-1-1.7l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.7l7 4a2 2 0 0 0 2 0l7-4a2 2 0 0 0 1-1.7Z"/><path d="m3.3 7 8.7 5 8.7-5"/><path d="M12 22V12"/></svg>';

/**
 * The floor's plan from Maps & Floor Plans on the Geofence & Alerts page: the plan image, the zones drawn on it, the
 * devices and assets plotted on it, and the live "Total Assets" count of each zone (assets its gateways hear).
 * Floor view shows everything on the floor; with `zoneId` set only that zone and what is plotted in it.
 */
@Component({
  selector: 'app-floor-plan-map',
  standalone: true,
  imports: [MapControls, MapPopup],
  template: `<div #host class="fp-map" [class.fp-map--blank]="imageMissing"></div>
    <app-map-controls [zoom]="true" (zoomIn)="zoomBy(0.5)" (zoomOut)="zoomBy(-0.5)" (fit)="fitView()" (fullscreenChange)="refit()"></app-map-controls>
    @if (view; as v) {
      <app-map-popup [heading]="v.title" [subtitle]="v.subtitle" [badge]="v.badge" [badgeTone]="v.tone" [anchor]="anchor" (close)="closePopup()">
        <svg mpIcon viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
          @switch (v.kind) {
            @case ('gateway') { <path d="M4.9 19.1a10 10 0 0 1 0-14.2"/><path d="M7.8 16.2a6 6 0 0 1 0-8.4"/><circle cx="12" cy="12" r="2"/><path d="M16.2 7.8a6 6 0 0 1 0 8.4"/><path d="M19.1 4.9a10 10 0 0 1 0 14.2"/> }
            @case ('zone') { <path d="M20 10c0 6-8 12-8 12s-8-6-8-12a8 8 0 0 1 16 0Z"/><circle cx="12" cy="10" r="3"/> }
            @default { <path d="M21 8a2 2 0 0 0-1-1.7l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.7l7 4a2 2 0 0 0 2 0l7-4a2 2 0 0 0 1-1.7Z"/><path d="m3.3 7 8.7 5 8.7-5"/><path d="M12 22V12"/> }
          }
        </svg>
        @if (v.kind === 'zone') {
          <h4 class="mp__h">Total assets <span class="mp__n">{{ v.total }}</span></h4>
          @for (n of v.names; track n) {
            <article class="mp__asset"><span class="mp__aic"><svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M21 8a2 2 0 0 0-1-1.7l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.7l7 4a2 2 0 0 0 2 0l7-4a2 2 0 0 0 1-1.7Z"/></svg></span><div class="mp__ainfo"><strong>{{ n }}</strong></div></article>
          }
          @if (v.more) { <p class="mp__note">+ {{ v.more }} more</p> }
        } @else {
          <dl class="mp__grid">
            @for (r of v.rows; track r[0]) {
              <div [class.mp__wide]="r[0] === 'Last seen'"><dt>{{ r[0] }}</dt><dd>{{ r[1] }}</dd></div>
            }
          </dl>
        }
        @if (v.kind === 'asset' && v.actions) {
          <div mpActions class="mp__acts">
            <button type="button" class="mp__btn mp__btn--primary" (click)="popupAction('locate')">Locate</button>
            <button type="button" class="mp__btn" (click)="popupAction('signal')">LED / Buzzer</button>
          </div>
        }
      </app-map-popup>
    }
    @if (note) {
      <div class="fp-note" role="status">{{ note }}</div>
    } @else if (imageMissing) {
      <div class="fp-note fp-note--top" role="status">Floor plan image is missing - upload it again in Maps &amp; Floor Plans. Zones and items are shown on a blank plan.</div>
    }`,
  styles: `
    app-floor-plan-map { position: absolute; inset: 0; display: block; }
    .fp-map { position: absolute; inset: 0; background: #f8fafc; }
    .fp-map .leaflet-container, .fp-map.leaflet-container { background: #f8fafc; font: inherit; }
    .fp-note { position: absolute; left: 50%; top: 50%; transform: translate(-50%, -50%); z-index: 500; padding: 10px 16px; border-radius: 10px;
      background: #fff; box-shadow: 0 6px 24px rgba(15, 23, 42, .12); color: #475569; font-size: 0.813rem; text-align: center; max-width: 80%; }
    .fp-note--top { top: 14px; transform: translateX(-50%); padding: 7px 14px; font-size: 0.75rem; border: 1px solid #fde68a; background: #fffbeb; color: #92400e; }
    .fp-map--blank { background-color: #fff; background-image: linear-gradient(#eef2f7 1px, transparent 1px), linear-gradient(90deg, #eef2f7 1px, transparent 1px);
      background-size: 32px 32px; }
    .fp-zone-shape { cursor: zoom-in; }
    .fp-pin { display: grid; place-items: center; width: 30px; height: 30px; border-radius: 50%; border: 2px solid #fff; color: #fff;
      box-shadow: 0 0 0 1px rgba(15, 23, 42, .45), 0 3px 8px rgba(15, 23, 42, .4); transition: transform .15s; }
    .fp-pin:hover { transform: scale(1.12); }
    .fp-pin--device { background: #059669; }
    .fp-pin--asset { background: #7c3aed; }
    .fp-pin--hl { box-shadow: 0 0 0 4px rgba(124, 58, 237, .35), 0 0 0 9px rgba(124, 58, 237, .15), 0 3px 10px rgba(15, 23, 42, .28); transform: scale(1.15); }
    .fp-pin-label { position: absolute; top: 32px; left: 50%; transform: translateX(-50%); white-space: nowrap; padding: 1px 6px; border-radius: 6px;
      background: rgba(255, 255, 255, .92); color: #0f172a; font-size: 0.688rem; font-weight: 600; }
    .fp-total { display: inline-flex; align-items: center; gap: 6px; padding: 4px 10px; border-radius: 999px; background: #fff; color: #4c1d95;
      border: 1.5px solid #c4b5fd; box-shadow: 0 4px 14px rgba(76, 29, 149, .18); font-size: 0.719rem; font-weight: 600; white-space: nowrap;
      transform: translate(-50%, -50%); }
    .fp-total b { display: inline-grid; place-items: center; min-width: 20px; height: 20px; padding: 0 5px; border-radius: 999px; background: #6d28d9; color: #fff; font-size: 0.688rem; }
    /* zone name tab: outside the zone, its edge against the zone's border; constant size, never clickable */
    .fp-zone-label { position: absolute; left: 0; top: 0; display: block; max-width: none; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; padding: 2px 9px; background: var(--c); color: #fff;
      font-size: 0.719rem; font-weight: 700; line-height: 1.25; pointer-events: none; box-shadow: 0 1px 4px rgba(15, 23, 42, .35); }
    .fp-zl--top { transform: translate(0, -100%); border-radius: 6px 6px 0 0; }
    .fp-zl--bottom { transform: none; border-radius: 0 0 6px 6px; }
    .fp-zl--left { transform: translate(-100%, 0); border-radius: 6px 0 0 6px; }
    .fp-zl--right { transform: none; border-radius: 0 6px 6px 0; }
    .fp-zl--short { max-width: 72px; }
    .fp-no-zn .fp-zone-label { display: none; }
    .fp-live-marker { transition: transform 1.6s ease-out; }
    .leaflet-zoom-anim .fp-live-marker { transition: none; }
    .fp-live { position: relative; display: grid; place-items: center; width: 22px; height: 22px; }
    .fp-live i { position: absolute; inset: 0; border-radius: 50%; background: rgba(220, 38, 38, .35); animation: fp-live-pulse 1.6s ease-out infinite; }
    .fp-live b { position: relative; width: 14px; height: 14px; border-radius: 50%; background: #dc2626; border: 3px solid #fff; box-shadow: 0 2px 8px rgba(15, 23, 42, .4); }
    .fp-live span { position: absolute; top: 24px; left: 50%; transform: translateX(-50%); white-space: nowrap; padding: 2px 8px; border-radius: 6px;
      background: #dc2626; color: #fff; font-size: 0.688rem; font-weight: 700; box-shadow: 0 2px 6px rgba(15, 23, 42, .25); }
    .fp-signal { position: relative; display: grid; place-items: center; width: 34px; height: 34px; }
    .fp-signal i { position: absolute; inset: 0; border-radius: 50%; background: rgba(245, 158, 11, .45); animation: fp-signal 0.9s ease-in-out infinite; }
    .fp-signal b { position: relative; display: grid; place-items: center; width: 24px; height: 24px; border-radius: 50%; background: #f59e0b;
      border: 3px solid #fff; box-shadow: 0 2px 8px rgba(15, 23, 42, .4); color: #fff; font-size: 0.813rem; line-height: 1; }
    .fp-signal span { position: absolute; top: 36px; left: 50%; transform: translateX(-50%); white-space: nowrap; padding: 2px 8px; border-radius: 6px;
      background: #b45309; color: #fff; font-size: 0.688rem; font-weight: 700; box-shadow: 0 2px 6px rgba(15, 23, 42, .25); }
    .fp-signal--wait i { animation: none; background: rgba(148, 163, 184, .35); }
    .fp-signal--wait b { background: #94a3b8; }
    @keyframes fp-signal { 0%, 100% { transform: scale(.7); opacity: .25; } 50% { transform: scale(1.6); opacity: 1; } }
    .fp-live--stale i { animation: none; background: rgba(100, 116, 139, .25); }
    .fp-live--stale b, .fp-live--stale span { background: #64748b; }
    @keyframes fp-live-pulse { 0% { transform: scale(.6); opacity: 1; } 100% { transform: scale(2.6); opacity: 0; } }
    /* assets inside their zone (Real-Time Location) */
    /* a glide between zones is an inline transform transition set by the map (updateAssets), so zoom / refit / first draw never animate */
    .fp-asset-marker { will-change: transform; }
    .leaflet-zoom-anim .fp-asset-marker { transition: none !important; }
    /* the asset marker is a small trolley, constant 30 px on screen at every zoom (--fp-am) */
    .fp-am { position: absolute; left: 50%; top: 50%; display: block; width: var(--fp-am, 24px); height: var(--fp-am, 24px);
      margin: calc(var(--fp-am, 24px) / -2) 0 0 calc(var(--fp-am, 24px) / -2); }
    .fp-am svg { position: absolute; inset: 0; width: 100%; height: 100%; overflow: visible; filter: drop-shadow(0 1px 1.5px rgba(15, 23, 42, .6)); shape-rendering: geometricPrecision; }
    .fp-am .tr-fill { fill: var(--c); stroke: #fff; stroke-width: 1.7; stroke-linejoin: round; paint-order: stroke; }
    .fp-am--hl .tr-fill { stroke-width: 2.6; }
    .fp-am .tr-handle { fill: none; stroke: var(--c); stroke-width: 2.3; stroke-linecap: round; stroke-linejoin: round; }
    .fp-am .tr-halo { fill: none; stroke: #fff; stroke-width: 4.6; stroke-linecap: round; stroke-linejoin: round; }
    .fp-am-lbl { display: none; position: absolute; top: 100%; left: 50%; transform: translateX(-50%); margin-top: 2px; white-space: nowrap; padding: 1px 6px; border-radius: 6px;
      background: rgba(255, 255, 255, .94); color: #0f172a; font-size: 0.688rem; font-weight: 600; text-decoration: none; box-shadow: 0 1px 3px rgba(15, 23, 42, .25); pointer-events: none; }
    .fp-labels .fp-am-lbl { display: block; }
    .fp-cl { position: absolute; left: 50%; top: 50%; width: var(--d, 34px); height: var(--d, 34px); margin: calc(var(--d, 34px) / -2) 0 0 calc(var(--d, 34px) / -2); display: grid; place-items: center;
      box-sizing: border-box; border-radius: 50%; background: #6d28d9; color: #fff; border: 2px solid #fff; font-size: 0.813rem; font-weight: 800; cursor: pointer;
      box-shadow: 0 0 0 1px rgba(15, 23, 42, .4), 0 3px 8px rgba(15, 23, 42, .45); }
    .fp-cl:hover { background: #5b21b6; }
    .fp-am i { position: absolute; inset: 0; border-radius: 50%; opacity: 0; pointer-events: none; }
    .fp-am u { position: absolute; inset: -4px; border-radius: 50%; border: 2px solid #6d28d9; background: rgba(109, 40, 217, .12); opacity: 0; pointer-events: none; text-decoration: none; }
    .fp-am--moving i.r1 { background: rgba(22, 163, 74, .35); animation: fp-am-pulse 1.8s ease-out infinite; }
    .fp-am--hl { --c: #6d28d9 !important; }
    .fp-am--hl u { opacity: 1; border-width: 3px; animation: fp-am-ring 1.4s ease-in-out infinite; }
    .fp-am--hl-anim i { background: none; border: 2px solid #6d28d9; opacity: 1; animation: fp-am-ripple 1.5s ease-out infinite; }
    .fp-am--hl-anim i.r2 { animation-delay: .75s; }
    .fp-am--hl-anim svg { animation: fp-am-bounce .9s ease-in-out 3; }
    .fp-trail { animation: fp-trail-fade 8s ease-out forwards; }
    @keyframes fp-am-ring { 0%, 100% { transform: scale(1); } 50% { transform: scale(1.18); } }
    @keyframes fp-am-pulse { 0% { transform: scale(.7); opacity: .9; } 100% { transform: scale(2.4); opacity: 0; } }
    @keyframes fp-am-ripple { 0% { transform: scale(.6); opacity: .9; } 100% { transform: scale(3); opacity: 0; } }
    @keyframes fp-am-bounce { 0%, 100% { transform: translateY(0); } 40% { transform: translateY(-9px); } 70% { transform: translateY(-2px); } }
    @keyframes fp-trail-fade { from { opacity: .8; } to { opacity: 0; } }
    @media (prefers-reduced-motion: reduce) {
      .fp-asset-marker { transition: none; }
      .fp-am i, .fp-am svg, .fp-am u, .fp-trail { animation: none !important; }
      .fp-am i { display: none; }
    }
    .fp-tip strong { display: block; font-size: 0.781rem; }
    .fp-tip span { display: block; color: #64748b; font-size: 0.719rem; }
  `,
  encapsulation: ViewEncapsulation.None,
})
export class FloorPlanMap implements AfterViewInit, OnChanges, OnDestroy {
  /** Plan picture (floor plan file, or the floor image from Master Data). */
  @Input() imageUrl: string | null = null;
  /** Floor picture from Master Data, used when the plan file cannot be loaded. */
  @Input() fallbackUrl: string | null = null;
  @Input() zones: PlanZone[] = [];
  @Input() plots: PlanPlot[] = [];
  /** Zone view: the drawn zone shown alone; undefined = whole floor; null = the picked zone is not drawn yet. */
  @Input() zoneId: string | null | undefined = undefined;
  /** Live assets per gateway, keyed by gateway id in lower case. */
  @Input() feed: Record<string, GatewayFeed> = {};
  @Input() showLabels = true;
  @Input() showAssets = true;
  @Input() showDevices = true;
  /** Device ids (any case) to emphasise, e.g. the gateway hearing the selected asset. */
  @Input() highlight: string[] = [];
  /** Shown over the map (e.g. loading / no floor plan). */
  @Input() note = '';
  /** The asset followed live; null = none. Moves without redrawing the plan. */
  @Input() live: LivePoint | null = null;
  /** Where the live asset has been since following started (oldest first). */
  @Input() liveTrail: { x: number; y: number }[] = [];
  /** An asset whose tag is blinking / beeping (Locate asset › LED / buzzer); state "Live" pulses, anything else waits. */
  @Input() signal: LivePoint | null = null;
  /** Fly to a point (zoom in) whenever its key changes. */
  @Input() focus: MapFocus | null = null;
  /** Assets drawn inside their zone (stable layout, glide to the new zone when it changes). */
  @Input() assetMarkers: PlanAsset[] = [];
  /** Highlight + fit to these assets. */
  @Input() assetFocus: AssetFocus | null = null;
  /** The "Total Assets" bubble in the middle of each zone (off where the assets themselves are drawn). */
  @Input() showTotals = true;
  @Output() assetSelect = new EventEmitter<string>();
  /** Popup buttons of an asset marker (Real-Time Location): Locate, and LED / Buzzer (the page opens its existing panel). */
  @Output() assetLocate = new EventEmitter<string>();
  @Output() assetSignal = new EventEmitter<string>();

  @ViewChild('host', { static: true }) private host!: ElementRef<HTMLDivElement>;
  private readonly ngZone = inject(NgZone);
  private readonly viewPrefs = inject(MapViewPrefsService);
  private viewSub?: { unsubscribe(): void };
  private L?: L;
  private map?: Leaflet.Map;
  private image?: Leaflet.ImageOverlay;
  private shapes?: Leaflet.LayerGroup;
  private markers?: Leaflet.LayerGroup;
  private liveLayer?: Leaflet.LayerGroup;
  private assetLayer?: Leaflet.LayerGroup;
  private assetMk = new Map<string, { m: Leaflet.Marker; zoneId: string; state: string; name: string; code: string }>();
  /** Zones drawn on the current floor / zone view (set by updateAssets, read by the clustering). */
  private curZones = new Map<string, PlanZone>();
  private clusterMk: Leaflet.Marker[] = [];
  private zoneLabels: { z: PlanZone; m: Leaflet.Marker; b: Leaflet.LatLngBounds }[] = [];
  private reclusterTimer?: ReturnType<typeof setTimeout>;
  /** An asset that changed zone keeps counting in its OLD zone / cluster until its glide ends (then both counts update). */
  private zoneHold = new Map<string, { zone: string; timer: ReturnType<typeof setTimeout>; flier?: Leaflet.Marker }>();
  /** Zones opened by clicking their cluster: shown split until the user zooms out again. */
  private expand?: { zones: Set<string>; zoom: number };
  private scopeBounds: Leaflet.LatLngBounds | null = null;
  private shownHl = '';
  private hlTimer?: ReturnType<typeof setTimeout>;
  private liveMarker?: Leaflet.Marker;
  private liveLine?: Leaflet.Polyline;
  private signalMarker?: Leaflet.Marker;
  private shownFocus = '';
  /** Plan height in map units (from the image's aspect ratio). */
  private h = W * 0.6;
  private shownImage: string | null = null;
  private shownScope: string | null | undefined = '\u0000';
  private resize?: ResizeObserver;
  private destroyed = false;

  async ngAfterViewInit(): Promise<void> {
    if (typeof window === 'undefined') return;
    const mod = (await import('leaflet')) as unknown as L & { default?: L };
    if (this.destroyed) return;
    const Lf: L = (typeof (mod as { map?: unknown }).map === 'function' ? mod : mod.default) as L;
    this.L = Lf;
    this.ngZone.runOutsideAngular(() => {
      const map = Lf.map(this.host.nativeElement, { crs: Lf.CRS.Simple, minZoom: -3, maxZoom: 3, zoomSnap: 0.25, zoomDelta: 0.5, attributionControl: false, zoomControl: false });
      this.map = map;
      this.shapes = Lf.layerGroup().addTo(map);
      this.markers = Lf.layerGroup().addTo(map);
      this.liveLayer = Lf.layerGroup().addTo(map);
      // zone name chips: above the plan and zone fill, below the assets / gateways, never block a click
      const lp = map.createPane('fpZoneLabels');
      lp.style.zIndex = '450';
      lp.style.pointerEvents = 'none';
      this.assetLayer = Lf.layerGroup().addTo(map);
      map.setView([this.h / 2, W / 2], -1);
      // the popup follows its object, and closes on a click on the map or a drag (pan)
      // the trolley markers keep one size on screen at every zoom; a zoom / refit never glides them
      this.host.nativeElement.style.setProperty('--fp-am', '30px');
      map.on('zoomstart viewreset', () => this.stopGlides());
      map.on('zoomend moveend resize', () => this.scheduleRecluster());
      map.on('zoomend resize', () => this.layoutZoneLabels());
      map.on('move zoom resize', () => this.placePopup());
      map.on('click dragstart', () => this.closePopup());
      this.resize = new ResizeObserver(() => map.invalidateSize({ animate: false }));
      this.resize.observe(this.host.nativeElement);
      // floor plan / zone fill opacity (shared sliders): live, without a redraw
      this.viewSub = this.viewPrefs.prefs$.subscribe(() => this.ngZone.runOutsideAngular(() => this.applyView()));
      this.sync();
    });
  }

  /** Floor plan picture opacity and zone fill opacity from the shared sliders; borders, icons and labels are untouched. */
  private applyView(): void {
    const v = this.viewPrefs.value;
    this.host.nativeElement.classList.toggle('fp-no-zn', !v.names);
    this.image?.setOpacity(v.plan / 100);
    this.shapes?.eachLayer((l) => {
      const path = l as Leaflet.Path;
      if (path.options?.className === 'fp-zone-shape') path.setStyle({ fillOpacity: v.zone / 100 });
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    // A parent getter can hand over a new but identical highlight array on every change detection: that alone
    // must not clear and redraw the whole plan (it made the map flicker every second)
    const h = changes['highlight'];
    if (h && Object.keys(changes).length === 1 && sameList(h.previousValue, h.currentValue)) return;
    if (this.popup) this.rebuildView();
    if (!this.map) return;
    if (Object.keys(changes).every((k) => LIVE_INPUTS.has(k))) {
      this.ngZone.runOutsideAngular(() => {
        this.updateLive();
        this.updateAssets();
      });
      return;
    }
    this.ngZone.runOutsideAngular(() => this.sync());
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    clearTimeout(this.hlTimer);
    clearTimeout(this.reclusterTimer);
    this.abortMoves();
    clearInterval(this.viewTimer);
    this.viewSub?.unsubscribe();
    this.resize?.disconnect();
    this.map?.remove();
  }

  /** Loads a new image when it changed, then redraws. */
  private sync(): void {
    const L = this.L;
    const map = this.map;
    if (!L || !map) return;
    if (this.imageUrl !== this.shownImage) {
      this.shownImage = this.imageUrl;
      this.image?.remove();
      this.image = undefined;
      this.planReady = false;
      this.shapes!.clearLayers();
      this.markers!.clearLayers();
      this.liveLayer?.clearLayers();
      this.liveMarker = undefined;
      this.liveLine = undefined;
      this.signalMarker = undefined;
      this.shownFocus = '';
      const url = this.imageUrl;
      if (!url) return;
      this.setImageMissing(false);
      const img = new Image();
      img.onload = () => {
        if (url !== this.shownImage || !this.map) return;
        this.h = img.naturalWidth ? (W * img.naturalHeight) / img.naturalWidth : W * 0.6;
        const bounds = L.latLngBounds([0, 0], [this.h, W]);
        this.image = L.imageOverlay(img.src, bounds, { interactive: false, opacity: this.viewPrefs.value.plan / 100 }).addTo(map);
        this.image.bringToBack();
        this.ready(bounds);
      };
      // The file is gone (e.g. deleted from Media): use the floor picture from Master Data, else a blank plan
      img.onerror = () => {
        if (url !== this.shownImage || !this.map) return;
        if (this.fallbackUrl && img.src !== this.fallbackUrl && !img.dataset['fallback']) {
          img.dataset['fallback'] = '1';
          img.src = this.fallbackUrl;
          return;
        }
        this.h = W * 0.6;
        this.setImageMissing(true);
        this.ready(L.latLngBounds([0, 0], [this.h, W]));
      };
      img.src = url;
      return;
    }
    if (this.planReady) this.draw();
  }

  /** The plan (image or blank) is in place: zones and items can be drawn. */
  private planReady = false;
  /** The plan image could not be loaded. */
  imageMissing = false;

  private ready(bounds: Leaflet.LatLngBounds): void {
    this.planReady = true;
    this.map!.setMaxBounds(bounds.pad(0.4));
    this.shownScope = '\u0000';
    this.draw();
  }

  private setImageMissing(missing: boolean): void {
    if (missing !== this.imageMissing) this.ngZone.run(() => (this.imageMissing = missing));
  }

  /** % of the plan (x right, y down) → map point. */
  private at(x: number, y: number): [number, number] {
    return [this.h * (1 - y / 100), (W * x) / 100];
  }

  private draw(): void {
    const L = this.L!;
    const map = this.map!;
    this.shapes!.clearLayers();
    this.markers!.clearLayers();
    const zones = this.zoneId === undefined ? this.zones : this.zones.filter((z) => z.id === this.zoneId);
    const plots = (this.zoneId === undefined ? this.plots : this.plots.filter((p) => !!this.zoneId && p.zoneId === this.zoneId)).filter(
      (p) => (p.kind === 'device' ? this.showDevices : this.showAssets)
    );
    let focus: Leaflet.LatLngBounds | null = null;

    this.zoneLabels = [];
    for (const z of zones) {
      const style: Leaflet.PathOptions = { color: darken(z.color || '#7c3aed'), weight: 2.5, fillColor: z.color || '#7c3aed', fillOpacity: this.viewPrefs.value.zone / 100, dashArray: z.active ? undefined : '6 6', className: 'fp-zone-shape' };
      let shape: Leaflet.Path & { getBounds(): Leaflet.LatLngBounds };
      if (z.shape === 'Circle') {
        shape = L.circle(this.at(z.x + z.w / 2, z.y + z.h / 2), { ...style, radius: ((z.radius ?? z.w / 2) / 100) * W });
      } else if ((z.shape === 'Polygon' || z.shape === 'Custom') && z.points.length >= 3) {
        shape = L.polygon(z.points.map((p) => this.at(p.x, p.y)), style);
      } else {
        shape = L.rectangle([this.at(z.x, z.y + z.h), this.at(z.x + z.w, z.y)], style);
      }
      shape.bindTooltip(`<div class="fp-tip"><strong>${esc(z.name)} (${esc(z.code)})</strong><span>${esc(z.type)}</span></div>`, { sticky: true, direction: 'top', opacity: 1 });
      shape.addTo(this.shapes!);
      const b = shape.getBounds();
      // Click a zone: zoom in on it
      shape.on('click', () => map.flyToBounds(b, { padding: [40, 40], maxZoom: 2, duration: 0.6 }));
      focus = focus ? focus.extend(b) : L.latLngBounds(b.getSouthWest(), b.getNorthEast());
      // zone name: a small tab outside the zone (placed by layoutZoneLabels), in its own pane under the assets and gateways
      const chip = L.marker([b.getNorth(), b.getWest()], {
        pane: 'fpZoneLabels',
        interactive: false,
        keyboard: false,
        icon: L.divIcon({ className: '', html: `<span class="fp-zone-label" style="--c:${esc(darken(z.color || '#7c3aed'))}">${esc(z.name)}</span>`, iconSize: [0, 0] }),
      }).addTo(this.shapes!);
      this.zoneLabels.push({ z, m: chip, b: L.latLngBounds(b.getSouthWest(), b.getNorthEast()) });
      // live "Total Assets": distinct assets heard by the gateways plotted in this zone
      const names = this.zoneAssets(z.id);
      if (names.length && this.showTotals) {
        L.marker(b.getCenter(), {
          keyboard: false,
          zIndexOffset: -500,
          icon: L.divIcon({ className: '', html: `<span class="fp-total">Total Assets <b>${names.length}</b></span>`, iconSize: [0, 0] }),
        })
          .bindTooltip(`<div class="fp-tip"><strong>${esc(z.name)} · ${names.length} asset${names.length === 1 ? '' : 's'}</strong>${names.map((n) => `<span>${esc(n)}</span>`).join('')}</div>`, { direction: 'top', opacity: 1 })
          .on('click', () => this.openPopup({ kind: 'zone', id: z.id }, b.getCenter()))
          .addTo(this.markers!);
      }
    }

    for (const p of plots) {
      const pos = this.at(p.x, p.y);
      const heard = p.kind === 'device' ? this.gatewayOf(p) : null;
      const extra = heard ? `<span>Hears ${heard.assetCount} linked asset${heard.assetCount === 1 ? '' : 's'}</span>` : '';
      L.marker(pos, {
        keyboard: false,
        icon: L.divIcon({
          className: '',
          html: `<span class="fp-pin fp-pin--${p.kind}${this.highlight.some((h) => h.toLowerCase() === p.itemId?.toLowerCase()) ? ' fp-pin--hl' : ''}">${p.kind === 'device' ? DEVICE_SVG : ASSET_SVG}</span>${this.showLabels ? `<span class="fp-pin-label">${esc(p.code || p.name)}</span>` : ''}`,
          iconSize: [30, 30],
          iconAnchor: [15, 15],
        }),
      })
        .bindTooltip(`<div class="fp-tip"><strong>${esc(p.name)}</strong><span>${esc(p.code)} · ${esc(p.type || (p.kind === 'device' ? 'Device' : 'Asset'))}</span><span>${esc(p.status || '')}</span>${extra}</div>`, { direction: 'top', offset: [0, -14], opacity: 1 })
        .on('click', () => this.openPopup({ kind: p.kind === 'device' ? 'gateway' : 'plot', id: p.itemId || p.code }, L.latLng(pos)))
        .addTo(this.markers!);
      if (this.zoneId !== undefined) focus = focus ? focus.extend(pos) : L.latLngBounds(pos, pos);
    }

    this.layoutZoneLabels();

    // frame the plan (floor view) or the zone (zone view) when the scope changes
    if (this.shownScope !== this.zoneId) {
      this.shownScope = this.zoneId;
      const target = this.zoneId !== undefined && focus ? focus : L.latLngBounds([0, 0], [this.h, W]);
      map.fitBounds(target, { padding: [24, 24], animate: true, maxZoom: 2 });
    }
    this.scopeBounds = this.zoneId !== undefined && focus ? focus : null;
    this.updateLive();
    this.updateAssets();
    this.checkPopup();
  }

  /** + / - of the shared control stack. */
  zoomBy(delta: number): void {
    if (delta > 0) this.map?.zoomIn(delta);
    else this.map?.zoomOut(-delta);
  }

  /** Fit to screen: the whole plan (or the zone in zone view). */
  fitView(): void {
    const L = this.L;
    if (!L || !this.map) return;
    this.map.fitBounds(this.scopeBounds ?? L.latLngBounds([0, 0], [this.h, W]), { padding: [24, 24], animate: !reducedMotion(), maxZoom: 2 });
  }

  /** The card went full screen / back: recalculate the size, then fit. */
  refit(): void {
    this.ngZone.runOutsideAngular(() => {
      this.map?.invalidateSize({ animate: false });
      this.fitView();
    });
  }

  // ---------------------------------------------------------------- assets inside their zone

  /** The zone as a polygon / circle in map units, with its bounding box. */
  private zoneGeom(z: PlanZone): { pts: [number, number][] | null; c: [number, number] | null; r: number; s: number; n: number; w: number; e: number } {
    let pts: [number, number][] | null = null;
    let c: [number, number] | null = null;
    let r = 0;
    if (z.shape === 'Circle') {
      c = this.at(z.x + z.w / 2, z.y + z.h / 2);
      r = ((z.radius ?? z.w / 2) / 100) * W;
    } else if ((z.shape === 'Polygon' || z.shape === 'Custom') && z.points.length >= 3) {
      pts = z.points.map((p) => this.at(p.x, p.y));
    } else {
      pts = [this.at(z.x, z.y), this.at(z.x + z.w, z.y), this.at(z.x + z.w, z.y + z.h), this.at(z.x, z.y + z.h)];
    }
    const lat = pts ? pts.map((p) => p[0]) : [c![0] - r, c![0] + r];
    const lng = pts ? pts.map((p) => p[1]) : [c![1] - r, c![1] + r];
    return { pts, c, r, s: Math.min(...lat), n: Math.max(...lat), w: Math.min(...lng), e: Math.max(...lng) };
  }

  /**
   * Stable spots for `n` assets inside a zone: a grid over the zone's width / height (cells outside a polygon / circle are skipped),
   * filled in row order. The same asset ids always get the same spots, so markers do not jump between refreshes.
   */
  private zoneSpots(z: PlanZone, n: number): [number, number][] {
    const g = this.zoneGeom(z);
    const inside = (lat: number, lng: number): boolean => {
      if (g.c) return Math.hypot(lat - g.c[0], lng - g.c[1]) <= g.r * 0.92;
      let on = false;
      const p = g.pts!;
      for (let i = 0, j = p.length - 1; i < p.length; j = i++) {
        if (p[i][0] > lat !== p[j][0] > lat && lng < ((p[j][1] - p[i][1]) * (lat - p[i][0])) / (p[j][0] - p[i][0]) + p[i][1]) on = !on;
      }
      return on;
    };
    const padY = (g.n - g.s) * 0.1;
    const padX = (g.e - g.w) * 0.1;
    const s = g.s + padY, n2 = g.n - padY, w = g.w + padX, e = g.e - padX;
    let d = Math.sqrt(((n2 - s) * (e - w)) / Math.max(1, n));
    for (let tries = 0; tries < 40; tries++) {
      const cells: [number, number][] = [];
      const rows = Math.max(1, Math.floor((n2 - s) / d));
      const cols = Math.max(1, Math.floor((e - w) / d));
      for (let r = 0; r < rows; r++) {
        for (let c = 0; c < cols; c++) {
          const lat = n2 - ((r + 0.5) * (n2 - s)) / rows;
          const lng = w + ((c + 0.5) * (e - w)) / cols;
          if (inside(lat, lng)) cells.push([lat, lng]);
        }
      }
      if (cells.length >= n) return cells;
      d *= 0.85;
    }
    return Array.from({ length: n }, () => [(g.s + g.n) / 2, (g.w + g.e) / 2] as [number, number]);
  }

  private assetTip(a: PlanAsset): string {
    return `<div class="fp-tip"><strong>${esc(a.name)}</strong><span>${esc(a.code)} · ${esc(a.zoneName)}</span><span>Status: ${a.state}</span></div>`;
  }

  private assetHtml(a: PlanAsset): string {
    return `<span class="fp-am fp-am--${a.state.toLowerCase()}" style="--c:${STATE_COLOR[a.state]}"><u></u><i class="r1"></i><i class="r2"></i>${TROLLEY_SVG}<s class="fp-am-lbl">${esc(a.name)}</s></span>`;
  }

  private assetIcon(a: PlanAsset): Leaflet.DivIcon {
    return this.L!.divIcon({ className: 'fp-asset-marker', html: this.assetHtml(a), iconSize: [0, 0], iconAnchor: [0, 0] });
  }

  /** Inline transform transition for the next setLatLng: a continuing move starts from where the icon is now (browser retargets). */
  private glide(m: Leaflet.Marker): void {
    const el = m.getElement();
    if (!el || reducedMotion()) return;
    el.style.transition = `transform ${GLIDE_MS}ms cubic-bezier(.45, 0, .55, 1)`;
    clearTimeout(this.glideTimers.get(el));
    this.glideTimers.set(el, setTimeout(() => { el.style.transition = ''; this.glideTimers.delete(el); }, GLIDE_MS + 100));
  }

  private stopGlides(): void {
    this.abortMoves();
    for (const e of this.assetMk.values()) {
      const el = e.m.getElement();
      if (el) { el.style.transition = ''; clearTimeout(this.glideTimers.get(el)); this.glideTimers.delete(el); }
    }
  }

  private readonly glideTimers = new Map<HTMLElement, ReturnType<typeof setTimeout>>();

  /** Draws / moves the asset markers. Existing markers keep their element, so a move glides (CSS transition on the marker). */
  private updateAssets(): void {
    const L = this.L;
    const map = this.map;
    if (!L || !map || !this.assetLayer || !this.planReady) return;
    this.host.nativeElement.classList.toggle('fp-labels', this.showLabels);
    const zones = new Map((this.zoneId === undefined ? this.zones : this.zones.filter((z) => z.id === this.zoneId)).map((z) => [z.id, z]));
    const want = this.showAssets ? this.assetMarkers.filter((a) => zones.has(a.zoneId)) : [];
    const byZone = new Map<string, PlanAsset[]>();
    for (const a of want) byZone.set(a.zoneId, [...(byZone.get(a.zoneId) ?? []), a]);
    const pos = new Map<string, [number, number]>();
    for (const [zid, list] of byZone) {
      list.sort((x, y) => x.id.localeCompare(y.id));
      const spots = this.zoneSpots(zones.get(zid)!, list.length);
      list.forEach((a, i) => pos.set(a.id, spots[i]));
    }

    for (const [id, e] of this.assetMk) {
      if (pos.has(id)) continue;
      e.m.remove();
      this.assetMk.delete(id);
    }
    for (const a of want) {
      const at = pos.get(a.id)!;
      const e = this.assetMk.get(a.id);
      if (!e) {
        const m = L.marker(at, { keyboard: false, riseOnHover: true, zIndexOffset: 600, icon: this.assetIcon(a) })
          .bindTooltip(this.assetTip(a), { direction: 'top', offset: [0, -10], opacity: 1 })
          .on('click', () => {
            this.openPopup({ kind: 'asset', id: a.id }, null);
            this.ngZone.run(() => this.assetSelect.emit(a.id));
          })
          ;
        this.assetMk.set(a.id, { m, zoneId: a.zoneId, state: a.state, name: a.name, code: a.code });
        continue;
      }
      const from = e.m.getLatLng();
      const moved = Math.abs(from.lat - at[0]) > 0.01 || Math.abs(from.lng - at[1]) > 0.01;
      if (e.state !== a.state) {
        e.state = a.state;
        // the icon (not just its classes) is replaced, so a marker that is re-added after a split keeps its state colour
        e.m.setIcon(this.assetIcon(a));
      }
      if (moved && e.zoneId !== a.zoneId && !reducedMotion() && this.map?.hasLayer(this.assetLayer)) {
        // changed zone: a flier glides from the old spot to the new one; both clusters' counts update when it lands
        this.startMove(a, e, from, at);
      } else if (moved) {
        if (this.assetLayer.hasLayer(e.m)) this.glide(e.m);
        e.m.setLatLng(at);
      }
      e.zoneId = a.zoneId;
      e.name = a.name;
      e.code = a.code;
      e.m.setTooltipContent(this.assetTip(a));
    }
    this.curZones = zones;
    this.recluster();
    this.applyAssetFocus(zones);
    this.checkPopup();
    if (this.live?.assetId || this.signal?.assetId) this.updateLive();
  }


  // ---------------------------------------------------------------- zone name chips

  /**
   * Each zone's name sits OUTSIDE the zone as a tab on its top edge (left-aligned); when that would leave the plan or touch another
   * name it goes to the bottom edge, then the left / right side. Chips keep one size on screen, so this is redone after every zoom.
   * Below a small zoom the names are shortened (the zone's hover tooltip has the full name).
   */
  private layoutZoneLabels(): void {
    const map = this.map;
    const L = this.L;
    if (!map || !L || !this.zoneLabels.length) return;
    const short = map.getZoom() < SHORT_NAME_ZOOM;
    const plan0 = map.latLngToContainerPoint([0, 0]);
    const plan1 = map.latLngToContainerPoint([this.h, W]);
    const plan = { l: Math.min(plan0.x, plan1.x), r: Math.max(plan0.x, plan1.x), t: Math.min(plan0.y, plan1.y), b: Math.max(plan0.y, plan1.y) };
    interface Rect { x: number; y: number; w: number; h: number }
    const hit = (a: Rect, b: Rect) => a.x < b.x + b.w + 3 && b.x < a.x + a.w + 3 && a.y < b.y + b.h + 3 && b.y < a.y + a.h + 3;
    const inside = (r: Rect) => r.x >= plan.l - 1 && r.y >= plan.t - 1 && r.x + r.w <= plan.r + 1 && r.y + r.h <= plan.b + 1;
    const placed: Rect[] = [];
    // top-left zones first, so the first choice (top edge) goes to the ones that are most likely to need it
    const order = [...this.zoneLabels].sort((a, b) => b.b.getNorth() - a.b.getNorth() || a.b.getWest() - b.b.getWest());
    for (const l of order) {
      const chip = l.m.getElement()?.firstElementChild as HTMLElement | null;
      if (!chip) continue;
      chip.classList.toggle('fp-zl--short', short);
      const cw = chip.offsetWidth || 60;
      const ch = chip.offsetHeight || 20;
      const nw = map.latLngToContainerPoint(l.b.getNorthWest());
      const se = map.latLngToContainerPoint(l.b.getSouthEast());
      const ne = map.latLngToContainerPoint(l.b.getNorthEast());
      const cands: { side: string; r: Rect; ll: Leaflet.LatLng }[] = [
        { side: 'top', r: { x: nw.x, y: nw.y - ch, w: cw, h: ch }, ll: l.b.getNorthWest() },
        { side: 'bottom', r: { x: nw.x, y: se.y, w: cw, h: ch }, ll: l.b.getSouthWest() },
        { side: 'left', r: { x: nw.x - cw, y: nw.y, w: cw, h: ch }, ll: l.b.getNorthWest() },
        { side: 'right', r: { x: ne.x, y: ne.y, w: cw, h: ch }, ll: l.b.getNorthEast() },
      ];
      const pick = cands.find((c) => inside(c.r) && !placed.some((p) => hit(c.r, p))) ?? cands.find((c) => !placed.some((p) => hit(c.r, p))) ?? cands[0];
      l.m.setLatLng(pick.ll);
      chip.classList.remove('fp-zl--top', 'fp-zl--bottom', 'fp-zl--left', 'fp-zl--right');
      chip.classList.add('fp-zl--' + pick.side);
      placed.push(pick.r);
    }
  }

  // ---------------------------------------------------------------- zone changes (flier) + clustering

  /** An asset left its zone: a temporary icon glides old spot -> new spot; until it lands the old zone still counts it. */
  private startMove(a: PlanAsset, e: { m: Leaflet.Marker; zoneId: string }, from: Leaflet.LatLng, to: [number, number]): void {
    const L = this.L!;
    const prev = this.zoneHold.get(a.id);
    const startAt = prev?.flier ? prev.flier.getLatLng() : from; // an update during a glide continues from where the icon is now
    const oldZone = prev?.zone ?? e.zoneId;
    if (prev) {
      clearTimeout(prev.timer);
      prev.flier?.remove();
    }
    const flier = L.marker(startAt, { interactive: false, keyboard: false, zIndexOffset: 700, icon: this.assetIcon(a) }).addTo(this.assetLayer!);
    e.m.setLatLng(to);
    const timer = setTimeout(() => this.endMove(a.id), GLIDE_MS + 120);
    this.zoneHold.set(a.id, { zone: oldZone, timer, flier });
    requestAnimationFrame(() => {
      if (this.zoneHold.get(a.id)?.flier !== flier) return;
      this.glide(flier);
      flier.setLatLng(to);
    });
  }

  private endMove(id: string): void {
    const h = this.zoneHold.get(id);
    if (!h) return;
    clearTimeout(h.timer);
    h.flier?.remove();
    this.zoneHold.delete(id);
    this.recluster();
  }

  private abortMoves(): void {
    if (!this.zoneHold.size) return;
    for (const h of this.zoneHold.values()) {
      clearTimeout(h.timer);
      h.flier?.remove();
    }
    this.zoneHold.clear();
  }

  private scheduleRecluster(): void {
    clearTimeout(this.reclusterTimer);
    this.reclusterTimer = setTimeout(() => this.recluster(), 100);
  }

  /**
   * Zoomed out / icons would overlap: one badge per zone with the count; zoomed in: the single icons on the in-zone grid.
   * Badges (and icons) of different zones that overlap on screen are merged into one badge. The asset being located / selected
   * is always an icon of its own. Gateways are separate markers and never take part.
   */
  private recluster(): void {
    const L = this.L;
    const map = this.map;
    const layer = this.assetLayer;
    if (!L || !map || !layer || !this.planReady) return;
    for (const c of this.clusterMk) c.remove();
    this.clusterMk = [];

    const zoom = map.getZoom();
    if (this.expand && zoom < this.expand.zoom - 0.5) this.expand = undefined;
    const pinned = new Set<string>([...(this.assetFocus?.ids ?? []), this.live?.assetId ?? '', this.signal?.assetId ?? ''].filter(Boolean));
    const px = (ll: Leaflet.LatLng) => map.latLngToLayerPoint(ll);

    interface Unit { ids: string[]; held: number; zones: Set<string>; x: number; y: number; r: number; ll: Leaflet.LatLng }
    const perZone = new Map<string, { shown: [string, Leaflet.LatLng][]; held: number }>();
    for (const [id, e] of this.assetMk) {
      if (pinned.has(id) && !this.zoneHold.has(id)) continue;
      const hold = this.zoneHold.get(id);
      const z = hold ? hold.zone : e.zoneId;
      const g = perZone.get(z) ?? { shown: [], held: 0 };
      if (hold) g.held++;
      else g.shown.push([id, e.m.getLatLng()]);
      perZone.set(z, g);
    }

    const units: Unit[] = [];
    const single = new Set<string>(); // assets drawn as icons
    for (const [zid, g] of perZone) {
      const total = g.shown.length + g.held;
      const open = !!this.expand?.zones.has(zid);
      let cluster = false;
      if (total >= 2 && !open) {
        cluster = zoom < CLUSTER_ZOOM;
        if (!cluster) {
          const pts = g.shown.map(([, ll]) => px(ll));
          outer: for (let i = 0; i < pts.length; i++) for (let j = i + 1; j < pts.length; j++) if (pts[i].distanceTo(pts[j]) < ICON_PX * 0.95) { cluster = true; break outer; }
        }
      }
      if (cluster) {
        const n = g.shown.length;
        const ll = n
          ? L.latLng(g.shown.reduce((t, [, p]) => t + p.lat, 0) / n, g.shown.reduce((t, [, p]) => t + p.lng, 0) / n)
          : this.zoneCentre(zid) ?? L.latLng(this.h / 2, W / 2);
        const c = px(ll);
        units.push({ ids: g.shown.map(([id]) => id), held: g.held, zones: new Set([zid]), x: c.x, y: c.y, r: this.clusterSize(total) / 2, ll });
      } else {
        for (const [id, ll] of g.shown) {
          const c = px(ll);
          units.push({ ids: [id], held: 0, zones: new Set([zid]), x: c.x, y: c.y, r: ICON_PX / 2, ll });
        }
      }
    }

    // different zones whose icons / badges overlap on screen -> one badge with the total
    for (let guard = 0; guard < 12; guard++) {
      let merged = false;
      outer2: for (let i = 0; i < units.length; i++) {
        for (let j = i + 1; j < units.length; j++) {
          const a = units[i], b = units[j];
          const sameZoneIcons = a.ids.length === 1 && b.ids.length === 1 && [...a.zones][0] === [...b.zones][0];
          if (sameZoneIcons || Math.hypot(a.x - b.x, a.y - b.y) >= a.r + b.r) continue;
          const na = a.ids.length + a.held, nb = b.ids.length + b.held;
          const w = na + nb;
          a.x = (a.x * na + b.x * nb) / w;
          a.y = (a.y * na + b.y * nb) / w;
          a.ll = L.latLng((a.ll.lat * na + b.ll.lat * nb) / w, (a.ll.lng * na + b.ll.lng * nb) / w);
          a.ids = [...a.ids, ...b.ids];
          a.held += b.held;
          b.zones.forEach((z) => a.zones.add(z));
          a.r = this.clusterSize(a.ids.length + a.held) / 2;
          units.splice(j, 1);
          merged = true;
          break outer2;
        }
      }
      if (!merged) break;
    }

    for (const u of units) {
      const n = u.ids.length + u.held;
      if (u.ids.length === 1 && !u.held && u.zones.size === 1) single.add(u.ids[0]);
      else this.addClusterMarker(u.ids, n, u.zones, u.ll);
    }
    for (const id of pinned) if (this.assetMk.has(id) && !this.zoneHold.has(id)) single.add(id);
    for (const [id, e] of this.assetMk) {
      const show = single.has(id) && this.showAssets;
      const on = layer.hasLayer(e.m);
      if (show && !on) e.m.addTo(layer);
      else if (!show && on) e.m.remove();
    }
    const ids = new Set(this.assetFocus?.ids ?? []);
    for (const [id, e] of this.assetMk) e.m.getElement()?.firstElementChild?.classList.toggle('fp-am--hl', ids.has(id));
  }

  private clusterSize(n: number): number {
    return Math.round(ICON_PX + Math.min(14, Math.log2(Math.max(1, n)) * 4));
  }

  private zoneCentre(zid: string): Leaflet.LatLng | null {
    const z = this.curZones.get(zid);
    if (!z) return null;
    const g = this.zoneGeom(z);
    return this.L!.latLng((g.s + g.n) / 2, (g.w + g.e) / 2);
  }

  private addClusterMarker(ids: string[], n: number, zones: Set<string>, ll: Leaflet.LatLng): void {
    const L = this.L!;
    const d = this.clusterSize(n);
    const names = [...ids.map((id) => this.assetMk.get(id)?.name ?? ''), ...[...this.zoneHold.entries()].filter(([, h]) => zones.has(h.zone)).map(([id]) => this.assetMk.get(id)?.name ?? '')].filter(Boolean);
    const shown = names.slice(0, 8).map((x) => `<span>${esc(x)}</span>`).join('');
    const more = names.length > 8 ? `<span>and ${names.length - 8} more</span>` : '';
    const m = L.marker(ll, {
      keyboard: false,
      zIndexOffset: 500,
      icon: L.divIcon({ className: 'fp-cluster-mk', html: `<span class="fp-cl" style="--d:${d}px">${n}</span>`, iconSize: [0, 0], iconAnchor: [0, 0] }),
    })
      .bindTooltip(`<div class="fp-tip"><strong>${n} asset${n === 1 ? '' : 's'}</strong>${shown}${more}</div>`, { direction: 'top', offset: [0, -d / 2], opacity: 1 })
      .on('click', () => this.openCluster(zones))
      .addTo(this.assetLayer!);
    this.clusterMk.push(m);
  }

  /** Click on a badge: zoom to its zone(s) until the icons split (they stay split until the user zooms back out). */
  private openCluster(zones: Set<string>): void {
    const L = this.L!;
    const map = this.map!;
    let b: Leaflet.LatLngBounds | null = null;
    for (const zid of zones) {
      const z = this.curZones.get(zid);
      if (!z) continue;
      const g = this.zoneGeom(z);
      const zb = L.latLngBounds([g.s, g.w], [g.n, g.e]);
      b = b ? b.extend(zb) : zb;
    }
    if (!b) return;
    const target = Math.min(2, map.getBoundsZoom(b, false, L.point(80, 80)));
    this.expand = { zones, zoom: Math.max(target, map.getZoom() + 0.5) };
    map.flyToBounds(b, { padding: [40, 40], maxZoom: 2, duration: reducedMotion() ? 0 : 0.6 });
  }

  // ---------------------------------------------------------------- click popup (same card as Maps & Floor Plans › Map Library)

  /** The object whose popup is open (one at a time); `ll` is where it sits (an asset marker is looked up live, it glides). */
  private popup: PopupTarget | null = null;
  private popupLL: Leaflet.LatLng | null = null;
  private viewTimer?: ReturnType<typeof setInterval>;
  /** Popup content (null = closed) and the object's position in the map's pixels. */
  view: PopupView | null = null;
  anchor: { x: number; y: number; inView: boolean } | null = null;

  private openPopup(p: PopupTarget, ll: Leaflet.LatLng | null): void {
    this.popup = p;
    this.popupLL = ll;
    this.ngZone.run(() => {
      this.rebuildView();
      this.placePopup(false);
      clearInterval(this.viewTimer);
      // "12 s ago" and the gateway's online state keep up while the popup is open
      this.viewTimer = setInterval(() => this.rebuildView(), 1000);
    });
  }

  closePopup(): void {
    if (!this.popup && !this.view) return;
    this.popup = null;
    this.popupLL = null;
    clearInterval(this.viewTimer);
    this.ngZone.run(() => {
      this.view = null;
      this.anchor = null;
    });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closePopup();
  }

  popupAction(kind: 'locate' | 'signal'): void {
    const id = this.popup?.id;
    this.closePopup();
    if (!id) return;
    if (kind === 'locate') this.assetLocate.emit(id);
    else this.assetSignal.emit(id);
  }

  /** Keeps the popup on its object while the map pans / zooms / resizes. */
  private placePopup(inZone = true): void {
    const map = this.map;
    if (!this.popup || !map) return;
    const ll = this.popup.kind === 'asset' ? this.assetMk.get(this.popup.id)?.m.getLatLng() : this.popupLL;
    if (!ll) return;
    const pt = map.latLngToContainerPoint(ll);
    const size = map.getSize();
    const next = { x: Math.round(pt.x), y: Math.round(pt.y), inView: pt.x >= 0 && pt.y >= 0 && pt.x <= size.x && pt.y <= size.y };
    if (this.anchor && this.anchor.x === next.x && this.anchor.y === next.y && this.anchor.inView === next.inView) return;
    if (inZone) this.ngZone.run(() => (this.anchor = next));
    else this.anchor = next;
  }

  /** After a redraw: close the popup when its object is gone, else refresh it. */
  private checkPopup(): void {
    const p = this.popup;
    if (!p) return;
    const gone =
      p.kind === 'asset' ? !this.assetMk.has(p.id)
      : p.kind === 'zone' ? !this.zones.some((z) => z.id === p.id) || (this.zoneId !== undefined && this.zoneId !== p.id)
      : !this.plots.some((x) => (x.itemId || x.code) === p.id);
    if (gone) this.closePopup();
    else this.ngZone.run(() => this.rebuildView());
  }

  private ago(iso: string | null | undefined): string {
    if (!iso) return 'No readings yet';
    const t = new Date(iso).getTime();
    if (Number.isNaN(t)) return 'No readings yet';
    const s = Math.max(0, Math.round((Date.now() - t) / 1000));
    return s < 5 ? 'Just now' : s < 60 ? `${s} s ago` : s < 3600 ? `${Math.floor(s / 60)} min ago` : s < 86400 ? `${Math.floor(s / 3600)} h ago` : `${Math.floor(s / 86400)} d ago`;
  }

  private rebuildView(): void {
    const p = this.popup;
    if (!p) {
      this.view = null;
      return;
    }
    const zoneName = (id: string | null | undefined) => this.zones.find((z) => z.id === id)?.name || '–';
    const base = { kind: p.kind, subtitle: '', badge: '', tone: 'mute' as const, rows: [] as [string, string][], names: [] as string[], total: 0, more: 0, actions: false };
    if (p.kind === 'gateway') {
      const plot = this.plots.find((x) => x.kind === 'device' && (x.itemId || x.code) === p.id);
      if (!plot) return;
      const feed = this.gatewayOf(plot);
      const seen = feed?.lastSeenAt ?? null;
      const online = !!seen && Date.now() - new Date(seen).getTime() <= 120_000;
      this.view = { ...base, title: plot.name || plot.code, subtitle: `Gateway ID: ${plot.code || plot.itemId}`, badge: online ? 'Online' : 'Offline', tone: online ? 'ok' : 'bad',
        rows: [['Zone', zoneName(plot.zoneId)], ['Status', online ? 'Online' : 'Offline'], ['Assets in range', String(online ? (feed?.assetCount ?? 0) : 0)], ['Last seen', this.ago(seen)]] };
    } else if (p.kind === 'zone') {
      const z = this.zones.find((x) => x.id === p.id);
      if (!z) return;
      const names = this.zoneAssets(z.id);
      this.view = { ...base, title: z.name, subtitle: `Zone ID: ${z.code}`, total: names.length, names: names.slice(0, 5), more: Math.max(0, names.length - 5) };
    } else if (p.kind === 'asset') {
      const a = this.assetMarkers.find((x) => x.id === p.id);
      if (!a) return;
      this.view = { ...base, title: a.name, subtitle: `Asset ID: ${a.code}`, actions: this.assetLocate.observed || this.assetSignal.observed,
        rows: [['Tag', a.tag || '–'], ['Zone', a.zoneName || '–'], ['Status', a.state === 'Unknown' ? 'No signal' : a.state], ['Last seen', this.ago(a.lastSeenAt)]] };
    } else {
      const x = this.plots.find((q) => q.kind === 'asset' && (q.itemId || q.code) === p.id);
      if (!x) return;
      this.view = { ...base, title: x.name, subtitle: `Asset ID: ${x.code}`, rows: [['Type', x.type || '–'], ['Zone', zoneName(x.zoneId)], ['Status', x.status || '–']] };
    }
  }

  private drawTrail(a: [number, number], b: [number, number]): void {
    const L = this.L!;
    const line = L.polyline([a, b], { color: '#16a34a', weight: 3, opacity: 0.8, dashArray: '4 7', interactive: false, className: 'fp-trail' }).addTo(this.assetLayer!);
    setTimeout(() => line.remove(), 8200);
  }

  /** Highlight (ripple + bounce ~3 s, then a steady ring) and fit the view to the zones of the focused assets. */
  private applyAssetFocus(zones: Map<string, PlanZone>): void {
    const L = this.L!;
    const f = this.assetFocus;
    const ids = new Set(f?.ids ?? []);
    for (const [id, e] of this.assetMk) e.m.getElement()?.firstElementChild?.classList.toggle('fp-am--hl', ids.has(id));
    if (!f) {
      this.shownHl = '';
      return;
    }
    if (f.key === this.shownHl) return;
    const found = f.ids.map((id) => this.assetMk.get(id)).filter((e): e is NonNullable<typeof e> => !!e);
    if (!found.length) return; // not drawn yet (floor still loading): try again on the next update
    this.shownHl = f.key;
    const els = found.map((e) => e.m.getElement()?.firstElementChild as HTMLElement | undefined);
    if (!reducedMotion()) {
      els.forEach((el) => el?.classList.add('fp-am--hl-anim'));
      clearTimeout(this.hlTimer);
      this.hlTimer = setTimeout(() => els.forEach((el) => el?.classList.remove('fp-am--hl-anim')), 3000);
    }
    // the zone fills ~2/3 of the view (17 % padding each side); one asset: centred on its own marker, several: fit their markers
    const map = this.map!;
    const size = map.getSize();
    const pad = L.point(size.x * 0.17, size.y * 0.17);
    const maxZoom = map.getMaxZoom() || 4;
    const go = (to: Leaflet.LatLngExpression, zoom: number) => {
      const z = Math.min(zoom, maxZoom);
      if (reducedMotion()) map.setView(to, z, { animate: false });
      else map.flyTo(to, z, { duration: 0.8 });
    };
    const zoneBounds = (zoneId: string): Leaflet.LatLngBounds | null => {
      const z = zones.get(zoneId);
      if (!z) return null;
      const g = this.zoneGeom(z);
      return L.latLngBounds([g.s, g.w], [g.n, g.e]);
    };
    if (found.length === 1) {
      const zb = zoneBounds(found[0].zoneId);
      go(found[0].m.getLatLng(), zb ? map.getBoundsZoom(zb, false, pad) : Math.max(map.getZoom(), 1.5));
    } else {
      let b = L.latLngBounds(found.map((e) => e.m.getLatLng()));
      const flat = b.getNorth() - b.getSouth() < 1 && b.getEast() - b.getWest() < 1;
      if (flat) for (const e of found) { const zb = zoneBounds(e.zoneId); if (zb) b = b.extend(zb); }
      go(b.getCenter(), map.getBoundsZoom(b, false, pad));
    }
  }

  /** The amber marker of a tag that is blinking / beeping. */
  private updateSignal(): void {
    const L = this.L;
    if (!L || !this.liveLayer) return;
    const s = this.signal;
    if (!s) {
      this.signalMarker?.remove();
      this.signalMarker = undefined;
      return;
    }
    const pos = this.pointOf(s);
    const wait = s.state !== 'Live';
    const icon = L.divIcon({
      className: '',
      html: `<span class="fp-signal${wait ? ' fp-signal--wait' : ''}"><i></i><b>&#9679;</b><span>${esc(s.label)}</span></span>`,
      iconSize: [34, 34],
      iconAnchor: [17, 17],
    });
    if (!this.signalMarker) this.signalMarker = L.marker(pos, { keyboard: false, interactive: false, zIndexOffset: 900, icon }).addTo(this.liveLayer);
    else {
      this.signalMarker.setLatLng(pos);
      this.signalMarker.setIcon(icon);
    }
  }

  /** The live asset marker, its trail and any zoom request; the plan itself is left alone. */
  private updateLive(): void {
    const L = this.L;
    const map = this.map;
    if (!L || !map || !this.liveLayer) return;
    if (!this.planReady) return;
    this.updateSignal();
    const live = this.live;
    if (!live) {
      this.liveMarker?.remove();
      this.liveLine?.remove();
      this.liveMarker = undefined;
      this.liveLine = undefined;
      this.applyFocus();
      return;
    }

    const pos = this.pointOf(live);
    const stale = live.state !== 'Live';
    const html = `<span class="fp-live${stale ? ' fp-live--stale' : ''}"><i></i><b></b><span>${esc(live.label)}${stale ? ' · ' + esc(live.state.toLowerCase()) : ''}</span></span>`;
    if (!this.liveMarker) {
      this.liveMarker = L.marker(pos, {
        keyboard: false,
        interactive: false,
        zIndexOffset: 1000,
        icon: L.divIcon({ className: 'fp-live-marker', html, iconSize: [22, 22], iconAnchor: [11, 11] }),
      }).addTo(this.liveLayer);
    } else {
      // setLatLng moves the same element, so the CSS transition glides it to the new point
      this.liveMarker.setLatLng(pos);
      this.liveMarker.setIcon(L.divIcon({ className: 'fp-live-marker', html, iconSize: [22, 22], iconAnchor: [11, 11] }));
    }

    // the trail is gateway-to-gateway: it does not fit a marker that sits in its zone's grid spot
    const path = this.assetMk.has(live.assetId ?? '') ? [] : this.liveTrail.map((p) => this.at(p.x, p.y));
    if (path.length > 1) {
      if (!this.liveLine) this.liveLine = L.polyline(path, { color: '#dc2626', weight: 3, opacity: 0.55, dashArray: '6 6', interactive: false }).addTo(this.liveLayer);
      else this.liveLine.setLatLngs(path);
    } else if (this.liveLine) {
      this.liveLine.remove();
      this.liveLine = undefined;
    }

    this.applyFocus();
  }

  /** Where a live / signal point is drawn: on the asset's own marker in its zone when there is one, else at x / y (gateway). */
  private pointOf(p: LivePoint): Leaflet.LatLngExpression {
    const mk = p.assetId ? this.assetMk.get(p.assetId) : undefined;
    return mk ? mk.m.getLatLng() : this.at(p.x, p.y);
  }

  private applyFocus(): void {
    const map = this.map;
    const focus = this.focus;
    if (!map) return;
    if (!focus) {
      this.shownFocus = '';
      return;
    }
    if (focus.key !== this.shownFocus) {
      this.shownFocus = focus.key;
      map.flyTo(this.at(focus.x, focus.y), Math.max(map.getZoom(), 1.25), { duration: 0.8 });
    }
  }

  private gatewayOf(p: PlanPlot): GatewayFeed | null {
    return this.feed[p.itemId?.toLowerCase()] ?? this.feed[p.code?.toLowerCase()] ?? null;
  }

  /** Asset names heard by the gateways plotted in a zone (deduplicated). */
  private zoneAssets(zoneId: string): string[] {
    const names = new Map<string, string>();
    for (const p of this.plots) {
      if (p.kind !== 'device' || p.zoneId !== zoneId) continue;
      for (const a of this.gatewayOf(p)?.assets ?? []) names.set(a.assetId.toLowerCase(), a.assetName ? `${a.assetName} (${a.assetId})` : a.assetId);
    }
    return [...names.values()].sort((a, b) => a.localeCompare(b));
  }
}
