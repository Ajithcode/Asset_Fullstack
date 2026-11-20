/**
 * API endpoints for the micro-frontends. Re-exports the shell's environment so there is one place to
 * change them; the shell and every remote read the same values.
 */
// "@a360/app-environment" is src/environments/environment.ts in the apps (root tsconfig paths) and library-environment.ts
// in the standalone library build (tsconfig.lib.json), which cannot include files outside projects/shared-ui.
export { environment } from '@a360/app-environment';
