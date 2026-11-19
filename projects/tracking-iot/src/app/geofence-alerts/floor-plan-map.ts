import { AfterViewInit, Component, ElementRef, Input, NgZone, OnChanges, OnDestroy, SimpleChanges, ViewChild, ViewEncapsulation, inject } from '@angular/core';
import type * as Leaflet from 'leaflet';
import { GatewayFeed, PlanPlot, PlanZone } from './floor-map-api.service';

type L = typeof Leaflet;

/** The asset followed live (Real-Time Location › Locate asset): an estimated point on the plan, in % of the plan. */
export interface LivePoint {
  x: number;
  y: number;
  label: string;
  /** Live, Stale or Offline: Stale / Offline are drawn greyed out. */
  state: string;
}

/** Zoom request: the map flies to the point whenever `key` changes. */
export interface MapFocus {
  key: string;
  x: number;
  y: number;
}

/** Inputs that only move the live marker: they never redraw the plan. */
const LIVE_INPUTS = new Set(['live', 'liveTrail', 'focus']);

/** Plan width in map units; the height follows the image's aspect ratio. */
const W = 1000;

/** Same items in the same order (case-insensitive). */
const sameList = (a: string[] | null | undefined, b: string[] | null | undefined) =>
  (a ?? []).length === (b ?? []).length && (a ?? []).every((x, i) => x.toLowerCase() === (b ?? [])[i]?.toLowerCase());
const esc = (s: string) => s.replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]!);

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
  template: `<div #host class="fp-map" [class.fp-map--blank]="imageMissing"></div>
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
      background: #fff; box-shadow: 0 6px 24px rgba(15, 23, 42, .12); color: #475569; font-size: 13px; text-align: center; max-width: 80%; }
    .fp-note--top { top: 14px; transform: translateX(-50%); padding: 7px 14px; font-size: 12px; border: 1px solid #fde68a; background: #fffbeb; color: #92400e; }
    .fp-map--blank { background-color: #fff; background-image: linear-gradient(#eef2f7 1px, transparent 1px), linear-gradient(90deg, #eef2f7 1px, transparent 1px);
      background-size: 32px 32px; }
    .fp-zone-shape { cursor: zoom-in; }
    .fp-pin { display: grid; place-items: center; width: 30px; height: 30px; border-radius: 50%; border: 2.5px solid #fff; color: #fff;
      box-shadow: 0 3px 10px rgba(15, 23, 42, .28); transition: transform .15s; }
    .fp-pin:hover { transform: scale(1.12); }
    .fp-pin--device { background: #059669; }
    .fp-pin--asset { background: #7c3aed; }
    .fp-pin--hl { box-shadow: 0 0 0 4px rgba(124, 58, 237, .35), 0 0 0 9px rgba(124, 58, 237, .15), 0 3px 10px rgba(15, 23, 42, .28); transform: scale(1.15); }
    .fp-pin-label { position: absolute; top: 32px; left: 50%; transform: translateX(-50%); white-space: nowrap; padding: 1px 6px; border-radius: 6px;
      background: rgba(255, 255, 255, .92); color: #0f172a; font-size: 11px; font-weight: 600; }
    .fp-total { display: inline-flex; align-items: center; gap: 6px; padding: 4px 10px; border-radius: 999px; background: #fff; color: #4c1d95;
      border: 1.5px solid #c4b5fd; box-shadow: 0 4px 14px rgba(76, 29, 149, .18); font-size: 11.5px; font-weight: 600; white-space: nowrap;
      transform: translate(-50%, -50%); }
    .fp-total b { display: inline-grid; place-items: center; min-width: 20px; height: 20px; padding: 0 5px; border-radius: 999px; background: #6d28d9; color: #fff; font-size: 11px; }
    .fp-zone-label { transform: translate(-50%, -100%); white-space: nowrap; padding: 2px 8px; border-radius: 6px; background: var(--c); color: #fff;
      font-size: 11px; font-weight: 600; box-shadow: 0 2px 6px rgba(15, 23, 42, .2); }
    .fp-live-marker { transition: transform 1.6s ease-out; }
    .leaflet-zoom-anim .fp-live-marker { transition: none; }
    .fp-live { position: relative; display: grid; place-items: center; width: 22px; height: 22px; }
    .fp-live i { position: absolute; inset: 0; border-radius: 50%; background: rgba(220, 38, 38, .35); animation: fp-live-pulse 1.6s ease-out infinite; }
    .fp-live b { position: relative; width: 14px; height: 14px; border-radius: 50%; background: #dc2626; border: 3px solid #fff; box-shadow: 0 2px 8px rgba(15, 23, 42, .4); }
    .fp-live span { position: absolute; top: 24px; left: 50%; transform: translateX(-50%); white-space: nowrap; padding: 2px 8px; border-radius: 6px;
      background: #dc2626; color: #fff; font-size: 11px; font-weight: 700; box-shadow: 0 2px 6px rgba(15, 23, 42, .25); }
    .fp-live--stale i { animation: none; background: rgba(100, 116, 139, .25); }
    .fp-live--stale b, .fp-live--stale span { background: #64748b; }
    @keyframes fp-live-pulse { 0% { transform: scale(.6); opacity: 1; } 100% { transform: scale(2.6); opacity: 0; } }
    .fp-tip strong { display: block; font-size: 12.5px; }
    .fp-tip span { display: block; color: #64748b; font-size: 11.5px; }
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
  /** Fly to a point (zoom in) whenever its key changes. */
  @Input() focus: MapFocus | null = null;

  @ViewChild('host', { static: true }) private host!: ElementRef<HTMLDivElement>;
  private readonly ngZone = inject(NgZone);
  private L?: L;
  private map?: Leaflet.Map;
  private image?: Leaflet.ImageOverlay;
  private shapes?: Leaflet.LayerGroup;
  private markers?: Leaflet.LayerGroup;
  private liveLayer?: Leaflet.LayerGroup;
  private liveMarker?: Leaflet.Marker;
  private liveLine?: Leaflet.Polyline;
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
      const map = Lf.map(this.host.nativeElement, { crs: Lf.CRS.Simple, minZoom: -3, maxZoom: 3, zoomSnap: 0.25, zoomDelta: 0.5, attributionControl: false, zoomControl: true });
      this.map = map;
      this.shapes = Lf.layerGroup().addTo(map);
      this.markers = Lf.layerGroup().addTo(map);
      this.liveLayer = Lf.layerGroup().addTo(map);
      map.setView([this.h / 2, W / 2], -1);
      this.resize = new ResizeObserver(() => map.invalidateSize({ animate: false }));
      this.resize.observe(this.host.nativeElement);
      this.sync();
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    // A parent getter can hand over a new but identical highlight array on every change detection: that alone
    // must not clear and redraw the whole plan (it made the map flicker every second)
    const h = changes['highlight'];
    if (h && Object.keys(changes).length === 1 && sameList(h.previousValue, h.currentValue)) return;
    if (!this.map) return;
    if (Object.keys(changes).every((k) => LIVE_INPUTS.has(k))) {
      this.ngZone.runOutsideAngular(() => this.updateLive());
      return;
    }
    this.ngZone.runOutsideAngular(() => this.sync());
  }

  ngOnDestroy(): void {
    this.destroyed = true;
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
      this.shownFocus = '';
      const url = this.imageUrl;
      if (!url) return;
      this.setImageMissing(false);
      const img = new Image();
      img.onload = () => {
        if (url !== this.shownImage || !this.map) return;
        this.h = img.naturalWidth ? (W * img.naturalHeight) / img.naturalWidth : W * 0.6;
        const bounds = L.latLngBounds([0, 0], [this.h, W]);
        this.image = L.imageOverlay(img.src, bounds, { interactive: false }).addTo(map);
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

    for (const z of zones) {
      const style: Leaflet.PathOptions = { color: z.color || '#7c3aed', weight: 2.2, fillColor: z.color || '#7c3aed', fillOpacity: 0.22, dashArray: z.active ? undefined : '6 6', className: 'fp-zone-shape' };
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
      if (this.showLabels) {
        L.marker([b.getNorth(), b.getCenter().lng], {
          interactive: false,
          keyboard: false,
          icon: L.divIcon({ className: '', html: `<span class="fp-zone-label" style="--c:${esc(z.color || '#7c3aed')}">${esc(z.name)}</span>`, iconSize: [0, 0] }),
        }).addTo(this.shapes!);
      }
      // live "Total Assets": distinct assets heard by the gateways plotted in this zone
      const names = this.zoneAssets(z.id);
      if (names.length) {
        L.marker(b.getCenter(), {
          keyboard: false,
          zIndexOffset: -500,
          icon: L.divIcon({ className: '', html: `<span class="fp-total">Total Assets <b>${names.length}</b></span>`, iconSize: [0, 0] }),
        })
          .bindTooltip(`<div class="fp-tip"><strong>${esc(z.name)} · ${names.length} asset${names.length === 1 ? '' : 's'}</strong>${names.map((n) => `<span>${esc(n)}</span>`).join('')}</div>`, { direction: 'top', opacity: 1 })
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
        .addTo(this.markers!);
      if (this.zoneId !== undefined) focus = focus ? focus.extend(pos) : L.latLngBounds(pos, pos);
    }

    // frame the plan (floor view) or the zone (zone view) when the scope changes
    if (this.shownScope !== this.zoneId) {
      this.shownScope = this.zoneId;
      const target = this.zoneId !== undefined && focus ? focus : L.latLngBounds([0, 0], [this.h, W]);
      map.fitBounds(target, { padding: [24, 24], animate: true, maxZoom: 2 });
    }
    this.updateLive();
  }

  /** The live asset marker, its trail and any zoom request; the plan itself is left alone. */
  private updateLive(): void {
    const L = this.L;
    const map = this.map;
    if (!L || !map || !this.liveLayer) return;
    if (!this.planReady) return;
    const live = this.live;
    if (!live) {
      this.liveLayer.clearLayers();
      this.liveMarker = undefined;
      this.liveLine = undefined;
      this.shownFocus = '';
      return;
    }

    const pos = this.at(live.x, live.y);
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

    const path = this.liveTrail.map((p) => this.at(p.x, p.y));
    if (path.length > 1) {
      if (!this.liveLine) this.liveLine = L.polyline(path, { color: '#dc2626', weight: 3, opacity: 0.55, dashArray: '6 6', interactive: false }).addTo(this.liveLayer);
      else this.liveLine.setLatLngs(path);
    } else if (this.liveLine) {
      this.liveLine.remove();
      this.liveLine = undefined;
    }

    const focus = this.focus;
    if (focus && focus.key !== this.shownFocus) {
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
