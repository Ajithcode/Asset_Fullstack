import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, throwError } from 'rxjs';
import { environment } from 'shared-ui';
import { CURRENCIES } from '../organization/organization/organization.data';
import { ApiCodeStore } from '../shared/code-store';
import { MDM_API, MdmApi, MdmAudit } from '../shared/mdm-api';
import { DATE_FORMATS, LANGUAGE_CODES, TIME_FORMATS, TIME_ZONE_IDS, defaultSettings } from './system-config.data';
import { ChartPreview, ChartTypeRecord, ConfigStatus, SyncStatusRecord, SystemSettings, UpdateSourceRecord } from './system-config.model';

/* System configuration masters and settings, backed by /api/master-data. */

/** `yyyy-mm-ddThh:mm` from an API timestamp, as the pages show it. */
function stampOf(value: string | null | undefined): string {
  return (value ?? '').slice(0, 16);
}

function statusOf(value: string | null): ConfigStatus {
  return value === 'Inactive' ? 'Inactive' : 'Active';
}

/** Code-keyed master with a status PATCH. */
abstract class ConfigStore<T extends { code: string; name: string }, TDto extends MdmAudit> extends ApiCodeStore<T, TDto> {
  setStatus(record: T, status: ConfigStatus): Observable<T> {
    const id = this.idOf(record.code);
    if (!id) return throwError(() => new Error('Record not found.'));
    return this.api.setStatus<TDto>(this.resource, id, status).pipe(
      map((dto) => {
        const saved = this.toRecord(dto);
        this.save(saved, record.code);
        return saved;
      })
    );
  }
}

interface SyncStatusDto extends MdmAudit {
  statusName: string;
  code: string;
  description: string | null;
  color: string | null;
  isSuccess: boolean;
}

@Injectable({ providedIn: 'root' })
export class SyncStatusService extends ConfigStore<SyncStatusRecord, SyncStatusDto> {
  protected readonly resource = 'api-sync-statuses';

  constructor() {
    super();
  }

  protected toRecord(dto: SyncStatusDto): SyncStatusRecord {
    return {
      code: dto.code,
      name: dto.statusName,
      description: dto.description ?? '',
      // The API stores colours in upper case; the swatches are lower case
      color: (dto.color ?? '').toLowerCase(),
      isSuccess: dto.isSuccess,
      status: statusOf(dto.status),
      createdOn: stampOf(dto.createdAt),
      updatedOn: stampOf(dto.updatedAt)
    };
  }

  protected toRequest(r: SyncStatusRecord): object {
    return { statusName: r.name, code: r.code, description: r.description, color: r.color, isSuccess: r.isSuccess, status: r.status };
  }
}

interface ChartTypeDto extends MdmAudit {
  chartTypeName: string;
  code: string;
  category: string;
  description: string | null;
  iconUrl: string | null;
  previewType: string | null;
}

@Injectable({ providedIn: 'root' })
export class ChartTypeService extends ConfigStore<ChartTypeRecord, ChartTypeDto> {
  protected readonly resource = 'chart-types';

  constructor() {
    super();
  }

  protected toRecord(dto: ChartTypeDto): ChartTypeRecord {
    return {
      code: dto.code,
      name: dto.chartTypeName,
      category: dto.category,
      description: dto.description ?? '',
      preview: (dto.previewType as ChartPreview) || 'column',
      image: dto.iconUrl,
      status: statusOf(dto.status),
      createdOn: stampOf(dto.createdAt),
      updatedOn: stampOf(dto.updatedAt)
    };
  }

  protected toRequest(r: ChartTypeRecord): object {
    return { chartTypeName: r.name, code: r.code, category: r.category, description: r.description, iconUrl: r.image, previewType: r.preview, status: r.status };
  }
}

interface UpdateSourceDto extends MdmAudit {
  sourceName: string;
  code: string;
  type: string;
  description: string | null;
}

@Injectable({ providedIn: 'root' })
export class UpdateSourceService extends ConfigStore<UpdateSourceRecord, UpdateSourceDto> {
  protected readonly resource = 'update-sources';

  constructor() {
    super();
  }

  protected toRecord(dto: UpdateSourceDto): UpdateSourceRecord {
    return {
      code: dto.code,
      name: dto.sourceName,
      type: dto.type,
      description: dto.description ?? '',
      status: statusOf(dto.status),
      createdOn: stampOf(dto.createdAt),
      updatedOn: stampOf(dto.updatedAt)
    };
  }

  protected toRequest(r: UpdateSourceRecord): object {
    return { sourceName: r.name, code: r.code, type: r.type, description: r.description, status: r.status };
  }
}

/** General section as the API stores it: codes instead of the labels the page shows. */
interface GeneralDto {
  systemName: string;
  systemUrl: string | null;
  defaultTimeZone: string;
  dateFormat: string;
  timeFormat: string;
  defaultLanguage: string;
  defaultCurrency: string;
  recordsPerPage: number;
  enableAuditLog: boolean;
  allowOfflineMode: boolean;
  systemMaintenanceMode: boolean;
}

type SettingsDto = { general: GeneralDto; updatedAt: string | null } & { [K in Exclude<keyof SystemSettings, 'general'>]?: Partial<SystemSettings[K]> | null };

/** Label whose value starts with `code`, e.g. 'AED' -> 'AED - UAE Dirham'; falls back to the code. */
function labelFor(labels: string[], code: string, keyOf: (label: string) => string): string {
  return labels.find((l) => keyOf(l) === code) ?? code;
}

const firstWord = (label: string) => label.split(' ')[0];
const timeKey = (label: string) => label.replace(/ \(.*$/, '').replace(' ', '');

/** One settings document per client, read and saved whole. */
@Injectable({ providedIn: 'root' })
export class SystemSettingsService {
  private readonly http = inject(HttpClient);
  private readonly api = inject(MdmApi);
  private settings: SystemSettings = defaultSettings();
  private savedOn = '';

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

  /** GET the client's settings; sections never saved come back with the API defaults. */
  load(): Observable<SystemSettings> {
    const params = new HttpParams().set('clientId', environment.clientId);
    return this.http.get<SettingsDto>(`${MDM_API}system-settings`, { params }).pipe(map((dto) => this.adopt(dto)));
  }

  save(settings: SystemSettings): Observable<SystemSettings> {
    const { general, ...sections } = settings;
    const body = {
      clientId: environment.clientId,
      tenantId: environment.tenantId,
      updatedBy: this.api.actor(),
      general: {
        systemName: general.systemName,
        systemUrl: general.systemUrl,
        defaultTimeZone: TIME_ZONE_IDS[general.timeZone] ?? general.timeZone,
        dateFormat: firstWord(general.dateFormat),
        timeFormat: timeKey(general.timeFormat),
        defaultLanguage: LANGUAGE_CODES[general.language] ?? general.language,
        defaultCurrency: firstWord(general.currency),
        recordsPerPage: general.recordsPerPage,
        enableAuditLog: general.auditLog,
        allowOfflineMode: general.offlineMode,
        systemMaintenanceMode: general.maintenanceMode
      } satisfies GeneralDto,
      ...sections
    };
    return this.http.put<SettingsDto>(`${MDM_API}system-settings`, body).pipe(map((dto) => this.adopt(dto)));
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

  private adopt(dto: SettingsDto): SystemSettings {
    const defaults = defaultSettings();
    const g = dto.general;
    const timeZone = Object.keys(TIME_ZONE_IDS).find((label) => TIME_ZONE_IDS[label] === g.defaultTimeZone) ?? g.defaultTimeZone;
    const language = Object.keys(LANGUAGE_CODES).find((name) => LANGUAGE_CODES[name] === g.defaultLanguage) ?? g.defaultLanguage;
    const settings: SystemSettings = {
      ...defaults,
      general: {
        systemName: g.systemName,
        systemUrl: g.systemUrl ?? '',
        timeZone,
        dateFormat: labelFor(DATE_FORMATS, g.dateFormat, firstWord),
        timeFormat: labelFor(TIME_FORMATS, g.timeFormat, timeKey),
        language,
        currency: labelFor(CURRENCIES, g.defaultCurrency, firstWord),
        recordsPerPage: g.recordsPerPage,
        auditLog: g.enableAuditLog,
        offlineMode: g.allowOfflineMode,
        maintenanceMode: g.systemMaintenanceMode
      }
    };
    // Other sections are stored as the page sends them; missing or null values keep the page defaults
    for (const key of Object.keys(defaults) as (keyof SystemSettings)[]) {
      const section = dto[key as Exclude<keyof SystemSettings, 'general'>];
      if (key === 'general' || !section) continue;
      const kept = Object.fromEntries(Object.entries(section).filter(([, v]) => v !== null && v !== undefined));
      (settings as unknown as Record<string, object>)[key] = { ...defaults[key], ...kept };
    }
    this.settings = settings;
    this.savedOn = stampOf(dto.updatedAt);
    return settings;
  }
}
