import { Component, Input, OnChanges, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Info, LucideAngularModule, Trash2 } from 'lucide-angular';
import { ACCESS_RULES, MapZone, ZONE_TYPES, zoneIcon } from '../maps.data';
import { MapsStore } from '../maps.store';

/** Right panel form for the selected zone: identity, access rule and geofence alerts. */
@Component({
  standalone: true,
  selector: 'app-zone-form',
  imports: [FormsModule, LucideAngularModule],
  template: `
    @if (draft) {
      <div class="rhead">
        <span class="rhead__icon"><lucide-icon [img]="icon" [size]="17"></lucide-icon></span>
        <h3>{{ draft.name }} ({{ draft.code }})</h3>
        <span class="pill" [attr.data-tone]="draft.active ? 'green' : 'grey'"><i></i>{{ draft.active ? 'Active' : 'Inactive' }}</span>
      </div>

      <form class="rform" (ngSubmit)="save()">
        <div class="field" [class.invalid]="tried && !draft.name.trim()">
          <label for="zf-name">Zone Name <span class="req">*</span></label>
          <input id="zf-name" name="name" [(ngModel)]="draft.name" [disabled]="!store.editing" />
          @if (tried && !draft.name.trim()) { <small>Zone name is required</small> }
        </div>
        <div class="field" [class.invalid]="tried && !codeOk">
          <label for="zf-code">Zone Code <span class="req">*</span></label>
          <input id="zf-code" name="code" [(ngModel)]="draft.code" [disabled]="!store.editing" />
          @if (tried && !codeOk) { <small>{{ draft.code.trim() ? 'Code already used by another zone' : 'Zone code is required' }}</small> }
        </div>
        <div class="field">
          <label for="zf-type">Zone Type <span class="req">*</span></label>
          <select id="zf-type" name="type" [(ngModel)]="draft.type" [disabled]="!store.editing">
            @for (t of types; track t.type) { <option [ngValue]="t.type">{{ t.type }}</option> }
          </select>
        </div>
        <div class="field">
          <label for="zf-parent">Parent Location</label>
          <input id="zf-parent" name="parent" [value]="store.locationLabel()" readonly />
        </div>
        <div class="field">
          <label for="zf-area">Area (calculated)</label>
          <input id="zf-area" name="area" [value]="draft.areaM2.toLocaleString('en-US', { minimumFractionDigits: 2 }) + ' m²'" readonly />
        </div>
        <div class="field">
          <label for="zf-desc">Description</label>
          <textarea id="zf-desc" name="description" rows="3" [(ngModel)]="draft.description" [disabled]="!store.editing"></textarea>
        </div>
        <div class="field">
          <label for="zf-access">Access Rule</label>
          <select id="zf-access" name="access" [(ngModel)]="draft.accessRule" [disabled]="!store.editing">
            @for (r of accessRules; track r) { <option [value]="r">{{ r }}</option> }
          </select>
        </div>

        <h4>
          Geofence &amp; Alerts
          <label class="switch">
            <input type="checkbox" name="geofence" [(ngModel)]="draft.geofence" [disabled]="!store.editing" aria-label="Geofence alerts" /><i></i>
          </label>
        </h4>
        <label class="check-row"><input type="checkbox" name="entry" [(ngModel)]="draft.entryAlert" [disabled]="!draft.geofence || !store.editing" /> Entry Alert</label>
        <label class="check-row"><input type="checkbox" name="exit" [(ngModel)]="draft.exitAlert" [disabled]="!draft.geofence || !store.editing" /> Exit Alert</label>
        <label class="check-row">
          <input type="checkbox" name="dwell" [(ngModel)]="draft.dwellAlert" [disabled]="!draft.geofence || !store.editing" /> Dwell Time Alert
          <lucide-icon [img]="icons.Info" [size]="13" class="muted" title="Alert when someone stays in the zone longer than this"></lucide-icon>
          <input class="mini-input" type="number" min="1" name="dwellMin" aria-label="Dwell minutes" [(ngModel)]="draft.dwellMinutes" [disabled]="!draft.dwellAlert || !draft.geofence || !store.editing" />
          <span class="muted">min</span>
        </label>
        <label class="check-row"><input type="checkbox" name="active" [(ngModel)]="draft.active" [disabled]="!store.editing" /> Zone is active</label>

        <div class="rfoot">
          <button type="button" class="icon-btn btn--danger" aria-label="Delete zone" [disabled]="!store.editing" (click)="remove()">
            <lucide-icon [img]="icons.Trash2" [size]="15"></lucide-icon>
          </button>
          <span class="map-tools__spacer"></span>
          <button type="button" class="btn" (click)="reset()">Cancel</button>
          <button type="submit" class="btn btn--primary" [disabled]="!store.editing">Save Changes</button>
        </div>
      </form>
    } @else {
      <p class="empty">Select a zone on the map, or draw a new one.</p>
    }
  `,
  styleUrls: ['../../shared/admin-workspace.css', '../maps.css'],
  styles: `.rfoot { align-items: center; } .rfoot .icon-btn { color: #ef4444; border-color: #fca5a5; }`
})
export class ZoneForm implements OnChanges {
  readonly store = inject(MapsStore);
  readonly icons = { Info, Trash2 };
  readonly types = ZONE_TYPES;
  readonly accessRules = ACCESS_RULES;

  @Input() zone: MapZone | undefined;

  draft: MapZone | null = null;
  tried = false;

  ngOnChanges(): void {
    this.reset();
  }

  get icon() {
    return zoneIcon(this.draft!.type);
  }

  get codeOk(): boolean {
    const code = this.draft!.code.trim().toUpperCase();
    return !!code && !this.store.zones.some((z) => z.id !== this.draft!.id && z.code.toUpperCase() === code);
  }

  reset(): void {
    this.draft = this.zone ? { ...this.zone } : null;
    this.tried = false;
  }

  save(): void {
    if (!this.draft) return;
    this.tried = true;
    if (!this.draft.name.trim() || !this.codeOk) return;
    this.draft.code = this.draft.code.trim().toUpperCase();
    this.store.saveZone(this.draft);
    this.tried = false;
    this.store.notify(`Zone ${this.draft.code} saved`);
  }

  remove(): void {
    if (!this.draft) return;
    const code = this.draft.code;
    this.store.removeZone(this.draft.id);
    this.store.notify(`Zone ${code} deleted - use Undo to restore it`);
  }
}
