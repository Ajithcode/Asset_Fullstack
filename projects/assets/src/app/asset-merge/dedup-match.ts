/** Duplicate detection for Asset Merge / Dedup: a weighted similarity score between two asset records. */

export type MatchField = 'name' | 'serialNumber' | 'model' | 'manufacturer' | 'category' | 'location';

export interface DedupRules {
  fields: MatchField[];
  /** Lowest score (0-100) for each confidence band. */
  high: number;
  medium: number;
  low: number;
  /** Also compare disposed assets. */
  includeInactive: boolean;
}

/** The parts of a record that are compared. */
export type Comparable = Record<MatchField, string> & { status: string };

export const MATCH_FIELDS: { key: MatchField; label: string; weight: number }[] = [
  { key: 'name', label: 'Asset Name', weight: 35 },
  { key: 'serialNumber', label: 'Serial Number', weight: 40 },
  { key: 'model', label: 'Model', weight: 25 },
  { key: 'manufacturer', label: 'Manufacturer', weight: 10 },
  { key: 'category', label: 'Category', weight: 10 },
  { key: 'location', label: 'Location', weight: 10 },
];

export const DEFAULT_RULES: DedupRules = { fields: ['name', 'serialNumber', 'model'], high: 90, medium: 70, low: 50, includeInactive: true };

/** Lower case letters and digits only, so "250KVA" and "250 kVA" compare equal. */
function squash(s: string): string {
  return s.toLowerCase().replace(/[^a-z0-9]/g, '');
}

/** Sørensen–Dice coefficient on letter pairs (0-1). */
export function similarity(a: string, b: string): number {
  const x = squash(a);
  const y = squash(b);
  if (!x || !y) return 0;
  if (x === y) return 1;
  if (x.length < 2 || y.length < 2) return 0;
  const pairs = new Map<string, number>();
  for (let i = 0; i < x.length - 1; i++) {
    const p = x.slice(i, i + 2);
    pairs.set(p, (pairs.get(p) ?? 0) + 1);
  }
  let hits = 0;
  for (let i = 0; i < y.length - 1; i++) {
    const p = y.slice(i, i + 2);
    const n = pairs.get(p) ?? 0;
    if (n) {
      hits++;
      pairs.set(p, n - 1);
    }
  }
  return (2 * hits) / (x.length - 1 + y.length - 1);
}

/** Match score 0-100. Serial numbers must match exactly; an empty serial counts as unknown (half weight). */
export function matchScore(a: Comparable, b: Comparable, rules: DedupRules): number {
  let total = 0;
  let got = 0;
  for (const f of MATCH_FIELDS) {
    if (!rules.fields.includes(f.key)) continue;
    total += f.weight;
    const x = a[f.key];
    const y = b[f.key];
    let sim: number;
    if (f.key === 'serialNumber') sim = !x || !y ? 0.5 : squash(x) === squash(y) ? 1 : 0;
    else sim = similarity(x, y);
    got += f.weight * sim;
  }
  return total ? Math.round((got / total) * 100) : 0;
}

export function confidence(score: number, rules: DedupRules): 'High' | 'Medium' | 'Low' | null {
  return score >= rules.high ? 'High' : score >= rules.medium ? 'Medium' : score >= rules.low ? 'Low' : null;
}
