import { Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Box, Clock, Layers, LucideAngularModule, LucideIconData, MapPin, MapPinned, Settings, Shield, SquareStack } from 'lucide-angular';
import { ADMINISTRATION_BASE_PATH, ADMINISTRATION_MENU } from 'shared-ui';
import { MapsTabId } from './maps.data';
import { MapsStore } from './maps.store';
import { FloorPlansTab } from './tabs/floor-plans';
import { LayersTab } from './tabs/layers';
import { MapLibraryTab } from './tabs/map-library';
import { PlottingTab } from './tabs/plotting';
import { SettingsTab } from './tabs/settings';
import { VersionsTab } from './tabs/versions';
import { ZonesTab } from './tabs/zones';

const MODULE = ADMINISTRATION_MENU.find((m) => m.slug === 'maps-floor-plans')!;

const TAB_ICONS: Record<MapsTabId, LucideIconData> = {
  'map-library': Box,
  'floor-plans': SquareStack,
  'zones-geofences': Shield,
  'device-asset-plotting': MapPin,
  layers: Layers,
  'versions-publishing': Clock,
  settings: Settings
};

/**
 * Administration > Maps & Floor Plans. One page per sub module: the route's `:tab` (the sidebar child slug)
 * picks the tab, and the tab bar navigates, so the sidebar and the tabs always agree.
 */
@Component({
  standalone: true,
  selector: 'app-mapsandfloorplan',
  imports: [RouterLink, LucideAngularModule, MapLibraryTab, FloorPlansTab, ZonesTab, PlottingTab, LayersTab, VersionsTab, SettingsTab],
  providers: [MapsStore],
  templateUrl: './mapsandfloorplan.html',
  styleUrls: ['../shared/admin-workspace.css', './maps.css', './mapsandfloorplan.css']
})
export class Mapsandfloorplan {
  readonly store = inject(MapsStore);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly icons = { MapPinned };
  readonly basePath = `${ADMINISTRATION_BASE_PATH}/${MODULE.slug}`;
  readonly tabs = MODULE.children!.map((c) => ({ id: c.slug as MapsTabId, label: c.label, icon: TAB_ICONS[c.slug as MapsTabId] ?? Box }));

  active: MapsTabId = this.tabs[0].id;

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed(inject(DestroyRef))).subscribe((params) => {
      const tab = params.get('tab') as MapsTabId | null;
      if (tab && this.tabs.some((t) => t.id === tab)) this.active = tab;
      // Unknown sub page: fall back to the first tab without leaving a bad entry in history
      else void this.router.navigate([this.basePath, this.tabs[0].id], { replaceUrl: true });
    });
  }

  get activeLabel(): string {
    return this.tabs.find((t) => t.id === this.active)!.label;
  }

  /** Cross-tab links (e.g. "View on Map", "Manage Zones") go through the router like the tab bar. */
  goTo(tab: string): void {
    void this.router.navigate([this.basePath, tab]);
  }
}
