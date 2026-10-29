import { Injectable } from '@angular/core';
import { ApiCodeStore, MasterDto } from '../../shared/api-code-store';
import { blankPriority } from './priority-severity.data';
import { LevelKind, PriorityRecord } from './priority-severity.model';

interface PrioritySeverityDto extends MasterDto {
  name: string;
  // "Priority" or "Severity"
  type: string;
  color: string | null;
  responseTime: string | null;
  description: string | null;
}

/**
 * Priorities and severities from {projectApiUrl}api/master-data/priority-severities. Names repeat
 * across the two kinds (Critical), so uniqueness is checked per kind; work types refer to a
 * priority by name. Usage rules are UI-only for now.
 */
@Injectable({ providedIn: 'root' })
export class PriorityService extends ApiCodeStore<PriorityRecord, PrioritySeverityDto> {
  constructor() {
    super('priority-severities');
    this.load();
  }

  isNameTakenIn(kind: LevelKind, name: string, exceptCode: string | null): boolean {
    const wanted = name.trim().toLowerCase();
    return this.records.some((r) => r.kind === kind && r.name.toLowerCase() === wanted && r.code !== exceptCode);
  }

  activePriorityNames(keep = ''): string[] {
    return this.records.filter((r) => r.kind === 'Priority' && (r.status === 'Active' || r.name === keep)).map((r) => r.name);
  }

  protected toRecord(dto: PrioritySeverityDto, cached: PriorityRecord | undefined): PriorityRecord {
    const base = cached ?? blankPriority();
    return {
      ...base,
      name: dto.name,
      kind: dto.type === 'Severity' ? 'Severity' : 'Priority',
      color: dto.color?.toLowerCase() ?? base.color,
      responseTime: dto.responseTime ?? '',
      description: dto.description ?? ''
    };
  }

  protected toRequest(r: PriorityRecord) {
    return { name: r.name, code: r.code, type: r.kind, color: r.color, responseTime: r.responseTime, description: r.description };
  }
}
