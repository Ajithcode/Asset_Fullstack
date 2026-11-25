import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from 'shared-ui';

/** Where a signed-in user starts when they have not picked anything else. */
export const DEFAULT_LANDING = '/dashboard';
/** Preferences are saved per signed-in user (same key the Preferences dialog writes). */
const PREFS_KEY = 'a360.prefs';

export function prefsKey(email: string | null | undefined): string {
  return `${PREFS_KEY}:${(email ?? '').toLowerCase()}`;
}

/** The user's saved landing page (Account menu > Preferences), default Dashboard. */
export function landingFor(email: string | null | undefined): string {
  try {
    const landing = JSON.parse(localStorage.getItem(prefsKey(email)) ?? '{}').landing;
    return typeof landing === 'string' && landing.startsWith('/') ? landing : DEFAULT_LANDING;
  } catch {
    return DEFAULT_LANDING;
  }
}

/** "/" : signed-in users go to their landing page, everyone else to the sign-in page. */
export const rootRedirectGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return router.parseUrl(auth.isLoggedIn() ? landingFor(auth.getUserEmail()) : '/login');
};
