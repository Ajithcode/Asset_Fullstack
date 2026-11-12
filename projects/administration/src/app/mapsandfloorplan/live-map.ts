import { inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AssetTelemetryService, GatewayAsset, GatewayAssets, LiveState } from './asset-telemetry.service';
import { ZonePopupInfo } from './asset-live-popup/asset-live-popup';
import { MapZone, PlotItem } from './maps.data';

/**
 * Live gateway data on a plan map, shared by the tabs that show it (Map Library, Device & Asset Plotting): the
 * "Total Assets" count on each zone and the popup over a clicked asset, device or zone count.
 * Create it in a component field initializer (it injects and unsubscribes with the component).
 */
export class LiveMap {
  /**
   * Assets per gateway from the Dashboard service (live): a tag counts at the gateway of its newest reading
   * (NetworkTimeSeries meta.gatewayid), and the tag's asset comes from Tag Management. Keyed by gateway id in lower case.
   */
  gatewayCounts: Record<string, { count: number; names: string[] }> | null = null;
  /** The same feed in full (assets, tags, last reading) for the device popup, keyed by gateway id in lower case. */
  private gateways: Record<string, GatewayAssets> = {};
  gatewayState: LiveState = 'connecting';

  /** Asset / device whose popup is open over the map; null when closed. */
  popupItem: PlotItem | null = null;
  /** Where the popup's item sits on the map (pixels), so the popup opens above it. */
  popupAnchor: { x: number; y: number; inView: boolean } | null = null;
  /** A zone whose "Total Assets" label was clicked: its popup lists the linked assets its gateways hear. */
  zonePopup: { zone: MapZone; gateways: PlotItem[] } | null = null;

  constructor() {
    inject(AssetTelemetryService)
      .gatewaysLive()
      .pipe(takeUntilDestroyed())
      .subscribe((m) => {
        this.gatewayState = m.state;
        if (!m.data) return;
        const counts: Record<string, { count: number; names: string[] }> = {};
        const full: Record<string, GatewayAssets> = {};
        for (const g of m.data) {
          const key = g.gatewayId.toLowerCase();
          full[key] = g;
          counts[key] = { count: g.assetCount, names: g.assets.map((a) => (a.assetName ? `${a.assetName} (${a.assetId})` : a.assetId)) };
        }
        this.gateways = full;
        this.gatewayCounts = counts;
      });
  }

  /** What the gateways feed says about a device (matched by device id or code); null when it reports nothing. */
  gatewayOf(i: PlotItem): GatewayAssets | null {
    return this.gateways[i.itemId?.toLowerCase()] ?? this.gateways[i.code?.toLowerCase()] ?? null;
  }

  /** Opens the popup of an asset or device. */
  open(i: PlotItem): void {
    this.popupItem = i;
    this.zonePopup = null;
  }

  openZoneTotals(e: { zone: MapZone; gateways: PlotItem[] }): void {
    if (!e.gateways.length) return;
    this.zonePopup = e;
    // the popup needs an item; the zone's first gateway (a device opens no socket of its own)
    this.popupItem = e.gateways[0];
  }

  close(): void {
    this.popupItem = null;
    this.zonePopup = null;
  }

  /** The zone popup's content from the live gateways feed: one row per asset across the zone's gateways. */
  get zonePopupInfo(): ZonePopupInfo | null {
    const zp = this.zonePopup;
    if (!zp) return null;
    // same object until the zone or the feed changes (a new object on every check would trip change detection)
    if (this.zoneInfoCache?.zp === zp && this.zoneInfoCache.feed === this.gateways) return this.zoneInfoCache.info;
    const info = this.buildZoneInfo(zp);
    this.zoneInfoCache = { zp, feed: this.gateways, info };
    return info;
  }

  private zoneInfoCache: { zp: { zone: MapZone; gateways: PlotItem[] }; feed: Record<string, GatewayAssets>; info: ZonePopupInfo } | null = null;

  private buildZoneInfo(zp: { zone: MapZone; gateways: PlotItem[] }): ZonePopupInfo {
    const feeds = zp.gateways.map((g) => this.gatewayOf(g)).filter((g): g is GatewayAssets => !!g);
    const assets = new Map<string, GatewayAsset>();
    for (const a of feeds.flatMap((f) => f.assets)) {
      const key = a.assetId.toLowerCase();
      const seen = assets.get(key);
      assets.set(key, seen
        ? { ...seen, tagIds: [...new Set([...seen.tagIds, ...a.tagIds])], lastSeenAt: [seen.lastSeenAt, a.lastSeenAt].filter(Boolean).sort().at(-1) ?? null }
        : a);
    }
    return {
      name: zp.zone.name,
      code: zp.zone.code,
      gateways: zp.gateways.map((g) => g.name),
      assets: [...assets.values()].sort((a, b) => (a.assetName ?? a.assetId).localeCompare(b.assetName ?? b.assetId)),
      unlinkedTags: new Set(feeds.flatMap((f) => f.unlinkedTagIds.map((t) => t.toLowerCase()))).size
    };
  }
}
