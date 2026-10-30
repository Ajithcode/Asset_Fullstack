import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, map, of, tap, throwError } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { CURRENT_USER } from '../shared/admin-common';
import {
  ApiKey,
  Environment,
  FieldMapping,
  Integration,
  LogEntry,
  LogLevel,
  PROVIDERS,
  SyncRun,
  inDays
} from './integration-api.data';

/** Editable fields of an integration (the Integration Settings drawer). */
export interface IntegrationForm {
  name: string;
  code: string;
  provider: string;
  environment: Environment;
  baseUrl: string;
  authMethod: string;
  clientId: string;
  syncFrequency: string;
  timeout: number;
  retryPolicy: string;
  enabled: boolean;
}

/** Request body for POST {administrationApiUrl}api/administration/integrations */
interface CreateIntegrationRequest {
  integrationName: string;
  integrationCode: string;
  provider: string;
  environment: Environment;
  baseUrl: string;
  authenticationMethod: string;
  authClientId: string;
  secretKey: string;
  syncFrequency: string;
  timeoutSeconds: number;
  retryPolicy: string;
  status: 'Active' | 'Inactive';
  createdBy: string;
  tenantId: string;
  clientId: string;
}

/** Request body for PUT {administrationApiUrl}api/administration/integrations/{integrationId}. A blank secretKey keeps the stored secret. */
interface UpdateIntegrationRequest {
  integrationName: string;
  integrationCode: string;
  provider: string;
  environment: Environment;
  baseUrl: string;
  authenticationMethod: string;
  authClientId: string;
  secretKey: string | null;
  syncFrequency: string;
  timeoutSeconds: number;
  retryPolicy: string;
  status: 'Active' | 'Inactive';
  updatedBy: string;
}

/** Raw shape returned by the integrations API. The secret itself is never returned. */
interface IntegrationDto {
  integrationId: string;
  integrationName: string;
  integrationCode: string;
  provider: string;
  environment: Environment;
  baseUrl: string;
  authenticationMethod: string;
  authClientId: string;
  secretKeyConfigured: boolean;
  syncFrequency: string;
  timeoutSeconds: number;
  retryPolicy: string;
  status: string | null;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  tenantId: string | null;
  clientId: string | null;
}

/** Stands in for `secretLast4` when a secret is stored but its characters are unknown (the API never returns it). */
export const SECRET_MASK = '••••';

/**
 * Maps the API response to the page model. Type, logo, direction and data objects come from the provider;
 * test / sync / key / webhook state has no API yet and starts empty. `secretLast4` is taken from the
 * value the user just typed, or masked when the secret was stored earlier (the API only says whether one is stored).
 */
function toAppIntegration(dto: IntegrationDto, secret = ''): Integration {
  const p = PROVIDERS[dto.provider] ?? PROVIDERS['Custom REST'];
  const createdAt = dto.createdAt ?? new Date().toISOString();
  return {
    id: dto.integrationId,
    code: dto.integrationCode,
    name: dto.integrationName,
    provider: dto.provider,
    type: p.type,
    version: 'v1.0',
    logo: p.logo,
    status: dto.status === 'Inactive' ? 'Disabled' : 'Active',
    environment: dto.environment,
    baseUrl: dto.baseUrl,
    authMethod: dto.authenticationMethod,
    clientId: dto.authClientId,
    secretLast4: dto.secretKeyConfigured ? secret.slice(-4) || SECRET_MASK : '',
    secretRotatedAt: dto.updatedAt ?? createdAt,
    syncFrequency: dto.syncFrequency,
    timeout: dto.timeoutSeconds,
    retryPolicy: dto.retryPolicy,
    ipAllowlist: true,
    lastTestAt: createdAt,
    lastTestPassed: false,
    lastSync: null,
    recordsSynced: 0,
    syncDirection: p.direction,
    dataObjects: [...p.objects],
    mappings: [],
    runs: [],
    apiKeys: [],
    webhooks: [],
    logs: []
  };
}

/** Readable message from an API error (validation problem, conflict or network failure). */
export function apiErrorMessage(err: unknown, fallback: string): string {
  if (!(err instanceof HttpErrorResponse)) return fallback;
  if (err.status === 0) return `${fallback} The Administration service is unreachable.`;
  const body = err.error as { message?: string; title?: string; errors?: Record<string, string[]> } | null;
  const first = body?.errors ? Object.values(body.errors).flat()[0] : undefined;
  return first ?? body?.message ?? body?.title ?? fallback;
}

const DAY = 86400000;
const BASE62 = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789';

/** Random demo secret. Never logged or stored: callers keep only the last 4 characters. */
function randomSecret(length: number): string {
  const bytes = new Uint8Array(length);
  crypto.getRandomValues(bytes);
  let out = '';
  for (const b of bytes) out += BASE62[b % BASE62.length];
  return out;
}

/**
 * Integration settings are saved through the Administration API (list, create, update, enable / disable, delete).
 * Imported integrations stay unsaved drafts until a secret key is entered.
 * DEMO MODE: connection tests, syncs, field mappings, API keys and webhooks are simulated in memory.
 */
@Injectable({ providedIn: 'root' })
export class IntegrationApiService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly apiUrl = `${environment.administrationApiUrl}api/administration/integrations`;

  readonly integrations = signal<Integration[]>([]);
  readonly requestsToday = signal(0);

  readonly activeCount = computed(() => this.integrations().filter((i) => i.status === 'Connected' || i.status === 'Active').length);
  readonly activeWebhooks = computed(() => this.integrations().reduce((n, i) => n + i.webhooks.filter((w) => w.active).length, 0));
  readonly failedSyncs = computed(() => {
    const since = Date.now() - DAY;
    return this.integrations().reduce(
      (n, i) => n + i.runs.filter((r) => r.status === 'Failed' && new Date(r.startedAt).getTime() >= since).length,
      0
    );
  });

  private seq = 5000;
  /** Imported integrations not yet saved to the API (it requires a secret key, which imports never carry). */
  private readonly drafts = new Set<string>();

  private id(prefix: string): string {
    return `${prefix}-${this.seq++}`;
  }

  byId(id: string | null): Integration | undefined {
    return id ? this.integrations().find((i) => i.id === id) : undefined;
  }

  isDraft(id: string): boolean {
    return this.drafts.has(id);
  }

  codeTaken(code: string, exceptId: string | null): boolean {
    const c = code.trim().toUpperCase();
    return this.integrations().some((i) => i.id !== exceptId && i.code.toUpperCase() === c);
  }

  private patch(id: string, fn: (i: Integration) => Integration): void {
    this.integrations.update((list) => list.map((i) => (i.id === id ? fn(i) : i)));
  }

  private log(level: LogLevel, event: string, message: string, actor = this.auth.getUserEmail() || CURRENT_USER.name): LogEntry {
    return { id: this.id('LOG'), at: new Date().toISOString(), level, event, message, actor };
  }

  // ----- Load / create / update / delete -----

  /**
   * GET /api/administration/integrations. Simulated state (runs, mappings, keys, webhooks, logs) already held for
   * an integration is kept, and unsaved imported drafts stay in the list.
   */
  load(): Observable<Integration[]> {
    const params = { tenantId: environment.tenantId, clientId: environment.clientId };
    return this.http.get<IntegrationDto[]>(this.apiUrl, { params }).pipe(
      map((dtos) => {
        const current = new Map(this.integrations().map((i) => [i.id, i]));
        const loaded = dtos.map((dto) => {
          const fresh = toAppIntegration(dto);
          const known = current.get(fresh.id);
          return known ? this.merge(known, fresh) : fresh;
        });
        const drafts = this.integrations().filter((i) => this.drafts.has(i.id));
        return [...loaded, ...drafts];
      }),
      tap((list) => this.integrations.set(list))
    );
  }

  /** Saved settings from the API over the simulated state already held for the integration. */
  private merge(known: Integration, saved: Integration): Integration {
    const status = saved.status === 'Disabled' ? 'Disabled' : known.status === 'Disabled' ? 'Active' : known.status;
    return {
      ...known,
      code: saved.code,
      name: saved.name,
      provider: saved.provider,
      type: saved.type,
      logo: saved.logo,
      status,
      environment: saved.environment,
      baseUrl: saved.baseUrl,
      authMethod: saved.authMethod,
      clientId: saved.clientId,
      secretLast4: saved.secretLast4 && saved.secretLast4 !== SECRET_MASK ? saved.secretLast4 : known.secretLast4 || saved.secretLast4,
      syncFrequency: saved.syncFrequency,
      timeout: saved.timeout,
      retryPolicy: saved.retryPolicy
    };
  }

  /** POST /api/administration/integrations, then adds the saved integration to the list. */
  create(form: IntegrationForm, secret: string): Observable<Integration> {
    return this.post(form, secret).pipe(tap((created) => this.integrations.update((list) => [...list, created])));
  }

  /** The secret is sent once and only its last 4 characters are kept. */
  private post(form: IntegrationForm, secret: string): Observable<Integration> {
    const request: CreateIntegrationRequest = {
      integrationName: form.name.trim(),
      integrationCode: form.code.trim().toUpperCase(),
      provider: form.provider,
      environment: form.environment,
      baseUrl: form.baseUrl.trim(),
      authenticationMethod: form.authMethod,
      authClientId: form.clientId.trim(),
      secretKey: secret,
      syncFrequency: form.syncFrequency,
      timeoutSeconds: form.timeout,
      retryPolicy: form.retryPolicy,
      status: form.enabled ? 'Active' : 'Inactive',
      createdBy: this.auth.getUserEmail() || 'unknown',
      tenantId: environment.tenantId,
      clientId: environment.clientId
    };
    return this.http.post<IntegrationDto>(this.apiUrl, request).pipe(
      map((dto) => {
        const created = toAppIntegration(dto, secret);
        return { ...created, logs: [this.log('Info', 'Integration created', `${created.name} added in ${created.environment}`)] };
      })
    );
  }

  /**
   * Adds an imported integration to the list without saving it. It is saved (POST) the first time
   * it is edited with a secret key.
   */
  addDraft(form: IntegrationForm): Integration {
    const p = PROVIDERS[form.provider] ?? PROVIDERS['Custom REST'];
    const now = new Date().toISOString();
    const draft: Integration = {
      id: this.id('INT'),
      code: form.code.trim().toUpperCase(),
      name: form.name.trim(),
      provider: form.provider,
      type: p.type,
      version: 'v1.0',
      logo: p.logo,
      status: form.enabled ? 'Active' : 'Disabled',
      environment: form.environment,
      baseUrl: form.baseUrl.trim(),
      authMethod: form.authMethod,
      clientId: form.clientId.trim(),
      secretLast4: '',
      secretRotatedAt: now,
      syncFrequency: form.syncFrequency,
      timeout: form.timeout,
      retryPolicy: form.retryPolicy,
      ipAllowlist: true,
      lastTestAt: now,
      lastTestPassed: false,
      lastSync: null,
      recordsSynced: 0,
      syncDirection: p.direction,
      dataObjects: [...p.objects],
      mappings: [],
      runs: [],
      apiKeys: [],
      webhooks: [],
      logs: [this.log('Info', 'Integration imported', `${form.name.trim()} imported (not saved until credentials are added)`)]
    };
    this.drafts.add(draft.id);
    this.integrations.update((list) => [...list, draft]);
    return draft;
  }

  /**
   * `secret` is only passed when the user typed a new secret key. Imported drafts are saved with POST and
   * replaced by the saved record; saved integrations are updated with PUT (a blank secret keeps the stored one).
   */
  update(id: string, form: IntegrationForm, secret: string): Observable<Integration> {
    if (this.drafts.has(id)) {
      return this.post(form, secret).pipe(
        tap((saved) => {
          this.drafts.delete(id);
          this.integrations.update((list) => list.map((i) => (i.id === id ? saved : i)));
        })
      );
    }
    return this.put(id, form, secret).pipe(
      map((saved) => {
        const current = this.byId(id);
        if (!current) return saved;
        const logs = [this.log('Info', 'Configuration updated', 'Integration settings saved')];
        if (secret) logs.unshift(this.log('Info', 'Secret rotated', 'Secret key replaced'));
        return {
          ...this.merge(current, saved),
          secretLast4: secret ? secret.slice(-4) : current.secretLast4 || saved.secretLast4,
          secretRotatedAt: secret ? new Date().toISOString() : current.secretRotatedAt,
          logs: [...logs, ...current.logs]
        };
      }),
      tap((updated) => this.patch(id, () => updated))
    );
  }

  /** PUT /api/administration/integrations/{integrationId}. */
  private put(id: string, form: IntegrationForm, secret: string): Observable<Integration> {
    const request: UpdateIntegrationRequest = {
      integrationName: form.name.trim(),
      integrationCode: form.code.trim().toUpperCase(),
      provider: form.provider,
      environment: form.environment,
      baseUrl: form.baseUrl.trim(),
      authenticationMethod: form.authMethod,
      authClientId: form.clientId.trim(),
      secretKey: secret || null,
      syncFrequency: form.syncFrequency,
      timeoutSeconds: form.timeout,
      retryPolicy: form.retryPolicy,
      status: form.enabled ? 'Active' : 'Inactive',
      updatedBy: this.auth.getUserEmail() || 'unknown'
    };
    return this.http.put<IntegrationDto>(`${this.apiUrl}/${encodeURIComponent(id)}`, request).pipe(map((dto) => toAppIntegration(dto, secret)));
  }

  /** DELETE /api/administration/integrations/{integrationId}; unsaved drafts are only dropped from the list. */
  remove(id: string): Observable<void> {
    const drop = () => {
      this.drafts.delete(id);
      this.integrations.update((list) => list.filter((i) => i.id !== id));
    };
    if (this.drafts.has(id)) {
      drop();
      return of(undefined);
    }
    return this.http.delete<void>(`${this.apiUrl}/${encodeURIComponent(id)}`).pipe(tap(drop));
  }

  /** Saves the status through PUT, re-sending the stored settings (the secret is kept). Drafts change only locally. */
  setEnabled(id: string, enabled: boolean): Observable<Integration> {
    const i = this.byId(id);
    if (!i) return throwError(() => new Error('Integration not found.'));
    const stamp = (x: Integration): Integration => ({
      ...x,
      status: enabled ? (x.status === 'Disabled' ? 'Active' : x.status) : 'Disabled',
      logs: [this.log(enabled ? 'Info' : 'Warning', enabled ? 'Integration enabled' : 'Integration disabled', `${x.name} ${enabled ? 'enabled' : 'disabled'}`), ...x.logs]
    });
    if (this.drafts.has(id)) {
      this.patch(id, stamp);
      return of(this.byId(id)!);
    }
    const form: IntegrationForm = {
      name: i.name,
      code: i.code,
      provider: i.provider,
      environment: i.environment,
      baseUrl: i.baseUrl,
      authMethod: i.authMethod,
      clientId: i.clientId,
      syncFrequency: i.syncFrequency,
      timeout: i.timeout,
      retryPolicy: i.retryPolicy,
      enabled
    };
    return this.put(id, form, '').pipe(
      map((saved) => stamp(this.merge(this.byId(id) ?? i, saved))),
      tap((updated) => this.patch(id, () => updated))
    );
  }

  // ----- Simulated connection / sync -----

  /** Simulated credential test: passes for enabled integrations with an https endpoint. */
  testConnection(id: string): { ok: boolean; message: string } {
    const i = this.byId(id);
    if (!i) return { ok: false, message: 'Integration not found.' };
    const ms = 80 + ([...i.code].reduce((n, c) => n + c.charCodeAt(0), 0) % 120);
    const ok = i.status !== 'Disabled' && i.baseUrl.startsWith('https://');
    const message = ok ? `Connection succeeded in ${ms} ms (TLS 1.3).` : i.status === 'Disabled' ? 'Integration is disabled.' : 'Endpoint must use HTTPS.';
    this.patch(id, (x) => ({
      ...x,
      lastTestAt: new Date().toISOString(),
      lastTestPassed: ok,
      logs: [this.log(ok ? 'Info' : 'Error', 'Connection test', ok ? `Credential test passed (${ms} ms)` : message), ...x.logs]
    }));
    this.requestsToday.update((n) => n + 1);
    return { ok, message };
  }

  /** Simulated sync of up to three data objects; returns the new runs, or null when disabled. */
  syncNow(id: string): SyncRun[] | null {
    const i = this.byId(id);
    if (!i || i.status === 'Disabled') return null;
    const now = Date.now();
    const created: SyncRun[] = i.dataObjects.slice(0, 3).map((object, idx) => {
      const records = ((i.runs.length + 1) * 53 + idx * 17) % 400 + 5;
      const direction = i.syncDirection === 'Outbound' ? 'out' : i.syncDirection === 'Inbound' ? 'in' : idx % 2 ? 'out' : 'in';
      return {
        id: this.id('RUN'),
        object,
        direction,
        records,
        startedAt: new Date(now - idx * 1000).toISOString(),
        durationSec: Math.round(records / 20) + 4,
        status: 'Success',
        message: 'Completed (manual sync)'
      };
    });
    const total = created.reduce((n, r) => n + r.records, 0);
    this.patch(id, (x) => ({
      ...x,
      status: x.status === 'Active' ? 'Active' : 'Connected',
      lastSync: new Date(now).toISOString(),
      recordsSynced: x.recordsSynced + total,
      runs: [...created, ...x.runs],
      logs: [
        ...created.map((r) =>
          this.log('Info', 'Sync completed', `${r.object}: ${r.records} records ${r.direction === 'in' ? 'received' : 'sent'} — manual sync`)
        ),
        ...x.logs
      ]
    }));
    this.requestsToday.update((n) => n + created.length * 4 + Math.round(total / 10));
    return created;
  }

  retryRun(id: string, runId: string): SyncRun | null {
    const i = this.byId(id);
    const run = i?.runs.find((r) => r.id === runId);
    if (!i || !run || i.status === 'Disabled') return null;
    const retry: SyncRun = {
      ...run,
      id: this.id('RUN'),
      startedAt: new Date().toISOString(),
      records: run.records || 42,
      status: 'Success',
      message: 'Completed (retry)'
    };
    this.patch(id, (x) => ({
      ...x,
      lastSync: retry.startedAt,
      recordsSynced: x.recordsSynced + retry.records,
      runs: [retry, ...x.runs],
      logs: [this.log('Info', 'Sync retried', `${run.object}: retry succeeded with ${retry.records} records`), ...x.logs]
    }));
    this.requestsToday.update((n) => n + 3);
    return retry;
  }

  saveMappings(id: string, mappings: FieldMapping[]): void {
    this.patch(id, (x) => ({
      ...x,
      mappings: mappings.map((m) => ({ ...m, source: m.source.trim(), target: m.target.trim() })),
      logs: [this.log('Info', 'Mapping updated', `${mappings.length} field mappings saved`), ...x.logs]
    }));
  }

  newMappingId(): string {
    return this.id('MAP');
  }

  // ----- API keys -----

  /** Creates a key and returns its full value. Only the prefix and last 4 characters are kept. */
  createApiKey(id: string, name: string, scopes: string[], days: number): string {
    const i = this.byId(id);
    const prefix = i?.environment === 'Production' ? 'a360_live_' : 'a360_test_';
    const secret = prefix + randomSecret(32);
    const now = new Date().toISOString();
    const apiKey: ApiKey = {
      id: this.id('KEY'),
      name: name.trim(),
      prefix,
      last4: secret.slice(-4),
      createdAt: now,
      expiresAt: inDays(days),
      scopes: [...scopes],
      status: 'Active',
      lastUsed: null
    };
    this.patch(id, (x) => ({
      ...x,
      apiKeys: [apiKey, ...x.apiKeys],
      logs: [this.log('Info', 'API key created', `Key "${apiKey.name}" created (…${apiKey.last4})`), ...x.logs]
    }));
    return secret;
  }

  /** Issues a replacement value for a key, keeping its name, scopes and expiry. */
  rotateApiKey(id: string, keyId: string): string {
    const k = this.byId(id)?.apiKeys.find((a) => a.id === keyId);
    const prefix = k?.prefix ?? 'a360_live_';
    const secret = prefix + randomSecret(32);
    this.patch(id, (x) => ({
      ...x,
      apiKeys: x.apiKeys.map((a) => (a.id === keyId ? { ...a, last4: secret.slice(-4), createdAt: new Date().toISOString() } : a)),
      logs: [this.log('Info', 'API key rotated', `Key "${k?.name ?? keyId}" rotated (…${secret.slice(-4)})`), ...x.logs]
    }));
    return secret;
  }

  revokeApiKey(id: string, keyId: string): void {
    this.patch(id, (x) => ({
      ...x,
      apiKeys: x.apiKeys.map((a) => (a.id === keyId ? { ...a, status: 'Revoked' } : a)),
      logs: [this.log('Warning', 'API key revoked', `Key …${x.apiKeys.find((a) => a.id === keyId)?.last4 ?? ''} revoked`), ...x.logs]
    }));
  }

  // ----- Webhooks -----

  /** Adds an endpoint and returns its signing secret (shown once). */
  addWebhook(id: string, url: string, events: string[]): string {
    const secret = 'whsec_' + randomSecret(32);
    this.patch(id, (x) => ({
      ...x,
      webhooks: [
        { id: this.id('WH'), url: url.trim(), events: [...events], active: true, secretLast4: secret.slice(-4), lastDelivery: null, lastStatus: 'Never' },
        ...x.webhooks
      ],
      logs: [this.log('Info', 'Webhook added', url.trim()), ...x.logs]
    }));
    return secret;
  }

  setWebhookActive(id: string, hookId: string, active: boolean): void {
    this.patch(id, (x) => ({
      ...x,
      webhooks: x.webhooks.map((w) => (w.id === hookId ? { ...w, active } : w)),
      logs: [this.log('Info', active ? 'Webhook enabled' : 'Webhook disabled', x.webhooks.find((w) => w.id === hookId)?.url ?? ''), ...x.logs]
    }));
  }

  deleteWebhook(id: string, hookId: string): void {
    this.patch(id, (x) => ({
      ...x,
      webhooks: x.webhooks.filter((w) => w.id !== hookId),
      logs: [this.log('Warning', 'Webhook deleted', x.webhooks.find((w) => w.id === hookId)?.url ?? ''), ...x.logs]
    }));
  }

  /** Simulated test delivery to an endpoint. */
  pingWebhook(id: string, hookId: string): boolean {
    const w = this.byId(id)?.webhooks.find((h) => h.id === hookId);
    if (!w || !w.active) return false;
    this.patch(id, (x) => ({
      ...x,
      webhooks: x.webhooks.map((h) => (h.id === hookId ? { ...h, lastDelivery: new Date().toISOString(), lastStatus: 'Delivered' } : h)),
      logs: [this.log('Info', 'Webhook test', `Test event delivered to ${w.url}`), ...x.logs]
    }));
    return true;
  }

  // ----- Import / export (never includes secrets) -----

  exportData(): object[] {
    return this.integrations().map((i) => ({
      code: i.code,
      name: i.name,
      provider: i.provider,
      environment: i.environment,
      baseUrl: i.baseUrl,
      authMethod: i.authMethod,
      syncFrequency: i.syncFrequency,
      timeout: i.timeout,
      retryPolicy: i.retryPolicy,
      status: i.status,
      dataObjects: i.dataObjects,
      webhooks: i.webhooks.map((w) => ({ url: w.url, events: w.events, active: w.active }))
    }));
  }
}
