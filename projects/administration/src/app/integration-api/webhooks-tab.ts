import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LucideAngularModule, Plus, Send, Trash2, Webhook as WebhookIcon } from 'lucide-angular';
import { Integration, WEBHOOK_EVENTS, Webhook } from './integration-api.data';
import { IntegrationApiService } from './integration-api.service';
import { HTTPS_URL, masked, stampLabel } from './integration-format';

/** Webhooks tab: outgoing endpoints, their events and masked signing secrets. */
@Component({
  standalone: true,
  selector: 'app-integration-webhooks-tab',
  imports: [FormsModule, LucideAngularModule],
  styleUrls: ['../shared/admin-workspace.css', './integration-api.css'],
  template: `
    <section class="sub-panel">
      <div class="panel__head">
        <h3>Webhook Endpoints ({{ integration.webhooks.length }})</h3>
        @if (!adding) {
          <button type="button" class="btn btn--sm btn--outline" (click)="startAdd()">
            <lucide-icon [img]="icons.Plus" [size]="14"></lucide-icon> Add Webhook
          </button>
        }
      </div>

      @if (adding) {
        <div class="int-add">
          <div class="field" [class.invalid]="submitted && !!urlError()">
            <label for="int-wh-url">Endpoint URL <span class="req">*</span></label>
            <input id="int-wh-url" placeholder="https://example.com/hooks/asset360" [(ngModel)]="url" />
            @if (submitted && urlError()) {
              <small>{{ urlError() }}</small>
            }
          </div>
          <div class="field" [class.invalid]="submitted && !events.length">
            <span>Events <span class="req">*</span></span>
            <div class="checks">
              @for (e of allEvents; track e) {
                <label><input type="checkbox" [checked]="events.includes(e)" (change)="toggleEvent(e)" /> {{ e }}</label>
              }
            </div>
            @if (submitted && !events.length) {
              <small>Pick at least one event.</small>
            }
          </div>
          <div class="int-foot">
            <button type="button" class="btn" (click)="adding = false">Cancel</button>
            <button type="button" class="btn btn--primary" (click)="add()">Add Endpoint</button>
          </div>
        </div>
      }

      <div class="table-wrap">
        <table class="table">
          <thead>
            <tr>
              <th>Endpoint</th>
              <th>Events</th>
              <th>Signing Secret</th>
              <th>Last Delivery</th>
              <th>Status</th>
              <th><span class="sr-only">Actions</span></th>
            </tr>
          </thead>
          <tbody>
            @for (w of integration.webhooks; track w.id) {
              <tr>
                <td class="int-url">{{ w.url }}</td>
                <td>
                  <div class="chips">
                    @for (e of w.events; track e) {
                      <span class="pill" data-tone="blue">{{ e }}</span>
                    }
                  </div>
                </td>
                <td><code>{{ secret(w) }}</code></td>
                <td>
                  {{ stamp(w.lastDelivery) }}
                  @if (w.lastStatus === 'Failed') {
                    <span class="pill" data-tone="red">Failed</span>
                  }
                </td>
                <td>
                  <label class="switch">
                    <input type="checkbox" [checked]="w.active" (change)="toggle(w)" [attr.aria-label]="'Enable ' + w.url" />
                    <i></i>
                    <span class="muted">{{ w.active ? 'Active' : 'Inactive' }}</span>
                  </label>
                </td>
                <td class="int-actions">
                  <button type="button" class="icon-btn" title="Send test event" aria-label="Send test event" (click)="ping(w)">
                    <lucide-icon [img]="icons.Send" [size]="14"></lucide-icon>
                  </button>
                  <button type="button" class="icon-btn" title="Delete" aria-label="Delete webhook" (click)="pendingDelete = w">
                    <lucide-icon [img]="icons.Trash2" [size]="14"></lucide-icon>
                  </button>
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="6" class="center muted">
                  <lucide-icon [img]="icons.WebhookIcon" [size]="16"></lucide-icon> No webhooks configured.
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </section>

    @if (pendingDelete) {
      <div class="overlay">
        <div class="confirm" role="alertdialog" aria-modal="true" aria-labelledby="int-wh-delete">
          <h2 id="int-wh-delete">Delete webhook?</h2>
          <p>{{ pendingDelete.url }} will no longer receive events.</p>
          <div class="confirm__actions">
            <button type="button" class="btn" (click)="pendingDelete = null">Cancel</button>
            <button type="button" class="btn btn--danger" (click)="remove()">Delete</button>
          </div>
        </div>
      </div>
    }
  `
})
export class IntegrationWebhooksTab {
  private readonly service = inject(IntegrationApiService);

  @Input({ required: true }) integration!: Integration;
  @Output() secretIssued = new EventEmitter<{ title: string; value: string }>();
  @Output() notice = new EventEmitter<string>();

  readonly icons = { Plus, Trash2, Send, WebhookIcon };
  readonly allEvents = WEBHOOK_EVENTS;

  adding = false;
  submitted = false;
  url = '';
  events: string[] = [];
  pendingDelete: Webhook | null = null;

  secret(w: Webhook): string {
    return masked(w.secretLast4, 'whsec_');
  }

  stamp(iso: string | null): string {
    return stampLabel(iso);
  }

  startAdd(): void {
    this.adding = true;
    this.submitted = false;
    this.url = '';
    this.events = ['asset.created', 'asset.updated'];
  }

  toggleEvent(e: string): void {
    this.events = this.events.includes(e) ? this.events.filter((x) => x !== e) : [...this.events, e];
  }

  urlError(): string {
    const u = this.url.trim();
    if (!u) return 'Endpoint URL is required.';
    if (!HTTPS_URL.test(u)) return 'Enter a valid https:// URL.';
    if (this.integration.webhooks.some((w) => w.url.toLowerCase() === u.toLowerCase())) return 'This endpoint is already registered.';
    return '';
  }

  add(): void {
    this.submitted = true;
    if (this.urlError() || !this.events.length) return;
    const value = this.service.addWebhook(this.integration.id, this.url, this.events);
    this.adding = false;
    this.secretIssued.emit({ title: 'Webhook signing secret', value });
  }

  toggle(w: Webhook): void {
    this.service.setWebhookActive(this.integration.id, w.id, !w.active);
    this.notice.emit(`Webhook ${w.active ? 'disabled' : 'enabled'}.`);
  }

  ping(w: Webhook): void {
    const ok = this.service.pingWebhook(this.integration.id, w.id);
    this.notice.emit(ok ? `DEMO MODE: test event delivered to ${w.url}.` : 'Enable the webhook before sending a test event.');
  }

  remove(): void {
    if (!this.pendingDelete) return;
    this.service.deleteWebhook(this.integration.id, this.pendingDelete.id);
    this.notice.emit('Webhook deleted.');
    this.pendingDelete = null;
  }
}
