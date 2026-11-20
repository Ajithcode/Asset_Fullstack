import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, Subject, catchError } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { apiError, tenantParams, trackingUrl } from '../shared/tracking-api';
import { AssetLocation, Gateway } from '../tracking-telemetry/telemetry-overview-api';

/*
 * Real-Time Location page data (Tracking service, GET /api/real-time-location/...). Same location engine as
 * Tracking & Telemetry: BLE proximity, location = zone of the gateway hearing the tag strongest.
 */

export type Freshness = 'Live' | 'Stale' | 'Offline' | 'Unknown';
export type Movement = 'Moving' | 'Stationary' | 'Unknown';
export type TrailRange = '30m' | '1h' | '24h' | '7d';

/** Moving + Stationary + Unknown = assetsInView. */
export interface RtlKpis { assetsInView: number; moving: number; stationary: number; unknown: number; locationAlerts: number }

/** The location registered on the asset (Asset Registration), i.e. where it is expected to be. */
export interface ExpectedLocation { zoneId: string | null; zoneName: string | null; floorId: string | null; floorName: string | null; buildingName: string | null; siteName: string | null; locationPath: string | null }

export interface RtlAsset {
  assetId: string;
  assetTag: string | null;
  name: string;
  assetType: string | null;
  operationalStatus: string | null;
  technology: string;
  tracker: string | null;
  tags: string[];
  location: AssetLocation | null;
  freshness: Freshness;
  movement: Movement;
  lastZoneChangeAt: string | null;
  lastValidLocationAt: string | null;
  /** e.g. BLE Proximity. */
  locationSource: string;
  /** Zone level / Gateway range / Unknown (no metre value for BLE proximity). */
  accuracy: string;
  confidence: 'High' | 'Medium' | 'Low' | 'Unknown';
  confidenceBasis: string;
  gatewaysHearing: number;
  signalMarginDb: number | null;
  rssi: number | null;
  activeLocationAlerts: number;
  expected: ExpectedLocation | null;
  /** Match, Mismatch, Unknown or Not set. */
  comparisonStatus: string;
  comparisonNote: string;
}

export interface RtlOverview { generatedAt: string; kpis: RtlKpis; assets: RtlAsset[]; gateways: Gateway[] }

export interface TrailSegment { gatewayId: string; zoneName: string | null; floorName: string | null; from: string; to: string; minutes: number; initial: boolean }
export interface RtlTrail { assetId: string; name: string; range: TrailRange; from: string; to: string; segments: TrailSegment[]; zoneChanges: number; timeInZones: { zone: string; minutes: number }[]; note: string }

export interface RtlEvent { eventId: string; at: string; assetId: string | null; assetName: string | null; eventType: string; zone: string | null; floor: string | null; details: string | null; severity: string; source: string | null; status: string }
export interface RtlEventPage { items: RtlEvent[]; total: number; page: number; pageSize: number; totalPages: number; rangeLabel: string; eventTypes: string[] }

/** "subscribed" reply of the Locating socket /ws/locate: the tag's latest gateway, from NetworkTimeSeries. */
export interface LocateReply {
  tagid: string;
  gatewayid: string;
  lastSeen: string | null;
  rssi: number | null;
  mqttConnected?: boolean;
}

export interface LiveMessage {
  state: 'connecting' | 'reconnecting' | 'live' | 'error';
  data?: LocateReply;
  message?: string;
}

/** A message from /ws/locate (subscribed, commandSent, commandStatus, error…); fields as the backend sends them. */
export interface LocateMessage {
  type: string;
  tagid?: string;
  gatewayid?: string;
  rssi?: number | null;
  lastSeen?: string | null;
  reqId?: number;
  command?: string;
  /** commandStatus: accepted | completed | failed | timeout. */
  status?: string;
  message?: string;
}

/** An open /ws/locate connection: messages in, JSON objects out (queued until the socket is open). */
export interface LocateSocket {
  messages: Observable<LocateMessage>;
  send(message: object): void;
  close(): void;
}

/** What the live panel shows: the socket's reply placed on a floor plan (built by the page). */
export interface LiveSnapshot {
  assetId: string;
  name: string;
  tagId: string;
  /** Live (heard in the last minute) or Stale. */
  state: 'Live' | 'Stale';
  gatewayId: string;
  rssi: number | null;
  lastSeenAt: string | null;
  floorPlanId: string | null;
  zoneName: string | null;
  floorName: string | null;
  /** The gateway's point on the floor plan (% from the left / top); null when it is not plotted. */
  x: number | null;
  y: number | null;
  note: string;
}

@Injectable({ providedIn: 'root' })
export class RtlApi {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  /**
   * Live location of a tag over the Locating socket (ws /ws/locate on the Tracking service, the same socket the
   * locate LED / buzzer commands use). Sends {"type":"subscribe","tagid"} and repeats it every few seconds: each
   * "subscribed" reply carries the tag's latest gateway, lastSeen and rssi from NetworkTimeSeries. Unsubscribing
   * (the Stop button) sends {"type":"unsubscribe"} and closes the socket. Reconnects with backoff if it drops.
   */
  locate(tagId: string, everyMs = 3000): Observable<LiveMessage> {
    return new Observable<LiveMessage>((subscriber) => {
      if (typeof WebSocket === 'undefined') {
        subscriber.next({ state: 'error', message: 'This browser does not support live updates.' });
        subscriber.complete();
        return undefined;
      }
      let socket: WebSocket | null = null;
      let stopped = false;
      let retries = 0;
      let retryTimer: ReturnType<typeof setTimeout> | undefined;
      let pollTimer: ReturnType<typeof setInterval> | undefined;
      const subscribe = () => {
        if (socket?.readyState === WebSocket.OPEN) socket.send(JSON.stringify({ type: 'subscribe', tagid: tagId }));
      };
      const connect = () => {
        subscriber.next({ state: retries ? 'reconnecting' : 'connecting' });
        socket = new WebSocket(this.locateUrl());
        socket.onopen = () => {
          retries = 0;
          subscribe();
          clearInterval(pollTimer);
          pollTimer = setInterval(subscribe, everyMs);
        };
        socket.onmessage = (e) => {
          try {
            const msg = JSON.parse(e.data as string) as LocateReply & { type: string; message?: string };
            if (msg.type === 'subscribed') subscriber.next({ state: 'live', data: msg });
            else if (msg.type === 'error') subscriber.next({ state: 'error', message: msg.message ?? 'Live location failed.' });
          } catch {
            // not JSON: ignore
          }
        };
        socket.onerror = () => socket?.close();
        socket.onclose = () => {
          clearInterval(pollTimer);
          if (stopped) return;
          retries++;
          subscriber.next({ state: 'reconnecting' });
          retryTimer = setTimeout(connect, Math.min(15_000, 1000 * 2 ** Math.min(retries - 1, 4)));
        };
      };
      connect();
      return () => {
        stopped = true;
        clearTimeout(retryTimer);
        clearInterval(pollTimer);
        if (socket?.readyState === WebSocket.OPEN) socket.send(JSON.stringify({ type: 'unsubscribe' }));
        if (socket && socket.readyState <= WebSocket.OPEN) socket.close(1000, 'stopped');
      };
    });
  }

  /**
   * One Locating socket (/ws/locate) for LED / buzzer commands: send {"type":"subscribe"|"led"|"buzzer"|"both"|"off",
   * "tagid",...} and read subscribed / commandSent / commandStatus / error. close() ends it.
   */
  locateSocket(): LocateSocket {
    const out = new Subject<LocateMessage>();
    const queue: string[] = [];
    let closed = false;
    const socket = typeof WebSocket === 'undefined' ? null : new WebSocket(this.locateUrl());
    if (!socket) queueMicrotask(() => out.next({ type: 'error', message: 'This browser does not support WebSockets.' }));
    if (socket) {
      socket.onopen = () => queue.splice(0).forEach((m) => socket.send(m));
      socket.onmessage = (e) => {
        try {
          out.next(JSON.parse(e.data as string) as LocateMessage);
        } catch {
          // not JSON: ignore
        }
      };
      socket.onerror = () => out.next({ type: 'error', message: 'Could not connect to the locating service.' });
      socket.onclose = () => {
        if (!closed) out.next({ type: 'closed' });
        out.complete();
      };
    }
    return {
      messages: out.asObservable(),
      send: (message: object) => {
        const text = JSON.stringify(message);
        if (socket?.readyState === WebSocket.OPEN) socket.send(text);
        else if (socket?.readyState === WebSocket.CONNECTING) queue.push(text);
        else out.next({ type: 'error', message: 'The locating connection is closed. Open the asset again.' });
      },
      close: () => {
        closed = true;
        if (socket && socket.readyState <= WebSocket.OPEN) socket.close(1000, 'done');
      },
    };
  }

  private locateUrl(): string {
    const url = new URL(`${environment.trackingApiUrl}ws/locate`, typeof window !== 'undefined' ? window.location.href : undefined);
    url.protocol = url.protocol === 'https:' ? 'wss:' : 'ws:';
    const token = this.auth.getToken();
    if (token) url.searchParams.set('access_token', token);
    return url.toString();
  }

  overview(siteId: string, floorPlanId: string): Observable<RtlOverview> {
    return this.http.get<RtlOverview>(trackingUrl('real-time-location/overview'), { params: tenantParams({ siteId, floorPlanId }) })
      .pipe(catchError((err) => apiError(err, 'Could not load asset locations.')));
  }

  trail(assetId: string, range: TrailRange): Observable<RtlTrail> {
    return this.http.get<RtlTrail>(trackingUrl(`real-time-location/assets/${encodeURIComponent(assetId)}/trail`), { params: tenantParams({ range }) })
      .pipe(catchError((err) => apiError(err, 'Could not load the location trail.')));
  }

  events(range: string, siteId: string, page: number, pageSize: number, assetId = '', eventType = ''): Observable<RtlEventPage> {
    return this.http.get<RtlEventPage>(trackingUrl('real-time-location/events'), { params: tenantParams({ range, siteId, page, pageSize, assetId, eventType }) })
      .pipe(catchError((err) => apiError(err, 'Could not load location events.')));
  }
}
