import { Component, ElementRef, EventEmitter, Input, Output, ViewChild, inject } from '@angular/core';
import { LucideAngularModule } from 'lucide-angular';
import { MapZone, PlotItem, TONE_HEX, zoneIcon } from '../maps.data';
import { MapsStore } from '../maps.store';

/**
 * The floor plan drawing with zones and plotted devices / assets on top. Positions are kept in % of the plan,
 * so the overlay scales with the zoom. Used by every map tab and, with `preview`, as a thumbnail.
 */
@Component({
  standalone: true,
  selector: 'app-map-canvas',
  imports: [LucideAngularModule],
  template: `
    <div class="viewport" [class.viewport--preview]="preview" [class.placing]="placing">
      <div class="plan" #plan [style.width.%]="zoom" (click)="onPlanClick($event)">
        <svg class="plan__base" viewBox="0 0 600 320" preserveAspectRatio="none" aria-hidden="true">
          <rect x="6" y="6" width="588" height="308" fill="#fff" stroke="#334155" stroke-width="5" />
          <!-- interior walls and rooms -->
          <g stroke="#94a3b8" stroke-width="1.6" fill="none">
            <path d="M120 6 V300 M6 140 H120 M6 230 H120 M120 230 H170 V314 M170 270 H400 M400 230 V314 M400 230 H594" />
            <path d="M405 40 H594 M405 140 H594 M405 220 H594 M405 40 V220" />
            <path d="M440 230 V314 M480 230 V314 M520 230 V314 M560 230 V314" />
            <path d="M30 250 V300 M60 250 V300 M90 250 V300 M200 290 H380" />
          </g>
          <g stroke="#cbd5e1" stroke-width="1" fill="none">
            @for (r of racks; track $index) {
              <rect [attr.x]="r[0]" [attr.y]="r[1]" width="44" height="16" />
            }
          </g>
          <g fill="#e2e8f0" stroke="#94a3b8">
            <rect x="122" y="150" width="20" height="40" />
            <rect x="570" y="250" width="20" height="40" />
          </g>
        </svg>

        @if (showZones) {
          @for (z of store.zones; track z.id) {
            <button
              type="button"
              class="zone"
              [class.zone--circle]="z.shape === 'Circle'"
              [class.active]="z.id === selectedZoneId"
              [style.left.%]="z.x"
              [style.top.%]="z.y"
              [style.width.%]="z.w"
              [style.height.%]="z.h"
              [style.--zone]="hex(z)"
              [attr.aria-label]="z.name + ' ' + z.code"
              [tabindex]="preview ? -1 : 0"
              (click)="onZone($event, z)"
            >
              @if (!preview) {
                <lucide-icon [img]="icon(z)" [size]="22"></lucide-icon>
                @if (showLabels) {
                  <strong>{{ z.name }}</strong>
                  <small>({{ z.code }})</small>
                }
              } @else {
                <small class="zone__mini">{{ z.name }}<br />({{ z.code }})</small>
              }
            </button>
          }
        }

        @if (!preview) {
          @for (i of store.plotted; track i.id) {
            @if ((i.kind === 'device' && showDevices) || (i.kind === 'asset' && showAssets)) {
              <button
                type="button"
                class="marker"
                [class.marker--asset]="i.kind === 'asset'"
                [class.active]="i.id === selectedItemId"
                [style.left.%]="i.x"
                [style.top.%]="i.y"
                [style.--tone]="toneHex(i)"
                [attr.aria-label]="i.name + ' ' + i.code"
                (click)="onItem($event, i)"
              >
                <span class="marker__dot"><lucide-icon [img]="i.icon" [size]="13"></lucide-icon></span>
                @if (showLabels && showItemLabels) {
                  <span class="marker__label">{{ i.code }}</span>
                }
              </button>
            }
          }
        }
      </div>

      @if (!preview && showMinimap) {
        <div class="minimap" aria-hidden="true">
          <svg viewBox="0 0 600 320"><rect x="6" y="6" width="588" height="308" fill="#fff" stroke="#475569" stroke-width="10" /></svg>
          <i [style.width.%]="100 / (zoom / 100)"></i>
        </div>
      }
      @if (!preview && showScale) {
        <div class="scale" aria-hidden="true">
          <span class="scale__bar"></span>
          <span class="scale__nums"><b>0</b><b>5</b><b>10</b><b>20 m</b></span>
        </div>
      }
    </div>
  `,
  styles: `
    :host { display: block; min-width: 0; }
    .viewport {
      position: relative;
      overflow: auto;
      border: 1px solid #e6e8ef;
      border-radius: 10px;
      background: #f8fafc;
      padding: 14px 14px 64px;
      min-height: 360px;
    }
    .viewport--preview { padding: 6px; min-height: 0; overflow: hidden; background: #fff; }
    .viewport.placing .plan { cursor: crosshair; }
    .plan { position: relative; aspect-ratio: 600 / 320; margin: 0 auto; transition: width 0.15s ease; }
    .plan__base { position: absolute; inset: 0; width: 100%; height: 100%; display: block; }
    .zone {
      position: absolute;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 2px;
      padding: 4px;
      border: 1.5px dashed var(--zone);
      border-radius: 4px;
      background: color-mix(in srgb, var(--zone) 22%, transparent);
      color: color-mix(in srgb, var(--zone) 70%, #0f172a);
      font: inherit;
      text-align: center;
      cursor: pointer;
      overflow: hidden;
    }
    .zone--circle { border-radius: 50%; }
    .zone:hover { background: color-mix(in srgb, var(--zone) 32%, transparent); }
    .zone.active { border-style: solid; border-width: 2px; box-shadow: 0 0 0 3px color-mix(in srgb, var(--zone) 25%, transparent); }
    .zone strong { font-size: 12px; font-weight: 700; line-height: 1.15; color: #1e293b; }
    .zone small { font-size: 11px; color: #334155; }
    .zone__mini { font-size: 7px !important; line-height: 1.1; }
    .viewport--preview .zone { border-width: 1px; cursor: default; }
    .marker {
      position: absolute;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 2px;
      padding: 0;
      border: 0;
      background: none;
      transform: translate(-50%, -50%);
      cursor: pointer;
      font: inherit;
      z-index: 2;
    }
    .marker__dot {
      display: grid;
      place-items: center;
      width: 24px;
      height: 24px;
      border-radius: 50%;
      background: var(--tone);
      color: #fff;
      box-shadow: 0 0 0 2px #fff, 0 2px 6px rgba(15, 23, 42, 0.25);
    }
    .marker--asset .marker__dot { border-radius: 6px; }
    .marker.active .marker__dot { box-shadow: 0 0 0 2px #fff, 0 0 0 5px var(--tone); }
    .marker__label {
      padding: 0 4px;
      border-radius: 4px;
      background: rgba(255, 255, 255, 0.92);
      font-size: 10.5px;
      font-weight: 700;
      color: #0f172a;
      white-space: nowrap;
    }
    .minimap {
      position: sticky;
      float: right;
      right: 0;
      bottom: 0;
      width: 110px;
      margin-top: -54px;
      padding: 4px;
      border: 1px solid #e6e8ef;
      border-radius: 8px;
      background: #fff;
    }
    .minimap svg { display: block; width: 100%; height: auto; }
    .minimap i { position: absolute; left: 4px; top: 4px; bottom: 4px; max-width: calc(100% - 8px); border: 2px solid #7c3aed; background: rgba(124, 58, 237, 0.15); }
    .scale { position: sticky; left: 0; float: left; margin-top: -42px; font-size: 10px; color: #334155; }
    .scale__bar { display: block; width: 120px; height: 6px; border: 1.5px solid #334155; border-top: 0; }
    .scale__nums { display: flex; justify-content: space-between; width: 140px; }
    .scale__nums b { font-weight: 600; }
  `
})
export class MapCanvas {
  readonly store = inject(MapsStore);

  @ViewChild('plan') planEl?: ElementRef<HTMLElement>;

  /** Width of the plan in % of the viewport (100 = fit). */
  @Input() zoom = 100;
  @Input() showZones = true;
  @Input() showDevices = true;
  @Input() showAssets = true;
  @Input() showLabels = true;
  @Input() showItemLabels = true;
  @Input() showMinimap = true;
  @Input() showScale = true;
  @Input() preview = false;
  @Input() selectedZoneId: string | null = null;
  @Input() selectedItemId: string | null = null;
  /** While set, a click on the plan reports its position instead of selecting. */
  @Input() placing = false;

  @Output() zoneSelect = new EventEmitter<MapZone>();
  @Output() itemSelect = new EventEmitter<PlotItem>();
  @Output() place = new EventEmitter<{ x: number; y: number }>();

  readonly racks = [
    [150, 60], [210, 60], [270, 60], [330, 60], [150, 100], [210, 100], [270, 100], [330, 100], [150, 170], [270, 170], [330, 170]
  ];

  icon(z: MapZone) {
    return zoneIcon(z.type);
  }

  hex(z: MapZone): string {
    return TONE_HEX[z.tone];
  }

  toneHex(i: PlotItem): string {
    return TONE_HEX[i.tone];
  }

  onZone(event: MouseEvent, z: MapZone): void {
    if (this.preview || this.placing) return;
    event.stopPropagation();
    this.zoneSelect.emit(z);
  }

  onItem(event: MouseEvent, i: PlotItem): void {
    if (this.placing) return;
    event.stopPropagation();
    this.itemSelect.emit(i);
  }

  onPlanClick(event: MouseEvent): void {
    if (!this.placing || !this.planEl) return;
    const box = this.planEl.nativeElement.getBoundingClientRect();
    this.place.emit({ x: ((event.clientX - box.left) / box.width) * 100, y: ((event.clientY - box.top) / box.height) * 100 });
  }
}
