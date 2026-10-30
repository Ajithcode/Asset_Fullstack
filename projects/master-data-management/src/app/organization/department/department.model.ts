import { MasterIcon } from '../../shared/master-icons';

export const DEPARTMENT_TYPES = ['Technical', 'Operational', 'Quality', 'Support', 'Administrative', 'Finance', 'Commercial'] as const;
export type DepartmentType = (typeof DEPARTMENT_TYPES)[number];
export type DepartmentStatus = 'Active' | 'Inactive';

export interface DepartmentRecord {
  code: string;
  name: string;
  businessUnitCode: string;
  // Optional parent department for sub-departments
  parentCode: string | null;
  icon: MasterIcon;
  logo: string | null;
  type: DepartmentType;
  head: string;
  deputy: string;
  costCenter: string;
  description: string;
  status: DepartmentStatus;
  // Counts owned by other modules (people, assets, work orders); shown read-only
  employees: number;
  assets: number;
  openWorkOrders: number;
  email: string;
  phoneCountry: string;
  phone: string;
  annualBudget: number | null;
  currency: string;
  glCode: string;
  approvalLimit: number | null;
  allowAssetOwnership: boolean;
  requireAssignmentApproval: boolean;
  includeInCostAllocation: boolean;
  effectiveFrom: string;
  effectiveTo: string;
  createdOn: string;
  updatedOn: string;
}

export interface DepartmentFilters {
  search: string;
  businessUnitCode: string;
  status: '' | DepartmentStatus;
}
