import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-system-configuration-update-source',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class UpdateSourcePage {
  readonly config: MasterTableConfig = {
    key: 'system-configuration/update-source',
    title: 'Update Source',
    singular: 'Update Source',
    codePrefix: 'SRC',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'sourceType', label: 'Source Type', type: 'select', options: ['Manual', 'IoT Gateway', 'API', 'File Import', 'Mobile App'] },
      { key: 'priority', label: 'Priority', type: 'number' }
    ],
    rows: [
      { code: 'SRC-0001', name: 'Manual entry', sourceType: 'Manual', priority: 3, status: 'Active' },
      { code: 'SRC-0002', name: 'RFID / BLE gateway', sourceType: 'IoT Gateway', priority: 1, status: 'Active' },
      { code: 'SRC-0003', name: 'ERP sync', sourceType: 'API', priority: 2, status: 'Active' },
      { code: 'SRC-0004', name: 'Excel import', sourceType: 'File Import', priority: 4, status: 'Active' }
    ]
  };
}
