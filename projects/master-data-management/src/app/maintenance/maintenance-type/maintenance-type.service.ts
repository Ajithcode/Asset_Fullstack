import { Injectable } from '@angular/core';
import { MdmAudit } from '../../shared/mdm-api';
import { MasterApiStore, stampOf } from '../master-api-store';
import { MaintenanceTypeRecord } from './maintenance-type.model';

interface MaintenanceTypeDto extends MdmAudit {
  maintenanceTypeName: string;
  code: string;
  description: string;
  color: string | null;
  planned: boolean;
  approvalRequired: boolean;
  countsAsDowntime: boolean;
  defaultPriority: string | null;
}

/** Maintenance types (/api/master-data/maintenance-types); work types and task templates refer to them by name. */
@Injectable({ providedIn: 'root' })
export class MaintenanceTypeService extends MasterApiStore<MaintenanceTypeRecord, MaintenanceTypeDto> {
  protected readonly resource = 'maintenance-types';

  constructor() {
    super();
  }

  /** Active names, plus `keep` so an existing choice stays selectable. */
  activeNames(keep = ''): string[] {
    return this.records.filter((r) => r.status === 'Active' || r.name === keep).map((r) => r.name);
  }

  colorOf(name: string): string {
    return this.findByName(name)?.color ?? '#94a3b8';
  }

  protected toRecord(dto: MaintenanceTypeDto): MaintenanceTypeRecord {
    return {
      code: dto.code,
      name: dto.maintenanceTypeName,
      description: dto.description ?? '',
      color: (dto.color ?? '#94a3b8').toLowerCase(),
      status: dto.status === 'Inactive' ? 'Inactive' : 'Active',
      planned: dto.planned,
      approvalRequired: dto.approvalRequired,
      countsAsDowntime: dto.countsAsDowntime,
      defaultPriority: dto.defaultPriority ?? '',
      createdOn: stampOf(dto.createdAt),
      updatedOn: stampOf(dto.updatedAt ?? dto.createdAt)
    };
  }

  protected toRequest(r: MaintenanceTypeRecord): object {
    return {
      maintenanceTypeName: r.name,
      code: r.code,
      description: r.description,
      color: r.color,
      status: r.status,
      planned: r.planned,
      approvalRequired: r.approvalRequired,
      countsAsDowntime: r.countsAsDowntime,
      defaultPriority: r.defaultPriority || null
    };
  }
}
