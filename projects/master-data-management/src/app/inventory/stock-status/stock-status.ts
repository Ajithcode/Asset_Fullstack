import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-inventory-stock-status',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class StockStatusPage {
  readonly config: MasterTableConfig = {
    key: 'inventory/stock-status',
    title: 'Stock Status',
    singular: 'Stock Status',
    codePrefix: 'SS',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'usable', label: 'Usable', type: 'boolean' },
      { key: 'color', label: 'Color', type: 'color' }
    ],
    rows: [
      { code: 'SS-0001', name: 'Available', usable: true, color: '#16a34a', status: 'Active' },
      { code: 'SS-0002', name: 'Reserved', usable: false, color: '#2563eb', status: 'Active' },
      { code: 'SS-0003', name: 'Quarantine', usable: false, color: '#f97316', status: 'Active' },
      { code: 'SS-0004', name: 'Damaged', usable: false, color: '#dc2626', status: 'Active' }
    ]
  };
}
