export interface WorkTypeRecord {
  /** Mongo id from the API; empty until the record is first saved. */
  id: string;
  code: string;
  name: string;
  // Maintenance type name from the Maintenance Type master
  maintenanceType: string;
  description: string;
  durationHours: number;
  status: 'Active' | 'Inactive';
  // Additional details
  defaultPriority: string;
  requiredSkill: string;
  checklistCode: string;
  permitRequired: boolean;
  createdOn: string;
  updatedOn: string;
}

export interface WorkTypeFilters {
  search: string;
  maintenanceType: string;
  status: '' | 'Active' | 'Inactive';
}
