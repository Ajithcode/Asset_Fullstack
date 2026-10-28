import { AfterViewInit, Component, ElementRef, Input, OnChanges, OnDestroy, ViewChild } from '@angular/core';

const SATELLITE_TILES = 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}';

/** Satellite map with one labelled pin, used for a site's position. */
@Component({
  selector: 'app-site-map',
  template: `
    @if (lat !== null && lng !== null) {
      <div class="site-map" #mapEl></div>
    } @else {
      <p class="site-map__empty">No coordinates yet. Add latitude and longitude on the Address &amp; Location tab.</p>
    }
  `,
  styles: [
    `
      :host { display: block; height: 100%; min-height: 220px; }
      .site-map { height: 100%; min-height: inherit; border-radius: 10px; overflow: hidden; }
      .site-map__empty {
        margin: 0; padding: 40px 16px; text-align: center; font-size: 13px; color: #94a3b8;
        border: 1px dashed #d6d9e2; border-radius: 10px;
      }
      :host ::ng-deep .site-map-label {
        background: #fff; border: none; border-radius: 8px; box-shadow: 0 2px 8px rgba(15, 23, 42, 0.25);
        font-family: 'Red Hat Display', sans-serif; font-size: 12.5px; font-weight: 700; color: #0f172a; padding: 6px 10px;
      }
      :host ::ng-deep .site-map-label::before { display: none; }
      :host ::ng-deep .site-map-pin { background: transparent; border: none; }
    `
  ]
})
export class SiteMap implements AfterViewInit, OnChanges, OnDestroy {
  @Input() lat: number | null = null;
  @Input() lng: number | null = null;
  @Input() label = '';
  @Input() zoom = 16;

  @ViewChild('mapEl') mapEl?: ElementRef<HTMLDivElement>;

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private map: any;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private marker: any;
  private viewReady = false;

  async ngAfterViewInit(): Promise<void> {
    this.viewReady = true;
    await this.render();
  }

  async ngOnChanges(): Promise<void> {
    if (this.viewReady) {
      // The element is recreated when coordinates appear or disappear
      this.map?.remove();
      this.map = null;
      setTimeout(() => this.render());
    }
  }

  ngOnDestroy(): void {
    this.map?.remove();
  }

  private async render(): Promise<void> {
    if (!this.mapEl || this.lat === null || this.lng === null) return;
    const mod = await import('leaflet');
    // Leaflet is UMD; depending on the bundler the API is on the module or its default export
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const L = (mod as any).map ? mod : (mod as any).default;
    if (!this.map) {
      this.map = L.map(this.mapEl.nativeElement, { attributionControl: false });
      L.tileLayer(SATELLITE_TILES, { maxZoom: 20, maxNativeZoom: 19 }).addTo(this.map);
      L.control.scale({ position: 'bottomleft', imperial: false }).addTo(this.map);
    }
    this.marker?.remove();
    this.marker = L.marker([this.lat, this.lng], {
      icon: L.divIcon({
        className: 'site-map-pin',
        html: '<svg viewBox="0 0 24 24" width="30" height="30"><path fill="#4f2bd8" stroke="#fff" stroke-width="1.5" d="M12 2a7 7 0 0 0-7 7c0 5 7 13 7 13s7-8 7-13a7 7 0 0 0-7-7z"/><circle cx="12" cy="9" r="2.8" fill="#fff"/></svg>',
        iconSize: [30, 30],
        iconAnchor: [15, 28]
      })
    })
      .bindTooltip(this.label, { permanent: true, direction: 'right', offset: [10, -14], className: 'site-map-label' })
      .addTo(this.map);
    this.map.setView([this.lat, this.lng], this.zoom);
    setTimeout(() => this.map?.invalidateSize());
  }
}
