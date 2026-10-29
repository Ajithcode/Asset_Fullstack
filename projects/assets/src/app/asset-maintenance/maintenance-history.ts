/**
 * Maintenance History side panel: the detail shown for one completed work order.
 * DEMO MODE: work orders only carry dates, costs and the technician, so the checklist, parts lines, documents and
 * times are derived from them (same work order → same detail). Parts lines always add up to the order's parts cost.
 */

export type HistType = 'Preventive' | 'Corrective' | 'Inspection' | 'Service';

export interface HistOrder {
  id: string;
  date: string;
  type: HistType;
  description: string;
  labor: number;
  parts: number;
  service: number;
  downtimeMin: number;
  reason: string;
  status: string;
  technician: string;
  priority?: 'High' | 'Medium' | 'Low';
  rootCause?: string;
  resolution?: string;
  nextAction?: string;
}

export interface WoDetail {
  start: string;
  end: string;
  supervisor: string;
  rootCause: string;
  resolution: string;
  nextAction: string;
  priority: 'High' | 'Medium' | 'Low';
  tasks: { task: string; done: boolean; by: string }[];
  parts: { part: string; partNo: string; qty: number; amount: number }[];
  costs: { label: string; amount: number }[];
  documents: { name: string; kind: string; size: string }[];
}

const TASKS: Record<HistType, string[]> = {
  Preventive: ['Isolate and lock out the unit', 'Change oil and filters', 'Check belts, hoses and coolant level', 'Inspect battery and terminals', 'Run no-load test', 'Update the PM checklist'],
  Corrective: ['Isolate and lock out the unit', 'Diagnose the fault', 'Replace / repair the failed part', 'Test run under load', 'Record root cause'],
  Inspection: ['Visual inspection', 'Measure readings against limits', 'Record findings', 'Issue inspection report'],
  Service: ['Isolate the unit', 'Clean and service components', 'Test run', 'Record service details'],
};

const PARTS: Record<HistType, { part: string; partNo: string }[]> = {
  Preventive: [{ part: 'Lube oil 15W-40 (20 L)', partNo: 'CAT-DEO-20L' }, { part: 'Oil filter', partNo: 'CAT-1R-1808' }, { part: 'Fuel filter', partNo: 'CAT-1R-0751' }, { part: 'Air filter element', partNo: 'CAT-132-9461' }],
  Corrective: [{ part: 'Replacement component', partNo: 'CAT-SPARE-01' }, { part: 'Coolant ELC (20 L)', partNo: 'CAT-ELC-20L' }, { part: 'Gasket kit', partNo: 'CAT-GSK-220' }, { part: 'Fasteners & consumables', partNo: 'GEN-CONS-01' }],
  Inspection: [{ part: 'Test consumables', partNo: 'GEN-CONS-02' }],
  Service: [{ part: 'Air filter element', partNo: 'CAT-132-9461' }, { part: 'Cleaning consumables', partNo: 'GEN-CONS-03' }],
};

const DEFAULT_NOTES: Record<HistType, { rootCause: string; resolution: string; nextAction: string }> = {
  Preventive: { rootCause: 'Planned preventive maintenance', resolution: 'All checklist tasks completed', nextAction: 'Next PM as per schedule' },
  Corrective: { rootCause: 'Component failure', resolution: 'Failed part replaced and unit tested', nextAction: 'Monitor at the next PM' },
  Inspection: { rootCause: 'Scheduled / statutory inspection', resolution: 'Readings within limits, report issued', nextAction: 'Next inspection as per schedule' },
  Service: { rootCause: 'Routine service', resolution: 'Service completed and unit tested', nextAction: 'Next service as per schedule' },
};

function hash(s: string): number {
  let h = 0;
  for (const ch of s) h = (h * 31 + ch.charCodeAt(0)) >>> 0;
  return h;
}

function addMinutes(date: string, hhmm: string, minutes: number): string {
  const d = new Date(`${date}T${hhmm}`);
  d.setMinutes(d.getMinutes() + minutes);
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}`;
}

export function buildWoDetail(w: HistOrder): WoDetail {
  const h = hash(w.id);
  const startTime = ['08:00', '08:30', '09:00', '09:30', '10:00'][h % 5];
  // work takes the downtime when the unit was stopped, otherwise about 2 h
  const minutes = w.downtimeMin || 90 + (h % 4) * 30;
  const notes = { ...DEFAULT_NOTES[w.type], ...(w.rootCause ? { rootCause: w.rootCause } : {}), ...(w.resolution ? { resolution: w.resolution } : {}), ...(w.nextAction ? { nextAction: w.nextAction } : {}) };

  const names = TASKS[w.type];
  const tasks = names.map((task) => ({ task, done: true, by: w.technician }));

  const catalogue = PARTS[w.type];
  const n = w.parts ? Math.min(catalogue.length, 1 + (h % catalogue.length)) : 0;
  const weights = Array.from({ length: n }, (_, i) => n - i + 1);
  const wsum = weights.reduce((t, x) => t + x, 0);
  let left = w.parts;
  const parts = catalogue.slice(0, n).map((c, i) => {
    const amount = i === n - 1 ? left : Math.round((w.parts * weights[i]) / wsum / 10) * 10;
    left -= amount;
    return { ...c, qty: 1 + ((h >> i) % 2), amount };
  });

  const costs = [
    { label: 'Labor', amount: w.labor },
    { label: 'Spare Parts', amount: w.parts },
    { label: 'External Service', amount: w.service },
  ].filter((c) => c.amount > 0);

  const documents = [
    { name: `${w.id} work order report.pdf`, kind: 'Report', size: `${180 + (h % 300)} KB` },
    { name: `${w.type} checklist.pdf`, kind: 'Checklist', size: `${90 + (h % 120)} KB` },
    ...(w.service ? [{ name: 'Vendor service sheet.pdf', kind: 'Vendor', size: `${120 + (h % 200)} KB` }] : []),
    { name: 'Before.jpg', kind: 'Photo', size: `${(1.1 + (h % 9) / 10).toFixed(1)} MB` },
    { name: 'After.jpg', kind: 'Photo', size: `${(1.2 + (h % 7) / 10).toFixed(1)} MB` },
  ];

  return {
    start: `${w.date} ${startTime}`,
    end: addMinutes(w.date, startTime, minutes),
    supervisor: w.technician === 'Ahmed Khan' ? 'Salim Ahmed' : 'Ahmed Khan',
    ...notes,
    priority: w.priority ?? (w.type === 'Corrective' ? 'High' : w.type === 'Inspection' ? 'Medium' : 'Low'),
    tasks,
    parts,
    costs,
    documents,
  };
}
