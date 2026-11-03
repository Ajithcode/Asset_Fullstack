/** Service requests raised against one asset: types, SLA policy and the figures shown in the header. */

export type SrStatus = 'Open' | 'In Progress' | 'Resolved' | 'Closed';
export type SrPriority = 'High' | 'Medium' | 'Low';
export type SrCategory = 'Mechanical' | 'Electrical' | 'General' | 'Others';

export interface ServiceRequest {
  id: string;
  /** yyyy-MM-dd of createdAt (used by the activity feed). */
  date: string;
  title: string;
  description: string;
  category: SrCategory;
  priority: SrPriority;
  status: SrStatus;
  /** yyyy-MM-ddTHH:mm */
  createdAt: string;
  requestedBy: string;
  requesterDept: string;
  assignedTo: string;
  /** First response (acknowledged / assigned), or ''. */
  respondedAt: string;
  /** Resolved, or ''. */
  resolvedAt: string;
  targetDate: string;
  workOrder: string;
  /** 1-5 from the requester once resolved, 0 = not rated. */
  rating: number;
  updates: { at: string; by: string; text: string }[];
  /** by / at / description are set on files added after the request was raised; url on files uploaded this session. */
  attachments: { name: string; size: string; by?: string; at?: string; description?: string; url?: string }[];
  /** Set by the New Service Request page. */
  location?: string;
  downtimeHours?: number;
  /** yyyy-MM-ddTHH:mm */
  plannedStart?: string;
  plannedEnd?: string;
  relatedRequest?: string;
  tags?: string[];
  reference?: string;
}

export const SR_STATUSES: SrStatus[] = ['Open', 'In Progress', 'Resolved', 'Closed'];
export const SR_PRIORITIES: SrPriority[] = ['High', 'Medium', 'Low'];
export const SR_CATEGORIES: SrCategory[] = ['Mechanical', 'Electrical', 'General', 'Others'];
export const SR_STATUS_COLORS: Record<SrStatus, string> = { Resolved: '#16a34a', Open: '#2563eb', 'In Progress': '#7c3aed', Closed: '#9ca3af' };
export const SR_CATEGORY_COLORS: Record<SrCategory, string> = { Mechanical: '#7c5cff', Electrical: '#2563eb', General: '#16a34a', Others: '#eaa200' };

/** Response target in hours and resolution target in days, by priority. */
export const SR_SLA: Record<SrPriority, { responseHours: number; resolutionDays: number }> = {
  High: { responseHours: 4, resolutionDays: 2 },
  Medium: { responseHours: 8, resolutionDays: 3 },
  Low: { responseHours: 24, resolutionDays: 5 },
};

const HOUR = 3_600_000;

export function isOpen(r: ServiceRequest): boolean {
  return r.status === 'Open' || r.status === 'In Progress';
}

function ms(v: string): number {
  return new Date(v).getTime();
}

export function responseHours(r: ServiceRequest): number | null {
  return r.respondedAt ? (ms(r.respondedAt) - ms(r.createdAt)) / HOUR : null;
}

export function resolutionDays(r: ServiceRequest): number | null {
  return r.resolvedAt ? (ms(r.resolvedAt) - ms(r.createdAt)) / (24 * HOUR) : null;
}

export function fmtSpan(hours: number): string {
  const sign = hours < 0 ? '-' : '';
  const m = Math.round(Math.abs(hours) * 60);
  if (m < 60) return `${sign}${m}m`;
  if (m < 48 * 60) return `${sign}${Math.floor(m / 60)}h${m % 60 ? ` ${m % 60}m` : ''}`;
  const d = Math.round(m / 1440 * 10) / 10;
  return `${sign}${d} days`;
}

/** SLA state of a request against its priority's targets, at `now`. */
export function slaOf(r: ServiceRequest, now = Date.now()): { response: { label: string; ok: boolean | null }; resolution: { label: string; ok: boolean | null } } {
  const sla = SR_SLA[r.priority];
  const created = ms(r.createdAt);
  const resp = responseHours(r);
  const respDue = created + sla.responseHours * HOUR;
  const response = resp !== null
    ? { label: `${fmtSpan(resp)} (${resp <= sla.responseHours ? 'On Track' : 'Breached'})`, ok: resp <= sla.responseHours }
    : now <= respDue
      ? { label: `${fmtSpan((respDue - now) / HOUR)} left (On Track)`, ok: true }
      : { label: `No response (Breached)`, ok: false };
  const resoDue = created + sla.resolutionDays * 24 * HOUR;
  const reso = resolutionDays(r);
  const resolution = reso !== null
    ? { label: `${fmtSpan(reso * 24)} (${reso <= sla.resolutionDays ? 'Met' : 'Breached'})`, ok: reso <= sla.resolutionDays }
    : now <= resoDue
      ? { label: `${fmtSpan((resoDue - now) / HOUR)} left`, ok: true }
      : { label: `Overdue by ${fmtSpan((now - resoDue) / HOUR)}`, ok: false };
  return { response, resolution };
}

export interface SrKpis {
  open: number;
  openChange: number;
  responseHours: number;
  responseChange: number;
  resolutionDays: number;
  resolutionChange: number;
  rating: number;
  ratingChange: number;
}

const avg = (xs: number[]) => (xs.length ? xs.reduce((t, x) => t + x, 0) / xs.length : 0);
const change = (now: number, last: number) => (last ? Math.round(((now - last) / last) * 100) : 0);

/** Header figures; `last` holds last month's recorded values for the comparison. */
export function srKpis(list: ServiceRequest[], last?: { open: number; responseHours: number; resolutionDays: number; rating: number }): SrKpis {
  const open = list.filter(isOpen).length;
  const resp = avg(list.map(responseHours).filter((x): x is number => x !== null));
  const reso = avg(list.map(resolutionDays).filter((x): x is number => x !== null));
  const rated = list.filter((r) => r.rating > 0).map((r) => r.rating);
  const rating = avg(rated);
  return {
    open,
    openChange: change(open, last?.open ?? 0),
    responseHours: Math.round(resp * 10) / 10,
    responseChange: change(resp, last?.responseHours ?? 0),
    resolutionDays: Math.round(reso * 10) / 10,
    resolutionChange: change(reso, last?.resolutionDays ?? 0),
    rating: Math.round(rating * 10) / 10,
    ratingChange: change(rating, last?.rating ?? 0),
  };
}
