import { Injectable } from '@angular/core';
import { ApiCodeStore, MasterDto } from '../../shared/api-code-store';
import { blankFailureCode } from './failure-code.data';
import { FailureCodeRecord, FailureSeverity } from './failure-code.model';

interface FailureCodeDto extends MasterDto {
  failureCodeName: string;
  category: string;
  severity: string;
  description: string | null;
}

/**
 * Failure codes from {projectApiUrl}api/master-data/failure-codes. Asset categories, causes and
 * actions are UI-only for now.
 */
@Injectable({ providedIn: 'root' })
export class FailureCodeService extends ApiCodeStore<FailureCodeRecord, FailureCodeDto> {
  constructor() {
    super('failure-codes');
    this.load();
  }

  protected toRecord(dto: FailureCodeDto, cached: FailureCodeRecord | undefined): FailureCodeRecord {
    return {
      ...(cached ?? blankFailureCode()),
      name: dto.failureCodeName,
      category: dto.category,
      severity: dto.severity as FailureSeverity,
      description: dto.description ?? ''
    };
  }

  protected toRequest(r: FailureCodeRecord) {
    return { failureCodeName: r.name, code: r.code, category: r.category, severity: r.severity, description: r.description };
  }
}
