/* Types and helpers for Device Health & Status. Devices come from /api/device-health, enriched with /api/iot-devices and /api/firmware-versions. */
import { DeviceHealthDto } from './device-health-api.service';

export type Conn = 'Online' | 'Warning' | 'Offline';
/** Page status: the API's Health, with Attention shown as Warning and an Offline connection as Offline. */
export type Status = 'Healthy' | 'Warning' | 'Critical' | 'Offline' | 'Maintenance';
export type Power = 'Battery' | 'External' | 'PoE';
export type AssetKind = 'forklift' | 'cold' | 'pump' | 'generator' | 'truck' | 'box';
export type Level = 'red' | 'amber' | 'green' | 'purple' | 'grey';
export type SignalQuality = 'Strong' | 'Good' | 'Fair' | 'Poor';

export interface Device {
  /** Device id (e.g. VS-30018). */
  id: string;
  model: string;
  /** Device type from the IoT device registry; '—' when the device is not registered. */
  type: string;
  asset: { name: string; code: string; kind: AssetKind } | null;
  site: string;
  /** Gateway id, or "Cellular" for devices that report over the mobile network; '—' when unknown. */
  gateway: string;
  conn: Conn;
  /** RSSI in dBm; null when not reported. */
  rssi: number | null;
  snr: number | null;
  quality: SignalQuality | null;
  /** Battery %, null when powered externally / PoE. */
  battery: number | null;
  power: Power;
  /** Minutes since the last report; null when never reported. */
  lastSeenMin: number | null;
  status: Status;
  /** Installed firmware from the device registry; '—' when unknown. */
  firmware: string;
  /** Latest firmware of the model (firmware inventory); null when the model has no firmware record. */
  latestFirmware: string | null;
  /** Protocol from the device registry (e.g. LoRaWAN 1.0.3); '—' when unknown. */
  protocol: string;
  /** The API record. */
  api: DeviceHealthDto;
}

export interface DevEvent { min: number; title: string; detail: string; level: Level }

/** Technicians issues can be assigned to (Master Data GET /api/master-data/technicians, filled on load). */
export const ASSIGNEES: string[] = [];
export const PRIORITIES = ['Low', 'Medium', 'High', 'Critical'];

/** Quality for an RSSI reading (same bands as the API). */
export function signalQuality(rssi: number | null): SignalQuality | null {
  if (rssi === null) return null;
  return rssi >= -60 ? 'Strong' : rssi > -70 ? 'Good' : rssi >= -85 ? 'Fair' : 'Poor';
}

/** True when a connected device has not reported for over an hour. */
export function isStale(d: Device): boolean {
  return d.conn !== 'Offline' && d.lastSeenMin !== null && d.lastSeenMin > 60;
}

/** Health score 0–100 derived from connectivity, battery, signal and heartbeat freshness. */
export function healthScore(d: Device): number {
  let s = 100;
  if (d.conn === 'Offline') s -= 40;
  else if (d.conn === 'Warning') s -= 12;
  if (d.battery !== null) s -= d.battery < 20 ? 20 : d.battery < 30 ? 10 : 0;
  if (d.rssi !== null) s -= d.rssi < -90 ? 12 : d.rssi < -85 ? 6 : d.rssi < -75 ? 2 : 0;
  if (isStale(d)) s -= 6;
  return Math.max(0, s);
}

/** Events derived from the device's last report (newest first). The API keeps no event history. */
export function baseEvents(d: Device): DevEvent[] {
  const ev: DevEvent[] = [];
  const at = d.lastSeenMin;
  if (at !== null) {
    if (d.conn === 'Offline') ev.push({ min: at, title: 'Last heartbeat before going offline', detail: 'No heartbeat received since', level: 'red' });
    if (d.conn === 'Warning') ev.push({ min: at, title: 'Intermittent connectivity', detail: 'Connection reported as unstable', level: 'amber' });
    if (d.battery !== null && d.battery < 20) ev.push({ min: at, title: 'Low battery level', detail: `Battery level at ${d.battery}%`, level: 'amber' });
    else if (d.battery !== null && d.battery < 30) ev.push({ min: at, title: 'Battery running low', detail: `Battery level at ${d.battery}%`, level: 'amber' });
    if (d.rssi !== null && d.rssi < -90) ev.push({ min: at, title: 'Poor signal quality', detail: `RSSI ${d.rssi} dBm, below -90 dBm`, level: 'red' });
    else if (d.rssi !== null && d.rssi < -85) ev.push({ min: at, title: 'Weak signal', detail: `RSSI ${d.rssi} dBm, below -85 dBm`, level: 'amber' });
    if (d.conn !== 'Offline') ev.push({ min: at, title: 'Payload received', detail: d.gateway !== '—' ? `Telemetry via ${d.gateway}` : 'Telemetry received', level: 'green' });
  }
  return ev;
}

/** Hourly battery % and RSSI over the last 24 h: x = minutes ago of each hour. */
export interface Telemetry { mins: number[]; battery: number[]; rssi: number[] }

/**
 * Hourly averages of a device's readings (GET /api/telemetry-readings) over the 24 h before `now`, for the hours that
 * have a battery or RSSI reading. An hour missing one of the two takes the nearest earlier value (else the next one);
 * a series with no reading at all is drawn at the device's current value (or the axis floor).
 */
export function hourlyTelemetry(readings: { at: number; kind: 'battery' | 'rssi'; value: number }[], now: number, current: { battery: number | null; rssi: number | null }): Telemetry {
  const hours = new Map<number, { b: number[]; r: number[] }>();
  for (const x of readings) {
    const ago = (now - x.at) / 60000;
    if (ago < 0 || ago > 1440) continue;
    const h = Math.min(23, Math.floor(ago / 60));
    const slot = hours.get(h) ?? { b: [], r: [] };
    (x.kind === 'battery' ? slot.b : slot.r).push(x.value);
    hours.set(h, slot);
  }
  const keys = [...hours.keys()].sort((a, b) => b - a); // oldest hour first
  const avg = (v: number[]) => (v.length ? Math.round((v.reduce((s, n) => s + n, 0) / v.length) * 10) / 10 : null);
  const fill = (vals: (number | null)[], fallback: number): number[] => {
    let last: number | null = vals.find((v) => v !== null) ?? null;
    return vals.map((v) => (last = v ?? last ?? fallback));
  };
  const b = keys.map((h) => avg(hours.get(h)!.b));
  const r = keys.map((h) => avg(hours.get(h)!.r));
  return {
    mins: keys.map((h) => h * 60 + 30),
    battery: fill(b, current.battery ?? 0).map((v) => Math.max(0, Math.min(100, v))),
    rssi: fill(r, current.rssi ?? -110).map((v) => Math.max(-110, Math.min(-30, v))),
  };
}
