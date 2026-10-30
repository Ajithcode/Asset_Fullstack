/** Preventive maintenance schedules for one asset: types, status rules and date maths. */
import { MaintenanceTask } from '../asset-details/asset-details';

export type PmCategory = 'Routine' | 'Inspection' | 'Testing' | 'Overhaul';
export type PmStatus = 'On Schedule' | 'Due Soon' | 'Overdue';

export interface PmSchedule {
  id: string;
  name: string;
  description: string;
  component: string;
  category: PmCategory;
  frequency: string;
  /** yyyy-MM-dd, or '' if never done. */
  lastPerformed: string;
  /** yyyy-MM-dd */
  nextDue: string;
  assignedTo: string;
  durationHours: number;
  estCost: number;
  template: string;
  /** Spare parts reserved before the job, or 'none'. */
  parts: string;
  /** Words that identify this schedule's completed work orders in the history. */
  match: string[];
  tasks: string[];
  checklist: string[];
  documents: { name: string; size: string }[];
  /** Next-action keys already done for the current due date. */
  actionsDone?: string[];
}

export const PM_CATEGORIES: PmCategory[] = ['Routine', 'Inspection', 'Testing', 'Overhaul'];
export const PM_FREQUENCIES = ['Monthly', 'Quarterly', 'Every 6 months', 'Bi-Annual', 'Yearly', 'Every 36 months'];
export const PM_STATUSES: PmStatus[] = ['On Schedule', 'Due Soon', 'Overdue'];
export const PM_STATUS_COLORS: Record<PmStatus, string> = { 'On Schedule': '#16a34a', 'Due Soon': '#eaa200', Overdue: '#dc2626' };
/** Due within this many days counts as "Due Soon". */
export const DUE_SOON_DAYS = 30;

const MONTHS_OF: Record<string, number> = { Monthly: 1, Quarterly: 3, 'Every 6 months': 6, 'Bi-Annual': 6, Yearly: 12, 'Every 36 months': 36 };

export function frequencyMonths(frequency: string): number {
  if (MONTHS_OF[frequency]) return MONTHS_OF[frequency];
  const n = /every\s+(\d+)\s+month/i.exec(frequency);
  return n ? Number(n[1]) : 12;
}

export function addMonths(iso: string, months: number): string {
  const d = new Date(iso + 'T00:00');
  const day = d.getDate();
  d.setDate(1);
  d.setMonth(d.getMonth() + months);
  // keep the day, clamped to the month's length (31 Jan + 1 month = 28/29 Feb)
  d.setDate(Math.min(day, new Date(d.getFullYear(), d.getMonth() + 1, 0).getDate()));
  return isoOf(d);
}

export function addDays(iso: string, days: number): string {
  const d = new Date(iso + 'T00:00');
  d.setDate(d.getDate() + days);
  return isoOf(d);
}

export function isoOf(d: Date): string {
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
}

export function daysUntil(iso: string, today: string): number {
  return Math.round((new Date(iso + 'T00:00').getTime() - new Date(today + 'T00:00').getTime()) / 86_400_000);
}

export function pmStatus(s: PmSchedule, today: string): PmStatus {
  const days = daysUntil(s.nextDue, today);
  return days < 0 ? 'Overdue' : days <= DUE_SOON_DAYS ? 'Due Soon' : 'On Schedule';
}

function categoryOf(task: string): PmCategory {
  if (/overhaul/i.test(task)) return 'Overhaul';
  if (/test/i.test(task)) return 'Testing';
  if (/inspect|check|survey|scan/i.test(task)) return 'Inspection';
  return 'Routine';
}

/** Assets without their own PM list start from the maintenance plan on their health record. */
export function schedulesFromPlan(plan: MaintenanceTask[], manufacturer: string): PmSchedule[] {
  const prefix = (manufacturer || 'GEN').slice(0, 3).toUpperCase();
  return plan
    .filter((p) => p.nextDue)
    .map((p, i) => ({
      id: `PM-${String(i + 1).padStart(2, '0')}`,
      name: p.task,
      description: `${p.task} as per the manufacturer's maintenance schedule.`,
      component: 'Main unit',
      category: categoryOf(p.task),
      frequency: p.frequency,
      lastPerformed: p.lastDone,
      nextDue: p.nextDue,
      assignedTo: p.owner,
      durationHours: 2,
      estCost: 400,
      template: `${prefix}-PM-${String(i + 1).padStart(3, '0')}`,
      parts: 'as per checklist',
      match: [p.task.split(/[ ,]/)[0].toLowerCase()],
      tasks: ['Isolate the unit', p.task, 'Test run', 'Record readings'],
      checklist: ['Permit / lock-out in place', 'Work done as per procedure', 'Readings within limits', 'Area left clean', 'PM record signed'],
      documents: [{ name: `${p.task} procedure.pdf`, size: '200 KB' }],
    }));
}
