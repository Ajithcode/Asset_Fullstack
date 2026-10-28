import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { KeyRound, LucideAngularModule, Plus, RefreshCw, ShieldCheck } from 'lucide-angular';
import { ApiKey, Integration } from './integration-api.data';
import { IntegrationApiService } from './integration-api.service';
import { masked, relative, shortDate } from './integration-format';

/** API Credentials tab: masked API keys with rotate / revoke. Full key values are never displayed here. */
@Component({
  standalone: true,
  selector: 'app-integration-credentials-tab',
  imports: [LucideAngularModule],
  styleUrls: ['../shared/admin-workspace.css', './integration-api.css'],
  template: `
    <section class="sub-panel">
      <div class="panel__head">
        <h3>Client Credentials</h3>
      </div>
      <dl class="kv">
        <dt>Authentication</dt>
        <dd>{{ integration.authMethod }}</dd>
        <dt>Client ID</dt>
        <dd><code>{{ integration.clientId }}</code></dd>
        <dt>Secret Key</dt>
        <dd>
          @if (integration.secretLast4) {
            <code>{{ mask(integration.secretLast4) }}</code>
            <span class="muted"> · rotated {{ date(integration.secretRotatedAt) }}</span>
          } @else {
            <span class="pill" data-tone="orange">Not set</span>
          }
        </dd>
      </dl>
    </section>

    <section class="sub-panel">
      <div class="panel__head">
        <h3>API Keys ({{ activeCount() }} active)</h3>
        <button type="button" class="btn btn--sm btn--outline" (click)="create.emit()">
          <lucide-icon [img]="icons.Plus" [size]="14"></lucide-icon> Create API Key
        </button>
      </div>
      <p class="note int-gap"><lucide-icon [img]="icons.ShieldCheck" [size]="16"></lucide-icon> Key values are shown only once when created or rotated. Store them in a secrets manager.</p>
      <div class="table-wrap">
        <table class="table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Key</th>
              <th>Created</th>
              <th>Expires</th>
              <th>Scopes</th>
              <th>Last Used</th>
              <th>Status</th>
              <th><span class="sr-only">Actions</span></th>
            </tr>
          </thead>
          <tbody>
            @for (k of integration.apiKeys; track k.id) {
              <tr>
                <td><b>{{ k.name }}</b></td>
                <td><code>{{ keyMask(k) }}</code></td>
                <td>{{ date(k.createdAt) }}</td>
                <td>
                  {{ date(k.expiresAt) }}
                  @if (k.status === 'Active' && expiresSoon(k)) {
                    <span class="pill" data-tone="orange">Soon</span>
                  }
                </td>
                <td>
                  <div class="chips">
                    @for (s of k.scopes; track s) {
                      <span class="pill" data-tone="purple">{{ s }}</span>
                    }
                  </div>
                </td>
                <td class="muted">{{ k.lastUsed ? rel(k.lastUsed) : 'Never' }}</td>
                <td><span class="pill" [attr.data-tone]="k.status === 'Active' ? 'green' : 'grey'"><i></i> {{ k.status }}</span></td>
                <td class="int-actions">
                  @if (k.status === 'Active') {
                    <button type="button" class="btn btn--sm" (click)="rotate(k)">
                      <lucide-icon [img]="icons.RefreshCw" [size]="13"></lucide-icon> Rotate
                    </button>
                    <button type="button" class="btn btn--sm btn--danger" (click)="pendingRevoke = k">Revoke</button>
                  }
                </td>
              </tr>
            } @empty {
              <tr>
                <td colspan="8" class="center muted">
                  <lucide-icon [img]="icons.KeyRound" [size]="16"></lucide-icon> No API keys for this integration yet.
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </section>

    @if (pendingRevoke) {
      <div class="overlay">
        <div class="confirm" role="alertdialog" aria-modal="true" aria-labelledby="int-revoke-title">
          <h2 id="int-revoke-title">Revoke API key?</h2>
          <p>
            “{{ pendingRevoke.name }}” ({{ keyMask(pendingRevoke) }}) will stop working immediately. Systems using it will receive
            401 Unauthorized.
          </p>
          <div class="confirm__actions">
            <button type="button" class="btn" (click)="pendingRevoke = null">Cancel</button>
            <button type="button" class="btn btn--danger" (click)="revoke()">Revoke Key</button>
          </div>
        </div>
      </div>
    }
  `
})
export class IntegrationCredentialsTab {
  private readonly service = inject(IntegrationApiService);

  @Input({ required: true }) integration!: Integration;
  @Output() create = new EventEmitter<void>();
  /** A freshly issued secret to show once in the secret dialog. */
  @Output() secret = new EventEmitter<{ title: string; value: string }>();
  @Output() notice = new EventEmitter<string>();

  readonly icons = { Plus, KeyRound, RefreshCw, ShieldCheck };
  pendingRevoke: ApiKey | null = null;

  activeCount(): number {
    return this.integration.apiKeys.filter((k) => k.status === 'Active').length;
  }

  mask(last4: string): string {
    return masked(last4);
  }

  keyMask(k: ApiKey): string {
    return masked(k.last4, k.prefix);
  }

  date(iso: string): string {
    return shortDate(iso);
  }

  rel(iso: string): string {
    return relative(iso);
  }

  expiresSoon(k: ApiKey): boolean {
    return new Date(k.expiresAt).getTime() - Date.now() < 30 * 86400000;
  }

  rotate(k: ApiKey): void {
    const value = this.service.rotateApiKey(this.integration.id, k.id);
    this.secret.emit({ title: `API key rotated: ${k.name}`, value });
  }

  revoke(): void {
    if (!this.pendingRevoke) return;
    this.service.revokeApiKey(this.integration.id, this.pendingRevoke.id);
    this.notice.emit(`API key “${this.pendingRevoke.name}” revoked.`);
    this.pendingRevoke = null;
  }
}
