import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-maintenance-task-template',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class TaskTemplatePage {
  readonly config: MasterTableConfig = {
    key: 'maintenance/task-template',
    title: 'Task Template',
    singular: 'Task Template',
    codePrefix: 'TT',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'workType', label: 'Work Type', type: 'select', options: ['Inspection', 'Lubrication', 'Repair', 'Replacement'] },
      { key: 'hours', label: 'Estimated Hours', type: 'number' },
      { key: 'skill', label: 'Required Skill', type: 'select', options: ['Electrical', 'Mechanical', 'HVAC', 'Plumbing'] },
      { key: 'steps', label: 'Steps', type: 'textarea', hideInTable: true }
    ],
    rows: [
      { code: 'TT-0001', name: 'DG monthly check', workType: 'Inspection', hours: 1.5, skill: 'Electrical', steps: '', status: 'Active' },
      { code: 'TT-0002', name: 'AHU filter change', workType: 'Replacement', hours: 1, skill: 'HVAC', steps: '', status: 'Active' }
    ]
  };
}
