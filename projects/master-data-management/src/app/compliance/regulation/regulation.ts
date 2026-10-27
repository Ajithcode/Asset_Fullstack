import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-compliance-regulation',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class RegulationPage {
  readonly config: MasterTableConfig = {
    key: 'compliance/regulation',
    title: 'Regulation',
    singular: 'Regulation',
    codePrefix: 'REG',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'authority', label: 'Authority' },
      { key: 'jurisdiction', label: 'Jurisdiction' },
      { key: 'effectiveDate', label: 'Effective Date', type: 'date' }
    ],
    rows: [
      { code: 'REG-0001', name: 'Factories Act 1948', authority: 'Ministry of Labour', jurisdiction: 'India', effectiveDate: '1949-04-01', status: 'Active' },
      { code: 'REG-0002', name: 'Central Electricity Authority Regulations', authority: 'CEA', jurisdiction: 'India', effectiveDate: '2023-06-08', status: 'Active' }
    ]
  };
}
