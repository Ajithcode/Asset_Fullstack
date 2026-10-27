import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-maintenance-priority-severity',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class PrioritySeverityPage {
  readonly config: MasterTableConfig = {
    key: 'maintenance/priority-severity',
    title: 'Priority / Severity',
    singular: 'Priority / Severity',
    codePrefix: 'PRI',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'kind', label: 'Kind', type: 'select', options: ['Priority', 'Severity'] },
      { key: 'rank', label: 'Rank', type: 'number' },
      { key: 'slaHours', label: 'SLA (hrs)', type: 'number' },
      { key: 'color', label: 'Color', type: 'color' }
    ],
    rows: [
      { code: 'PRI-0001', name: 'P1 - Urgent', kind: 'Priority', rank: 1, slaHours: 4, color: '#dc2626', status: 'Active' },
      { code: 'PRI-0002', name: 'P2 - High', kind: 'Priority', rank: 2, slaHours: 24, color: '#f97316', status: 'Active' },
      { code: 'PRI-0003', name: 'P3 - Normal', kind: 'Priority', rank: 3, slaHours: 72, color: '#2563eb', status: 'Active' },
      { code: 'PRI-0004', name: 'S1 - Major', kind: 'Severity', rank: 1, slaHours: 8, color: '#7c3aed', status: 'Active' }
    ]
  };
}
