/**
 * Maintenance History side panel: the detail shown for one completed work order.
 * Work orders only carry dates, costs, notes and the technician, so the detail shows those; the checklist, parts
 * lines and documents stay empty until the Maintenance API serves them.
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

const DEFAULT_PRIORITY: Record<HistType, 'High' | 'Medium' | 'Low'> = { Preventive: 'Low', Corrective: 'High', Inspection: 'Medium', Service: 'Low' };

export function buildWoDetail(w: HistOrder): WoDetail {
  const costs = [
    { label: 'Labor', amount: w.labor },
    { label: 'Spare Parts', amount: w.parts },
    { label: 'External Service', amount: w.service },
  ].filter((c) => c.amount > 0);

  return {
    start: w.date,
    end: '',
    supervisor: '',
    rootCause: w.rootCause ?? '',
    resolution: w.resolution ?? '',
    nextAction: w.nextAction ?? '',
    priority: w.priority ?? DEFAULT_PRIORITY[w.type],
    tasks: [],
    parts: [],
    costs,
    documents: [],
  };
}
