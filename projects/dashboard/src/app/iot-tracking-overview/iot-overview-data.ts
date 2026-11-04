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

/** Asset types for the filter; loaded from master data. */
export const ASSET_TYPES: string[] = [];
export const STATUSES: DeviceStatus[] = ['Online', 'Idle', 'Offline', 'Unknown'];
export const STATUS_COLORS: Record<DeviceStatus, string> = { Online: '#16a34a', Idle: '#2563eb', Offline: '#dc2626', Unknown: '#cbd5e1' };
export const HEALTH_COLORS: Record<HealthBand, string> = { Healthy: '#16a34a', Warning: '#f5b324', Critical: '#dc2626', Unknown: '#cbd5e1' };
export const SERIES: { key: EventSeries; label: string; color: string }[] = [
  { key: 'telemetry', label: 'Telemetry Data', color: '#2563eb' },
  { key: 'location', label: 'Location Updates', color: '#16a34a' },
  { key: 'geofence', label: 'Geofence Events', color: '#f5b324' },
  { key: 'alerts', label: 'Alerts', color: '#dc2626' },
];
/** Firmware every device should run; others count as pending updates. Empty until provided by the API. */
export const LATEST_FIRMWARE = '';

export const SITES: Site[] = [];
export const DEVICES: Device[] = [];
export const ALERTS: IotAlert[] = [];
export const EVENT_HISTORY: EventDay[] = [];

/** Figures for the previous period, so the KPIs can show a change; null until provided by the API. */
export const PREVIOUS: { connected: number; coverage: number; freshness: number } | null = null;

export function healthBand(h: number | null): HealthBand {
  return h === null ? 'Unknown' : h >= 70 ? 'Healthy' : h >= 40 ? 'Warning' : 'Critical';
}
