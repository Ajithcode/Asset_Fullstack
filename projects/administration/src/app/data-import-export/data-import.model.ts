export type JobType = 'Import' | 'Export';
export type JobStatus = 'Completed' | 'Running' | 'Failed' | 'Scheduled' | 'Draft';
export type JobOperation = 'Insert' | 'Update' | 'Upsert';
export type FileFormat = 'XLSX' | 'CSV';
export type ScheduleMode = 'manual' | 'once' | 'recurring';
export type ScheduleFrequency = 'Daily' | 'Weekly' | 'Monthly';
export type IssueSeverity = 'Error' | 'Warning';

export interface ValidationIssue {
  row: number;
  field: string;
  issue: string;
  severity: IssueSeverity;
  status: 'Open' | 'Resolved';
}

/** One source column mapped onto an Asset360 field ('' = not mapped). */
export interface FieldMapping {
  source: string;
  target: string;
  sample: string;
}

export interface ValidationRule {
  id: string;
  name: string;
  description: string;
  field: string;
  severity: IssueSeverity;
  enabled: boolean;
}

export interface JobSchedule {
  mode: ScheduleMode;
  /** One-time run date (yyyy-mm-dd). */
  date: string;
  frequency: ScheduleFrequency;
  /** Weekday for weekly runs, day of month for monthly runs. */
  day: string;
  time: string;
}

export interface JobRun {
  id: string;
  startedAt: string;
  durationSec: number;
  runBy: string;
  total: number;
  successful: number;
  failed: number;
  status: 'Completed' | 'Failed';
}

export interface DataJob {
  id: string;
  code: string;
  name: string;
  type: JobType;
  dataObject: string;
  owner: string;
  createdBy: string;
  version: string;
  status: JobStatus;
  active: boolean;
  /** Last run / last change (yyyy-mm-ddThh:mm). */
  date: string;
  records: number;
  successful: number;
  failed: number;
  warnings: number;
  failureReason: string;
  operation: JobOperation;
  fileName: string;
  fileFormat: FileFormat;
  fileSize: string;
  uploadedBy: string;
  uploadedOn: string;
  duplicateHandling: string;
  dateFormat: string;
  delimiter: string;
  encoding: string;
  batchSize: number;
  schedule: JobSchedule;
  recipients: string[];
  mapping: FieldMapping[];
  rules: ValidationRule[];
  issues: ValidationIssue[];
  runs: JobRun[];
}
