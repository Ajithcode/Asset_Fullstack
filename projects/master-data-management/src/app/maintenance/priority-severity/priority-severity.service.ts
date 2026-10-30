import { Injectable } from '@angular/core';
import { MdmAudit } from '../../shared/mdm-api';
import { MasterApiStore, stampOf } from '../master-api-store';
import { LevelKind, PriorityRecord } from './priority-severity.model';

interface PrioritySeverityDto extends MdmAudit {
  name: string;
  code: string;
  type: string;
  color: string | null;
  responseTime: string | null;
  description: string | null;
  appliesTo: string[];
  escalateAfterHours: number | null;
  notifyManager: boolean;
}

/**
 * Priorities and severities (/api/master-data/priority-severities). Names repeat across the two kinds
 * (Critical), so uniqueness is checked per kind; work types refer to a priority by name.
 */
@Injectable({ providedIn: 'root' })
export class PriorityService extends MasterApiStore<PriorityRecord, PrioritySeverityDto> {
  protected readonly resource = 'priority-severities';

  constructor() {
    super();
  }

  isNameTakenIn(kind: LevelKind, name: string, exceptCode: string | null): boolean {
    const wanted = name.trim().toLowerCase();
    return this.records.some((r) => r.kind === kind && r.name.toLowerCase() === wanted && r.code !== exceptCode);
  }

  activePriorityNames(keep = ''): string[] {
    return this.records.filter((r) => r.kind === 'Priority' && (r.status === 'Active' || r.name === keep)).map((r) => r.name);
  }

  protected toRecord(dto: PrioritySeverityDto): PriorityRecord {
    return {
      code: dto.code,
      name: dto.name,
      kind: dto.type === 'Severity' ? 'Severity' : 'Priority',
      color: (dto.color ?? '#94a3b8').toLowerCase(),
      responseTime: dto.responseTime ?? '',
      description: dto.description ?? '',
      status: dto.status === 'Inactive' ? 'Inactive' : 'Active',
      appliesTo: dto.appliesTo ?? [],
      escalateAfterHours: dto.escalateAfterHours ?? null,
      notifyManager: dto.notifyManager,
      createdOn: stampOf(dto.createdAt),
      updatedOn: stampOf(dto.updatedAt ?? dto.createdAt)
    };
  }

  protected toRequest(r: PriorityRecord): object {
    return {
      name: r.name,
      code: r.code,
      type: r.kind,
      color: r.color,
      responseTime: r.responseTime,
      description: r.description,
      appliesTo: r.appliesTo,
      escalateAfterHours: r.escalateAfterHours,
      notifyManager: r.notifyManager,
      status: r.status
    };
  }
}
