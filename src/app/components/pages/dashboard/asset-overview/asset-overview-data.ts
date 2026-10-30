/**
 * DEMO MODE: the asset portfolio behind the Asset Overview dashboard.
 * Generated from a fixed seed so every visit shows the same assets.
 */

export type PortfolioStatus = 'Active' | 'Idle' | 'In Transit' | 'Under Maintenance' | 'Retired' | 'Disposed';
export type HealthBand = 'Healthy' | 'Warning' | 'Critical' | 'Unknown';
export type Criticality = 'Critical' | 'High' | 'Medium' | 'Low';
export type AgeBand = 'New' | 'Mature' | 'Aging' | 'End of Life';

export interface PortfolioAsset {
  id: string;
  name: string;
  category: string;
  type: string;
  site: string;
  status: PortfolioStatus;
  /** 0-100, or null when never assessed. */
  health: number | null;
  criticality: Criticality;
  /** Current book value (USD). */
  value: number;
  acquired: string;
  manufacturer: string;
  model: string;
  serial: string;
  owner: string;
  department: string;
  documents: number;
  lastService: string;
  nextService: string;
  connected: boolean;
  lastUpdate: string;
  /** Status and health 30 days earlier, for the period comparison. */
  prevStatus: PortfolioStatus;
  prevHealth: number | null;
}

export interface PortfolioSite {
  name: string;
  lat: number;
  lng: number;
  /** Which side of the pin the card sits on. */
  card: 'left' | 'right';
}

export const CATEGORIES: { name: string; color: string; icon: string }[] = [
  { name: 'Production Equipment', color: '#2f80ed', icon: 'factory' },
  { name: 'Vehicles & Fleet', color: '#2f6fe4', icon: 'truck' },
  { name: 'Facilities & Infrastructure', color: '#7c5cff', icon: 'building' },
  { name: 'IT & Network', color: '#14b8c4', icon: 'monitor' },
  { name: 'Tools & Instruments', color: '#f5b324', icon: 'wrench' },
  { name: 'Safety Equipment', color: '#ef4b5f', icon: 'hard-hat' },
];
export const STATUSES: PortfolioStatus[] = ['Active', 'Idle', 'In Transit', 'Under Maintenance', 'Retired', 'Disposed'];
export const STATUS_COLORS: Record<PortfolioStatus, string> = {
  Active: '#16a34a', Idle: '#2563eb', 'In Transit': '#7c3aed', 'Under Maintenance': '#f59e0b', Retired: '#64748b', Disposed: '#475569',
};
export const HEALTH_COLORS: Record<HealthBand, string> = { Healthy: '#16a34a', Warning: '#f5b324', Critical: '#ef4b5f', Unknown: '#a3adbd' };
export const AGE_BANDS: { band: AgeBand; label: string; color: string; min: number; max: number }[] = [
  { band: 'New', label: '0-2 years', color: '#1d8cf8', min: 0, max: 3 },
  { band: 'Mature', label: '3-5 years', color: '#22b55e', min: 3, max: 6 },
  { band: 'Aging', label: '6-10 years', color: '#f5b324', min: 6, max: 11 },
  { band: 'End of Life', label: '10+ years', color: '#ef4b5f', min: 11, max: 99 },
];
export const SITES: PortfolioSite[] = [
  { name: 'Dubai Factory', lat: 25.2048, lng: 55.2708, card: 'left' },
  { name: 'Sharjah Yard', lat: 25.3463, lng: 55.4209, card: 'right' },
  { name: 'Abu Dhabi Warehouse', lat: 24.4539, lng: 54.3773, card: 'left' },
  { name: 'Doha Operations', lat: 25.2854, lng: 51.531, card: 'right' },
];

const SITE_SHARE = [1542, 486, 1026, 788];
const CATEGORY_SHARE = [0.325, 0.219, 0.182, 0.111, 0.1, 0.063];
const CATALOG: Record<string, { prefix: string; name: string; type: string; maker: string; model: string; value: [number, number] }[]> = {
  'Production Equipment': [
    { prefix: 'CNC', name: 'CNC Machine - HMC 500', type: 'CNC Machine', maker: 'DMG Mori', model: 'HMC 500', value: [180_000, 1_400_000] },
    { prefix: 'CRN', name: 'Overhead Crane 50 Ton', type: 'Crane', maker: 'Konecranes', model: 'CXT 50', value: [250_000, 900_000] },
    { prefix: 'PRS', name: 'Hydraulic Press 400T', type: 'Press', maker: 'Schuler', model: 'HPX 400', value: [120_000, 650_000] },
  ],
  'Vehicles & Fleet': [
    { prefix: 'FLT', name: 'Forklift - Toyota 8T', type: 'Forklift', maker: 'Toyota', model: '8FBE20', value: [30_000, 140_000] },
    { prefix: 'TRK', name: 'Delivery Truck 7.5T', type: 'Truck', maker: 'Isuzu', model: 'NPR 75', value: [45_000, 120_000] },
    { prefix: 'VAN', name: 'Service Van', type: 'Van', maker: 'Toyota', model: 'Hiace', value: [25_000, 60_000] },
  ],
  'Facilities & Infrastructure': [
    { prefix: 'GEN', name: 'Diesel Generator 500kVA', type: 'Generator', maker: 'Caterpillar', model: 'C15', value: [90_000, 900_000] },
    { prefix: 'HVAC', name: 'HVAC Unit AHU-3', type: 'HVAC Unit', maker: 'Trane', model: 'CLCP', value: [40_000, 480_000] },
    { prefix: 'TRF', name: 'Transformer 1 MVA', type: 'Transformer', maker: 'ABB', model: 'RESIBLOC', value: [80_000, 520_000] },
  ],
  'IT & Network': [
    { prefix: 'SRV', name: 'Server Rack R740', type: 'Server', maker: 'Dell', model: 'PowerEdge R740', value: [12_000, 90_000] },
    { prefix: 'SW', name: 'Core Switch 48P', type: 'Network Switch', maker: 'Cisco', model: 'C9300-48P', value: [6_000, 30_000] },
    { prefix: 'UPS', name: 'UPS 60 kVA', type: 'UPS', maker: 'Eaton', model: '93PM', value: [15_000, 70_000] },
  ],
  'Tools & Instruments': [
    { prefix: 'TL', name: 'Laser Alignment Kit', type: 'Instrument', maker: 'Fluke', model: '830', value: [4_000, 25_000] },
    { prefix: 'TL', name: 'Thermal Imaging Camera', type: 'Instrument', maker: 'FLIR', model: 'T540', value: [8_000, 30_000] },
    { prefix: 'TL', name: 'Torque Wrench Set', type: 'Tool', maker: 'Norbar', model: 'TTi 300', value: [1_000, 6_000] },
  ],
  'Safety Equipment': [
    { prefix: 'FP', name: 'Fire Pump', type: 'Fire System', maker: 'Grundfos', model: 'NKF 125', value: [20_000, 120_000] },
    { prefix: 'FA', name: 'Fire Alarm Panel', type: 'Fire System', maker: 'Honeywell', model: 'NFS2-3030', value: [8_000, 40_000] },
    { prefix: 'GD', name: 'Gas Detector', type: 'Detector', maker: 'Dräger', model: 'Polytron 8000', value: [2_000, 12_000] },
  ],
};
const OWNERS = ['Ahmed Basim', 'Sara Ahmed', 'Mohan Kumar', 'Rashid Ali', 'Noura Saeed', 'Faisal Ahmed', 'Ibrahim Khan', 'Hamad Lateef', 'Ramesh Kumar', 'Priya Nair'];
const DEPARTMENTS = ['Operations', 'Facilities', 'Logistics', 'IT', 'Engineering', 'Safety'];

function rng(seed: number): () => number {
  let s = seed >>> 0;
  return () => {
    s = (s * 1664525 + 1013904223) >>> 0;
    return s / 4294967296;
  };
}

const pad = (n: number) => String(n).padStart(2, '0');
export const isoDate = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;

function pick(r: () => number, weights: number[]): number {
  let x = r();
  for (let i = 0; i < weights.length; i++) {
    if ((x -= weights[i]) < 0) return i;
  }
  return weights.length - 1;
}

function buildAssets(now: Date): PortfolioAsset[] {
  const r = rng(3842);
  const out: PortfolioAsset[] = [];
  const counters: Record<string, number> = {};
  SITES.forEach((site, si) => {
    for (let k = 0; k < SITE_SHARE[si]; k++) {
      const category = CATEGORIES[pick(r, CATEGORY_SHARE)].name;
      const item = CATALOG[category][Math.floor(r() * 3)];
      counters[item.prefix] = (counters[item.prefix] ?? 0) + 1;
      const status = STATUSES[pick(r, [0.766, 0.111, 0.056, 0.048, 0.015, 0.004])];
      const hb = pick(r, [0.699, 0.211, 0.056, 0.034]);
      const health = hb === 3 ? null : hb === 0 ? 70 + Math.round(r() * 30) : hb === 1 ? 40 + Math.round(r() * 29) : 8 + Math.round(r() * 31);
      const age = pick(r, [0.322, 0.282, 0.237, 0.159]);
      const years = [r() * 3, 3 + r() * 3, 6 + r() * 5, 11 + r() * 8][age];
      const acquired = new Date(now.getTime() - years * 365.25 * 86_400_000);
      const [lo, hi] = item.value;
      // value depreciates with age; small share of critical assets
      const value = Math.round(((lo + r() * (hi - lo)) * Math.max(0.25, 1 - years * 0.06)) / 1000) * 1000;
      const crit: Criticality = r() < 0.019 + (health !== null && health < 40 ? 0.15 : 0) ? 'Critical' : (['High', 'Medium', 'Low'] as Criticality[])[pick(r, [0.25, 0.5, 0.25])];
      const lastService = new Date(now.getTime() - (5 + r() * 170) * 86_400_000);
      const nextService = new Date(lastService.getTime() + 180 * 86_400_000);
      const prevStatus: PortfolioStatus = r() < 0.06 ? STATUSES[pick(r, [0.8, 0.1, 0.05, 0.04, 0.01, 0])] : status;
      const prevHealth = health === null ? null : Math.max(0, Math.min(100, health - Math.round((r() - 0.25) * 6)));
      const serialMaker = item.maker.replace(/\W/g, '').slice(0, 3).toUpperCase();
      out.push({
        id: `${item.prefix}-${String(counters[item.prefix] + 100).padStart(5, '0')}`,
        name: item.name, category, type: item.type, site: site.name, status, health, criticality: crit, value,
        acquired: isoDate(acquired), manufacturer: item.maker, model: item.model, serial: `${serialMaker}${item.model.replace(/\W/g, '').slice(0, 3).toUpperCase()}-${10000 + Math.floor(r() * 89999)}`,
        owner: r() < 0.0058 ? '' : OWNERS[Math.floor(r() * OWNERS.length)], department: DEPARTMENTS[Math.floor(r() * DEPARTMENTS.length)],
        documents: r() < 0.0024 ? 0 : 1 + Math.floor(r() * 8),
        lastService: isoDate(lastService), nextService: isoDate(nextService),
        connected: r() < 0.7, lastUpdate: `${isoDate(new Date(now.getTime() - r() * 3 * 86_400_000))}T${pad(Math.floor(r() * 24))}:${pad(Math.floor(r() * 60))}`,
        prevStatus, prevHealth,
      });
    }
  });
  return out;
}

export const PORTFOLIO: PortfolioAsset[] = buildAssets(new Date());

export function healthBand(h: number | null): HealthBand {
  return h === null ? 'Unknown' : h >= 70 ? 'Healthy' : h >= 40 ? 'Warning' : 'Critical';
}

export function ageYears(acquired: string, asOf: Date): number {
  return (asOf.getTime() - new Date(acquired + 'T00:00').getTime()) / (365.25 * 86_400_000);
}
