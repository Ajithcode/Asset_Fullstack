import { Injectable, inject } from '@angular/core';
import { MdmAudit } from '../../shared/mdm-api';
import { MaintenanceTypeService } from '../maintenance-type/maintenance-type.service';
import { MasterApiStore, stampOf } from '../master-api-store';
import { TaskTemplateRecord } from './task-template.model';

interface TaskTemplateDto extends MdmAudit {
  templateName: string;
  code: string;
  maintenanceType: { id: string | null; name: string } | null;
  estimatedDuration: number | null;
  description: string | null;
  tasks: { name: string; description: string | null; durationMinutes: number | null; isRequired: boolean }[];
}

/** Task templates (/api/master-data/task-templates); tasks are stored as ordered steps. */
@Injectable({ providedIn: 'root' })
export class TaskTemplateService extends MasterApiStore<TaskTemplateRecord, TaskTemplateDto> {
  protected readonly resource = 'task-templates';
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

  protected toRecord(dto: TaskTemplateDto): TaskTemplateRecord {
    return {
      code: dto.code,
      name: dto.templateName,
      maintenanceType: dto.maintenanceType?.name ?? '',
      estimatedHours: dto.estimatedDuration ?? 0,
      description: dto.description ?? '',
      status: dto.status === 'Inactive' ? 'Inactive' : 'Active',
      tasks: (dto.tasks ?? []).map((t) => ({ description: t.name, minutes: t.durationMinutes ?? 0, required: t.isRequired })),
      createdOn: stampOf(dto.createdAt),
      updatedOn: stampOf(dto.updatedAt ?? dto.createdAt)
    };
  }

  protected toRequest(r: TaskTemplateRecord): object {
    return {
      templateName: r.name,
      code: r.code,
      maintenanceType: r.maintenanceType ? { id: this.types.idOfName(r.maintenanceType), name: r.maintenanceType } : null,
      estimatedDuration: r.estimatedHours,
      description: r.description,
      tasks: r.tasks.map((t) => ({ name: t.description, durationMinutes: t.minutes, isRequired: t.required })),
      status: r.status
    };
  }
}
