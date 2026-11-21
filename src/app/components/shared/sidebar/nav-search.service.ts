import { Injectable, signal } from '@angular/core';

/** A page the topbar search can open (from the sidebar menu the signed-in user can see). */
export interface NavPage {
  label: string;
  path: string;
  /** Where it sits in the menu, e.g. "Tracking & IoT". */
  section: string;
  /** The module is not part of the licence (its page shows "Module not activated"). */
  locked: boolean;
}

/**
 * The sidebar publishes its current menu (already filtered by role access and licence) here, and the topbar search
 * reads it, so search only finds pages the user can open from the sidebar.
 */
@Injectable({ providedIn: 'root' })
export class NavSearchService {
  readonly pages = signal<NavPage[]>([]);
}
