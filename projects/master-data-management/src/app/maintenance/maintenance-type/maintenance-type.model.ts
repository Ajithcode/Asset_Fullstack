export type MasterStatus = 'Active' | 'Inactive';

export interface MaintenanceTypeRecord {
  code: string;
  name: string;
  description: string;
  color: string;
  status: MasterStatus;
  // Settings
  planned: boolean;
  approvalRequired: boolean;
  countsAsDowntime: boolean;
  defaultPriority: string;
  createdOn: string;
  updatedOn: string;
}

export interface MaintenanceTypeFilters {
  search: string;
  status: '' | MasterStatus;
}
