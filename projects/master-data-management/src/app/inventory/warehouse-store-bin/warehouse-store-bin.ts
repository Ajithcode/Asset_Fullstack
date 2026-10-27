import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-inventory-warehouse-store-bin',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class WarehouseStoreBinPage {
  readonly config: MasterTableConfig = {
    key: 'inventory/warehouse-store-bin',
    title: 'Warehouse / Store / Bin',
    singular: 'Warehouse / Store / Bin',
    codePrefix: 'WH',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'level', label: 'Level', type: 'select', options: ['Warehouse', 'Store', 'Bin'] },
      { key: 'parent', label: 'Parent' },
      { key: 'site', label: 'Site', type: 'select', options: ['Chennai HQ', 'Bengaluru Plant', 'Pune Warehouse'] }
    ],
    rows: [
      { code: 'WH-0001', name: 'Central Warehouse', level: 'Warehouse', parent: '-', site: 'Pune Warehouse', status: 'Active' },
      { code: 'WH-0002', name: 'Spares Store', level: 'Store', parent: 'Central Warehouse', site: 'Pune Warehouse', status: 'Active' },
      { code: 'WH-0003', name: 'Bin A-01', level: 'Bin', parent: 'Spares Store', site: 'Pune Warehouse', status: 'Active' }
    ]
  };
}
