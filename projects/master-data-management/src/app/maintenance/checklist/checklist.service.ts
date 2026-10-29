import { Injectable } from '@angular/core';
import { ApiCodeStore, MasterDto } from '../../shared/api-code-store';
import { blankChecklist } from './checklist.data';
import { ChecklistRecord, ResponseType } from './checklist.model';

interface StepDto {
  name: string;
  description: string | null;
  required: boolean | null;
  responseType: string | null;
}

interface ChecklistDto extends MasterDto {
  checklistName: string;
  checklistType: string;
  category: string;
  description: string | null;
  items: StepDto[];
}

/**
 * Checklists from {projectApiUrl}api/master-data/checklists; work types refer to one by code.
 * Each item's question is stored as the step name.
 */
@Injectable({ providedIn: 'root' })
export class ChecklistService extends ApiCodeStore<ChecklistRecord, ChecklistDto> {
  constructor() {
    super('checklists');
    this.load();
  }

  protected toRecord(dto: ChecklistDto, cached: ChecklistRecord | undefined): ChecklistRecord {
    return {
      ...(cached ?? blankChecklist()),
      name: dto.checklistName,
      checklistType: dto.checklistType,
      category: dto.category,
      description: dto.description ?? '',
      items: dto.items.map((i) => ({ question: i.name, response: (i.responseType ?? 'Yes / No') as ResponseType, required: i.required ?? true }))
    };
  }

  protected toRequest(r: ChecklistRecord) {
    return {
      checklistName: r.name,
      code: r.code,
      checklistType: r.checklistType,
      category: r.category,
      description: r.description,
      items: r.items.map((i) => ({ name: i.question, responseType: i.response, required: i.required }))
    };
  }
}
