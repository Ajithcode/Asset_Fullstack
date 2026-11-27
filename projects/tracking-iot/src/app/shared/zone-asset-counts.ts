import { Observable, catchError, map, of } from 'rxjs';
import { RtlApi } from '../real-time-location/rtl-api';

/** How often the zone counts are re-read (Real-Time Location refreshes every 10 s). */
export const ZONE_COUNTS_MS = 10000;

/**
 * Asset labels per floor-plan zone id from Real-Time Location's overview (worker-resolved TagLocationState: one zone per tag, stale =
 * no location). Same rule as the RTL map: an asset counts in the zone it is resolved to. Null when the request failed.
 */
export function zoneAssetLabels(api: RtlApi, floorPlanId: string): Observable<Record<string, string[]> | null> {
  return api.overview('', floorPlanId).pipe(
    map((o) => {
      const byZone: Record<string, string[]> = {};
      for (const a of o.assets) {
        const zoneId = a.location?.zoneId;
        if (!zoneId) continue;
        (byZone[zoneId] ??= []).push(a.assetTag && a.assetTag !== a.name ? `${a.name} (${a.assetTag})` : a.name);
      }
      for (const k of Object.keys(byZone)) byZone[k].sort((x, y) => x.localeCompare(y));
      return byZone;
    }),
    catchError(() => of(null)),
  );
}
