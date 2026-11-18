import { WorkTypeRecord } from './work-type.model';

export function blankWorkType(): WorkTypeRecord {
  return {
    code: '',
    name: '',
    maintenanceType: '',
    description: '',
    durationHours: 1,
    status: 'Active',
    defaultPriority: 'Medium',
    requiredSkill: '',
    checklistCode: '',
    permitRequired: false,
    createdOn: '',
    updatedOn: ''
  };
}
