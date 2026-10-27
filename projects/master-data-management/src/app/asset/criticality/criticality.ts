import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-asset-criticality',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class CriticalityPage {
  readonly config: MasterTableConfig = {
    key: 'asset/criticality',
    title: 'Criticality',
    singular: 'Criticality',
    codePrefix: 'CRT',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'level', label: 'Level', type: 'number' },
      { key: 'responseHours', label: 'Response Time (hrs)', type: 'number' }
    ],
    rows: [
      { code: 'CRT-0001', name: 'Critical', level: 1, responseHours: 2, status: 'Active' },
      { code: 'CRT-0002', name: 'High', level: 2, responseHours: 8, status: 'Active' },
      { code: 'CRT-0003', name: 'Medium', level: 3, responseHours: 24, status: 'Active' },
      { code: 'CRT-0004', name: 'Low', level: 4, responseHours: 72, status: 'Active' }
    ]
  };
}
