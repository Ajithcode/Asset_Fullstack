export type AuditSeverity = 'Critical' | 'High' | 'Medium' | 'Low';
export type AuditOutcome = 'Success' | 'Failed' | 'Blocked';
export type AuditCategory =
  | 'User Activity'
  | 'Configuration'
  | 'Security'
  | 'Data Changes'
  | 'Integrations'
  | 'Workflow'
  | 'Imports/Exports';

/** One immutable audit record. */
export interface AuditEvent {
  id: string;
  /** Local time, yyyy-mm-ddThh:mm:ss (GMT+4). */
  timestamp: string;
  event: string;
  category: AuditCategory;
  module: string;
  action: string;
  user: string;
  userId: string;
  role: string;
  entityType: string;
  /** What the Entity / Record column shows. */
  entity: string;
  recordId: string;
  ip: string;
  device: string;
  location: string;
  countryCode: string;
  requestId: string;
  severity: AuditSeverity;
  outcome: AuditOutcome;
  mfa: 'Enabled' | 'Not enrolled';
  sessionId: string;
  authMethod: string;
  riskScore: number;
  /** Pretty JSON before / after the change, when the event changed a value. */
  before: string;
  after: string;
}

export interface AuditFilters {
  from: string;
  to: string;
  eventType: string;
  module: string;
  user: string;
  severity: '' | AuditSeverity;
  ip: string;
  outcome: '' | AuditOutcome;
}

export interface SavedAuditFilter {
  name: string;
  filters: AuditFilters;
  category: '' | AuditCategory;
}
