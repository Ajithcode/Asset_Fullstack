import { Injectable } from '@angular/core';
import { ExportColumn, ExportRequest } from 'shared-ui';

export interface Report {
  id: string;
  slNo: number;
  title: string;
  timeRange: string;
  expireOn: string;
  recurrence: string;
  createdOn: string;
  type: string;
  shareWith: string;
  generatedOn: string;
  status: 'Completed' | 'Pending' | 'Failed';
}

// Every report carries its own column headers and data rows, so the Reports list
// and the report View page can both render/download real, template-shaped
// data instead of a stub.
export interface GeneratedReport extends Report {
  headers: string[];
  rows: Record<string, string | number>[];
}

// Column headers per report template, taken from the "Key Information" each
// template was specified with. A handful of narrative (non-comma-list)
// descriptions were reworded into concise column headers by judgement call
// (Asset Maintenance History Report, Maintenance Cost Report, WIP Movement &
// Stage History Report) — flagged so they're easy to revise later.
export const TEMPLATE_HEADERS: Record<string, string[]> = {
  // Asset Tracking
  'Asset Location & Movement Report': ['Asset ID', 'Asset Name', 'Current Location', 'Previous Location', 'Movement Date/Time', 'Duration at Location', 'Movement History'],
  'Asset Location History / Trail Report': ['Location Trail', 'Check-In/Out', 'Source Technology', 'Timestamp', 'User/Device'],
  'Asset Missing / Not-Seen Report': ['Not Detected Within', 'Last Seen Location/Time', 'Custodian', 'Risk/Priority'],
  'Asset Movement & Exception Report': ['Unauthorized Movement', 'Geofence Violation', 'After-Hours Movement', 'Unusual Movement', 'Alerts'],
  'Asset Utilization & Dwell-Time Report': ['Time by Location/Zone', 'Idle Duration', 'Movement Frequency', 'Utilization %', 'Frequently/Rarely Moved Assets'],

  // Asset Management
  'Asset Register Report': ['Asset ID', 'Category', 'Make/Model', 'Serial Number', 'Status', 'Location', 'Custodian', 'Acquisition Details'],
  'Asset Lifecycle Report': ['Procurement', 'Commissioning', 'Transfer', 'Maintenance', 'Status Changes', 'Retirement/Disposal History'],
  'Asset Allocation & Custodian Report': ['Asset-to-User/Department/Site Allocation', 'Issue Date', 'Transfer History', 'Acknowledgement'],
  'Asset Valuation & Depreciation Report': ['Purchase Value', 'Capitalization Date', 'Depreciation Method', 'Accumulated Depreciation', 'NBV'],
  'Warranty / AMC / Contract Expiry Report': ['Warranty Dates', 'AMC Details', 'Supplier', 'Contract Reference', 'Expiry Date', 'Upcoming Expirations'],

  // Maintenance Management
  'Preventive Maintenance Compliance Report': ['Planned vs Completed PM', 'Due/Overdue Jobs', 'Completion %', 'SLA Compliance'],
  'Maintenance Work Order Report': ['WO Number', 'Asset', 'Maintenance Type', 'Technician', 'Priority', 'Status', 'Dates', 'Labour/Materials', 'Completion'],
  'Asset Maintenance History Report': ['Asset', 'Date', 'Failures', 'Repairs', 'Parts', 'Cost'],
  'Breakdown & Reliability Report': ['Breakdown Count', 'Downtime', 'MTBF', 'MTTR', 'Recurring Failures', 'Failure Category/Root Cause'],
  'Maintenance Cost Report': ['Labour Cost', 'Spare Parts Cost', 'Vendor Cost', 'Total Cost', 'Asset/Site/Category/Period'],

  // Inspection Management
  'Detailed Inspection Report / Certificate': ['Asset Information', 'Task/Checkpoint', 'Result (Pass/Fail/N/A)', 'Remarks', 'Photos', 'Videos/References', 'Signatures', 'Inspector', 'Approval Information'],
  'Inspection Compliance Summary Report': ['Total Inspections', 'Completed', 'Passed', 'Failed', 'Overdue', 'Compliance %', 'Site/Category Comparison'],
  'Failed Inspection & Findings Report': ['Failed Assets/Tasks', 'Severity', 'Observation', 'Evidence', 'Inspector', 'Corrective Action', 'Responsible Person'],
  'Corrective Action & Reinspection Report': ['Finding', 'Corrective Action', 'Assignee', 'Target Date', 'Evidence', 'Reinspection', 'Closure'],
  'Inspection & Approval Audit Trail Report': ['Inspector Actions', 'Timestamps', 'Submissions', 'L1/L2/L3 Approvals', 'Rejection', 'Redo Requests', 'Comments', 'Final Approval'],

  // Work in Progress (WIP)
  'WIP Status Summary Report': ['Total WIP', 'Stage', 'Status', 'Quantity/Items', 'Owner', 'Location', 'Planned vs Actual Progress'],
  'WIP Ageing Report': ['Item/Asset', 'Current Stage', 'Entry Date', 'Days in Stage', 'Ageing Bucket', 'Responsible Person'],
  'WIP Movement & Stage History Report': ['From Stage', 'To Stage', 'Location', 'User', 'Date/Time', 'Duration'],
  'WIP Delay / Bottleneck Report': ['Delayed Items', 'Expected Completion', 'Actual Progress', 'Stage Delay', 'Ageing', 'Reason', 'Owner'],
  'WIP Completion & Turnaround-Time Report': ['Start Date', 'Completion Date', 'Total Cycle Time', 'Stage-wise Duration', 'SLA/Target vs Actual']
};

// ===== CSV / PDF export (shared by the Reports list and the report View page) =====
const SUMMARY_COLUMNS: ExportColumn<Report>[] = [
  { label: 'Sl No', key: 'slNo' },
  { label: 'Report', key: 'title' },
  { label: 'Time Range', key: 'timeRange' },
  { label: 'Expire On', key: 'expireOn' },
  { label: 'Recurrence', key: 'recurrence' },
  { label: 'Created On', key: 'createdOn' },
  { label: 'Type', key: 'type' },
  { label: 'Share With', key: 'shareWith' },
  { label: 'Generated On', key: 'generatedOn' },
  { label: 'Status', key: 'status' }
];

function fileSafe(name: string): string {
  return (name || 'report').replace(/[^a-z0-9]+/gi, '-').toLowerCase();
}

/**
 * Export request for one report: its template columns and rows (optionally only some columns), or its summary row when
 * there is no template data behind it.
 */
export function reportExportRequest(report: GeneratedReport, columns: string[] = report.headers): ExportRequest {
  const base = { title: report.title || 'Report', fileBase: `${fileSafe(report.title)}-${report.createdOn || new Date().toISOString().slice(0, 10)}`, filters: { Type: report.type, 'Time range': report.timeRange } };
  if (columns.length > 0) {
    return { ...base, columns: columns.map((label) => ({ label, value: (r: Record<string, string | number>) => r[label] ?? '' })), rows: () => report.rows };
  }
  return { ...base, columns: SUMMARY_COLUMNS, rows: () => [report] };
}

/** Export request for a list of reports (one summary row each). */
export function reportsExportRequest(reports: Report[]): ExportRequest<Report> {
  return { title: 'Generated Reports', fileBase: 'generated-reports', columns: SUMMARY_COLUMNS, rows: () => reports };
}

@Injectable({ providedIn: 'root' })
export class GeneratedReportService {
  // Generated reports come from the reporting API (not wired yet); this list only holds reports created this session.
  private reports: GeneratedReport[] = [];
  private nextSequence = 1;

  addReport(report: Omit<GeneratedReport, 'id' | 'slNo'>): GeneratedReport {
    const created: GeneratedReport = {
      ...report,
      id: `RPT-GEN-${this.nextSequence}`,
      slNo: this.nextSequence
    };
    this.nextSequence++;
    this.reports = [created, ...this.reports];
    return created;
  }

  getAll(): GeneratedReport[] {
    return this.reports;
  }

  getById(id: string): GeneratedReport | undefined {
    return this.reports.find((r) => r.id === id);
  }

  get mostRecent(): GeneratedReport | null {
    return this.reports[0] ?? null;
  }
}
