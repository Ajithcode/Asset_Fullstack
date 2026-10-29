export type ResponseType = 'Yes / No' | 'Pass / Fail' | 'Number' | 'Text' | 'Photo';

export interface ChecklistItem {
  question: string;
  response: ResponseType;
  required: boolean;
}

export interface ChecklistRecord {
  /** Mongo id from the API; empty until the record is first saved. */
  id: string;
  code: string;
  name: string;
  checklistType: string;
  category: string;
  description: string;
  status: 'Active' | 'Inactive';
  items: ChecklistItem[];
  createdOn: string;
  updatedOn: string;
}

export interface ChecklistFilters {
  search: string;
  checklistType: string;
  status: '' | 'Active' | 'Inactive';
}
