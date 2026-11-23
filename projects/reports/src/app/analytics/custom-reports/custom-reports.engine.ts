import { DATA_SOURCES, FIELD_GROUPS } from './custom-reports.data';
import {
  CalcField, DataSource, FieldDef, FieldGroup, FieldKind, FilterOperator, PreviewColumn,
  ReportDefinition, ReportFilter
} from './custom-reports.model';

// Report definition helpers for the builder; report rows come from POST /api/custom-reports/preview

export const COUNT_KEY = '__count';

/** Placeholder until the data sources are loaded */
const NO_SOURCE: DataSource = { key: '', label: 'No data source', module: 'Assets', groups: [], sources: [], description: '' };

export function isNumeric(kind: FieldKind): boolean {
  return kind === 'number' || kind === 'money' || kind === 'percent';
}

export function sourceOf(key: string): DataSource {
  return DATA_SOURCES.find((s) => s.key === key) ?? DATA_SOURCES[0] ?? NO_SOURCE;
}

export function groupsOf(sourceKey: string): FieldGroup[] {
  const source = sourceOf(sourceKey);
  return source.groups.map((k) => FIELD_GROUPS.find((g) => g.key === k)).filter((g): g is FieldGroup => !!g);
}

/** Fields of the data source plus the report's calculated fields. */
export function availableFields(def: ReportDefinition): FieldDef[] {
  const base = groupsOf(def.sourceKey).flatMap((g) => g.fields);
  return [...base, ...def.calcFields.map(calcAsField)];
}

export function calcAsField(c: CalcField): FieldDef {
  return { key: c.key, label: c.label, kind: 'number', group: 'calculated' };
}

export function fieldOf(def: ReportDefinition, key: string): FieldDef | undefined {
  if (key === COUNT_KEY) return { key, label: 'Records', kind: 'number', group: '' };
  const calc = def.calcFields.find((c) => c.key === key);
  return calc ? calcAsField(calc) : FIELD_GROUPS.flatMap((g) => g.fields).find((f) => f.key === key);
}

export function operatorsFor(kind: FieldKind): FilterOperator[] {
  if (isNumeric(kind)) return ['=', '≠', '>', '<', '≥', '≤'];
  if (kind === 'date') return ['on', 'before', 'after'];
  return ['equals', 'not equals', 'contains'];
}

/** Validation message for a filter row, or '' when it is usable. */
export function filterError(def: ReportDefinition, f: ReportFilter): string {
  const field = fieldOf(def, f.field);
  if (!field) return 'Choose a field.';
  if (!f.op) return 'Choose an operator.';
  const value = f.value.trim();
  if (!value) return 'Enter a value.';
  if (isNumeric(field.kind) && Number.isNaN(Number(value))) return 'Enter a number.';
  if (field.kind === 'date' && Number.isNaN(new Date(value).getTime())) return 'Enter a valid date.';
  return '';
}

/** Output columns for the current definition (grouped reports show the group, a record count and numeric fields). */
export function outputColumns(def: ReportDefinition): PreviewColumn[] {
  const selected = def.fields.map((k) => fieldOf(def, k)).filter((f): f is FieldDef => !!f);
  const group = def.groupBy ? fieldOf(def, def.groupBy) : undefined;
  if (!group) return selected.map((f) => ({ key: f.key, label: f.label, kind: f.kind }));
  const numeric = selected.filter((f) => isNumeric(f.kind) && f.key !== group.key);
  return [
    { key: group.key, label: group.label, kind: group.kind },
    { key: COUNT_KEY, label: 'Records', kind: 'number' },
    ...numeric.map((f) => ({ key: f.key, label: `${f.label} (${(def.aggregate || 'Sum').toLowerCase()})`, kind: f.kind }))
  ];
}

/** Formats a cell for display. */
export function formatCell(kind: FieldKind, value: string | number | undefined): string {
  if (value === undefined || value === '') return '—';
  if (kind === 'money') return `AED ${Number(value).toLocaleString('en-US', { maximumFractionDigits: 0 })}`;
  if (kind === 'percent') return `${Number(value).toLocaleString('en-US', { maximumFractionDigits: 1 })}%`;
  if (kind === 'number') return Number(value).toLocaleString('en-US', { maximumFractionDigits: 2 });
  if (kind === 'date') {
    const d = new Date(`${String(value).slice(0, 10)}T00:00:00Z`);
    return Number.isNaN(d.getTime()) ? String(value) : d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric', timeZone: 'UTC' });
  }
  return String(value);
}

/** Switches the data source and drops fields, filters and grouping the new source does not have. Returns the number of fields removed. */
export function applySource(def: ReportDefinition, sourceKey: string): number {
  def.sourceKey = sourceKey;
  const baseKeys = new Set(groupsOf(sourceKey).flatMap((g) => g.fields).map((f) => f.key));
  def.calcFields = def.calcFields.filter((c) => baseKeys.has(c.left) && baseKeys.has(c.right));
  const keys = new Set([...baseKeys, ...def.calcFields.map((c) => c.key)]);
  const before = def.fields.length;
  def.fields = def.fields.filter((k) => keys.has(k));
  def.filters = def.filters.filter((f) => keys.has(f.field));
  if (def.groupBy && !keys.has(def.groupBy)) def.groupBy = '';
  if (def.sortBy && def.sortBy !== COUNT_KEY && !keys.has(def.sortBy)) def.sortBy = '';
  if (def.chartValue && def.chartValue !== COUNT_KEY && !keys.has(def.chartValue)) def.chartValue = '';
  return before - def.fields.length;
}

/** Removes a selected field and anything that depends on it. */
export function removeField(def: ReportDefinition, key: string): void {
  def.fields = def.fields.filter((k) => k !== key);
  if (def.groupBy === key) def.groupBy = '';
  if (def.sortBy === key) def.sortBy = '';
  if (def.chartValue === key) def.chartValue = '';
}
