import { Injectable } from '@angular/core';
import { CodeStore } from '../../shared/code-store';
import { initialPriorities } from './priority-severity.data';
import { LevelKind, PriorityRecord } from './priority-severity.model';

/**
 * Priorities and severities. Names repeat across the two kinds (Critical), so uniqueness is
 * checked per kind; work types refer to a priority by name.
 */
@Injectable({ providedIn: 'root' })
export class PriorityService extends CodeStore<PriorityRecord> {
  constructor() {
    super(initialPriorities());
  }

  isNameTakenIn(kind: LevelKind, name: string, exceptCode: string | null): boolean {
    const wanted = name.trim().toLowerCase();
    return this.records.some((r) => r.kind === kind && r.name.toLowerCase() === wanted && r.code !== exceptCode);
  }

  activePriorityNames(keep = ''): string[] {
    return this.records.filter((r) => r.kind === 'Priority' && (r.status === 'Active' || r.name === keep)).map((r) => r.name);
  }
}
