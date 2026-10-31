// Shapes returned by the Inspection API (A360.Inspection.Api, reached through the API gateway).
// JSON is camelCase; every record also carries the audit fields in `InspectionAudit`.
// Dates are ISO strings ('2026-10-22' for dates, full ISO for date-times).

export interface InspectionAudit {
  id: string;
  status: string | null;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  clientId: string | null;
  tenantId: string | null;
}

export type Severity = 'Critical' | 'High' | 'Medium' | 'Low';
export type Priority = 'Critical' | 'High' | 'Medium' | 'Low';

/** /api/inspection-types — code IT-001 */
export interface InspectionType extends InspectionAudit {
  inspectionTypeCode: string;
  inspectionTypeName: string;
  category: string;
  description: string;
  defaultChecklistCode: string;
  defaultChecklistName: string;
  defaultPriority: string;
  defaultFrequency: string;
  applicableAssets: string;
  responsibleRole: string;
  photoEvidenceOnFailure: boolean;
  signatureRequired: boolean;
  correctiveActionOnFailure: boolean;
  permitRequired: boolean;
  isActive: boolean;
}

/** /api/task-categories — code TC-001 */
export interface TaskCategory extends InspectionAudit {
  categoryCode: string;
  categoryName: string;
  description: string;
  displayOrder: number;
}

/** /api/inspection-tasks — code ITK-001 */
export interface InspectionTask extends InspectionAudit {
  taskCode: string;
  taskTitle: string;
  taskCategory: string;
  taskDescription: string;
  /** 'Pass / Fail' | 'Yes / No' | 'Numeric' | 'Text' */
  responseType: string;
  unit: string;
  estimatedDurationMinutes: number;
  minValue: number | null;
  maxValue: number | null;
  procedure: string;
  readingRequired: boolean;
  photoRequiredOnFailure: boolean;
  createFindingOutsideLimits: boolean;
  defaultSeverity: string;
  isCriticalTask: boolean;
  isMandatory: boolean;
  isActive: boolean;
}

export interface ChecklistTaskItem {
  taskId: string;
  taskCode: string;
  taskTitle: string;
  responseType: string;
  weight: number;
  isMandatory: boolean;
  acceptanceCriteria: string;
  order: number;
}

export interface ChecklistSection {
  sectionName: string;
  order: number;
  tasks: ChecklistTaskItem[];
}

/** /api/checklists — code CHK-001; `status` is 'Published' | 'Draft' */
export interface Checklist extends InspectionAudit {
  checklistCode: string;
  templateName: string;
  inspectionTypeId: string;
  inspectionTypeName: string;
  assetCategory: string;
  assetType: string;
  /** Modules that reuse the checklist: 'Inspection' | 'Maintenance' | 'WIP' | 'Audit' */
  modules: string[];
  scoring: string;
  owner: string;
  version: number;
  versionLabel: string;
  effectiveDate: string | null;
  sections: ChecklistSection[];
  allMandatoryRequired: boolean;
  photoOnFailedItems: boolean;
  inspectorSignatureRequired: boolean;
  criticalFailureOverridesScore: boolean;
  createFindingOnFailure: boolean;
  routeCorrectiveAction: boolean;
  isActive: boolean;
}

/** /api/inspection-plans — code PLN-001; `status` is 'Active' | 'Inactive' */
export interface InspectionPlan extends InspectionAudit {
  planCode: string;
  planName: string;
  inspectionTypeName: string;
  category: string;
  /** 'Daily' | 'Weekly' | 'Monthly' | 'Quarterly' | 'Annually' */
  frequency: string;
  assetCount: number;
  nextDueDate: string | null;
  inspectorName: string;
  checklistCode: string;
  checklistName: string;
  leadTimeDays: number;
  autoGenerate: boolean;
  isActive: boolean;
}

export interface InspectionResponse {
  sectionName: string;
  taskCode: string;
  taskTitle: string;
  responseType: string;
  unit: string;
  minValue: number | null;
  maxValue: number | null;
  procedure: string;
  required: boolean;
  reading: string;
  /** 'Pass' | 'Fail' | 'N/A' | '' (pending) */
  result: string;
  notes: string;
  evidence: string[];
}

export interface SignOffStep {
  role: string;
  name: string;
  /** 'Signed' | 'Awaiting review' | 'Pending' */
  state: string;
  signedAt: string | null;
}

/**
 * /api/inspections — code INS-2026-0148.
 * `status` is 'Draft' | 'Scheduled' | 'In Progress' | 'Completed' | 'Overdue' | 'Cancelled'.
 */
export interface Inspection extends InspectionAudit {
  inspectionCode: string;
  title: string;
  inspectionTypeName: string;
  /** Short label for the register chip: 'Safety' | 'Condition' | 'Statutory' | 'Pre-use' ... */
  typeShort: string;
  priority: string;
  siteName: string;
  department: string;
  planCode: string;
  planName: string;
  /** 'Scheduled' | 'Manual' | 'Event' */
  trigger: string;
  description: string;
  /** 'Asset-based' | 'Location-based' */
  scope: string;
  category: string;
  referenceStandard: string;
  permitRequired: boolean;
  evidenceRequired: boolean;
  assetCode: string;
  assetName: string;
  location: string;
  assignedTo: string;
  assignedTeam: string;
  dueDate: string | null;
  scheduledStart: string | null;
  scheduledEnd: string | null;
  startedAt: string | null;
  completedAt: string | null;
  findingsCount: number;
  checklistCode: string;
  checklistName: string;
  responses: InspectionResponse[];
  signOff: SignOffStep[];
}

export interface FindingActivity {
  at: string;
  by: string;
  text: string;
}

/**
 * /api/inspection-findings — code FND-2026-0087.
 * `status` is 'Open' | 'In Progress' | 'Pending Verification' | 'Overdue' | 'Closed'.
 */
export interface InspectionFinding extends InspectionAudit {
  findingCode: string;
  title: string;
  inspectionCode: string;
  assetCode: string;
  assetName: string;
  sourceTask: string;
  severity: string;
  owner: string;
  dueDate: string | null;
  recordedValue: string;
  acceptance: string;
  observation: string;
  evidence: string[];
  correctiveAction: string;
  assignedTo: string;
  workOrder: string;
  workOrderStatus: string;
  /** 'Open' | 'Action' | 'Verify' | 'Closed' */
  workflowStep: string;
  activity: FindingActivity[];
}

export interface CalibrationPoint {
  reference: number;
  asFound: number;
  asLeft: number;
  result: string;
}

export interface CalibrationHistoryEntry {
  date: string;
  result: string;
  certificate: string;
  performedBy: string;
}

/**
 * /api/calibration-instruments — code PG-001 (free text, entered by the user).
 * `status` is 'Valid' | 'Due Soon' | 'Overdue' | 'Out of Tolerance'.
 */
export interface CalibrationInstrument extends InspectionAudit {
  instrumentCode: string;
  instrumentName: string;
  serialNumber: string;
  location: string;
  range: string;
  unit: string;
  tolerance: string;
  intervalMonths: number;
  provider: string;
  /** 'In-house' | 'Service Provider' */
  providerType: string;
  assignedTo: string;
  lastCalibration: string | null;
  nextDue: string | null;
  certificate: string;
  lastResult: string;
  results: CalibrationPoint[];
  referenceStandard: string;
  referenceCertificate: string;
  validAtCalibration: boolean;
  history: CalibrationHistoryEntry[];
}

/**
 * /api/signatures-and-stamps — code SIG-001 / STP-001.
 * One collection for both: `kind` is 'Signature' | 'Stamp'. `status` is 'Active' | 'Inactive'.
 */
export interface SignatureAndStamp extends InspectionAudit {
  signatureCode: string;
  kind: string;
  user: string;
  signatureName: string;
  role: string;
  department: string;
  authorizedRole: string;
  /** Data URL or URL of the specimen image */
  signatureImage: string;
  stampImage: string;
  assignedStampCode: string;
  effectiveDate: string | null;
  expiryDate: string | null;
  signaturePosition: string;
  stampPosition: string;
  includeSignerNameDate: boolean;
  requirePasswordConfirmationBeforeSigning: boolean;
  isActive: boolean;
}

/** /api/notification-templates — code NT-001; `status` is 'Active' | 'Draft' */
export interface NotificationTemplate extends InspectionAudit {
  templateCode: string;
  templateName: string;
  event: string;
  /** 'Email' | 'In-app' | 'Push' */
  channels: string[];
  subject: string;
  messageBody: string;
  recipients: string[];
  ccRecipients: string[];
  buttonLabel: string;
  timing: string;
  respectUserLocale: boolean;
  preventDuplicates: boolean;
  isActive: boolean;
}

export interface ReportSection {
  name: string;
  enabled: boolean;
  order: number;
}

/** /api/report-templates — code RT-001; `status` is 'Published' | 'Draft' */
export interface ReportTemplate extends InspectionAudit {
  reportTemplateCode: string;
  reportTitle: string;
  inspectionTypeName: string;
  format: string;
  pageSize: string;
  orientation: string;
  language: string;
  accentColor: string;
  versionLabel: string;
  reportNumberFormat: string;
  sections: ReportSection[];
  includePassedItems: boolean;
  includePhotos: boolean;
  showAcceptanceCriteria: boolean;
  showFindingStatus: boolean;
  includeSignatures: boolean;
  includeInternalNotes: boolean;
  isActive: boolean;
}

export interface SequenceAllocation {
  reference: string;
  record: string;
  allocatedAt: string;
  /** 'Created' | 'Cancelled' */
  state: string;
}

/** /api/numbering-sequences — code NS-001; `status` is 'Active' | 'Inactive' */
export interface NumberingSequence extends InspectionAudit {
  sequenceCode: string;
  sequenceName: string;
  /** Record type: 'Inspection' | 'Plan' | 'Finding' | 'Report' | 'Calibration' */
  numberType: string;
  prefix: string;
  /** '-' | '/' | '' */
  separator: string;
  /** 'YYYY' | 'YY' | '' (no year) */
  yearFormat: string;
  sequenceDigits: number;
  startingNumber: number;
  /** Next number to allocate */
  runningNumber: number;
  /** 'Annually' | 'Monthly' | 'Never' */
  resetFrequency: string;
  scope: string;
  timezone: string;
  /** e.g. 'INS-{YYYY}-{SEQ}' */
  format: string;
  generateOnFinalCreation: boolean;
  doNotReuse: boolean;
  allowManualOverride: boolean;
  lastAllocated: string;
  assignedThisYear: number;
  allocations: SequenceAllocation[];
  isActive: boolean;
}

export interface WorkingDay {
  day: string;
  working: boolean;
  start: string;
  end: string;
}

export interface CalendarException {
  date: string;
  description: string;
  duration: string;
  /** 'Site exception' | 'Working override' | 'Holiday' */
  type: string;
}

/** /api/holiday-and-working-calendars — code CAL-001 */
export interface WorkingCalendar extends InspectionAudit {
  calendarCode: string;
  calendarName: string;
  siteName: string;
  timezone: string;
  effectiveFrom: string | null;
  workingWeek: WorkingDay[];
  breakPeriod: string;
  exceptions: CalendarException[];
  /** 'Move to next working day' | 'Move to previous working day' | 'Keep date' */
  nonWorkingDayRule: string;
  /** 'Working days' | 'Calendar days' */
  dueDateCalculation: string;
  slaCalendar: string;
  skipHolidays: boolean;
  respectInspectorShift: boolean;
  allowManualOverride: boolean;
  assignedPlans: number;
  assignedInspectors: number;
  assignedSites: number;
  isActive: boolean;
}
