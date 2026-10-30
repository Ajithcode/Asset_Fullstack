import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { AuthUser, LoginData, RegisterData, SignupRole } from './auth.model';

// DEMO MODE: this is a static app, so sign-in and sign-up never call a backend.
// 'token' and 'userEmail' are read elsewhere (the auth interceptor, createdBy/updatedBy in services)
const TOKEN_KEY = 'token';
const EMAIL_KEY = 'userEmail';
const USER_KEY = 'authUser';
/** Accounts created on the demo sign-up page, so their role shows after signing in. */
const DEMO_ACCOUNTS_KEY = 'demoAccounts';

export const DEMO_ROLES: SignupRole[] = [
  { roleId: 'admin', roleName: 'Administrator', description: 'Full access to every module and setting.' },
  { roleId: 'manager', roleName: 'Asset Manager', description: 'Manages assets, maintenance and reports.' },
  { roleId: 'technician', roleName: 'Technician', description: 'Works on assigned maintenance and inspections.' },
  { roleId: 'viewer', roleName: 'Viewer', description: 'Read-only access to dashboards and reports.' },
];

@Injectable({ providedIn: 'root' })
export class AuthService {
  constructor(private router: Router) {}

  /** Signs in with any username/password and starts a demo session. */
  login(data: LoginData): AuthUser {
    const account = this.demoAccounts()[data.userName.toLowerCase()];
    const user: AuthUser = account ?? {
      userName: data.userName,
      email: data.userName.includes('@') ? data.userName : '',
      roleName: 'Administrator',
    };
    this.store(TOKEN_KEY, 'demo-token-' + Date.now());
    this.store(USER_KEY, JSON.stringify(user));
    this.setUserEmail(user.email || user.userName);
    return user;
  }

  /** Remembers the new account in this browser; it can then sign in with any password. */
  register(data: RegisterData): void {
    const accounts = this.demoAccounts();
    accounts[data.userName.toLowerCase()] = {
      userName: data.userName,
      email: data.email,
      roleName: DEMO_ROLES.find((r) => r.roleId === data.userRoleId)?.roleName ?? 'Administrator',
    };
    this.store(DEMO_ACCOUNTS_KEY, JSON.stringify(accounts));
  }

  logout(): void {
    this.clearSession();
    this.router.navigate(['/login']);
  }

  isLoggedIn(): boolean {
    return !!this.getToken();
  }

  getToken(): string | null {
    return this.read(TOKEN_KEY);
  }

  getUser(): AuthUser | null {
    return this.parse<AuthUser | null>(this.read(USER_KEY), null);
  }

  setUserEmail(email: string): void {
    if (email) this.store(EMAIL_KEY, email);
  }

  getUserEmail(): string | null {
    return this.read(EMAIL_KEY);
  }

  clearSession(): void {
    [TOKEN_KEY, EMAIL_KEY, USER_KEY].forEach((key) => this.remove(key));
  }

  private demoAccounts(): Record<string, AuthUser> {
    return this.parse<Record<string, AuthUser>>(this.read(DEMO_ACCOUNTS_KEY), {});
  }

  private parse<T>(json: string | null, fallback: T): T {
    try {
      return json ? (JSON.parse(json) as T) : fallback;
    } catch {
      return fallback;
    }
  }

  private read(key: string): string | null {
    return typeof window !== 'undefined' ? localStorage.getItem(key) : null;
  }

  private store(key: string, value: string): void {
    if (typeof window !== 'undefined') localStorage.setItem(key, value);
  }

  private remove(key: string): void {
    if (typeof window !== 'undefined') localStorage.removeItem(key);
  }
}
