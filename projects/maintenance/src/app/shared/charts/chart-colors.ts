/** Series colours used by the Maintenance charts, matching the mockups. */
export const C = {
  green: '#15803d',
  greenLight: '#34d399',
  blue: '#2563eb',
  red: '#ef4444',
  purple: '#8b5cf6',
  violet: '#7c3aed',
  navy: '#1d4ed8',
  amber: '#f59e0b',
  yellow: '#fbbf24',
  teal: '#14b8a6',
  orange: '#f97316',
  lavender: '#c4b5fd'
};

/** Colour per status / risk / type label; unknown labels fall back to grey. */
export const LABEL_COLORS: Record<string, string> = {
  Completed: C.green,
  Resolved: C.green,
  'On Schedule': C.green,
  Healthy: C.green,
  'In Progress': C.blue,
  Overdue: C.red,
  'High Risk': C.red,
  High: C.red,
  Scheduled: C.purple,
  Open: C.purple,
  'At Risk': C.yellow,
  Preventive: C.navy,
  Corrective: C.violet,
  Predictive: C.amber,
  Inspection: C.teal,
  Service: C.orange,
  'Time Based': C.navy,
  'Usage Based': C.violet,
  'Condition Based': C.amber,
  'Calendar Based': C.teal,
  'Equipment Issue': C.navy,
  'Facility Issue': C.violet,
  'Spare Part Request': C.amber,
  'Inspection Request': C.teal,
  Other: C.orange
};

export function colorFor(label: string): string {
  return LABEL_COLORS[label] ?? '#94a3b8';
}
