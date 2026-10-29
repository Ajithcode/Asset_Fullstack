export const ORGANIZATION_TYPES = ['Head Office', 'Legal Entity', 'Subsidiary', 'Branch', 'Division'] as const;
export type OrganizationType = (typeof ORGANIZATION_TYPES)[number];
export type OrganizationStatus = 'Active' | 'Inactive';

export interface OrganizationAddress {
  line1: string;
  line2: string;
  city: string;
  state: string;
  country: string;
  postalCode: string;
}

export interface OrganizationContact {
  person: string;
  designation: string;
  email: string;
  // Country name whose dial code prefixes `phone`
  phoneCountry: string;
  phone: string;
}

export interface OrganizationSettings {
  currency: string;
  timeZone: string;
  language: string;
  dateFormat: string;
  // 'YYYY-MM-DD'; effectiveTo is optional
  effectiveFrom: string;
  effectiveTo: string;
  allowSubOrganizations: boolean;
  allowMultipleLocations: boolean;
  useForAssetNumbering: boolean;
  enableFinancialTracking: boolean;
}

export interface OrganizationRecord {
  code: string;
  name: string;
  type: OrganizationType;
  parentCode: string | null;
  // Data URL or app asset path
  logo: string | null;
  registrationNumber: string;
  website: string;
  description: string;
  // Counts owned by the Business Unit and Site masters; shown read-only here
  businessUnits: number;
  sites: number;
  status: OrganizationStatus;
  address: OrganizationAddress;
  contact: OrganizationContact;
  settings: OrganizationSettings;
  createdOn: string;
  updatedOn: string;
}

export interface OrganizationFilters {
  search: string;
  status: '' | OrganizationStatus;
  type: '' | OrganizationType;
}
