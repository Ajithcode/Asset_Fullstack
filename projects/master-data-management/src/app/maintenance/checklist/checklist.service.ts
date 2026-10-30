import { Injectable } from '@angular/core';
import { MdmAudit } from '../../shared/mdm-api';
import { MasterApiStore, stampOf } from '../master-api-store';
import { RESPONSE_TYPES } from './checklist.data';
import { ChecklistRecord, ResponseType } from './checklist.model';

interface ChecklistDto extends MdmAudit {
  checklistName: string;
  code: string;
  checklistType: string;
  category: string;
  description: string | null;
  items: { name: string; description: string | null; responseType: string | null; isRequired: boolean }[];
}

/** Checklists (/api/master-data/checklists); work types refer to one by code. */
@Injectable({ providedIn: 'root' })
export class ChecklistService extends MasterApiStore<ChecklistRecord, ChecklistDto> {
  protected readonly resource = 'checklists';

  constructor() {
    super();
  }

  protected toRecord(dto: ChecklistDto): ChecklistRecord {
    return {
      code: dto.code,
      name: dto.checklistName,
      checklistType: dto.checklistType,
      category: dto.category,
      description: dto.description ?? '',
      status: dto.status === 'Inactive' ? 'Inactive' : 'Active',
      items: (dto.items ?? []).map((i) => ({
        question: i.name,
        response: RESPONSE_TYPES.includes(i.responseType as ResponseType) ? (i.responseType as ResponseType) : 'Pass / Fail',
        required: i.isRequired
      })),
      createdOn: stampOf(dto.createdAt),
      updatedOn: stampOf(dto.updatedAt ?? dto.createdAt)
    };
  }

  protected toRequest(r: ChecklistRecord): object {
    return {
      checklistName: r.name,
      code: r.code,
      checklistType: r.checklistType,
      category: r.category,
      description: r.description,
      items: r.items.map((i) => ({ name: i.question, responseType: i.response, isRequired: i.required })),
      status: r.status
    };
  }
}
