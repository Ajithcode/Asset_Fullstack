import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import {
  ArrowLeft,
  ArrowLeftRight,
  ArrowRight,
  Calendar,
  Clock,
  Database,
  EllipsisVertical,
  ExternalLink,
  Globe,
  Info,
  Layers,
  Lock,
  LucideAngularModule,
  Package,
  RefreshCw
} from 'lucide-angular';
import { FREQUENCY_MINUTES, Integration, SyncRun } from './integration-api.data';
import { IntegrationApiService } from './integration-api.service';
import { duration, relative, runTone, stampLabel } from './integration-format';

/** Overview tab: connection summary and the latest sync runs. */
@Component({
  standalone: true,
  selector: 'app-integration-overview-tab',
  imports: [LucideAngularModule],
  styleUrls: ['../shared/admin-workspace.css', './integration-api.css'],
  template: `
    <section class="sub-panel">
      <h3>Connection Summary</h3>
      <div class="int-summary">
        <dl class="int-kv">
          <dt><lucide-icon [img]="icons.Globe" [size]="16"></lucide-icon> Endpoint URL</dt>
          <dd>
            <a class="int-link" [href]="integration.baseUrl" target="_blank" rel="noopener noreferrer">
              {{ integration.baseUrl }} <lucide-icon [img]="icons.ExternalLink" [size]="13"></lucide-icon>
            </a>
          </dd>
          <dt><lucide-icon [img]="icons.Lock" [size]="16"></lucide-icon> Authentication</dt>
          <dd>{{ integration.authMethod }}</dd>
          <dt><lucide-icon [img]="icons.Layers" [size]="16"></lucide-icon> Environment</dt>
          <dd><span class="pill" [attr.data-tone]="envTone()">{{ integration.environment }}</span></dd>
          <dt><lucide-icon [img]="icons.Clock" [size]="16"></lucide-icon> Last Sync</dt>
          <dd>
            {{ stamp(integration.lastSync) }}
            @if (integration.lastSync) {
              <span class="muted">({{ rel(integration.lastSync) }})</span>
            }
          </dd>
        </dl>
        <dl class="int-kv">
          <dt><lucide-icon [img]="icons.RefreshCw" [size]="16"></lucide-icon> Sync Frequency</dt>
          <dd>{{ integration.syncFrequency }}</dd>
          <dt><lucide-icon [img]="icons.Database" [size]="16"></lucide-icon> Records Synced</dt>
          <dd>
            {{ integration.recordsSynced.toLocaleString('en-US') }}
            <lucide-icon [img]="icons.Info" [size]="13" class="muted" title="Total records exchanged since the integration was created"></lucide-icon>
          </dd>
          <dt><lucide-icon [img]="icons.Calendar" [size]="16"></lucide-icon> Next Sync</dt>
          <dd>
            @if (nextSync(); as next) {
              {{ stamp(next) }} <span class="muted">({{ rel(next) }})</span>
            } @else {
              <span class="muted">{{ integration.status === 'Disabled' ? 'Paused (disabled)' : 'Manual only' }}</span>
            }
          </dd>
          <dt><lucide-icon [img]="icons.ArrowLeftRight" [size]="16"></lucide-icon> Sync Direction</dt>
          <dd><span class="pill" data-tone="purple">{{ integration.syncDirection }}</span></dd>
          <dt><lucide-icon [img]="icons.Package" [size]="16"></lucide-icon> Data Objects</dt>
          <dd class="chips">
            @for (o of integration.dataObjects; track o) {
              <span class="pill" data-tone="blue">{{ o }}</span>
            }
          </dd>
        </dl>
      </div>
    </section>

    <section class="sub-panel">
      <div class="panel__head">
        <h3>Recent Sync Activity</h3>
        <button type="button" class="btn btn--link" (click)="viewLogs.emit()">
          View All Logs <lucide-icon [img]="icons.ArrowRight" [size]="14"></lucide-icon>
        </button>
      </div>
      <div class="table-wrap">
        <table class="table">
          <thead>
            <tr>
              <th>Object</th>
              <th>Direction</th>
              <th>Records</th>
              <th>Started</th>
              <th>Duration</th>
              <th>Status</th>
              <th><span class="sr-only">Actions</span></th>
            </tr>
          </thead>
          <tbody>
            @for (r of recent(); track r.id) {
              <tr>
                <td><b>{{ r.object }}</b></td>
                <td class="int-dir">
                  <lucide-icon [img]="r.direction === 'in' ? icons.ArrowRight : icons.ArrowLeft" [size]="15"></lucide-icon>
                  {{ directionLabel(r) }}
                </td>
                <td>{{ r.records.toLocaleString('en-US') }}</td>
                <td>{{ stamp(r.startedAt) }}</td>
                <td>{{ dur(r.durationSec) }}</td>
                <td><span class="pill" [attr.data-tone]="tone(r)" [title]="r.message"><i></i> {{ r.status }}</span></td>
                <td class="center">
                  <div class="row-menu">
                    <button type="button" class="icon-btn icon-btn--bare" [attr.aria-label]="'Actions for ' + r.object" (click)="toggleMenu(r.id)">
                      <lucide-icon [img]="icons.EllipsisVertical" [size]="16"></lucide-icon>
                    </button>
                    @if (menuFor === r.id) {
                      <div class="menu">
                        <button type="button" (click)="details(r)">View details</button>
                        @if (r.status !== 'Success') {
                          <button type="button" (click)="retry(r)">Retry sync</button>
                        }
                        <button type="button" (click)="menuFor = null; viewLogs.emit()">Open logs</button>
                      </div>
                    }
                  </div>
                </td>
              </tr>
            } @empty {
              <tr><td colspan="7" class="center muted">No sync activity yet. Use Sync Now to run the first sync.</td></tr>
            }
          </tbody>
        </table>
      </div>
    </section>
  `
})
export class IntegrationOverviewTab {
  private readonly service = inject(IntegrationApiService);

  @Input({ required: true }) integration!: Integration;
  @Output() viewLogs = new EventEmitter<void>();
  @Output() notice = new EventEmitter<string>();

  readonly icons = { Globe, Lock, Layers, Clock, RefreshCw, Database, Info, Calendar, ArrowLeftRight, Package, ArrowRight, ArrowLeft, ExternalLink, EllipsisVertical };
  menuFor: string | null = null;

  recent(): SyncRun[] {
    return this.integration.runs.slice(0, 5);
  }

  nextSync(): string | null {
    const every = FREQUENCY_MINUTES[this.integration.syncFrequency] ?? 0;
    if (!every || this.integration.status === 'Disabled') return null;
    const last = this.integration.lastSync ? new Date(this.integration.lastSync).getTime() : Date.now();
    let next = last + every * 60000;
    // Skip slots already in the past
    if (next < Date.now()) next += Math.ceil((Date.now() - next) / (every * 60000)) * every * 60000;
    return new Date(next).toISOString();
  }

  envTone(): string {
    return this.integration.environment === 'Production' ? 'green' : this.integration.environment === 'Staging' ? 'orange' : 'blue';
  }

  directionLabel(r: SyncRun): string {
    return r.direction === 'in' ? `${this.integration.type === 'ERP' ? 'ERP' : this.integration.name} to Asset360` : `Asset360 to ${this.integration.type === 'ERP' ? 'ERP' : this.integration.name}`;
  }

  stamp(iso: string | null): string {
    return stampLabel(iso);
  }

  rel(iso: string): string {
    return relative(iso);
  }

  dur(sec: number): string {
    return duration(sec);
  }

  tone(r: SyncRun): string {
    return runTone(r.status);
  }

  toggleMenu(id: string): void {
    this.menuFor = this.menuFor === id ? null : id;
  }

  details(r: SyncRun): void {
    this.menuFor = null;
    this.notice.emit(`${r.object}: ${r.message} (${r.records.toLocaleString('en-US')} records, ${duration(r.durationSec)}).`);
  }

  retry(r: SyncRun): void {
    this.menuFor = null;
    const run = this.service.retryRun(this.integration.id, r.id);
    this.notice.emit(run ? `DEMO MODE: ${r.object} re-synced, ${run.records} records.` : 'Enable the integration before retrying.');
  }
}
