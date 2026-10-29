import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, ReplaySubject, map, tap } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { CURRENCIES } from '../organization/organization/organization.data';
import { apiErrorMessage } from '../shared/api-error';
import { ApiCodeStore, MasterDto } from '../shared/api-code-store';
import { DATE_FORMATS, LANGUAGES, TIME_FORMATS, TIME_ZONES, blankChartType, blankSyncStatus, blankUpdateSource, initialSettings } from './system-config.data';
import { ChartTypeRecord, SyncStatusRecord, SystemSettings, UpdateSourceRecord } from './system-config.model';

/*
 * System configuration masters from {projectApiUrl}api/master-data/... Fields the API doesn't
 * store yet are kept for the session only.
 */

interface SyncStatusDto extends MasterDto {
  statusName: string;
  description: string | null;
  color: string | null;
  isSuccess: boolean;
}

@Injectable({ providedIn: 'root' })
export class SyncStatusService extends ApiCodeStore<SyncStatusRecord, SyncStatusDto> {
  constructor() {
    super('api-sync-statuses');
    this.load();
  }

  protected toRecord(dto: SyncStatusDto, cached: SyncStatusRecord | undefined): SyncStatusRecord {
    const base = cached ?? blankSyncStatus();
    return { ...base, name: dto.statusName, description: dto.description ?? '', color: dto.color?.toLowerCase() ?? base.color, isSuccess: dto.isSuccess };
  }

  protected toRequest(r: SyncStatusRecord) {
    return { statusName: r.name, code: r.code, description: r.description, color: r.color, isSuccess: r.isSuccess };
  }
}

interface ChartTypeDto extends MasterDto {
  chartTypeName: string;
  category: string;
  description: string | null;
  iconUrl: string | null;
}

/** Chart types; the built-in preview choice is UI-only, an uploaded image is stored as the icon. */
@Injectable({ providedIn: 'root' })
export class ChartTypeService extends ApiCodeStore<ChartTypeRecord, ChartTypeDto> {
  constructor() {
    super('chart-types');
    this.load();
  }

  protected toRecord(dto: ChartTypeDto, cached: ChartTypeRecord | undefined): ChartTypeRecord {
    return { ...(cached ?? blankChartType()), name: dto.chartTypeName, category: dto.category, description: dto.description ?? '', image: dto.iconUrl };
  }

  protected toRequest(r: ChartTypeRecord) {
    // TODO: upload through the Media service and send its URL; this sends the picked image as a data URL
    return { chartTypeName: r.name, code: r.code, category: r.category, description: r.description, iconUrl: r.image };
  }
}

interface UpdateSourceDto extends MasterDto {
  sourceName: string;
  type: string;
  description: string | null;
}

@Injectable({ providedIn: 'root' })
export class UpdateSourceService extends ApiCodeStore<UpdateSourceRecord, UpdateSourceDto> {
  constructor() {
    super('update-sources');
    this.load();
  }

  protected toRecord(dto: UpdateSourceDto, cached: UpdateSourceRecord | undefined): UpdateSourceRecord {
    return { ...(cached ?? blankUpdateSource()), name: dto.sourceName, type: dto.type, description: dto.description ?? '' };
  }

  protected toRequest(r: UpdateSourceRecord) {
    return { sourceName: r.name, code: r.code, type: r.type, description: r.description };
  }
}

/** The General section as GET/PUT {projectApiUrl}api/master-data/system-settings stores it. */
interface GeneralSettingsDto {
  systemName: string;
  systemUrl: string | null;
  // IANA id, e.g. Asia/Dubai
  defaultTimeZone: string;
  // .NET pattern, e.g. dd-MMM-yyyy
  dateFormat: string;
  // "24Hour" or "12Hour"
  timeFormat: string;
  // Language code, e.g. en
  defaultLanguage: string;
  // ISO 4217, e.g. AED
  defaultCurrency: string;
  recordsPerPage: number;
  enableAuditLog: boolean;
  allowOfflineMode: boolean;
  systemMaintenanceMode: boolean;
}

interface SystemSettingsDto {
  general: GeneralSettingsDto;
  updatedAt: string | null;
}

// The page's time zone labels and the IANA ids the API stores, in the same order as TIME_ZONES
const TIME_ZONE_IDS = ['Asia/Dubai', 'Asia/Riyadh', 'Asia/Qatar', 'Asia/Kolkata', 'Europe/London', 'America/New_York'];
const LANGUAGE_CODES = ['en', 'ar', 'hi', 'fr'];

/** Picks the label whose stored value matches, or the first label when none does. */
function labelFor(labels: string[], values: string[], value: string): string {
  const i = values.indexOf(value);
  return labels[i >= 0 ? i : 0];
}

/**
 * System settings. Only the General section is stored by the API; the other sections are kept for
 * the session until the backend adds them.
 */
@Injectable({ providedIn: 'root' })
export class SystemSettingsService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly apiUrl = `${environment.projectApiUrl}api/master-data/system-settings`;

  private settings: SystemSettings = initialSettings();
  private savedOn = '';
  loading = false;
  loadError = '';
  private readonly settled = new ReplaySubject<void>(1);
  /** Emits once the first load has finished (or failed). */
  readonly settled$ = this.settled.asObservable();

  constructor() {
    this.load();
  }

  load(): void {
    if (this.loading) return;
    this.loading = true;
    this.loadError = '';
    this.http.get<SystemSettingsDto>(this.apiUrl, { params: { clientId: environment.clientId } }).subscribe({
      next: (dto) => {
        this.apply(dto);
        this.loading = false;
        this.settled.next();
      },
      error: (err) => {
        this.loadError = apiErrorMessage(err);
        this.loading = false;
        this.settled.next();
      }
    });
  }

  /** A copy to edit; nothing changes until `save`. */
  draft(): SystemSettings {
    return structuredClone(this.settings);
  }

  current(): SystemSettings {
    return this.settings;
  }

  lastSaved(): string {
    return this.savedOn;
  }

  /** PUT the General section; the other sections are kept in memory. */
  save(settings: SystemSettings, stamp: string): Observable<SystemSettings> {
    const g = settings.general;
    const general: GeneralSettingsDto = {
      systemName: g.systemName,
      systemUrl: g.systemUrl || null,
      defaultTimeZone: TIME_ZONE_IDS[TIME_ZONES.indexOf(g.timeZone)] ?? TIME_ZONE_IDS[0],
      // "dd-MMM-yyyy (26-Oct-2026)" -> "dd-MMM-yyyy"
      dateFormat: g.dateFormat.split(' (')[0],
      timeFormat: g.timeFormat.startsWith('12') ? '12Hour' : '24Hour',
      defaultLanguage: LANGUAGE_CODES[LANGUAGES.indexOf(g.language)] ?? 'en',
      // "AED - UAE Dirham" -> "AED"
      defaultCurrency: g.currency.slice(0, 3),
      recordsPerPage: g.recordsPerPage,
      enableAuditLog: g.auditLog,
      allowOfflineMode: g.offlineMode,
      systemMaintenanceMode: g.maintenanceMode
    };
    const body = {
      clientId: environment.clientId,
      tenantId: environment.tenantId,
      updatedBy: this.auth.getUserEmail() || 'unknown',
      general
    };
    return this.http.put<SystemSettingsDto>(this.apiUrl, body).pipe(
      tap((dto) => {
        this.settings = structuredClone(settings);
        this.apply(dto);
        this.savedOn = stamp;
      }),
      map(() => this.settings)
    );
  }

  private apply(dto: SystemSettingsDto): void {
    const g = dto.general;
    this.settings = {
      ...this.settings,
      general: {
        ...this.settings.general,
        systemName: g.systemName,
        systemUrl: g.systemUrl ?? '',
        timeZone: labelFor(TIME_ZONES, TIME_ZONE_IDS, g.defaultTimeZone),
        dateFormat: DATE_FORMATS.find((f) => f.split(' (')[0] === g.dateFormat) ?? DATE_FORMATS[0],
        timeFormat: TIME_FORMATS.find((f) => f.startsWith(g.timeFormat === '12Hour' ? '12' : '24')) ?? TIME_FORMATS[0],
        language: labelFor(LANGUAGES, LANGUAGE_CODES, g.defaultLanguage),
        currency: CURRENCIES.find((c) => c.startsWith(g.defaultCurrency)) ?? g.defaultCurrency,
        recordsPerPage: g.recordsPerPage,
        auditLog: g.enableAuditLog,
        offlineMode: g.allowOfflineMode,
        maintenanceMode: g.systemMaintenanceMode
      }
    };
    if (dto.updatedAt) this.savedOn = dto.updatedAt.slice(0, 16).replace('T', ' ');
  }

  /** Default code references, so the referenced master can't be deleted. */
  isDefault(kind: 'updateSource' | 'chartType', code: string): boolean {
    return this.settings.defaults[kind] === code;
  }

  usesSyncStatus(code: string): boolean {
    return this.settings.integration.failedSyncStatus === code;
  }

  usesSource(code: string): boolean {
    return this.settings.defaults.updateSource === code || this.settings.integration.sources.includes(code);
  }
}
