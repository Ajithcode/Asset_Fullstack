import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-inventory-uom',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class UomPage {
  readonly config: MasterTableConfig = {
    key: 'inventory/uom',
    title: 'UOM',
    singular: 'UOM',
    codePrefix: 'UOM',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'symbol', label: 'Symbol' },
      { key: 'kind', label: 'Type', type: 'select', options: ['Count', 'Weight', 'Volume', 'Length'] }
    ],
    rows: [
      { code: 'UOM-0001', name: 'Numbers', symbol: 'Nos', kind: 'Count', status: 'Active' },
      { code: 'UOM-0002', name: 'Kilogram', symbol: 'Kg', kind: 'Weight', status: 'Active' },
      { code: 'UOM-0003', name: 'Litre', symbol: 'L', kind: 'Volume', status: 'Active' },
      { code: 'UOM-0004', name: 'Metre', symbol: 'm', kind: 'Length', status: 'Active' }
    ]
  };
}
