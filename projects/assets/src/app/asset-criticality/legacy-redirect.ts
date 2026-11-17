import { inject } from '@angular/core';
import { CanMatchFn, Router } from '@angular/router';

/**
 * Keeps old links working: /assets/criticality?tag=GEN-00045 (Asset 360 tab bar, history, bookmarks) now opens
 * /assets/criticality/asset/GEN-00045; other query params are kept. Without ?tag= the landing page opens.
 */
export const legacyCriticalityRedirect: CanMatchFn = () => {
  const router = inject(Router);
  const query = router.getCurrentNavigation()?.extractedUrl.queryParams ?? {};
  const tag = query['tag'];
  if (typeof tag !== 'string' || !tag.trim()) return true;
  const { tag: _tag, ...rest } = query;
  return router.createUrlTree(['/assets/criticality/asset', tag.trim()], { queryParams: rest });
};
