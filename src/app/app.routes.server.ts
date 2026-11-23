import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  {
    path: 'login',
    renderMode: RenderMode.Client   // ← login must run in browser
  },
  {
    path: 'signup',
    renderMode: RenderMode.Client   // ← signup must run in browser
  },
  {
    path: 'accept-invite',
    renderMode: RenderMode.Client   // ← invitation page runs in the browser
  },
  {
    path: 'locating',
    renderMode: RenderMode.Client   // ← locating uses Leaflet which needs window
  },
  {
    path: '**',
    renderMode: RenderMode.Server   // ← other pages use SSR normally
  }
];