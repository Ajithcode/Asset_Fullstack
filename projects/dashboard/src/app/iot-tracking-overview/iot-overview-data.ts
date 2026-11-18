/**
 * Types, display config (colours, series) and helpers for the IoT & Tracking Overview.
 * All figures come from the APIs (services/dashboard/dashboard.ts); nothing here is data.
 */

export type DeviceStatus = 'Online' | 'Idle' | 'Offline' | 'Unknown';
export type HealthBand = 'Healthy' | 'Warning' | 'Critical' | 'Unknown';
export type Severity = 'Critical' | 'Warning' | 'Info';
export type AlertStatus = 'Open' | 'Acknowledged' | 'In Progress' | 'Resolved';
export type EventSeries = 'telemetry' | 'location' | 'geofence' | 'alerts';

/** A registered asset on the map, from GET api/asset-dashboard/sites. */
export interface MapAsset {
  /** Asset registration id. */
  id: string;
  tag: string;
  name: string;
  type: string;
  site: string;
  siteId: string;
  /** Network status of the asset's tag. */
  status: DeviceStatus;
  assetStatus: string | null;
  lat: number | null;
  lng: number | null;
  location: string | null;
  gateway: string | null;
  lastSeen: string | null;
}

export interface EventDay {
  date: string;
  telemetry: number;
  location: number;
  geofence: number;
  alerts: number;
}

export const STATUSES: DeviceStatus[] = ['Online', 'Idle', 'Offline', 'Unknown'];
export const STATUS_COLORS: Record<DeviceStatus, string> = { Online: '#16a34a', Idle: '#2563eb', Offline: '#dc2626', Unknown: '#cbd5e1' };
export const HEALTH_COLORS: Record<HealthBand, string> = { Healthy: '#16a34a', Warning: '#f5b324', Critical: '#dc2626', Unknown: '#cbd5e1' };
export const SERIES: { key: EventSeries; label: string; color: string }[] = [
  { key: 'telemetry', label: 'Telemetry Data', color: '#2563eb' },
  { key: 'location', label: 'Location Updates', color: '#16a34a' },
  { key: 'geofence', label: 'Geofence Events', color: '#f5b324' },
  { key: 'alerts', label: 'Alerts', color: '#dc2626' },
];
export function healthBand(h: number | null): HealthBand {
  return h === null ? 'Unknown' : h >= 70 ? 'Healthy' : h >= 40 ? 'Warning' : 'Critical';
}

/** One GET api/asset-dashboard/location-updates?days=k window: totals from `from` up to the API's fixed end (next midnight). */
export interface LocationWindow {
  from: string;
  totalCount: number;
  tags: { tagId: string; count: number }[];
}

/**
 * Daily location updates from cumulative windows. The API only returns totals for "the last k days", all ending at
 * the same moment, so day k-ago = window(k) - window(k-1). windows[k - 1] is the response for days=k (k = 1..n).
 * Returns the days oldest first, plus each day's updates per tag.
 */
export function dailyLocationUpdates(windows: LocationWindow[]): { days: EventDay[]; tagsByDay: Map<string, { tagId: string; count: number }[]> } {
  const days: EventDay[] = [];
  const tagsByDay = new Map<string, { tagId: string; count: number }[]>();
  windows.forEach((w, i) => {
    const prev = windows[i - 1];
    const date = w.from.slice(0, 10);
    const before = new Map((prev?.tags ?? []).map((t) => [t.tagId, t.count]));
    const tags = w.tags
      .map((t) => ({ tagId: t.tagId, count: Math.max(0, t.count - (before.get(t.tagId) ?? 0)) }))
      .filter((t) => t.count > 0)
      .sort((a, b) => b.count - a.count);
    days.push({ date, telemetry: 0, location: Math.max(0, w.totalCount - (prev?.totalCount ?? 0)), geofence: 0, alerts: 0 });
    tagsByDay.set(date, tags);
  });
  return { days: days.reverse(), tagsByDay };
}
