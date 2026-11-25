import { Component, ElementRef, HostListener, Output, EventEmitter, inject, Input } from '@angular/core';
import { NavPage, NavSearchService } from '../sidebar/nav-search.service';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  LucideAngularModule,
  Check,
  ChevronRight,
  CircleQuestionMark,
  ExternalLink,
  Eye,
  EyeOff,
  FileText,
  Globe,
  Landmark,
  Lock,
  LogOut,
  Settings,
  Sparkles,
  User,
  Users,
  X,
} from 'lucide-angular';
import { Router, NavigationEnd, RouterModule } from '@angular/router';
import { filter, forkJoin, map } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { AccessService, AlertFeedService, AuthService, DesktopAlertsService, FeedAlert, pushToast } from 'shared-ui';
import { prefsKey } from '../landing-page';
import { environment } from '../../../../environments/environment';
import { AlertPopups } from '../alert-popups/alert-popups';

type ProfileSection = '' | 'language' | 'site' | 'help';
type ProfileDialog = '' | 'profile' | 'preferences' | 'whatsNew' | 'docs' | 'password';

interface Profile {
  displayName: string;
  phone: string;
}

interface Preferences {
  landing: string;
  timeFormat: '12h' | '24h';
  notifySound: boolean;
}

const PROFILE_KEY = 'a360.profile';
const SITE_KEY = 'a360.site';
const WHATS_NEW_KEY = 'a360.whatsNewSeen';
/** Bump when the release notes below change; the red dot shows until the user opens them. */
const WHATS_NEW_VERSION = '2026.10';

function readJson<T>(key: string, fallback: T): T {
  try {
    const v = localStorage.getItem(key);
    return v ? { ...fallback, ...JSON.parse(v) } : fallback;
  } catch {
    return fallback;
  }
}

function writeValue(key: string, value: unknown): void {
  try {
    localStorage.setItem(key, typeof value === 'string' ? value : JSON.stringify(value));
  } catch {}
}

interface BreadcrumbItem {
  label: string;
  path: string;
}

@Component({
  selector: 'app-topbar',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule, AlertPopups],
  templateUrl: './topbar.html',
  styleUrls: ['./topbar.scss']
})
export class Topbar {
  @Output() toggleSidebar = new EventEmitter<void>();
  /** Sidebar state, for the menu button's label. */
  @Input() sidebarCollapsed = false;

  // ---- page search (Ctrl+K): the pages of the sidebar menu
  private readonly navSearch = inject(NavSearchService);
  searchQuery = '';
  searchOpen = false;
  searchIndex = 0;

  get searchResults(): NavPage[] {
    const q = this.searchQuery.trim().toLowerCase();
    if (!q) return [];
    const words = q.split(/\s+/);
    return this.navSearch
      .pages()
      .map((p) => ({ p, text: `${p.label} ${p.section}`.toLowerCase() }))
      .filter(({ text }) => words.every((w) => text.includes(w)))
      // Label matches first, then shorter labels
      .sort((a, b) => Number(!a.p.label.toLowerCase().includes(q)) - Number(!b.p.label.toLowerCase().includes(q)) || a.p.label.length - b.p.label.length)
      .slice(0, 8)
      .map(({ p }) => p);
  }

  onSearchInput(): void {
    this.searchOpen = true;
    this.searchIndex = 0;
  }

  onSearchKey(e: KeyboardEvent): void {
    const n = this.searchResults.length;
    if (e.key === 'ArrowDown' && n) {
      e.preventDefault();
      this.searchIndex = (this.searchIndex + 1) % n;
    } else if (e.key === 'ArrowUp' && n) {
      e.preventDefault();
      this.searchIndex = (this.searchIndex - 1 + n) % n;
    } else if (e.key === 'Enter' && n) {
      e.preventDefault();
      this.openPage(this.searchResults[this.searchIndex]);
    } else if (e.key === 'Escape') {
      this.closeSearch();
      (e.target as HTMLInputElement).blur();
    }
  }

  openPage(p: NavPage): void {
    this.closeSearch();
    void this.router.navigateByUrl(p.path);
  }

  closeSearch(): void {
    this.searchOpen = false;
    this.searchQuery = '';
    this.searchIndex = 0;
  }

  /** Ctrl+K / ⌘K focuses the search from anywhere. */
  @HostListener('document:keydown', ['$event'])
  onGlobalKey(e: KeyboardEvent): void {
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
      e.preventDefault();
      (this.elementRef.nativeElement.querySelector('.topbar-search input') as HTMLInputElement | null)?.focus();
      this.searchOpen = true;
    }
  }
  breadcrumbs: BreadcrumbItem[] = [];
  isDropdownOpen = false;
  flyoutPosition: { top: number; left: number } | null = null;

  readonly icons = { Check, ChevronRight, CircleQuestionMark, ExternalLink, Eye, EyeOff, FileText, Globe, Landmark, Lock, LogOut, Settings, Sparkles, User, Users, X };
  /** Switch Tenant / Site: the real lists from Master Data (Organization, Site); loaded when the row is first opened. */
  readonly allSites = 'All Sites';
  organizations: string[] = [];
  sites: string[] = [];
  sitesState: 'idle' | 'loading' | 'ready' | 'error' = 'idle';
  readonly languages = [{ code: 'en', label: 'English', ready: true }, { code: 'ar', label: 'العربية (Arabic)', ready: false }];
  readonly landingPages = [
    { path: '/dashboard', label: 'Dashboard' },
    { path: '/assets/list', label: 'Asset List' },
    { path: '/tracking-iot/analytics-insights', label: 'IoT Analytics & Insights' },
    { path: '/tracking-iot/tag-management', label: 'Tag Management' },
    { path: '/wip-operations/job-orders', label: 'WIP Job Orders' },
  ];
  readonly docs = [
    { title: 'Assets', text: 'Register assets, follow their location, maintenance, health and documents.', path: '/assets/list' },
    { title: 'Tracking & IoT', text: 'Analytics, tag identifiers and real-time location of tagged assets.', path: '/tracking-iot/analytics-insights' },
    { title: 'WIP / Operations', text: 'Job templates and orders, routing, resources, SLAs and output.', path: '/wip-operations/job-orders' },
    { title: 'Administration', text: 'Administration settings.', path: '/administration' },
  ];
  readonly whatsNew = [
    { date: 'Oct 2026', title: 'Tag IDs in Tracking & IoT', text: 'Register, assign, reassign and bulk-import barcode, QR, RFID and BLE identifiers.' },
    { date: 'Oct 2026', title: 'IoT Analytics & Insights', text: 'Connectivity, movement heatmap, telemetry trends, alerts and site summary with saved views.' },
    { date: 'Oct 2026', title: 'Asset maintenance workspace', text: 'Overview, preventive plans, service requests, history, spare parts and costs for each asset.' },
    { date: 'Oct 2026', title: 'New look', text: 'Purple gradient sidebar and header, glass dashboard tabs and cards that fade in as you scroll.' },
  ];

  section: ProfileSection = '';
  dialog: ProfileDialog = '';
  private readonly access = inject(AccessService);
  // Saved per signed-in user, so one person's display name never shows for the next person on this browser
  private readonly profileKey = `${PROFILE_KEY}:${(inject(AuthService).getUserEmail() ?? '').toLowerCase()}`;
  profile: Profile = readJson<Profile>(this.profileKey, { displayName: '', phone: '' });
  profileForm: Profile = { ...this.profile };
  private readonly prefsStorageKey = prefsKey(inject(AuthService).getUserEmail());
  prefs: Preferences = readJson<Preferences>(this.prefsStorageKey, { landing: '/dashboard', timeFormat: '12h', notifySound: true });
  prefsForm: Preferences = { ...this.prefs };
  private readonly siteStorageKey = `${SITE_KEY}:${(inject(AuthService).getUserEmail() ?? '').toLowerCase()}`;
  site = this.readSite();
  private readonly http = inject(HttpClient);
  whatsNewSeen = this.readSeen();
  pw = { current: '', next: '', confirm: '', show: false };
  pwError = '';
  pwSaving = false;
  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  isNotificationsOpen = false;
  notificationsFlyoutPosition: { top: number; left: number } | null = null;

  constructor(
    private router: Router,
    private elementRef: ElementRef,
    private auth: AuthService
  ) {
    this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe(() => this.updateBreadcrumbs());

    // Desktop notifications for IoT alerts: service worker + this browser's push subscription; live alerts go to the bell
    this.desktopAlerts.soundEnabled = this.prefs.notifySound;
    void this.desktopAlerts.init();
    // The bell: unacknowledged alerts from the Tracking alert feed (polled every 10 s)
    this.alertFeed.start();
  }

  readonly desktopAlerts = inject(DesktopAlertsService);
  readonly alertFeed = inject(AlertFeedService);

  enableDesktopAlerts(): void {
    void this.desktopAlerts.enable().then((ok) =>
      this.showToast(ok ? 'Desktop notifications are on for this browser.' : this.desktopAlerts.state === 'denied' ? 'Notifications are blocked for this site in the browser settings.' : 'Could not turn on desktop notifications.')
    );
  }

  get unreadNotificationCount(): number {
    return this.alertFeed.unacknowledged;
  }

  get bellAlerts(): FeedAlert[] {
    return this.alertFeed.latest;
  }

  sevOf(a: FeedAlert): string {
    return /critical|high/i.test(a.severity) ? 'error' : /medium|warn/i.test(a.severity) ? 'warning' : 'info';
  }

  alertTime(a: FeedAlert): string {
    return new Date(a.raisedAt).toLocaleString([], { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' });
  }

  async acknowledgeAlert(a: FeedAlert): Promise<void> {
    if (!(await this.alertFeed.acknowledge(a))) this.showToast('Could not acknowledge the alert. Try again.');
  }

  async acknowledgeAllAlerts(): Promise<void> {
    if (!(await this.alertFeed.acknowledgeAll())) this.showToast('Could not acknowledge the alerts. Try again.');
  }

  viewAllAlerts(): void {
    this.closeNotifications();
    void this.router.navigate(['/tracking-iot/geofence-alerts'], { queryParams: { tab: 'alerts' } });
  }

  toggleNotifications(triggerEl: EventTarget | null): void {
    this.isNotificationsOpen = !this.isNotificationsOpen;
    if (this.isNotificationsOpen && triggerEl instanceof HTMLElement) {
      const rect = triggerEl.getBoundingClientRect();
      this.notificationsFlyoutPosition = {
        top: rect.bottom + window.scrollY + 4,
        left: rect.right - 340 + window.scrollX
      };
    } else {
      this.notificationsFlyoutPosition = null;
    }
  }


  /** Name shown in the header chip: the name you set in My Profile, else the name from User Management, else the login name. */
  get displayName(): string {
    const name = this.profile.displayName.trim() || this.access.access()?.displayName || this.auth.getUser()?.userName || '';
    return name ? name.charAt(0).toUpperCase() + name.slice(1) : this.displayEmail;
  }

  get displayEmail(): string {
    return this.access.access()?.email || this.auth.getUserEmail() || environment.supportEmail;
  }

  /** The user's primary role from User Management, else the sign-in role. */
  get roleName(): string {
    return this.access.access()?.roleName || this.auth.getUser()?.roleName || 'User';
  }

  /** Two letters for the avatar, from the signed-in user's name. */
  get initials(): string {
    const name = this.profile.displayName.trim() || this.access.access()?.displayName || this.auth.getUser()?.userName || this.displayEmail.split('@')[0];
    const parts = name.split(/[\s._-]+/).filter(Boolean);
    const letters = parts.length > 1 ? parts[0][0] + parts[1][0] : name.slice(0, 2);
    return letters.toUpperCase() || 'AD';
  }

  toggleMenu(): void {
    this.toggleSidebar.emit();
  }

  toggleAdminDropdown(triggerEl: EventTarget | null): void {
    this.isDropdownOpen = !this.isDropdownOpen;
    this.section = '';
    if (this.isDropdownOpen) this.closeNotifications();
    if (this.isDropdownOpen && triggerEl instanceof HTMLElement) {
      const rect = triggerEl.getBoundingClientRect();
      // right edge lines up with the profile chip
      const width = Math.min(300, window.innerWidth - 16);
      this.flyoutPosition = {
        top: rect.bottom + 8,
        left: Math.max(8, rect.right - width)
      };
    } else {
      this.flyoutPosition = null;
    }
  }

  private closeProfileMenu(): void {
    this.isDropdownOpen = false;
    this.flyoutPosition = null;
    this.section = '';
  }

  private closeNotifications(): void {
    this.isNotificationsOpen = false;
    this.notificationsFlyoutPosition = null;
  }

  toggleSection(s: ProfileSection): void {
    this.section = this.section === s ? '' : s;
    if (this.section === 'site') this.loadSites();
  }

  retrySites(): void {
    this.sitesState = 'idle';
    this.loadSites();
  }

  /** Master Data > Organization / Site (active, not deleted). Failures show a message instead of made-up names. */
  private loadSites(): void {
    if (this.sitesState === 'loading' || this.sitesState === 'ready') return;
    this.sitesState = 'loading';
    type Row = { organizationName?: string; siteName?: string; status?: string | null; isDeleted?: boolean };
    const base = `${environment.masterDataApiUrl}api/master-data`;
    const names = (rows: Row[], key: 'organizationName' | 'siteName') =>
      [...new Set(rows.filter((r) => r.isDeleted !== true && !/^(inactive|disabled|deleted)$/i.test(r.status ?? '')).map((r) => (r[key] ?? '').trim()).filter(Boolean))].sort((a, b) => a.localeCompare(b));
    const list = (resource: string) => this.http.get<Row[] | { items?: Row[] }>(`${base}/${resource}`).pipe(map((res) => (Array.isArray(res) ? res : res?.items ?? [])));
    forkJoin({ orgs: list('organizations'), sites: list('sites') }).subscribe({
      next: ({ orgs, sites }) => {
        this.organizations = names(orgs, 'organizationName');
        this.sites = names(sites, 'siteName');
        this.sitesState = 'ready';
      },
      error: () => (this.sitesState = 'error'),
    });
  }

  open(d: ProfileDialog): void {
    this.closeProfileMenu();
    this.dialog = d;
    this.pwError = '';
    if (d === 'profile') this.profileForm = { ...this.profile };
    if (d === 'preferences') this.prefsForm = { ...this.prefs };
    if (d === 'password') this.pw = { current: '', next: '', confirm: '', show: false };
    if (d === 'whatsNew') {
      this.whatsNewSeen = true;
      writeValue(WHATS_NEW_KEY, WHATS_NEW_VERSION);
    }
  }

  closeDialog(): void {
    this.dialog = '';
  }

  saveProfile(): void {
    this.profile = { displayName: this.profileForm.displayName.trim(), phone: this.profileForm.phone.trim() };
    writeValue(this.profileKey, this.profile);
    this.dialog = '';
    this.showToast('Profile updated.');
  }

  savePrefs(): void {
    this.prefs = { ...this.prefsForm };
    writeValue(this.prefsStorageKey, this.prefs);
    this.desktopAlerts.soundEnabled = this.prefs.notifySound;
    this.dialog = '';
    this.showToast('Preferences saved.');
  }

  get landingLabel(): string {
    return this.landingPages.find((p) => p.path === this.prefs.landing)?.label ?? 'Dashboard';
  }

  setLanguage(code: string): void {
    const lang = this.languages.find((l) => l.code === code);
    if (!lang?.ready) return;
    this.section = '';
    this.showToast(`Language set to ${lang.label}.`);
  }

  /** The chosen site is kept per user as the current site shown in the menu. No module filters its data by it yet. */
  setSite(site: string): void {
    this.site = site;
    writeValue(this.siteStorageKey, site);
    this.section = '';
    this.showToast(`Switched to ${site}.`);
  }

  private readSite(): string {
    try {
      return localStorage.getItem(this.siteStorageKey) || this.allSites;
    } catch {
      return this.allSites;
    }
  }

  private readSeen(): boolean {
    try {
      return localStorage.getItem(WHATS_NEW_KEY) === WHATS_NEW_VERSION;
    } catch {
      return false;
    }
  }

  goTo(path: string): void {
    this.closeProfileMenu();
    this.dialog = '';
    this.router.navigateByUrl(path);
  }

  get supportEmail(): string {
    return environment.supportEmail;
  }

  get supportMail(): string {
    return `mailto:${environment.supportEmail}?subject=${encodeURIComponent('Asset360 support request')}`;
  }

  get reportMail(): string {
    const body = `Page: ${this.router.url}\nUser: ${this.displayEmail}\n\nWhat happened:\n`;
    return `mailto:${environment.supportEmail}?subject=${encodeURIComponent('Asset360 problem report')}&body=${encodeURIComponent(body)}`;
  }

  get pwChecks(): { label: string; ok: boolean }[] {
    const v = this.pw.next;
    return [
      // Same rule as the server (UserAccount PasswordPolicy)
      { label: 'At least 8 characters', ok: v.length >= 8 },
      { label: 'At least one letter', ok: /\p{L}/u.test(v) },
      { label: 'At least one number', ok: /\d/.test(v) },
    ];
  }

  savePassword(): void {
    if (!this.pw.current) return void (this.pwError = 'Enter your current password.');
    if (this.pwChecks.some((c) => !c.ok)) return void (this.pwError = 'The new password does not meet the rules below.');
    if (this.pw.next === this.pw.current) return void (this.pwError = 'Choose a password different from the current one.');
    if (this.pw.next !== this.pw.confirm) return void (this.pwError = 'The new passwords do not match.');
    if (this.pwSaving) return;
    this.pwSaving = true;
    this.pwError = '';
    this.auth.changePassword(this.pw.current, this.pw.next).subscribe({
      next: (res) => {
        this.pwSaving = false;
        this.dialog = '';
        this.pw = { current: '', next: '', confirm: '', show: false };
        const others = res.otherSessionsEnded > 0 ? ` Signed out of ${res.otherSessionsEnded} other session${res.otherSessionsEnded === 1 ? '' : 's'}.` : '';
        this.showToast(`Password changed. Use it the next time you sign in.${others}`);
      },
      error: (err: unknown) => {
        this.pwSaving = false;
        this.pwError = this.auth.errorMessage(err, 'The password could not be changed. Please try again.');
      }
    });
  }

  private showToast(message: string): void {
    // shared toast: durations, hover-pause and close button live in shared-ui (toast.ts)
    pushToast(message);
  }

  logout(): void {
    this.closeProfileMenu();
    this.auth.logout();
  }

  private updateBreadcrumbs(): void {
    this.breadcrumbs = [];
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!event.target) {
      return;
    }
    const clickedInside = this.elementRef.nativeElement.contains(event.target);

    if (this.isDropdownOpen) {
      const clickedInsideFlyout = !!(event.target as HTMLElement).closest('.admin-flyout');
      if (!clickedInside && !clickedInsideFlyout) {
        this.isDropdownOpen = false;
        this.flyoutPosition = null;
      }
    }

    if (this.isNotificationsOpen) {
      const clickedInsideNotifications = !!(event.target as HTMLElement).closest('.notifications-flyout');
      if (!clickedInside && !clickedInsideNotifications) {
        this.isNotificationsOpen = false;
        this.notificationsFlyoutPosition = null;
      }
    }
  }

  @HostListener('window:resize')
  @HostListener('window:scroll')
  onViewportChange(): void {
    if (this.isDropdownOpen) {
      this.isDropdownOpen = false;
      this.flyoutPosition = null;
    }
    if (this.isNotificationsOpen) {
      this.isNotificationsOpen = false;
      this.notificationsFlyoutPosition = null;
    }
  }

  @HostListener('document:keydown.escape')
  onEscapeKey(): void {
    if (this.dialog) {
      this.dialog = '';
      return;
    }
    if (this.isDropdownOpen) {
      this.isDropdownOpen = false;
      this.flyoutPosition = null;
    }
    if (this.isNotificationsOpen) {
      this.isNotificationsOpen = false;
      this.notificationsFlyoutPosition = null;
    }
  }
}
