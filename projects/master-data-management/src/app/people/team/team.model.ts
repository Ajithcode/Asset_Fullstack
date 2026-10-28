export type TeamStatus = 'Active' | 'On Leave' | 'Inactive';

export interface TeamRecord {
  code: string;
  name: string;
  department: string;
  // Employee or technician ID; always one of the members
  leaderId: string;
  description: string;
  status: TeamStatus;
  // Employee (EMP…) and technician (TECH…) IDs
  members: string[];
  shiftCode: string;
  responsibilities: string[];
  notifyLeader: boolean;
  autoAssign: boolean;
  createdOn: string;
  updatedOn: string;
}

export interface TeamFilters {
  search: string;
  department: string;
  status: '' | TeamStatus;
}
