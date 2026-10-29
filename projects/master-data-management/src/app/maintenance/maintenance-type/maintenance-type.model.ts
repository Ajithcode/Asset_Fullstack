export type MasterStatus = 'Active' | 'Inactive';

export interface MaintenanceTypeRecord {
  /** Mongo id from the API; empty until the record is first saved. */
  id: string;
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
