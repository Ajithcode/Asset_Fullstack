import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  ArrowDown,
  ArrowUp,
  Ban,
  ChartColumn,
  ChevronRight,
  Download,
  FileText,
  KeyRound,
  LayoutDashboard,
  Link,
  LucideAngularModule,
  Pencil,
  Plus,
  Power,
  RefreshCw,
  Search,
  Settings,
  TriangleAlert,
  Upload,
  Webhook,
  Workflow
} from 'lucide-angular';
import { AdminPage } from '../shared/admin-common';
import { ApiKeyDialog, ApiKeyRequest } from './api-key-dialog';
import { IntegrationCredentialsTab } from './credentials-tab';
import { AUTH_METHODS, ENVIRONMENTS, Environment, Integration, PROVIDERS, RETRY_POLICIES, STATUSES, SYNC_FREQUENCIES } from './integration-api.data';
import { IntegrationApiService, IntegrationForm, apiErrorMessage } from './integration-api.service';
import { HTTPS_URL, slug, stampLabel, statusTone } from './integration-format';
import { IntegrationSettingsDrawer } from './integration-settings-drawer';
import { IntegrationLogsTab } from './logs-tab';
import { IntegrationMappingTab } from './mapping-tab';
import { IntegrationOverviewTab } from './overview-tab';
import { SecretDialog } from './secret-dialog';
import { IntegrationWebhooksTab } from './webhooks-tab';

type TabId = 'overview' | 'mapping' | 'credentials' | 'webhooks' | 'logs';

/** Administration > Integration & API */
@Component({
  standalone: true,
  selector: 'app-admin-integration-api',
  imports: [
    FormsModule,
    RouterLink,
    LucideAngularModule,
    IntegrationOverviewTab,
    IntegrationMappingTab,
    IntegrationCredentialsTab,
    IntegrationWebhooksTab,
    IntegrationLogsTab,
    IntegrationSettingsDrawer,
    ApiKeyDialog,
    SecretDialog
  ],
  templateUrl: './integration-api.html',
  styleUrls: ['../shared/admin-workspace.css', './integration-api.css']
})
export class IntegrationApiPage extends AdminPage implements OnInit {
  private readonly service = inject(IntegrationApiService);

  readonly icons = {
    Link,
    ChartColumn,
    Webhook,
    TriangleAlert,
    ArrowUp,
    ArrowDown,
    Plus,
    KeyRound,
    Upload,
    Download,
    Search,
    RefreshCw,
    Pencil,
    Ban,
    Power,
    ChevronRight
  };
  readonly tabs: { id: TabId; label: string; icon: typeof Link }[] = [
    { id: 'overview', label: 'Overview', icon: LayoutDashboard },
    { id: 'mapping', label: 'Data Mapping', icon: Workflow },
    { id: 'credentials', label: 'API Credentials', icon: Settings },
    { id: 'webhooks', label: 'Webhooks', icon: Webhook },
    { id: 'logs', label: 'Logs', icon: FileText }
  ];
  readonly statuses = STATUSES;

  selectedId: string | null = this.service.integrations()[0]?.id ?? null;
  tab: TabId = 'overview';
  drawerOpen = true;
  creating = false;

  listQuery = '';
  query = '';
  typeFilter = '';
  statusFilter = '';

  showKeyDialog = false;
  secret: { title: string; value: string } | null = null;
  confirmDisable = false;

  ngOnInit(): void {
    this.service.load().subscribe({
      next: (list) => {
        if (!this.service.byId(this.selectedId)) this.selectedId = list[0]?.id ?? null;
      },
      error: (err) => this.notify(apiErrorMessage(err, 'Could not load integrations.'))
    });
  }

  // ----- Derived data -----

  get integrations(): Integration[] {
    return this.service.integrations();
  }

  get selected(): Integration | null {
    return this.service.byId(this.selectedId) ?? null;
  }

  get types(): string[] {
    return [...new Set(this.integrations.map((i) => i.type))].sort();
  }

  get filtered(): Integration[] {
    const q1 = this.query.trim().toLowerCase();
    const q2 = this.listQuery.trim().toLowerCase();
    return this.integrations.filter(
      (i) =>
        (!this.typeFilter || i.type === this.typeFilter) &&
        (!this.statusFilter || i.status === this.statusFilter) &&
        this.matches(i, q1) &&
        this.matches(i, q2)
    );
  }

  get activeCount(): number {
    return this.service.activeCount();
  }

  get requestsToday(): string {
    return this.service.requestsToday().toLocaleString('en-US');
  }

  get activeWebhooks(): number {
    return this.service.activeWebhooks();
  }

  get failedSyncs(): number {
    return this.service.failedSyncs();
  }

  private matches(i: Integration, q: string): boolean {
    return !q || [i.name, i.code, i.type, i.provider].some((v) => v.toLowerCase().includes(q));
  }

  // ----- Template helpers -----

  tone(i: Integration): string {
    return statusTone(i.status);
  }

  stamp(iso: string | null): string {
    return stampLabel(iso);
  }

  // ----- Selection -----

  select(i: Integration): void {
    if (this.selectedId !== i.id) this.tab = 'overview';
    this.selectedId = i.id;
    this.creating = false;
  }

  addIntegration(): void {
    this.creating = true;
    this.drawerOpen = true;
  }

  edit(): void {
    this.creating = false;
    this.drawerOpen = true;
  }

  closeDrawer(): void {
    this.drawerOpen = false;
    this.creating = false;
  }

  onSaved(id: string): void {
    this.creating = false;
    this.selectedId = id;
  }

  onDeleted(id: string): void {
    if (this.selectedId === id) {
      this.selectedId = this.integrations[0]?.id ?? null;
      this.tab = 'overview';
    }
  }

  // ----- Simulated actions (DEMO MODE) -----

  testConnection(): void {
    const i = this.selected;
    if (!i) return;
    const result = this.service.testConnection(i.id);
    this.notify(`DEMO MODE: ${i.name} — ${result.message}`);
  }

  syncNow(): void {
    const i = this.selected;
    if (!i) return;
    const runs = this.service.syncNow(i.id);
    if (!runs) {
      this.notify(`${i.name} is disabled. Enable it to sync.`);
      return;
    }
    const total = runs.reduce((n, r) => n + r.records, 0);
    this.notify(`DEMO MODE: ${i.name} synced ${total.toLocaleString('en-US')} records across ${runs.length} object(s).`);
  }

  toggleEnabled(): void {
    const i = this.selected;
    if (!i) return;
    if (i.status === 'Disabled') {
      this.service.setEnabled(i.id, true).subscribe({
        next: () => this.notify(`${i.name} enabled.`),
        error: (err) => this.notify(apiErrorMessage(err, `Could not enable ${i.name}.`))
      });
    } else {
      this.confirmDisable = true;
    }
  }

  disable(): void {
    const i = this.selected;
    this.confirmDisable = false;
    if (!i) return;
    this.service.setEnabled(i.id, false).subscribe({
      next: () => this.notify(`${i.name} disabled. Scheduled syncs are paused.`),
      error: (err) => this.notify(apiErrorMessage(err, `Could not disable ${i.name}.`))
    });
  }

  // ----- API keys -----

  openKeyDialog(): void {
    if (!this.integrations.length) {
      this.notify('Add an integration first.');
      return;
    }
    this.showKeyDialog = true;
  }

  createKey(req: ApiKeyRequest): void {
    const value = this.service.createApiKey(req.integrationId, req.name, req.scopes, req.days);
    this.showKeyDialog = false;
    this.selectedId = req.integrationId;
    this.tab = 'credentials';
    this.secret = { title: `API key created: ${req.name}`, value };
  }

  showSecret(s: { title: string; value: string }): void {
    this.secret = s;
  }

  /** Drops the one-time secret from memory. */
  closeSecret(): void {
    this.secret = null;
  }

  // ----- Import / export (secrets are never included) -----

  exportIntegrations(): void {
    const json = JSON.stringify({ organization: this.organization, exportedAt: new Date().toISOString(), integrations: this.service.exportData() }, null, 2);
    const url = URL.createObjectURL(new Blob([json], { type: 'application/json' }));
    const a = document.createElement('a');
    a.href = url;
    a.download = `asset360-integrations-${new Date().toISOString().slice(0, 10)}.json`;
    a.click();
    URL.revokeObjectURL(url);
    this.notify(`Exported ${this.integrations.length} integrations (credentials excluded).`);
  }

  /**
   * Imports integrations from a JSON export. They arrive disabled and stay unsaved (the API requires a
   * secret key) until a secret key is entered in Edit.
   */
  importIntegrations(input: HTMLInputElement): void {
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => {
      let parsed: unknown;
      try {
        parsed = JSON.parse(String(reader.result));
      } catch {
        this.notify('That file is not valid JSON.');
        return;
      }
      const root = parsed as { integrations?: unknown };
      const items = Array.isArray(parsed) ? parsed : Array.isArray(root?.integrations) ? root.integrations : null;
      if (!items) {
        this.notify('No integrations found in that file.');
        return;
      }
      let added = 0;
      let skipped = 0;
      for (const raw of items) {
        const form = this.toForm(raw);
        if (!form) {
          skipped++;
          continue;
        }
        this.service.addDraft(form);
        added++;
      }
      this.notify(`Imported ${added} integration(s)${skipped ? `, skipped ${skipped} (invalid or duplicate code)` : ''}. Add a secret key in Edit to save them.`);
    };
    reader.readAsText(file);
  }

  private toForm(raw: unknown): IntegrationForm | null {
    if (!raw || typeof raw !== 'object') return null;
    const r = raw as Record<string, unknown>;
    const text = (v: unknown) => (typeof v === 'string' ? v.trim() : '');
    const name = text(r['name']);
    const code = text(r['code']).toUpperCase() || `INT-${slug(name).toUpperCase().slice(0, 8)}-001`;
    const baseUrl = text(r['baseUrl']);
    if (!name || !HTTPS_URL.test(baseUrl) || !/^[A-Z0-9][A-Z0-9-]{2,29}$/.test(code) || this.service.codeTaken(code, null)) return null;
    const provider = text(r['provider']);
    const env = text(r['environment']) as Environment;
    const timeout = Number(r['timeout']);
    const pick = (v: unknown, list: string[], fallback: string) => (list.includes(text(v)) ? text(v) : fallback);
    return {
      name,
      code,
      provider: PROVIDERS[provider] ? provider : 'Custom REST',
      environment: ENVIRONMENTS.includes(env) ? env : 'Development',
      baseUrl,
      authMethod: pick(r['authMethod'], AUTH_METHODS, AUTH_METHODS[0]),
      clientId: text(r['clientId']),
      syncFrequency: pick(r['syncFrequency'], SYNC_FREQUENCIES, 'Manual'),
      timeout: Number.isInteger(timeout) && timeout >= 5 && timeout <= 600 ? timeout : 300,
      retryPolicy: pick(r['retryPolicy'], RETRY_POLICIES, RETRY_POLICIES[2]),
      enabled: false
    };
  }
}
