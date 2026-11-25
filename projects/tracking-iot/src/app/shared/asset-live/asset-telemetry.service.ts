import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AuthService, environment } from 'shared-ui';

/** Connection of a tag: Online (newest reading online, within 10 min), Offline, Idle (no reading for 10 min) or No data. */
export type TagConnection = 'Online' | 'Offline' | 'Idle' | 'No data';

/** The newest NetworkTimeSeries reading of one tag (Dashboard service). */
export interface TagTelemetry {
  tagId: string;
  isAssigned: boolean;
  gatewayId: string | null;
  /** Time of the newest reading (UTC ISO); null when the tag has never reported. */
  timestamp: string | null;
  status: string | null;
  connection: TagConnection;
  /** Battery % */
  battery: number | null;
  /** Signal strength in dBm. */
  rssi: number | null;
}

/** GET /api/dashboards/network-status/assets/{id} and each message of its /live WebSocket. */
export interface AssetTelemetry {
  assetRegistrationId: string;
  assetTag: string | null;
  assetName: string | null;
  tags: TagTelemetry[];
}

export type LiveState = 'connecting' | 'live' | 'reconnecting';

export interface LiveEvent {
  state: LiveState;
  data?: AssetTelemetry;
}

/**
 * Live readings of a plotted asset's tags from the Dashboard service (the same feed as Administration > Maps & Floor
 * Plans uses): the server sends the current readings on connect and again whenever a reading changes.
 */
@Injectable({ providedIn: 'root' })
export class AssetTelemetryService {
  private readonly auth = inject(AuthService);
  private readonly base = `${environment.dashboardApiUrl}api/dashboards/network-status/assets`;

  /**
   * WebSocket that reconnects with back-off (1 s, 2 s, 4 s … up to 30 s); unsubscribing closes it. Browsers can't send
   * headers on a WebSocket, so the sign-in token goes in ?access_token= (the API accepts it there).
   */
  live(assetId: string): Observable<LiveEvent> {
    const httpUrl = `${this.base}/${encodeURIComponent(assetId)}/live`;
    return new Observable<LiveEvent>((subscriber) => {
      if (typeof WebSocket === 'undefined') {
        subscriber.complete();
        return undefined;
      }
      let socket: WebSocket | null = null;
      let retries = 0;
      let stopped = false;
      let retryTimer: ReturnType<typeof setTimeout> | undefined;

      const connect = () => {
        subscriber.next({ state: retries ? 'reconnecting' : 'connecting' });
        socket = new WebSocket(this.socketUrl(httpUrl));
        socket.onopen = () => {
          retries = 0;
          subscriber.next({ state: 'live' });
        };
        socket.onmessage = (event) => {
          try {
            subscriber.next({ state: 'live', data: JSON.parse(event.data as string) as AssetTelemetry });
          } catch {
            // ignore a message that is not JSON
          }
        };
        socket.onerror = () => socket?.close();
        socket.onclose = () => {
          if (stopped) return;
          retries++;
          subscriber.next({ state: 'reconnecting' });
          retryTimer = setTimeout(connect, Math.min(30_000, 1000 * 2 ** Math.min(retries - 1, 5)));
        };
      };

      connect();
      return () => {
        stopped = true;
        clearTimeout(retryTimer);
        if (socket && socket.readyState <= WebSocket.OPEN) socket.close(1000);
      };
    });
  }

  private socketUrl(httpUrl: string): string {
    const url = new URL(httpUrl, typeof window !== 'undefined' ? window.location.href : undefined);
    url.protocol = url.protocol === 'https:' ? 'wss:' : 'ws:';
    const token = this.auth.getToken();
    if (token) url.searchParams.set('access_token', token);
    return url.toString();
  }
}
