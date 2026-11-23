export type ReportStatus = 'Draft' | 'Published' | 'Scheduled';
export type ReportVisibility = 'Only me' | 'My team' | 'Organization';
export type VizType = 'Table' | 'Bar' | 'Line' | 'Donut';
export type FieldKind = 'text' | 'number' | 'money' | 'percent' | 'date';
export type CalcOperator = '+' | '-' | '×' | '÷';
export type Aggregate = 'Sum' | 'Average' | 'Min' | 'Max';
export type SortDir = 'asc' | 'desc';
export type FilterOperator =
  | 'equals' | 'not equals' | 'contains'
  | '=' | '≠' | '>' | '<' | '≥' | '≤'
  | 'on' | 'before' | 'after';

/** A reportable field. `values` lists the fixed options offered in filters. */
export interface FieldDef {
  key: string;
  label: string;
  kind: FieldKind;
  group: string;
  values?: string[];
  /** ID pattern, e.g. 'WO-{n}' */
  pattern?: string;
  start?: number;
  pad?: number;
  min?: number;
  max?: number;
  step?: number;
  /** Dates: first day and span in days */
  from?: string;
  span?: number;
}

export interface FieldGroup {
  key: string;
  label: string;
  fields: FieldDef[];
}

export interface DataSource {
  key: string;
  label: string;
  module: string;
  groups: string[];
  /** API data source keys (assets, maintenance, inspection) */
  sources: string[];
  description: string;
}

/** Calculated field: left <op> right, both numeric fields. Stored with key "calc_<slug>" (the key the API derives from the label). */
export interface CalcField {
  key: string;
  label: string;
  left: string;
  op: CalcOperator;
  right: string;
}

export interface ReportFilter {
  field: string;
  op: FilterOperator;
  value: string;
}

/** What the builder edits: data, filters, grouping and visualization. */
export interface ReportDefinition {
  sourceKey: string;
  fields: string[];
  calcFields: CalcField[];
  filters: ReportFilter[];
  groupBy: string;
  aggregate: Aggregate;
  sortBy: string;
  sortDir: SortDir;
  /** null = no limit (new reports start without one) */
  rowLimit: number | null;
  viz: VizType;
  /** Column plotted on charts; '' picks the first numeric column */
  chartValue: string;
}

export interface CustomReport extends ReportDefinition {
  id: string;
  name: string;
  description: string;
  owner: string;
  module: string;
  status: ReportStatus;
  favourite: boolean;
  /** ISO local date-time, Asia/Dubai */
  updatedAt: string;
  visibility: ReportVisibility;
  schedule: string;
  /** Owner's user id (Owner filter) */
  ownerId: string;
  /** False when the report belongs to someone else (the API rejects changes) */
  canEdit: boolean;
  /** The library list item has no definition: open the report (GET /{id}) to load it */
  loaded: boolean;
}

export type ReportRow = Record<string, string | number>;

export interface PreviewColumn {
  key: string;
  label: string;
  kind: FieldKind;
}

export interface PreviewResult {
  columns: PreviewColumn[];
  rows: ReportRow[];
  /** Rows matching the filters before the row limit */
  matched: number;
  labelKey: string;
  valueKey: string;
  title: string;
  /** Chart points over every matching row (API chart), null for a table */
  points?: { label: string; value: number }[] | null;
}
