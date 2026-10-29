/** A reusable task: instructions, requirements and completion criteria used by process stages and job orders. */

export type TaskStatus = 'Draft' | 'Published';

export interface Measurement {
  id: number;
  parameter: string;
  /** Target or limit as written, e.g. "6.0 ± 0.2" or "None". */
  target: string;
  unit: string;
  required: boolean;
}

export interface ChecklistItem {
  id: number;
  text: string;
  mandatory: boolean;
}

export interface TaskMaterial {
  id: number;
  code: string;
  name: string;
  quantity: number;
  unit: string;
  optional: boolean;
}

export interface CompletionRule {
  key: 'checklist' | 'readings' | 'evidence' | 'approval';
  label: string;
  on: boolean;
}

export interface TaskVersion {
  version: string;
  date: string;
  by: string;
  note: string;
  status: TaskStatus;
}

export interface TaskDef {
  code: string;
  name: string;
  version: string;
  status: TaskStatus;
  /** yyyy-MM-dd */
  lastUpdated: string;
  lastUpdatedBy: string;
  /** Job template stages that use this task. */
  usedIn: { template: string; templateName: string; stage: string }[];
  general: {
    category: string;
    taskType: string;
    assetType: string;
    durationMins: number;
    priority: string;
    responsibleRole: string;
    owner: string;
    description: string;
  };
  instructions: string[];
  /** Standard operating procedure document, or null when none is attached. */
  sop: { name: string; revision: string } | null;
  measurements: Measurement[];
  captureReadings: boolean;
  requirePassFail: boolean;
  resources: { skill: string; resourceType: string; defaultAsset: string; crewSize: string; tools: string[] };
  checklist: { name: string; items: ChecklistItem[]; photoEvidence: string; minPhotos: number; signature: string; attachment: string };
  materials: TaskMaterial[];
  safety: { ppe: string[]; hazards: string[]; permitRequired: boolean; permitType: string; isolationRequired: boolean };
  completion: { rules: CompletionRule[]; failureAction: string };
  controls: { mandatory: boolean; allowSkip: boolean; approvalRequired: boolean; slaMins: number };
  notifications: { warnPercent: number; onComplete: string; onFail: string; onOverdue: string };
  versions: TaskVersion[];
}
