import { Injectable } from '@angular/core';
import { CodeStore } from '../shared/code-store';
import { initialChartTypes, initialSettings, initialSyncStatuses, initialUpdateSources } from './system-config.data';
import { ChartTypeRecord, SyncStatusRecord, SystemSettings, UpdateSourceRecord } from './system-config.model';

/* DEMO MODE: in-memory system configuration; replace with API calls when the backend is ready. */

@Injectable({ providedIn: 'root' })
export class SyncStatusService extends CodeStore<SyncStatusRecord> {
  constructor() {
    super(initialSyncStatuses());
  }
}

@Injectable({ providedIn: 'root' })
export class ChartTypeService extends CodeStore<ChartTypeRecord> {
  constructor() {
    super(initialChartTypes());
  }
}

@Injectable({ providedIn: 'root' })
export class UpdateSourceService extends CodeStore<UpdateSourceRecord> {
  constructor() {
    super(initialUpdateSources());
  }
}

@Injectable({ providedIn: 'root' })
export class SystemSettingsService {
  private settings: SystemSettings = initialSettings();
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

  save(settings: SystemSettings, stamp: string): void {
    this.settings = structuredClone(settings);
    this.savedOn = stamp;
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
