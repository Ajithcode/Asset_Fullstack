import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-vendor-amc-vendor',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class AmcVendorPage {
  readonly config: MasterTableConfig = {
    key: 'vendor/amc-vendor',
    title: 'AMC Vendor',
    singular: 'AMC Vendor',
    codePrefix: 'AMC',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'contractNo', label: 'Contract No.' },
      { key: 'startDate', label: 'Start Date', type: 'date' },
      { key: 'endDate', label: 'End Date', type: 'date' },
      { key: 'value', label: 'Contract Value', type: 'number' }
    ],
    rows: [
      { code: 'AMC-0001', name: 'Cummins Sales & Service', contractNo: 'AMC/2026/014', startDate: '2026-04-01', endDate: '2027-03-31', value: 240000, status: 'Active' },
      { code: 'AMC-0002', name: 'Carrier Aircon', contractNo: 'AMC/2025/188', startDate: '2025-10-01', endDate: '2026-09-30', value: 180000, status: 'Inactive' }
    ]
  };
}
