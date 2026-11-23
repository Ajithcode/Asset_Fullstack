import { DataSource, FieldGroup, ReportDefinition, ReportVisibility } from './custom-reports.model';

// Option lists for Custom Reports. The field catalogue, data sources and module list come from the Report Analytics API
// (GET /api/custom-reports/fields, /data-sources and /options); CustomReportsService fills these arrays in place so the
// builder steps, which read them synchronously, always see the loaded catalogue.

/** Library "Module" filter: GET /api/custom-reports/options modules. */
export const RA_MODULES: string[] = [];

export const VISIBILITIES: ReportVisibility[] = ['Only me', 'My team', 'Organization'];

export const SCHEDULES = [
  'Not scheduled',
  'Daily – 07:00',
  'Weekly – Monday, 08:00',
  'Monthly – 1st day, 08:00',
  'Quarterly – 1st day, 08:00'
];

/** Field groups from GET /api/custom-reports/fields (one group per API group and data source). */
export const FIELD_GROUPS: FieldGroup[] = [];

/**
 * Data source choices built from GET /api/custom-reports/data-sources: the required source (assets) alone and combined with
 * each set of optional sources. The key is the API source keys joined with '+', e.g. 'assets+maintenance'.
 */
export const DATA_SOURCES: DataSource[] = [];

export function blankDefinition(sourceKey = 'assets'): ReportDefinition {
  return {
    sourceKey,
    fields: [],
    calcFields: [],
    filters: [],
    groupBy: '',
    aggregate: 'Sum',
    sortBy: '',
    sortDir: 'desc',
    rowLimit: 25,
    viz: 'Table',
    chartValue: ''
  };
}
