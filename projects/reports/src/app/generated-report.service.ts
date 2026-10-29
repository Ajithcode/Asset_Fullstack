import { Injectable } from '@angular/core';
import * as XLSX from 'xlsx';

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

// Every report — whether produced via Create Report or one of the seeded demo
// rows — carries its own column headers and data rows, so the Reports list
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

// ===== Mock row generation (for reports created via Create Report) =====
// There's no backend, so a report "generated" from a template needs
// plausible-looking rows built on the fly. Rather than hand-author bespoke
// data for 25 different templates, each column gets a value shaped by
// recognizing common header patterns (dates, percentages, names, statuses…),
// falling back to a readable placeholder for anything bespoke.
const SAMPLE_ASSET_IDS = ['AST-1001', 'AST-1002', 'AST-1003'];
const SAMPLE_ASSET_NAMES = ['HVAC Unit 1', 'Fire Panel A', 'Chiller Pump 2'];
const SAMPLE_PEOPLE = ['David Smith', 'Sarah Wilson', 'Faseeh Akthar', 'Joseph'];
const SAMPLE_LOCATIONS = ['Dubai HQ Tower', 'Sharjah Gate Tower', 'Abudhabi ADDAX Tower'];
const SAMPLE_STATUSES = ['Completed', 'Pending', 'In Progress'];

function pick<T>(list: T[], index: number): T {
  return list[index % list.length];
}

function sampleDate(rowIndex: number): string {
  const base = new Date(2026, 4, 1 + rowIndex * 7);
  return base.toISOString().slice(0, 10);
}

function sampleValueFor(header: string, rowIndex: number): string | number {
  const h = header.toLowerCase();

  if (h.includes('asset id')) return pick(SAMPLE_ASSET_IDS, rowIndex);
  if (h.includes('asset name') || h === 'asset' || h.startsWith('item/asset')) return pick(SAMPLE_ASSET_NAMES, rowIndex);
  if (h.includes('date') || h.includes('timestamp')) return sampleDate(rowIndex);
  if (h.includes('%')) return pick([92, 78, 65, 88], rowIndex);
  if (h.includes('cost') || h.includes('value') || h === 'nbv') return pick([1250, 3400, 875, 2100], rowIndex);
  if (h.includes('count') || h.includes('duration') || h.includes('mtbf') || h.includes('mttr') || h.includes('time') || h.includes('quantity')) {
    return pick([2, 5, 8, 12], rowIndex);
  }
  if (h.includes('pass/fail') || h.includes('result')) return pick(['Pass', 'Fail', 'Pass', 'Pass'], rowIndex);
  if (h.includes('status')) return pick(SAMPLE_STATUSES, rowIndex);
  if (h.includes('technician') || h.includes('inspector') || h.includes('owner') || h.includes('custodian') || h.includes('responsible') || h.includes('assignee') || h.includes('user')) {
    return pick(SAMPLE_PEOPLE, rowIndex);
  }
  if (h.includes('location') || h.includes('site')) return pick(SAMPLE_LOCATIONS, rowIndex);
  if (h.includes('priority') || h.includes('risk') || h.includes('severity')) return pick(['High', 'Medium', 'Low'], rowIndex);
  if (h.includes('supplier') || h.includes('vendor')) return pick(['Carrier Corporation', 'Siemens Energy AG', 'Kirloskar Pumps Ltd.'], rowIndex);

  return `${header} ${rowIndex + 1}`;
}

export function buildSampleRows(headers: string[], count: number): Record<string, string | number>[] {
  return Array.from({ length: count }, (_, rowIndex) => {
    const row: Record<string, string | number> = {};
    headers.forEach((header) => {
      row[header] = sampleValueFor(header, rowIndex);
    });
    return row;
  });
}

// ===== Excel export (shared by the Reports list and the report View page) =====
function reportWorksheet(report: GeneratedReport): XLSX.WorkSheet {
  if (report.headers.length > 0) {
    return XLSX.utils.json_to_sheet(report.rows, { header: report.headers });
  }
  // No template data behind this report — export its summary row instead of
  // producing an empty file.
  return XLSX.utils.json_to_sheet([{
    'Sl No': report.slNo,
    Report: report.title,
    'Time Range': report.timeRange,
    'Expire On': report.expireOn,
    Recurrence: report.recurrence,
    'Created On': report.createdOn,
    Type: report.type,
    'Share With': report.shareWith,
    'Generated On': report.generatedOn,
    Status: report.status
  }]);
}

function fileSafe(name: string): string {
  return (name || 'report').replace(/[^a-z0-9]+/gi, '-').toLowerCase();
}

export function downloadReportAsExcel(report: GeneratedReport): void {
  const workbook = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(workbook, reportWorksheet(report), 'Report');
  XLSX.writeFile(workbook, `${fileSafe(report.title)}-${report.createdOn || new Date().toISOString().slice(0, 10)}.xlsx`);
}

export function downloadReportsAsWorkbook(reports: GeneratedReport[], fileName: string): void {
  if (reports.length === 0) return;
  const workbook = XLSX.utils.book_new();
  reports.forEach((report, index) => {
    const sheetName = fileSafe(report.title || `Report ${index + 1}`).slice(0, 31) || `Report ${index + 1}`;
    XLSX.utils.book_append_sheet(workbook, reportWorksheet(report), sheetName);
  });
  XLSX.writeFile(workbook, fileName);
}

// ===== Seed data =====
// The four demo rows the Reports list originally shipped with, now given
// real column headers/rows (drawn from example report-view screens) so
// clicking through to "View" shows something structured instead of nothing.
const SEED_REPORTS: GeneratedReport[] = [
  {
    id: 'RPT-SEED-ASSET-TRACKING', slNo: 1, title: 'Asset Tracking', timeRange: '1 - Month',
    expireOn: '2027-07-01', recurrence: 'Once', createdOn: '2026-05-01',
    type: 'URL', shareWith: 'piq@gmail.com', generatedOn: '2026-05-01',
    status: 'Completed',
    headers: ['Asset ID', 'Asset Name', 'Current Location', 'Previous Location', 'Movement Date/Time', 'Duration at Location'],
    rows: [
      { 'Asset ID': 'AST-1001', 'Asset Name': 'HVAC Unit 1', 'Current Location': 'Dubai HQ Tower', 'Previous Location': 'Sharjah Gate Tower', 'Movement Date/Time': '2026-05-01 08:15', 'Duration at Location': '3 days' },
      { 'Asset ID': 'AST-1002', 'Asset Name': 'Fire Panel A', 'Current Location': 'Sharjah Gate Tower', 'Previous Location': 'Abudhabi ADDAX Tower', 'Movement Date/Time': '2026-05-08 09:05', 'Duration at Location': '5 days' },
      { 'Asset ID': 'AST-1003', 'Asset Name': 'Chiller Pump 2', 'Current Location': 'Abudhabi ADDAX Tower', 'Previous Location': 'Dubai HQ Tower', 'Movement Date/Time': '2026-05-15 07:50', 'Duration at Location': '2 days' }
    ]
  },
  {
    id: 'RPT-SEED-ASSET-MANAGEMENT', slNo: 2, title: 'Asset Management', timeRange: '1 - Month',
    expireOn: '2027-07-01', recurrence: 'Once', createdOn: '2026-05-01',
    type: 'URL', shareWith: 'piq@gmail.com', generatedOn: '2026-05-01',
    status: 'Completed',
    headers: ['Asset ID', 'Category', 'Make/Model', 'Serial Number', 'Status', 'Location', 'Custodian'],
    rows: [
      { 'Asset ID': 'AST-1001', Category: 'HVAC', 'Make/Model': 'Carrier 30XA', 'Serial Number': 'CR-30XA-8841', Status: 'Completed', Location: 'Dubai HQ Tower', Custodian: 'David Smith' },
      { 'Asset ID': 'AST-1002', Category: 'Fire Safety', 'Make/Model': 'Siemens FC901', 'Serial Number': 'SM-FC901-2207', Status: 'Pending', Location: 'Sharjah Gate Tower', Custodian: 'Sarah Wilson' },
      { 'Asset ID': 'AST-1003', Category: 'Plumbing', 'Make/Model': 'Kirloskar KDS-150', 'Serial Number': 'KP-KDS-5563', Status: 'In Progress', Location: 'Abudhabi ADDAX Tower', Custodian: 'Faseeh Akthar' }
    ]
  },
  {
    id: 'RPT-SEED-MAINTENANCE-MANAGEMENT', slNo: 3, title: 'Maintenance Management', timeRange: '1 - Month',
    expireOn: '2027-07-01', recurrence: 'Once', createdOn: '2026-05-01',
    type: 'URL', shareWith: 'piq@gmail.com', generatedOn: '2026-05-01',
    status: 'Completed',
    headers: ['WO Number', 'Asset', 'Maintenance Type', 'Technician', 'Priority', 'Status', 'Completion'],
    rows: [
      { 'WO Number': 'WO-4021', Asset: 'HVAC Unit 1', 'Maintenance Type': 'Preventive', Technician: 'David Smith', Priority: 'Medium', Status: 'Completed', Completion: '100%' },
      { 'WO Number': 'WO-4022', Asset: 'Fire Panel A', 'Maintenance Type': 'Corrective', Technician: 'Sarah Wilson', Priority: 'High', Status: 'In Progress', Completion: '60%' },
      { 'WO Number': 'WO-4023', Asset: 'Chiller Pump 2', 'Maintenance Type': 'Preventive', Technician: 'Faseeh Akthar', Priority: 'Low', Status: 'Pending', Completion: '0%' }
    ]
  },
  {
    id: 'RPT-SEED-INSPECTION', slNo: 4, title: 'Inspection', timeRange: '1 - Month',
    expireOn: '2027-07-01', recurrence: 'Once', createdOn: '2026-05-01',
    type: 'URL', shareWith: 'piq@gmail.com', generatedOn: '2026-05-01',
    status: 'Pending',
    headers: ['Asset Information', 'Task/Checkpoint', 'Result (Pass/Fail/N/A)', 'Remarks', 'Inspector', 'Approval Information'],
    rows: [
      { 'Asset Information': 'AST-1001 · HVAC Unit 1', 'Task/Checkpoint': 'Filter Condition', 'Result (Pass/Fail/N/A)': 'Pass', Remarks: 'No action needed', Inspector: 'David Smith', 'Approval Information': 'Approved' },
      { 'Asset Information': 'AST-1002 · Fire Panel A', 'Task/Checkpoint': 'Alarm Trigger Test', 'Result (Pass/Fail/N/A)': 'Fail', Remarks: 'Sensor requires replacement', Inspector: 'Sarah Wilson', 'Approval Information': 'Pending Review' },
      { 'Asset Information': 'AST-1003 · Chiller Pump 2', 'Task/Checkpoint': 'Vibration Levels', 'Result (Pass/Fail/N/A)': 'N/A', Remarks: 'Scheduled for next cycle', Inspector: 'Faseeh Akthar', 'Approval Information': 'Awaiting Inspection' }
    ]
  }
];

@Injectable({ providedIn: 'root' })
export class GeneratedReportService {
  private reports: GeneratedReport[] = [...SEED_REPORTS];
  private nextSequence = SEED_REPORTS.length + 1;

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
