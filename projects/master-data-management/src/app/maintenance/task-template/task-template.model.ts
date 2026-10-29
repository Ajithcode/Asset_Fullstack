export interface TemplateTask {
  description: string;
  minutes: number;
  required: boolean;
}

export interface TaskTemplateRecord {
  /** Mongo id from the API; empty until the record is first saved. */
  id: string;
  code: string;
  name: string;
  // Maintenance type name from the Maintenance Type master
  maintenanceType: string;
  estimatedHours: number;
  description: string;
  status: 'Active' | 'Inactive';
  tasks: TemplateTask[];
  createdOn: string;
  updatedOn: string;
}

export interface TaskTemplateFilters {
  search: string;
  maintenanceType: string;
  status: '' | 'Active' | 'Inactive';
}
