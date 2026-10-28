export type ComplianceStatus = 'Active' | 'Inactive';

export interface CertificationTypeRecord {
  code: string;
  name: string;
  category: string;
  // Asset, Vendor, People
  applicableTo: string[];
  validityMonths: number;
  renewalReminderDays: number;
  description: string;
  status: ComplianceStatus;
  // Requirements
  issuingBody: string;
  // Standard code, e.g. STD-001
  standardCode: string;
  requiredDocuments: string[];
  inspectionRequired: boolean;
  // Settings
  blockWhenExpired: boolean;
  notifyOwner: boolean;
  createdOn: string;
  updatedOn: string;
}

export interface PermitTypeRecord {
  code: string;
  name: string;
  category: string;
  validDays: number;
  description: string;
  status: ComplianceStatus;
  // Workflow & validity
  approvalLevels: number;
  approverRoles: string[];
  maxExtensionDays: number;
  isolationRequired: boolean;
  gasTestRequired: boolean;
  closeOutInspection: boolean;
  createdOn: string;
  updatedOn: string;
}

export interface RegulationRecord {
  code: string;
  name: string;
  country: string;
  category: string;
  issuingAuthority: string;
  description: string;
  status: ComplianceStatus;
  // Scope & applicability
  assetCategories: string[];
  standardCodes: string[];
  effectiveDate: string;
  reviewMonths: number;
  penalties: string;
  createdOn: string;
  updatedOn: string;
}

export interface StandardRecord {
  code: string;
  name: string;
  organization: string;
  category: string;
  version: string;
  description: string;
  status: ComplianceStatus;
  // Details
  title: string;
  publishedOn: string;
  nextReview: string;
  url: string;
  mandatory: boolean;
  createdOn: string;
  updatedOn: string;
}
