/**
 * DEMO MODE: the report catalogue behind the Reports dashboard.
 * Generated from a fixed seed so every visit shows the same reports.
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

export const CURRENT_USER = 'Ahmed Usman';

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
export const SITES = ['Dubai Factory', 'Sharjah Yard', 'Abu Dhabi Warehouse', 'Doha Operations'];
export const OWNERS = ['Ahmed Usman', 'Priya Shah', 'Omar Khan', 'Sara Ali', 'Daniel Brooks'];
export const FILE_TYPES: FileType[] = ['PDF', 'XLS', 'CSV'];
export const SERIES: { key: keyof Omit<ActivityDay, 'date'>; label: string; color: string }[] = [
  { key: 'generated', label: 'Generated', color: '#1d8cf8' },
  { key: 'viewed', label: 'Viewed', color: '#8b5cf6' },
  { key: 'exported', label: 'Exported', color: '#16b67a' },
  { key: 'scheduled', label: 'Scheduled', color: '#f59e0b' },
];
export const CHANNELS: { channel: Channel; color: string; ok: number; failed: number }[] = [
  { channel: 'PDF', color: '#ef4b5f', ok: 1245, failed: 19 },
  { channel: 'Excel', color: '#16a34a', ok: 892, failed: 26 },
  { channel: 'CSV', color: '#2563eb', ok: 640, failed: 21 },
  { channel: 'API', color: '#7c3aed', ok: 312, failed: 3 },
  { channel: 'Email', color: '#f59e0b', ok: 428, failed: 19 },
];

const MODULE_COUNTS = [28, 22, 20, 16, 14, 12, 10, 6];
const NAMES: Record<string, string[]> = {
  Dashboard: ['Asset Health Summary', 'Executive KPI Digest', 'Operations Snapshot', 'Site Performance Overview'],
  Assets: ['Asset Register Export', 'Asset Utilization Review', 'Asset Lifecycle Aging', 'Critical Asset Register'],
  Maintenance: ['Maintenance Cost Analysis', 'PM Compliance Summary', 'Work Order Backlog', 'MTTR / MTBF Trend'],
  Inspection: ['Inspection Compliance', 'Failed Checklist Items', 'Inspector Workload'],
  Inventory: ['Inventory & Spare Parts', 'Stock Reorder Forecast', 'Parts Consumption'],
  'IoT & Tracking': ['IoT Utilization Analytics', 'Geofence Breach Log', 'Device Uptime Report'],
  Financial: ['Depreciation Schedule', 'Capex vs Opex', 'Asset Valuation'],
  Compliance: ['Compliance Exception Digest', 'Certificate Expiry Register'],
};

/** Deterministic pseudo-random numbers (mulberry32). */
function seeded(seed: number): () => number {
  let s = seed;
  return () => {
    s |= 0;
    s = (s + 0x6d2b79f5) | 0;
    let t = Math.imul(s ^ (s >>> 15), 1 | s);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

export function isoDate(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

const DAY = 86_400_000;
const TODAY = new Date();
const rand = seeded(360);
const pick = <T,>(list: T[]): T => list[Math.floor(rand() * list.length)];

function stamp(daysAgo: number): string {
  const d = new Date(TODAY.getTime() - daysAgo * DAY);
  return `${isoDate(d)}T${String(7 + Math.floor(rand() * 11)).padStart(2, '0')}:${String(Math.floor(rand() * 60)).padStart(2, '0')}`;
}

function buildLibrary(): LibraryReport[] {
  const out: LibraryReport[] = [];
  MODULES.forEach((m, mi) => {
    for (let i = 0; i < MODULE_COUNTS[mi]; i++) {
      const names = NAMES[m.name];
      const base = names[i % names.length];
      const n = out.length + 1;
      out.push({
        id: `RPT-${String(n).padStart(4, '0')}`,
        name: i < names.length ? base : `${base} - ${SITES[i % SITES.length].split(' ')[0]}`,
        module: m.name,
        site: SITES[(i + mi) % SITES.length],
        owner: OWNERS[(i + mi * 2) % OWNERS.length],
        type: i % 3 === 2 ? 'CSV' : (i + mi) % 2 ? 'XLS' : 'PDF',
        // the first report of each module is the most read one
        views: i === 0 ? [1248, 312, 892, 756, 631, 428, 220, 180][mi] : Math.round(20 + rand() * 300),
        created: isoDate(new Date(TODAY.getTime() - Math.floor(rand() * 420) * DAY)),
        updated: stamp(Math.floor(rand() * 85)),
        scheduled: rand() < 0.27,
        favorite: rand() < 0.15,
        sharedWithMe: rand() < 0.3,
        pendingApproval: rand() < 0.095,
        stale: rand() < 0.05,
      });
    }
  });
  // headline reports are generated most recently
  out.filter((r) => r.views > 300).forEach((r, i) => (r.updated = stamp(i)));
  return out;
}

function buildActivity(): ActivityDay[] {
  return Array.from({ length: 90 }, (_, i) => {
    const date = isoDate(new Date(TODAY.getTime() - (89 - i) * DAY));
    const weekend = [5, 6].includes(new Date(date).getDay());
    const k = weekend ? 0.55 : 1;
    const generated = Math.round((30 + rand() * 32) * k);
    return {
      date,
      generated,
      viewed: Math.round(generated * (0.9 + rand() * 0.6)),
      exported: Math.round(generated * (0.35 + rand() * 0.3)),
      scheduled: Math.round(10 + rand() * 9 * k),
    };
  });
}

function nextRun(days: number): string {
  return isoDate(new Date(TODAY.getTime() + days * DAY));
}

/** Headline figures shown in the preview card, per module. */
export const PREVIEW_STATS: Record<string, { label: string; value: string; tone: 'blue' | 'green' | 'red' }[]> = {
  Dashboard: [{ label: 'Total Assets', value: '3,842', tone: 'blue' }, { label: 'Healthy', value: '69.9%', tone: 'green' }, { label: 'Critical', value: '74', tone: 'red' }],
  Assets: [{ label: 'Registered', value: '3,842', tone: 'blue' }, { label: 'Utilized', value: '78.4%', tone: 'green' }, { label: 'No Owner', value: '112', tone: 'red' }],
  Maintenance: [{ label: 'Work Orders', value: '1,206', tone: 'blue' }, { label: 'On Time', value: '91.2%', tone: 'green' }, { label: 'Overdue', value: '38', tone: 'red' }],
  Inspection: [{ label: 'Inspections', value: '864', tone: 'blue' }, { label: 'Passed', value: '94.1%', tone: 'green' }, { label: 'Failed', value: '51', tone: 'red' }],
  Inventory: [{ label: 'SKUs', value: '2,415', tone: 'blue' }, { label: 'In Stock', value: '88.6%', tone: 'green' }, { label: 'Below Min', value: '64', tone: 'red' }],
  'IoT & Tracking': [{ label: 'Devices', value: '1,980', tone: 'blue' }, { label: 'Online', value: '92.3%', tone: 'green' }, { label: 'Offline', value: '153', tone: 'red' }],
  Financial: [{ label: 'Book Value', value: '$48.2M', tone: 'blue' }, { label: 'Budget Used', value: '71.5%', tone: 'green' }, { label: 'Over Budget', value: '9', tone: 'red' }],
  Compliance: [{ label: 'Certificates', value: '642', tone: 'blue' }, { label: 'Valid', value: '96.3%', tone: 'green' }, { label: 'Expiring', value: '24', tone: 'red' }],
};

export const LIBRARY: LibraryReport[] = buildLibrary();
export const ACTIVITY: ActivityDay[] = buildActivity();
export const SCHEDULES: ScheduledReport[] = [
  { name: 'Weekly Asset Health Summary', module: 'Dashboard', cadence: 'Every Monday at 08:00 AM', recipients: 5, nextRun: nextRun(3), active: true, icon: 'file', tone: 'blue' },
  { name: 'Daily Critical Asset Alerts', module: 'Assets', cadence: 'Daily at 07:00 AM', recipients: 8, nextRun: nextRun(1), active: true, icon: 'alert', tone: 'red' },
  { name: 'Monthly Maintenance Cost', module: 'Maintenance', cadence: '1st of every month at 09:00 AM', recipients: 6, nextRun: nextRun(2), active: true, icon: 'chart', tone: 'blue' },
  { name: 'Compliance Exception Digest', module: 'Compliance', cadence: 'Every Friday at 04:00 PM', recipients: 4, nextRun: nextRun(4), active: false, icon: 'shield', tone: 'violet' },
  { name: 'IoT Device Uptime', module: 'IoT & Tracking', cadence: 'Every Sunday at 06:00 AM', recipients: 3, nextRun: nextRun(5), active: true, icon: 'chart', tone: 'blue' },
  { name: 'Inventory Reorder Forecast', module: 'Inventory', cadence: 'Daily at 06:30 AM', recipients: 2, nextRun: nextRun(1), active: true, icon: 'file', tone: 'blue' },
];
export const RECENT: ActivityEvent[] = [
  { owner: 'Ahmed Usman', action: 'Generated', report: 'Asset Health Summary', at: stamp(0) },
  { owner: 'Priya Shah', action: 'Exported to Excel', report: 'Maintenance Cost Analysis', at: stamp(0) },
  { owner: 'Omar Khan', action: 'Scheduled', report: 'Compliance Exception Digest', at: stamp(0) },
  { owner: 'Sara Ali', action: 'Shared', report: 'Inspection Compliance', at: stamp(1) },
  { owner: 'Daniel Brooks', action: 'Generated', report: 'IoT Utilization Analytics', at: stamp(1) },
  { owner: 'Priya Shah', action: 'Approved', report: 'Depreciation Schedule', at: stamp(2) },
];
