/**
 * Licence: the only place that lists which modules this installation is licensed for.
 * Names are the sidebar labels (same names as the role permission matrix). Every other module stays visible in the
 * sidebar with a lock icon, and its routes (sidebar click or direct URL) show the shared "Module not activated" page
 * without loading the module's remote bundle (see licence.guard.ts and app.routes.ts).
 */
export const LICENSED_MODULES: readonly string[] = [
  'Dashboard',
  'Assets',
  'Tracking & IoT',
  'Reports & Analytics',
  'Administration',
  'Master Data Management'
];

export function isModuleLicensed(module: string): boolean {
  return LICENSED_MODULES.includes(module);
}
