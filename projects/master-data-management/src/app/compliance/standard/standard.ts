import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-compliance-standard',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class StandardPage {
  readonly config: MasterTableConfig = {
    key: 'compliance/standard',
    title: 'Standard',
    singular: 'Standard',
    codePrefix: 'STD',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'number', label: 'Standard No.' },
      { key: 'body', label: 'Body', type: 'select', options: ['ISO', 'IEC', 'BIS', 'NFPA', 'ASHRAE'] },
      { key: 'version', label: 'Version' }
    ],
    rows: [
      { code: 'STD-0001', name: 'Quality management systems', number: 'ISO 9001', body: 'ISO', version: '2015', status: 'Active' },
      { code: 'STD-0002', name: 'Asset management', number: 'ISO 55001', body: 'ISO', version: '2024', status: 'Active' },
      { code: 'STD-0003', name: 'Fire alarm code', number: 'NFPA 72', body: 'NFPA', version: '2022', status: 'Active' }
    ]
  };
}
