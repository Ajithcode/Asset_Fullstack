import { AfterViewInit, Component, ElementRef, EventEmitter, Input, NgZone, OnChanges, OnDestroy, Output, ViewChild, ViewEncapsulation, inject } from '@angular/core';
import type * as Leaflet from 'leaflet';

type L = typeof Leaflet;

/** One location pin: the assets currently there. */
export interface MapZone {
  zone: string;
  site: string;
  lat: number;
  lng: number;
  assets: { tag: string; name: string; since: string; source: string; unverified: boolean }[];
}

export interface MapSite {
  site: string;
  lat: number;
  lng: number;
}

const TILES = {
  map: { url: 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', opts: { maxZoom: 20, maxNativeZoom: 19, attribution: '&copy; OpenStreetMap contributors' } },
  sat: { url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', opts: { maxZoom: 20, maxNativeZoom: 19, attribution: 'Imagery &copy; Esri' } },
};

const esc = (s: string) => s.replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]!);

/** Leaflet map of the Location History overview: a pin per location with its asset count, site boundaries, popups listing the assets. */
@Component({
  selector: 'app-location-overview-map',
  standalone: true,
  template: '<div #host class="lom-map"></div>',
  styles: [
    `
      app-location-overview-map { display: flex; flex-direction: column; }
      .lom-map { flex: 1 1 auto; min-height: 460px; border: 1px solid #e5e7eb; border-radius: 10px; overflow: hidden; z-index: 0; }
      .lom-pin { display: flex; align-items: center; justify-content: center; }
      .lom-pin span {
        display: flex; align-items: center; justify-content: center; min-width: 30px; height: 30px; padding: 0 6px;
        border: 3px solid #fff; border-radius: 16px; background: #4f2bd8; color: #fff; font: 700 0.813rem 'Red Hat Display', sans-serif;
        box-shadow: 0 3px 10px rgba(30, 27, 75, 0.35);
      }
      .lom-pin.is-warn span { background: #dc2626; }
      .lom-site { font: 700 0.75rem 'Red Hat Display', sans-serif; color: #1e1b4b; background: rgba(255, 255, 255, 0.9); border: 0; box-shadow: none; }
      .lom-pop { font-family: 'Red Hat Display', sans-serif; min-width: 220px; }
      .lom-pop h4 { margin: 0; font-size: 0.875rem; color: #1e1b4b; }
      .lom-pop small { color: #6b7280; }
      .lom-pop ul { margin: 8px 0 0; padding: 0; list-style: none; max-height: 200px; overflow-y: auto; }
      .lom-pop li { display: flex; justify-content: space-between; gap: 10px; padding: 5px 0; border-top: 1px solid #f0f1f3; font-size: 0.781rem; }
      .lom-pop button { padding: 0; border: 0; background: none; font: 700 0.781rem inherit; color: #4f2bd8; cursor: pointer; }
      .lom-pop button:hover { text-decoration: underline; }
      .lom-pop .warn { color: #b91c1c; font-weight: 600; }
    `,
  ],
  encapsulation: ViewEncapsulation.None,
})
export class LocationOverviewMap implements AfterViewInit, OnChanges, OnDestroy {
  @Input() zones: MapZone[] = [];
  @Input() sites: MapSite[] = [];
  /** Asset tag clicked in a popup. */
  @Output() open = new EventEmitter<string>();

  @ViewChild('host', { static: true }) private host!: ElementRef<HTMLDivElement>;
  private readonly ngZone = inject(NgZone);
  private L?: L;
  private map?: Leaflet.Map;
  private layer?: Leaflet.LayerGroup;
  private resize?: ResizeObserver;
  private destroyed = false;

  async ngAfterViewInit(): Promise<void> {
    if (typeof window === 'undefined') return;
    const mod = (await import('leaflet')) as unknown as L & { default?: L };
    // UMD build: the API is the module itself or its default export, depending on the bundler
    const Lf: L = (typeof (mod as { map?: unknown }).map === 'function' ? mod : mod.default) as L;
    if (this.destroyed) return;
    this.L = Lf;
    this.ngZone.runOutsideAngular(() => {
      const map = (this.map = Lf.map(this.host.nativeElement, { zoomControl: true, attributionControl: true }));
      const street = Lf.tileLayer(TILES.map.url, TILES.map.opts).addTo(map);
      const sat = Lf.tileLayer(TILES.sat.url, TILES.sat.opts);
      Lf.control.layers({ Map: street, Satellite: sat }, undefined, { position: 'topright' }).addTo(map);
      this.layer = Lf.layerGroup().addTo(map);
      this.draw(true);
      this.resize = new ResizeObserver(() => map.invalidateSize());
      this.resize.observe(this.host.nativeElement);
    });
  }

  ngOnChanges(): void {
    if (this.map) this.ngZone.runOutsideAngular(() => this.draw(false));
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    this.resize?.disconnect();
    this.map?.remove();
  }

  private draw(fit: boolean): void {
    const Lf = this.L;
    const map = this.map;
    const layer = this.layer;
    if (!Lf || !map || !layer) return;
    layer.clearLayers();

    // Site boundaries
    for (const s of this.sites) {
      Lf.circle([s.lat, s.lng], { radius: 115, color: '#7c5cf5', weight: 1.5, dashArray: '5 5', fillColor: '#7c5cf5', fillOpacity: 0.06 })
        .bindTooltip(esc(s.site), { permanent: true, direction: 'top', offset: [0, -100], className: 'lom-site' })
        .addTo(layer);
    }

    // One pin per location, sized by its asset count
    for (const z of this.zones) {
      const warn = z.assets.some((a) => a.unverified);
      const icon = Lf.divIcon({ className: 'lom-pin' + (warn ? ' is-warn' : ''), html: `<span>${z.assets.length}</span>`, iconSize: [40, 36], iconAnchor: [20, 18] });
      const marker = Lf.marker([z.lat, z.lng], { icon, title: `${z.zone}: ${z.assets.length} assets` }).addTo(layer);
      marker.bindTooltip(esc(z.zone), { direction: 'bottom', offset: [0, 14] });
      marker.bindPopup(() => this.popup(z), { maxWidth: 300 });
    }

    if (fit) {
      const pts = [...this.zones.map((z) => [z.lat, z.lng] as [number, number]), ...this.sites.map((s) => [s.lat, s.lng] as [number, number])];
      if (pts.length) map.fitBounds(Lf.latLngBounds(pts), { padding: [40, 40], maxZoom: 18 });
      else map.setView([13.098, 80.163], 15);
    }
  }

  private popup(z: MapZone): HTMLElement {
    const el = document.createElement('div');
    el.className = 'lom-pop';
    el.innerHTML =
      `<h4>${esc(z.zone)}</h4><small>${esc(z.site)} · ${z.assets.length} asset${z.assets.length === 1 ? '' : 's'}</small><ul>` +
      z.assets
        .map(
          (a) =>
            `<li><span><button type="button" data-tag="${esc(a.tag)}">${esc(a.tag)}</button><br /><small>${esc(a.name)}</small>` +
            `${a.unverified ? '<br /><small class="warn">Unverified</small>' : ''}</span><small>${esc(a.since)}<br />${esc(a.source)}</small></li>`,
        )
        .join('') +
      '</ul>';
    el.addEventListener('click', (e) => {
      const tag = (e.target as HTMLElement).closest('button')?.getAttribute('data-tag');
      if (tag) this.ngZone.run(() => this.open.emit(tag));
    });
    return el;
  }
}
