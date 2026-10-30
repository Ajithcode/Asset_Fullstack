/**
 * DEMO MODE: the connected-asset fleet behind the IoT & Tracking Overview.
 * Generated from a fixed seed so every visit shows the same devices, alerts and history.
 */

export type DeviceStatus = 'Online' | 'Idle' | 'Offline' | 'Unknown';
export type HealthBand = 'Healthy' | 'Warning' | 'Critical' | 'Unknown';
export type Severity = 'Critical' | 'Warning' | 'Info';
export type AlertStatus = 'Open' | 'Acknowledged' | 'In Progress' | 'Resolved';
export type EventSeries = 'telemetry' | 'location' | 'geofence' | 'alerts';

export interface Site {
  name: string;
  /** Zone outline on the map, [lat, lng] corners. */
  zone: [number, number][];
  color: string;
  image: string;
}

export interface Device {
  id: string;
  assetId: string;
  name: string;
  type: string;
  manufacturer: string;
  model: string;
  serial: string;
  site: string;
  status: DeviceStatus;
  /** 0-100, or null when the device has not reported. */
  health: number | null;
  /** 0-100 share of the period the asset was running. */
  utilization: number;
  firmwareLatest: boolean;
  firmware: string;
  /** A sensor is outside its normal band. */
  abnormal: boolean;
  lat: number;
  lng: number;
  commissioned: string;
  /** yyyy-MM-ddTHH:mm */
  lastUpdate: string;
  battery: number;
  signal: number;
  temperature: number;
  vibration: number;
  lastService: string;
  nextService: string;
}

export interface IotAlert {
  id: string;
  /** yyyy-MM-ddTHH:mm */
  at: string;
  deviceId: string;
  event: string;
  severity: Severity;
  status: AlertStatus;
}

export interface EventDay {
  date: string;
  telemetry: number;
  location: number;
  geofence: number;
  alerts: number;
}

export const ASSET_TYPES = ['Generators', 'CNC Machines', 'Forklifts', 'HVAC Units', 'Compressors'] as const;
export const STATUSES: DeviceStatus[] = ['Online', 'Idle', 'Offline', 'Unknown'];
export const STATUS_COLORS: Record<DeviceStatus, string> = { Online: '#16a34a', Idle: '#2563eb', Offline: '#dc2626', Unknown: '#cbd5e1' };
export const HEALTH_COLORS: Record<HealthBand, string> = { Healthy: '#16a34a', Warning: '#f5b324', Critical: '#dc2626', Unknown: '#cbd5e1' };
export const SERIES: { key: EventSeries; label: string; color: string }[] = [
  { key: 'telemetry', label: 'Telemetry Data', color: '#2563eb' },
  { key: 'location', label: 'Location Updates', color: '#16a34a' },
  { key: 'geofence', label: 'Geofence Events', color: '#f5b324' },
  { key: 'alerts', label: 'Alerts', color: '#dc2626' },
];
/** Firmware every device should run; others count as pending updates. */
export const LATEST_FIRMWARE = 'v4.2.1';

// Campus near Jebel Ali, Dubai: one zone per site
export const SITES: Site[] = [
  { name: 'Factory A', color: '#2563eb', image: 'factory', zone: [[25.0108, 55.1238], [25.0112, 55.1262], [25.0091, 55.1268], [25.0086, 55.1243]] },
  { name: 'Central Warehouse', color: '#7c3aed', image: 'warehouse', zone: [[25.0121, 55.1279], [25.0125, 55.1303], [25.0099, 55.1309], [25.0094, 55.1284]] },
  { name: 'North Yard', color: '#16a34a', image: 'yard', zone: [[25.0138, 55.1222], [25.0142, 55.1243], [25.0124, 55.1247], [25.0119, 55.1227]] },
  { name: 'Warehouse B', color: '#0891b2', image: 'warehouse', zone: [[25.0133, 55.1253], [25.0135, 55.1270], [25.0122, 55.1273], [25.0120, 55.1256]] },
  { name: 'Dubai Factory', color: '#ea580c', image: 'factory', zone: [[25.0083, 55.1281], [25.0086, 55.1298], [25.0071, 55.1301], [25.0068, 55.1284]] },
];

const SITE_SHARE = [432, 298, 186, 172, 160];
const TYPE_INFO: Record<(typeof ASSET_TYPES)[number], { prefix: string; names: string[]; makers: [string, string][]; util: number }> = {
  Generators: { prefix: 'GEN', names: ['Diesel Generator 500kVA', 'Diesel Generator 250kVA', 'Gas Generator 750kVA'], makers: [['Caterpillar', 'C15'], ['Cummins', 'QSK19'], ['Perkins', '2506C']], util: 76 },
  'CNC Machines': { prefix: 'CNC', names: ['CNC Machine - HMC 500', 'CNC Lathe - LT 300', 'CNC Mill - VMC 850'], makers: [['DMG Mori', 'HMC 500'], ['Haas', 'ST-30'], ['Mazak', 'VCN-530C']], util: 62 },
  Forklifts: { prefix: 'FLT', names: ['Forklift - Toyota 8T', 'Forklift - Electric 3T', 'Reach Truck 2T'], makers: [['Toyota', '8FBE20'], ['Linde', 'E30'], ['Jungheinrich', 'ETV 216']], util: 71 },
  'HVAC Units': { prefix: 'AHU', names: ['HVAC Unit AHU-3', 'Rooftop Package Unit', 'Chiller 300TR'], makers: [['Trane', 'CLCP'], ['Carrier', '50TC'], ['Daikin', 'EWAD']], util: 54 },
  Compressors: { prefix: 'CMP', names: ['Air Compressor - CP200', 'Screw Compressor 75kW', 'Air Dryer AD-90'], makers: [['Atlas Copco', 'GA 37'], ['Ingersoll Rand', 'R75'], ['Kaeser', 'ASD 50']], util: 68 },
};
const EVENTS: { event: string; severity: Severity }[] = [
  { event: 'Vibration High', severity: 'Critical' }, { event: 'Temperature High', severity: 'Warning' }, { event: 'Geofence Breach', severity: 'Critical' },
  { event: 'Sensor Offline', severity: 'Warning' }, { event: 'Pressure Low', severity: 'Warning' }, { event: 'Battery Low', severity: 'Info' },
  { event: 'Unauthorized Movement', severity: 'Critical' }, { event: 'Signal Lost', severity: 'Warning' },
];

function rng(seed: number): () => number {
  let s = seed >>> 0;
  return () => {
    s = (s * 1664525 + 1013904223) >>> 0;
    return s / 4294967296;
  };
}

const pad = (n: number) => String(n).padStart(2, '0');
const iso = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
const stamp = (d: Date) => `${iso(d)}T${pad(d.getHours())}:${pad(d.getMinutes())}`;

function insideZone(lat: number, lng: number, zone: [number, number][]): boolean {
  let inside = false;
  for (let i = 0, j = zone.length - 1; i < zone.length; j = i++) {
    const [yi, xi] = zone[i];
    const [yj, xj] = zone[j];
    if (yi > lat !== yj > lat && lng < ((xj - xi) * (lat - yi)) / (yj - yi) + xi) inside = !inside;
  }
  return inside;
}

function pointIn(zone: [number, number][], r: () => number): [number, number] {
  const lats = zone.map((p) => p[0]);
  const lngs = zone.map((p) => p[1]);
  for (;;) {
    const lat = Math.min(...lats) + r() * (Math.max(...lats) - Math.min(...lats));
    const lng = Math.min(...lngs) + r() * (Math.max(...lngs) - Math.min(...lngs));
    if (insideZone(lat, lng, zone)) return [lat, lng];
  }
}

function buildDevices(now: Date): Device[] {
  const r = rng(1248);
  const out: Device[] = [];
  const counters: Record<string, number> = {};
  SITES.forEach((site, si) => {
    for (let k = 0; k < SITE_SHARE[si]; k++) {
      const type = ASSET_TYPES[Math.floor(r() * ASSET_TYPES.length)];
      const info = TYPE_INFO[type];
      const variant = Math.floor(r() * info.names.length);
      counters[info.prefix] = (counters[info.prefix] ?? 0) + 1;
      const n = counters[info.prefix];
      const roll = r();
      const status: DeviceStatus = roll < 0.871 ? 'Online' : roll < 0.95 ? 'Idle' : roll < 0.983 ? 'Offline' : 'Unknown';
      const hRoll = r();
      const health = status === 'Unknown' ? null : hRoll < 0.72 ? 70 + Math.round(r() * 30) : hRoll < 0.93 ? 40 + Math.round(r() * 29) : 10 + Math.round(r() * 29);
      const [lat, lng] = pointIn(site.zone, r);
      const updated = new Date(now.getTime() - (status === 'Online' ? r() * 10 : status === 'Idle' ? 30 + r() * 240 : 600 + r() * 4000) * 60_000);
      const commissioned = new Date(2019 + Math.floor(r() * 6), Math.floor(r() * 12), 1 + Math.floor(r() * 27));
      const lastService = new Date(now.getTime() - (5 + r() * 160) * 86_400_000);
      const nextService = new Date(lastService.getTime() + 180 * 86_400_000);
      const latest = r() < 0.82;
      out.push({
        id: `DEV-${String(out.length + 1).padStart(5, '0')}`,
        assetId: `${info.prefix}-${String(100 + n * 3).padStart(5, '0')}`,
        name: info.names[variant], type, manufacturer: info.makers[variant][0], model: info.makers[variant][1],
        serial: `${info.makers[variant][0].slice(0, 3).toUpperCase()}${info.makers[variant][1].replace(/\W/g, '').slice(0, 3).toUpperCase()}-${String(10000 + Math.floor(r() * 89999))}`,
        site: site.name, status, health,
        utilization: status === 'Online' ? Math.max(5, Math.min(100, Math.round(info.util + (r() - 0.5) * 40))) : status === 'Idle' ? Math.round(r() * 30) : 0,
        firmwareLatest: latest, firmware: latest ? LATEST_FIRMWARE : ['v4.1.0', 'v4.0.3', 'v3.9.8'][Math.floor(r() * 3)],
        abnormal: health !== null && health < 45 && r() < 0.4,
        lat, lng, commissioned: iso(commissioned), lastUpdate: stamp(updated),
        battery: Math.round(20 + r() * 80), signal: status === 'Offline' ? 0 : Math.round(-95 + r() * 45),
        temperature: Math.round((35 + r() * 50) * 10) / 10, vibration: Math.round(r() * 120) / 10,
        lastService: iso(lastService), nextService: iso(nextService),
      });
    }
  });
  return out;
}

function buildAlerts(devices: Device[], now: Date): IotAlert[] {
  const r = rng(42);
  const out: IotAlert[] = [];
  // 90 days of alerts; the overview filters them by the selected period
  for (let i = 0; i < 128; i++) {
    const d = devices[Math.floor(r() * devices.length)];
    const e = EVENTS[Math.floor(r() * EVENTS.length)];
    const ageMin = i < 42 ? r() * 30 * 1440 : 30 * 1440 + r() * 60 * 1440;
    const at = new Date(now.getTime() - ageMin * 60_000);
    const old = ageMin > 7 * 1440;
    const sRoll = r();
    const status: AlertStatus = old ? (sRoll < 0.8 ? 'Resolved' : 'Acknowledged') : sRoll < 0.5 ? 'Open' : sRoll < 0.75 ? 'Acknowledged' : 'In Progress';
    out.push({ id: `ALT-${String(9000 + i)}`, at: stamp(at), deviceId: d.id, event: e.event, severity: e.severity, status });
  }
  return out.sort((a, b) => b.at.localeCompare(a.at));
}

function buildEvents(now: Date): EventDay[] {
  const r = rng(7);
  return Array.from({ length: 90 }, (_, i) => {
    const d = new Date(now.getFullYear(), now.getMonth(), now.getDate() - 89 + i);
    const wave = Math.sin(i / 3) * 8;
    return {
      date: iso(d),
      telemetry: Math.round(90 + wave + r() * 25),
      location: Math.round(75 + wave * 0.6 + r() * 18),
      geofence: Math.round(45 + r() * 15),
      alerts: Math.round(110 + r() * 40 + (r() < 0.08 ? 50 : 0)),
    };
  });
}

const NOW = new Date();
export const DEVICES: Device[] = buildDevices(NOW);
export const ALERTS: IotAlert[] = buildAlerts(DEVICES, NOW);
export const EVENT_HISTORY: EventDay[] = buildEvents(NOW);

/** Figures for the previous period, so the KPIs can show a change. */
export const PREVIOUS = { connected: 1149, coverage: 92.1, freshness: 97.3 };

export function healthBand(h: number | null): HealthBand {
  return h === null ? 'Unknown' : h >= 70 ? 'Healthy' : h >= 40 ? 'Warning' : 'Critical';
}
