// Types and option lists for Administration > Integration & API.

export type IntegrationStatus = 'Connected' | 'Active' | 'Warning' | 'Disabled';
export type Environment = 'Development' | 'Staging' | 'Production';
export type SyncDirection = 'Inbound' | 'Outbound' | 'Bi-directional';
export type RunStatus = 'Success' | 'Warning' | 'Failed';
export type LogLevel = 'Info' | 'Warning' | 'Error';

export interface SyncRun {
  id: string;
  object: string;
  /** "in" = external system to Asset360, "out" = Asset360 to external system. */
  direction: 'in' | 'out';
  records: number;
  startedAt: string;
  durationSec: number;
  status: RunStatus;
  message: string;
}

export interface FieldMapping {
  id: string;
  object: string;
  source: string;
  target: string;
  transform: string;
  required: boolean;
}

/** API keys keep only a prefix and the last 4 characters; the full secret is shown once at creation. */
export interface ApiKey {
  id: string;
  name: string;
  prefix: string;
  last4: string;
  createdAt: string;
  expiresAt: string;
  scopes: string[];
  status: 'Active' | 'Revoked';
  lastUsed: string | null;
}

export interface Webhook {
  id: string;
  url: string;
  events: string[];
  active: boolean;
  secretLast4: string;
  lastDelivery: string | null;
  lastStatus: 'Delivered' | 'Failed' | 'Never';
}

export interface LogEntry {
  id: string;
  at: string;
  level: LogLevel;
  event: string;
  message: string;
  actor: string;
}

export interface IntegrationLogo {
  text: string;
  bg: string;
  ink: string;
}

export interface Integration {
  id: string;
  code: string;
  name: string;
  provider: string;
  type: string;
  version: string;
  logo: IntegrationLogo;
  status: IntegrationStatus;
  environment: Environment;
  baseUrl: string;
  authMethod: string;
  clientId: string;
  /** Last 4 characters of the stored secret key; the secret itself is never kept in the UI. */
  secretLast4: string;
  secretRotatedAt: string;
  syncFrequency: string;
  timeout: number;
  retryPolicy: string;
  ipAllowlist: boolean;
  lastTestAt: string;
  lastTestPassed: boolean;
  lastSync: string | null;
  recordsSynced: number;
  syncDirection: SyncDirection;
  dataObjects: string[];
  mappings: FieldMapping[];
  runs: SyncRun[];
  apiKeys: ApiKey[];
  webhooks: Webhook[];
  logs: LogEntry[];
}

export const ENVIRONMENTS: Environment[] = ['Development', 'Staging', 'Production'];
export const AUTH_METHODS = [
  'OAuth 2.0 (Client Credentials)',
  'OAuth 2.0 (Authorization Code)',
  'OpenID Connect',
  'API Key',
  'Basic Authentication',
  'Mutual TLS'
];
export const SYNC_FREQUENCIES = ['Real-time', 'Every 5 minutes', 'Every 15 minutes', 'Every 30 minutes', 'Hourly', 'Every 6 hours', 'Daily', 'Manual'];
export const RETRY_POLICIES = ['No retries', '3 retries (fixed 30s)', '3 retries (exponential backoff)', '5 retries (exponential backoff)'];
export const API_SCOPES = ['assets:read', 'assets:write', 'inventory:read', 'inventory:write', 'workorders:read', 'workorders:write', 'people:read', 'webhooks:manage'];
export const WEBHOOK_EVENTS = [
  'asset.created',
  'asset.updated',
  'asset.deleted',
  'workorder.created',
  'workorder.completed',
  'inventory.low_stock',
  'alert.raised',
  'sync.failed'
];
export const TRANSFORMS = ['None', 'Uppercase', 'Trim', 'Date (ISO 8601)', 'Lookup', 'Currency (AED)', 'Concatenate'];
export const STATUSES: IntegrationStatus[] = ['Connected', 'Active', 'Warning', 'Disabled'];

/** Minutes between scheduled syncs, used for "Next Sync". 0 = not scheduled. */
export const FREQUENCY_MINUTES: Record<string, number> = {
  'Real-time': 1,
  'Every 5 minutes': 5,
  'Every 15 minutes': 15,
  'Every 30 minutes': 30,
  Hourly: 60,
  'Every 6 hours': 360,
  Daily: 1440,
  Manual: 0
};

export interface ProviderInfo {
  type: string;
  logo: IntegrationLogo;
  direction: SyncDirection;
  objects: string[];
}

/** Known providers: integration type, badge and the objects they usually exchange. */
export const PROVIDERS: Record<string, ProviderInfo> = {
  'SAP ERP': { type: 'ERP', logo: { text: 'SAP', bg: '#0a6ed1', ink: '#fff' }, direction: 'Bi-directional', objects: ['Assets', 'Vendors', 'Work Orders', 'Inventory'] },
  'Microsoft Entra ID': { type: 'Identity', logo: { text: 'ID', bg: '#e0edff', ink: '#0b63ce' }, direction: 'Inbound', objects: ['Users', 'Groups'] },
  'IoT Platform': { type: 'IoT', logo: { text: 'IoT', bg: '#e0f2fe', ink: '#0369a1' }, direction: 'Inbound', objects: ['Sensors', 'Telemetry', 'Alerts'] },
  'Microsoft Power BI': { type: 'Analytics', logo: { text: 'BI', bg: '#fef3c7', ink: '#b45309' }, direction: 'Outbound', objects: ['Assets', 'Work Orders', 'Costs'] },
  ServiceNow: { type: 'ITSM', logo: { text: 'SN', bg: '#dcfce7', ink: '#15803d' }, direction: 'Bi-directional', objects: ['Incidents', 'Assets'] },
  'Oracle Fusion': { type: 'Finance', logo: { text: 'O', bg: '#ef4444', ink: '#fff' }, direction: 'Bi-directional', objects: ['Assets', 'Depreciation', 'Purchase Orders'] },
  SendGrid: { type: 'Notification', logo: { text: '@', bg: '#dbeafe', ink: '#1d4ed8' }, direction: 'Outbound', objects: ['Notifications'] },
  Workday: { type: 'HR', logo: { text: 'HR', bg: '#e0e7ff', ink: '#3730a3' }, direction: 'Inbound', objects: ['Employees', 'Departments'] },
  'Microsoft Teams': { type: 'Collaboration', logo: { text: 'T', bg: '#ede9fe', ink: '#5b21b6' }, direction: 'Outbound', objects: ['Notifications', 'Approvals'] },
  Geotab: { type: 'IoT', logo: { text: 'FT', bg: '#ffedd5', ink: '#c2410c' }, direction: 'Inbound', objects: ['Vehicles', 'GPS'] },
  Salesforce: { type: 'CRM', logo: { text: 'SF', bg: '#e0f2fe', ink: '#0284c7' }, direction: 'Bi-directional', objects: ['Customers', 'Contracts'] },
  'Custom REST': { type: 'Custom', logo: { text: 'API', bg: '#f1f5f9', ink: '#334155' }, direction: 'Bi-directional', objects: ['Assets'] }
};

/** ISO time `days` from now. */
export function inDays(days: number): string {
  return new Date(Date.now() + days * 86400000).toISOString();
}
