import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-people-shift',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class ShiftPage {
  readonly config: MasterTableConfig = {
    key: 'people/shift',
    title: 'Shift',
    singular: 'Shift',
    codePrefix: 'SFT',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'start', label: 'Start Time', type: 'time' },
      { key: 'end', label: 'End Time', type: 'time' }
    ],
    rows: [
      { code: 'SFT-0001', name: 'General', start: '09:00', end: '18:00', status: 'Active' },
      { code: 'SFT-0002', name: 'Morning', start: '06:00', end: '14:00', status: 'Active' },
      { code: 'SFT-0003', name: 'Night', start: '22:00', end: '06:00', status: 'Active' }
    ]
  };
}
