import { PersonStatus } from '../people.data';

export type EmployeeStatus = Exclude<PersonStatus, 'On Duty'>;

export interface Certification {
  name: string;
  // YYYY-MM-DD; empty when it doesn't expire
  expiry: string;
}

export interface EmployeeRecord {
  /** Mongo id from the API; empty until the record is first saved. `id` is the person's own number. */
  recordId: string;
  id: string;
  name: string;
  photo: string | null;
  department: string;
  designation: string;
  email: string;
  phone: string;
  status: EmployeeStatus;
  // Employment
  employmentType: string;
  joiningDate: string;
  site: string;
  // Employee ID of the manager
  reportingManager: string;
  costCenter: string;
  // Contact
  alternatePhone: string;
  emergencyName: string;
  emergencyPhone: string;
  address: string;
  nationality: string;
  // Skills & certification (skills by name)
  skills: string[];
  certifications: Certification[];
  // Access & settings
  accessRole: string;
  canApprove: boolean;
  notifyEmail: boolean;
  notifySms: boolean;
  createdOn: string;
  updatedOn: string;
}

export interface EmployeeFilters {
  search: string;
  department: string;
  status: '' | EmployeeStatus;
}
