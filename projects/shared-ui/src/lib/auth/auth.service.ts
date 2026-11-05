import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, map } from 'rxjs';
import { environment } from '../environment/environment';
import { AuthTokenResponse, AuthUser, LoginData, LoginResponse, RegisterData, RegisterResponse, SignupRole } from './auth.model';

// 'token' and 'userEmail' are read elsewhere (the auth interceptor, createdBy/updatedBy in services)
const TOKEN_KEY = 'token';
const REFRESH_KEY = 'refreshToken';
const EMAIL_KEY = 'userEmail';
const USER_KEY = 'authUser';

/**
 * Sign-in against the UserAccount service (through the API gateway):
 * 1. login(): username + password → the second step (authenticator app code, or emailed code)
 * 2. verifyTotp() / verifyOtp(): the 6-digit code → access + refresh tokens, kept in this browser
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly api = `${environment.authApiUrl}api/auth`;

  /** Step 1: checks the password; the response says which second step comes next. */
  login(data: LoginData): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.api}/login`, { userName: data.userName, password: data.password });
  }

  /** Step 2 with the authenticator app (Google Authenticator etc.); starts the session. */
  verifyTotp(userName: string, loginToken: string, code: string): Observable<AuthUser> {
    return this.http
      .post<AuthTokenResponse>(`${this.api}/verify-totp`, { userName, loginToken, code })
      .pipe(map((res) => this.startSession(res)));
  }

  /** Step 2 with an emailed code; starts the session. */
  verifyOtp(userName: string, otp: string): Observable<AuthUser> {
    return this.http.post<AuthTokenResponse>(`${this.api}/verify-otp`, { userName, otp }).pipe(map((res) => this.startSession(res)));
  }

  /** Roles a new account can pick on the sign-up page. */
  getSignupRoles(): Observable<SignupRole[]> {
    return this.http.get<SignupRole[]>(`${this.api}/roles`);
  }

  register(data: RegisterData): Observable<RegisterResponse> {
    return this.http.post<RegisterResponse>(`${this.api}/register`, data);
  }

  /** Ends the session on the server (revokes the refresh token) and in this browser. */
  logout(): void {
    const refreshToken = this.read(REFRESH_KEY);
    if (refreshToken) this.http.post(`${this.api}/logout`, { refreshToken }).subscribe({ error: () => undefined });
    this.clearSession();
    this.router.navigate(['/login']);
  }

  /** True while this browser holds an unexpired access token from the server (old demo tokens don't count). */
  isLoggedIn(): boolean {
    const token = this.getToken();
    if (!token) return false;
    const expiresAt = this.tokenExpiry(token);
    if (expiresAt === null || expiresAt <= Date.now()) {
      this.clearSession();
      return false;
    }
    return true;
  }

  /** Expiry of a JWT in ms (its `exp` claim), or null when the token is not a JWT. */
  private tokenExpiry(token: string): number | null {
    try {
      const payload = token.split('.')[1];
      if (!payload) return null;
      const json = JSON.parse(atob(payload.replace(/-/g, '+').replace(/_/g, '/')));
      return typeof json.exp === 'number' ? json.exp * 1000 : null;
    } catch {
      return null;
    }
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
    [TOKEN_KEY, REFRESH_KEY, EMAIL_KEY, USER_KEY].forEach((key) => this.remove(key));
    // The role-based access cached by AccessService belongs to this session too
    if (typeof window !== 'undefined') {
      try {
        sessionStorage.removeItem('myAccess');
      } catch {
        // storage unavailable
      }
    }
  }

  /** Readable message for a failed auth call: the server's message, first validation error, or the status. */
  errorMessage(err: unknown, fallback = 'Something went wrong. Please try again.'): string {
    if (!(err instanceof HttpErrorResponse)) return fallback;
    if (err.status === 0) return 'Cannot reach the server. Check that the backend is running.';
    const body = err.error;
    const firstError = body?.errors && Object.values(body.errors as Record<string, string[]>)[0]?.[0];
    return body?.message || firstError || body?.title || fallback;
  }

  private startSession(res: AuthTokenResponse): AuthUser {
    const u = res.user;
    const user: AuthUser = {
      userName: u.userName, email: u.email, roleName: u.roleName, id: u.id, userId: u.userId, userRoleId: u.userRoleId, clientId: u.clientId, tenantId: u.tenantId,
    };
    this.store(TOKEN_KEY, res.accessToken);
    this.store(REFRESH_KEY, res.refreshToken);
    this.store(USER_KEY, JSON.stringify(user));
    this.setUserEmail(user.email || user.userName);
    return user;
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
