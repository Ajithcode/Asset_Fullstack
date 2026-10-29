import { Injectable } from '@angular/core';
import { CodeStore } from '../../shared/code-store';
import { initialChecklists } from './checklist.data';
import { ChecklistRecord } from './checklist.model';

/** Checklists; work types refer to one by code. */
@Injectable({ providedIn: 'root' })
export class ChecklistService extends CodeStore<ChecklistRecord> {
  constructor() {
    super(initialChecklists());
  }
}
