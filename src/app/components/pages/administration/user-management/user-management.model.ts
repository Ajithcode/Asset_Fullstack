export type UserStatus = 'Active' | 'Pending' | 'Suspended';
export type Tone = 'green' | 'orange' | 'red' | 'pink' | 'blue' | 'purple' | 'grey';

/** A user account of the platform. Passwords are never stored or typed here; sign-in is set up by invitation. */
export interface AdminUser {
  /** Internal key; stays the same when the email or employee ID changes. */
  id: string;
  name: string;
  email: string;
  employeeId: string;
  department: string;
  jobTitle: string;
  phone: string;
  location: string;
  timeZone: string;
  /** Uploaded photo as a data URL; initials are shown without one. */
  photo: string | null;
  primaryRole: string;
  secondaryRoles: string[];
  /** 'All Locations' or one location name. */
  accessScope: string;
  mfa: boolean;
  status: UserStatus;
  suspendReason: string;
  /** ISO date-times. */
  lastLogin: string | null;
  created: string;
  invitedAt: string | null;
  modules: string[];
}

export interface UserRole {
  name: string;
  /** Shorter label for the table pill. */
  short: string;
  tone: Tone;
  modules: string[];
  /** Secondary-only roles can't be someone's primary role. */
  secondaryOnly?: boolean;
}
