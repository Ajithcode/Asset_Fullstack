import { ChangeDetectionStrategy, Component, Injectable, Input, ViewEncapsulation, inject } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { AuthService } from '../auth/auth.service';

/** How the maps show the plan and its zones. Shared by every map (one value, remembered per user). */
export interface MapViewPrefs {
  /** Floor plan picture opacity, 20 - 100 %. */
  plan: number;
  /** Zone fill opacity, 0 - 60 %. The zone border stays solid. */
  zone: number;
  /** Zone name chips on the map (the "Labels" toggle is for asset labels only). */
  names: boolean;
}

export const MAP_VIEW_DEFAULTS: MapViewPrefs = { plan: 100, zone: 18, names: true };
const clamp = (v: unknown, lo: number, hi: number, d: number): number => {
  const n = Math.round(Number(v));
  return Number.isFinite(n) ? Math.min(hi, Math.max(lo, n)) : d;
};

@Injectable({ providedIn: 'root' })
export class MapViewPrefsService {
  private readonly auth = inject(AuthService);
  private readonly subject = new BehaviorSubject<MapViewPrefs>(this.load());
  readonly prefs$ = this.subject.asObservable();

  get value(): MapViewPrefs {
    return this.subject.value;
  }

  set(change: Partial<MapViewPrefs>): void {
    const v = this.value;
    const next: MapViewPrefs = { plan: clamp(change.plan ?? v.plan, 20, 100, 100), zone: clamp(change.zone ?? v.zone, 0, 60, 18), names: change.names ?? v.names };
    if (next.plan === v.plan && next.zone === v.zone && next.names === v.names) return;
    this.subject.next(next);
    try {
      localStorage.setItem(this.key(), JSON.stringify(next));
    } catch {
      /* private window / blocked storage: the value just lives for this page */
    }
  }

  reset(): void {
    this.set(MAP_VIEW_DEFAULTS);
  }

  private key(): string {
    let who = '';
    try {
      who = this.auth.getUserEmail() ?? '';
    } catch {
      /* no session yet */
    }
    return `a360.mapview:${who}`;
  }

  private load(): MapViewPrefs {
    try {
      const raw = localStorage.getItem(this.key());
      if (raw) {
        const o = JSON.parse(raw) as Partial<MapViewPrefs>;
        return { plan: clamp(o.plan, 20, 100, 100), zone: clamp(o.zone, 0, 60, 18), names: o.names !== false };
      }
    } catch {
      /* unreadable: defaults */
    }
    return { ...MAP_VIEW_DEFAULTS };
  }
}

/**
 * Two small sliders for the strip below a map: "Floor plan" opacity and "Zones" fill opacity (+ a Reset link).
 * The maps listen to MapViewPrefsService, so the change shows while dragging.
 */
@Component({
  selector: 'app-map-opacity',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.Default,
  encapsulation: ViewEncapsulation.None,
  template: `<span class="mvp" role="group" aria-label="Map opacity">
    @if (zoneNames) {
      <label class="mvp-i" title="Show the zone names on the map"><input type="checkbox" [checked]="prefs.value.names" (change)="prefs.set({ names: $any($event.target).checked })" />Zone names</label>
    }
    <label class="mvp-i" title="Floor plan opacity">Floor plan
      <input type="range" min="20" max="100" step="1" [value]="prefs.value.plan" (input)="prefs.set({ plan: +$any($event.target).value })" aria-label="Floor plan opacity" />
      <b>{{ prefs.value.plan }}%</b></label>
    <label class="mvp-i" title="Zone fill opacity (the border stays solid)">Zones
      <input type="range" min="0" max="60" step="1" [value]="prefs.value.zone" (input)="prefs.set({ zone: +$any($event.target).value })" aria-label="Zone fill opacity" />
      <b>{{ prefs.value.zone }}%</b></label>
    <button type="button" class="mvp-reset" (click)="prefs.reset()">Reset</button>
  </span>`,
  styles: `
    .mvp { display: inline-flex; flex-wrap: wrap; align-items: center; gap: 4px 14px; }
    .mvp-i { display: inline-flex; align-items: center; gap: 6px; white-space: nowrap; }
    .mvp-i input[type='checkbox'] { width: 13px; height: 13px; margin: 0; accent-color: #6d28d9; }
    .mvp-i input[type='range'] { width: 84px; height: 14px; margin: 0; accent-color: #6d28d9; cursor: pointer; }
    .mvp-i b { min-width: 2.6em; font-weight: 700; font-variant-numeric: tabular-nums; }
    .mvp-reset { padding: 0; border: 0; background: none; color: #6d28d9; font: inherit; text-decoration: underline; cursor: pointer; }
  `,
})
export class MapOpacity {
  readonly prefs = inject(MapViewPrefsService);
  /** The "Zone names" checkbox (maps that draw zone name chips). */
  @Input() zoneNames = true;
}
