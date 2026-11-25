import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, concat, forkJoin, last, map, of, switchMap, tap, throwError } from 'rxjs';
import { AuthService, environment, toTimeZoneId } from 'shared-ui';
import { CURRENT_USER } from '../shared/admin-common';
import { ConfigChange, ConfigSection, ConfigSetting, ConfigValue, buildSections } from './system-configuration.data';

export type ConfigValues = Record<string, ConfigValue>;

interface PlatformIdentityDto {
  platformName: string;
  organizationName: string;
  logo: string | null;
  defaultLandingPage: string | null;
}

interface RegionalDefaultsDto {
  country: string | null;
  timeZone: string | null;
  dateFormat: string | null;
  currency: string | null;
  language: string | null;
}

interface UserExperienceDto {
  theme: string;
  compactMode: boolean;
  defaultRowsPerPage: number;
  enableTooltips: boolean;
}

interface DataAndRecordsDto {
  defaultStatus: string;
  autoSaveIntervalSeconds: number;
  requireDeleteConfirmation: boolean;
}

/** Body of POST /api/administration/system-configuration; every section is replaced on save. */
interface SaveSystemConfigurationRequest {
  platformIdentity: PlatformIdentityDto;
  regionalDefaults: RegionalDefaultsDto;
  userExperience: UserExperienceDto;
  dataAndRecords: DataAndRecordsDto;
  updatedBy: string | null;
  tenantId: string;
  clientId: string;
}

/** Raw shape returned by the system-configuration API. */
interface SystemConfigurationDto {
  configurationId: string;
  platformIdentity: PlatformIdentityDto;
  regionalDefaults: RegionalDefaultsDto;
  userExperience: UserExperienceDto;
  dataAndRecords: DataAndRecordsDto;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  tenantId: string | null;
  clientId: string | null;
}

/** Master Data organization (GET /api/master-data/organizations), the fields the PUT needs back. */
interface OrganizationDto {
  id: string;
  organizationName: string;
  code: string | null;
  logoUrl: string | null;
  type: string | null;
  parentOrganization: unknown;
  businessUnits: unknown[] | null;
  sites: unknown[] | null;
  registrationNumber: string | null;
  website: string | null;
  description: string | null;
  address: { line1: string | null; line2: string | null; city: string | null; state: string | null; country: string | null; postalCode: string | null } | null;
  contact: { name: string | null; designation: string | null; email: string | null; phoneCountry: string | null; phone: string | null } | null;
  settings: unknown;
  status: string | null;
  updatedBy?: string | null;
  updatedAt?: string | null;
  createdBy?: string | null;
  createdAt?: string | null;
}

/** Administration numbering sequence (GET /api/administration/numbering-sequences). */
interface NumberingSequenceDto {
  sequenceId: string;
  sequenceName: string;
  sequenceCode: string;
  module: string;
  recordType: string;
  prefix: string;
  separator: string;
  numberLength: number;
  startNumber?: number | null;
  incrementBy: number;
  resetFrequency: string;
  scope: string;
  status: string | null;
  updatedBy?: string | null;
  updatedAt?: string | null;
  createdBy?: string | null;
  createdAt?: string | null;
}

/** One entry of GET /api/eventlogs (the audit trail written when these APIs save). */
interface EventLogDto {
  id: string;
  entityType: string;
  entityId: string;
  entityName: string;
  action: string;
  performedBy: string | null;
  createdAt: string | null;
  message: string;
}


/** UI auto-save labels and the interval in seconds the API stores (0 = off). */
const AUTO_SAVE_SECONDS: Record<string, number> = {
  Off: 0,
  '15 seconds': 15,
  '30 seconds': 30,
  '1 minute': 60,
  '5 minutes': 300
};

/** Settings stored by the Administration system-configuration API. */
export const API_SETTING_KEYS = new Set([
  'platformName', 'organizationName', 'logo', 'defaultLandingPage',
  'country', 'timeZone', 'dateFormat', 'currency', 'language',
  'theme', 'compactMode', 'rowsPerPage', 'enableTooltips',
  'defaultStatus', 'autoSaveInterval', 'confirmBeforeDelete'
]);

/** Organization Profile settings stored on the tenant's Master Data organization. */
export const ORG_SETTING_KEYS = new Set(['legalName', 'tradeLicense', 'headquarters', 'contactEmail', 'contactPhone', 'website']);

/** Code Prefixes & Sequences settings stored as Administration numbering sequences. */
export const SEQUENCE_SETTING_KEYS = new Set(['assetPrefix', 'workOrderPrefix', 'inventoryPrefix', 'sequenceDigits', 'sequenceReset']);

/** Every setting saved on the server; the rest only last for the browser session. */
export const STORED_SETTING_KEYS = new Set([...API_SETTING_KEYS, ...ORG_SETTING_KEYS, ...SEQUENCE_SETTING_KEYS]);

/** The numbering sequence behind each prefix setting. */
const SEQUENCES: { key: string; code: string; name: string; module: string; recordType: string }[] = [
  { key: 'assetPrefix', code: 'SYS-ASSET', name: 'Asset Code', module: 'Assets', recordType: 'Asset' },
  { key: 'workOrderPrefix', code: 'SYS-WORK-ORDER', name: 'Work Order Number', module: 'Maintenance', recordType: 'Work Order' },
  { key: 'inventoryPrefix', code: 'SYS-INVENTORY', name: 'Inventory Item Code', module: 'Inventory', recordType: 'Inventory Item' }
];

/** Audit entity types written by the APIs this page saves to. */
const HISTORY_ENTITY_TYPES: { type: string; section: string }[] = [
  { type: 'SystemConfiguration', section: 'General Settings' },
  { type: 'Organization', section: 'Organization Profile' },
  { type: 'NumberingSequence', section: 'Numbering & Master Data' }
];

function text(v: ConfigValue | undefined): string | null {
  const s = v === undefined ? '' : String(v).trim();
  return s ? s : null;
}

function labelFor(lookup: Record<string, string | number>, stored: string | number): string | undefined {
  return Object.keys(lookup).find((k) => lookup[k] === stored);
}

/** "AST-" -> { prefix: "AST", separator: "-" }; a prefix without separator gets "-". */
export function splitPrefix(value: ConfigValue): { prefix: string; separator: string } | null {
  const m = /^([A-Za-z0-9]+)([^A-Za-z0-9]{0,3})$/.exec(String(value ?? '').trim());
  return m ? { prefix: m[1], separator: m[2] || '-' } : null;
}

function toRequest(v: ConfigValues, updatedBy: string | null): SaveSystemConfigurationRequest {
  const currency = text(v['currency']);
  return {
    platformIdentity: {
      platformName: String(v['platformName'] ?? '').trim(),
      organizationName: String(v['organizationName'] ?? '').trim(),
      logo: text(v['logo']),
      defaultLandingPage: text(v['defaultLandingPage'])
    },
    regionalDefaults: {
      country: text(v['country']),
      timeZone: text(v['timeZone']) ? toTimeZoneId(text(v['timeZone'])) : null,
      dateFormat: text(v['dateFormat']),
      // "AED (د.إ)" -> "AED"
      currency: currency ? currency.slice(0, 3).toUpperCase() : null,
      language: text(v['language'])
    },
    userExperience: {
      theme: String(v['theme'] ?? 'Light'),
      compactMode: !!v['compactMode'],
      defaultRowsPerPage: Number(v['rowsPerPage']) || 25,
      enableTooltips: !!v['enableTooltips']
    },
    dataAndRecords: {
      defaultStatus: String(v['defaultStatus'] ?? 'Active'),
      autoSaveIntervalSeconds: AUTO_SAVE_SECONDS[String(v['autoSaveInterval'])] ?? 30,
      requireDeleteConfirmation: !!v['confirmBeforeDelete']
    },
    updatedBy,
    tenantId: environment.tenantId,
    clientId: environment.clientId
  };
}

/** Setting values held by the API, in the shape the form uses. */
function toValues(dto: SystemConfigurationDto, settings: ConfigSetting[]): ConfigValues {
  const p = dto.platformIdentity;
  const r = dto.regionalDefaults;
  const u = dto.userExperience;
  const d = dto.dataAndRecords;
  const out: ConfigValues = {
    platformName: p.platformName,
    organizationName: p.organizationName,
    logo: p.logo ?? '',
    theme: u.theme,
    compactMode: u.compactMode,
    rowsPerPage: String(u.defaultRowsPerPage),
    enableTooltips: u.enableTooltips,
    defaultStatus: d.defaultStatus,
    autoSaveInterval: labelFor(AUTO_SAVE_SECONDS, d.autoSaveIntervalSeconds) ?? `${d.autoSaveIntervalSeconds} seconds`,
    confirmBeforeDelete: d.requireDeleteConfirmation
  };
  // Optional values the API left empty keep the client default
  if (p.defaultLandingPage) out['defaultLandingPage'] = p.defaultLandingPage;
  if (r.country) out['country'] = r.country;
  if (r.timeZone) out['timeZone'] = r.timeZone;
  if (r.dateFormat) out['dateFormat'] = r.dateFormat;
  if (r.currency) {
    const code = r.currency;
    out['currency'] = settings.find((s) => s.key === 'currency')?.options?.find((o) => o.startsWith(code)) ?? code;
  }
  if (r.language) out['language'] = r.language;
  return out;
}

/** Organization Profile values from the Master Data organization. */
function orgValues(o: OrganizationDto): ConfigValues {
  return {
    legalName: o.organizationName ?? '',
    tradeLicense: o.registrationNumber ?? '',
    headquarters: o.address?.city ?? '',
    contactEmail: o.contact?.email ?? '',
    contactPhone: o.contact?.phone ?? '',
    website: o.website ?? ''
  };
}

function errorMessage(err: HttpErrorResponse): string {
  const errors = err.error?.errors as Record<string, string[]> | undefined;
  const first = errors ? Object.values(errors)[0]?.[0] : null;
  return first || err.error?.message || err.error?.title || 'The configuration service could not be reached.';
}

/**
 * System Configuration data. Saved on the server:
 *  - General Settings (platform identity, regional defaults, user experience, data & records): Administration
 *    /api/administration/system-configuration;
 *  - Organization Profile (legal name, trade licence, headquarters, contact email / phone, website): the tenant's
 *    Master Data organization (/api/master-data/organizations/{id});
 *  - Code Prefixes & Sequences (asset / work order / inventory prefix, digits, reset): Administration numbering
 *    sequences (/api/administration/numbering-sequences);
 *  - change history: the audit trail those saves write (GET /api/eventlogs, through the gateway).
 * Every other setting and the review queue only last for the browser session (no API stores them); those settings
 * are flagged `stored: false` so the page can say so.
 */
@Injectable({ providedIn: 'root' })
export class SystemConfigurationService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly apiUrl = `${environment.administrationApiUrl}api/administration/system-configuration`;
  private readonly orgUrl = `${environment.masterDataApiUrl}api/master-data/organizations`;
  private readonly seqUrl = `${environment.administrationApiUrl}api/administration/numbering-sequences`;
  private readonly eventLogUrl = `${environment.eventLogApiUrl}api/eventlogs`;
  private readonly tenant = { tenantId: environment.tenantId, clientId: environment.clientId };

  readonly sections = signal<ConfigSection[]>(markStored(buildSections()));
  /** Changes made in this session (review queue, session-only settings, and saves until history reloads). */
  readonly changes = signal<ConfigChange[]>([]);
  /** Saved changes from the audit trail. */
  readonly serverHistory = signal<ConfigChange[]>([]);
  readonly loading = signal(false);

  readonly pending = computed(() => this.changes().filter((c) => c.status === 'Pending'));

  /** Changes newest first: this session's changes and the audit trail of saved ones. */
  readonly history = computed(() => [...this.changes(), ...this.serverHistory()].sort((a, b) => b.date.localeCompare(a.date)));

  /** The tenant's Master Data organization (Organization Profile is saved on it); null when there is none. */
  private organization: OrganizationDto | null = null;
  /** Numbering sequence per prefix setting key, once created. */
  private sequences = new Map<string, NumberingSequenceDto>();
  private nextChange = 1;

  allSettings(): ConfigSetting[] {
    return this.sections().flatMap((s) => s.groups.flatMap((g) => g.settings));
  }

  setting(key: string): ConfigSetting | undefined {
    return this.allSettings().find((s) => s.key === key);
  }

  sectionOf(key: string): ConfigSection | undefined {
    return this.sections().find((s) => s.groups.some((g) => g.settings.some((x) => x.key === key)));
  }

  /** Saved value of every setting, keyed by setting key. */
  values(): ConfigValues {
    const out: ConfigValues = {};
    for (const s of this.allSettings()) out[s.key] = s.value;
    return out;
  }

  /** Security & audit settings currently switched on / configured. */
  activePolicies(): number {
    return this.sections()
      .filter((s) => s.requiresApproval)
      .flatMap((s) => s.groups.flatMap((g) => g.settings))
      .filter((s) => isActiveValue(s.value)).length;
  }

  /**
   * Loads everything stored on the server: general settings (a tenant with nothing saved yet, 404, keeps the
   * defaults), the organization profile, the numbering sequences and the change history. Each part loads on its own,
   * so one failing API doesn't hide the others; the first error is reported.
   */
  load(): Observable<void> {
    this.loading.set(true);
    const errors: string[] = [];
    const soft = <T>(o: Observable<T>, fallback: T, what: string) =>
      o.pipe(catchError((err: HttpErrorResponse) => {
        if (err.status !== 404) errors.push(`${what}: ${errorMessage(err)}`);
        return of(fallback);
      }));
    return forkJoin({
      config: soft(this.http.get<SystemConfigurationDto>(this.apiUrl, { params: this.tenant }).pipe(map((d): SystemConfigurationDto | null => d)), null, 'General settings'),
      orgs: soft(this.http.get<OrganizationDto[] | { items: OrganizationDto[] }>(this.orgUrl).pipe(map(asList)), [] as OrganizationDto[], 'Organization profile'),
      seqs: soft(this.http.get<NumberingSequenceDto[] | { items: NumberingSequenceDto[] }>(this.seqUrl, { params: this.tenant }).pipe(map(asList)), [] as NumberingSequenceDto[], 'Code prefixes'),
      history: this.loadHistory()
    }).pipe(
      map(({ config, orgs, seqs, history }) => {
        if (config) this.applyDto(config);
        this.applyOrganization(pickOrganization(orgs));
        this.applySequences(seqs);
        this.serverHistory.set(history);
        this.loading.set(false);
        if (errors.length) throw new Error(errors[0]);
      }),
      catchError((err: Error) => {
        this.loading.set(false);
        return throwError(() => err);
      })
    );
  }

  /**
   * Saves changed settings. Settings in a section that requires approval are queued as "Pending"; everything else is
   * applied straight away. Applied server-stored settings are saved to their API (general settings in one POST, the
   * organization in one PUT, each changed numbering sequence) and nothing is recorded if a save fails.
   */
  submit(entries: { key: string; value: ConfigValue }[], reason: string): Observable<ConfigChange[]> {
    const user = CURRENT_USER.name || this.auth.getUserEmail() || '';
    const changes: ConfigChange[] = [];
    for (const { key, value } of entries) {
      const s = this.setting(key);
      const section = this.sectionOf(key);
      if (!s || !section || s.value === value) continue;
      changes.push({
        id: `CHG-${String(this.nextChange++).padStart(4, '0')}`,
        key,
        setting: s.label,
        section: section.title,
        oldValue: s.value,
        newValue: value,
        updatedBy: user,
        date: new Date().toISOString(),
        reason,
        status: section.requiresApproval ? 'Pending' : 'Applied'
      });
    }
    const applied = changes.filter((c) => c.status === 'Applied');
    const next = this.values();
    applied.forEach((c) => (next[c.key] = c.newValue));

    const saves: Observable<unknown>[] = [];
    if (applied.some((c) => API_SETTING_KEYS.has(c.key))) {
      saves.push(this.http.post<SystemConfigurationDto>(this.apiUrl, toRequest(next, this.auth.getUserEmail())).pipe(tap((dto) => this.applyDto(dto))));
    }
    if (applied.some((c) => ORG_SETTING_KEYS.has(c.key))) {
      const org = this.organization;
      if (!org) return throwError(() => new Error('No organization exists in Master Data yet. Create it in Master Data > Organization first.'));
      saves.push(this.http.put<OrganizationDto>(`${this.orgUrl}/${encodeURIComponent(org.id)}`, this.orgRequest(org, next)).pipe(tap((o) => this.applyOrganization(o))));
    }
    if (applied.some((c) => SEQUENCE_SETTING_KEYS.has(c.key))) {
      const shared = ['sequenceDigits', 'sequenceReset'].some((k) => applied.some((c) => c.key === k));
      for (const s of SEQUENCES) {
        if (!shared && !applied.some((c) => c.key === s.key)) continue;
        const parts = splitPrefix(next[s.key]);
        if (!parts) return throwError(() => new Error(`${this.setting(s.key)?.label ?? s.key}: use letters / digits followed by up to 3 symbols, e.g. AST-`));
        saves.push(this.saveSequence(s, parts, next));
      }
    }

    const record = (): ConfigChange[] => {
      // session-only settings take effect here; server-stored ones were applied from the API's replies
      for (const c of applied) if (!STORED_SETTING_KEYS.has(c.key)) this.apply(c.key, c.newValue, user);
      for (const c of applied) if (SEQUENCE_SETTING_KEYS.has(c.key)) this.apply(c.key, c.newValue, user);
      // saved changes show up in the audit trail; keep only the others in this session's list
      const sessionOnly = changes.filter((c) => c.status !== 'Applied' || !STORED_SETTING_KEYS.has(c.key));
      this.changes.update((list) => [...sessionOnly, ...list]);
      return changes;
    };
    if (!saves.length) return of(record());
    return concat(...saves).pipe(
      last(),
      switchMap(() => this.loadHistory()),
      map((history) => {
        this.serverHistory.set(history);
        return record();
      }),
      catchError((err: unknown) => throwError(() => new Error(err instanceof HttpErrorResponse ? errorMessage(err) : String((err as Error)?.message ?? err))))
    );
  }

  // Only approval sections are queued and none of them are stored on the server, so approving stays client-side
  approve(id: string): void {
    const change = this.changes().find((c) => c.id === id);
    if (!change || change.status !== 'Pending') return;
    this.apply(change.key, change.newValue, change.updatedBy);
    this.setStatus(id, 'Applied');
  }

  reject(id: string): void {
    this.setStatus(id, 'Rejected');
  }

  private setStatus(id: string, status: ConfigChange['status']): void {
    this.changes.update((list) => list.map((c) => (c.id === id ? { ...c, status } : c)));
  }

  // ===== Organization profile (Master Data) =====

  /** PUT body: the organization as stored, with the Organization Profile values replaced. */
  private orgRequest(o: OrganizationDto, v: ConfigValues): Record<string, unknown> {
    const address = o.address ?? { line1: null, line2: null, city: null, state: null, country: null, postalCode: null };
    const contact = o.contact ?? { name: null, designation: null, email: null, phoneCountry: null, phone: null };
    return {
      organizationName: text(v['legalName']) ?? o.organizationName,
      code: o.code,
      logoUrl: o.logoUrl,
      type: o.type,
      parentOrganization: o.parentOrganization ?? null,
      businessUnits: o.businessUnits ?? [],
      sites: o.sites ?? [],
      registrationNumber: text(v['tradeLicense']),
      website: text(v['website']),
      description: o.description,
      address: { ...address, city: text(v['headquarters']) },
      contact: { ...contact, email: text(v['contactEmail']), phone: text(v['contactPhone']) },
      settings: o.settings ?? null,
      status: o.status,
      updatedBy: this.auth.getUserEmail()
    };
  }

  private applyOrganization(o: OrganizationDto | null): void {
    this.organization = o;
    if (!o) return;
    this.setValues(orgValues(o), o.updatedBy || o.createdBy || '', o.updatedAt || o.createdAt || '');
  }

  // ===== Code prefixes (numbering sequences) =====

  private applySequences(list: NumberingSequenceDto[]): void {
    this.sequences.clear();
    for (const s of SEQUENCES) {
      const found = list.find((x) => x.sequenceCode === s.code) ?? list.find((x) => x.recordType?.toLowerCase() === s.recordType.toLowerCase());
      if (found) this.sequences.set(s.key, found);
    }
    const values: ConfigValues = {};
    let stamp: NumberingSequenceDto | undefined;
    for (const s of SEQUENCES) {
      const q = this.sequences.get(s.key);
      if (!q) continue;
      values[s.key] = `${q.prefix}${q.separator ?? ''}`;
      stamp ??= q;
    }
    if (stamp) {
      values['sequenceDigits'] = stamp.numberLength;
      values['sequenceReset'] = stamp.resetFrequency;
    }
    if (Object.keys(values).length) this.setValues(values, stamp?.updatedBy || stamp?.createdBy || '', stamp?.updatedAt || stamp?.createdAt || '');
  }

  /** Creates the prefix's numbering sequence on first save, then updates it. */
  private saveSequence(s: (typeof SEQUENCES)[number], parts: { prefix: string; separator: string }, v: ConfigValues): Observable<NumberingSequenceDto> {
    const existing = this.sequences.get(s.key);
    const body = {
      sequenceName: existing?.sequenceName ?? s.name,
      sequenceCode: existing?.sequenceCode ?? s.code,
      module: existing?.module ?? s.module,
      recordType: existing?.recordType ?? s.recordType,
      prefix: parts.prefix,
      separator: parts.separator,
      numberLength: Number(v['sequenceDigits']) || 6,
      startNumber: existing?.startNumber ?? 1,
      incrementBy: existing?.incrementBy ?? 1,
      resetFrequency: String(v['sequenceReset'] ?? 'Never'),
      scope: existing?.scope ?? 'Organization',
      status: existing?.status ?? 'Active'
    };
    const call = existing
      ? this.http.put<NumberingSequenceDto>(`${this.seqUrl}/${encodeURIComponent(existing.sequenceId)}`, body)
      : this.http.post<NumberingSequenceDto>(this.seqUrl, { ...body, createdBy: this.auth.getUserEmail(), ...this.tenant });
    return call.pipe(tap((saved) => this.sequences.set(s.key, saved)));
  }

  // ===== Change history (audit trail) =====

  /** Saves recorded by the audit trail for system configuration, organizations and numbering sequences. */
  private loadHistory(): Observable<ConfigChange[]> {
    return forkJoin(
      HISTORY_ENTITY_TYPES.map(({ type, section }) =>
        this.http.get<{ items: EventLogDto[] }>(this.eventLogUrl, { params: { entityType: type, page: 1, pageSize: 100 } }).pipe(
          map((res) =>
            (res.items ?? []).map(
              (e): ConfigChange => ({
                id: `LOG-${e.id}`,
                key: '',
                setting: type === 'SystemConfiguration' ? 'General settings' : type === 'Organization' ? `Organization profile: ${e.entityName}` : `Code prefix: ${e.entityName}`,
                section,
                oldValue: '',
                newValue: e.message || e.action,
                updatedBy: e.performedBy || '',
                date: e.createdAt || '',
                reason: e.action,
                status: 'Applied'
              })
            )
          ),
          catchError(() => of([] as ConfigChange[]))
        )
      )
    ).pipe(map((lists) => lists.flat()));
  }

  // ===== Applying values =====

  /** Copies the API values into the settings, stamped with who saved them last. */
  private applyDto(dto: SystemConfigurationDto): void {
    this.setValues(toValues(dto, this.allSettings()), dto.updatedBy || dto.createdBy || '', dto.updatedAt || dto.createdAt || '');
  }

  private setValues(values: ConfigValues, by: string, at: string): void {
    const when = at || new Date().toISOString();
    this.sections.update((sections) =>
      sections.map((sec) => ({
        ...sec,
        groups: sec.groups.map((g) => ({
          ...g,
          settings: g.settings.map((s) =>
            s.key in values ? { ...s, value: values[s.key], updatedBy: by || s.updatedBy, updatedAt: when, effectiveDate: when.slice(0, 10) } : s
          )
        }))
      }))
    );
  }

  private apply(key: string, value: ConfigValue, by: string): void {
    this.setValues({ [key]: value }, by, new Date().toISOString());
  }
}

/** A list response as an array (some list endpoints return { items }). */
function asList<T>(res: T[] | { items: T[] } | null): T[] {
  return Array.isArray(res) ? res : res?.items ?? [];
}

/** The organization the profile describes: the head office, else the first active one. */
function pickOrganization(list: OrganizationDto[]): OrganizationDto | null {
  const active = list.filter((o) => !o.status || o.status === 'Active');
  return active.find((o) => /head/i.test(o.type ?? '')) ?? active[0] ?? list[0] ?? null;
}

/** Flags which settings are saved on the server. */
function markStored(sections: ConfigSection[]): ConfigSection[] {
  return sections.map((sec) => ({
    ...sec,
    groups: sec.groups.map((g) => ({ ...g, settings: g.settings.map((s) => ({ ...s, stored: STORED_SETTING_KEYS.has(s.key) })) }))
  }));
}

export function isActiveValue(v: ConfigValue): boolean {
  if (typeof v === 'boolean') return v;
  if (typeof v === 'number') return v > 0;
  return v !== '' && v !== 'None' && v !== 'Optional';
}
