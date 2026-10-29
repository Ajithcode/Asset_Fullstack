import { Injectable, inject } from '@angular/core';
import { ApiCodeStore, MasterDto } from '../../shared/api-code-store';
import { MaintenanceTypeService } from '../maintenance-type/maintenance-type.service';
import { blankTaskTemplate } from './task-template.data';
import { TaskTemplateRecord } from './task-template.model';

interface ReferenceDto {
  id: string | null;
  name: string;
}

interface StepDto {
  name: string;
  description: string | null;
  minutes: number | null;
  required: boolean | null;
}

interface TaskTemplateDto extends MasterDto {
  templateName: string;
  maintenanceType: ReferenceDto | null;
  // In hours
  estimatedDuration: number | null;
  description: string | null;
  tasks: StepDto[];
}

/**
 * Task templates from {projectApiUrl}api/master-data/task-templates. The maintenance type is shown
 * by name and linked by id; each task's text is stored as the step name.
 */
@Injectable({ providedIn: 'root' })
export class TaskTemplateService extends ApiCodeStore<TaskTemplateRecord, TaskTemplateDto> {
  private readonly types = inject(MaintenanceTypeService);

  constructor() {
    super('task-templates');
    this.load();
  }

  usesType(name: string): number {
    return this.records.filter((r) => r.maintenanceType === name).length;
  }

  // The API renames the type on templates; this keeps the cached rows in step
  renameType(from: string, to: string): void {
    if (from === to) return;
    this.records = this.records.map((r) => (r.maintenanceType === from ? { ...r, maintenanceType: to } : r));
  }

  protected toRecord(dto: TaskTemplateDto, cached: TaskTemplateRecord | undefined): TaskTemplateRecord {
    const base = cached ?? blankTaskTemplate();
    return {
      ...base,
      name: dto.templateName,
      maintenanceType: dto.maintenanceType?.name ?? '',
      estimatedHours: dto.estimatedDuration ?? base.estimatedHours,
      description: dto.description ?? '',
      tasks: dto.tasks.map((t) => ({ description: t.name, minutes: t.minutes ?? 0, required: t.required ?? true }))
    };
  }

  protected toRequest(r: TaskTemplateRecord) {
    return {
      templateName: r.name,
      code: r.code,
      maintenanceType: this.types.refOf(r.maintenanceType),
      estimatedDuration: r.estimatedHours,
      description: r.description,
      tasks: r.tasks.map((t) => ({ name: t.description, minutes: t.minutes, required: t.required }))
    };
  }
}
