/**
 * Types, display config (colours, icons) and helpers for the Asset Overview dashboard.
 * All figures come from the APIs (services/dashboard/dashboard.ts); nothing here is data.
 */

export type PortfolioStatus = 'Active' | 'Idle' | 'In Transit' | 'Under Maintenance' | 'Retired' | 'Disposed';
export type HealthBand = 'Healthy' | 'Warning' | 'Critical' | 'Unknown';
export type Criticality = 'Critical' | 'High' | 'Medium' | 'Low';
export type AgeBand = 'New' | 'Mature' | 'Aging' | 'End of Life';

/** Icon and colour per category name (display only; categories and counts come from the API). */
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
/** Colour of each age band; the bands, labels and counts come from GET api/asset-dashboard/aging. */
export const AGE_COLORS: Record<string, string> = { New: '#1d8cf8', Mature: '#22b55e', Aging: '#f5b324', 'End of Life': '#ef4b5f' };

const pad = (n: number) => String(n).padStart(2, '0');
export const isoDate = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;

export function healthBand(h: number | null): HealthBand {
  return h === null ? 'Unknown' : h >= 70 ? 'Healthy' : h >= 40 ? 'Warning' : 'Critical';
}

