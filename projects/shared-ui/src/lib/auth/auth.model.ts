/** Sign-up form data (DEMO MODE: kept in the browser only, never sent to a server). */
export interface RegisterData {
  fullName: string;
  userName: string;
  email: string;
  contactNo: string;
  password: string;
  /** roleId of one of the demo roles. */
  userRoleId: string;
}

/** A role offered on the sign-up page. */
export interface SignupRole {
  roleId: string;
  roleName: string;
  description: string;
}

export interface LoginData {
  userName: string;
  password: string;
}

/** The signed-in demo user. */
export interface AuthUser {
  userName: string;
  email: string;
  roleName: string;
}
