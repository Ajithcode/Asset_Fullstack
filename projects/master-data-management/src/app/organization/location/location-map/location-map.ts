import {
  AfterViewInit,
  Component,
  ElementRef,
  Input,
  OnChanges,
  OnDestroy,
  ViewChild,
  inject
} from '@angular/core';
import { LucideAngularModule, Maximize2, Minimize2 } from 'lucide-angular';
import { findBuilding, zonesOnFloor } from '../location.data';
import { Bounds, buildingBounds, locationBounds, zoneCells } from '../location.geometry';
import { LocationRecord } from '../location.model';
import { LocationService } from '../location.service';

const ZONE_COLORS = ['#22c55e', '#f59e0b', '#14b8a6', '#ec4899', '#0ea5e9', '#a3e635'];
const SATELLITE_TILES = 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}';

interface PlanShape {
  x: number;
  y: number;
  w: number;
  h: number;
  label: string;
  color: string;
}

/**
 * Map & Layout card for one location: a satellite map with the building, its floor's zones and
 * the location drawn on top, or the same layout as a simple floor plan.
 */
@Component({
  selector: 'app-location-map',
  imports: [LucideAngularModule],
  templateUrl: './location-map.html',
  styleUrl: './location-map.css'
})
export class LocationMap implements AfterViewInit, OnChanges, OnDestroy {
  @Input({ required: true }) location!: LocationRecord;
  @Input() view: 'map' | 'plan' = 'map';

  @ViewChild('mapEl') mapEl?: ElementRef<HTMLDivElement>;

  private readonly service = inject(LocationService);
  readonly icons = { Maximize2, Minimize2 };

  expanded = false;
  plan: { building: PlanShape | null; zones: PlanShape[]; location: PlanShape | null } = { building: null, zones: [], location: null };

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private L: any;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private map: any;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private layers: any;
  private viewReady = false;

  async ngAfterViewInit(): Promise<void> {
    this.viewReady = true;
    await this.render();
  }

  async ngOnChanges(): Promise<void> {
    this.buildPlan();
    if (this.viewReady) await this.render();
  }

  ngOnDestroy(): void {
    this.map?.remove();
  }

  setView(view: 'map' | 'plan'): void {
    this.view = view;
    // The map element is recreated when switching back, so start a fresh map
    this.map?.remove();
    this.map = null;
    setTimeout(() => this.render());
  }

  toggleExpanded(): void {
    this.expanded = !this.expanded;
    setTimeout(() => this.map?.invalidateSize(), 220);
  }

  private async render(): Promise<void> {
    if (this.view !== 'map' || !this.mapEl || !this.location) return;
    if (!this.L) {
      const mod = await import('leaflet');
      // Leaflet is UMD; depending on the bundler the API is on the module or its default export
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      this.L = (mod as any).map ? mod : (mod as any).default;
    }
    const L = this.L;
    if (!this.map) {
      this.map = L.map(this.mapEl.nativeElement, { zoomControl: true, attributionControl: false });
      L.tileLayer(SATELLITE_TILES, { maxZoom: 20, maxNativeZoom: 19 }).addTo(this.map);
      L.control.scale({ position: 'bottomright', imperial: false }).addTo(this.map);
      this.layers = L.layerGroup().addTo(this.map);
    }
    this.layers.clearLayers();

    const building = findBuilding(this.location.buildingCode);
    if (!building) return;
    const outline = buildingBounds(building);
    L.rectangle(this.toLatLngs(outline), { color: '#2563eb', weight: 2, fillOpacity: 0.04 }).addTo(this.layers);

    const zones = zonesOnFloor(building.code, this.location.floorCode);
    const cells = zoneCells(building, zones);
    zones.forEach((zone, i) => {
      const cell = cells.get(zone.code)!;
      const color = ZONE_COLORS[i % ZONE_COLORS.length];
      const current = zone.code === this.location.zoneCode;
      const rect = L.rectangle(this.toLatLngs(cell), {
        color: current ? '#7c3aed' : color,
        weight: current ? 2.5 : 2,
        fillColor: current ? '#7c3aed' : color,
        fillOpacity: current ? 0.18 : 0.32
      }).addTo(this.layers);
      // The location's own label covers its zone, so only other zones get a name tag
      rect.bindTooltip(zone.name, { permanent: !current, direction: 'center', className: 'loc-map-label' });
    });

    const cell = cells.get(this.location.zoneCode);
    const box = cell ? locationBounds(cell, this.location.code) : null;
    if (box) {
      L.rectangle(this.toLatLngs(box), { color: '#ffffff', weight: 2, fillColor: '#7c3aed', fillOpacity: 0.55 })
        .bindTooltip(this.location.name, { permanent: true, direction: 'top', className: 'loc-map-label loc-map-label--active' })
        .addTo(this.layers);
    }
    L.marker([this.location.lat, this.location.lng], {
      icon: L.divIcon({
        className: 'loc-map-pin',
        html: '<svg viewBox="0 0 24 24" width="22" height="22"><path fill="#ef4444" stroke="#fff" stroke-width="1.5" d="M12 2a7 7 0 0 0-7 7c0 5 7 13 7 13s7-8 7-13a7 7 0 0 0-7-7z"/><circle cx="12" cy="9" r="2.6" fill="#fff"/></svg>',
        iconSize: [22, 22],
        iconAnchor: [11, 21]
      })
    }).addTo(this.layers);

    this.map.fitBounds(this.toLatLngs(outline), { padding: [12, 12] });
    setTimeout(() => this.map?.invalidateSize());
  }

  // Same layout as the map, drawn into a 400 x 260 SVG floor plan
  private buildPlan(): void {
    const building = this.location && findBuilding(this.location.buildingCode);
    if (!building) {
      this.plan = { building: null, zones: [], location: null };
      return;
    }
    const outline = buildingBounds(building);
    const toPlan = (b: Bounds, label: string, color: string): PlanShape => {
      const pad = 16;
      const sx = (400 - pad * 2) / (outline[3] - outline[1]);
      const sy = (260 - pad * 2) / (outline[2] - outline[0]);
      return { x: pad + (b[1] - outline[1]) * sx, y: pad + (outline[2] - b[2]) * sy, w: (b[3] - b[1]) * sx, h: (b[2] - b[0]) * sy, label, color };
    };
    const zones = zonesOnFloor(building.code, this.location.floorCode);
    const cells = zoneCells(building, zones);
    const cell = cells.get(this.location.zoneCode);
    this.plan = {
      building: toPlan(outline, `${building.name} · ${this.service.floorName(this.location.floorCode)}`, '#2563eb'),
      zones: zones.map((z, i) =>
        toPlan(cells.get(z.code)!, z.name, z.code === this.location.zoneCode ? '#7c3aed' : ZONE_COLORS[i % ZONE_COLORS.length])
      ),
      location: cell ? toPlan(locationBounds(cell, this.location.code), this.location.name, '#7c3aed') : null
    };
  }

  private toLatLngs(b: Bounds): [[number, number], [number, number]] {
    return [
      [b[0], b[1]],
      [b[2], b[3]]
    ];
  }
}
