import { Observable, catchError, map, of } from 'rxjs';
import type { PlanAsset } from '../geofence-alerts/floor-plan-map';
import { RtlApi, RtlAsset } from '../real-time-location/rtl-api';

/** How often Telemetry and Geofence & Alerts re-read the live asset positions (Real-Time Location refreshes every 10 s). */
export const LIVE_MARKERS_MS = 10000;

/**
 * Asset markers for a floor plan map from the SAME source Real-Time Location uses: GET real-time-location/overview, which reads
 * the worker-resolved TagLocationState (one zone per tag, stale = no location) and the current MDM zone name. Every map that shows
 * an asset count per zone feeds `[assetMarkers]` from here, so the counts, the clustering and the zone names are the same on
 * Real-Time Location, Tracking & Telemetry and Geofence & Alerts.
 */
export function toPlanAssets(assets: RtlAsset[]): PlanAsset[] {
  const list: PlanAsset[] = [];
  for (const a of assets) {
    const zoneId = a.location?.zoneId;
    if (!zoneId) continue;
    const state = a.freshness === 'Offline' || a.freshness === 'Unknown' ? 'Unknown' : 'Stationary';
    list.push({ id: a.assetId, name: a.name, code: a.assetTag || a.assetId, zoneId, zoneName: a.location?.zoneName ?? '', state, tag: a.tags[0] ?? a.tracker ?? '', lastSeenAt: a.lastValidLocationAt });
  }
  return list;
}

export function liveZoneMarkers(api: RtlApi, floorPlanId: string): Observable<PlanAsset[]> {
  return api.overview('', floorPlanId).pipe(map((o) => toPlanAssets(o.assets)), catchError(() => of(null as unknown as PlanAsset[]))) as Observable<PlanAsset[]>;
}

/** True when the markers differ from the last ones, so an unchanged poll doesn't re-cluster the map. */
export function markersKey(list: PlanAsset[]): string {
  return list.map((x) => `${x.id}|${x.zoneId}|${x.state}|${x.lastSeenAt ?? ''}`).join(',');
}
