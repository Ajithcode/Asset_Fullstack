export type UserStatus = 'Active' | 'Pending' | 'Suspended';
export type Tone = 'green' | 'orange' | 'red' | 'pink' | 'blue' | 'purple' | 'grey';

/** A user account of the platform. Passwords are never stored or typed here; sign-in is set up by invitation. */
export interface AdminUser {
  /** Internal key; stays the same when the email or employee ID changes. */
  id: string;
  name: string;
  /** Sign-in name (login); new users default to their email address. */
  username: string;
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
  /** Sign-in account id in the UserAccount service (password reset); null when the user has no password yet. */
  loginAccountId?: string | null;
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
