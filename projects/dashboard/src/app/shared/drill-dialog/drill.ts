/** Content of the drill-down popup opened by clicking a widget on the dashboard overviews. */
export type DrillValue = string | number;

export interface DrillColumn {
  key: string;
  label: string;
  /** Right-aligned, sorted as a number. */
  num?: boolean;
  /** Shows the value as a coloured pill; colour per value. */
  pill?: Record<string, string>;
}

export interface DrillRow {
  cells: Record<string, DrillValue>;
  /** Opens this record in the popup (with Back to the list). */
  open?: () => Drill;
}

export interface Drill {
  title: string;
  subtitle?: string;
  /** Headline figures shown as tiles. */
  stats?: { label: string; value: DrillValue; color?: string }[];
  /** Label / value pairs for a single record. */
  fields?: { label: string; value: DrillValue; color?: string }[];
  tableTitle?: string;
  columns?: DrillColumn[];
  rows?: DrillRow[];
  empty?: string;
  /** Footer buttons; the popup closes after running one. */
  actions?: { label: string; run: () => void }[];
}

/** Rounded percentage with one decimal. */
export function pct(n: number, of: number): number {
  return of ? Math.round((n / of) * 1000) / 10 : 0;
}
