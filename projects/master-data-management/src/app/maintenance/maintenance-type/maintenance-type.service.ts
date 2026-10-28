import { Injectable } from '@angular/core';
import { CodeStore } from '../../shared/code-store';
import { initialMaintenanceTypes } from './maintenance-type.data';
import { MaintenanceTypeRecord } from './maintenance-type.model';

/** Maintenance types; work types and task templates refer to them by name. */
@Injectable({ providedIn: 'root' })
export class MaintenanceTypeService extends CodeStore<MaintenanceTypeRecord> {
  constructor() {
    super(initialMaintenanceTypes());
  }

  /** Active names, plus `keep` so an existing choice stays selectable. */
  activeNames(keep = ''): string[] {
    return this.records.filter((r) => r.status === 'Active' || r.name === keep).map((r) => r.name);
  }

  colorOf(name: string): string {
    return this.findByName(name)?.color ?? '#94a3b8';
  }
}
