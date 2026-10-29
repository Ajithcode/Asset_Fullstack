export type LevelKind = 'Priority' | 'Severity';

export interface PriorityRecord {
  /** Mongo id from the API; empty until the record is first saved. */
  id: string;
  code: string;
  name: string;
  kind: LevelKind;
  color: string;
  // Target response, e.g. "4 hours"; severities describe the effect instead
  responseTime: string;
  description: string;
  status: 'Active' | 'Inactive';
  // Usage rules
  appliesTo: string[];
  escalateAfterHours: number | null;
  notifyManager: boolean;
  createdOn: string;
  updatedOn: string;
}

export interface PriorityFilters {
  search: string;
  kind: '' | LevelKind;
  status: '' | 'Active' | 'Inactive';
}
