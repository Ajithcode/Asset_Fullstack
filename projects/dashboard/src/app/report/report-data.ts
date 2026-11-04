/**
 * Types and config for the Reports dashboard. Records stay empty until the reports API is wired.
 */

export type FileType = 'PDF' | 'XLS' | 'CSV';
export type Channel = 'PDF' | 'Excel' | 'CSV' | 'API' | 'Email';

export interface LibraryReport {
  id: string;
  name: string;
  module: string;
  site: string;
  owner: string;
  type: FileType;
  views: number;
  /** ISO date the report was set up. */
  created: string;
  /** ISO date-time of the last generation. */
  updated: string;
  /** Runs on a schedule. */
  scheduled: boolean;
  favorite: boolean;
  sharedWithMe: boolean;
  pendingApproval: boolean;
  /** Data older than the expected refresh SLA. */
  stale: boolean;
}

export interface ScheduledReport {
  name: string;
  module: string;
  cadence: string;
  recipients: number;
  nextRun: string;
  active: boolean;
  icon: 'file' | 'alert' | 'chart' | 'shield';
  tone: 'blue' | 'red' | 'violet';
}

export interface ActivityDay {
  date: string;
  generated: number;
  viewed: number;
  exported: number;
  scheduled: number;
}

export interface ActivityEvent {
  owner: string;
  action: string;
  report: string;
  at: string;
}

/** Signed-in user's display name; filled from the auth/user API once wired. */
export const CURRENT_USER = '';

export const MODULES: { name: string; color: string }[] = [
  { name: 'Dashboard', color: '#2f6fe4' },
  { name: 'Assets', color: '#7c5cff' },
  { name: 'Maintenance', color: '#16a34a' },
  { name: 'Inspection', color: '#f5b324' },
  { name: 'Inventory', color: '#ef4b5f' },
  { name: 'IoT & Tracking', color: '#14b8c4' },
  { name: 'Financial', color: '#a855f7' },
  { name: 'Compliance', color: '#a3adbd' },
];
/** Filter options; filled from the API once wired. */
export const SITES: string[] = [];
export const OWNERS: string[] = [];
export const FILE_TYPES: FileType[] = ['PDF', 'XLS', 'CSV'];
export const SERIES: { key: keyof Omit<ActivityDay, 'date'>; label: string; color: string }[] = [
  { key: 'generated', label: 'Generated', color: '#1d8cf8' },
  { key: 'viewed', label: 'Viewed', color: '#8b5cf6' },
  { key: 'exported', label: 'Exported', color: '#16b67a' },
  { key: 'scheduled', label: 'Scheduled', color: '#f59e0b' },
];
/** Delivery channels with their colours; ok/failed counts stay 0 until the API is wired. */
export const CHANNELS: { channel: Channel; color: string; ok: number; failed: number }[] = [
  { channel: 'PDF', color: '#ef4b5f', ok: 0, failed: 0 },
  { channel: 'Excel', color: '#16a34a', ok: 0, failed: 0 },
  { channel: 'CSV', color: '#2563eb', ok: 0, failed: 0 },
  { channel: 'API', color: '#7c3aed', ok: 0, failed: 0 },
  { channel: 'Email', color: '#f59e0b', ok: 0, failed: 0 },
];

export function isoDate(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

/** Headline figures shown in the preview card, per module (values filled from the API once wired). */
export const PREVIEW_STATS: Record<string, { label: string; value: string; tone: 'blue' | 'green' | 'red' }[]> = {};

/** Report catalogue, activity history, schedules and recent events; empty until the reports API is wired. */
export const LIBRARY: LibraryReport[] = [];
export const ACTIVITY: ActivityDay[] = [];
export const SCHEDULES: ScheduledReport[] = [];
export const RECENT: ActivityEvent[] = [];
