import { Injectable } from '@angular/core';
import { CodeStore } from '../../shared/code-store';
import { initialFailureCodes } from './failure-code.data';
import { FailureCodeRecord } from './failure-code.model';

@Injectable({ providedIn: 'root' })
export class FailureCodeService extends CodeStore<FailureCodeRecord> {
  constructor() {
    super(initialFailureCodes());
  }
}
