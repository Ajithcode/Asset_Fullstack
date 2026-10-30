import { Injectable } from '@angular/core';
import { MdmAudit } from '../../shared/mdm-api';
import { MasterApiStore, stampOf } from '../master-api-store';
import { FAILURE_SEVERITIES } from './failure-code.data';
import { FailureCodeRecord, FailureSeverity } from './failure-code.model';

interface FailureCodeDto extends MdmAudit {
  failureCodeName: string;
  code: string;
  category: string;
  severity: string;
  description: string | null;
  assetCategories: string[];
  causes: string[];
  actions: string[];
}

/** Failure codes (/api/master-data/failure-codes). */
@Injectable({ providedIn: 'root' })
export class FailureCodeService extends MasterApiStore<FailureCodeRecord, FailureCodeDto> {
  protected readonly resource = 'failure-codes';

  constructor() {
    super();
  }

  protected toRecord(dto: FailureCodeDto): FailureCodeRecord {
    return {
      code: dto.code,
      name: dto.failureCodeName,
      category: dto.category,
      description: dto.description ?? '',
      severity: FAILURE_SEVERITIES.includes(dto.severity as FailureSeverity) ? (dto.severity as FailureSeverity) : 'Medium',
      status: dto.status === 'Inactive' ? 'Inactive' : 'Active',
      assetCategories: dto.assetCategories ?? [],
      causes: dto.causes ?? [],
      actions: dto.actions ?? [],
      createdOn: stampOf(dto.createdAt),
      updatedOn: stampOf(dto.updatedAt ?? dto.createdAt)
    };
  }

  protected toRequest(r: FailureCodeRecord): object {
    return {
      failureCodeName: r.name,
      code: r.code,
      category: r.category,
      severity: r.severity,
      description: r.description,
      assetCategories: r.assetCategories,
      causes: r.causes,
      actions: r.actions,
      status: r.status
    };
  }
}
