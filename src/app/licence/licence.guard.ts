import { CanMatchFn, Route } from '@angular/router';
import { isModuleLicensed } from './licence.config';
import { ModuleLocked } from './module-locked';

/** canMatch guard: the module's real route only matches when the module is licensed (so its bundle is never loaded otherwise). */
export function licenceGuard(module: string): CanMatchFn {
  return () => isModuleLicensed(module);
}

/** Marks a shell route as belonging to `module`: it gets the licence guard, and a locked fallback is added for it. */
export function licensed(module: string, route: Route): Route {
  return { ...route, canMatch: [...(route.canMatch ?? []), licenceGuard(module)], data: { ...route.data, licenceModule: module } };
}

/**
 * Fallback routes for the unlicensed modules among `routes`: the module path and every page under it (deep links)
 * show the shared ModuleLocked page. Append after the real routes.
 */
export function lockedModuleRoutes(routes: Route[]): Route[] {
  const seen = new Set<string>();
  const result: Route[] = [];
  for (const r of routes) {
    const module = r.data?.['licenceModule'] as string | undefined;
    const path = r.path?.split('/')[0];
    if (!module || !path || isModuleLicensed(module) || seen.has(path)) continue;
    seen.add(path);
    result.push({ path, children: [{ path: '**', component: ModuleLocked, data: { licenceModule: module } }] });
  }
  return result;
}
