/**
 * Output units for a job, shared by the Product / Output and Progress Log pages.
 * DEMO MODE: loaded from outputs.json and kept in memory until the production API is wired up.
 */
import DATA from './outputs.json';

export type Disposition = 'Accepted' | 'Awaiting QC' | 'Rework' | 'Rejected';
export type HandoverState = 'Received' | 'Pending' | 'Not eligible';

export interface OutputUnit {
  serial: string;
  stage: string;
  producedAt: string;
  operator: string;
  disposition: Disposition;
  inspection: string;
  inspectionRef: string;
  location: string;
  /** Goods receipt the unit was handed over on (empty until received into stock). */
  handoverRef: string;
  /** Asset register tag once registered (qualifying serialized equipment). */
  assetTag: string;
  notes: string;
  evidence: number;
  reworkCount: number;
  rejectReason: string;
  history: { at: string; by: string; text: string }[];
}

export interface Handover {
  ref: string;
  store: string;
  at: string;
  by: string;
  serials: string[];
}

export interface OutputJob {
  code: string;
  name: string;
  status: string;
  item: string;
  batch: string;
  uom: string;
  site: string;
  planned: number;
  routing: string;
}

const SOURCE = DATA as unknown as {
  asOf: string;
  job: OutputJob;
  components: { part: string; ref: string }[];
  outputs: OutputUnit[];
  handovers: Handover[];
  stores: string[];
  locations: string[];
  operators: string[];
  stages: string[];
};

export const OUTPUT_AS_OF = SOURCE.asOf;
export const OUTPUT_JOB: OutputJob = SOURCE.job;
export const OUTPUT_COMPONENTS = SOURCE.components;
export const OUTPUTS: OutputUnit[] = structuredClone(SOURCE.outputs);
export const HANDOVERS: Handover[] = structuredClone(SOURCE.handovers);
export const OUTPUT_LISTS = { stores: SOURCE.stores, locations: SOURCE.locations, operators: SOURCE.operators, stages: SOURCE.stages };

/** Only accepted units can be received into stock. */
export function handoverState(u: OutputUnit): HandoverState {
  if (u.disposition !== 'Accepted') return 'Not eligible';
  return u.handoverRef ? 'Received' : 'Pending';
}

/** Shared demo clock, so both pages agree on "now": the data's time plus the time since the first page opened. */
let OPENED_AT = 0;
const pad = (n: number) => String(n).padStart(2, '0');
export const stamp = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;

export function parseLocal(s: string): Date {
  const [date, time = '00:00'] = s.split('T');
  const [y, m, d] = date.split('-').map(Number);
  const [hh, mm] = time.split(':').map(Number);
  return new Date(y, m - 1, d, hh, mm);
}

export function demoNow(): string {
  if (!OPENED_AT) OPENED_AT = Date.now();
  return stamp(new Date(parseLocal(OUTPUT_AS_OF).getTime() + (Date.now() - OPENED_AT)));
}
