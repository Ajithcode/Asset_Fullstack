export type WorkflowStatus = 'Active' | 'Draft' | 'Inactive';
export type NodeType = 'trigger' | 'condition' | 'approval' | 'action' | 'notification' | 'end';
export type EdgeLabel = '' | 'Yes' | 'No' | 'Rejected';
export type WorkflowIcon = 'zap' | 'circle' | 'cart' | 'shield' | 'package' | 'user' | 'wrench' | 'file' | 'truck' | 'clipboard';
export type Tone = 'purple' | 'blue' | 'green' | 'pink' | 'orange' | 'red';

/** Every node keeps the full settings bag; the drawer only shows the fields that apply to its type. */
export interface NodeConfig {
  // Trigger
  triggerEvent: string;
  triggerModule: string;
  // Condition
  conditionField: string;
  operator: string;
  conditionValue: string;
  // Approval
  approvalType: string;
  approverSource: string;
  approverRole: string;
  approverUser: string;
  slaHours: number | null;
  escalationHours: number | null;
  requiredApprovals: number | null;
  allowDelegation: boolean;
  commentsRequired: boolean;
  // Action
  actionType: string;
  actionTarget: string;
  actionValue: string;
  // Notification
  template: string;
  channel: string;
  recipient: string;
  // End
  outcome: string;
  notifyRequestor: boolean;
}

export interface WfNode {
  id: string;
  type: NodeType;
  name: string;
  x: number;
  y: number;
  config: NodeConfig;
}

export interface WfEdge {
  id: string;
  from: string;
  to: string;
  label: EdgeLabel;
}

export interface WfGraph {
  nodes: WfNode[];
  edges: WfEdge[];
}

/** A published snapshot, picked from the version selector. */
export interface WorkflowVersion {
  version: string;
  publishedOn: string;
  graph: WfGraph;
}

export interface Workflow {
  id: string;
  code: string;
  name: string;
  category: string;
  owner: string;
  description: string;
  status: WorkflowStatus;
  updated: string;
  version: string;
  versions: WorkflowVersion[];
  graph: WfGraph;
  runningInstances: number;
  pendingApprovals: number;
  icon: WorkflowIcon;
  tone: Tone;
}

/** Card sizes on the canvas (canvas units, before zoom). */
export const NODE_SIZE: Record<NodeType, { w: number; h: number }> = {
  trigger: { w: 84, h: 88 },
  condition: { w: 100, h: 100 },
  approval: { w: 84, h: 88 },
  action: { w: 84, h: 88 },
  notification: { w: 96, h: 88 },
  end: { w: 140, h: 48 }
};

export const NODE_LABELS: Record<NodeType, string> = {
  trigger: 'Trigger',
  condition: 'Condition',
  approval: 'Approval',
  action: 'Action',
  notification: 'Notification',
  end: 'End'
};
