import { Injectable } from '@angular/core';
import { CodeStore } from '../../shared/code-store';
import { initialTaskTemplates } from './task-template.data';
import { TaskTemplateRecord } from './task-template.model';

@Injectable({ providedIn: 'root' })
export class TaskTemplateService extends CodeStore<TaskTemplateRecord> {
  constructor() {
    super(initialTaskTemplates());
  }

  usesType(name: string): number {
    return this.records.filter((r) => r.maintenanceType === name).length;
  }

  renameType(from: string, to: string): void {
    if (from === to) return;
    this.records = this.records.map((r) => (r.maintenanceType === from ? { ...r, maintenanceType: to } : r));
  }
}
