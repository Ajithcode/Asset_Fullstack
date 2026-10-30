export interface TemplateTask {
  description: string;
  minutes: number;
  required: boolean;
}

export interface TaskTemplateRecord {
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
