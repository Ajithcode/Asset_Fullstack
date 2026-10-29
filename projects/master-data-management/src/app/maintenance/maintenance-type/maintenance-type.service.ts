import { Injectable } from '@angular/core';
import { ApiCodeStore, MasterDto } from '../../shared/api-code-store';
import { blankMaintenanceType } from './maintenance-type.data';
import { MaintenanceTypeRecord } from './maintenance-type.model';

interface MaintenanceTypeDto extends MasterDto {
  maintenanceTypeName: string;
  description: string;
  color: string | null;
}

/**
 * Maintenance types from {projectApiUrl}api/master-data/maintenance-types. Work types and task
 * templates refer to them by name. The settings switches and default priority are UI-only for now.
 */
@Injectable({ providedIn: 'root' })
export class MaintenanceTypeService extends ApiCodeStore<MaintenanceTypeRecord, MaintenanceTypeDto> {
  constructor() {
    super('maintenance-types');
    this.load();
  }

  /** Active names, plus `keep` so an existing choice stays selectable. */
  activeNames(keep = ''): string[] {
    return this.records.filter((r) => r.status === 'Active' || r.name === keep).map((r) => r.name);
  }

  colorOf(name: string): string {
    return this.findByName(name)?.color ?? '#94a3b8';
  }

  /** The API's {id, name} reference for a type name, or null. */
  refOf(name: string) {
    const type = this.findByName(name);
    return type ? { id: type.id, name: type.name } : null;
  }

  protected toRecord(dto: MaintenanceTypeDto, cached: MaintenanceTypeRecord | undefined): MaintenanceTypeRecord {
    const base = cached ?? blankMaintenanceType();
    return { ...base, name: dto.maintenanceTypeName, description: dto.description ?? '', color: dto.color?.toLowerCase() ?? base.color };
  }

  protected toRequest(r: MaintenanceTypeRecord) {
    return { maintenanceTypeName: r.name, code: r.code, description: r.description, color: r.color };
  }
}
