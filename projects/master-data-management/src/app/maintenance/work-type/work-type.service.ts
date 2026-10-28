import { Injectable } from '@angular/core';
import { CodeStore } from '../../shared/code-store';
import { initialWorkTypes } from './work-type.data';
import { WorkTypeRecord } from './work-type.model';

@Injectable({ providedIn: 'root' })
export class WorkTypeService extends CodeStore<WorkTypeRecord> {
  constructor() {
    super(initialWorkTypes());
  }

  usesType(name: string): number {
    return this.records.filter((r) => r.maintenanceType === name).length;
  }

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
}
