/** One execution of a published Job Template: the template version is fixed when the order is created. */

export type OrderStatus = 'Planned' | 'In Progress' | 'Paused' | 'Completed';
export type StageStatus = 'Pending' | 'In Progress' | 'Completed';
export type TaskStatus = 'Pending' | 'In Progress' | 'Completed';
export type GateStatus = 'Pending' | 'In Progress' | 'Done';
export type Severity = 'Low' | 'Medium' | 'High' | 'Critical';
export type UnitStatus = 'Not Started' | 'In Progress' | 'Accepted' | 'Rework' | 'Rejected';

export interface OrderTask {
  name: string;
  assignee: string;
  status: TaskStatus;
  /** Number of photos attached. */
  evidence: number;
}

export interface OrderStage {
  name: string;
  status: StageStatus;
  /** Local date-time yyyy-MM-ddTHH:mm, or '' when not reached. */
  start: string;
  end: string;
  tasks: OrderTask[];
  checklist: { item: string; done: boolean }[];
}

export interface Gate {
  name: string;
  /** Stage whose completion passes the gate, or '' for one checked before work starts. */
  stage: string;
  status: GateStatus;
  at: string;
}

export interface Permit {
  name: string;
  number: string;
  issuer: string;
  status: string;
  validUntil: string;
}

export interface QualityResult {
  name: string;
  stage: string;
  criteria: string;
  result: string;
}

export interface OrderResource {
  name: string;
  type: string;
  assigned: string;
  status: string;
}

export interface MaterialLine {
  item: string;
  code: string;
  reserved: number;
  issued: number;
  consumed: number;
  unit: string;
}

export interface Issue {
  id: string;
  title: string;
  severity: Severity;
  stage: string;
  owner: string;
  status: 'Open' | 'In Progress' | 'Resolved';
  raisedAt: string;
  description: string;
}

export interface ActivityEntry {
  at: string;
  title: string;
  detail: string;
}

export interface UnitRecord {
  serial: string;
  status: UnitStatus;
  stage: string;
  note: string;
}

export interface JobOrder {
  code: string;
  templateCode: string;
  templateVersion: string;
  name: string;
  /** Public path of the product picture. */
  image: string;
  status: OrderStatus;
  priority: Severity;
  batch: string;
  site: string;
  location: string;
  supervisor: string;
  team: string;
  plannedStart: string;
  due: string;
  actualStart: string;
  /** Time the sample data was captured; the page's clock runs on from here. */
  asOf: string;
  linkedAsset: { tag: string; name: string };
  output: { planned: number; unit: string; completed: number; accepted: number; rework: number; rejected: number };
  stages: OrderStage[];
  gates: Gate[];
  permits: Permit[];
  qualityChecks: QualityResult[];
  resources: OrderResource[];
  materials: MaterialLine[];
  issues: Issue[];
  /** Newest first. */
  activity: ActivityEntry[];
  serials: UnitRecord[];
}
