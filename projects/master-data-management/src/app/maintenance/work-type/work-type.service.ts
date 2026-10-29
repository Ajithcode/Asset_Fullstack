import { Injectable, inject } from '@angular/core';
import { ApiCodeStore, MasterDto } from '../../shared/api-code-store';
import { MaintenanceTypeService } from '../maintenance-type/maintenance-type.service';
import { blankWorkType } from './work-type.data';
import { WorkTypeRecord } from './work-type.model';

interface ReferenceDto {
  id: string | null;
  name: string;
}

interface WorkTypeDto extends MasterDto {
  workTypeName: string;
  maintenanceType: ReferenceDto | null;
  description: string | null;
  // In hours
  estimatedDuration: number | null;
}

/**
 * Work types from {projectApiUrl}api/master-data/work-types. The maintenance type is shown by name
 * and linked by id. Default priority, required skill, checklist and permit are UI-only for now.
 */
@Injectable({ providedIn: 'root' })
export class WorkTypeService extends ApiCodeStore<WorkTypeRecord, WorkTypeDto> {
  private readonly types = inject(MaintenanceTypeService);

  constructor() {
    super('work-types');
    this.load();
  }

  usesType(name: string): number {
    return this.records.filter((r) => r.maintenanceType === name).length;
  }

  // The API renames the type on work types; this keeps the cached rows in step
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

  protected toRecord(dto: WorkTypeDto, cached: WorkTypeRecord | undefined): WorkTypeRecord {
    const base = cached ?? blankWorkType();
    return {
      ...base,
      name: dto.workTypeName,
      maintenanceType: dto.maintenanceType?.name ?? '',
      description: dto.description ?? '',
      durationHours: dto.estimatedDuration ?? base.durationHours
    };
  }

  protected toRequest(r: WorkTypeRecord) {
    return {
      workTypeName: r.name,
      code: r.code,
      maintenanceType: this.types.refOf(r.maintenanceType),
      description: r.description,
      estimatedDuration: r.durationHours
    };
  }
}
