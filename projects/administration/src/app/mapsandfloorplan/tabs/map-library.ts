import { Component, EventEmitter, HostListener, Output, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  ChevronDown,
  Circle,
  Cpu,
  Hexagon,
  LucideAngularModule,
  MousePointer2,
  Package,
  PenTool,
  Square,
  Upload
} from 'lucide-angular';
import { MapCanvas } from '../map-canvas/map-canvas';
import { MapViewer } from '../map-viewer/map-viewer';
import { MapZone, PlotItem, PlotKind, fmtDate } from '../maps.data';
import { MapsStore } from '../maps.store';
import { SiteTree } from '../site-tree/site-tree';
import { UploadDialog } from '../upload-dialog/upload-dialog';
import { ZoneForm } from '../zone-form/zone-form';

/** Map Library: browse sites / floors, view the map, draw zones and plot items; zone details on the right. */
@Component({
  standalone: true,
  selector: 'app-maps-library',
  imports: [FormsModule, LucideAngularModule, SiteTree, MapViewer, MapCanvas, ZoneForm, UploadDialog],
  templateUrl: './map-library.html',
  styleUrls: ['../../shared/admin-workspace.css', '../maps.css']
})
export class MapLibraryTab {
  readonly store = inject(MapsStore);
  readonly icons = { ChevronDown, Circle, Cpu, Hexagon, MousePointer2, Package, PenTool, Square, Upload };
  readonly shapes: { shape: MapZone['shape']; label: string; icon: typeof Square }[] = [
    { shape: 'Polygon', label: 'Polygon Zone', icon: Hexagon },
    { shape: 'Rectangle', label: 'Rectangle Zone', icon: Square },
    { shape: 'Circle', label: 'Circle Zone', icon: Circle },
    { shape: 'Custom', label: 'Custom Shape', icon: PenTool }
  ];

  @Output() goTo = new EventEmitter<string>();

  rightTab: 'zone' | 'layers' | 'info' = 'zone';
  drawOpen = false;
  uploadMenuOpen = false;
  uploadOpen = false;
  /** Plot Device / Plot Asset: the next plan click drops the first unplotted item of that kind. */
  placing: PlotKind | null = null;
  show = { zones: true, devices: true, assets: true, labels: true };

  readonly usedGb = 3.2;
  readonly totalGb = 10;
  date = fmtDate;

  @HostListener('document:click')
  closeMenus(): void {
    this.drawOpen = false;
    this.uploadMenuOpen = false;
  }

  toggleDraw(event: MouseEvent): void {
    event.stopPropagation();
    this.drawOpen = !this.drawOpen;
    this.placing = null;
  }

  draw(shape: MapZone['shape']): void {
    this.drawOpen = false;
    const zone = this.store.addZone(shape);
    this.rightTab = 'zone';
    this.store.notify(`${zone.name} added - fill in its details and save`);
  }

  startPlacing(kind: PlotKind): void {
    if (this.placing === kind) {
      this.placing = null;
      return;
    }
    if (!this.store.unplotted.some((i) => i.kind === kind)) {
      this.store.notify(`Every ${kind} is already plotted - use Device & Asset Plotting to reposition`);
      return;
    }
    this.placing = kind;
  }

  place(pos: { x: number; y: number }): void {
    const item = this.store.unplotted.find((i) => i.kind === this.placing);
    this.placing = null;
    if (!item) return;
    this.store.plot(item.id, pos.x, pos.y);
    this.store.selectedItemId = item.id;
    this.store.notify(`${item.name} (${item.code}) plotted`);
  }

  selectZone(z: MapZone): void {
    this.store.selectedZoneId = z.id;
    this.rightTab = 'zone';
  }

  selectItem(i: PlotItem): void {
    this.store.selectedItemId = i.id;
    this.goTo.emit('device-asset-plotting');
  }

  get storagePct(): number {
    return Math.round((this.usedGb / this.totalGb) * 100);
  }

  toggleUploadMenu(event: MouseEvent): void {
    event.stopPropagation();
    this.uploadMenuOpen = !this.uploadMenuOpen;
  }

  uploaded(name: string): void {
    this.uploadOpen = false;
    this.store.notify(`${name} uploaded as a draft`);
  }
}
