import { Routes } from '@angular/router';
import { loadRemoteModule } from '@angular-architects/native-federation';
import { ModulePlaceholder } from 'shared-ui';
import { Login } from './components/pages/login/login/login';
import { Signup } from './components/pages/signup/signup/signup';
import { SectionUnavailable } from './section-unavailable/section-unavailable';
import { Events } from './components/pages/events/events/events';
import { Layout } from './components/shared/layout/layout';

import { authGuard } from './components/guard/auth-guard';
import { moduleGuard } from './components/guard/module-guard';
import { NoAccess } from './section-unavailable/no-access';
import { ProcessAutomation } from './components/pages/process-automation/process-automation/process-automation';
import { ProcessAutomationAdd } from './components/pages/process-automation/process-automation-add/process-automation-add';
import { licensed, lockedModuleRoutes } from './licence/licence.guard';

function remoteRoutes(remoteName: string) {
  return () =>
    loadRemoteModule({ remoteName, exposedModule: './routes' })
      .then((m) => m.routes)
      .catch((err) => {
        console.error(`Failed to load remote "${remoteName}":`, err);
        // '**' so deep links into the module (e.g. /assets/list) show the message too
        return [{ path: '**', component: SectionUnavailable }];
      });
}

/** Shell routes plus the "Module not activated" fallbacks for unlicensed modules (after the real routes). */
function withLicenceLock(children: Routes): Routes {
  return [...children, ...lockedModuleRoutes(children)];
}

export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: 'login', component: Login },
  { path: 'signup', component: Signup },
  {
    path: '',
    component: Layout,
    // Every page needs a signed-in user (see authGuard)
    canActivate: [authGuard],
    // Licence (licence/licence.config.ts): unlicensed modules' routes don't match, the locked fallbacks below show the lock page
    children: withLicenceLock([
      // Each main sidebar menu is its own micro-frontend (projects/<name>); its pages are routed inside it
      licensed('Dashboard', { path: 'dashboard', loadChildren: remoteRoutes('dashboard'), canActivate: [moduleGuard('Dashboard')], canActivateChild: [moduleGuard('Dashboard')] }),
      licensed('Assets', { path: 'assets', loadChildren: remoteRoutes('assets'), canActivate: [moduleGuard('Assets')], canActivateChild: [moduleGuard('Assets')] }),
      // previous floor-plan Locating module (federated remote), kept reachable
      licensed('Tracking & IoT', { path: 'tracking-iot/locating-classic', loadChildren: remoteRoutes('locating'), canActivate: [moduleGuard('Tracking & IoT')], canActivateChild: [moduleGuard('Tracking & IoT')] }),
      licensed('Tracking & IoT', { path: 'tracking-iot', loadChildren: remoteRoutes('trackingIot'), canActivate: [moduleGuard('Tracking & IoT')], canActivateChild: [moduleGuard('Tracking & IoT')] }),
      licensed('WIP / Operations', { path: 'wip-operations', loadChildren: remoteRoutes('wipOperations'), canActivate: [moduleGuard('WIP / Operations')], canActivateChild: [moduleGuard('WIP / Operations')] }),
      licensed('Inspection', { path: 'inspection', loadChildren: remoteRoutes('inspection'), canActivate: [moduleGuard('Inspection')], canActivateChild: [moduleGuard('Inspection')] }),
      licensed('Maintenance', { path: 'maintenance', loadChildren: remoteRoutes('maintenance'), canActivate: [moduleGuard('Maintenance')], canActivateChild: [moduleGuard('Maintenance')] }),
      licensed('Reports & Analytics', { path: 'reports-analytics', loadChildren: remoteRoutes('reports'), canActivate: [moduleGuard('Reports & Analytics')], canActivateChild: [moduleGuard('Reports & Analytics')] }),
      licensed('Administration', { path: 'administration', loadChildren: remoteRoutes('administration'), canActivate: [moduleGuard('Administration')], canActivateChild: [moduleGuard('Administration')] }),
      licensed('Master Data Management', { path: 'master-data-management', loadChildren: remoteRoutes('masterDataManagement'), canActivate: [moduleGuard('Master Data Management')], canActivateChild: [moduleGuard('Master Data Management')] }),
      // New UI modules: placeholder pages until their screens are built
      licensed('Movement & Custody', { path: 'movement-custody', component: ModulePlaceholder, data: { title: 'Movement & Custody' }, canActivate: [moduleGuard('Movement & Custody')], canActivateChild: [moduleGuard('Movement & Custody')] }),
      licensed('Inventory', { path: 'inventory', component: ModulePlaceholder, data: { title: 'Inventory' }, canActivate: [moduleGuard('Inventory')], canActivateChild: [moduleGuard('Inventory')] }),
      licensed('Audit & Compliance', { path: 'audit-compliance', component: ModulePlaceholder, data: { title: 'Audit & Compliance' }, canActivate: [moduleGuard('Audit & Compliance')], canActivateChild: [moduleGuard('Audit & Compliance')] }),
      licensed('Contracts & Warranty', { path: 'contracts-warranty', component: ModulePlaceholder, data: { title: 'Contracts & Warranty' }, canActivate: [moduleGuard('Contracts & Warranty')], canActivateChild: [moduleGuard('Contracts & Warranty')] }),
      licensed('Financial', { path: 'financial', component: ModulePlaceholder, data: { title: 'Financial' }, canActivate: [moduleGuard('Financial')], canActivateChild: [moduleGuard('Financial')] }),
      { path: 'locating', redirectTo: 'tracking-iot/real-time-location', pathMatch: 'full' },
      // Reports moved into the Reports & Analytics remote; old links keep working
      { path: 'report', redirectTo: 'reports-analytics/standard-reports', pathMatch: 'full' },
      { path: 'report/create', redirectTo: 'reports-analytics/create', pathMatch: 'full' },
      { path: 'report/view/:id', redirectTo: 'reports-analytics/view/:id', pathMatch: 'full' },
      // Role-based access: sections the user's roles don't allow redirect here when nothing else is open to them
      { path: 'no-access', component: NoAccess },
      licensed('Events', { path: 'events', component: Events }),
      licensed('Process Automation', { path: 'process-automation', component: ProcessAutomation }),
      licensed('Process Automation', { path: 'process-automation/add', component: ProcessAutomationAdd })
    ])
  }
];
