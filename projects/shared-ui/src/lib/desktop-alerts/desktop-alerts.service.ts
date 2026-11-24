import { HttpClient } from '@angular/common/http';
import { Injectable, NgZone, inject } from '@angular/core';
import { Subject, firstValueFrom } from 'rxjs';
import { environment } from '../environment/environment';

/** unsupported: no service worker / push in this browser; default: not asked yet; denied: blocked; enabled: subscribed. */
export type DesktopAlertsState = 'unsupported' | 'default' | 'denied' | 'disabled' | 'enabled';

/** An alert pushed by the Tracking service (also forwarded to open Asset360 tabs by the service worker). */
export interface DesktopAlertMessage {
  type: 'iot-alert';
  alertId: string;
  title: string;
  body: string;
  severity?: string;
  sound: boolean;
  number: number;
  url?: string;
  raisedAt?: string;
  /** Tracking API endpoint for Acknowledge / Remind me, and the alert's secret for it. */
  actionUrl?: string;
  actionToken?: string;
}

const SW_URL = '/sw.js';

/**
 * Desktop (Web Push) notifications for IoT alerts. The browser subscribes once (Enable on this device); the Tracking
 * service then pushes each alert to the service worker (public/sw.js), which shows a system notification with
 * Acknowledge / Remind me even when Asset360 is closed or in another tab. While a tab is open, the alert is also
 * passed to the app (alerts$) and a sound is played.
 */
@Injectable({ providedIn: 'root' })
export class DesktopAlertsService {
  private readonly http = inject(HttpClient);
  private readonly zone = inject(NgZone);
  private readonly api = `${environment.trackingApiUrl}api/push`;
  private started = false;

  state: DesktopAlertsState = this.supported ? (Notification.permission === 'denied' ? 'denied' : 'default') : 'unsupported';
  /** Alerts received while this tab is open. */
  readonly alerts$ = new Subject<DesktopAlertMessage>();
  /** Play the in-app sound (the shell turns it off with the user's "Notification sound" preference). */
  soundEnabled = true;

  private readonly handled = new Set<string>();

  /** False when this alert (and reminder number) was already shown by the alert feed or the service worker. */
  markHandled(alertId: string, number: number): boolean {
    const key = `${alertId}#${number}`;
    if (this.handled.has(key)) return false;
    this.handled.add(key);
    return true;
  }

  get supported(): boolean {
    return typeof window !== 'undefined' && 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
  }

  /** Call once after sign-in: registers the service worker and refreshes this browser's subscription if allowed. */
  async init(): Promise<void> {
    if (!this.supported || this.started) return;
    this.started = true;
    navigator.serviceWorker.addEventListener('message', (event: MessageEvent) => {
      const data = event.data as DesktopAlertMessage | undefined;
      if (data?.type !== 'iot-alert') return;
      // the 10 s alert feed may have shown this alert already (first notification only; reminders always show)
      if (!this.markHandled(data.alertId, data.number)) return;
      this.zone.run(() => this.alerts$.next(data));
      if (data.sound && this.soundEnabled) this.playSound();
    });
    try {
      const registration = await navigator.serviceWorker.register(SW_URL);
      if (Notification.permission === 'granted') await this.subscribe(registration);
      else this.state = Notification.permission === 'denied' ? 'denied' : 'default';
    } catch {
      // service worker blocked (e.g. not served from the app root): stay in-app only
    }
  }

  /** Asks for permission (needs a click) and subscribes this browser. */
  async enable(): Promise<boolean> {
    if (!this.supported) return false;
    const permission = await Notification.requestPermission();
    if (permission !== 'granted') {
      this.state = permission === 'denied' ? 'denied' : 'default';
      return false;
    }
    try {
      const registration = await navigator.serviceWorker.register(SW_URL);
      await navigator.serviceWorker.ready;
      await this.subscribe(registration);
      return this.state === 'enabled';
    } catch {
      this.state = 'disabled';
      return false;
    }
  }

  /** Stops desktop notifications on this browser. */
  async disable(): Promise<void> {
    if (!this.supported) return;
    const registration = await navigator.serviceWorker.getRegistration(SW_URL);
    const subscription = await registration?.pushManager.getSubscription();
    if (subscription) {
      await firstValueFrom(this.http.post(`${this.api}/subscriptions/remove`, { endpoint: subscription.endpoint })).catch(() => undefined);
      await subscription.unsubscribe();
    }
    this.state = 'disabled';
  }

  /** Acknowledge / Remind me from the in-app pop-up (same endpoint as the desktop notification's buttons). */
  async act(alert: DesktopAlertMessage, action: 'acknowledge' | 'remind'): Promise<boolean> {
    const url = alert.actionUrl || `${this.api}/actions`;
    if (!alert.actionToken) return false;
    let endpoint: string | null = null;
    try {
      const registration = await navigator.serviceWorker?.getRegistration(SW_URL);
      endpoint = (await registration?.pushManager.getSubscription())?.endpoint ?? null;
    } catch {
      // no subscription: the server records "Desktop notification" as who acknowledged
    }
    try {
      await firstValueFrom(this.http.post(url, { alertId: alert.alertId, token: alert.actionToken, action, endpoint }));
      // the notification for this alert is no longer needed
      const registration = await navigator.serviceWorker?.getRegistration(SW_URL);
      (await registration?.getNotifications({ tag: `iot-alert-${alert.alertId}` }))?.forEach((n) => n.close());
      return true;
    } catch {
      return false;
    }
  }

  /** Short two-tone chime (Web Audio; no sound file needed). */
  playSound(): void {
    try {
      const Ctx = (window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext);
      const ctx = new Ctx();
      const tone = (freq: number, start: number, length: number) => {
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.type = 'sine';
        osc.frequency.value = freq;
        gain.gain.setValueAtTime(0.0001, ctx.currentTime + start);
        gain.gain.exponentialRampToValueAtTime(0.25, ctx.currentTime + start + 0.02);
        gain.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + start + length);
        osc.connect(gain).connect(ctx.destination);
        osc.start(ctx.currentTime + start);
        osc.stop(ctx.currentTime + start + length + 0.05);
      };
      tone(880, 0, 0.25);
      tone(1320, 0.18, 0.35);
      setTimeout(() => ctx.close(), 1000);
    } catch {
      // audio blocked until the user interacts with the page
    }
  }

  private async subscribe(registration: ServiceWorkerRegistration): Promise<void> {
    const { publicKey } = await firstValueFrom(this.http.get<{ publicKey: string }>(`${this.api}/public-key`));
    let subscription = await registration.pushManager.getSubscription();
    const key = urlBase64ToUint8Array(publicKey);
    // a subscription made with other keys cannot receive our pushes
    if (subscription && !sameKey(subscription.options.applicationServerKey, key)) {
      await subscription.unsubscribe();
      subscription = null;
    }
    subscription ??= await registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: key as BufferSource });
    const json = subscription.toJSON();
    await firstValueFrom(
      this.http.post(`${this.api}/subscriptions`, {
        endpoint: json.endpoint,
        p256dh: json.keys?.['p256dh'],
        auth: json.keys?.['auth'],
        apiUrl: environment.trackingApiUrl,
        appUrl: window.location.origin,
        clientId: environment.clientId,
        tenantId: environment.tenantId,
      })
    );
    this.state = 'enabled';
  }
}

function urlBase64ToUint8Array(base64: string): Uint8Array {
  const padded = (base64 + '='.repeat((4 - (base64.length % 4)) % 4)).replace(/-/g, '+').replace(/_/g, '/');
  const raw = atob(padded);
  return Uint8Array.from(raw, (c) => c.charCodeAt(0));
}

function sameKey(current: ArrayBuffer | null | undefined, wanted: Uint8Array): boolean {
  if (!current) return false;
  const a = new Uint8Array(current);
  return a.length === wanted.length && a.every((v, i) => v === wanted[i]);
}
