import { AfterViewInit, Component, ElementRef, Input, NgZone, OnChanges, OnDestroy, ViewChild, ViewEncapsulation, inject } from '@angular/core';
import type * as Leaflet from 'leaflet';
import { GatewayFeed, PlanPlot, PlanZone } from './floor-map-api.service';

type L = typeof Leaflet;

/** Plan width in map units; the height follows the image's aspect ratio. */
const W = 1000;

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
  template: `<div #host class="fp-map"></div>
    @if (note) { <div class="fp-note" role="status">{{ note }}</div> }`,
  styles: `
    app-floor-plan-map { position: absolute; inset: 0; display: block; }
    .fp-map { position: absolute; inset: 0; background: #f8fafc; }
    .fp-map .leaflet-container, .fp-map.leaflet-container { background: #f8fafc; font: inherit; }
    .fp-note { position: absolute; left: 50%; top: 50%; transform: translate(-50%, -50%); z-index: 500; padding: 10px 16px; border-radius: 10px;
      background: #fff; box-shadow: 0 6px 24px rgba(15, 23, 42, .12); color: #475569; font-size: 13px; text-align: center; max-width: 80%; }
    .fp-pin { display: grid; place-items: center; width: 30px; height: 30px; border-radius: 50%; border: 2.5px solid #fff; color: #fff;
      box-shadow: 0 3px 10px rgba(15, 23, 42, .28); transition: transform .15s; }
    .fp-pin:hover { transform: scale(1.12); }
    .fp-pin--device { background: #059669; }
    .fp-pin--asset { background: #7c3aed; }
    .fp-pin-label { position: absolute; top: 32px; left: 50%; transform: translateX(-50%); white-space: nowrap; padding: 1px 6px; border-radius: 6px;
      background: rgba(255, 255, 255, .92); color: #0f172a; font-size: 11px; font-weight: 600; }
    .fp-total { display: inline-flex; align-items: center; gap: 6px; padding: 4px 10px; border-radius: 999px; background: #fff; color: #4c1d95;
      border: 1.5px solid #c4b5fd; box-shadow: 0 4px 14px rgba(76, 29, 149, .18); font-size: 11.5px; font-weight: 600; white-space: nowrap;
      transform: translate(-50%, -50%); }
    .fp-total b { display: inline-grid; place-items: center; min-width: 20px; height: 20px; padding: 0 5px; border-radius: 999px; background: #6d28d9; color: #fff; font-size: 11px; }
    .fp-zone-label { transform: translate(-50%, -100%); white-space: nowrap; padding: 2px 8px; border-radius: 6px; background: var(--c); color: #fff;
      font-size: 11px; font-weight: 600; box-shadow: 0 2px 6px rgba(15, 23, 42, .2); }
    .fp-tip strong { display: block; font-size: 12.5px; }
    .fp-tip span { display: block; color: #64748b; font-size: 11.5px; }
  `,
  encapsulation: ViewEncapsulation.None,
})
export class FloorPlanMap implements AfterViewInit, OnChanges, OnDestroy {
  /** Plan picture (floor plan file, or the floor image from Master Data). */
  @Input() imageUrl: string | null = null;
  @Input() zones: PlanZone[] = [];
  @Input() plots: PlanPlot[] = [];
  /** Zone view: the drawn zone shown alone; undefined = whole floor; null = the picked zone is not drawn yet. */
  @Input() zoneId: string | null | undefined = undefined;
  /** Live assets per gateway, keyed by gateway id in lower case. */
  @Input() feed: Record<string, GatewayFeed> = {};
  @Input() showLabels = true;
  @Input() showAssets = true;
  @Input() showDevices = true;
  /** Shown over the map (e.g. loading / no floor plan). */
  @Input() note = '';

  @ViewChild('host', { static: true }) private host!: ElementRef<HTMLDivElement>;
  private readonly ngZone = inject(NgZone);
  private L?: L;
  private map?: Leaflet.Map;
  private image?: Leaflet.ImageOverlay;
  private shapes?: Leaflet.LayerGroup;
  private markers?: Leaflet.LayerGroup;
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
      map.setView([this.h / 2, W / 2], -1);
      this.resize = new ResizeObserver(() => map.invalidateSize({ animate: false }));
      this.resize.observe(this.host.nativeElement);
      this.sync();
    });
  }

  ngOnChanges(): void {
    if (this.map) this.ngZone.runOutsideAngular(() => this.sync());
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
      this.shapes!.clearLayers();
      this.markers!.clearLayers();
      const url = this.imageUrl;
      if (!url) return;
      const img = new Image();
      img.onload = () => {
        if (url !== this.shownImage || !this.map) return;
        this.h = img.naturalWidth ? (W * img.naturalHeight) / img.naturalWidth : W * 0.6;
        const bounds = L.latLngBounds([0, 0], [this.h, W]);
        this.image = L.imageOverlay(url, bounds, { interactive: false }).addTo(map);
        this.image.bringToBack();
        map.setMaxBounds(bounds.pad(0.4));
        this.shownScope = '\u0000';
        this.draw();
      };
      img.src = url;
      return;
    }
    if (this.image) this.draw();
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
      const style: Leaflet.PathOptions = { color: z.color || '#7c3aed', weight: 2.2, fillColor: z.color || '#7c3aed', fillOpacity: 0.22, dashArray: z.active ? undefined : '6 6' };
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
          html: `<span class="fp-pin fp-pin--${p.kind}">${p.kind === 'device' ? DEVICE_SVG : ASSET_SVG}</span>${this.showLabels ? `<span class="fp-pin-label">${esc(p.code || p.name)}</span>` : ''}`,
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
