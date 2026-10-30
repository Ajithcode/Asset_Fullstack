import { EmployeeRecord } from './employee.model';

export const NATIONALITIES = ['UAE', 'India', 'Pakistan', 'Philippines', 'Egypt', 'Jordan', 'United Kingdom', 'Spain', 'Brazil', 'Sri Lanka'];
export const EMPLOYEE_SITES = ['Dubai Factory', 'Abu Dhabi Office', 'Sharjah Warehouse', 'Riyadh Plant', 'Doha Office'];

export function blankEmployee(id: string): EmployeeRecord {
  return {
    id,
    name: '',
    photo: null,
    department: '',
    designation: '',
    email: '',
    phone: '',
    status: 'Active',
    employmentType: 'Permanent',
    joiningDate: new Date().toISOString().slice(0, 10),
    site: '',
    reportingManager: '',
    costCenter: '',
    alternatePhone: '',
    emergencyName: '',
    emergencyPhone: '',
    address: '',
    nationality: '',
    skills: [],
    certifications: [],
    accessRole: 'Viewer',
    canApprove: false,
    notifyEmail: true,
    notifySms: false,
    createdOn: '',
    updatedOn: ''
  };
}
