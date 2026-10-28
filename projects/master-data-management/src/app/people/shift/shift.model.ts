export type ShiftStatus = 'Active' | 'Inactive';
export type ShiftType = 'Regular' | 'Flexible' | 'On Call' | 'Rotational';

export interface ShiftBreak {
  name: string;
  start: string;
  minutes: number;
}

export interface ShiftRecord {
  code: string;
  name: string;
  shiftType: ShiftType;
  // HH:mm; empty for shifts without fixed hours (Flexible, On Call)
  startTime: string;
  endTime: string;
  color: string;
  workingDays: string[];
  description: string;
  status: ShiftStatus;
  // Schedule
  effectiveFrom: string;
  graceMinutes: number;
  rotationPattern: string;
  breaks: ShiftBreak[];
  // Settings
  overtimeAllowed: boolean;
  nightAllowance: boolean;
  attendanceRequired: boolean;
  createdOn: string;
  updatedOn: string;
}

export interface ShiftFilters {
  search: string;
  shiftType: '' | ShiftType;
  status: '' | ShiftStatus;
}
