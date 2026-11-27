import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { trackingUrl } from '../shared/tracking-api';

interface DevTag { tagId: string; asset: string | null }
interface DevZone { zoneId: string; name: string }
interface Tap { id: string; tag: string; zone: string; tapMs: number; note: string | null; decisionMs: number | null; wsMs: number | null; matched: boolean }
interface Stage { n: number; p50: number; p95: number }
interface Move { id: string; tag: string; kind: string; atMs: number; toZone: string | null; fromZone: string | null; stages: Record<string, number | null> }
interface LatencyReply { stats: Record<string, Stage | null>; moves: Move[] }

const STAGES: [string, string][] = [
  ['deviceToRecv', 'Device timestamp → server receive'], ['recvToRead', 'Receive → worker read'], ['readToDecision', 'Worker read → decision'],
  ['decisionToSaved', 'Decision → state saved'], ['savedToPublished', 'Saved → published (pub/sub)'], ['publishedToWs', 'Published → WebSocket sent'],
  ['total', 'Device timestamp → WebSocket sent'],
];

/**
 * DEV ONLY (route /tracking-iot/dev-location, not in the menu; the API answers 404 unless DevTools:Enabled).
 * 1) Ground truth: pick a trolley + zone and tap "I'm in this zone now" when it really crosses; the system latency per move
 *    (tap → decision, tap → WebSocket) is computed from the next matching decision. The phone clock is aligned to the server with a
 *    few /dev/time round trips. A human tap is ±1-2 s of the real crossing: judge medians, not single moves.
 * 2) Latency per stage of the last zone changes with p50 / p95. Only live fast-mode decisions are listed (shadow mode logs elsewhere).
 */
@Component({
  selector: 'app-dev-location',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
  <div class="dl">
    <h2>Fast tracking – dev tools</h2>
    <p class="muted">Clock offset to server: {{ offsetMs === null ? 'measuring…' : offsetMs + ' ms' }} {{ error }}</p>

    <section>
      <h3>Ground truth</h3>
      <label>Trolley
        <select [(ngModel)]="tagId"><option *ngFor="let t of tags" [value]="t.tagId">{{ t.asset || '—' }} · {{ t.tagId }}</option></select>
      </label>
      <label>Zone it is in right now
        <select [(ngModel)]="zoneId"><option *ngFor="let z of zones" [value]="z.zoneId">{{ z.name }}</option></select>
      </label>
      <button class="tap" (click)="tap()" [disabled]="!tagId || !zoneId || busy">I'm in this zone now</button>
      <p class="muted" *ngIf="last">Logged {{ last }}</p>
      <table>
        <tr><th>Time</th><th>Trolley</th><th>Zone</th><th>Tap → decision</th><th>Tap → WebSocket</th></tr>
        <tr *ngFor="let t of taps">
          <td>{{ t.tapMs | date: 'HH:mm:ss' }}</td><td>{{ t.tag.slice(-4) }}</td><td>{{ t.zone }}</td>
          <td>{{ t.matched ? sec(t.decisionMs) : 'no matching decision' }}</td><td>{{ sec(t.wsMs) }}</td>
        </tr>
      </table>
    </section>

    <section>
      <h3>Latency per stage (last {{ moves.length }} moves)</h3>
      <table>
        <tr><th>Stage</th><th>n</th><th>p50</th><th>p95</th></tr>
        <tr *ngFor="let s of stages"><td>{{ s[1] }}</td><td>{{ stats?.[s[0]]?.n ?? 0 }}</td><td>{{ ms(stats?.[s[0]]?.p50) }}</td><td>{{ ms(stats?.[s[0]]?.p95) }}</td></tr>
      </table>
      <table>
        <tr><th>Time</th><th>Trolley</th><th>Change</th><th>Device→recv</th><th>Decision</th><th>Push→WS</th><th>Total</th></tr>
        <tr *ngFor="let m of moves">
          <td>{{ m.atMs | date: 'HH:mm:ss' }}</td><td>{{ m.tag.slice(-4) }}</td><td>{{ m.fromZone || '—' }} → {{ m.toZone || '—' }}</td>
          <td>{{ ms(m.stages['deviceToRecv']) }}</td><td>{{ ms(m.stages['readToDecision']) }}</td><td>{{ ms(m.stages['publishedToWs']) }}</td><td>{{ ms(m.stages['total']) }}</td>
        </tr>
      </table>
    </section>
  </div>`,
  styles: [`
    .dl { max-width: 760px; margin: 0 auto; padding: 16px; display: grid; gap: 16px; }
    section { border: 1px solid var(--border, #d9dce3); border-radius: 10px; padding: 12px; display: grid; gap: 10px; }
    label { display: grid; gap: 4px; font-size: 13px; }
    select { min-height: 44px; font-size: 16px; }
    .tap { min-height: 72px; font-size: 20px; font-weight: 600; border-radius: 12px; border: 0; background: #184f95; color: #fff; }
    .tap:disabled { opacity: .5; }
    table { width: 100%; border-collapse: collapse; font-size: 13px; }
    th, td { text-align: left; padding: 4px 6px; border-bottom: 1px solid #e5e7eb; }
    .muted { color: #6b7280; font-size: 12px; margin: 0; }
  `],
})
export class DevLocation implements OnInit, OnDestroy {
  private readonly http = inject(HttpClient);
  private timer?: ReturnType<typeof setInterval>;
  readonly stages = STAGES;
  tags: DevTag[] = []; zones: DevZone[] = []; taps: Tap[] = []; moves: Move[] = [];
  stats: Record<string, Stage | null> | null = null;
  tagId = ''; zoneId = ''; offsetMs: number | null = null; busy = false; last = ''; error = '';

  ngOnInit(): void {
    const base = trackingUrl('real-time-location/dev');
    this.http.get<DevTag[]>(`${base}/tags`).subscribe({ next: (t) => { this.tags = t; this.tagId = t[0]?.tagId ?? ''; }, error: () => (this.error = '(dev tools are off or you are not signed in)') });
    this.http.get<DevZone[]>(`${base}/zones`).subscribe({ next: (z) => { this.zones = z; this.zoneId = z[0]?.zoneId ?? ''; }, error: () => undefined });
    void this.measureOffset();
    this.refresh();
    this.timer = setInterval(() => this.refresh(), 3000);
  }

  ngOnDestroy(): void { clearInterval(this.timer); }

  // Offset = phone clock - server clock, from the round trip with the shortest RTT of 6.
  private async measureOffset(): Promise<void> {
    const base = trackingUrl('real-time-location/dev');
    const samples: { rtt: number; offset: number }[] = [];
    for (let i = 0; i < 6; i++) {
      try {
        const t0 = Date.now();
        const r = await new Promise<{ serverMs: number }>((res, rej) => this.http.get<{ serverMs: number }>(`${base}/time`).subscribe({ next: res, error: rej }));
        const t1 = Date.now();
        samples.push({ rtt: t1 - t0, offset: (t0 + t1) / 2 - r.serverMs });
      } catch { /* ignore */ }
    }
    if (samples.length) this.offsetMs = Math.round(samples.sort((a, b) => a.rtt - b.rtt)[0].offset);
  }

  tap(): void {
    this.busy = true;
    const body = { tagId: this.tagId, zoneId: this.zoneId, clientMs: Date.now(), clockOffsetMs: this.offsetMs ?? 0 };
    this.http.post(`${trackingUrl('real-time-location/dev')}/ground-truth`, body).subscribe({
      next: () => { this.busy = false; this.last = new Date().toLocaleTimeString(); this.refresh(); },
      error: () => { this.busy = false; this.error = '(tap failed)'; },
    });
  }

  private refresh(): void {
    const base = trackingUrl('real-time-location/dev');
    this.http.get<Tap[]>(`${base}/ground-truth?limit=15`).subscribe({ next: (t) => (this.taps = t), error: () => undefined });
    this.http.get<LatencyReply>(`${base}/latency?limit=30`).subscribe({ next: (r) => { this.stats = r.stats; this.moves = r.moves; }, error: () => undefined });
  }

  ms(v: number | null | undefined): string { return v === null || v === undefined ? '—' : `${v} ms`; }
  sec(v: number | null | undefined): string { return v === null || v === undefined ? '—' : `${(v / 1000).toFixed(1)} s`; }
}
