import { MasterIcon } from '../../shared/master-icons';

export type BusinessUnitStatus = 'Active' | 'Inactive';

// Built-in logo shown when no image is uploaded
export type BusinessUnitIcon = MasterIcon;

export interface BusinessUnitRecord {
  code: string;
  name: string;
  // Organization master code, e.g. DCS
  organizationCode: string;
  // Optional parent business unit for sub-units
  parentCode: string | null;
  icon: BusinessUnitIcon;
  logo: string | null;
  description: string;
  head: string;
  costCenter: string;
  currency: string;
  status: BusinessUnitStatus;
  address: { line1: string; line2: string; city: string; state: string; country: string; postalCode: string };
  contactEmail: string;
  phoneCountry: string;
  phone: string;
  effectiveFrom: string;
  effectiveTo: string;
  allowSites: boolean;
  budgetTracking: boolean;
  assetNumbering: boolean;
  approvals: boolean;
  createdOn: string;
  updatedOn: string;
}

export interface BusinessUnitFilters {
  search: string;
  status: '' | BusinessUnitStatus;
  organizationCode: string;
}
