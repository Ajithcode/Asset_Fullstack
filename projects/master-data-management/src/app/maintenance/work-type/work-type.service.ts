import { Injectable, inject } from '@angular/core';
import { MdmAudit } from '../../shared/mdm-api';
import { MaintenanceTypeService } from '../maintenance-type/maintenance-type.service';
import { MasterApiStore, stampOf } from '../master-api-store';
import { WorkTypeRecord } from './work-type.model';

interface WorkTypeDto extends MdmAudit {
  workTypeName: string;
  code: string;
  maintenanceType: { id: string | null; name: string } | null;
  description: string | null;
  estimatedDuration: number | null;
  defaultPriority: string | null;
  requiredSkill: string | null;
  checklistCode: string | null;
  permitRequired: boolean;
}

/** Work types (/api/master-data/work-types). The maintenance type is sent as { id, name }. */
@Injectable({ providedIn: 'root' })
export class WorkTypeService extends MasterApiStore<WorkTypeRecord, WorkTypeDto> {
  protected readonly resource = 'work-types';
  private readonly types = inject(MaintenanceTypeService);

  constructor() {
    super();
  }

  usesType(name: string): number {
    return this.records.filter((r) => r.maintenanceType === name).length;
  }

  /** Follows a renamed maintenance type locally; the API renames the stored references itself. */
  renameType(from: string, to: string): void {
    if (from === to) return;
    this.records = this.records.map((r) => (r.maintenanceType === from ? { ...r, maintenanceType: to } : r));
  }

  usesChecklist(code: string): number {
    return this.records.filter((r) => r.checklistCode === code).length;
  }

  renameChecklist(from: string, to: string): void {
    if (from === to) return;
    this.records = this.records.map((r) => (r.checklistCode === from ? { ...r, checklistCode: to } : r));
  }

  usesPriority(name: string): number {
    return this.records.filter((r) => r.defaultPriority === name).length;
  }

  renamePriority(from: string, to: string): void {
    if (from === to) return;
    this.records = this.records.map((r) => (r.defaultPriority === from ? { ...r, defaultPriority: to } : r));
  }

  protected toRecord(dto: WorkTypeDto): WorkTypeRecord {
    return {
      code: dto.code,
      name: dto.workTypeName,
      maintenanceType: dto.maintenanceType?.name ?? '',
      description: dto.description ?? '',
      durationHours: dto.estimatedDuration ?? 0,
      status: dto.status === 'Inactive' ? 'Inactive' : 'Active',
      defaultPriority: dto.defaultPriority ?? '',
      requiredSkill: dto.requiredSkill ?? '',
      checklistCode: dto.checklistCode ?? '',
      permitRequired: dto.permitRequired,
      createdOn: stampOf(dto.createdAt),
      updatedOn: stampOf(dto.updatedAt ?? dto.createdAt)
    };
  }

  protected toRequest(r: WorkTypeRecord): object {
    return {
      workTypeName: r.name,
      code: r.code,
      maintenanceType: r.maintenanceType ? { id: this.types.idOfName(r.maintenanceType), name: r.maintenanceType } : null,
      description: r.description,
      estimatedDuration: r.durationHours,
      status: r.status,
      defaultPriority: r.defaultPriority || null,
      requiredSkill: r.requiredSkill || null,
      checklistCode: r.checklistCode || null,
      permitRequired: r.permitRequired
    };
  }
}
