/** A reusable job definition. Job Orders are created from a published template and record actual execution. */

export type TemplateStatus = 'Draft' | 'Published';
export type Priority = 'Low' | 'Medium' | 'High' | 'Critical';
export type ResourceType = 'Team' | 'Role' | 'Equipment';

export interface TemplateGeneral {
  jobCategory: string;
  assetType: string;
  organization: string;
  defaultSite: string;
  outputItem: string;
  baseQuantity: number;
  unit: string;
  estimatedHours: number;
  defaultPriority: Priority;
  owner: string;
  description: string;
  requireApproval: boolean;
  allowParallel: boolean;
  batchTraceability: boolean;
}

export interface Stage {
  id: number;
  name: string;
  durationHours: number;
  /** Name of a resource in the template's resource list. */
  resource: string;
  /** Name of a checklist, or '' for none. */
  checklist: string;
  /** Condition to leave the stage. */
  gate: string;
}

export interface Task {
  id: number;
  /** Name of the stage the task belongs to. */
  stage: string;
  name: string;
  durationMins: number;
  mandatory: boolean;
  skill: string;
}

export interface Checklist {
  id: number;
  name: string;
  stage: string;
  items: number;
}

export interface Resource {
  id: number;
  name: string;
  type: ResourceType;
  quantity: number;
  stage: string;
}

export interface Material {
  id: number;
  code: string;
  name: string;
  quantity: number;
  unit: string;
  stage: string;
}

export interface QualityCheck {
  id: number;
  name: string;
  stage: string;
  criteria: string;
}

export interface PermitRule {
  id: number;
  name: string;
  stage: string;
  issuer: string;
}

export interface Kpi {
  id: number;
  name: string;
  target: number;
  unit: string;
}

export interface AlertRule {
  id: number;
  event: string;
  notify: string;
  channel: string;
}

export interface ApprovalPolicy {
  approvers: string[];
  minApprovals: number;
}

export interface VersionEntry {
  version: string;
  /** yyyy-MM-dd */
  date: string;
  by: string;
  note: string;
  status: TemplateStatus;
}

export interface JobTemplate {
  code: string;
  name: string;
  version: string;
  status: TemplateStatus;
  /** yyyy-MM-dd */
  lastUpdated: string;
  lastUpdatedBy: string;
  general: TemplateGeneral;
  stages: Stage[];
  tasks: Task[];
  checklists: Checklist[];
  resources: Resource[];
  materials: Material[];
  quality: { checks: QualityCheck[]; permits: PermitRule[]; kpis: Kpi[] };
  sla: { targetHours: number; warningPercent: number; escalateTo: string; alerts: AlertRule[] };
  access: { approvalPolicy: ApprovalPolicy | null; editors: string[]; viewers: string[]; versions: VersionEntry[] };
  execution: { reworkAllowed: boolean; completionGate: string; workflow: string };
}
