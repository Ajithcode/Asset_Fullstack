import { Certification } from '../employee/employee.model';
import { PersonStatus } from '../people.data';

export type SkillLevel = 'Beginner' | 'Intermediate' | 'Advanced' | 'Expert';

export interface TechnicianRecord {
  id: string;
  name: string;
  photo: string | null;
  // Skill names from the Skill master
  primarySkill: string;
  email: string;
  phone: string;
  status: PersonStatus;
  // Skills & certification
  skillLevel: SkillLevel;
  secondarySkills: string[];
  certifications: Certification[];
  // Employment
  employeeId: string;
  source: 'In-house' | 'Contractor';
  employmentType: string;
  joiningDate: string;
  hourlyRate: number | null;
  // Contact
  emergencyName: string;
  emergencyPhone: string;
  address: string;
  // Settings
  availableForDispatch: boolean;
  mobileAppAccess: boolean;
  maxOpenWorkOrders: number;
  createdOn: string;
  updatedOn: string;
}

export interface TechnicianFilters {
  search: string;
  status: '' | PersonStatus;
  skill: string;
}
