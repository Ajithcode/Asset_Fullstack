export type FailureSeverity = 'Critical' | 'High' | 'Medium' | 'Low';

export interface FailureCodeRecord {
  code: string;
  name: string;
  category: string;
  description: string;
  severity: FailureSeverity;
  status: 'Active' | 'Inactive';
  // Classification
  assetCategories: string[];
  causes: string[];
  actions: string[];
  createdOn: string;
  updatedOn: string;
}

export interface FailureCodeFilters {
  search: string;
  category: string;
  severity: '' | FailureSeverity;
}
