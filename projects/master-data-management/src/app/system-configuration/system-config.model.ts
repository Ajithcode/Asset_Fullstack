export type ConfigStatus = 'Active' | 'Inactive';

export interface SyncStatusRecord {
  code: string;
  name: string;
  description: string;
  color: string;
  isSuccess: boolean;
  status: ConfigStatus;
  createdOn: string;
  updatedOn: string;
}

export type ChartPreview = 'column' | 'bar' | 'line' | 'pie' | 'doughnut' | 'area' | 'gauge' | 'table';

export interface ChartTypeRecord {
  code: string;
  name: string;
  category: string;
  description: string;
  // Built-in preview, or an uploaded image
  preview: ChartPreview;
  image: string | null;
  status: ConfigStatus;
  createdOn: string;
  updatedOn: string;
}

export interface UpdateSourceRecord {
  code: string;
  name: string;
  type: string;
  description: string;
  status: ConfigStatus;
  createdOn: string;
  updatedOn: string;
}

/** Everything on the System Settings page, saved together. */
export interface SystemSettings {
  general: {
    systemName: string;
    systemUrl: string;
    timeZone: string;
    dateFormat: string;
    timeFormat: string;
    language: string;
    currency: string;
    recordsPerPage: number;
    auditLog: boolean;
    offlineMode: boolean;
    maintenanceMode: boolean;
  };
  dateTime: { weekStartsOn: string; fiscalYearStart: string; workdayStart: string; workdayEnd: string };
  branding: { logo: string | null; primaryColor: string; loginMessage: string };
  fileUpload: { maxSizeMb: number; allowedTypes: string[]; virusScan: boolean };
  session: { timeoutMinutes: number; idleWarningMinutes: number; rememberMeDays: number; concurrentSessions: boolean };
  defaults: { criticality: string; condition: string; updateSource: string; chartType: string };
  security: {
    minPasswordLength: number;
    requireUppercase: boolean;
    requireNumber: boolean;
    requireSymbol: boolean;
    passwordExpiryDays: number;
    lockoutAttempts: number;
    mfaRequired: boolean;
    ipAllowlist: string;
  };
  email: { smtpHost: string; smtpPort: number; senderName: string; senderEmail: string; useTls: boolean; channels: string[]; digest: string };
  localization: { languages: string[]; rtl: boolean; numberFormat: string; units: string };
  numbering: { prefix: string; pattern: string; startNumber: number; digits: number; resetYearly: boolean; includeSite: boolean };
  integration: { erp: string; apiEnabled: boolean; rateLimitPerMinute: number; webhookUrl: string; syncIntervalMinutes: number; sources: string[]; failedSyncStatus: string };
  backup: { frequency: string; time: string; retentionDays: number; auditRetentionMonths: number; deletedRetentionDays: number };
  other: { betaFeatures: boolean; helpdeskEmail: string; supportPhone: string; termsUrl: string };
}
