import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, map, of, throwError } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
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

/** UI time-zone labels and the IANA ids the API stores. */
const TIME_ZONE_IDS: Record<string, string> = {
  '(GMT+04:00) Dubai': 'Asia/Dubai',
  '(GMT+03:00) Riyadh': 'Asia/Riyadh',
  '(GMT+03:00) Doha': 'Asia/Qatar',
  '(GMT+00:00) London': 'Europe/London',
  '(GMT+08:00) Singapore': 'Asia/Singapore',
  '(GMT+05:30) Kolkata': 'Asia/Kolkata',
  '(GMT+00:00) UTC': 'UTC'
};

/** UI auto-save labels and the interval in seconds the API stores (0 = off). */
const AUTO_SAVE_SECONDS: Record<string, number> = {
  Off: 0,
  '15 seconds': 15,
  '30 seconds': 30,
  '1 minute': 60,
  '5 minutes': 300
};

/** Setting keys stored by the Administration API; everything else stays client-side. */
export const API_SETTING_KEYS = new Set([
  'platformName', 'organizationName', 'logo', 'defaultLandingPage',
  'country', 'timeZone', 'dateFormat', 'currency', 'language',
  'theme', 'compactMode', 'rowsPerPage', 'enableTooltips',
  'defaultStatus', 'autoSaveInterval', 'confirmBeforeDelete'
]);

function text(v: ConfigValue | undefined): string | null {
  const s = v === undefined ? '' : String(v).trim();
  return s ? s : null;
}

function labelFor(lookup: Record<string, string | number>, stored: string | number): string | undefined {
  return Object.keys(lookup).find((k) => lookup[k] === stored);
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
      timeZone: TIME_ZONE_IDS[String(v['timeZone'])] ?? text(v['timeZone']),
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
  if (r.timeZone) out['timeZone'] = labelFor(TIME_ZONE_IDS, r.timeZone) ?? r.timeZone;
  if (r.dateFormat) out['dateFormat'] = r.dateFormat;
  if (r.currency) {
    const code = r.currency;
    out['currency'] = settings.find((s) => s.key === 'currency')?.options?.find((o) => o.startsWith(code)) ?? code;
  }
  if (r.language) out['language'] = r.language;
  return out;
}

function errorMessage(err: HttpErrorResponse): string {
  const errors = err.error?.errors as Record<string, string[]> | undefined;
  const first = errors ? Object.values(errors)[0]?.[0] : null;
  return first || err.error?.title || 'The configuration service could not be reached.';
}

// Settings in API_SETTING_KEYS are loaded from / saved to the Administration API. The other
// settings, the review queue and the change history stay in memory: the API has no endpoints for them.
@Injectable({ providedIn: 'root' })
export class SystemConfigurationService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly apiUrl = `${environment.administrationApiUrl}api/administration/system-configuration`;

  readonly sections = signal<ConfigSection[]>(buildSections());
  readonly changes = signal<ConfigChange[]>([]);
  readonly loading = signal(false);

  readonly pending = computed(() => this.changes().filter((c) => c.status === 'Pending'));

  /** Changes newest first. */
  readonly history = computed(() => [...this.changes()].sort((a, b) => b.date.localeCompare(a.date)));

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

  /** GET /api/administration/system-configuration. A tenant with nothing saved yet (404) keeps the defaults. */
  load(): Observable<void> {
    this.loading.set(true);
    const params = { tenantId: environment.tenantId, clientId: environment.clientId };
    return this.http.get<SystemConfigurationDto>(this.apiUrl, { params }).pipe(
      map((dto) => {
        this.applyDto(dto);
        this.loading.set(false);
      }),
      catchError((err: HttpErrorResponse) => {
        this.loading.set(false);
        return err.status === 404 ? of(undefined) : throwError(() => new Error(errorMessage(err)));
      })
    );
  }

  /**
   * Saves changed settings. Settings in a section that requires approval are queued as "Pending";
   * everything else is applied straight away. Applied API-backed settings go out in one
   * POST /api/administration/system-configuration (with the other API-backed values unchanged)
   * and nothing is recorded if it fails.
   */
  submit(entries: { key: string; value: ConfigValue }[], reason: string): Observable<ConfigChange[]> {
    const user = CURRENT_USER.name;
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
    const record = (): ConfigChange[] => {
      for (const c of changes) {
        if (c.status === 'Applied' && !API_SETTING_KEYS.has(c.key)) this.apply(c.key, c.newValue, user);
      }
      this.changes.update((list) => [...changes, ...list]);
      return changes;
    };
    const remote = changes.filter((c) => c.status === 'Applied' && API_SETTING_KEYS.has(c.key));
    if (!remote.length) return of(record());
    const next = this.values();
    remote.forEach((c) => (next[c.key] = c.newValue));
    return this.http.post<SystemConfigurationDto>(this.apiUrl, toRequest(next, this.auth.getUserEmail())).pipe(
      map((dto) => {
        this.applyDto(dto);
        return record();
      }),
      catchError((err: HttpErrorResponse) => throwError(() => new Error(errorMessage(err))))
    );
  }

  // Only approval sections are queued and none of them are API-backed, so approving stays client-side
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

  /** Copies the API values into the settings, stamped with who saved them last. */
  private applyDto(dto: SystemConfigurationDto): void {
    const values = toValues(dto, this.allSettings());
    const by = dto.updatedBy || dto.createdBy || '';
    const at = dto.updatedAt || dto.createdAt || new Date().toISOString();
    this.sections.update((sections) =>
      sections.map((sec) => ({
        ...sec,
        groups: sec.groups.map((g) => ({
          ...g,
          settings: g.settings.map((s) =>
            s.key in values
              ? { ...s, value: values[s.key], updatedBy: by || s.updatedBy, updatedAt: at, effectiveDate: at.slice(0, 10) }
              : s
          )
        }))
      }))
    );
  }

  private apply(key: string, value: ConfigValue, by: string): void {
    const now = new Date().toISOString();
    this.sections.update((sections) =>
      sections.map((sec) => ({
        ...sec,
        groups: sec.groups.map((g) => ({
          ...g,
          settings: g.settings.map((s) =>
            s.key === key ? { ...s, value, updatedBy: by, updatedAt: now, effectiveDate: now.slice(0, 10) } : s
          )
        }))
      }))
    );
  }
}

export function isActiveValue(v: ConfigValue): boolean {
  if (typeof v === 'boolean') return v;
  if (typeof v === 'number') return v > 0;
  return v !== '' && v !== 'None' && v !== 'Optional';
}
