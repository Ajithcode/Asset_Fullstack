import { HttpClient } from '@angular/common/http';
import { Injectable, NgZone, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environment/environment';
import { DesktopAlertsService } from './desktop-alerts.service';

/** One alert of GET /api/alerts/feed (the same feed the mobile app polls, see docs/MOBILE-ALERTS.md). */
export interface FeedAlert {
  alertId: string;
  raisedAt: string;
  severity: string;
  eventType: string;
  assetId: string | null;
  assetName: string;
  zone: string | null;
  siteName: string | null;
  message: string | null;
  status: string;
  ruleName: string | null;
  /** Notification channels of the rule: In-app, Push (desktop), Email. */
  channels: string[];
  sound: boolean;
  acknowledgedBy: string | null;
  acknowledgedAt: string | null;
}

interface FeedResponse { serverTime: string; unacknowledged: number; items: FeedAlert[] }

const POLL_MS = 10_000;
const ASKED_KEY = 'a360.desktopNotifyAsked';
export const ALERTS_ROUTE = '/tracking-iot/geofence-alerts';

/**
 * Header bell + toast + sound + desktop notification for alerts. Polls the Tracking alert feed every 10 s: the bell shows
 * the unacknowledged count and the latest 5 open alerts; an alert raised after sign-in pops up once as a toast (rule channel
 * In-app) and as a browser notification (channel Push, permission asked once), with a sound when the rule has "Play sound".
 */
@Injectable({ providedIn: 'root' })
export class AlertFeedService {
  private readonly http = inject(HttpClient);
  private readonly zone = inject(NgZone);
  private readonly desktop = inject(DesktopAlertsService);
  private readonly api = `${environment.trackingApiUrl}api/alerts`;
  private timer?: ReturnType<typeof setInterval>;
  private since: number | null = null;
  private readonly seen = new Set<string>();

  unacknowledged = 0;
  /** The latest 5 open alerts (newest first). */
  latest: FeedAlert[] = [];
  busy = false;

  start(): void {
    if (this.timer) return;
    this.askPermissionOnce();
    void this.poll();
    this.zone.runOutsideAngular(() => (this.timer = setInterval(() => void this.poll(), POLL_MS)));
  }

  stop(): void {
    clearInterval(this.timer);
    this.timer = undefined;
  }

  async poll(): Promise<void> {
    try {
      const r = await firstValueFrom(this.http.get<FeedResponse>(`${this.api}/feed`, { params: { status: 'Open', limit: 20 } }));
      this.zone.run(() => {
        const firstLoad = this.since === null;
        this.since ??= new Date(r.serverTime).getTime();
        this.unacknowledged = r.unacknowledged;
        this.latest = r.items.slice(0, 5);
        for (const a of [...r.items].reverse()) {
          if (this.seen.has(a.alertId)) continue;
          this.seen.add(a.alertId);
          if (!firstLoad && new Date(a.raisedAt).getTime() >= this.since!) this.announce(a);
        }
      });
    } catch {
      // offline / signed out: try again on the next tick
    }
  }

  async acknowledge(alert: FeedAlert): Promise<boolean> {
    try {
      await firstValueFrom(this.http.post(`${this.api}/${encodeURIComponent(alert.alertId)}/acknowledge`, {}));
      this.closeDesktop(alert.alertId);
      await this.poll();
      return true;
    } catch {
      return false;
    }
  }

  async acknowledgeAll(): Promise<boolean> {
    this.busy = true;
    try {
      await firstValueFrom(this.http.post(`${this.api}/acknowledge-all`, {}));
      await this.poll();
      return true;
    } catch {
      return false;
    } finally {
      this.busy = false;
    }
  }

  /** Which delivery channels the server has (Email needs Smtp__* settings). */
  channels(): Promise<{ email: { available: boolean; missing: string[] } }> {
    return firstValueFrom(this.http.get<{ email: { available: boolean; missing: string[] } }>(`${this.api}/channels`));
  }

  /** Toast, sound and browser notification of one new alert, as its rule's channels say. */
  private announce(a: FeedAlert): void {
    if (!this.desktop.markHandled(a.alertId, 1)) return;
    const title = `${a.eventType}: ${a.assetName}`;
    const body = a.message ?? [a.zone, a.siteName].filter(Boolean).join(' · ');
    if (a.channels.includes('In-app')) {
      this.desktop.alerts$.next({ type: 'iot-alert', alertId: a.alertId, title, body, severity: a.severity, sound: a.sound, number: 1, url: ALERTS_ROUTE, raisedAt: a.raisedAt });
    }

    if (a.channels.includes('Push') && typeof Notification !== 'undefined' && Notification.permission === 'granted') {
      try {
        // same tag as the service worker's notification, so a push for the same alert replaces it instead of doubling
        const n = new Notification(title, { body, tag: `iot-alert-${a.alertId}`, silent: !a.sound, requireInteraction: /critical|high/i.test(a.severity) });
        n.onclick = () => {
          window.focus();
          window.location.assign(ALERTS_ROUTE);
          n.close();
        };
      } catch {
        // the browser refused (e.g. insecure context): the toast still shows
      }
    }

    if (a.sound && this.desktop.soundEnabled && (a.channels.includes('In-app') || a.channels.includes('Push'))) this.desktop.playSound();
  }

  private closeDesktop(alertId: string): void {
    void navigator.serviceWorker?.getRegistration('/sw.js').then((r) => r?.getNotifications({ tag: `iot-alert-${alertId}` }).then((l) => l.forEach((n) => n.close())));
  }

  /** Asks for the browser's notification permission once, on the first click after sign-in (browsers want a user gesture). */
  private askPermissionOnce(): void {
    try {
      if (typeof Notification === 'undefined' || Notification.permission !== 'default' || localStorage.getItem(ASKED_KEY)) return;
      const ask = () => {
        document.removeEventListener('click', ask, true);
        try {
          localStorage.setItem(ASKED_KEY, '1');
        } catch {
          // storage blocked: asks again next visit
        }
        void Notification.requestPermission();
      };
      document.addEventListener('click', ask, true);
    } catch {
      // no Notification API
    }
  }
}
