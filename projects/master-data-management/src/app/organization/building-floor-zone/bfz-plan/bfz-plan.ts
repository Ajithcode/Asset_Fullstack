import { AfterViewInit, Component, ElementRef, Input, OnChanges, OnDestroy, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LucideAngularModule, Maximize2, Minimize2 } from 'lucide-angular';
import { BfzRecord } from '../bfz.model';
import { BfzService } from '../bfz.service';

type PlanView = 'map' | 'plan' | '3d';
// x, y, width, height as fractions of the floor
type Frac = [number, number, number, number];

const SATELLITE_TILES = 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}';
// Half the building footprint in degrees (~170 m x 200 m)
const HALF_LAT = 0.0008;
const HALF_LNG = 0.001;

/**
 * Lays a floor's zones out without survey data: one zone fills the floor, three put the first on
 * the left and stack the others on the right (as in the design), otherwise a two-column grid.
 */
export function zoneLayout(count: number): Frac[] {
  if (count <= 0) return [];
  if (count === 1) return [[0, 0, 1, 1]];
  if (count === 2) return [[0, 0, 0.5, 1], [0.5, 0, 0.5, 1]];
  if (count === 3) return [[0, 0, 0.52, 1], [0.52, 0, 0.48, 0.5], [0.52, 0.5, 0.48, 0.5]];
  const rows = Math.ceil(count / 2);
  return Array.from({ length: count }, (_, i) => [(i % 2) * 0.5, Math.floor(i / 2) / rows, 0.5, 1 / rows] as Frac);
}

/** "Floor Plan / Map" card for a building: map, floor plan and a stacked 3D view, one floor at a time. */
@Component({
  selector: 'app-bfz-plan',
  imports: [FormsModule, LucideAngularModule],
  templateUrl: './bfz-plan.html',
  styleUrl: './bfz-plan.css'
})
export class BfzPlan implements AfterViewInit, OnChanges, OnDestroy {
  @Input({ required: true }) building!: BfzRecord;
  // Floor to open with, and a zone to highlight
  @Input() floorCode: string | null = null;
  @Input() zoneCode: string | null = null;
  @Input() view: PlanView = 'plan';

  @ViewChild('mapEl') mapEl?: ElementRef<HTMLDivElement>;

  private readonly service = inject(BfzService);
  readonly icons = { Maximize2, Minimize2 };
  readonly views: { id: PlanView; label: string }[] = [
    { id: 'map', label: 'Map View' },
    { id: 'plan', label: 'Floor Plan' },
    { id: '3d', label: '3D View' }
  ];

  selectedFloor: string | null = null;
  expanded = false;

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private L: any;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private map: any;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private layers: any;
  private viewReady = false;

  get floors(): BfzRecord[] {
    return this.service.floorsOf(this.building.code);
  }

  get zones(): BfzRecord[] {
    return this.service.zonesOfFloor(this.selectedFloor);
  }

  // Zones placed on the 300 x 240 plan image (inside its walls)
  get planZones(): { zone: BfzRecord; x: number; y: number; w: number; h: number }[] {
    const [ox, oy, w, h] = [22, 22, 256, 196];
    const layout = zoneLayout(this.zones.length);
    return this.zones.map((zone, i) => {
      const [fx, fy, fw, fh] = layout[i];
      return { zone, x: ox + fx * w + 2, y: oy + fy * h + 2, w: fw * w - 4, h: fh * h - 4 };
    });
  }

  // Floors stacked bottom-up for the 3D view
  get stack(): { floor: BfzRecord; y: number }[] {
    const floors = this.floors;
    const gap = Math.min(34, 170 / Math.max(floors.length, 1));
    return floors.map((floor, i) => ({ floor, y: 196 - i * gap })).reverse();
  }

  async ngAfterViewInit(): Promise<void> {
    this.viewReady = true;
    await this.renderMap();
  }

  async ngOnChanges(): Promise<void> {
    const floors = this.floors;
    this.selectedFloor = floors.some((f) => f.code === this.floorCode) ? this.floorCode : (floors[0]?.code ?? null);
    if (this.viewReady) await this.renderMap();
  }

  ngOnDestroy(): void {
    this.map?.remove();
  }

  setView(view: PlanView): void {
    this.view = view;
    this.map?.remove();
    this.map = null;
    setTimeout(() => this.renderMap());
  }

  onFloorChange(): void {
    this.renderMap();
  }

  pickFloor(code: string): void {
    this.selectedFloor = code;
  }

  toggleExpanded(): void {
    this.expanded = !this.expanded;
    setTimeout(() => this.map?.invalidateSize(), 220);
  }

  private async renderMap(): Promise<void> {
    if (this.view !== 'map' || !this.mapEl) return;
    const site = this.service.site(this.building.siteId);
    const lat = this.building.lat ?? site?.lat;
    const lng = this.building.lng ?? site?.lng;
    if (lat === undefined || lng === undefined || lat === null || lng === null) return;
    if (!this.L) {
      const mod = await import('leaflet');
      // Leaflet is UMD; depending on the bundler the API is on the module or its default export
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      this.L = (mod as any).map ? mod : (mod as any).default;
    }
    const L = this.L;
    if (!this.map) {
      this.map = L.map(this.mapEl.nativeElement, { attributionControl: false });
      L.tileLayer(SATELLITE_TILES, { maxZoom: 20, maxNativeZoom: 19 }).addTo(this.map);
      L.control.scale({ position: 'bottomright', imperial: false }).addTo(this.map);
      this.layers = L.layerGroup().addTo(this.map);
    }
    this.layers.clearLayers();

    const [s, w, n, e] = [lat - HALF_LAT, lng - HALF_LNG, lat + HALF_LAT, lng + HALF_LNG];
    L.rectangle([[s, w], [n, e]], { color: '#2563eb', weight: 2, fillOpacity: 0.05 })
      .bindTooltip(this.service.label(this.building), { permanent: true, direction: 'top', className: 'bfz-map-label' })
      .addTo(this.layers);
    const layout = zoneLayout(this.zones.length);
    this.zones.forEach((zone, i) => {
      const [fx, fy, fw, fh] = layout[i];
      const top = n - fy * (n - s);
      const left = w + fx * (e - w);
      const highlight = zone.code === this.zoneCode;
      L.rectangle([[top - fh * (n - s), left], [top, left + fw * (e - w)]], {
        color: highlight ? '#7c3aed' : zone.color,
        weight: highlight ? 3 : 2,
        fillColor: zone.color,
        fillOpacity: 0.35
      })
        .bindTooltip(zone.name, { permanent: true, direction: 'center', className: 'bfz-map-label' })
        .addTo(this.layers);
    });
    this.map.fitBounds([[s, w], [n, e]], { padding: [16, 16] });
    setTimeout(() => this.map?.invalidateSize());
  }
}
