import { TaskTemplateRecord } from './task-template.model';

export function blankTaskTemplate(): TaskTemplateRecord {
  return { code: '', name: '', maintenanceType: '', estimatedHours: 1, description: '', status: 'Active', tasks: [], createdOn: '', updatedOn: '' };
}
