import { FailureCodeRecord, FailureSeverity } from './failure-code.model';

export const FAILURE_CATEGORIES = ['Electrical', 'Mechanical', 'Instrumentation', 'Hydraulic', 'Software', 'Structural', 'General'];
export const FAILURE_SEVERITIES: FailureSeverity[] = ['Critical', 'High', 'Medium', 'Low'];

export function blankFailureCode(): FailureCodeRecord {
  return {
    code: '',
    name: '',
    category: '',
    description: '',
    severity: 'Medium',
    status: 'Active',
    assetCategories: [],
    causes: [],
    actions: [],
    createdOn: '',
    updatedOn: ''
  };
}
